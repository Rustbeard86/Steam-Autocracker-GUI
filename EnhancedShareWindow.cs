using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using APPID;
using APPID.Properties;
using APPID.Services;
using APPID.Services.Interfaces;
using SAC_GUI;
using SteamAutocrackGUI;
using Timer = System.Windows.Forms.Timer;

namespace SteamAppIdIdentifier;

/// <summary>
///     Custom progress bar with neon blue gradient styling for EnhancedShareWindow
/// </summary>
public class ShareNeonProgressBar : ProgressBar
{
    public ShareNeonProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;

        // Dark background
        using (var bgBrush = new SolidBrush(Color.FromArgb(10, 12, 20)))
        {
            e.Graphics.FillRectangle(bgBrush, rect);
        }

        // Border
        using (var borderPen = new Pen(Color.FromArgb(40, 50, 70)))
        {
            e.Graphics.DrawRectangle(borderPen, 0, 0, rect.Width - 1, rect.Height - 1);
        }

        // Progress fill with neon blue gradient
        if (Value > 0)
        {
            int fillWidth = (int)((rect.Width - 4) * ((double)Value / Maximum));
            if (fillWidth > 0)
            {
                using (var brush = new LinearGradientBrush(
                           new Rectangle(2, 2, fillWidth, rect.Height - 4),
                           Color.FromArgb(0, 150, 255), // Bright neon blue
                           Color.FromArgb(0, 100, 200), // Darker blue
                           LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, 2, 2, fillWidth, rect.Height - 4);
                }

                // Add glow effect on top
                using (var glowBrush = new SolidBrush(Color.FromArgb(40, 150, 220, 255)))
                {
                    e.Graphics.FillRectangle(glowBrush, 2, 2, fillWidth, (rect.Height - 4) / 3);
                }
            }
        }
    }
}

/// <summary>
///     Custom DataGridView that supports transparent background for acrylic effect
/// </summary>
public class TransparentDataGridView : DataGridView
{
    public TransparentDataGridView()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Opaque, false);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Don't paint background - let acrylic show through
    }
}

public partial class EnhancedShareWindow : Form
{
    // Cache info icon for cell painting
    private static Image _infoIcon;
    private static bool _infoIconLoaded;
    private readonly IFormattingService _formatting;

    // Service dependencies
    private readonly IBatchGameDataService _gameData;

    // Crack details tracking
    private readonly Dictionary<string, SteamAppId.CrackDetails> crackDetailsMap = new();

    private readonly Form parentForm;

    // Cancellation support for batch processing
    private CancellationTokenSource batchCancellationTokenSource;

    // Batch compression settings
    private string batchCompressionFormat = "ZIP";
    private string batchCompressionLevel = "0";
    private bool batchUsePassword;
    private Button btnCancelUpload;
    private bool cancelAllRemaining;
    private string currentProcessingGame;
    private bool gameSizeColumnSortedOnce;
    private Label lblUploadEta;
    private Label lblUploadGame;
    private Label lblUploadSize;
    private Label lblUploadSpeed;

    private Point mouseDownPoint = Point.Empty;
    private bool skipCurrentGame;

    // Toggle states for processing logic
    private bool toggleCrackOn;
    private bool toggleShareOn;
    private bool toggleZipOn;

    // Upload details panel controls
    private Panel uploadDetailsPanel;
    private ShareNeonProgressBar uploadProgressBar;

    public EnhancedShareWindow(Form parent, IBatchGameDataService gameData = null, IFormattingService formatting = null)
    {
        parentForm = parent;

        // Initialize services - use provided or create defaults
        _gameData = gameData ?? new BatchGameDataService(new FileSystemService());
        _formatting = formatting ?? new FormattingService();

        // Set dark background BEFORE InitializeComponent to prevent white flash
        BackColor = Color.FromArgb(5, 8, 20);

        // Enable double buffering to prevent white flash during load
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        InitializeComponent(); // Use the Designer.cs file instead!

        // Setup custom painting for settings button with proper icon scaling
        Image settingsIcon = null;
        try { settingsIcon = Resources.settings_icon; }
        catch { }

        btnSettings.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var btn = (Button)s;
            var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);

            // Background
            Color bgColor = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position))
                ? Color.FromArgb(50, 50, 55)
                : Color.FromArgb(38, 38, 42);
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, rect);
            }

            // Draw icon centered with proper aspect ratio
            if (settingsIcon != null)
            {
                int padding = 6;
                int availableW = btn.Width - padding * 2;
                int availableH = btn.Height - padding * 2;

                float scale = Math.Min((float)availableW / settingsIcon.Width, (float)availableH / settingsIcon.Height);
                int drawW = (int)(settingsIcon.Width * scale);
                int drawH = (int)(settingsIcon.Height * scale);

                int drawX = padding + (availableW - drawW) / 2;
                int drawY = padding + (availableH - drawH) / 2;

                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(settingsIcon, drawX, drawY, drawW, drawH);
            }
        };
        btnSettings.MouseEnter += (s, e) => btnSettings.Invalidate();
        btnSettings.MouseLeave += (s, e) => btnSettings.Invalidate();

        // Enable double buffering on the grid to prevent flicker
        typeof(DataGridView).InvokeMember("DoubleBuffered",
            BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
            null, gamesGrid, [true]);

        // Setup modern progress bar style
        SetupModernProgressBar();

        // Add custom sort for GameSize column
        gamesGrid.SortCompare += GamesGrid_SortCompare;
        gamesGrid.ColumnHeaderMouseClick += GamesGrid_ColumnHeaderMouseClick;

        // Make main panel draggable (empty space drags window)
        mainPanel.MouseDown += TitleBar_MouseDown;

        // Make data grid draggable but exclude column resizing
        gamesGrid.MouseDown += DataGrid_MouseDown;

        // Start invisible to prevent white flash
        Opacity = 0;

        // Apply acrylic blur and rounded corners
        Load += (s, e) =>
        {
            ApplyAcrylicEffect();
            // Center over parent when loaded
            CenterOverParent();
        };

        // Show window after everything is rendered - use BeginInvoke to ensure paint is complete
        Shown += (s, e) =>
        {
            BeginInvoke(() => Opacity = 0.95);
        };

        // ESC key closes form and returns to caller
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    // Force Windows to composite the entire form before displaying (prevents white flash)
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }

    private void GamesGrid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
    {
        // Custom paint dark mode checkboxes for SelectGame column
        if (e.ColumnIndex >= 0 && gamesGrid.Columns[e.ColumnIndex].Name == "SelectGame" && e.RowIndex >= 0)
        {
            e.PaintBackground(e.ClipBounds, true);

            // Draw dark checkbox
            int checkSize = 16;
            int x = e.CellBounds.X + (e.CellBounds.Width - checkSize) / 2;
            int y = e.CellBounds.Y + (e.CellBounds.Height - checkSize) / 2;
            var checkRect = new Rectangle(x, y, checkSize, checkSize);

            // Dark background with border
            using (var brush = new SolidBrush(Color.FromArgb(30, 30, 35)))
            {
                e.Graphics.FillRectangle(brush, checkRect);
            }

            using (var pen = new Pen(Color.FromArgb(80, 80, 90), 1))
            {
                e.Graphics.DrawRectangle(pen, checkRect);
            }

            // Draw checkmark if checked
            bool isChecked = e.Value is true;
            if (isChecked)
            {
                using var pen = new Pen(Color.FromArgb(100, 200, 255), 2);
                // Draw checkmark
                e.Graphics.DrawLine(pen, x + 3, y + 8, x + 6, y + 12);
                e.Graphics.DrawLine(pen, x + 6, y + 12, x + 13, y + 4);
            }

            e.Handled = true;
        }

        // Custom paint for Details/Info button column - only show icon if there's data
        if (e.ColumnIndex >= 0 && gamesGrid.Columns[e.ColumnIndex].Name == "Details" && e.RowIndex >= 0)
        {
            // Check if we have details for this row
            var row = gamesGrid.Rows[e.RowIndex];
            string installPath = row.Cells["InstallPath"].Value?.ToString();
            bool hasDetails = !string.IsNullOrEmpty(installPath) &&
                              crackDetailsMap.ContainsKey(installPath) &&
                              crackDetailsMap[installPath].HasDetails;

            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

            // Only draw icon if we have details
            if (hasDetails)
            {
                // Load info icon once
                if (!_infoIconLoaded)
                {
                    try { _infoIcon = Resources.info_icon; }
                    catch { }

                    _infoIconLoaded = true;
                }

                // Draw icon centered in cell
                if (_infoIcon != null)
                {
                    int iconSize = Math.Min(e.CellBounds.Width - 8, e.CellBounds.Height - 8);
                    iconSize = Math.Min(iconSize, 20); // Cap at 20px
                    int iconX = e.CellBounds.X + (e.CellBounds.Width - iconSize) / 2;
                    int iconY = e.CellBounds.Y + (e.CellBounds.Height - iconSize) / 2;
                    e.Graphics.DrawImage(_infoIcon, iconX, iconY, iconSize, iconSize);
                }
                else
                {
                    // Fallback to text if icon not available
                    using var textBrush = new SolidBrush(Color.FromArgb(150, 200, 255));
                    using var font = new Font("Segoe UI", 8);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString("ⓘ", font, textBrush, e.CellBounds, sf);
                }
            }

            e.Handled = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+S opens compression settings
        if (keyData == (Keys.Control | Keys.S))
        {
            OpenBatchCompressionSettings();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void GamesGrid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        // SelectGame column header click = toggle select all/none
        if (gamesGrid.Columns[e.ColumnIndex].Name == "SelectGame")
        {
            // Check if all are selected
            bool allSelected = true;
            foreach (DataGridViewRow row in gamesGrid.Rows)
            {
                if (row.Cells["SelectGame"].Value is not true)
                {
                    allSelected = false;
                    break;
                }
            }

            // Toggle: if all selected, unselect all; otherwise select all
            bool newValue = !allSelected;
            foreach (DataGridViewRow row in gamesGrid.Rows)
            {
                row.Cells["SelectGame"].Value = newValue;
            }

            UpdateSelectedCount();
            return;
        }

        // On first click of GameSize column, sort descending (biggest to smallest)
        if (gamesGrid.Columns[e.ColumnIndex].Name == "GameSize" && !gameSizeColumnSortedOnce)
        {
            gameSizeColumnSortedOnce = true;
            gamesGrid.Sort(gamesGrid.Columns[e.ColumnIndex], ListSortDirection.Descending);
        }
    }

    private void GamesGrid_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
    {
        // Sort GameSize column by actual byte value stored in Tag
        if (e.Column.Name == "GameSize")
        {
            long size1 = gamesGrid.Rows[e.RowIndex1].Cells["GameSize"].Tag as long? ?? 0;
            long size2 = gamesGrid.Rows[e.RowIndex2].Cells["GameSize"].Tag as long? ?? 0;

            e.SortResult = size1.CompareTo(size2);
            e.Handled = true;
        }
        // Sort LastUpdated column by actual timestamp stored in Tag
        else if (e.Column.Name == "LastUpdated")
        {
            long time1 = gamesGrid.Rows[e.RowIndex1].Cells["LastUpdated"].Tag as long? ?? 0;
            long time2 = gamesGrid.Rows[e.RowIndex2].Cells["LastUpdated"].Tag as long? ?? 0;

            e.SortResult = time1.CompareTo(time2);
            e.Handled = true;
        }
    }

    private void CenterOverParent()
    {
        if (parentForm is { IsHandleCreated: true })
        {
            int x = parentForm.Left + (parentForm.Width - Width) / 2;
            int y = parentForm.Top + (parentForm.Height - Height) / 2;

            // Clamp to screen bounds
            var screen = Screen.FromControl(parentForm).WorkingArea;
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - Width));
            y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - Height));

            Location = new Point(x, y);
        }
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void ApplyAcrylicEffect()
    {
        AcrylicHelper.ApplyAcrylic(this, true, true);
    }

    private void SetupModernProgressBar()
    {
        progressBar.Paint += (s, e) =>
        {
            Rectangle rec = e.ClipRectangle;
            rec.Width = (int)(rec.Width * ((double)progressBar.Value / progressBar.Maximum)) - 4;
            if (ProgressBarRenderer.IsSupported)
            {
                rec.Height = rec.Height - 4;
            }

            // Draw transparent/dark background
            e.Graphics.Clear(Color.Transparent);

            // Draw blue gradient progress bar
            rec.Height = rec.Height - 4;
            if (rec.Width > 0)
            {
                using var brush = new LinearGradientBrush(
                    new Point(0, 0),
                    new Point(rec.Width, 0),
                    Color.FromArgb(0, 120, 215), // Light blue
                    Color.FromArgb(0, 90, 180));
                e.Graphics.FillRectangle(brush, 2, 2, rec.Width, rec.Height);
            }
        };
    }

    private async Task LoadGames()
    {
        // Update header
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                if (Controls.Find("lblHeader", true).FirstOrDefault() is Label lblHeader)
                {
                    lblHeader.Text = "Your Steam Library";
                    lblHeader.ForeColor = Color.FromArgb(100, 200, 255);
                }
            });
        }

        // Scan Steam libraries on background thread to avoid blocking UI
        var games = await Task.Run(ScanSteamLibraries);

        // Load shared games data
        var sharedGames = LoadSharedGamesData();
        Debug.WriteLine($"[SHARED GAMES] Loaded {sharedGames.Count} entries");
        foreach (var kvp in sharedGames)
        {
            Debug.WriteLine($"[SHARED GAMES]   {kvp.Key} = {kvp.Value}");
        }

        // Add to grid with placeholders, calculate sizes async
        foreach (var game in games)
        {
            // Quick pre-check: skip games without exe files (don't add to grid at all)
            if (!Directory.Exists(game.InstallDir))
            {
                continue;
            }

            try
            {
                bool hasExe = Directory.EnumerateFiles(game.InstallDir, "*.exe", SearchOption.AllDirectories).Any();
                if (!hasExe)
                {
                    continue;
                }
            }
            catch { continue; }

            var row = gamesGrid.Rows[gamesGrid.Rows.Add()];
            row.Cells["GameName"].Value = game.Name;
            row.Cells["BuildID"].Value = game.BuildId;
            row.Cells["AppID"].Value = game.AppId;
            row.Cells["InstallPath"].Value = game.InstallDir;
            row.Cells["GameSize"].Value = "...";

            // Format and display last updated date
            if (game.LastUpdated > 0)
            {
                DateTime lastUpdatedDate = DateTimeOffset.FromUnixTimeSeconds(game.LastUpdated).LocalDateTime;
                row.Cells["LastUpdated"].Value = lastUpdatedDate.ToString("yyyy-MM-dd HH:mm");
                row.Cells["LastUpdated"].Tag = game.LastUpdated; // Store timestamp for sorting
            }
            else
            {
                row.Cells["LastUpdated"].Value = "Unknown";
            }

            // Check if we've shared this game before and update button text
            string key = $"{game.AppId}_{game.BuildId}";
            Debug.WriteLine($"[SHARED GAMES] Checking key: {key}");
            if (sharedGames.TryGetValue(key, out string? sharedData))
            {
                var sharedTypes = sharedData.Split(',').ToList();
                Debug.WriteLine($"[SHARED GAMES] Found types for {key}: {string.Join(", ", sharedTypes)}");

                if (sharedTypes.Contains("cracked_only"))
                {
                    row.Cells["CrackOnly"].Value = "✅ Cracked!";
                    row.Cells["CrackOnly"].Style.BackColor = Color.FromArgb(60, 0, 60);
                }
                else
                {
                    row.Cells["CrackOnly"].Value = "⚡ Crack";
                }

                if (sharedTypes.Contains("clean"))
                {
                    row.Cells["ShareClean"].Value = "✅ Shared";
                    row.Cells["ShareClean"].Style.BackColor = Color.FromArgb(0, 60, 0);
                }
                else
                {
                    row.Cells["ShareClean"].Value = "📦 Share";
                }

                if (sharedTypes.Contains("cracked"))
                {
                    row.Cells["ShareCracked"].Value = "✅ Shared";
                    row.Cells["ShareCracked"].Style.BackColor = Color.FromArgb(0, 60, 0);
                }
                else
                {
                    row.Cells["ShareCracked"].Value = "🎮 Share";
                }
            }
            else
            {
                // Default button texts
                row.Cells["CrackOnly"].Value = "⚡ Crack";
                row.Cells["ShareClean"].Value = "📦 Share";
                row.Cells["ShareCracked"].Value = "🎮 Share";
            }

            // Calculate size asynchronously for each game
            int rowIndex = row.Index;
            _ = Task.Run(() =>
            {
                long dirSize = 0;
                try
                {
                    if (Directory.Exists(game.InstallDir))
                    {
                        dirSize = new DirectoryInfo(game.InstallDir)
                            .EnumerateFiles("*", SearchOption.AllDirectories)
                            .Sum(file => file.Length);
                    }
                }
                catch { }

                // Check if directory contains any exe files
                bool hasExe = false;
                try
                {
                    hasExe = Directory.EnumerateFiles(game.InstallDir, "*.exe", SearchOption.AllDirectories).Any();
                }
                catch { }

                // Update UI on main thread
                Invoke(() =>
                {
                    if (rowIndex < gamesGrid.Rows.Count)
                    {
                        // Hide 0B games or games without exe from the list entirely
                        if (dirSize == 0 || !hasExe)
                        {
                            gamesGrid.Rows[rowIndex].Visible = false;
                            return;
                        }

                        gamesGrid.Rows[rowIndex].Cells["GameSize"].Value = _gameData.FormatFileSize(dirSize);
                        gamesGrid.Rows[rowIndex].Cells["GameSize"].Tag = dirSize; // Store actual bytes for sorting
                    }
                });
            });
        }
    }


    private async void GamesGrid_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        var row = gamesGrid.Rows[e.RowIndex];
        var colName = gamesGrid.Columns[e.ColumnIndex].Name;

        // Handle SelectGame checkbox
        if (colName == "SelectGame")
        {
            var cell = row.Cells[e.ColumnIndex];
            bool currentValue = cell.Value is true;
            cell.Value = !currentValue;
            UpdateSelectedCount();
            return;
        }

        // Count how many games are checked
        int selectedCount = 0;
        foreach (DataGridViewRow r in gamesGrid.Rows)
        {
            if (r.Cells["SelectGame"].Value is true)
            {
                selectedCount++;
            }
        }

        var gameName = row.Cells["GameName"].Value?.ToString();
        var appId = row.Cells["AppID"].Value?.ToString();
        var installPath = row.Cells["InstallPath"].Value?.ToString();

        // Handle action button clicks
        if (colName == "CrackOnly")
        {
            if (selectedCount == 0)
            {
                // No games checked - crack this single game immediately
                await CrackOnlyGame(gameName, installPath, appId, row);
            }
            else
            {
                // Games are checked - set crack flag and blink Process
                if (!toggleCrackOn)
                {
                    toggleCrackOn = true;
                }

                _ = BlinkProcessButton();
            }

            return;
        }

        if (colName == "ShareClean")
        {
            if (selectedCount == 0)
            {
                // No games checked - share clean this single game immediately
                await ShareGame(gameName, installPath, appId, false, row);
            }
            else
            {
                // Games are checked - set zip+share flags and blink Process
                if (!toggleZipOn)
                {
                    toggleZipOn = true;
                }

                if (!toggleShareOn)
                {
                    toggleShareOn = true;
                }

                _ = BlinkProcessButton();
            }

            return;
        }

        if (colName == "ShareCracked")
        {
            if (selectedCount == 0)
            {
                // No games checked - share cracked this single game immediately
                await ShareGame(gameName, installPath, appId, true, row);
            }
            else
            {
                // Games are checked - set all flags and blink Process
                if (!toggleCrackOn)
                {
                    toggleCrackOn = true;
                }

                if (!toggleZipOn)
                {
                    toggleZipOn = true;
                }

                if (!toggleShareOn)
                {
                    toggleShareOn = true;
                }

                _ = BlinkProcessButton();
            }

            return;
        }

        // Handle Details button click
        if (colName == "Details")
        {
            if (!string.IsNullOrEmpty(installPath) && crackDetailsMap.ContainsKey(installPath))
            {
                ShowCrackDetails(crackDetailsMap[installPath]);
            }
            else
            {
                // Non-blocking status update instead of MessageBox
                lblStatus.Text = "No details yet - details populate after cracking/zipping/uploading";
                lblStatus.ForeColor = Color.FromArgb(255, 200, 100);
                // Reset status after 3 seconds
                var timer = new Timer { Interval = 3000 };
                timer.Tick += (ts, te) =>
                {
                    lblStatus.Text = "Ready";
                    lblStatus.ForeColor = Color.FromArgb(150, 150, 150);
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
        }
    }

    private async Task BlinkProcessButton()
    {
        var originalBg = btnProcessSelected.BackColor;
        var blinkColor = Color.FromArgb(80, 200, 80);

        for (int i = 0; i < 3; i++)
        {
            btnProcessSelected.BackColor = blinkColor;
            await Task.Delay(150);
            btnProcessSelected.BackColor = originalBg;
            await Task.Delay(150);
        }
    }

    private async Task CrackOnlyGame(string gameName, string installPath, string appId, DataGridViewRow row)
    {
        try
        {
            // Check if game path is valid
            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            {
                MessageBox.Show("Game installation path not found!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Clean up any existing crack artifacts first (uncrack before recracking)
            try
            {
                Debug.WriteLine($"[SINGLE CRACK] Cleaning up {gameName} before cracking");

                // Restore .bak files
                foreach (var bakFile in Directory.GetFiles(installPath, "*.dll.bak", SearchOption.AllDirectories))
                {
                    try
                    {
                        var orig = bakFile.Substring(0, bakFile.Length - 4);
                        if (File.Exists(orig))
                        {
                            File.Delete(orig);
                        }

                        File.Move(bakFile, orig);
                    }
                    catch { }
                }

                foreach (var bakFile in Directory.GetFiles(installPath, "*.exe.bak", SearchOption.AllDirectories))
                {
                    try
                    {
                        var orig = bakFile.Substring(0, bakFile.Length - 4);
                        if (File.Exists(orig))
                        {
                            File.Delete(orig);
                        }

                        File.Move(bakFile, orig);
                    }
                    catch { }
                }

                // Delete steam_settings directories
                foreach (var dir in Directory.GetDirectories(installPath, "steam_settings",
                             SearchOption.AllDirectories))
                {
                    try { Directory.Delete(dir, true); }
                    catch { }
                }

                // Delete _[ prefixed files, lobby_connect files, shortcuts
                foreach (var f in Directory.GetFiles(installPath, "_[*", SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(f); }
                    catch { }
                }

                foreach (var f in Directory.GetFiles(installPath, "_lobby_connect*", SearchOption.AllDirectories))
                {
                    try { File.Delete(f); }
                    catch { }
                }

                foreach (var f in Directory.GetFiles(installPath, "lobby_connect*", SearchOption.AllDirectories))
                {
                    try { File.Delete(f); }
                    catch { }
                }

                foreach (var f in Directory.GetFiles(installPath, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(f); }
                    catch { }
                }

                // Delete common crack artifacts
                string[] artifacts =
                [
                    "CreamAPI.dll", "cream_api.ini", "CreamLinux", "steam_api_o.dll", "steam_api64_o.dll",
                    "local_save.txt"
                ];
                foreach (var artifact in artifacts)
                {
                    foreach (var f in Directory.GetFiles(installPath, artifact, SearchOption.AllDirectories))
                    {
                        try { File.Delete(f); }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SINGLE CRACK] Cleanup error: {ex.Message}");
            }

            // Verify this is the main form
            if (parentForm is not SteamAppId mainForm)
            {
                MessageBox.Show("Cannot access cracking functionality.", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Update UI to show cracking in progress
            row.Cells["CrackOnly"].Value = "⚙️ Cracking...";
            row.Cells["CrackOnly"].Style.BackColor = Color.FromArgb(30, 30, 0); // Subtle yellow tint

            // Create translucent overlay
            var overlay = new Form
            {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.Black,
                Opacity = 0.7,
                ShowInTaskbar = false,
                TopMost = true,
                Location = Location,
                Size = Size
            };

            var statusLabel = new Label
            {
                Text = $"Cracking {gameName}...",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.Cyan,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            statusLabel.Location = new Point(
                (overlay.Width - statusLabel.Width) / 2,
                (overlay.Height - statusLabel.Height) / 2
            );
            overlay.Controls.Add(statusLabel);
            overlay.Show();

            try
            {
                // Set game directory and APPID for cracking
                mainForm.GameDirectory = installPath;
                SteamAppId.CurrentAppId = appId;

                // Perform the crack
                bool success = await mainForm.CrackAsync();

                overlay.Close();

                if (success)
                {
                    row.Cells["CrackOnly"].Value = "✅ Cracked!";
                    row.Cells["CrackOnly"].Style.BackColor = Color.FromArgb(60, 0, 60); // Purple tint for cracked

                    // Save that we cracked this game
                    SaveSharedGame(appId, row.Cells["BuildID"].Value?.ToString(), "cracked_only");

                    MessageBox.Show($"{gameName} has been cracked successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    row.Cells["CrackOnly"].Value = "⚡ Crack";
                    row.Cells["CrackOnly"].Style.BackColor = Color.FromArgb(8, 8, 12); // Reset to default
                    MessageBox.Show($"Failed to crack {gameName}. Check the main window for details.",
                        "Crack Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                overlay.Close();
                row.Cells["CrackOnly"].Value = "⚡ Crack";
                row.Cells["CrackOnly"].Style.BackColor = Color.FromArgb(8, 8, 12); // Reset to default

                Debug.WriteLine($"[CRACK-ONLY] Error: {ex}");
                MessageBox.Show($"Error cracking {gameName}:\n{ex.Message}",
                    "Crack Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            row.Cells["CrackOnly"].Value = "⚡ Crack";
        }
    }

    private async Task ShareGame(string gameName, string installPath, string appId, bool cracked, DataGridViewRow row)
    {
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
        {
            MessageBox.Show("Game installation path not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // If sharing clean files, restore all .bak files and clean up crack artifacts
        if (!cracked)
        {
            try
            {
                if (parentForm is SteamAppId parentFormTyped)
                {
                    // Restore .bak files
                    try
                    {
                        var bakFiles = Directory.GetFiles(installPath, "*.bak", SearchOption.AllDirectories);
                        if (bakFiles.Length > 0)
                        {
                            Debug.WriteLine(
                                $"[SHARE CLEAN] Found {bakFiles.Length} .bak files, restoring clean files...");
                            foreach (var bakFile in bakFiles)
                            {
                                try
                                {
                                    var originalFile = bakFile.Substring(0, bakFile.Length - 4); // Remove .bak
                                    if (File.Exists(originalFile))
                                    {
                                        File.Delete(originalFile);
                                    }

                                    File.Move(bakFile, originalFile);
                                    Debug.WriteLine($"[SHARE CLEAN] Restored {Path.GetFileName(bakFile)}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[SHARE CLEAN] Failed to restore {bakFile}: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SHARE CLEAN] Failed to search for .bak files: {ex.Message}");
                    }

                    // Delete steam_settings directories (recursively search for all instances)
                    try
                    {
                        var steamSettingsDirs =
                            Directory.GetDirectories(installPath, "steam_settings", SearchOption.AllDirectories);
                        foreach (var steamSettingsDir in steamSettingsDirs)
                        {
                            try
                            {
                                Directory.Delete(steamSettingsDir, true);
                                Debug.WriteLine($"[SHARE CLEAN] Deleted steam_settings directory: {steamSettingsDir}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(
                                    $"[SHARE CLEAN] Failed to delete steam_settings at {steamSettingsDir}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SHARE CLEAN] Failed to search for steam_settings directories: {ex.Message}");
                    }

                    // Delete lobby_connect files (all variations: _lobby_connect* and lobby_connect*)
                    try
                    {
                        // Search for _lobby_connect* pattern
                        var lobbyConnectFiles =
                            Directory.GetFiles(installPath, "_lobby_connect*", SearchOption.AllDirectories);
                        foreach (var lobbyConnectFile in lobbyConnectFiles)
                        {
                            try
                            {
                                File.Delete(lobbyConnectFile);
                                Debug.WriteLine($"[SHARE CLEAN] Deleted lobby_connect file: {lobbyConnectFile}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(
                                    $"[SHARE CLEAN] Failed to delete lobby_connect file at {lobbyConnectFile}: {ex.Message}");
                            }
                        }

                        // Also search for lobby_connect* pattern (without underscore prefix)
                        lobbyConnectFiles =
                            Directory.GetFiles(installPath, "lobby_connect*", SearchOption.AllDirectories);
                        foreach (var lobbyConnectFile in lobbyConnectFiles)
                        {
                            try
                            {
                                File.Delete(lobbyConnectFile);
                                Debug.WriteLine($"[SHARE CLEAN] Deleted lobby_connect file: {lobbyConnectFile}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(
                                    $"[SHARE CLEAN] Failed to delete lobby_connect file at {lobbyConnectFile}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SHARE CLEAN] Failed to search for lobby_connect files: {ex.Message}");
                    }

                    // Delete shortcuts (*.lnk files in the game directory)
                    try
                    {
                        var shortcuts = Directory.GetFiles(installPath, "*.lnk", SearchOption.TopDirectoryOnly);
                        if (shortcuts.Length > 0)
                        {
                            foreach (var shortcut in shortcuts)
                            {
                                try
                                {
                                    File.Delete(shortcut);
                                    Debug.WriteLine($"[SHARE CLEAN] Deleted shortcut: {Path.GetFileName(shortcut)}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(
                                        $"[SHARE CLEAN] Failed to delete shortcut {Path.GetFileName(shortcut)}: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SHARE CLEAN] Failed to search for shortcuts: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SHARE CLEAN] Error during clean file preparation: {ex.Message}");
            }
        }

        // If sharing cracked version, first crack the game
        if (cracked)
        {
            // Call the main form's cracking method
            if (parentForm is SteamAppId parentFormTyped)
            {
                // Suppress status updates on main window while cracking from share window
                parentFormTyped.SetSuppressStatusUpdates(true);

                // Set the game directory and app ID in the main form
                parentFormTyped.GameDirectory = installPath;
                SteamAppId.CurrentAppId = appId;

                // Show status with visual indicator
                row.Cells["ShareCracked"].Value = "⚙️ Cracking...";
                row.Cells["ShareCracked"].Style.BackColor = Color.FromArgb(30, 30, 0); // Subtle yellow tint

                // Create translucent overlay
                var overlay = new Panel
                {
                    Size = ClientSize,
                    Location = new Point(0, 0),
                    BackColor = Color.FromArgb(180, 5, 8, 20), // Semi-transparent dark
                    Visible = true
                };
                // Enable double buffering to prevent flickering
                typeof(Panel).InvokeMember("DoubleBuffered",
                    BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, overlay, [true]);
                // Make overlay draggable
                overlay.MouseDown += TitleBar_MouseDown;

                var lblCracking = new Label
                {
                    Text = $"⚙️ Cracking {gameName}...\n\nInitializing...",
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 200, 255),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    AutoSize = false
                };
                // Make label draggable too
                lblCracking.MouseDown += TitleBar_MouseDown;

                overlay.Controls.Add(lblCracking);
                Controls.Add(overlay);
                overlay.BringToFront();
                Application.DoEvents(); // Force UI update

                // Helper to update crack progress
                void UpdateProgress(string status)
                {
                    if (lblCracking.IsHandleCreated)
                    {
                        lblCracking.Invoke(() => { lblCracking.Text = $"⚙️ Cracking {gameName}...\n\n{status}"; });
                    }
                }

                // Subscribe to main form's status updates
                EventHandler<string> statusHandler = (s, status) => UpdateProgress(status);
                parentFormTyped.CrackStatusChanged += statusHandler;

                try
                {
                    // Crack the game
                    bool crackSuccess = await parentFormTyped.CrackAsync();

                    // Unsubscribe
                    parentFormTyped.CrackStatusChanged -= statusHandler;

                    // Re-enable status updates
                    parentFormTyped.SetSuppressStatusUpdates(false);

                    // Remove overlay
                    Controls.Remove(overlay);
                    overlay.Dispose();

                    if (!crackSuccess)
                    {
                        MessageBox.Show(
                            "Failed to crack the game. This could be because:\n\n" +
                            "• The game doesn't have steam_api.dll or steam_api64.dll\n" +
                            "• The game is already cracked\n" +
                            "• Missing required files in _bin folder\n\n" +
                            "You can still share the game as Clean instead.",
                            "Crack Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        row.Cells["ShareCracked"].Value = "🎮 Share";
                        row.Cells["ShareCracked"].Style.BackColor = Color.FromArgb(8, 8, 12); // Reset to default
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Re-enable status updates
                    parentFormTyped.SetSuppressStatusUpdates(false);

                    // Remove overlay
                    Controls.Remove(overlay);
                    overlay.Dispose();

                    // Write full error to file for debugging
                    string errorLog = $"_ShareCrackError_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    File.WriteAllText(errorLog,
                        $"=== Share Cracked Error ===\n" +
                        $"Game: {gameName}\n" +
                        $"AppID: {appId}\n" +
                        $"Path: {installPath}\n" +
                        $"Time: {DateTime.Now}\n\n" +
                        $"Exception Type: {ex.GetType().Name}\n" +
                        $"Message: {ex.Message}\n\n" +
                        $"Stack Trace:\n{ex.StackTrace}\n\n" +
                        $"Full Exception:\n{ex}");

                    // Show user-friendly error
                    MessageBox.Show(
                        $"Error during cracking process:\n\n{ex.Message}\n\n" +
                        $"Full error details saved to:\n{errorLog}\n\n" +
                        "You can still share the game as Clean instead.",
                        "Crack Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    row.Cells["ShareCracked"].Value = "🎮 Share";
                    row.Cells["ShareCracked"].Style.BackColor = Color.FromArgb(8, 8, 12); // Reset to default
                    return;
                }
            }
            else
            {
                MessageBox.Show("Cannot access cracking functionality. Please use the main window to crack games.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        try
        {
            // Show the compression settings dialog
            using var compressionForm = new CompressionSettingsForm();
            compressionForm.StartPosition = FormStartPosition.CenterParent;
            if (compressionForm.ShowDialog(this) != DialogResult.OK)
            {
                // User cancelled - reset the button status
                if (cracked)
                {
                    row.Cells["ShareCracked"].Value = "🎮 Share";
                    row.Cells["ShareCracked"].Style.BackColor = Color.FromArgb(5, 8, 20);
                }
                else
                {
                    row.Cells["ShareClean"].Value = "📦 Share";
                    row.Cells["ShareClean"].Style.BackColor = Color.FromArgb(5, 8, 20);
                }

                return;
            }

            // Update button to show processing
            var btnColumn = cracked ? "ShareCracked" : "ShareClean";
            row.Cells[btnColumn].Value = "⏳ Compressing...";

            // Get selected settings
            string format = compressionForm.SelectedFormat;
            string level = compressionForm.SelectedLevel;
            bool usePassword = compressionForm.UseRinPassword;

            // Build output path
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string prefix = cracked ? "[SACGUI] CRACKED" : "[SACGUI] CLEAN";
            // Sanitize filename - remove invalid characters but keep spaces
            string safeGameName = gameName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeGameName = safeGameName.Replace(c.ToString(), "");
            }

            string zipName = $"{prefix} {safeGameName}.{format.ToLower()}";
            string outputPath = Path.Combine(desktopPath, zipName);

            // For clean shares, prepare proper Steam folder structure
            string pathToCompress = installPath;
            string tempCleanFolder = null;
            bool isCleanStructure = false;

            if (!cracked)
            {
                // Prepare clean game structure with depotcache and steamapps
                string buildId = row.Cells["BuildID"].Value?.ToString() ?? "Unknown";
                tempCleanFolder =
                    await Task.Run(() => PrepareCleanGameStructure(appId, gameName, installPath, buildId));

                if (!string.IsNullOrEmpty(tempCleanFolder))
                {
                    pathToCompress = tempCleanFolder;
                    isCleanStructure = true;
                    Debug.WriteLine($"[SHARE CLEAN] Using prepared structure: {pathToCompress}");
                }
                else
                {
                    Debug.WriteLine("[SHARE CLEAN] Failed to prepare structure, using direct path");
                }
            }

            // Show progress window
            var progressForm = new RGBProgressWindow(gameName, cracked ? "Cracked" : "Clean");
            progressForm.TopMost = TopMost;
            progressForm.Show(this);
            progressForm.CenterOverParent(this);
            progressForm.UpdateStatus($"Compressing with {format.ToUpper()} level {level}..." +
                                      (usePassword ? " (Password protected)" : ""));

            // Compress the game using the real compression with optional password
            bool compressionSuccess = await Task.Run(() => CompressGameProper(pathToCompress, outputPath, format, level,
                usePassword ? "cs.rin.ru" : null, progressForm, isCleanStructure));

            // Clean up temporary folder if created
            if (!string.IsNullOrEmpty(tempCleanFolder) && Directory.Exists(tempCleanFolder))
            {
                try
                {
                    Directory.Delete(tempCleanFolder, true);
                    Debug.WriteLine($"[SHARE CLEAN] Cleaned up temp folder: {tempCleanFolder}");
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[SHARE CLEAN] Failed to cleanup temp folder: {cleanupEx.Message}");
                }
            }

            progressForm.Close();

            if (!compressionSuccess)
            {
                MessageBox.Show("Compression failed!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                row.Cells[btnColumn].Value = cracked ? "🎮 Share" : "📦 Share";
                return;
            }

            // Compression done! Now show dialog with upload/explorer options
            row.Cells[btnColumn].Value = "✅ Compressed";

            var completionDialog = new Form
            {
                Text = "Compression Complete!",
                Size = new Size(400, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.FromArgb(5, 8, 20)
            };

            // Apply acrylic to completion dialog
            completionDialog.Load += (s, e) =>
            {
                AcrylicHelper.ApplyAcrylic(completionDialog);
            };

            var label = new Label
            {
                Text = $"Successfully compressed:\n{zipName}\n\nWhat would you like to do?",
                Size = new Size(360, 60),
                Location = new Point(20, 20),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var uploadBtn = new Button
            {
                Text = "📤 Upload to Backend",
                Size = new Size(150, 40),
                Location = new Point(40, 100),
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.FromArgb(150, 255, 150),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Yes
            };
            uploadBtn.FlatAppearance.BorderColor = Color.FromArgb(150, 255, 150);
            uploadBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);

            var explorerBtn = new Button
            {
                Text = "📁 Show in Explorer",
                Size = new Size(150, 40),
                Location = new Point(210, 100),
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.FromArgb(150, 255, 150),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.No
            };
            explorerBtn.FlatAppearance.BorderColor = Color.FromArgb(150, 255, 150);
            explorerBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);

            completionDialog.Controls.Add(label);
            completionDialog.Controls.Add(uploadBtn);
            completionDialog.Controls.Add(explorerBtn);

            // Match parent form's TopMost setting
            completionDialog.TopMost = TopMost;

            var result = completionDialog.ShowDialog(this);

            if (result == DialogResult.Yes)
            {
                // User chose to upload
                row.Cells[btnColumn].Value = "⏳ Uploading...";

                var uploadProgress = new RGBProgressWindow(gameName, "Uploading");
                uploadProgress.TopMost = TopMost;
                string uploadUrl = await UploadFileWithProgress(outputPath, uploadProgress);

                if (!string.IsNullOrEmpty(uploadUrl))
                {
                    // Check if it's a 1fichier URL (not converted yet)
                    bool isOneFichier = uploadUrl.Contains("1fichier.com");

                    Debug.WriteLine(
                        $"[SHARE] Final upload URL to show user: {uploadUrl} (isOneFichier: {isOneFichier})");

                    row.Cells[btnColumn].Value = "✅ Shared";
                    row.Cells[btnColumn].Style.BackColor = Color.FromArgb(0, 60, 0);

                    // Save shared game data
                    SaveSharedGame(appId, row.Cells["BuildID"].Value?.ToString(), cracked ? "cracked" : "clean");

                    // Get file size for conversion timing
                    long fileSize = new FileInfo(outputPath).Length;

                    // Show success modal with optional convert button for 1fichier links
                    ShowUploadSuccessWithConvert(uploadUrl, gameName, cracked, isOneFichier, fileSize);
                }
                else
                {
                    row.Cells[btnColumn].Value = "❌ Upload Failed";
                }
            }
            else
            {
                // User chose to show in explorer (save locally)
                Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
                row.Cells[btnColumn].Value = "💾 Saved";

                // Still save shared game data even if just saved locally
                SaveSharedGame(appId, row.Cells["BuildID"].Value?.ToString(), cracked ? "cracked" : "clean");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            row.Cells[cracked ? "ShareCracked" : "ShareClean"].Value = cracked ? "🎮 Share" : "📦 Share";
        }
    }

    /// <summary>
    ///     Prepares the proper Steam folder structure for clean game sharing
    /// </summary>
    private string PrepareCleanGameStructure(string appId, string gameName, string installPath, string buildId)
    {
        try
        {
            Debug.WriteLine($"[SHARE CLEAN] Preparing structure for {gameName} (AppID: {appId})");

            // Find the ACF file for this game
            string acfFilePath = null;
            string acfContent = null;

            var steamPaths = GetSteamLibraryPaths();
            foreach (var steamPath in steamPaths)
            {
                var potentialAcfPath = Path.Combine(steamPath, $"appmanifest_{appId}.acf");
                if (File.Exists(potentialAcfPath))
                {
                    acfFilePath = potentialAcfPath;
                    acfContent = File.ReadAllText(potentialAcfPath);
                    Debug.WriteLine($"[SHARE CLEAN] Found ACF: {acfFilePath}");
                    break;
                }
            }

            if (string.IsNullOrEmpty(acfFilePath))
            {
                Debug.WriteLine($"[SHARE CLEAN] No ACF file found for AppID {appId}");
                return null;
            }

            // Parse installed depots from ACF
            var depots = ParseInstalledDepots(acfContent);
            Debug.WriteLine($"[SHARE CLEAN] Found {depots.Count} depots");

            // Find depot manifests
            var manifestFiles = FindDepotManifests(appId, depots);
            Debug.WriteLine($"[SHARE CLEAN] Found {manifestFiles.Count} depot manifests");

            // Get install directory name from ACF
            var acfData = ParseAcfFile(acfContent);
            string installDir = acfData.TryGetValue("installdir", out string? value)
                ? value
                : Path.GetFileName(installPath);

            // Create temp folder with proper naming: GameName.Build.BuildID.Win64.public
            string tempBasePath = Path.Combine(Path.GetTempPath(),
                "SACGUI_Clean_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string cleanFolderName = $"{gameName.Replace(" ", ".")}.Build.{buildId}.Win64.public";
            // Sanitize folder name
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanFolderName = cleanFolderName.Replace(c.ToString(), "");
            }

            string cleanFolderPath = Path.Combine(tempBasePath, cleanFolderName);
            Directory.CreateDirectory(cleanFolderPath);

            // Create depotcache folder and copy manifests
            string depotcachePath = Path.Combine(cleanFolderPath, "depotcache");
            Directory.CreateDirectory(depotcachePath);

            foreach (var manifestFile in manifestFiles)
            {
                string destPath = Path.Combine(depotcachePath, Path.GetFileName(manifestFile));
                File.Copy(manifestFile, destPath, true);
                Debug.WriteLine($"[SHARE CLEAN] Copied manifest: {Path.GetFileName(manifestFile)}");
            }

            // Create steamapps folder
            string steamappsPath = Path.Combine(cleanFolderPath, "steamapps");
            Directory.CreateDirectory(steamappsPath);

            // Copy ACF file
            string destAcfPath = Path.Combine(steamappsPath, Path.GetFileName(acfFilePath));
            File.Copy(acfFilePath, destAcfPath, true);
            Debug.WriteLine("[SHARE CLEAN] Copied ACF file");

            // Create common folder and copy game files
            string commonPath = Path.Combine(steamappsPath, "common");
            Directory.CreateDirectory(commonPath);

            string gameDestPath = Path.Combine(commonPath, installDir);
            Debug.WriteLine($"[SHARE CLEAN] Copying game files from {installPath} to {gameDestPath}");

            // Copy entire game directory
            CopyDirectory(installPath, gameDestPath);

            Debug.WriteLine($"[SHARE CLEAN] Structure prepared successfully: {cleanFolderPath}");
            return cleanFolderPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SHARE CLEAN] Error preparing structure: {ex.Message}");
            Debug.WriteLine($"[SHARE CLEAN] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    ///     Converts a path to long path format (bypasses 260 character limit)
    /// </summary>
    private string ToLongPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Already a long path
        if (path.StartsWith(@"\\?\"))
        {
            return path;
        }

        // UNC path
        if (path.StartsWith(@"\\"))
        {
            return @"\\?\UNC\" + path.Substring(2);
        }

        // Regular path - must be absolute
        if (Path.IsPathRooted(path))
        {
            return @"\\?\" + path;
        }

        // Relative path - convert to absolute first
        return @"\\?\" + Path.GetFullPath(path);
    }

    /// <summary>
    ///     Recursively copies a directory and all its contents
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        // Ensure destination directory exists
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy files
        foreach (string file in Directory.EnumerateFiles(sourceDir))
        {
            try
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[COPY] Error copying file {file}: {ex.Message}");
                throw;
            }
        }

        // Copy subdirectories
        foreach (string subDir in Directory.EnumerateDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(subDir);
            string destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(subDir, destSubDir);
        }
    }

    private bool CompressGameProper(string sourcePath, string outputPath, string format, string level,
        string password = null, RGBProgressWindow progressWindow = null, bool includeParentFolder = false)
    {
        try
        {
            // Use the embedded 7-Zip from _bin folder (extracts if needed)
            string sevenZipPath = ResourceExtractor.GetBinFilePath(Path.Combine("7z", "7za.exe"));

            if (File.Exists(sevenZipPath))
            {
                // Use 7-Zip with proper compression level
                string compressionSwitch = "";
                int levelNum = int.Parse(level ?? "0");

                if (levelNum == 0)
                {
                    compressionSwitch = "-mx0"; // Store only
                }
                else if (levelNum <= 3)
                {
                    compressionSwitch = "-mx1"; // Fastest
                }
                else if (levelNum <= 6)
                {
                    compressionSwitch = "-mx5"; // Normal
                }
                else if (levelNum <= 9)
                {
                    compressionSwitch = "-mx7"; // Maximum
                }
                else
                {
                    compressionSwitch = "-mx9"; // Ultra

                    // For ultra compression on large files, add solid block size limit
                    // This prevents memory exhaustion during compression
                    compressionSwitch += " -ms=512m"; // 512MB solid blocks instead of unlimited
                }

                string archiveType = format.ToLower() == "7z" ? "7z" : "zip";

                // Smart dictionary size based on compression level and available RAM
                string dictParams = "";
                if (archiveType == "7z" && levelNum >= 7)
                {
                    try
                    {
                        // Get available RAM
                        var pc = new PerformanceCounter("Memory", "Available MBytes");
                        float availRAM = pc.NextValue();

                        // Calculate appropriate dictionary size (in MB)
                        // 64-bit 7z.exe can use MUCH more memory!
                        int dictSize = 256; // Default 256MB

                        if (availRAM < 2048)
                        {
                            dictSize = 64; // Very low RAM
                        }
                        else if (availRAM < 4096)
                        {
                            dictSize = 128; // Low RAM
                        }
                        else if (availRAM < 8192)
                        {
                            dictSize = 256; // Moderate RAM
                        }
                        else if (availRAM < 16384)
                        {
                            dictSize = 512; // Good RAM
                        }
                        else if (availRAM < 32768)
                        {
                            dictSize = 1024; // Great RAM (1GB dictionary)
                        }
                        else
                        {
                            dictSize = 1536; // Excellent RAM (1.5GB dictionary for your 64GB system!)
                        }

                        // Add memory limit for working buffers (separate from dictionary)
                        // This prevents runaway memory usage during compression
                        dictParams = $" -md={dictSize}m -mmt=on -mmem={dictSize * 3}m";
                        Debug.WriteLine($"[7z] Using dictionary size: {dictSize}MB (Available RAM: {availRAM}MB)");
                    }
                    catch
                    {
                        // If we can't detect RAM, use safe default
                        dictParams = " -md=256m";
                        Debug.WriteLine("[7z] Could not detect RAM, using 256MB dictionary");
                    }
                }

                // Add password if provided
                string passwordSwitch = !string.IsNullOrEmpty(password) ? $"-p\"{password}\"" : "";

                // For clean structure, we need to include the parent folder in the archive
                string arguments;
                string workingDirectory = null;

                if (includeParentFolder)
                {
                    // Compress from parent directory to include folder name in archive
                    string parentDir = Path.GetDirectoryName(sourcePath);
                    string folderName = Path.GetFileName(sourcePath);
                    workingDirectory = parentDir;
                    // Use trailing backslash to indicate it's a directory (no -r needed, 7z will recurse automatically)
                    arguments =
                        $"a -t{archiveType} {compressionSwitch}{dictParams} {passwordSwitch} -bsp1 \"{outputPath}\" \"{folderName}\\\"";
                }
                else
                {
                    // Compress contents only (original behavior)
                    arguments =
                        $"a -t{archiveType} {compressionSwitch}{dictParams} {passwordSwitch} -bsp1 \"{outputPath}\" \"{sourcePath}\\*\"";
                }

                Debug.WriteLine($"[7z] Command: 7za.exe {arguments}");
                Debug.WriteLine($"[7z] Working dir: {workingDirectory ?? "default"}");

                var psi = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                using var process = Process.Start(psi);
                var errorOutput = new StringBuilder();

                // Read output asynchronously to capture progress
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[7z OUT] {e.Data}");

                        if (progressWindow is { IsHandleCreated: true })
                        {
                            // 7-Zip outputs progress like "5%" or " 42%"
                            var match = Regex.Match(e.Data, @"(\d+)%");
                            if (match.Success)
                            {
                                int percentage = int.Parse(match.Groups[1].Value);
                                try
                                {
                                    progressWindow.Invoke(() =>
                                    {
                                        progressWindow.progressBar.Value = Math.Min(percentage, 100);
                                        progressWindow.lblStatus.Text = $"Compressing... {percentage}%";
                                    });
                                }
                                catch { }
                            }
                        }
                    }
                };

                // Capture stderr for error messages
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[7z ERR] {e.Data}");
                        errorOutput.AppendLine(e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // NO TIMEOUT - Let it run as long as needed
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errorMsg = errorOutput.ToString();
                    Debug.WriteLine($"[7z FAILED] Exit code: {process.ExitCode}");
                    Debug.WriteLine($"[7z FAILED] Errors: {errorMsg}");

                    // Check for memory errors
                    if (errorMsg.Contains("Can't allocate") || errorMsg.Contains("memory") ||
                        errorMsg.Contains("ERROR:"))
                    {
                        var result = MessageBox.Show(
                            "7-Zip ran out of memory during compression!\n\n" +
                            "This usually happens with ultra compression on large files.\n\n" +
                            "Options:\n" +
                            "• YES - Retry with lower compression (level 5)\n" +
                            "• NO - Cancel compression\n\n" +
                            "For best results, install 64-bit 7-Zip from:\n" +
                            "https://7-zip.org/download.html",
                            "Memory Error",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (result == DialogResult.Yes)
                        {
                            // Retry with lower compression
                            Debug.WriteLine("[7z] Retrying with lower compression level 5");

                            // Delete failed partial archive
                            if (File.Exists(outputPath))
                            {
                                try { File.Delete(outputPath); }
                                catch { }
                            }

                            // Retry with level 5 (normal compression)
                            string retryArgs =
                                $"a -t{archiveType} -mx5 -md=64m {passwordSwitch} -bsp1 \"{outputPath}\" \"{sourcePath}\\*\" -r";

                            var retryPsi = new ProcessStartInfo
                            {
                                FileName = sevenZipPath,
                                Arguments = retryArgs,
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using var retryProcess = Process.Start(retryPsi);
                            retryProcess.OutputDataReceived += (sender, e) =>
                            {
                                if (!string.IsNullOrEmpty(e.Data) && progressWindow is { IsHandleCreated: true })
                                {
                                    var match = Regex.Match(e.Data, @"(\d+)%");
                                    if (match.Success)
                                    {
                                        int percentage = int.Parse(match.Groups[1].Value);
                                        try
                                        {
                                            progressWindow.Invoke(() =>
                                            {
                                                progressWindow.progressBar.Value = Math.Min(percentage, 100);
                                                progressWindow.lblStatus.Text =
                                                    $"Compressing (reduced)... {percentage}%";
                                            });
                                        }
                                        catch { }
                                    }
                                }
                            };

                            retryProcess.BeginOutputReadLine();
                            retryProcess.WaitForExit();
                            return retryProcess.ExitCode == 0;
                        }
                    }
                    else if (!string.IsNullOrEmpty(errorMsg))
                    {
                        MessageBox.Show($"7-Zip compression failed:\n{errorMsg}", "Compression Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                return process.ExitCode == 0;
            }

            if (format.ToLower() == "zip")
            {
                // Fallback to built-in ZIP compression with progress
                var compressionLevel = CompressionLevel.Optimal;
                int levelNum = int.Parse(level ?? "5");

                if (levelNum == 0)
                {
                    compressionLevel = CompressionLevel.NoCompression;
                }
                else if (levelNum <= 5)
                {
                    compressionLevel = CompressionLevel.Fastest;
                }
                else
                {
                    compressionLevel = CompressionLevel.Optimal;
                }

                // Get all files first for progress tracking
                var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
                int totalFiles = files.Length;
                int currentFile = 0;

                using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
                foreach (string file in files)
                {
                    currentFile++;
                    string relativePath = file.Substring(sourcePath.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    archive.CreateEntryFromFile(file, relativePath, compressionLevel);

                    int percentage = currentFile * 100 / totalFiles;
                    if (progressWindow is { IsHandleCreated: true })
                    {
                        try
                        {
                            progressWindow.Invoke(() =>
                            {
                                progressWindow.progressBar.Value = Math.Min(percentage, 100);
                                progressWindow.lblStatus.Text =
                                    $"Compressing... {percentage}% ({currentFile}/{totalFiles} files)";
                            });
                        }
                        catch { }
                    }
                }

                return true;
            }

            MessageBox.Show("7-Zip not found! Install 7-Zip for 7z format support.", "Warning", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Compression error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task<string> UploadFileWithProgress(string filePath, RGBProgressWindow progressWindow)
    {
        Debug.WriteLine("[UPLOAD] === Starting 1fichier upload from EnhancedShareWindow ===");
        Debug.WriteLine($"[UPLOAD] File: {filePath}");
        Debug.WriteLine($"[UPLOAD] File exists: {File.Exists(filePath)}");
        if (File.Exists(filePath))
        {
            var fi = new FileInfo(filePath);
            Debug.WriteLine($"[UPLOAD] File size: {fi.Length / (1024.0 * 1024.0):F2} MB");
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;

            // Create progress handler that updates UI thread - only updates progress bar, not status text
            var progress = new Progress<double>(value =>
            {
                var percentage = (int)(value * 100);

                // Only update progress bar if we have actual progress
                if (progressWindow is { IsDisposed: false, IsHandleCreated: true })
                {
                    try
                    {
                        progressWindow.BeginInvoke(() =>
                        {
                            // Only update progress bar - status text is handled by statusCallback with MB/s info
                            progressWindow.progressBar.Value = Math.Max(0, Math.Min(100, percentage));
                        });
                    }
                    catch { }
                }
            });

            // Status callback for upload status with speed info
            var statusProgress = new Progress<string>(status =>
            {
                try
                {
                    Debug.WriteLine($"[STATUS CALLBACK] {status}");
                    if (progressWindow is { IsDisposed: false, IsHandleCreated: true })
                    {
                        progressWindow.BeginInvoke(() =>
                        {
                            progressWindow.lblStatus.Text = status;
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[STATUS CALLBACK ERROR] {ex.Message}");
                }
            });

            // Show the progress window centered over parent (after creating progress handlers)
            progressWindow.TopMost = TopMost;
            progressWindow.lblStatus.Text = "Starting upload...";
            progressWindow.Show(this);
            progressWindow.CenterOverParent(this);
            progressWindow.BringToFront();
            Application.DoEvents(); // Allow UI to update

            // Run upload on background thread to avoid UI lock
            OneFichierUploader.UploadResult result = null;
            var uploadTask = Task.Run(async () =>
            {
                using var uploader = new OneFichierUploader();
                result = await uploader.UploadFileAsync(filePath, progress, statusProgress);
            });

            // Wait for upload or cancellation
            while (!uploadTask.IsCompleted && !progressWindow.WasCancelled)
            {
                await Task.Delay(100);
            }

            // If cancelled, close window and return null
            if (progressWindow.WasCancelled)
            {
                progressWindow.Close();
                return null;
            }

            await uploadTask; // Ensure task completes

            if (result != null && !string.IsNullOrEmpty(result.DownloadUrl))
            {
                Debug.WriteLine($"[UPLOAD] 1fichier upload successful. Download URL: {result.DownloadUrl}");

                // Close progress window
                progressWindow.Close();

                // Return the 1fichier URL - the calling code will show modal with conversion logic
                return result.DownloadUrl;
            }

            progressWindow.Close();
            throw new Exception("Upload succeeded but no download URL was returned");
        }
        catch (HttpRequestException httpEx)
        {
            progressWindow.Close();
            Debug.WriteLine($"[UPLOAD] HTTP Request Exception: {httpEx.Message}");
            Debug.WriteLine($"[UPLOAD] Inner Exception: {httpEx.InnerException?.Message}");
            throw new Exception($"Upload failed (HTTP): {httpEx.Message}. Inner: {httpEx.InnerException?.Message}");
        }
        catch (TaskCanceledException tcEx)
        {
            progressWindow.Close();
            Debug.WriteLine($"[UPLOAD] Task Cancelled/Timeout: {tcEx.Message}");
            throw new Exception($"Upload timed out: {tcEx.Message}");
        }
        catch (Exception ex)
        {
            progressWindow.Close();
            Debug.WriteLine($"[UPLOAD] General Exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[UPLOAD] Stack: {ex.StackTrace}");
            throw new Exception($"Upload failed: {ex.Message}");
        }
    }

    private async Task<string> Convert1FichierLink(string oneFichierUrl, RGBProgressWindow progressWindow = null)
    {
        // Force HTTPS - AllDebrid requires it
        if (oneFichierUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            oneFichierUrl = "https://" + oneFichierUrl.Substring(7);
            Debug.WriteLine($"[CONVERT] Converted HTTP to HTTPS: {oneFichierUrl}");
        }

        // 100 retries at 30 seconds each
        int maxRetries = 100;
        int retryDelay = 30000; // 30 seconds

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var requestBody = new { link = oneFichierUrl };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine(
                    $"[CONVERT] Converting 1fichier link (attempt {attempt}/{maxRetries}): {oneFichierUrl}");

                if (progressWindow is { IsHandleCreated: true })
                {
                    progressWindow.Invoke(() =>
                    {
                        progressWindow.lblStatus.Text = "Checking status...";
                    });
                }

                var response =
                    await client.PostAsync("https://pydrive.harryeffingpotter.com/convert-1fichier", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CONVERT] Response: {responseJson}");

                    // Parse the response to get the converted link
                    var jsonDoc = JsonDocument.Parse(responseJson);
                    if (jsonDoc.RootElement.TryGetProperty("link", out var linkProperty))
                    {
                        string convertedLink = linkProperty.GetString();
                        Debug.WriteLine($"[CONVERT] Converted link: {convertedLink}");
                        return convertedLink;
                    }
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CONVERT] Failed with status: {response.StatusCode}, Content: {responseContent}");

                    // Check if it's a "still processing" error
                    if (responseContent.Contains("LINK_DOWN") || responseContent.Contains("wait"))
                    {
                        if (attempt < maxRetries)
                        {
                            Debug.WriteLine(
                                $"[CONVERT] 1fichier still processing, waiting {retryDelay / 1000}s before retry...");

                            if (progressWindow is { IsHandleCreated: true })
                            {
                                progressWindow.Invoke(() =>
                                {
                                    progressWindow.lblStatus.Text =
                                        $"1fichier is still scanning the file, will retry again in {retryDelay / 1000}s";
                                });
                            }

                            await Task.Delay(retryDelay);
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONVERT] Exception (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay);
                    continue;
                }
            }

            // If we get here and haven't returned, retry unless it's the last attempt
            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelay);
            }
        }

        Debug.WriteLine("[CONVERT] All retry attempts exhausted, returning original link");
        // If conversion fails after all retries, return original link
        return oneFichierUrl;
    }

    private async Task<string> ConvertWithStatusCallback(string oneFichierUrl, Action<string> statusCallback)
    {
        // Force HTTPS
        if (oneFichierUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            oneFichierUrl = "https://" + oneFichierUrl.Substring(7);
        }

        int maxRetries = 30;
        int baseRetryDelaySec = 10; // Start with 10 seconds between retries

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var requestBody = new { link = oneFichierUrl };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                statusCallback?.Invoke("Converting...");

                var response =
                    await client.PostAsync("https://pydrive.harryeffingpotter.com/convert-1fichier", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseJson);
                    if (jsonDoc.RootElement.TryGetProperty("link", out var linkProperty))
                    {
                        return linkProperty.GetString();
                    }
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Check if it's a "still processing" error
                    if (responseContent.Contains("LINK_DOWN") || responseContent.Contains("wait"))
                    {
                        if (attempt < maxRetries)
                        {
                            // Variable delay - increases with each attempt
                            int delaySec = baseRetryDelaySec + (attempt * 2);
                            delaySec = Math.Min(delaySec, 60); // Cap at 60 seconds

                            // Countdown display
                            for (int i = delaySec; i > 0; i--)
                            {
                                statusCallback?.Invoke(
                                    $"1fichier scanning... retry in {i}s (attempt {attempt}/{maxRetries})");
                                await Task.Delay(1000);
                            }

                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONVERT] Exception: {ex.Message}");
                if (attempt < maxRetries)
                {
                    int delaySec = baseRetryDelaySec;
                    for (int i = delaySec; i > 0; i--)
                    {
                        statusCallback?.Invoke($"Error, retry in {i}s (attempt {attempt}/{maxRetries})");
                        await Task.Delay(1000);
                    }

                    continue;
                }
            }

            if (attempt < maxRetries)
            {
                int delaySec = baseRetryDelaySec;
                for (int i = delaySec; i > 0; i--)
                {
                    statusCallback?.Invoke($"Retry in {i}s (attempt {attempt}/{maxRetries})");
                    await Task.Delay(1000);
                }
            }
        }

        return oneFichierUrl; // Return original if all retries fail
    }

    private void GamesGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        // Format both button columns and text columns
        if (e.ColumnIndex >= 0)
        {
            var colName = gamesGrid.Columns[e.ColumnIndex].Name;
            var cell = gamesGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];

            // Style the Share columns
            if (colName == "ShareClean")
            {
                // Green tint for clean - transparent background
                cell.Style.BackColor = Color.FromArgb(8, 8, 12);
                cell.Style.ForeColor = Color.FromArgb(100, 255, 150);
                cell.Style.SelectionBackColor = Color.FromArgb(15, 25, 20);
                cell.Style.SelectionForeColor = Color.FromArgb(150, 255, 200);
            }
            else if (colName == "ShareCracked")
            {
                // Purple/magenta tint for cracked - transparent background
                cell.Style.BackColor = Color.FromArgb(8, 8, 12);
                cell.Style.ForeColor = Color.FromArgb(255, 100, 255);
                cell.Style.SelectionBackColor = Color.FromArgb(20, 15, 25);
                cell.Style.SelectionForeColor = Color.FromArgb(255, 150, 255);
            }
        }
    }

    private void EnhancedShareWindow_FormClosed(object sender, FormClosedEventArgs e)
    {
        // Main form stays visible
    }

    private void EnhancedShareWindow_Load(object sender, EventArgs e)
    {
        // Wire up checkbox change events for selected count
        gamesGrid.CellValueChanged += GamesGrid_CellValueChanged;
        gamesGrid.CurrentCellDirtyStateChanged += GamesGrid_CurrentCellDirtyStateChanged;

        // Initialize batch controls as disabled (no selection yet)
        UpdateSelectedCount();

        // Load icon from resources
        try
        {
            Icon = Resources.sac_icon;
        }
        catch { }

        _ = LoadGames();
    }

    private void BtnSettings_Click(object sender, EventArgs e)
    {
        OpenBatchCompressionSettings();
    }

    private void GamesGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        // Commit checkbox changes immediately
        if (gamesGrid.IsCurrentCellDirty && gamesGrid.CurrentCell is DataGridViewCheckBoxCell)
        {
            gamesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void GamesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (gamesGrid.Columns[e.ColumnIndex].Name == "SelectGame")
        {
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        int count = 0;
        foreach (DataGridViewRow row in gamesGrid.Rows)
        {
            if (row.Cells["SelectGame"].Value is true)
            {
                count++;
            }
        }

        // Show/hide batch controls based on selection
        bool hasSelection = count > 0;

        // Update labels: "X" (green) + " selected" (gray)
        lblSelectedCount.Text = count.ToString();
        lblSelectedCount.Visible = hasSelection;

        lblSelectedSuffix.Text = " selected";
        lblSelectedSuffix.Visible = hasSelection;

        // Position elements in a row: count -> suffix -> Process -> Settings
        if (hasSelection)
        {
            lblSelectedSuffix.Location = new Point(lblSelectedCount.Right, lblSelectedCount.Top);
            btnProcessSelected.Location = new Point(lblSelectedSuffix.Right + 8, btnProcessSelected.Top);
            btnSettings.Location = new Point(btnProcessSelected.Right + 5, btnSettings.Top);
        }

        // Hide prefix - not needed anymore
        lblSelectedPrefix.Visible = false;

        btnSettings.Visible = hasSelection;
        btnProcessSelected.Visible = hasSelection;
    }

    private void OpenBatchCompressionSettings()
    {
        using var form = new CompressionSettingsForm();
        form.Owner = this;
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            batchCompressionFormat = form.SelectedFormat;
            batchCompressionLevel = form.SelectedLevel;
            batchUsePassword = form.UseRinPassword;
        }
    }

    private void BtnProcessSelected_Click(object sender, EventArgs e)
    {
        // Collect selected game paths
        var selectedPaths = new List<string>();
        foreach (DataGridViewRow row in gamesGrid.Rows)
        {
            bool selected = row.Cells["SelectGame"].Value is true;
            if (!selected)
            {
                continue;
            }

            string path = row.Cells["InstallPath"].Value?.ToString();
            if (!string.IsNullOrEmpty(path))
            {
                selectedPaths.Add(path);
            }
        }

        if (selectedPaths.Count == 0)
        {
            MessageBox.Show("Please select at least one game.",
                "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Close the share sheet
        Close();

        // Open BatchGameSelectionForm with selected paths
        if (parentForm is SteamAppId mainForm)
        {
            mainForm.OpenBatchConversionWithPaths(selectedPaths);
        }
    }

    /// <summary>
    ///     Processes selected games using toggle button state - crack, zip, share
    /// </summary>
    private async Task ProcessSelectedGames()
    {
        // Check if any toggles are on
        if (!toggleCrackOn && !toggleZipOn && !toggleShareOn)
        {
            MessageBox.Show("Please enable at least one action (Crack, Zip, or Share).",
                "No Action Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Collect selected games
        var gamesToProcess = new List<(DataGridViewRow row, string name, string path, string appId, string buildId)>();

        foreach (DataGridViewRow row in gamesGrid.Rows)
        {
            bool selected = row.Cells["SelectGame"].Value is true;
            if (!selected)
            {
                continue;
            }

            string name = row.Cells["GameName"].Value?.ToString();
            string path = row.Cells["InstallPath"].Value?.ToString();
            string appId = row.Cells["AppID"].Value?.ToString();
            string buildId = row.Cells["BuildID"].Value?.ToString();

            gamesToProcess.Add((row, name, path, appId, buildId));
        }

        if (gamesToProcess.Count == 0)
        {
            MessageBox.Show("Please select at least one game.",
                "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Reset cancellation state
        cancelAllRemaining = false;
        skipCurrentGame = false;
        currentProcessingGame = null;

        // Disable UI during processing
        btnProcessSelected.Enabled = false;
        btnSettings.Enabled = false;

        var mainForm = parentForm as SteamAppId;
        if (mainForm == null && toggleCrackOn)
        {
            MessageBox.Show("Cannot access main form for cracking.", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ReenableProcessButtons();
            return;
        }

        // Track results
        var crackResults = new Dictionary<string, bool>();
        var archivePaths = new Dictionary<string, string>();
        var uploadResults = new List<(string name, string url, long fileSize)>();

        // ========== PHASE 0: CLEAN UP OLD CRACK ARTIFACTS (Always) ==========
        Debug.WriteLine($"[BATCH PHASE 0] Starting cleanup for {gamesToProcess.Count} games");
        foreach (var game in gamesToProcess)
        {
            try
            {
                string installPath = game.path;
                Debug.WriteLine($"[BATCH] Cleaning up {game.name} at {installPath}");

                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    Debug.WriteLine($"[BATCH] SKIP - Invalid path: {installPath}");
                    continue;
                }

                // Restore .bak files (original Steam files - DLLs and EXEs)
                try
                {
                    // First handle *.dll.bak files (steam_api backups)
                    var dllBakFiles = Directory.GetFiles(installPath, "*.dll.bak", SearchOption.AllDirectories);
                    foreach (var bakFile in dllBakFiles)
                    {
                        try
                        {
                            var originalFile = bakFile.Substring(0, bakFile.Length - 4);
                            if (File.Exists(originalFile))
                            {
                                File.Delete(originalFile);
                            }

                            File.Move(bakFile, originalFile);
                            Debug.WriteLine($"[BATCH] Restored: {Path.GetFileName(originalFile)}");
                        }
                        catch { }
                    }

                    // Then handle *.exe.bak files (steamless backups)
                    var exeBakFiles = Directory.GetFiles(installPath, "*.exe.bak", SearchOption.AllDirectories);
                    foreach (var bakFile in exeBakFiles)
                    {
                        try
                        {
                            var originalFile = bakFile.Substring(0, bakFile.Length - 4);
                            if (File.Exists(originalFile))
                            {
                                File.Delete(originalFile);
                            }

                            File.Move(bakFile, originalFile);
                            Debug.WriteLine($"[BATCH] Restored: {Path.GetFileName(originalFile)}");
                        }
                        catch { }
                    }
                }
                catch { }

                // Delete steam_settings directories
                try
                {
                    var steamSettingsDirs =
                        Directory.GetDirectories(installPath, "steam_settings", SearchOption.AllDirectories);
                    foreach (var dir in steamSettingsDirs)
                    {
                        try { Directory.Delete(dir, true); }
                        catch { }
                    }
                }
                catch { }

                // Delete _[ prefixed files (LAN shortcuts)
                try
                {
                    var lanFiles = Directory.GetFiles(installPath, "_[*", SearchOption.TopDirectoryOnly);
                    foreach (var f in lanFiles)
                    {
                        try { File.Delete(f); }
                        catch { }
                    }
                }
                catch { }

                // Delete _lobby_connect* files
                try
                {
                    var files = Directory.GetFiles(installPath, "_lobby_connect*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        try { File.Delete(f); }
                        catch { }
                    }
                }
                catch { }

                // Delete lobby_connect* files
                try
                {
                    var files = Directory.GetFiles(installPath, "lobby_connect*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        try { File.Delete(f); }
                        catch { }
                    }
                }
                catch { }

                // Delete .lnk shortcuts
                try
                {
                    var files = Directory.GetFiles(installPath, "*.lnk", SearchOption.TopDirectoryOnly);
                    foreach (var f in files)
                    {
                        try { File.Delete(f); }
                        catch { }
                    }
                }
                catch { }

                // Delete common crack artifacts
                string[] artifacts =
                [
                    "CreamAPI.dll", "cream_api.ini", "CreamLinux", "steam_api_o.dll", "steam_api64_o.dll",
                    "local_save.txt"
                ];
                foreach (var artifact in artifacts)
                {
                    try
                    {
                        var files = Directory.GetFiles(installPath, artifact, SearchOption.AllDirectories);
                        foreach (var f in files)
                        {
                            try { File.Delete(f); }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BATCH] Cleanup error for {game.name}: {ex.Message}");
            }
        }

        // ========== PHASE 1: CRACK (Sequential due to shared state) ==========
        if (toggleCrackOn)
        {
            foreach (var game in gamesToProcess)
            {
                // Check for cancel all
                if (cancelAllRemaining)
                {
                    UpdateActionColumn(game.row, "CrackOnly", "Cancelled", Color.Gray);
                    crackResults[game.path] = false;
                    continue;
                }

                currentProcessingGame = game.name;
                UpdateActionColumn(game.row, "CrackOnly", "⚡ Cracking...", Color.Yellow);

                if (string.IsNullOrEmpty(game.appId))
                {
                    UpdateActionColumn(game.row, "CrackOnly", "❌ No AppID", Color.Orange);
                    crackResults[game.path] = false;
                    continue;
                }

                try
                {
                    mainForm.GameDirectory = game.path;
                    SteamAppId.CurrentAppId = game.appId;
                    mainForm.SetSuppressStatusUpdates(true);

                    bool success = await mainForm.CrackAsync();
                    crackResults[game.path] = success;

                    mainForm.SetSuppressStatusUpdates(false);

                    if (success)
                    {
                        UpdateActionColumn(game.row, "CrackOnly", "✅ Cracked!", Color.LightGreen,
                            Color.FromArgb(60, 0, 60));

                        // Save that we cracked this game (for crack-only mode)
                        if (!toggleZipOn && !toggleShareOn)
                        {
                            SaveSharedGame(game.appId, game.buildId, "cracked_only");
                        }
                    }
                    else
                    {
                        UpdateActionColumn(game.row, "CrackOnly", "❌ Failed", Color.Red);
                    }
                }
                catch (Exception ex)
                {
                    mainForm.SetSuppressStatusUpdates(false);
                    crackResults[game.path] = false;
                    UpdateActionColumn(game.row, "CrackOnly", "❌ Error", Color.Red);
                    Debug.WriteLine($"[BATCH] Crack error for {game.name}: {ex.Message}");
                }
            }
        }
        else
        {
            // No cracking, all games considered successful for next phase
            foreach (var game in gamesToProcess)
            {
                crackResults[game.path] = true;
            }
        }

        // ========== PHASE 2: ZIP (Parallel) ==========
        string shareColumn = toggleCrackOn ? "ShareCracked" : "ShareClean";
        if (toggleZipOn)
        {
            var gamesToZip = gamesToProcess.Where(g => crackResults.ContainsKey(g.path) && crackResults[g.path])
                .ToList();

            if (gamesToZip.Count > 0)
            {
                foreach (var game in gamesToZip)
                {
                    UpdateActionColumn(game.row, shareColumn, "📦 0%", Color.Cyan);
                }

                string sevenZipPath = ResourceExtractor.GetBinFilePath(Path.Combine("7z", "7za.exe"));
                string password = batchUsePassword ? "rin" : null;

                var zipTasks = gamesToZip.Select(async game =>
                {
                    string ext = batchCompressionFormat == "7Z" ? ".7z" : ".zip";
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string safeGameName = game.name;
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        safeGameName = safeGameName.Replace(c.ToString(), "");
                    }

                    string prefix = toggleCrackOn ? "[SACGUI] CRACKED" : "[SACGUI] CLEAN";
                    string archivePath = Path.Combine(desktopPath, $"{prefix} {safeGameName}{ext}");
                    archivePaths[game.path] = archivePath;

                    var gameRow = game.row;
                    var col = shareColumn;
                    var zipStartTime = DateTime.Now;

                    bool zipSuccess = await Task.Run(() =>
                    {
                        try
                        {
                            string formatArg = batchCompressionFormat == "7Z" ? "-t7z" : "-tzip";
                            string args =
                                $"a {formatArg} -mx={batchCompressionLevel} -bsp1 \"{archivePath}\" \"{game.path}\\*\"";
                            if (!string.IsNullOrEmpty(password))
                            {
                                args += $" -p{password}";
                            }

                            var psi = new ProcessStartInfo
                            {
                                FileName = sevenZipPath,
                                Arguments = args,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using var proc = Process.Start(psi);
                            // Throttle UI updates to prevent lag
                            DateTime lastUpdate = DateTime.MinValue;
                            string lastPct = "";

                            // Read stdout for progress updates
                            while (!proc.StandardOutput.EndOfStream)
                            {
                                string line = proc.StandardOutput.ReadLine();
                                if (line != null && line.Contains("%"))
                                {
                                    // Parse percentage from lines like " 12%" or "  5%"
                                    var match = Regex.Match(line, @"(\d+)%");
                                    if (match.Success)
                                    {
                                        string pct = match.Groups[1].Value;
                                        // Only update UI every 150ms or if percentage changed significantly
                                        if ((DateTime.Now - lastUpdate).TotalMilliseconds > 150 || pct != lastPct)
                                        {
                                            lastUpdate = DateTime.Now;
                                            lastPct = pct;
                                            if (!IsDisposed)
                                            {
                                                BeginInvoke(() =>
                                                {
                                                    UpdateActionColumn(gameRow, col, $"📦 {pct}%", Color.Cyan);
                                                });
                                            }
                                        }
                                    }
                                }
                            }

                            proc.WaitForExit();
                            return proc.ExitCode == 0;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    var zipDuration = DateTime.Now - zipStartTime;
                    long zipFileSize = File.Exists(archivePath) ? new FileInfo(archivePath).Length : 0;

                    return (game, zipSuccess, zipDuration, zipFileSize, archivePath);
                }).ToList();

                var zipResults = await Task.WhenAll(zipTasks);

                foreach (var result in zipResults)
                {
                    // Store zip details
                    if (!crackDetailsMap.ContainsKey(result.game.path))
                    {
                        crackDetailsMap[result.game.path] = new SteamAppId.CrackDetails
                        {
                            GameName = result.game.name, GamePath = result.game.path
                        };
                    }

                    var details = crackDetailsMap[result.game.path];
                    details.ZipAttempted = true;
                    details.ZipSuccess = result.zipSuccess;
                    details.ZipDuration = result.zipDuration;
                    details.ZipFileSize = result.zipFileSize;
                    details.ZipPath = result.archivePath;

                    if (result.zipSuccess)
                    {
                        UpdateActionColumn(result.game.row, shareColumn, "✅ Zipped", Color.LightGreen);
                    }
                    else
                    {
                        UpdateActionColumn(result.game.row, shareColumn, "❌ Zip Failed", Color.Red);
                        crackResults[result.game.path] = false; // Prevent upload
                    }
                }

                // Refresh grid to show details icons
                gamesGrid.Invalidate();
            }
        }

        // ========== PHASE 3: SHARE/UPLOAD ==========
        if (toggleShareOn && !cancelAllRemaining)
        {
            var gamesToUpload = gamesToProcess.Where(g => crackResults.ContainsKey(g.path) && crackResults[g.path])
                .ToList();
            const int maxRetries = 3;

            // Show upload panel (docked to bottom, will push grid up)
            if (gamesToUpload.Count > 0)
            {
                uploadDetailsPanel.Visible = true;
                uploadDetailsPanel.BringToFront();

                // Reset Skip/Cancel buttons
                if (uploadDetailsPanel.Controls["btnSkip"] is Button skipBtn)
                {
                    skipBtn.Enabled = true;
                    skipBtn.Text = "Skip";
                }

                btnCancelUpload.Enabled = true;
                btnCancelUpload.Text = "Cancel All";

                // Force layout update
                PerformLayout();
            }

            foreach (var game in gamesToUpload)
            {
                // Check for cancel all at start of each game
                if (cancelAllRemaining)
                {
                    UpdateActionColumn(game.row, shareColumn, "Cancelled", Color.Gray);
                    continue;
                }

                // Reset skip flag for this game
                skipCurrentGame = false;
                currentProcessingGame = game.name;

                string archivePath = archivePaths.GetValueOrDefault(game.path);
                if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
                {
                    UpdateActionColumn(game.row, shareColumn, "No Archive", Color.Orange);
                    continue;
                }

                // Show upload details
                long fileSize = new FileInfo(archivePath).Length;
                ShowUploadDetails(game.name, fileSize);

                bool uploadSuccess = false;
                string lastError = null;
                int attempt = 0;

                while (!uploadSuccess && attempt < maxRetries && !skipCurrentGame && !cancelAllRemaining)
                {
                    attempt++;
                    string statusPrefix = attempt > 1 ? $"Retry {attempt}/{maxRetries}: " : "";

                    UpdateActionColumn(game.row, shareColumn, $"{statusPrefix}0%", Color.Magenta);

                    // Create a new cancellation token for this upload
                    batchCancellationTokenSource?.Dispose();
                    batchCancellationTokenSource = new CancellationTokenSource();
                    var token = batchCancellationTokenSource.Token;

                    try
                    {
                        using var uploader = new OneFichierUploader();
                        var gameRow = game.row;
                        var col = shareColumn;
                        var currentAttempt = attempt;
                        var startTime = DateTime.Now;
                        long lastBytes = 0;
                        double smoothedSpeed = 0;
                        DateTime lastUIUpdate = DateTime.MinValue;
                        int lastPct = -1;

                        var progress = new Progress<double>(p =>
                        {
                            int pct = (int)(p * 100);
                            long uploadedBytes = (long)(p * fileSize);

                            // Calculate speed
                            var elapsed = (DateTime.Now - startTime).TotalSeconds;
                            double currentSpeed = elapsed > 0 ? uploadedBytes / elapsed : 0;

                            // Smooth speed calculation
                            if (smoothedSpeed == 0)
                            {
                                smoothedSpeed = currentSpeed;
                            }
                            else
                            {
                                smoothedSpeed = smoothedSpeed * 0.8 + currentSpeed * 0.2;
                            }

                            // Throttle UI updates to every 200ms or when percentage changes
                            if ((DateTime.Now - lastUIUpdate).TotalMilliseconds > 200 || pct != lastPct)
                            {
                                lastUIUpdate = DateTime.Now;
                                lastPct = pct;

                                if (!IsDisposed)
                                {
                                    BeginInvoke(() =>
                                    {
                                        // Don't update column at 100% - let final status handle it
                                        if (pct < 100)
                                        {
                                            string prefix = currentAttempt > 1 ? $"Retry {currentAttempt}: " : "";
                                            UpdateActionColumn(gameRow, col, $"{prefix}{pct}%", Color.Magenta);
                                        }

                                        UpdateUploadProgress(pct, uploadedBytes, fileSize, smoothedSpeed);
                                    });
                                }
                            }

                            lastBytes = uploadedBytes;
                        });

                        var result = await Task.Run(async () =>
                        {
                            token.ThrowIfCancellationRequested();
                            return await uploader.UploadFileAsync(archivePath, progress, null, token);
                        }, token);

                        if (result != null && !string.IsNullOrEmpty(result.DownloadUrl))
                        {
                            var uploadDuration = DateTime.Now - startTime;
                            long archiveSize = File.Exists(archivePath) ? new FileInfo(archivePath).Length : 0;
                            uploadResults.Add((game.name, result.DownloadUrl, archiveSize));
                            string retryNote = attempt > 1 ? $" (retry {attempt})" : "";
                            UpdateActionColumn(game.row, shareColumn, $"Shared!{retryNote}", Color.LightGreen,
                                Color.FromArgb(0, 60, 0));

                            // Store upload details including duration
                            UpdateUploadStatus(game.path, true, result.DownloadUrl, null, attempt - 1);
                            if (crackDetailsMap.TryGetValue(game.path, out SteamAppId.CrackDetails? value))
                            {
                                value.UploadDuration = uploadDuration;
                            }

                            uploadSuccess = true;

                            // Save shared game data for persistence
                            SaveSharedGame(game.appId, game.buildId, toggleCrackOn ? "cracked" : "clean");
                        }
                        else
                        {
                            lastError = "Upload returned no URL";
                            Debug.WriteLine(
                                $"[BATCH] Upload attempt {attempt} failed for {game.name}: No URL returned");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (skipCurrentGame && !cancelAllRemaining)
                        {
                            UpdateActionColumn(game.row, shareColumn, "Skipped", Color.Yellow);
                            Debug.WriteLine($"[BATCH] Upload skipped for {game.name}");
                        }
                        else if (cancelAllRemaining)
                        {
                            UpdateActionColumn(game.row, shareColumn, "Cancelled", Color.Gray);
                        }

                        break; // Exit retry loop
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        Debug.WriteLine($"[BATCH] Upload attempt {attempt} error for {game.name}: {ex.Message}");

                        // Wait before retry (exponential backoff) - but check for skip/cancel
                        if (attempt < maxRetries && !skipCurrentGame && !cancelAllRemaining)
                        {
                            for (int i = attempt * 2; i > 0 && !skipCurrentGame && !cancelAllRemaining; i--)
                            {
                                UpdateActionColumn(game.row, shareColumn, $"Retry in {i}s...", Color.Yellow);
                                await Task.Delay(1000);
                            }
                        }
                    }
                }

                if (!uploadSuccess && !skipCurrentGame && !cancelAllRemaining)
                {
                    // Show truncated error in status
                    string shortError = lastError?.Length > 30 ? lastError.Substring(0, 30) + "..." : lastError;
                    UpdateActionColumn(game.row, shareColumn, $"Failed: {shortError}", Color.Red);
                    UpdateUploadStatus(game.path, false, null, lastError, attempt);
                }
            }

            // Hide upload panel
            HideUploadDetails();
        }
        else if (cancelAllRemaining && toggleShareOn)
        {
            // Mark remaining as cancelled
            var gamesToUpload = gamesToProcess.Where(g => crackResults.ContainsKey(g.path) && crackResults[g.path])
                .ToList();
            foreach (var game in gamesToUpload)
            {
                UpdateActionColumn(game.row, shareColumn, "Cancelled", Color.Gray);
            }
        }
        else if (toggleCrackOn && !toggleZipOn && !toggleShareOn)
        {
            // Crack only - already updated above
        }

        // Re-enable UI
        ReenableProcessButtons();

        // Auto-uncheck processed games
        foreach (var game in gamesToProcess)
        {
            game.row.Cells["SelectGame"].Value = false;
        }

        UpdateSelectedCount();

        // Show summary with conversion option
        if (uploadResults.Count > 0)
        {
            // Update status to show we're waiting for conversions
            lblStatus.Text = "Waiting for Debrid conversions...";
            lblStatus.ForeColor = Color.FromArgb(255, 200, 100);
            lblUploadGame.Text = "Uploads complete - converting to PyDrive...";
            await ShowBatchUploadSummary(uploadResults, toggleCrackOn);
            lblStatus.Text = "Ready";
            lblStatus.ForeColor = Color.FromArgb(150, 150, 150);
        }
        else if (gamesToProcess.Count > 0)
        {
            MessageBox.Show("Batch processing complete!", "Batch Complete", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void UpdateActionColumn(DataGridViewRow row, string columnName, string text, Color? foreColor = null,
        Color? backColor = null)
    {
        row.Cells[columnName].Value = text;
        if (foreColor.HasValue)
        {
            row.Cells[columnName].Style.ForeColor = foreColor.Value;
        }

        if (backColor.HasValue)
        {
            row.Cells[columnName].Style.BackColor = backColor.Value;
        }
    }

    private void ReenableProcessButtons()
    {
        btnProcessSelected.Enabled = true;
        btnSettings.Enabled = true;

        // Hide upload details panel
        uploadDetailsPanel.Visible = false;
    }

    private async Task ShowBatchUploadSummary(List<(string name, string url, long fileSize)> uploadResults,
        bool wasCracked)
    {
        var summaryForm = new Form
        {
            Text = "Batch Upload Complete!",
            Size = new Size(550, 380),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(5, 8, 20),
            ForeColor = Color.White,
            ShowInTaskbar = false,
            TopMost = TopMost
        };

        summaryForm.Load += (s, e) =>
        {
            AcrylicHelper.ApplyAcrylic(summaryForm);
        };

        // Auto-convert flag
        bool autoConvertStarted = false;

        var lblTitle = new Label
        {
            Text = $"✅ {uploadResults.Count} file(s) uploaded successfully!",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 255, 150),
            Location = new Point(20, 15),
            Size = new Size(350, 30),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Format copy buttons in top right
        // Create tooltip for summary form buttons - must be added to form's components to stay alive
        var summaryComponents = new Container();
        var summaryTooltip = new ToolTip(summaryComponents)
        {
            AutoPopDelay = 5000, InitialDelay = 300, ReshowDelay = 200
        };
        summaryForm.FormClosed += (s, e) => summaryComponents.Dispose();

        var btnForums = new Button
        {
            Text = "Forums",
            Size = new Size(55, 24),
            Location = new Point(375, 18),
            BackColor = Color.FromArgb(50, 40, 60),
            ForeColor = Color.FromArgb(200, 180, 255),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };
        btnForums.FlatAppearance.BorderColor = Color.FromArgb(80, 60, 100);
        summaryTooltip.SetToolTip(btnForums, "Copy links as BBCode for forums [url=link]Game Name[/url]");

        var btnMarkdown = new Button
        {
            Text = "MD",
            Size = new Size(40, 24),
            Location = new Point(433, 18),
            BackColor = Color.FromArgb(40, 50, 60),
            ForeColor = Color.FromArgb(180, 200, 255),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };
        btnMarkdown.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 100);
        summaryTooltip.SetToolTip(btnMarkdown, "Copy links as Markdown [Game Name](link)");

        var btnPlain = new Button
        {
            Text = "Plain",
            Size = new Size(45, 24),
            Location = new Point(476, 18),
            BackColor = Color.FromArgb(45, 45, 50),
            ForeColor = Color.FromArgb(180, 180, 190),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8)
        };
        btnPlain.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        summaryTooltip.SetToolTip(btnPlain, "Copy links as plain text (Game Name: link)");

        var txtLinks = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Text = string.Join(Environment.NewLine, uploadResults.Select(r => $"{r.name}: {r.url}")),
            Location = new Point(20, 50),
            Size = new Size(510, 160),
            BackColor = Color.FromArgb(25, 25, 35),
            ForeColor = Color.FromArgb(200, 200, 220),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9)
        };

        // Store current links for format buttons (will be updated after conversion)
        var currentLinks = uploadResults.Select(r => (r.name, r.url)).ToList();

        // Helper to calculate wait time based on file size (same logic as single upload)
        int CalculateWaitTime(long fileSize)
        {
            if (fileSize > 5L * 1024 * 1024 * 1024) // 5GB+
            {
                long sizeInGB = fileSize / (1024 * 1024 * 1024);
                int seconds = (int)(sizeInGB * 12); // 12 seconds per GB
                return Math.Min(Math.Max(seconds, 30), 1800); // 30s to 30min
            }

            if (fileSize > 100 * 1024 * 1024) // 100MB+
            {
                return 10;
            }

            return 3; // Small files
        }

        var btn1Fichier = new Button
        {
            Text = "📋 Copy 1fichier",
            Size = new Size(130, 40),
            Location = new Point(20, 230),
            BackColor = Color.FromArgb(40, 40, 50),
            ForeColor = Color.FromArgb(220, 220, 255),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9)
        };
        btn1Fichier.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 100);
        summaryTooltip.SetToolTip(btn1Fichier, "Copy original 1fichier download links to clipboard");

        var btnConvert = new Button
        {
            Text = "🔄 Convert PyDrive",
            Size = new Size(140, 40),
            Location = new Point(155, 230),
            BackColor = Color.FromArgb(40, 80, 40),
            ForeColor = Color.FromArgb(150, 255, 150),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9)
        };
        btnConvert.FlatAppearance.BorderColor = Color.FromArgb(100, 180, 100);
        summaryTooltip.SetToolTip(btnConvert, "Convert 1fichier links to PyDrive high-speed download links");

        var btnClose = new Button
        {
            Text = "Close",
            Size = new Size(80, 40),
            Location = new Point(300, 230),
            BackColor = Color.FromArgb(50, 40, 40),
            ForeColor = Color.FromArgb(255, 150, 150),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9)
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(100, 60, 60);
        summaryTooltip.SetToolTip(btnClose, "Close this summary window");

        var lblStatus = new Label
        {
            Text = "",
            Location = new Point(20, 280),
            Size = new Size(510, 50),
            ForeColor = Color.FromArgb(192, 255, 255),
            TextAlign = ContentAlignment.MiddleCenter
        };

        btnForums.Click += (s, e) =>
        {
            // BBCode format: [url=link]Game Name[/url]
            var forumText = string.Join(Environment.NewLine, currentLinks.Select(r => $"[url={r.url}]{r.name}[/url]"));
            try { Clipboard.SetText(forumText); }
            catch { }

            lblStatus.Text = "Forum BBCode copied to clipboard!";
        };

        btnMarkdown.Click += (s, e) =>
        {
            // Markdown format: [Game Name](link)
            var mdText = string.Join(Environment.NewLine, currentLinks.Select(r => $"[{r.name}]({r.url})"));
            try { Clipboard.SetText(mdText); }
            catch { }

            lblStatus.Text = "Markdown links copied to clipboard!";
        };

        btnPlain.Click += (s, e) =>
        {
            // Plain format: Game Name: link
            var plainText = string.Join(Environment.NewLine, currentLinks.Select(r => $"{r.name}: {r.url}"));
            try { Clipboard.SetText(plainText); }
            catch { }

            lblStatus.Text = "Plain text links copied to clipboard!";
        };

        btn1Fichier.Click += (s, e) =>
        {
            var links = string.Join(Environment.NewLine, currentLinks.Select(r => $"{r.name}: {r.url}"));
            try { Clipboard.SetText(links); }
            catch { }

            lblStatus.Text = "1fichier links copied to clipboard!";
        };

        btnClose.Click += (s, e) => summaryForm.Close();

        // Conversion logic - can be triggered by button or auto-start
        async Task DoConversion()
        {
            if (autoConvertStarted)
            {
                return;
            }

            autoConvertStarted = true;

            // Keep all buttons enabled - user can still click convert to retry or close anytime
            btnConvert.Text = "Converting...";

            var convertedResults = new List<(string name, string url)>();
            int current = 0;

            foreach (var result in uploadResults)
            {
                current++;

                // Calculate wait time based on this file's size
                int waitTime = CalculateWaitTime(result.fileSize);
                string sizeStr = result.fileSize > 1024 * 1024 * 1024
                    ? $"{result.fileSize / (1024 * 1024 * 1024)}GB"
                    : result.fileSize > 1024 * 1024
                        ? $"{result.fileSize / (1024 * 1024)}MB"
                        : $"{result.fileSize / 1024}KB";

                // Countdown before attempting this file
                for (int i = waitTime; i > 0; i--)
                {
                    lblStatus.Text =
                        $"({current}/{uploadResults.Count}) {result.name} ({sizeStr}): Converting in {i}s...";
                    Application.DoEvents();
                    await Task.Delay(1000);
                }

                try
                {
                    // Convert with status callback for retry wait times
                    string converted = await ConvertWithStatusCallback(result.url, status =>
                    {
                        lblStatus.Text = $"({current}/{uploadResults.Count}) {result.name}: {status}";
                        Application.DoEvents();
                    });
                    convertedResults.Add((result.name, converted));
                }
                catch
                {
                    // If conversion fails, keep original
                    convertedResults.Add((result.name, result.url));
                }
            }

            // Update currentLinks so format buttons use converted links
            currentLinks.Clear();
            currentLinks.AddRange(convertedResults);

            // Update textbox with converted links
            txtLinks.Text = string.Join(Environment.NewLine, convertedResults.Select(r => $"{r.name}: {r.url}"));

            // Copy to clipboard
            var pydriveLinks = string.Join(Environment.NewLine, convertedResults.Select(r => $"{r.name}: {r.url}"));
            try
            {
                Clipboard.SetText(pydriveLinks);
            }
            catch
            {
            }

            lblStatus.Text = $"✅ {convertedResults.Count} PyDrive links converted & copied!";
            btnConvert.Enabled = true;
            btnConvert.Text = "🔄 Convert PyDrive";
            autoConvertStarted = false;
        }

        // Countdown timer for auto-conversion (declared first so click handler can reference it)
        int secondsRemaining = 5; // Start with 5 second delay
        Timer countdownTimer = null;

        btnConvert.Click += async (s, e) =>
        {
            countdownTimer?.Stop(); // Stop countdown if user clicks manually
            await DoConversion();
        };

        countdownTimer = new Timer { Interval = 1000 };

        countdownTimer.Tick += async (s, e) =>
        {
            secondsRemaining--;
            if (secondsRemaining > 0)
            {
                lblStatus.Text = $"Auto-converting in {secondsRemaining}s...";
            }
            else
            {
                countdownTimer.Stop();
                await DoConversion();
            }
        };

        // Auto-start countdown when form shows
        summaryForm.Shown += (s, e) =>
        {
            lblStatus.Text = $"Auto-converting in {secondsRemaining}s...";
            countdownTimer.Start();
        };

        // Stop timer if form closes
        summaryForm.FormClosing += (s, e) => countdownTimer.Stop();

        summaryForm.Controls.Add(lblTitle);
        summaryForm.Controls.Add(btnForums);
        summaryForm.Controls.Add(btnMarkdown);
        summaryForm.Controls.Add(btnPlain);
        summaryForm.Controls.Add(txtLinks);
        summaryForm.Controls.Add(btn1Fichier);
        summaryForm.Controls.Add(btnConvert);
        summaryForm.Controls.Add(btnClose);
        summaryForm.Controls.Add(lblStatus);

        // Copy 1fichier links immediately
        var initialLinks = string.Join(Environment.NewLine, uploadResults.Select(r => $"{r.name}: {r.url}"));
        try { Clipboard.SetText(initialLinks); }
        catch { }

        summaryForm.ShowDialog(this);
    }

    private async void ShowUploadSuccessWithConvert(string url, string gameName, bool cracked, bool isOneFichier,
        long fileSize)
    {
        // Auto-copy link to clipboard immediately
        Clipboard.SetText(url);

        var successForm = new Form
        {
            Text = "Upload Complete!",
            Size = new Size(500, 270),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(5, 8, 20),
            ForeColor = Color.White,
            ShowInTaskbar = false
        };

        // Apply acrylic effect and rounded corners
        successForm.Load += (s, e) =>
        {
            AcrylicHelper.ApplyAcrylic(successForm);
        };

        // Match parent form's TopMost setting
        successForm.TopMost = TopMost;

        // Click off to close
        successForm.Deactivate += (s, e) => successForm.Close();

        string linkType = isOneFichier ? "1fichier" : "PyDrive";
        var lblSuccess = new Label
        {
            Text =
                $"✅ {gameName} ({(cracked ? "Cracked" : "Clean")}) uploaded successfully!\n\n{linkType} link copied to clipboard!",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 255, 150),
            Location = new Point(20, 20),
            Size = new Size(460, 60),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var txtUrl = new TextBox
        {
            Text = url,
            Location = new Point(20, 90),
            Size = new Size(460, 30),
            ReadOnly = true,
            BackColor = Color.FromArgb(10, 15, 25),
            ForeColor = Color.Cyan,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9),
            TextAlign = HorizontalAlignment.Center
        };

        // Countdown label for 1fichier links
        Label lblCountdown = null;
        Timer countdownTimer = null;
        int secondsRemaining = 0;
        bool isConverting = false;

        if (isOneFichier)
        {
            // Calculate initial wait time based on file size
            if (fileSize > 5L * 1024 * 1024 * 1024) // 5GB+
            {
                long sizeInGB = fileSize / (1024 * 1024 * 1024);
                secondsRemaining = (int)(sizeInGB * 12); // 12 seconds per GB
                secondsRemaining = Math.Min(secondsRemaining, 1800); // Cap at 30 minutes
                secondsRemaining = Math.Max(secondsRemaining, 30); // At least 30 seconds
            }
            else
            {
                secondsRemaining = 3; // Small files
            }

            string reason = fileSize > 5L * 1024 * 1024 * 1024
                ? $"Waiting for 1fichier to scan the {fileSize / (1024 * 1024 * 1024)}GB file... "
                : "Waiting for 1fichier to process... ";

            lblCountdown = new Label
            {
                Text = $"{reason}Auto-converting in {secondsRemaining}s",
                Location = new Point(20, 125),
                Size = new Size(460, 20),
                ForeColor = Color.FromArgb(200, 200, 100),
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        // Create tooltip for this form's buttons
        var successComponents = new Container();
        var successTooltip = new ToolTip(successComponents)
        {
            AutoPopDelay = 5000, InitialDelay = 300, ReshowDelay = 200
        };
        successForm.FormClosed += (s, e) => successComponents.Dispose();

        var btnCopy = new Button
        {
            Text = "📋 Copy Link",
            Location = new Point(isOneFichier ? 50 : 100, 160),
            Size = new Size(120, 40),
            BackColor = Color.FromArgb(0, 80, 120),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        btnCopy.FlatAppearance.BorderColor = Color.FromArgb(100, 200, 255);
        successTooltip.SetToolTip(btnCopy, "Copy the download link to clipboard");
        btnCopy.Click += (s, e) =>
        {
            Clipboard.SetText(txtUrl.Text);
            btnCopy.Text = "✓ Copied!";
            btnCopy.BackColor = Color.FromArgb(0, 100, 0);
        };

        // Conversion logic (shared between auto-convert and manual Convert button)
        async Task AttemptConversion()
        {
            if (isConverting)
            {
                return;
            }

            isConverting = true;

            countdownTimer?.Stop();
            lblCountdown?.Text = "Converting...";
            Button convertBtn = successForm.Controls.OfType<Button>()
                .FirstOrDefault(b => b.Text.Contains("Convert") || b.Text.Contains("Converting"));
            if (convertBtn != null)
            {
                convertBtn.Enabled = false;
                convertBtn.Text = "Converting...";
            }

            try
            {
                string convertedUrl = await Convert1FichierLink(url);

                if (!string.IsNullOrEmpty(convertedUrl))
                {
                    // Success! Update the modal
                    txtUrl.Text = convertedUrl;
                    Clipboard.SetText(convertedUrl);
                    lblSuccess.Text =
                        $"✅ {gameName} ({(cracked ? "Cracked" : "Clean")}) uploaded successfully!\n\nPyDrive link copied to clipboard!";
                    if (lblCountdown != null)
                    {
                        lblCountdown.Visible = false;
                    }

                    if (convertBtn != null)
                    {
                        convertBtn.Visible = false;
                    }

                    var copyBtn = successForm.Controls.OfType<Button>().FirstOrDefault(b => b.Text.Contains("Copy"));
                    if (copyBtn != null)
                    {
                        copyBtn.Location = new Point(100, 160);
                        copyBtn.Size = new Size(140, 40);
                    }

                    var closeBtn = successForm.Controls.OfType<Button>().FirstOrDefault(b => b.Text.Contains("Close"));
                    if (closeBtn != null)
                    {
                        closeBtn.Location = new Point(260, 160);
                    }
                }
                else
                {
                    // Failed - setup retry
                    if (lblCountdown != null)
                    {
                        lblCountdown.Text = "Trying again in 30s...";
                    }

                    if (convertBtn != null)
                    {
                        convertBtn.Text = "🔗 Convert";
                        convertBtn.Enabled = true;
                    }

                    secondsRemaining = 30;
                    countdownTimer?.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONVERT] Error: {ex.Message}");
                // Failed - setup retry
                if (lblCountdown != null)
                {
                    lblCountdown.Text = "Trying again in 30s...";
                }

                if (convertBtn != null)
                {
                    convertBtn.Text = "🔗 Convert";
                    convertBtn.Enabled = true;
                }

                secondsRemaining = 30;
                countdownTimer?.Start();
            }

            isConverting = false;
        }

        Button btnConvert = null;
        if (isOneFichier)
        {
            btnConvert = new Button
            {
                Text = "🔗 Convert",
                Location = new Point(190, 160),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(80, 40, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnConvert.FlatAppearance.BorderColor = Color.FromArgb(255, 150, 50);
            successTooltip.SetToolTip(btnConvert, "Convert 1fichier link to PyDrive high-speed download");
            btnConvert.Click += async (s, e) => await AttemptConversion();

            // Create countdown timer
            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += async (s, e) =>
            {
                secondsRemaining--;
                if (secondsRemaining > 0)
                {
                    string reason = fileSize > 5L * 1024 * 1024 * 1024 && !lblCountdown.Text.Contains("Trying")
                        ? $"Waiting for 1fichier to scan the {fileSize / (1024 * 1024 * 1024)}GB file... "
                        : lblCountdown.Text.Contains("Trying")
                            ? ""
                            : "Waiting for 1fichier to process... ";
                    string action = lblCountdown.Text.Contains("Trying") ? "Trying again" : "Auto-converting";
                    lblCountdown.Text = $"{reason}{action} in {secondsRemaining}s";
                }
                else
                {
                    await AttemptConversion();
                }
            };
            countdownTimer.Start();
        }

        var btnClose = new Button
        {
            Text = "✓ Close",
            Location = new Point(isOneFichier ? 330 : 260, 160),
            Size = new Size(120, 40),
            BackColor = Color.FromArgb(0, 100, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(100, 255, 150);
        successTooltip.SetToolTip(btnClose, "Close this window");
        btnClose.Click += (s, e) =>
        {
            countdownTimer?.Stop();
            successForm.Close();
        };

        // Add controls to form
        if (btnConvert != null && lblCountdown != null)
        {
            successForm.Controls.AddRange(lblSuccess, txtUrl, lblCountdown, btnCopy, btnConvert, btnClose);
        }
        else
        {
            successForm.Controls.AddRange(lblSuccess, txtUrl, btnCopy, btnClose);
        }

        successForm.ShowDialog(this);
    }

    private void ShowUploadSuccess(string url, string gameName, bool cracked)
    {
        ShowUploadSuccessWithConvert(url, gameName, cracked, url.Contains("1fichier.com"), 0);
    }


    private List<SteamGame> ScanSteamLibraries()
    {
        var games = new List<SteamGame>();
        var paths = GetSteamLibraryPaths();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var manifests = Directory.GetFiles(path, "appmanifest_*.acf");
                foreach (var manifest in manifests)
                {
                    var game = ParseManifest(manifest);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
            }
        }

        return games;
    }

    private List<string> GetSteamLibraryPaths()
    {
        var paths = new HashSet<string>(); // Use HashSet to avoid duplicates

        // Get ALL drives on the system
        var drives = DriveInfo.GetDrives()
            .Where(d => d is { IsReady: true, DriveType: DriveType.Fixed })
            .Select(d => d.RootDirectory.FullName);

        // Check common Steam locations on ALL drives
        foreach (var drive in drives)
        {
            var potentialPaths = new[]
            {
                Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps"),
                Path.Combine(drive, "Program Files", "Steam", "steamapps"),
                Path.Combine(drive, "Steam", "steamapps"), Path.Combine(drive, "SteamLibrary", "steamapps"),
                Path.Combine(drive, "Games", "Steam", "steamapps"),
                Path.Combine(drive, "Games", "SteamLibrary", "steamapps")
            };

            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path))
                {
                    paths.Add(path);

                    // Found a Steam install, check for libraryfolders.vdf
                    var vdfPath = Path.Combine(path, "libraryfolders.vdf");
                    if (File.Exists(vdfPath))
                    {
                        var vdfContent = File.ReadAllText(vdfPath);
                        var pathMatches = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""");
                        foreach (Match match in pathMatches)
                        {
                            var libPath = Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "steamapps");
                            if (Directory.Exists(libPath))
                            {
                                paths.Add(libPath);
                            }
                        }
                    }
                }
            }
        }

        return paths.ToList();
    }

    private SteamGame ParseManifest(string manifestPath)
    {
        try
        {
            var content = File.ReadAllText(manifestPath);

            var appIdMatch = Regex.Match(content, @"""appid""\s+""(\d+)""");
            var nameMatch = Regex.Match(content, @"""name""\s+""([^""]+)""");
            var buildIdMatch = Regex.Match(content, @"""buildid""\s+""([^""]+)""");
            var installDirMatch = Regex.Match(content, @"""installdir""\s+""([^""]+)""");
            var lastUpdatedMatch = Regex.Match(content, @"""LastUpdated""\s+""(\d+)""");

            if (!appIdMatch.Success || !nameMatch.Success)
            {
                return null;
            }

            var steamPath = Path.GetDirectoryName(manifestPath);
            var installDir = installDirMatch.Success
                ? Path.Combine(steamPath, "common", installDirMatch.Groups[1].Value)
                : "";

            long lastUpdated = 0;
            if (lastUpdatedMatch.Success)
            {
                long.TryParse(lastUpdatedMatch.Groups[1].Value, out lastUpdated);
            }

            return new SteamGame
            {
                AppId = appIdMatch.Groups[1].Value,
                Name = nameMatch.Groups[1].Value,
                BuildId = buildIdMatch.Success ? buildIdMatch.Groups[1].Value : "Unknown",
                InstallDir = installDir,
                LastUpdated = lastUpdated
            };
        }
        catch
        {
            return null;
        }
    }

    private void DataGrid_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // Get hit test info to check if we're on a column divider
            var hitTest = gamesGrid.HitTest(e.X, e.Y);

            // Don't drag if clicking on column header (for resizing/sorting)
            if (hitTest.Type == DataGridViewHitTestType.ColumnHeader)
            {
                return;
            }

            // Start drag
            TitleBar_MouseDown(sender, e);
        }
    }

    private void TitleBar_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            mouseDownPoint = new Point(e.X, e.Y);
            Cursor = Cursors.SizeAll;

            // Handle mouse move
            var titleBarControl = sender as Control;
            titleBarControl.MouseMove += TitleBar_MouseMove;
            titleBarControl.MouseUp += TitleBar_MouseUp;
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && mouseDownPoint != Point.Empty)
        {
            Location = new Point(
                Location.X + e.X - mouseDownPoint.X,
                Location.Y + e.Y - mouseDownPoint.Y
            );
        }
    }

    private void TitleBar_MouseUp(object sender, MouseEventArgs e)
    {
        Cursor = Cursors.Default;
        mouseDownPoint = Point.Empty;

        var titleBarControl = sender as Control;
        titleBarControl.MouseMove -= TitleBar_MouseMove;
        titleBarControl.MouseUp -= TitleBar_MouseUp;
    }

    private void BtnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void BtnMinimize_Click(object sender, EventArgs e)
    {
        WindowState = FormWindowState.Minimized;
    }

    private async void BtnCustomPath_Click(object sender, EventArgs e)
    {
        // Create folder browser dialog
        using var folderDialog = new FolderBrowserDialog();
        folderDialog.Description = "Select a folder to scan for Steam games (will search for .acf files)";
        folderDialog.ShowNewFolderButton = false;

        if (folderDialog.ShowDialog() == DialogResult.OK)
        {
            string selectedPath = folderDialog.SelectedPath;

            // Show progress
            lblStatus.Text = "Scanning for games...";
            lblStatus.Visible = true;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;

            await Task.Run(() =>
            {
                // Search for .acf files recursively
                var acfFiles = Directory.GetFiles(selectedPath, "appmanifest_*.acf", SearchOption.AllDirectories);

                Debug.WriteLine($"[CUSTOM PATH] Scanning: {selectedPath}");
                Debug.WriteLine($"[CUSTOM PATH] Found {acfFiles.Length} ACF files");

                // Show visible status
                Invoke(() =>
                {
                    lblStatus.Text = $"Found {acfFiles.Length} ACF file(s) in {selectedPath}";
                });

                if (acfFiles.Length > 0)
                {
                    Invoke(() =>
                    {
                        lblStatus.Text = $"Found {acfFiles.Length} game manifest(s)";
                    });

                    foreach (var acfFile in acfFiles)
                    {
                        try
                        {
                            // Parse the ACF file
                            string content = File.ReadAllText(acfFile);
                            var manifest = ParseAcfFile(content);

                            if (manifest.ContainsKey("appid") && manifest.ContainsKey("name") &&
                                manifest.TryGetValue("installdir", out string? installDir))
                            {
                                string appId = manifest["appid"];
                                string gameName = manifest["name"];

                                // ACF file directory (where the manifest is)
                                string acfDirectory = Path.GetDirectoryName(acfFile);
                                string gamePath = null;

                                Debug.WriteLine($"[CUSTOM PATH] ACF: {acfFile}");
                                Debug.WriteLine($"[CUSTOM PATH] InstallDir from ACF: {installDir}");
                                Debug.WriteLine($"[CUSTOM PATH] ACF Directory: {acfDirectory}");

                                // Check 1: In common/ subfolder (standard Steam layout)
                                string inCommon = Path.Combine(acfDirectory, "common", installDir);
                                Debug.WriteLine($"[CUSTOM PATH] Checking: {inCommon}");
                                if (Directory.Exists(inCommon))
                                {
                                    gamePath = inCommon;
                                    Debug.WriteLine("[CUSTOM PATH] ✓ Found in common/");
                                }
                                // Check 2: Next to ACF file (extracted/portable games)
                                else
                                {
                                    string nextToAcf = Path.Combine(acfDirectory, installDir);
                                    Debug.WriteLine($"[CUSTOM PATH] Checking: {nextToAcf}");
                                    if (Directory.Exists(nextToAcf))
                                    {
                                        gamePath = nextToAcf;
                                        Debug.WriteLine("[CUSTOM PATH] ✓ Found next to ACF");
                                    }
                                    else
                                    {
                                        Debug.WriteLine("[CUSTOM PATH] ✗ Game folder not found!");
                                    }
                                }

                                if (gamePath != null)
                                {
                                    // Add to grid if not already present
                                    Invoke(() =>
                                    {
                                        // Check if game already in grid
                                        bool exists = false;
                                        foreach (DataGridViewRow existingRow in gamesGrid.Rows)
                                        {
                                            if (existingRow.Cells["AppID"].Value?.ToString() == appId)
                                            {
                                                exists = true;
                                                break;
                                            }
                                        }

                                        if (exists)
                                        {
                                            Debug.WriteLine($"[CUSTOM PATH] Skipping {gameName} - already in list");
                                        }

                                        if (!exists)
                                        {
                                            var row = gamesGrid.Rows[gamesGrid.Rows.Add()];
                                            row.Cells["GameName"].Value = gameName + " [Custom]";
                                            row.Cells["AppID"].Value = appId;
                                            row.Cells["InstallPath"].Value = gamePath;
                                            row.Cells["BuildID"].Value =
                                                manifest.GetValueOrDefault("buildid", "Unknown");

                                            // Get size from ACF file directly
                                            if (manifest.TryGetValue("SizeOnDisk", out string? value1))
                                            {
                                                if (long.TryParse(value1, out long sizeBytes))
                                                {
                                                    row.Cells["GameSize"].Value = _gameData.FormatFileSize(sizeBytes);
                                                    row.Cells["GameSize"].Tag = sizeBytes;
                                                }
                                                else
                                                {
                                                    row.Cells["GameSize"].Value = "Unknown";
                                                }
                                            }
                                            else
                                            {
                                                row.Cells["GameSize"].Value = "Unknown";
                                            }

                                            // Set button values
                                            row.Cells["CrackOnly"].Value = "⚡ Crack";
                                            row.Cells["ShareClean"].Value = "📦 Share";
                                            row.Cells["ShareCracked"].Value = "🎮 Share";

                                            // Mark as custom path game with different color
                                            row.DefaultCellStyle.ForeColor = Color.FromArgb(150, 200, 255);
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CUSTOM PATH] Error parsing {acfFile}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // No ACF files found - scan for game folders anyway
                    Invoke(() =>
                    {
                        lblStatus.Text = "No Steam manifests found, scanning for game folders...";
                    });

                    // Look for common game indicators (exe files, steam_api.dll, etc)
                    var exeFiles = Directory.GetFiles(selectedPath, "*.exe", SearchOption.AllDirectories);
                    var potentialGames = new HashSet<string>();

                    foreach (var exe in exeFiles)
                    {
                        var dir = Path.GetDirectoryName(exe);
                        // Check if this directory has steam_api.dll or steam_api64.dll
                        if (File.Exists(Path.Combine(dir, "steam_api.dll")) ||
                            File.Exists(Path.Combine(dir, "steam_api64.dll")))
                        {
                            potentialGames.Add(dir);
                        }
                    }

                    foreach (var gameDir in potentialGames)
                    {
                        Invoke(() =>
                        {
                            var gameName = Path.GetFileName(gameDir);
                            var row = gamesGrid.Rows[gamesGrid.Rows.Add()];
                            row.Cells["GameName"].Value = gameName + " [Unknown]";
                            row.Cells["AppID"].Value = "Manual";
                            row.Cells["InstallPath"].Value = gameDir;
                            row.Cells["BuildID"].Value = "Unknown";
                            row.Cells["GameSize"].Value = "...";

                            // Set button values - only crack available for unknown games
                            row.Cells["CrackOnly"].Value = "⚡ Crack";
                            row.Cells["ShareClean"].Value = "❌ No ID";
                            row.Cells["ShareCracked"].Value = "❌ No ID";

                            // Mark as unknown game with different color
                            row.DefaultCellStyle.ForeColor = Color.FromArgb(255, 200, 100);

                            // Calculate size since we don't have ACF
                            int rowIndex = row.Index;
                            string gamePath = gameDir;
                            _ = Task.Run(() =>
                            {
                                long size = GetDirectorySize(gamePath);
                                Invoke(() =>
                                {
                                    if (rowIndex < gamesGrid.Rows.Count)
                                    {
                                        gamesGrid.Rows[rowIndex].Cells["GameSize"].Value =
                                            _gameData.FormatFileSize(size);
                                        gamesGrid.Rows[rowIndex].Cells["GameSize"].Tag = size;
                                    }
                                });
                            });
                        });
                    }
                }
            });

            // Hide progress
            lblStatus.Visible = false;
            progressBar.Visible = false;
            progressBar.Style = ProgressBarStyle.Blocks;
        }
    }

    // Helper method to parse ACF files (if not in SteamManifestParser)
    private static Dictionary<string, string> ParseAcfFile(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keyValuePattern = @"""(\w+)""\s+""([^""]*)""";
        var matches = Regex.Matches(content, keyValuePattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                result.TryAdd(key, value);
            }
        }

        return result;
    }

    private List<(string depotId, string manifestId)> ParseInstalledDepots(string acfContent)
    {
        var depots = new List<(string, string)>();

        var depotPattern = @"""InstalledDepots"".*?\{(.*?)\n\t\}";
        var depotMatch = Regex.Match(acfContent, depotPattern, RegexOptions.Singleline);

        if (depotMatch.Success)
        {
            var depotSection = depotMatch.Groups[1].Value;
            var depotIdPattern = @"""(\d+)""\s*\{";
            var manifestPattern = @"""manifest""\s+""(\d+)""";

            var depotIds = Regex.Matches(depotSection, depotIdPattern);
            var manifestIds = Regex.Matches(depotSection, manifestPattern);

            for (int i = 0; i < Math.Min(depotIds.Count, manifestIds.Count); i++)
            {
                depots.Add((depotIds[i].Groups[1].Value, manifestIds[i].Groups[1].Value));
            }
        }

        return depots;
    }

    private List<string> FindDepotManifests(string appId, List<(string depotId, string manifestId)> depots)
    {
        var manifestFiles = new List<string>();

        var steamPaths = GetSteamLibraryPaths();

        foreach (var steamPath in steamPaths)
        {
            var depotcachePath = Path.Combine(Path.GetDirectoryName(steamPath), "depotcache");
            if (Directory.Exists(depotcachePath))
            {
                foreach (var (depotId, manifestId) in depots)
                {
                    var manifestFile = Path.Combine(depotcachePath, $"{depotId}_{manifestId}.manifest");
                    if (File.Exists(manifestFile))
                    {
                        manifestFiles.Add(manifestFile);
                        Debug.WriteLine($"[DEPOT] Found manifest: {manifestFile}");
                    }
                }
            }
        }

        return manifestFiles;
    }

    // Helper method to get directory size
    private long GetDirectorySize(string path)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    private void gamesGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
    }

    private void GamesGrid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
    {
        if (e is { RowIndex: >= 0, ColumnIndex: >= 0 })
        {
            var colName = gamesGrid.Columns[e.ColumnIndex].Name;

            // Set cursor for clickable cells
            if (colName == "ShareClean" || colName == "ShareCracked" || colName == "CrackOnly")
            {
                gamesGrid.Cursor = Cursors.Hand;
            }
        }
        // Show tooltips only on column headers (row index -1)
        else if (e is { RowIndex: -1, ColumnIndex: >= 0 })
        {
            var colName = gamesGrid.Columns[e.ColumnIndex].Name;
            string tooltipText = "";
            switch (colName)
            {
                case "GameName":
                    tooltipText = "Game name";
                    break;
                case "InstallPath":
                    tooltipText = "Full path where the game is installed";
                    break;
                case "GameSize":
                    tooltipText = "Total size of the game installation";
                    break;
                case "BuildID":
                    tooltipText = "Steam build ID version";
                    break;
                case "AppID":
                    tooltipText = "Steam Application ID";
                    break;
                case "CrackOnly":
                    tooltipText = "Click to crack this game (generates crack files only)";
                    break;
                case "ShareClean":
                    tooltipText = "Click to share the clean game (original Steam version)";
                    break;
                case "ShareCracked":
                    tooltipText = "Click to share the cracked game (includes emulator)";
                    break;
            }

            if (!string.IsNullOrEmpty(tooltipText))
            {
                toolTip.SetToolTip(gamesGrid, tooltipText);
            }
        }
    }

    private void GamesGrid_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
    {
        gamesGrid.Cursor = Cursors.Default;
    }

    private Dictionary<string, string> LoadSharedGamesData()
    {
        var result = new Dictionary<string, string>();
        try
        {
            string data = AppSettings.Default.SharedGamesData;
            if (!string.IsNullOrEmpty(data))
            {
                // Format: appid_buildid:type,type|appid_buildid:type,type|...
                var entries = data.Split('|');
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry))
                    {
                        continue;
                    }

                    var parts = entry.Split(':');
                    if (parts.Length == 2)
                    {
                        result[parts[0]] = parts[1];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SHARED GAMES] Error loading: {ex.Message}");
        }

        return result;
    }

    private void SaveSharedGame(string appId, string buildId, string shareType)
    {
        try
        {
            var sharedGames = LoadSharedGamesData();
            string key = $"{appId}_{buildId}";

            // Add or update the share type
            if (!sharedGames.TryAdd(key, shareType))
            {
                var types = sharedGames[key].Split(',').ToList();
                if (!types.Contains(shareType))
                {
                    types.Add(shareType);
                    sharedGames[key] = string.Join(",", types);
                }
            }

            // Save back to settings
            var entries = sharedGames.Select(kvp => $"{kvp.Key}:{kvp.Value}");
            AppSettings.Default.SharedGamesData = string.Join("|", entries);
            AppSettings.Default.Save();

            Debug.WriteLine($"[SHARED GAMES] Saved {appId} build {buildId} as {shareType}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SHARED GAMES] Error saving: {ex.Message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttribData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private class SteamGame
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string InstallDir { get; set; }
        public string BuildId { get; set; }
        public long LastUpdated { get; set; }
    }

    #region Upload Details Panel Methods

    /// <summary>
    ///     Shows the upload details panel with initial info
    /// </summary>
    public void ShowUploadDetails(string gameName, long totalBytes)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => ShowUploadDetails(gameName, totalBytes)); }
            catch { }

            return;
        }

        uploadDetailsPanel.Visible = true;
        lblUploadGame.Text = $"Uploading: {gameName}";
        lblUploadSize.Text = $"0 / {_gameData.FormatFileSize(totalBytes)}";
        lblUploadSpeed.Text = "";
        lblUploadEta.Text = "";
        uploadProgressBar.Value = 0;
        uploadProgressBar.Invalidate();

        // Reset buttons
        btnCancelUpload.Text = "Cancel All";
        btnCancelUpload.Enabled = true;
        if (uploadDetailsPanel.Controls["btnSkip"] is Button skipBtn)
        {
            skipBtn.Text = "Skip";
            skipBtn.Enabled = true;
        }
    }

    /// <summary>
    ///     Updates the upload progress with speed and ETA
    /// </summary>
    public void UpdateUploadProgress(int percent, long uploadedBytes, long totalBytes, double bytesPerSecond)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateUploadProgress(percent, uploadedBytes, totalBytes, bytesPerSecond)); }
            catch { }

            return;
        }

        uploadProgressBar.Value = Math.Min(percent, 100);
        uploadProgressBar.Invalidate();
        lblUploadSize.Text = $"{_gameData.FormatFileSize(uploadedBytes)} / {_gameData.FormatFileSize(totalBytes)}";

        if (bytesPerSecond > 0)
        {
            lblUploadSpeed.Text = $"{_gameData.FormatFileSize((long)bytesPerSecond)}/s";

            long remainingBytes = totalBytes - uploadedBytes;
            if (remainingBytes > 0)
            {
                double secondsRemaining = remainingBytes / bytesPerSecond;
                lblUploadEta.Text = _formatting.FormatEta(secondsRemaining);
            }
            else
            {
                lblUploadEta.Text = "";
            }
        }

        // Force immediate UI refresh
        uploadDetailsPanel.Refresh();
    }

    /// <summary>
    ///     Hides the upload details panel
    /// </summary>
    public void HideUploadDetails()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(HideUploadDetails); }
            catch { }

            return;
        }

        uploadDetailsPanel.Visible = false;
    }

    #endregion

    #region Crack Details Methods

    /// <summary>
    ///     Stores crack details for a game by install path
    /// </summary>
    public void SetCrackDetails(string installPath, SteamAppId.CrackDetails details)
    {
        if (string.IsNullOrEmpty(installPath) || details == null)
        {
            return;
        }

        crackDetailsMap[installPath] = details;
    }

    /// <summary>
    ///     Updates zip status for a game's crack details
    /// </summary>
    public void UpdateZipStatus(string installPath, bool success, string zipPath = null, string error = null)
    {
        if (string.IsNullOrEmpty(installPath))
        {
            return;
        }

        if (!crackDetailsMap.ContainsKey(installPath))
        {
            crackDetailsMap[installPath] = new SteamAppId.CrackDetails
            {
                GameName = Path.GetFileName(installPath), GamePath = installPath
            };
        }

        var details = crackDetailsMap[installPath];
        details.ZipAttempted = true;
        details.ZipSuccess = success;
        details.ZipPath = zipPath;
        details.ZipError = error;
    }

    /// <summary>
    ///     Updates upload status for a game's crack details
    /// </summary>
    public void UpdateUploadStatus(string installPath, bool success, string url = null, string error = null,
        int retryCount = 0)
    {
        if (string.IsNullOrEmpty(installPath))
        {
            return;
        }

        if (!crackDetailsMap.ContainsKey(installPath))
        {
            crackDetailsMap[installPath] = new SteamAppId.CrackDetails
            {
                GameName = Path.GetFileName(installPath), GamePath = installPath
            };
        }

        var details = crackDetailsMap[installPath];
        details.UploadAttempted = true;
        details.UploadSuccess = success;
        details.UploadUrl = url;
        details.UploadError = error;
        details.UploadRetryCount = retryCount;
    }

    /// <summary>
    ///     Shows crack details in a popup dialog
    /// </summary>
    private void ShowCrackDetails(SteamAppId.CrackDetails details)
    {
        if (details == null)
        {
            return;
        }

        var detailForm = new Form
        {
            Text = $"Details - {details.GameName}",
            Size = new Size(600, 500),
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(25, 28, 40),
            ForeColor = Color.White,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true
        };

        var textBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 33, 45),
            ForeColor = Color.White,
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(10)
        };

        // Build colored text
        textBox.Text = "";
        textBox.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
        textBox.SelectionColor = Color.Cyan;
        textBox.AppendText($"Details for {details.GameName}\n");
        textBox.SelectionFont = new Font("Consolas", 9);
        textBox.SelectionColor = Color.Gray;
        textBox.AppendText("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
        textBox.AppendText($"Path: {details.GamePath}\n");
        textBox.AppendText($"AppID: {details.AppId}\n");
        textBox.AppendText($"Time: {details.Timestamp:yyyy-MM-dd HH:mm:ss}\n");

        textBox.SelectionColor = details.Success ? Color.LightGreen : Color.Red;
        textBox.AppendText($"Crack Success: {(details.Success ? "✓ Yes" : "✗ No")}\n\n");

        if (details.DllsBackedUp.Count > 0)
        {
            textBox.SelectionColor = Color.Yellow;
            textBox.AppendText($"DLLs Backed Up ({details.DllsBackedUp.Count}):\n");
            textBox.SelectionColor = Color.White;
            foreach (var dll in details.DllsBackedUp)
            {
                textBox.AppendText($"  • {dll}\n");
            }

            textBox.AppendText("\n");
        }

        if (details.DllsReplaced.Count > 0)
        {
            textBox.SelectionColor = Color.LightGreen;
            textBox.AppendText($"DLLs Replaced ({details.DllsReplaced.Count}):\n");
            textBox.SelectionColor = Color.White;
            foreach (var dll in details.DllsReplaced)
            {
                textBox.AppendText($"  • {dll}\n");
            }

            textBox.AppendText("\n");
        }

        if (details.ExesTried.Count > 0)
        {
            textBox.SelectionColor = Color.Cyan;
            textBox.AppendText($"EXEs Scanned by Steamless ({details.ExesTried.Count}):\n");
            foreach (var exe in details.ExesTried)
            {
                bool wasUnpacked = details.ExesUnpacked.Any(u => u.EndsWith(exe));
                if (wasUnpacked)
                {
                    textBox.SelectionColor = Color.LightGreen;
                    textBox.AppendText($"  • {exe} [UNPACKED - Had Steam Stub]\n");
                }
                else
                {
                    textBox.SelectionColor = Color.Gray;
                    textBox.AppendText($"  • {exe} [No Steam Stub]\n");
                }
            }

            textBox.AppendText("\n");
        }

        if (details.Errors.Count > 0)
        {
            textBox.SelectionColor = Color.Red;
            textBox.AppendText($"Errors ({details.Errors.Count}):\n");
            textBox.SelectionColor = Color.Orange;
            foreach (var err in details.Errors)
            {
                textBox.AppendText($"  ! {err}\n");
            }

            textBox.AppendText("\n");
        }

        // Zip status
        if (details.ZipAttempted)
        {
            textBox.SelectionColor = Color.Gray;
            textBox.AppendText("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            textBox.SelectionColor = details.ZipSuccess ? Color.LightGreen : Color.Red;
            textBox.AppendText($"Zip: {(details.ZipSuccess ? "✓ Success" : "✗ Failed")}\n");
            if (!string.IsNullOrEmpty(details.ZipPath))
            {
                textBox.SelectionColor = Color.Gray;
                textBox.AppendText($"  Path: {details.ZipPath}\n");
            }

            if (details.ZipDuration.HasValue)
            {
                textBox.SelectionColor = Color.White;
                textBox.AppendText($"  Duration: {details.ZipDuration.Value:mm\\:ss}\n");
            }

            if (details.ZipFileSize > 0)
            {
                textBox.SelectionColor = Color.White;
                string sizeStr = details.ZipFileSize > 1024 * 1024 * 1024
                    ? $"{details.ZipFileSize / (1024.0 * 1024 * 1024):F2} GB"
                    : details.ZipFileSize > 1024 * 1024
                        ? $"{details.ZipFileSize / (1024.0 * 1024):F1} MB"
                        : $"{details.ZipFileSize / 1024.0:F0} KB";
                textBox.AppendText($"  Size: {sizeStr}\n");
            }

            if (!string.IsNullOrEmpty(details.ZipError))
            {
                textBox.SelectionColor = Color.Orange;
                textBox.AppendText($"  Error: {details.ZipError}\n");
            }

            textBox.AppendText("\n");
        }

        // Upload status
        if (details.UploadAttempted)
        {
            if (!details.ZipAttempted)
            {
                textBox.SelectionColor = Color.Gray;
                textBox.AppendText("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            }

            textBox.SelectionColor = details.UploadSuccess ? Color.LightGreen : Color.Red;
            textBox.AppendText($"Upload: {(details.UploadSuccess ? "✓ Success" : "✗ Failed")}\n");
            if (details.UploadRetryCount > 0)
            {
                textBox.SelectionColor = Color.Yellow;
                textBox.AppendText($"  Retries: {details.UploadRetryCount}\n");
            }

            if (details.UploadDuration.HasValue)
            {
                textBox.SelectionColor = Color.White;
                textBox.AppendText($"  Duration: {details.UploadDuration.Value:mm\\:ss}\n");
            }

            if (!string.IsNullOrEmpty(details.UploadUrl))
            {
                textBox.SelectionColor = Color.Cyan;
                textBox.AppendText($"  1fichier: {details.UploadUrl}\n");
            }

            if (!string.IsNullOrEmpty(details.PyDriveUrl))
            {
                textBox.SelectionColor = Color.LightGreen;
                textBox.AppendText($"  PyDrive: {details.PyDriveUrl}\n");
            }

            if (!string.IsNullOrEmpty(details.UploadError))
            {
                textBox.SelectionColor = Color.Orange;
                textBox.AppendText($"  Error: {details.UploadError}\n");
            }
        }

        if (details is { HasAnyChanges: false, Errors.Count: 0, ZipAttempted: false, UploadAttempted: false })
        {
            textBox.SelectionColor = Color.Orange;
            textBox.AppendText("\nNo modifications were made to this game.\n");
            textBox.AppendText("This could mean:\n");
            textBox.AppendText("  - No steam_api.dll or steam_api64.dll was found\n");
            textBox.AppendText("  - No Steam-protected EXEs were found\n");
            textBox.AppendText("  - The game was already cracked\n");
        }

        detailForm.Controls.Add(textBox);

        // Use Show() instead of ShowDialog() to not block during uploads
        detailForm.Show(this);
        detailForm.BringToFront();
        detailForm.Activate();
    }

    #endregion
}

// Modern Progress Bar with cool blue gradient
public class ModernProgressBar : ProgressBar
{
    public ModernProgressBar()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw dark background
        e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 15)), e.ClipRectangle);

        // Calculate progress bar fill width
        int progressWidth = (int)(e.ClipRectangle.Width * ((double)Value / Maximum));

        if (progressWidth > 0)
        {
            // Create gradient brush for sky blue effect
            var progressRect = new Rectangle(0, 0, progressWidth, e.ClipRectangle.Height);
            using (var brush = new LinearGradientBrush(
                       progressRect,
                       Color.FromArgb(135, 206, 250), // Light sky blue
                       Color.FromArgb(100, 175, 220), // Darker sky blue
                       LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, progressRect);
            }

            // Add subtle highlight on top for depth
            var highlightRect = new Rectangle(0, 0, progressWidth, e.ClipRectangle.Height / 3);
            using (var highlightBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            {
                e.Graphics.FillRectangle(highlightBrush, highlightRect);
            }
        }
    }
}

// RGB Progress Window
public class RGBProgressWindow : Form
{
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private Button btnCancel;
    private int colorStep;
    private DateTime countdownStartTime;
    private long currentFileSize;
    private Label lblScrollingInfo;
    internal Label lblStatus;

    internal ProgressBar progressBar;
    private Timer rgbTimer;
    private int scrollOffset;
    private string scrollText = "";
    private Timer scrollTimer;
    private int totalCountdownMinutes;

    public RGBProgressWindow(string gameName, string type)
    {
        GameName = gameName;
        InitializeWindow(gameName, type);

        // Enable dragging the form
        MouseDown += RGBProgressWindow_MouseDown;
    }

    public bool WasCancelled { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OneFichierUrl { get; set; }

    public string GameName { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    public void CenterOverParent(Form parent)
    {
        if (parent is { IsHandleCreated: true })
        {
            // Calculate center position
            int x = parent.Left + (parent.Width - Width) / 2;
            int y = parent.Top + (parent.Height - Height) / 2;
            Location = new Point(x, y);
        }
    }

    private void InitializeWindow(string gameName, string type)
    {
        Text = $"Sharing {gameName} ({type})";
        Size = new Size(500, 260); // Increased height for scrolling text
        StartPosition = FormStartPosition.Manual; // We'll set position manually
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(5, 8, 20);

        // Apply rounded corners when form loads
        Load += (s, e) =>
        {
            try
            {
                int preference = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference, sizeof(int));
            }
            catch { }
        };

        // Scrolling info label - hidden by default
        lblScrollingInfo = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9, FontStyle.Italic),
            ForeColor = Color.FromArgb(120, 192, 255), // Soft cyan
            Location = new Point(25, 25),
            Size = new Size(450, 20),
            AutoSize = false,
            Visible = false
        };

        lblStatus = new Label
        {
            Text = "Preparing...",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Cyan, // Start with a color, will be animated
            Location = new Point(25, 70),
            Size = new Size(450, 30)
        };

        progressBar = new ModernProgressBar
        {
            Location = new Point(25, 110),
            Size = new Size(450, 30),
            Style = ProgressBarStyle.Continuous,
            BackColor = Color.FromArgb(15, 15, 15),
            Value = 0,
            Minimum = 0,
            Maximum = 100
        };

        btnCancel = new Button
        {
            Text = "✖ Cancel",
            Location = new Point(200, 165),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Standard,
            Font = new Font("Segoe UI", 9)
        };
        btnCancel.Click += (s, e) =>
        {
            WasCancelled = true;
            lblStatus.Text = "Cancelled by user";
            lblStatus.ForeColor = Color.Orange;
            btnCancel.Enabled = false;
            rgbTimer?.Stop();
            scrollTimer?.Stop();
            Close();
        };

        Controls.AddRange(lblScrollingInfo, lblStatus, progressBar, btnCancel);

        // RGB effect
        SetupRGBEffect();

        // Apply acrylic on load
        Load += (s, e) => ApplyAcrylicToProgressWindow();
    }

    private void ApplyAcrylicToProgressWindow()
    {
        AcrylicHelper.ApplyAcrylic(this, false);
    }

    private void SetupRGBEffect()
    {
        rgbTimer = new Timer { Interval = 50 };
        rgbTimer.Tick += (s, e) =>
        {
            colorStep = (colorStep + 5) % 360;
            var color = HSLToRGB(colorStep, 1.0, 0.72); // Bright vibrant colors
            // Apply RGB effect to the text
            lblStatus.ForeColor = color;

            // Apply RGB to scrolling info text too
            if (lblScrollingInfo is { Visible: true })
            {
                lblScrollingInfo.ForeColor = color;
            }

            // Try to color the progress bar (might not work on all Windows versions)
            try
            {
                progressBar.ForeColor = color;
                // For Windows 10+, we can try to use visual styles
                progressBar.Style = ProgressBarStyle.Continuous;
            }
            catch { }
        };
        rgbTimer.Start();
    }

    private Color HSLToRGB(double h, double s, double l)
    {
        double r, g, b;
        h = h / 360.0;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRGB(p, q, h + 1.0 / 3);
            g = HueToRGB(p, q, h);
            b = HueToRGB(p, q, h - 1.0 / 3);
        }

        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private double HueToRGB(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6)
        {
            return p + (q - p) * 6 * t;
        }

        if (t < 1.0 / 2)
        {
            return q;
        }

        if (t < 2.0 / 3)
        {
            return p + (q - p) * (2.0 / 3 - t) * 6;
        }

        return p;
    }

    public void UpdateStatus(string status)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                lblStatus.Text = status;
                progressBar.Value = Math.Min(progressBar.Value + 20, 100);
            });
        }
    }

    public void SetProgress(int percentage, string status)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                lblStatus.Text = status;
                progressBar.Value = Math.Max(0, Math.Min(100, percentage));
            });
        }
    }

    public void Complete(string url)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                lblStatus.Text = "✅ Complete! Upload URL copied to clipboard.";
                lblStatus.ForeColor = Color.Lime;
                progressBar.Value = 100;
                Clipboard.SetText(url);
                rgbTimer.Stop();
                BackColor = Color.FromArgb(0, 50, 0);
            });
        }
    }

    public void ShowLargeFileWarning(long fileSizeBytes, int estimatedMinutes)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                // Set up countdown
                countdownStartTime = DateTime.Now;
                totalCountdownMinutes = estimatedMinutes;
                currentFileSize = fileSizeBytes;

                // Update scrolling text
                UpdateCountdownText();
                lblScrollingInfo.Visible = true;

                // Start scrolling animation with countdown updates
                if (scrollTimer == null)
                {
                    scrollTimer = new Timer();
                    scrollTimer.Interval = 1000; // Update every second for countdown
                    scrollTimer.Tick += (s, e) =>
                    {
                        UpdateCountdownText();

                        // Do scrolling effect
                        if (!string.IsNullOrEmpty(scrollText))
                        {
                            scrollOffset = (scrollOffset + 2) % scrollText.Length;
                            string displayText = scrollText.Substring(scrollOffset) +
                                                 scrollText.Substring(0, scrollOffset);
                            lblScrollingInfo.Text =
                                displayText.Substring(0, Math.Min(displayText.Length, 60)); // Show 60 chars
                        }
                    };
                }

                scrollTimer.Start();
            });
        }
    }

    private void UpdateCountdownText()
    {
        var elapsed = DateTime.Now - countdownStartTime;
        var remaining = totalCountdownMinutes - (int)elapsed.TotalMinutes;
        if (remaining < 1)
        {
            remaining = 1; // Always show at least 1 minute
        }

        string minuteText = remaining == 1 ? "minute" : "minutes";

        // Only show compression tip for files over 10GB
        string compressionTip = "";
        if (currentFileSize > 10L * 1024 * 1024 * 1024) // 10GB
        {
            compressionTip = "     💡 Tip: 7z ultra compression can make processing up to 40% faster!";
        }

        scrollText =
            $"     Waiting for 1fichier to scan the file...     This will take roughly {remaining} more {minuteText}...{compressionTip}     Cancel anytime to get the 1fichier link...     ";
    }

    public void ShowError(string error)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                lblStatus.Text = error;
                lblStatus.ForeColor = Color.Red;
                rgbTimer.Stop();
                BackColor = Color.FromArgb(50, 0, 0);
            });
        }
    }

    private void RGBProgressWindow_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }
}
