using System.ComponentModel;
using System.Data;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using APPID.Dialogs;
using APPID.Models;
using APPID.Properties;
using APPID.Services;
using APPID.Services.Interfaces;
using APPID.Utilities;
using APPID.Utilities.Steam;
using APPID.Utilities.Steam.SteamTools.SteamTools;
using Newtonsoft.Json.Linq;
using Clipboard = System.Windows.Forms.Clipboard;
using DataFormats = System.Windows.Forms.DataFormats;
using DragDropEffects = System.Windows.Forms.DragDropEffects;
using Timer = System.Windows.Forms.Timer;

namespace APPID;

public partial class SteamAppId : Form
{
    // Exe directory - use this for all paths
    public static readonly string ExeDir =
        Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;

    public static readonly string BinPath = Path.Combine(ExeDir, "_bin");
    public static int CurrentCell;
    public static string Appname = "";
    public static string Appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static bool VrlExists;
    public static string CurrentAppId;
    public static bool SearchPause;
    public static bool BackPressed;
    public static Timer T1;

    // Services for SOLID architecture
    private readonly IFileSystemService _fileSystem;
    private readonly IGameDetectionService _gameDetection;
    private readonly IManifestParsingService _manifestParsing;
    private readonly ISettingsService _settings;
    private readonly IUrlConversionService _urlConversion;
    private string _gameDir;
    private string _gameDirName;
    private bool _isFirstClickAfterSelection; // Track first click after folder/file selection
    private bool _isInitialFolderSearch; // Track if this is the FIRST search after folder selection
    private Timer _label5Timer;

    // Custom title bar drag functionality
    private Point _mouseDownPoint = Point.Empty;


    private string _parentOfSelection;
    private Timer _resinstruccZipTimer;

    private EnhancedShareWindow _shareWindow;
    private bool _suppressStatusUpdates; // Flag to suppress Tit/Tat when cracking from share window
    private bool _textChanged;

    private CancellationTokenSource _zipCancellationTokenSource;
    public bool AutoCrackEnabled = true; // Default to ON
    public bool Cracking;

    protected DataTableGeneration DataTableGeneration;
    public bool EnableLanMultiplayer;
    public bool Goldy;
    public bool Isnumeric;

    public SteamAppId()
    {
        // Initialize services for SOLID architecture
        _fileSystem = new FileSystemService();
        _settings = new SettingsService();
        _gameDetection = new GameDetectionService(_fileSystem);
        _manifestParsing = new ManifestParsingService(_fileSystem);
        _urlConversion = new UrlConversionService();

        ServicePointManager.ServerCertificateValidationCallback =
            (s, certificate, chain, sslPolicyErrors) => true;

        // Load auto-crack setting FIRST before anything else - directly assign the value
        AutoCrackEnabled = _settings.AutoCrack;
        Debug.WriteLine($"[CONSTRUCTOR] Loaded autoCrackEnabled = {AutoCrackEnabled} from Settings");

        DataTableGeneration = new DataTableGeneration();
        Task.Run(async () => await DataTableGeneration.GetDataTableAsync(DataTableGeneration)).Wait();
        InitializeComponent();

        // Set auto-crack UI immediately after InitializeComponent so it's ready BEFORE Form_Load
        if (AutoCrackEnabled)
        {
            autoCrackOn.BringToFront();
        }
        else
        {
            autoCrackOff.BringToFront();
        }

        // Apply saved pin state
        if (_settings.Pinned)
        {
            TopMost = true;
            unPin.BringToFront();
        }
        else
        {
            TopMost = false;
            pin.BringToFront();
        }

        InitializeTimers();

        // Make form background draggable (click empty space to drag)
        MouseDown += TitleBar_MouseDown;

        // Make main panel draggable too if it exists
        if (mainPanel != null)
        {
            mainPanel.MouseDown += TitleBar_MouseDown;
        }

        // Apply rounded corners
        Load += (s, e) => ApplyAcrylicEffect();

        // Force taskbar refresh after form is fully loaded
        Shown += (s, e) =>
        {
            var timer = new Timer();
            timer.Interval = 100;
            timer.Tick += (ts, te) =>
            {
                timer.Stop();
                timer.Dispose();
                SetForegroundWindow(GetDesktopWindow());
                SetForegroundWindow(Handle);
                Activate();
            };
            timer.Start();
        };

        // Apply rounded corners to buttons
        ApplyRoundedCornersToButton(OpenDir);
        ApplyRoundedCornersToButton(ZipToShare);
        ApplyRoundedCornersToButton(btnManualEntry);

        // Initialize batch progress indicator (hidden by default)
        InitializeBatchIndicator();
    }

    // Public property to allow EnhancedShareWindow to set game directory
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string GameDirectory
    {
        get { return _gameDir; }
        set
        {
            _gameDir = value;
            // Also set gameDirName when gameDir is set
            if (!string.IsNullOrEmpty(_gameDir))
            {
                _gameDirName = Path.GetFileName(_gameDir);
            }
        }
    }

    // Event for crack status updates
    public event EventHandler<string> CrackStatusChanged;

    private void ApplyRoundedCornersToButton(Button btn)
    {
        // Make button background transparent so we can draw custom
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.Transparent;
        btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btn.FlatAppearance.MouseDownBackColor = Color.Transparent;

        // Enable transparency support
        typeof(Button).InvokeMember("DoubleBuffered",
            BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
            null, btn, [true]);

        // Disable focus cues to prevent orange highlighting
        typeof(Button).InvokeMember("SetStyle",
            BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic,
            null, btn, [ControlStyles.Selectable, false]);

        // Paint event for rounded corners
        btn.Paint += (sender, e) =>
        {
            var b = sender as Button;

            // DISABLED - This causes deadlocks when updating buttons from background threads
            // Draw the parent's background in the button area first for true transparency
            /*if (b.Parent != null)
            {
                using (var bmp = new Bitmap(b.Parent.Width, b.Parent.Height))
                {
                    b.Parent.DrawToBitmap(bmp, new Rectangle(0, 0, b.Parent.Width, b.Parent.Height));
                    e.Graphics.DrawImage(bmp, new Rectangle(0, 0, b.Width, b.Height),
                        new Rectangle(b.Left, b.Top, b.Width, b.Height), GraphicsUnit.Pixel);
                }
            }*/

            // Enable maximum quality anti-aliasing
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Create rounded rectangle that fits perfectly - subtle modern look
            int radius = 8;
            var rect = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
            var path = new GraphicsPath();

            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            // Determine colors based on state
            var bgColor = Color.FromArgb(38, 38, 42);
            if (b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)))
            {
                bgColor = Color.FromArgb(50, 50, 55); // Lighter on hover
            }

            // Draw background
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            // Draw border
            using (var pen = new Pen(Color.FromArgb(55, 55, 60), 1.5f))
            {
                e.Graphics.DrawPath(pen, path);
            }

            // Draw text
            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle,
                Color.FromArgb(220, 220, 225),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        // Refresh on mouse enter/leave for hover effect
        btn.MouseEnter += (s, e) => btn.Invalidate();
        btn.MouseLeave += (s, e) => btn.Invalidate();
    }

    private void ApplyAcrylicEffect()
    {
        // Apply rounded corners (Windows 11)
        try
        {
            int preference = DwmwcpRound;
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch { }

        // 2.3 style - just opacity
        Opacity = 0.95;
    }

    private void TitleBar_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // Don't drag if clicking on DataGridView (allows column resizing)
            var clickedControl = GetChildAtPoint(PointToClient(Cursor.Position));
            if (clickedControl is DataGridView)
            {
                return;
            }

            _mouseDownPoint = new Point(e.X, e.Y);
            Cursor = Cursors.SizeAll;

            var titleBarControl = sender as Control;
            titleBarControl.MouseMove += TitleBar_MouseMove;
            titleBarControl.MouseUp += TitleBar_MouseUp;
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _mouseDownPoint != Point.Empty)
        {
            Location = new Point(
                Location.X + e.X - _mouseDownPoint.X,
                Location.Y + e.Y - _mouseDownPoint.Y
            );
        }
    }

    private void TitleBar_MouseUp(object sender, MouseEventArgs e)
    {
        Cursor = Cursors.Default;
        _mouseDownPoint = Point.Empty;

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

    // User tracking removed - anonymous sharing now

    private void InitializeTimers()
    {
        // Timer for label5 - visible for 10 seconds
        _label5Timer = new Timer();
        _label5Timer.Interval = 10000; // 10 seconds
        _label5Timer.Tick += (s, e) =>
        {
            label5.Visible = false;
            _label5Timer.Stop();
        };

        // Timer for resinstruccZip - disappear after 30 seconds
        _resinstruccZipTimer = new Timer();
        _resinstruccZipTimer.Interval = 30000; // 30 seconds
        _resinstruccZipTimer.Tick += (s, e) =>
        {
            resinstruccZip.Visible = false;
            _resinstruccZipTimer.Stop();
        };

        // Start label5 timer on form load
        label5.Visible = true;
        _label5Timer.Start();
    }

    public static void Tit(string message, Color color)
    {
        // Skip if suppressing status updates (cracking from share window)
        if (Program.Form?._suppressStatusUpdates ?? false)
        {
            return;
        }

        // Handle cross-thread calls
        if (Program.Form?.InvokeRequired ?? false)
        {
            Program.Form?.BeginInvoke(() => Tit(message, color));
            return;
        }

        if (Program.Form == null)
        {
            return;
        }

        string messageLow = message.ToLower();
        if (messageLow.Contains("READY TO CRACK!".ToLower()))
        {
            Program.Form.StatusLabel.ForeColor = Color.HotPink;
        }
        else if (messageLow.Contains("Complete".ToLower()))
        {
            Program.Form.StatusLabel.ForeColor = Color.MediumSpringGreen;
        }
        else if (messageLow.Contains("no steam".ToLower()))
        {
            Program.Form.StatusLabel.ForeColor = Color.Crimson;
        }
        else
        {
            Program.Form.StatusLabel.ForeColor = color;
        }

        Program.Form.StatusLabel.Text = message;
        Program.Form.Text = message.Replace("&&", "&");
    }

    public static void Tat(string message)
    {
        // Skip if suppressing status updates (cracking from share window)
        if (Program.Form?._suppressStatusUpdates ?? false)
        {
            return;
        }

        // Handle cross-thread calls
        if (Program.Form?.InvokeRequired ?? false)
        {
            Program.Form?.BeginInvoke(() => Tat(message));
            return;
        }

        if (Program.Form != null)
        {
            Program.Form.currDIrText.Text = $"{message}";
        }
    }

    public static string RemoveSpecialCharacters(string str)
    {
        return Regex.Replace(str, "[^a-zA-Z0-9._0-]+", " ", RegexOptions.Compiled);
    }

    public static string ImprovedGameNameCleaning(string gameName)
    {
        string cleaned = gameName.ToLower().Trim();

        // Remove common gaming suffixes/prefixes that can cause mismatches
        string[] removeParts =
        [
            "definitive edition", "deluxe edition", "gold edition", "complete edition", "enhanced edition",
            "directors cut", "game of the year", "goty", "remastered", "redux", "ultimate", "premium",
            "collectors edition", "special edition", "anniversary edition", "standard edition", "base game",
            "digital deluxe", "legendary edition", "epic edition", "expanded edition"
        ];

        foreach (string part in removeParts)
        {
            cleaned = cleaned.Replace(part, "");
        }

        // Remove years (4 digits)
        cleaned = Regex.Replace(cleaned, @"\b(19|20)\d{2}\b", "", RegexOptions.Compiled);

        // Remove platform indicators
        cleaned = Regex.Replace(cleaned, @"\b(pc|steam|windows|x64|x86)\b", "", RegexOptions.Compiled);

        // Normalize spacing and remove extra characters
        cleaned = RemoveSpecialCharacters(cleaned);
        cleaned = Regex.Replace(cleaned, @"\s+", " ", RegexOptions.Compiled).Trim();

        return cleaned;
    }

    private void SetSelectedGame(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < dataGridView1.Rows.Count)
        {
            CurrentAppId = dataGridView1[1, rowIndex].Value.ToString();
            Appname = dataGridView1[0, rowIndex].Value.ToString().Trim();
            Tat($"{Appname} ({CurrentAppId})");
            searchTextBox.Clear();
            searchTextBox.Enabled = false;
            btnManualEntry.Visible = false;
            mainPanel.Visible = true;
            startCrackPic.Visible = true;
            resinstruccZip.Visible = false;

            // Auto-crack if enabled
            Debug.WriteLine($"[AUTO-CRACK CHECK] autoCrackEnabled={AutoCrackEnabled}, gameDir='{_gameDir ?? "NULL"}'");
            if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
            {
                Debug.WriteLine("[AUTO-CRACK] Triggering auto-crack NOW!");
                // Auto-cracking - show different message
                Tit("Auto-cracking...", Color.Lime);
                // Trigger crack just like clicking the button
                startCrackPic_Click(null, null);
            }
            else
            {
                Debug.WriteLine("[AUTO-CRACK] NOT triggering - showing manual message");
                // Manual mode - show ready message
                Tit("READY! Click skull folder above to perform crack!", Color.HotPink);
            }
        }
    }

    // Removed duplicate SetSelectedGame - using only the rowIndex version

    private bool PerformImprovedFuzzySearch(string gameFolderName)
    {
        // Try original search method first
        string search = RemoveSpecialCharacters(gameFolderName.ToLower()).Trim();
        string splitSearch = SplitCamelCase(RemoveSpecialCharacters(gameFolderName)).Trim();

        // Level 1: Exact match attempts
        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
            string.Format("Name like '" + search.Replace("_", "").Trim() + "'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
            string.Format("Name like '" + splitSearch.Replace("_", "").Trim() + "'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        // Level 2: Remove common gaming terms
        string improvedClean = ImprovedGameNameCleaning(gameFolderName);
        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
            string.Format("Name like '%" + improvedClean.Replace(" ", "%' AND Name LIKE '%") + "%'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        // Level 3: Original fallback patterns
        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
            splitSearch.ToLower().Replace("_", "").Replace("vr", "").Replace("vrs", "").Trim() + "'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
            splitSearch.ToLower().Replace("'", "").Replace("-", "").Replace(";", "").Trim() + "'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        // Level 4: Partial word matching
        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '%" +
            search.Replace("_", "").Replace(" ", "%' AND Name LIKE '%").Replace(" and ", " ").Replace(" the ", " ")
                .Replace(":", "") + "%'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        // Level 5: Try individual words for partial matches
        string[] words = improvedClean.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            // Try matching with the longest word first
            var sortedWords = words.OrderByDescending(w => w.Length).Where(w => w.Length > 3).Take(3);
            foreach (string word in sortedWords)
            {
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                    string.Format("Name like '%" + word + "%'");
                if (dataGridView1.Rows.Count > 0)
                {
                    return true;
                }
            }
        }

        // Level 6: Last resort - refresh data and try again
        DataTableGeneration = new DataTableGeneration();
        Task.Run(async () => await DataTableGeneration.GetDataTableAsync(DataTableGeneration)).Wait();
        dataGridView1.DataSource = DataTableGeneration.DataTableToGenerate;

        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
            string.Format("Name like '%" + improvedClean.Replace(" ", "%' AND Name LIKE '%") + "%'");
        if (dataGridView1.Rows.Count > 0)
        {
            return true;
        }

        // No good match found
        return false;
    }

    public async void SteamAppId_Load(object sender, EventArgs e)
    {
        // Wire up batch progress icon click to restore batch form
        if (batchProgressIcon != null)
        {
            batchProgressIcon.Click += (s, ev) =>
            {
                if (_activeBatchForm is { IsDisposed: false })
                {
                    _activeBatchForm.Show();
                    _activeBatchForm.WindowState = FormWindowState.Normal;
                    _activeBatchForm.BringToFront();
                    _activeBatchForm.Activate();
                    HideBatchIndicator(); // Hide icon/label when restored
                    Hide(); // Hide Form1 when batch form is restored
                }
            };
            batchProgressIcon.Cursor = Cursors.Hand;
        }

        if (batchProgressLabel != null)
        {
            batchProgressLabel.BringToFront(); // Make sure label is on top of icon
            batchProgressLabel.Click += (s, ev) =>
            {
                if (_activeBatchForm is { IsDisposed: false })
                {
                    _activeBatchForm.Show();
                    _activeBatchForm.WindowState = FormWindowState.Normal;
                    _activeBatchForm.BringToFront();
                    _activeBatchForm.Activate();
                    HideBatchIndicator(); // Hide icon/label when restored
                    Hide(); // Hide Form1 when batch form is restored
                }
            };
            batchProgressLabel.Cursor = Cursors.Hand;
        }

        // Load LAN multiplayer setting
        EnableLanMultiplayer = _settings.LanMultiplayer;
        lanMultiplayerCheckBox.Checked = EnableLanMultiplayer;

        if (_settings.Pinned)
        {
            TopMost = true;
            unPin.BringToFront();
        }
        else
        {
            TopMost = false;
            pin.BringToFront();
        }

        // Update UI for auto-crack setting (value already loaded in constructor)
        // When enabled, show the green ON button. When disabled, show the red OFF button.
        Debug.WriteLine(
            $"[FORM_LOAD] autoCrackEnabled = {AutoCrackEnabled}, bringing {(AutoCrackEnabled ? "autoCrackOn" : "autoCrackOff")} to front");
        if (AutoCrackEnabled)
        {
            autoCrackOn.BringToFront(); // Show green button when enabled
        }
        else
        {
            autoCrackOff.BringToFront(); // Show red button when disabled
        }

        if (_settings.Goldy)
        {
            dllSelect.SelectedIndex = 0;
            lanMultiplayerCheckBox.Visible = true;
        }
        else
        {
            dllSelect.SelectedIndex = 1;
            lanMultiplayerCheckBox.Visible = false;
        }

        Tit("Checking for Internet... ", Color.LightSkyBlue);
        ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        bool check = await Updater.CheckForNetAsync();
        if (check)
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            Tit("Checking for updates", Color.LightSkyBlue);
            await Updater.CheckGitHubNewerVersion("atom0s", "Steamless");
            await Updater.UpdateGoldBergAsync();
            await Task.Delay(1500);
            Tit("Click folder && select game's parent directory.", Color.Cyan);
        }

        T1 = new Timer();
        T1.Tick += t1_Tick;
        T1.Interval = 1000;
        if (Directory.Exists($"{Appdata}\\VRL"))
        {
            VrlExists = true;
        }

        dataGridView1.DataSource = DataTableGeneration.DataTableToGenerate;
        dataGridView1.MultiSelect = false;
        string args2 = "";
        dataGridView1.Columns[0].Width = 540;


        if (args2 != null)
        {
            foreach (string args1 in Program.CommandLineArgs ?? [])
            {
                string args3 = RemoveSpecialCharacters(args1);
                args2 += args3 + " ";
            }

            try
            {
                string search = RemoveSpecialCharacters(args2.ToLower()).Trim();
                string splitSearch = SplitCamelCase(RemoveSpecialCharacters(args2)).Trim();
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                    string.Format("Name like '" + search.Replace("_", "").Trim() + "'");

                if (dataGridView1.Rows.Count == 0)
                {
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                        string.Format("Name like '" + splitSearch.Replace("_", "").Trim() + "'");
                }

                if (dataGridView1.Rows.Count == 0)
                {
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
                        splitSearch.ToLower().Replace("_", "").Replace("vr", "").Replace("vrs", "").Trim() + "'");
                }

                if (dataGridView1.Rows.Count == 0)
                {
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
                        splitSearch.ToLower().Replace("'", "").Replace("-", "").Replace(";", "").Trim() + "'");
                }

                if (dataGridView1.Rows.Count == 0)
                {
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '%" +
                        search.Replace("_", "").Replace(" ", "%' AND Name LIKE '%").Replace(" and ", " ")
                            .Replace(" the ", " ").Replace(":", "") + "%'");
                    if (dataGridView1.Rows.Count > 0)
                    {
                        // Don't auto-select here - let it fall through to SearchComplete
                    }
                    else
                    {
                        DataTableGeneration = new DataTableGeneration();
                        Task.Run(async () => await DataTableGeneration.GetDataTableAsync(DataTableGeneration)).Wait();
                        dataGridView1.DataSource = DataTableGeneration.DataTableToGenerate;
                        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string
                            .Format("Name like '%" + splitSearch.Replace("_", "").Replace(" ", "%' AND Name LIKE '%")
                                .Replace(" and ", " ").Replace(" the ", " ").Replace(":", "") + "%'").Trim();
                        if (dataGridView1.Rows.Count > 0)
                        {
                            Clipboard.SetText($"{dataGridView1.Rows[0].Cells[1].Value}");
                        }
                        else
                        {
                            DataTableGeneration = new DataTableGeneration();
                            Task.Run(async () => await DataTableGeneration.GetDataTableAsync(DataTableGeneration))
                                .Wait();
                            dataGridView1.DataSource = DataTableGeneration.DataTableToGenerate;
                        }
                    }
                }
            }
            catch { }

            searchTextBox.Text = "";
        }

        dataGridView1.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
        dataGridView1.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;
        dataGridView1.ClearSelection();
        dataGridView1.DefaultCellStyle.SelectionBackColor = dataGridView1.DefaultCellStyle.BackColor;
        dataGridView1.DefaultCellStyle.SelectionForeColor = dataGridView1.DefaultCellStyle.ForeColor;
    }

    public static string SplitCamelCase(string input)
    {
        return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim();
    }

    private void btnSearch_Click(object sender, EventArgs e)
    {
        searchTextBox.Clear();
        searchTextBox.Focus();
    }

    private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        try
        {
            if (dataGridView1[1, e.RowIndex].Value == null)
            {
                return;
            }

            if (dataGridView1.SelectedCells.Count > 0)
            {
                CurrentCell = e.RowIndex;
            }
        }
        catch { }
    }

    private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (dataGridView1[1, e.RowIndex].Value == null)
        {
            return;
        }

        try
        {
            if (VrlExists)
            {
                string propName = RemoveSpecialCharacters(dataGridView1[0, e.RowIndex].Value.ToString());
                File.WriteAllText($"{Appdata}\\VRL\\ProperName.txt", propName);
            }

            CurrentAppId = dataGridView1[1, e.RowIndex].Value.ToString();
            Appname = dataGridView1[0, e.RowIndex].Value.ToString().Trim();
            Tat($"{Appname} ({CurrentAppId})");
            searchTextBox.Clear();
            searchTextBox.Enabled = false;
            mainPanel.Visible = true;
            btnManualEntry.Visible = false;
            startCrackPic.Visible = true;
            Tit("READY! Click skull folder above to perform crack!", Color.HotPink);

            resinstruccZip.Visible = false;

            // Auto-crack if enabled
            if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
            {
                // Trigger crack just like clicking the button
                startCrackPic_Click(null, null);
            }
        }
        catch { }
    }

    private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        try
        {
            if (VrlExists)
            {
                string propName = RemoveSpecialCharacters(dataGridView1[0, e.RowIndex].Value.ToString());
                File.WriteAllText($"{Appdata}\\VRL\\ProperName.txt", propName);
            }

            SetSelectedGame(e.RowIndex);
        }
        catch
        {
        }
    }

    private void label2_Click(object sender, EventArgs e)
    {
    }

    private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e.KeyCode == Keys.Up)
            {
                if (dataGridView1.Rows.Count > 0)
                {
                    if (dataGridView1.Rows[0].Selected)
                    {
                        searchTextBox.Focus();
                    }
                }
            }

            if (e.KeyCode == Keys.Escape)
            {
                // Go back to main view
                searchTextBox.Clear();
                mainPanel.Visible = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (dataGridView1[1, CurrentCell].Value == null)
                {
                    return;
                }

                if (VrlExists)
                {
                    string propName = RemoveSpecialCharacters(dataGridView1[0, CurrentCell].Value.ToString());
                    File.WriteAllText($"{Appdata}\\VRL\\ProperName.txt", propName);
                }

                CurrentAppId = dataGridView1[1, CurrentCell].Value.ToString();
                Appname = dataGridView1[0, CurrentCell].Value.ToString().Trim();
                Tat($"{Appname} ({CurrentAppId})");
                searchTextBox.Clear();
                searchTextBox.Enabled = false;
                mainPanel.Visible = true;
                btnManualEntry.Visible = false;
                startCrackPic.Visible = true;
                Tit("READY! Click skull folder above to perform crack!", Color.HotPink);
            }
            else if (e.KeyCode == Keys.Back)
            {
                string s = searchTextBox.Text;

                if (s.Length > 1)
                {
                    s = s.Substring(0, s.Length - 1);
                }
                else
                {
                    s = "0";
                }

                searchTextBox.Text = s;
                searchTextBox.Focus();
                dataGridView1.ClearSelection();
                searchTextBox.SelectionStart = searchTextBox.Text.Length;
            }
            // Removed: type-to-search from dataGridView - was causing issues with held keys
            else if (e.KeyCode == Keys.F1)
            {
                ManAppPanel.Visible = true;
                ManAppBox.Focus();
                ManAppPanel.BringToFront();
                ManAppBtn.Visible = true;
            }
            else if (e.KeyCode == Keys.Control | e.KeyCode == Keys.I)
            {
                ManAppPanel.Visible = true;
                ManAppBox.Focus();
                ManAppPanel.BringToFront();
                ManAppBtn.Visible = true;
            }
        }
        catch { }
    }


    private void searchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e is { Modifiers: Keys.LShiftKey, KeyCode: Keys.Oem4 })
            {
                searchTextBox.Text = $"${searchTextBox.Text}";
                searchTextBox.SelectionStart = searchTextBox.Text.Length + 1;
            }

            if (e is { Modifiers: Keys.Control, KeyCode: Keys.A })
            {
                searchTextBox.SelectAll();
                e.SuppressKeyPress = true;
            }

            if (dataGridView1.Rows.Count == 0)
            {
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                dataGridView1.Focus();
                dataGridView1.Rows[0].Selected = true;
                dataGridView1.SelectedCells[0].Selected = true;

                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                dataGridView1.Focus();
                dataGridView1.Rows[0].Selected = true;
                dataGridView1.SelectedCells[0].Selected = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // ENTER PRESSED - GO FUCKING NUTS!
                if (dataGridView1.Rows.Count == 1)
                {
                    // ONE MATCH - AUTO SELECT AND NUT GALLONS!
                    SetSelectedGame(0);
                    e.SuppressKeyPress = true;
                }
                else if (dataGridView1.Rows.Count > 1)
                {
                    // Multiple matches - select the first or highlighted one
                    if (dataGridView1.CurrentRow != null)
                    {
                        SetSelectedGame(dataGridView1.CurrentRow.Index);
                    }
                    else
                    {
                        SetSelectedGame(0);
                    }

                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                // Go back to main view
                searchTextBox.Clear();
                mainPanel.Visible = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                BackPressed = true;
            }
            else if (e is { Modifiers: Keys.Control, KeyCode: Keys.I })
            {
                ManAppPanel.Visible = true;
                ManAppBox.Focus();
                ManAppPanel.BringToFront();
                ManAppBtn.Visible = true;
            }
            else if (e.KeyCode == Keys.F1)
            {
                ManAppPanel.Visible = true;
                ManAppBox.Focus();
                ManAppPanel.BringToFront();
                ManAppBtn.Visible = true;
            }
            else
            {
                BackPressed = false;
                SearchPause = false;
                T1.Stop();
            }
        }
        catch
        {
            // ignored
        }
    }

    public static void t1_Tick(object sender, EventArgs e)
    {
        BackPressed = false;
        SearchPause = false;
        T1.Stop();
    }

    private async Task PutTaskDelay()
    {
        await Task.Delay(1000);
    }

    private async void searchTextBox_TextChanged(object sender, EventArgs e)
    {
        if (SearchPause && searchTextBox.Text.Length > 0)
        {
            return;
        }

        try
        {
            if (_textChanged)
            {
                await PutTaskDelay();
                _textChanged = false; // Reset the flag
                return;
            }

            _textChanged = true;
            string searchText = searchTextBox.Text.Trim();

            // BLIND SPOT DETECTION: Try to catch exact matches that are being missed
            if (searchText.Length > 2)
            {
                // First, let's see if there's an exact match hiding in the full dataset
                var dt = (DataTable)dataGridView1.DataSource;

                // Check for exact matches manually (case-insensitive)
                var exactMatches = dt.AsEnumerable().Where(row =>
                    string.Equals(row.Field<string>("Name"), searchText, StringComparison.OrdinalIgnoreCase)).ToList();

                // JACKBOX DEBUG: Special handling for numbered games that might have filtering issues
                if (!exactMatches.Any() && searchText.ToLower().Contains("party pack"))
                {
                    // Look for all party pack entries and see what's different
                    var allPartyPacks = dt.AsEnumerable().Where(row =>
                        row.Field<string>("Name")?.ToLower().Contains("party pack") == true).ToList();

                    // Try to find the exact match by checking each one manually
                    foreach (var pack in allPartyPacks)
                    {
                        string packName = pack.Field<string>("Name");
                        if (string.Equals(packName, searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            exactMatches.Add(pack);
                            break;
                        }
                    }
                }

                if (exactMatches.Any())
                {
                    // Found exact match! Apply filter to show only this
                    string exactName = exactMatches.First().Field<string>("Name");
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                        $"Name = '{exactName.Replace("'", "''")}'";
                    goto SearchComplete;
                }

                // SOUNDTRACK/DLC PRIORITY FIX: Check if we're finding longer titles when shorter exact ones exist
                if (searchText.Length > 3)
                {
                    var partialMatches = dt.AsEnumerable().Where(row =>
                        row.Field<string>("Name")?.ToLower().Contains(searchText.ToLower()) == true).ToList();

                    if (partialMatches.Any())
                    {
                        // Prioritize shorter matches (base games) over longer ones (soundtracks, DLC, etc.)
                        var sortedMatches = partialMatches
                            .OrderBy(row => row.Field<string>("Name")?.Length ?? int.MaxValue).ToList();

                        // Check if the shortest match is actually an exact match or very close
                        var bestMatch = sortedMatches.First();
                        string bestMatchName = bestMatch.Field<string>("Name");

                        // If the shortest match equals our search text, use it
                        if (string.Equals(bestMatchName, searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                                $"Name = '{bestMatchName.Replace("'", "''")}'";
                            goto SearchComplete;
                        }

                        // If we have multiple matches, prefer the one without extra words like "Soundtrack", "DLC", etc.
                        if (partialMatches.Count > 1)
                        {
                            var baseGameMatches = partialMatches.Where(row =>
                            {
                                string name = row.Field<string>("Name")?.ToLower() ?? "";
                                return !name.Contains("soundtrack") && !name.Contains("dlc") &&
                                       !name.Contains("season pass") && !name.Contains("expansion") &&
                                       !name.Contains("demo") && !name.Contains("beta");
                            }).OrderBy(row => row.Field<string>("Name")?.Length ?? int.MaxValue).ToList();

                            if (baseGameMatches.Any())
                            {
                                string baseGameName = baseGameMatches.First().Field<string>("Name");
                                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                                    $"Name = '{baseGameName.Replace("'", "''")}'";
                                goto SearchComplete;
                            }
                        }
                    }
                }

                // Check for matches with different Unicode normalization
                var normalizedSearch = searchText.Normalize(NormalizationForm.FormKC);
                var normalizedMatches = dt.AsEnumerable().Where(row =>
                    string.Equals(row.Field<string>("Name")?.Normalize(NormalizationForm.FormKC), normalizedSearch,
                        StringComparison.OrdinalIgnoreCase)).ToList();

                if (normalizedMatches.Any())
                {
                    string exactName = normalizedMatches.First().Field<string>("Name");
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                        $"Name = '{exactName.Replace("'", "''")}'";
                    goto SearchComplete;
                }

                // Check for matches ignoring certain problematic characters
                string cleanSearch = new string(searchText.Where(c =>
                        !char.IsControl(c) &&
                        c != '\u00A0' && // Non-breaking space
                        c != '\u2009' && // Thin space
                        c != '\u200B' && // Zero-width space
                        c != '\uFEFF' // Byte order mark
                ).ToArray()).Trim();

                if (cleanSearch != searchText)
                {
                    var cleanMatches = dt.AsEnumerable().Where(row =>
                    {
                        string rowName = row.Field<string>("Name");
                        if (rowName == null)
                        {
                            return false;
                        }

                        string cleanRowName = new string(rowName.Where(c =>
                            !char.IsControl(c) && c != '\u00A0' && c != '\u2009' && c != '\u200B' && c != '\uFEFF'
                        ).ToArray()).Trim();
                        return string.Equals(cleanRowName, cleanSearch, StringComparison.OrdinalIgnoreCase);
                    }).ToList();

                    if (cleanMatches.Any())
                    {
                        string exactName = cleanMatches.First().Field<string>("Name");
                        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                            $"Name = '{exactName.Replace("'", "''")}'";
                        goto SearchComplete;
                    }
                }
            }

            // Check if DataSource is ready
            if (dataGridView1.DataSource == null)
            {
                return;
            }

            string search = RemoveSpecialCharacters(searchTextBox.Text.ToLower()).Trim();
            string splitSearch = SplitCamelCase(RemoveSpecialCharacters(searchTextBox.Text)).Trim();
            ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                string.Format("Name like '" + search.Replace("_", "").Trim() + "'");

            if (dataGridView1.Rows.Count == 0)
            {
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                    string.Format("Name like '" + splitSearch.Replace("_", "").Trim() + "'");
            }

            if (dataGridView1.Rows.Count == 0)
            {
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
                    splitSearch.ToLower().Replace("_", "").Replace("vr", "").Replace("vrs", "").Trim() + "'");
            }

            if (dataGridView1.Rows.Count == 0)
            {
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
                    splitSearch.ToLower().Replace("'", "").Replace("-", "").Replace(";", "").Trim() + "'");
            }

            if (dataGridView1.Rows.Count == 0)
            {
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '%" +
                    search.Replace("_", "").Replace(" ", "%' AND Name LIKE '%").Replace(" and ", " ")
                        .Replace(" the ", " ").Replace(":", "") + "%'");
                if (dataGridView1.Rows.Count > 0)
                {
                    Clipboard.SetText($"{dataGridView1.Rows[0].Cells[1].Value}");
                }
                else
                {
                    ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string
                        .Format("Name like '%" + splitSearch.Replace("_", "").Replace(" ", "%' AND Name LIKE '%")
                            .Replace(" and ", " ").Replace(" the ", " ").Replace(":", "") + "%'").Trim();
                    if (dataGridView1.Rows.Count > 0)
                    {
                        // Don't auto-select here - let it fall through to SearchComplete
                    }
                    else
                    {
                        search = searchTextBox.Text.ToLower();
                        if (search.StartsWith("$"))
                        {
                            ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '" +
                                search.Replace("$", "").Replace("&", "and").Replace(":", " -") + "'");
                        }
                        else
                        {
                            search = RemoveSpecialCharacters(searchTextBox.Text.ToLower());
                            ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("Name like '%" +
                                search.Replace(" ", "%' AND Name LIKE '%").Replace(" and ", "").Replace(" & ", "")
                                    .Replace(":", "") + "%'");
                        }

                        dataGridView1.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
                        dataGridView1.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;
                        if (BackPressed)
                        {
                            SearchPause = true;
                            T1.Start();
                        }

                        dataGridView1.ClearSelection();
                        dataGridView1.DefaultCellStyle.SelectionBackColor = dataGridView1.DefaultCellStyle.BackColor;
                        dataGridView1.DefaultCellStyle.SelectionForeColor = dataGridView1.DefaultCellStyle.ForeColor;

                        // ENHANCED FALLBACK: If still no results, try improved cleaning and offer manual input
                        if (dataGridView1.Rows.Count == 0 && searchTextBox.Text.Length > 2)
                        {
                            string improvedClean = ImprovedGameNameCleaning(searchTextBox.Text);

                            // Try with improved cleaning
                            ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                                string.Format("Name like '%" + improvedClean.Replace(" ", "%' AND Name LIKE '%") +
                                              "%'");

                            // If still no match, try individual significant words
                            if (dataGridView1.Rows.Count == 0)
                            {
                                string[] words = improvedClean.Split([' '],
                                    StringSplitOptions.RemoveEmptyEntries);
                                if (words.Length > 1)
                                {
                                    var significantWords = words.Where(w => w.Length > 3)
                                        .OrderByDescending(w => w.Length).Take(2);
                                    foreach (string word in significantWords)
                                    {
                                        ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter =
                                            string.Format("Name like '%" + word + "%'");
                                        if (dataGridView1.Rows.Count > 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }

                            // If STILL no match, just show a status message - NO POPUP
                            if (dataGridView1.Rows.Count == 0)
                            {
                                Tit(
                                    $"No matches found for '{searchTextBox.Text}'. Try a different name or use Manual Entry button.",
                                    Color.Orange);
                                btnManualEntry.Visible = true; // Make sure manual entry button is visible
                                resinstruccZip.Visible = true;
                                _resinstruccZipTimer.Stop();
                                _resinstruccZipTimer.Start(); // Start 30 second timer
                                mainPanel.Visible = false; // Hide main panel so dataGridView is visible
                                searchTextBox.Enabled = true; // Enable search when AppID panel shows
                                // Don't show any popup - user can click Manual Entry if they want
                            }
                        }
                    }
                }
            }

            // Always go to SearchComplete to handle the results

            SearchComplete:
            // Only auto-select if this is the INITIAL folder search AND exactly 1 match
            if (dataGridView1.Rows.Count == 1)
            {
                if (_isInitialFolderSearch)
                {
                    // This is THE initial folder search with 1 match - auto-select it
                    SetSelectedGame(0);
                }
                else
                {
                    // User is manually searching - don't auto-select
                    Tit($"1 match: {dataGridView1[0, 0].Value} - Press Enter to select", Color.LightGreen);
                }
            }
            else if (dataGridView1.Rows.Count > 1)
            {
                Tit($"{dataGridView1.Rows.Count} matches found", Color.LightSkyBlue);
                // More than 1 match = no perfect match, show manual entry option
                btnManualEntry.Visible = true;
                resinstruccZip.Visible = true;
                _resinstruccZipTimer.Stop();
                _resinstruccZipTimer.Start(); // Start 30 second timer
            }

            // Clear the initial folder search flag - any further typing is manual
            _isInitialFolderSearch = false;

            // Fallback: If we still have no game selected but we do have search results, ensure buttons are visible
            if (string.IsNullOrEmpty(CurrentAppId) && dataGridView1.Rows.Count > 1)
            {
                btnManualEntry.Visible = true;
                resinstruccZip.Visible = true;
                _resinstruccZipTimer.Stop();
                _resinstruccZipTimer.Start(); // Start 30 second timer
            }

            _textChanged = false;
        }
        catch { }
        // Remove duplicate auto-selection code that was causing issues
    }

    private void dataGridView1_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        if (VrlExists)
        {
            string propName = RemoveSpecialCharacters(dataGridView1[0, e.RowIndex].Value.ToString());
            File.WriteAllText($"{Appdata}\\VRL\\ProperName.txt", propName);
        }

        Tit("READY! Click skull folder above to perform crack!", Color.LightSkyBlue);
        CurrentAppId = dataGridView1[1, CurrentCell].Value.ToString();
        Appname = dataGridView1[0, CurrentCell].Value.ToString().Trim();
        Tat($"{Appname} ({CurrentAppId})");
        searchTextBox.Clear();
        searchTextBox.Enabled = false;
        mainPanel.Visible = true;
        btnManualEntry.Visible = false;
        startCrackPic.Visible = true;
        resinstruccZip.Visible = false;

        // Auto-crack if enabled
        if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
        {
            Tit("Autocracking...", Color.Yellow);
            // Trigger crack just like clicking the button
            startCrackPic_Click(null, null);
        }
        else
        {
            Tit("READY! Click skull folder above to perform crack!", Color.LightSkyBlue);
        }
    }

    private void pictureBox1_Click(object sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/harryeffinpotter/SteamAPPIDFinder",
            UseShellExecute = true
        });
    }

    private void searchTextBox_Enter(object sender, EventArgs e)
    {
    }

    public async Task<bool> CrackAsync() // Return true if something was cracked
    {
        Debug.WriteLine("[CRACK] CrackAsync starting");
        // Use TaskScheduler.Default to avoid capturing UI synchronization context
        var result = await Task.Factory.StartNew(
            async () => await CrackCoreAsync(),
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default
        ).Unwrap();
        Debug.WriteLine($"[CRACK] CrackAsync completed, result: {result}");
        return result;
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        // Ensure paths end with separator for proper replacement
        if (!sourceDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            sourceDir += Path.DirectorySeparatorChar;
        }

        if (!targetDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            targetDir += Path.DirectorySeparatorChar;
        }

        // Create target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);

        // Create all directories
        foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = dirPath.Substring(sourceDir.Length);
            Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
        }

        // Copy all files
        foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string relativePath = filePath.Substring(sourceDir.Length);
            string targetPath = Path.Combine(targetDir, relativePath);
            File.Copy(filePath, targetPath, true);
        }
    }

    private async Task<bool> HandlePermissionError()
    {
        Tit("Showing permission error dialog...", Color.Yellow);

        DialogResult result = MessageBox.Show(
            "Permission error: Unable to modify files in the selected game folder.\n\n" +
            "Would you like to copy the game folder to your desktop and perform the action there?",
            "Permission Error",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string gameFolderName = Path.GetFileName(_gameDir);
            string newGameDir = Path.Combine(desktopPath, gameFolderName);

            try
            {
                Tit($"Copying game from {_gameDir} to Desktop...", Color.Yellow);

                // Copy all files recursively
                await Task.Run(() => CopyDirectory(_gameDir, newGameDir));

                // Update gameDir to the new location
                _gameDir = newGameDir;
                Tat(_gameDir); // Update the displayed directory

                Tit("Game copied to Desktop! Retrying crack with new location...", Color.Lime);

                // Return true to retry the crack - we're already IN a crack attempt when this error happened
                // The user already clicked crack, so continue with the same APPID
                return true; // Tell CrackCoreAsync to retry with the new location
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy game: {ex.Message}", "Copy Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Compares two files byte-by-byte to see if they are identical
    /// </summary>
    private bool AreFilesIdentical(string file1, string file2)
    {
        // Delegate to cracking service for file comparison
        var crackingService = new CrackingService(BinPath);
        return crackingService.AreFilesIdentical(file1, file2);
    }

    /// <summary>
    ///     Restores all .bak files in a directory (recursive)
    /// </summary>
    private void RestoreAllBakFiles(string directory)
    {
        try
        {
            var bakFiles = Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories);
            foreach (var bakFile in bakFiles)
            {
                var originalFile = bakFile.Substring(0, bakFile.Length - 4); // Remove .bak
                try
                {
                    if (File.Exists(originalFile))
                    {
                        File.Delete(originalFile);
                    }

                    File.Move(bakFile, originalFile);
                    Debug.WriteLine(
                        $"[CRACK] Restored {Path.GetFileName(bakFile)} -> {Path.GetFileName(originalFile)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CRACK] Failed to restore {bakFile}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CRACK] Error in RestoreAllBakFiles: {ex.Message}");
        }
    }

    private async Task<bool> CrackCoreAsync() // Core cracking logic moved to separate method
    {
        int execount = -20;
        int steam64Count = -1;
        int steamcount = -1;
        string parentdir = "";

        bool cracked = false;
        bool steamlessUnpacked = false; // Track if Steamless unpacked anything
        string originalGameDir = _gameDir; // Keep track of original location

        // Initialize crack details tracking
        CurrentCrackDetails = new CrackDetails
        {
            GameName = _gameDirName ?? Path.GetFileName(_gameDir), GamePath = _gameDir, AppId = CurrentAppId
        };

        Debug.WriteLine("[CRACK] === Starting CrackCoreAsync ===");
        Debug.WriteLine($"[CRACK] Game Directory: {_gameDir}");
        Debug.WriteLine($"[CRACK] AppID: {CurrentAppId}");
        Debug.WriteLine($"[CRACK] Current Directory: {Environment.CurrentDirectory}");
        LogHelper.Log(
            $"[CRACK] === Starting crack for: {_gameDirName ?? Path.GetFileName(_gameDir)} (AppID: {CurrentAppId}) ===");
        LogHelper.Log($"[CRACK] Path: {_gameDir}");

        try
        {
            var files = Directory.GetFiles(_gameDir, "*.*", SearchOption.AllDirectories);
            Debug.WriteLine($"[CRACK] Found {files.Length} files total");
            LogHelper.Log($"[CRACK] Found {files.Length} files, scanning for DLLs and EXEs...");


            foreach (string file in files)
            {
                if (file.EndsWith("steam_api64.dll"))
                {
                    Debug.WriteLine($"[CRACK] Found steam_api64.dll: {file}");
                    steam64Count++;
                    parentdir = Directory.GetParent(file).FullName;
                    string steam = $"{parentdir}\\steam_settings";
                    if (Directory.Exists(steam))
                    {
                        var filesz = Directory.GetFiles(steam, "*.*", SearchOption.AllDirectories);
                        foreach (var filee in filesz)
                        {
                            File.Delete(filee);
                        }

                        Directory.Delete(steam, true);
                    }

                    // Restore .bak if it exists (get clean file first)
                    if (File.Exists($"{file}.bak"))
                    {
                        Debug.WriteLine("[CRACK] Found existing .bak, restoring clean file first");
                        File.Delete(file);
                        File.Move($"{file}.bak", file);
                    }

                    Tit("Replacing steam_api64.dll.", Color.LightSkyBlue);
                    string emulatorName = Goldy ? "Goldberg" : "ALI213";
                    CrackStatusChanged?.Invoke(this, $"Applying {emulatorName} emulator...");

                    try
                    {
                        string sourceEmulatorDll = Goldy
                            ? $"{BinPath}\\Goldberg\\steam_api64.dll"
                            : $"{BinPath}\\ALI213\\steam_api64.dll";

                        // Check if current file is already the emulator DLL
                        if (AreFilesIdentical(file, sourceEmulatorDll))
                        {
                            Debug.WriteLine(
                                $"[CRACK] steam_api64.dll is already {emulatorName}, skipping replacement to preserve clean .bak");
                            Tit($"steam_api64.dll already {emulatorName}, skipped", Color.Yellow);
                            cracked = true;
                        }
                        else
                        {
                            // Create backup and replace with emulator
                            File.Move(file, $"{file}.bak");
                            CurrentCrackDetails.DllsBackedUp.Add(file);
                            if (Goldy)
                            {
                                Directory.CreateDirectory(steam);
                            }

                            File.Copy(sourceEmulatorDll, file);
                            CurrentCrackDetails.DllsReplaced.Add($"{file} ({emulatorName})");
                            cracked = true;
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // Handle permission error
                        Tit($"Permission error detected: {ex.Message}", Color.Orange);
                        if (await HandlePermissionError())
                        {
                            return await CrackCoreAsync(); // Retry with new location
                        }

                        return false;
                    }

                    if (!Goldy)
                    {
                        if (File.Exists(parentdir + "\\SteamConfig.ini"))
                        {
                            File.Delete(parentdir + "\\SteamConfig.ini");
                        }

                        IniFileEdit($"{BinPath}\\ALI213\\SteamConfig.ini [Settings] \"AppID = {CurrentAppId}\"");
                        File.Copy($"{BinPath}\\ALI213\\SteamConfig.ini", $"{parentdir}\\SteamConfig.ini");
                    }
                }

                if (file.EndsWith("steam_api.dll"))
                {
                    Debug.WriteLine($"[CRACK] Found steam_api.dll: {file}");
                    steamcount++;
                    parentdir = Directory.GetParent(file).FullName;
                    string steam = $"{parentdir}\\steam_settings";
                    if (Directory.Exists(steam))
                    {
                        var filesz = Directory.GetFiles(steam, "*.*", SearchOption.AllDirectories);
                        foreach (var filee in filesz)
                        {
                            File.Delete(filee);
                        }

                        Directory.Delete(steam, true);
                    }

                    // Restore .bak if it exists (get clean file first)
                    if (File.Exists($"{file}.bak"))
                    {
                        Debug.WriteLine("[CRACK] Found existing .bak, restoring clean file first");
                        File.Delete(file);
                        File.Move($"{file}.bak", file);
                    }

                    Tit("Replacing steam_api.dll.", Color.LightSkyBlue);
                    parentdir = Directory.GetParent(file).FullName;

                    try
                    {
                        string sourceEmulatorDll = Goldy
                            ? $"{BinPath}\\Goldberg\\steam_api.dll"
                            : $"{BinPath}\\ALI213\\steam_api.dll";

                        // Check if current file is already the emulator DLL
                        string emulatorName = Goldy ? "Goldberg" : "ALI213";
                        if (AreFilesIdentical(file, sourceEmulatorDll))
                        {
                            Debug.WriteLine(
                                $"[CRACK] steam_api.dll is already {emulatorName}, skipping replacement to preserve clean .bak");
                            Tit($"steam_api.dll already {emulatorName}, skipped", Color.Yellow);
                            cracked = true;
                        }
                        else
                        {
                            // Create backup and replace with emulator
                            File.Move(file, $"{file}.bak");
                            CurrentCrackDetails.DllsBackedUp.Add(file);
                            if (Goldy)
                            {
                                Directory.CreateDirectory(steam);
                            }

                            File.Copy(sourceEmulatorDll, file);
                            CurrentCrackDetails.DllsReplaced.Add($"{file} ({emulatorName})");
                            cracked = true;
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // Handle permission error
                        Tit($"Permission error detected: {ex.Message}", Color.Orange);
                        if (await HandlePermissionError())
                        {
                            return await CrackCoreAsync(); // Retry with new location
                        }

                        return false;
                    }

                    if (!Goldy)
                    {
                        try
                        {
                            if (File.Exists(parentdir + "\\SteamConfig.ini"))
                            {
                                File.Delete(parentdir + "\\SteamConfig.ini");
                            }

                            IniFileEdit($"\".\\_bin\\ALI213\\SteamConfig.ini\" [Settings] \"AppID = {CurrentAppId}\"");
                            File.Copy("_bin\\ALI213\\SteamConfig.ini", $"{parentdir}\\SteamConfig.ini");
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            Tit($"Permission error on SteamConfig.ini: {ex.Message}", Color.Orange);
                            if (await HandlePermissionError())
                            {
                                return await CrackCoreAsync();
                            }

                            return false;
                        }
                    }
                }

                if (Path.GetExtension(file) == ".exe")
                {
                    // NEW: Skip utility executables using CrackConstants
                    string fileName = Path.GetFileName(file);
                    if (CrackConstants.IsExcludedExecutable(fileName))
                    {
                        LogHelper.Log($"[CRACK] Skipping utility executable: {fileName}");
                        CurrentCrackDetails.ExesSkipped.Add(fileName);
                        continue; // Skip to next file
                    }

                    // Skip if file path is empty or file doesn't exist
                    if (string.IsNullOrEmpty(file) || !File.Exists(file))
                    {
                        Debug.WriteLine($"[CRACK] Invalid or missing file, skipping: {file}");
                        continue;
                    }

                    // Check if Steamless CLI exists before trying to use it (CLI version runs without GUI)
                    string steamlessPath = $"{BinPath}\\Steamless\\Steamless.CLI.exe";
                    Debug.WriteLine($"[CRACK] Checking for Steamless CLI at: {steamlessPath}");
                    Debug.WriteLine($"[CRACK] Steamless CLI exists: {File.Exists(steamlessPath)}");

                    if (!File.Exists(steamlessPath))
                    {
                        Debug.WriteLine($"[CRACK] Steamless CLI not found, skipping EXE unpacking for: {file}");
                        LogHelper.Log($"[STEAMLESS] ERROR: Steamless.CLI.exe not found at {steamlessPath}");
                        Tit($"Steamless.CLI.exe not found at {steamlessPath}, skipping EXE unpacking", Color.Yellow);
                        continue;
                    }

                    LogHelper.Log($"[STEAMLESS] Processing: {file}");
                    Debug.WriteLine($"[CRACK] Processing EXE: {file}");
                    CrackStatusChanged?.Invoke(this, $"Attempting to apply Steamless to {Path.GetFileName(file)}...");

                    // Track all EXEs we attempt to process
                    CurrentCrackDetails.ExesTried.Add(Path.GetFileName(file));

                    // Restore .bak if it exists (apply Steamless to clean exe)
                    if (File.Exists($"{file}.bak"))
                    {
                        Debug.WriteLine("[CRACK] Found existing .bak, restoring clean exe first");
                        File.Delete(file);
                        File.Move($"{file}.bak", file);
                    }

                    string exeparent = Directory.GetParent(file).FullName;
                    var x2 = new Process();
                    var pro = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WorkingDirectory = parentdir,
                        FileName = steamlessPath,
                        Arguments = $"\"{file}\""
                    };

                    Debug.WriteLine("[CRACK] Starting Steamless process");
                    Debug.WriteLine($"[CRACK] Working Directory: {parentdir}");
                    Debug.WriteLine($"[CRACK] Command: {pro.Arguments}");

                    x2.StartInfo = pro;
                    x2.Start();

                    // Read output asynchronously to avoid blocking
                    var outputTask = x2.StandardOutput.ReadToEndAsync();
                    var errorTask = x2.StandardError.ReadToEndAsync();

                    await Task.Run(() => x2.WaitForExit());

                    string output = await errorTask + await outputTask;

                    Debug.WriteLine($"[CRACK] Steamless output: {output}");
                    Debug.WriteLine($"[CRACK] Steamless exit code: {x2.ExitCode}");
                    LogHelper.Log($"[STEAMLESS] Exit code: {x2.ExitCode}, Output: {output.Trim()}");

                    if (File.Exists($"{file}.unpacked.exe"))
                    {
                        Debug.WriteLine($"[CRACK] Successfully unpacked: {file}");
                        LogHelper.Log($"[STEAMLESS] SUCCESS - Unpacked: {Path.GetFileName(file)}");
                        Tit($"Unpacked {file} successfully!", Color.LightSkyBlue);
                        File.Move(file, file + ".bak");
                        File.Move($"{file}.unpacked.exe", file);
                        steamlessUnpacked = true; // Mark that we unpacked something
                        CurrentCrackDetails.ExesUnpacked.Add(file);
                    }
                    else
                    {
                        Debug.WriteLine($"[CRACK] No unpacked file created for: {file}");
                        LogHelper.Log($"[STEAMLESS] No stub detected: {Path.GetFileName(file)}");
                    }
                }

                if (steamcount > 1 || execount > 1 || steam64Count > 1)
                {
                    DialogResult diagg = MessageBox.Show(
                        "This is the 2nd steam_api64.dll on this run - something is broken. " +
                        "The APPID has to match the ini/txt files or the cracks will not work.\n\n" +
                        "This usually happens when SACGUI determines the wrong parent dir " +
                        "from EXE or when user selects incorrect folder. Please select " +
                        "the GAME DIRECTORY, for example:\n\n" +
                        "-CORRECT-\nD:\\SteamLibrary\\Common\\SomeGameName\n\n" +
                        "-INCORRECT-\nD:\\SteamLibrary\\Common\n\n" +
                        "If you think this message is wrong, verify the path on bottom left and hit YES to continue..",
                        "Somethings wrong..., continue?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (diagg == DialogResult.Yes)
                    {
                        execount = -600;
                        steam64Count = -600;
                        steamcount = -600;
                    }
                    else
                    {
                        break;
                    }
                }
            }


            if (cracked)
            {
                if (Goldy)
                {
                    if (Directory.Exists($"{parentdir}\\steam_settings"))
                    {
                        Directory.Delete($"{parentdir}\\steam_settings", true);
                    }

                    Directory.CreateDirectory($"{parentdir}\\steam_settings");

                    // Create steam_appid.txt with just the AppId - this is what gbe_fork expects
                    File.WriteAllText($"{parentdir}\\steam_settings\\steam_appid.txt", CurrentAppId);

                    // Check if user wants LAN multiplayer support (from checkbox)
                    if (EnableLanMultiplayer)
                    {
                        // Create custom_broadcasts.txt for multiplayer discovery
                        // Just a simple list of IPs, one per line
                        string broadcastContent =
                            "255.255.255.255\n" + // Broadcast to local network
                            "127.0.0.1\n"; // Localhost

                        File.WriteAllText($"{parentdir}\\steam_settings\\custom_broadcasts.txt", broadcastContent);

                        // Copy lobby_connect tool to game directory for easy access
                        string lobbyConnectSource = "";
                        // Check in _bin folder first (where updater puts them)
                        if (File.Exists($"{BinPath}\\lobby_connect_x64.exe"))
                        {
                            lobbyConnectSource = $"{BinPath}\\lobby_connect_x64.exe";
                        }
                        else if (File.Exists($"{BinPath}\\lobby_connect_x32.exe"))
                        {
                            lobbyConnectSource = $"{BinPath}\\lobby_connect_x32.exe";
                        }
                        // Fallback to _bin\\Goldberg for backward compatibility
                        else if (File.Exists($"{BinPath}\\Goldberg\\lobby_connect_x64.exe"))
                        {
                            lobbyConnectSource = $"{BinPath}\\Goldberg\\lobby_connect_x64.exe";
                        }
                        else if (File.Exists($"{BinPath}\\Goldberg\\lobby_connect_x32.exe"))
                        {
                            lobbyConnectSource = $"{BinPath}\\Goldberg\\lobby_connect_x32.exe";
                        }

                        if (!string.IsNullOrEmpty(lobbyConnectSource))
                        {
                            // Copy lobby_connect tool to GAME directory (not crack directory)
                            string lobbyConnectFileName =
                                Path.GetFileName(lobbyConnectSource); // e.g. lobby_connect_x64.exe
                            string lobbyConnectDest =
                                Path.Combine(_gameDir, $"_{lobbyConnectFileName}"); // e.g. _lobby_connect_x64.exe

                            Tit($"Copying lobby_connect to game folder: {_gameDir}", Color.Yellow);
                            File.Copy(lobbyConnectSource, lobbyConnectDest, true);

                            // Find the main game executable IN THE GAME DIRECTORY
                            string gameExe = "";
                            var exeFiles = Directory.GetFiles(_gameDir, "*.exe", SearchOption.TopDirectoryOnly)
                                .Where(f => !f.Contains("lobby_connect") &&
                                            !f.Contains("UnityCrashHandler") &&
                                            !f.Contains("unins") &&
                                            !f.Contains("setup"))
                                .ToList();

                            if (exeFiles.Count == 1)
                            {
                                gameExe = exeFiles[0];
                            }
                            else if (exeFiles.Count > 1)
                            {
                                // Try to find the most likely game exe
                                gameExe = exeFiles.FirstOrDefault(f =>
                                              Path.GetFileNameWithoutExtension(f).ToLower()
                                                  .Contains(_gameDirName.ToLower()))
                                          ?? exeFiles[0];
                            }

                            // Create a config file for lobby_connect to remember the game exe
                            if (!string.IsNullOrEmpty(gameExe))
                            {
                                Tit($"Found game exe: {Path.GetFileName(gameExe)}", Color.Yellow);

                                // Save the game exe path for lobby_connect
                                string lobbyConfigFile = Path.GetFileNameWithoutExtension(lobbyConnectSource) + ".txt";
                                File.WriteAllText(Path.Combine(_gameDir, lobbyConfigFile), gameExe);

                                // Create batch files in steam_settings folder (tucked away)
                                string steamSettingsPath = Path.Combine(parentdir, "steam_settings");

                                // Join batch - automatically connects and launches the game
                                string lobbyExeName =
                                    "_" + Path.GetFileName(lobbyConnectSource); // e.g. _lobby_connect_x64.exe
                                string joinBatchContent = $@"@echo off
cd /d ""{_gameDir}""
echo Searching for LAN games and connecting...
{lobbyExeName} --connect ""{Path.GetFileName(gameExe)}""
if errorlevel 1 (
    echo Could not find any LAN games. Starting as host instead...
    start """" ""{Path.GetFileName(gameExe)}""
)
exit";

                                // Host batch - just launches the game normally
                                string hostBatchContent = $@"@echo off
cd /d ""{_gameDir}""
start """" ""{Path.GetFileName(gameExe)}""
exit";

                                // Edit broadcasts batch - opens custom_broadcasts.txt in notepad
                                string editBroadcastsContent = $@"@echo off
echo Opening custom_broadcasts.txt for editing...
echo Add your friends' public IPs to play over the internet!
echo.
echo This window will automatically close when you close notepad.
notepad ""{Path.Combine(steamSettingsPath, "custom_broadcasts.txt")}""
exit";

                                // Create batch files in steam_settings folder
                                string joinBatchPath = Path.Combine(steamSettingsPath, "_JoinLAN.bat");
                                string hostBatchPath = Path.Combine(steamSettingsPath, "_HostLAN.bat");
                                string editBroadcastsPath = Path.Combine(steamSettingsPath, "_EditMultiplayerIPs.bat");

                                Tit("Creating batch files in steam_settings folder", Color.Cyan);

                                File.WriteAllText(joinBatchPath, joinBatchContent);
                                File.WriteAllText(hostBatchPath, hostBatchContent);
                                File.WriteAllText(editBroadcastsPath, editBroadcastsContent);

                                // Verify batch files were created
                                if (!File.Exists(joinBatchPath) || !File.Exists(hostBatchPath))
                                {
                                    Tit("ERROR: Failed to create batch files!", Color.Red);
                                }
                                else
                                {
                                    Tit("Batch files created successfully", Color.Lime);
                                }

                                // Create Windows shortcuts in game folder pointing to batch files in steam_settings
                                try
                                {
                                    string gameName = Path.GetFileNameWithoutExtension(gameExe);

                                    // Shortcuts in game folder
                                    string joinShortcutPath = Path.Combine(_gameDir, $"_[Join LAN] {gameName}.lnk");
                                    string hostShortcutPath = Path.Combine(_gameDir, $"_[Host LAN] {gameName}.lnk");
                                    string editIPsShortcutPath = Path.Combine(_gameDir, "_[Edit Multiplayer IPs].lnk");

                                    Tit($"Creating shortcuts in game folder for {gameName}", Color.Yellow);

                                    // Use VBScript to create shortcuts pointing to batch files in steam_settings
                                    string vbsScript = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")

' Join LAN shortcut
Set oLink = oWS.CreateShortcut(""{joinShortcutPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}"")
oLink.TargetPath = ""{joinBatchPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}""
oLink.WorkingDirectory = ""{_gameDir.Replace("\\", "\\\\").Replace("\"", "\"\"")}""
oLink.Description = ""Search and join LAN/Internet games for {gameName.Replace("\"", "\"\"")}""
oLink.IconLocation = ""{gameExe.Replace("\\", "\\\\").Replace("\"", "\\\\")},0""
oLink.Save

' Host LAN shortcut
Set oLink2 = oWS.CreateShortcut(""{hostShortcutPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}"")
oLink2.TargetPath = ""{hostBatchPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}""
oLink2.WorkingDirectory = ""{_gameDir.Replace("\\", "\\\\").Replace("\"", "\"\"")}""
oLink2.Description = ""Host a LAN/Internet game for {gameName.Replace("\"", "\"\"")}""
oLink2.IconLocation = ""{gameExe.Replace("\\", "\\\\").Replace("\"", "\\\\")},0""
oLink2.Save

' Edit Multiplayer IPs shortcut
Set oLink3 = oWS.CreateShortcut(""{editIPsShortcutPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}"")
oLink3.TargetPath = ""{editBroadcastsPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}""
oLink3.WorkingDirectory = ""{steamSettingsPath.Replace("\\", "\\\\").Replace("\"", "\"\"")}""
oLink3.Description = ""Edit custom_broadcasts.txt to add friend IPs for internet play""
oLink3.IconLocation = ""notepad.exe,0""
oLink3.Save";

                                    string tempVbs = Path.GetTempFileName() + ".vbs";
                                    File.WriteAllText(tempVbs, vbsScript);
                                    var vbsProcess = Process.Start("wscript.exe", $"\"{tempVbs}\"");
                                    await Task.Run(() => vbsProcess.WaitForExit());

                                    try { File.Delete(tempVbs); }
                                    catch { }

                                    // Check if at least the main shortcuts were created
                                    if (File.Exists(joinShortcutPath) || File.Exists(hostShortcutPath))
                                    {
                                        Tit("Multiplayer shortcuts created in game folder!", Color.MediumSpringGreen);
                                        Tit("Batch files stored in steam_settings folder", Color.Lime);
                                    }
                                    else
                                    {
                                        Tit("Shortcuts failed, but batch files are ready in steam_settings!",
                                            Color.Yellow);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Tit($"Shortcut error: {ex.Message}", Color.Yellow);
                                    Tit("Batch files ready in steam_settings folder!", Color.MediumSpringGreen);
                                }
                            }
                        }
                    }

                    // Generate interfaces file if the tool exists (required for gbe_fork)
                    // Try different possible locations and names for the generate_interfaces tool
                    string generateInterfacesPath = "";
                    string[] possiblePaths =
                    [
                        $"{BinPath}\\Goldberg\\generate_interfaces_x64.exe",
                        $"{BinPath}\\Goldberg\\generate_interfaces_x32.exe",
                        $"{BinPath}\\generate_interfaces_x64.exe", $"{BinPath}\\generate_interfaces_x32.exe",
                        $"{BinPath}\\Goldberg\\generate_interfaces_file.exe", $"{BinPath}\\generate_interfaces.exe"
                    ];

                    foreach (string path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            generateInterfacesPath = path;
                            break;
                        }
                    }

                    if (File.Exists(generateInterfacesPath))
                    {
                        Tit("Generating steam interfaces...", Color.Cyan);
                        try
                        {
                            // Run generate_interfaces to create steam_interfaces.txt
                            var genInterfaces = new Process();
                            genInterfaces.StartInfo.CreateNoWindow = true;
                            genInterfaces.StartInfo.UseShellExecute = false;
                            genInterfaces.StartInfo.FileName = generateInterfacesPath;
                            genInterfaces.StartInfo.WorkingDirectory = parentdir;
                            // Try to find the original steam dll backup to generate interfaces from
                            string originalDll = "";
                            if (File.Exists($"{parentdir}\\steam_api64.dll.bak"))
                            {
                                originalDll = $"{parentdir}\\steam_api64.dll.bak";
                            }
                            else if (File.Exists($"{parentdir}\\steam_api.dll.bak"))
                            {
                                originalDll = $"{parentdir}\\steam_api.dll.bak";
                            }

                            if (!string.IsNullOrEmpty(originalDll))
                            {
                                genInterfaces.StartInfo.Arguments = $"\"{originalDll}\"";
                            }
                            else
                            {
                                // If no backup found, try with the replaced dll (might not work as well)
                                if (File.Exists($"{parentdir}\\steam_api64.dll"))
                                {
                                    genInterfaces.StartInfo.Arguments = $"\"{parentdir}\\steam_api64.dll\"";
                                }
                                else if (File.Exists($"{parentdir}\\steam_api.dll"))
                                {
                                    genInterfaces.StartInfo.Arguments = $"\"{parentdir}\\steam_api.dll\"";
                                }
                            }

                            genInterfaces.Start();
                            await Task.Run(() => genInterfaces.WaitForExit());

                            // Move generated file to steam_settings if it was created
                            if (File.Exists($"{parentdir}\\steam_interfaces.txt"))
                            {
                                File.Move($"{parentdir}\\steam_interfaces.txt",
                                    $"{parentdir}\\steam_settings\\steam_interfaces.txt");
                            }
                        }
                        catch { }
                    }

                    Tit("Fetching achievements...", Color.Cyan);
                    try
                    {
                        // IYKYK
                        const string config = "YWI3MTE0MzItMzYzMS00ODgyLWI2YzAtODY4ZmYzMzMxNzcx";
                        var parserConfig = Encoding.UTF8.GetString(Convert.FromBase64String(config));

                        const string baseUrl = "aHR0cHM6Ly9hcGkucnVzdGJlYXJkLmNvbQ==";
                        var baseConfig = Encoding.UTF8.GetString(Convert.FromBase64String(baseUrl));
                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("X-Gbe-Auth", parserConfig);

                        var parser = new SteamAchievementParser(httpClient, parserConfig);

                        bool achievementSuccess = await parser.GenerateAchievementsFileAsync(baseConfig,
                            null,
                            CurrentAppId,
                            parentdir
                        );

                        if (achievementSuccess)
                        {
                            Tit("Achievement data saved!", Color.Green);
                        }
                        else
                        {
                            Tit("No achievements found for this game", Color.Yellow);
                        }
                    }
                    catch (Exception ex)
                    {
                        Tit($"Failed to fetch achievements: {ex.Message}", Color.Yellow);
                        Debug.WriteLine($"[ACHIEVEMENTS] Exception: {ex.Message}");
                    }

                    // Get DLC info from Steam Store API (no API key needed)
                    Tit("Getting DLC info from Steam...", Color.Cyan);
                    try
                    {
                        await FetchDlcInfoAsync(CurrentAppId, $"{parentdir}\\steam_settings");
                    }
                    catch (Exception ex)
                    {
                        Tit($"Failed to get DLC info: {ex.Message}", Color.Yellow);
                        Debug.WriteLine($"[DLC] Exception: {ex.Message}");
                    }
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[CRACK] UnauthorizedAccessException: {ex.Message}");
            File.WriteAllText("_CRASHlog.txt", $"[UnauthorizedAccessException]\n{ex.StackTrace}\n{ex.Message}");
            Tit("Permission denied. Try running as administrator or copy game to desktop.", Color.Red);
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CRACK] Exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[CRACK] Stack trace: {ex.StackTrace}");
            File.WriteAllText("_CRASHlog.txt",
                $"[{ex.GetType().Name}]\n{ex.StackTrace}\n{ex.Message}\n\nFull Exception:\n{ex}");
            throw; // Re-throw to let caller handle it
        }

        Debug.WriteLine("[CRACK] === Crack Complete ===");
        Debug.WriteLine($"[CRACK] Cracked: {cracked}, Steamless Unpacked: {steamlessUnpacked}");

        // Finalize crack details
        bool success = cracked || steamlessUnpacked;
        CurrentCrackDetails.Success = success;

        // Check if no steam_api DLLs were found
        if (CurrentCrackDetails.DllsReplaced.Count == 0 && CurrentCrackDetails.DllsBackedUp.Count == 0)
        {
            CurrentCrackDetails.Errors.Add("No steam_api.dll or steam_api64.dll found in game folder");
        }

        // Add to history
        CrackHistory.Add(CurrentCrackDetails);

        Debug.WriteLine(
            $"[CRACK] Details - DLLs replaced: {CurrentCrackDetails.DllsReplaced.Count}, EXEs unpacked: {CurrentCrackDetails.ExesUnpacked.Count}, EXEs skipped: {CurrentCrackDetails.ExesSkipped.Count}");

        // Return true only if we actually cracked something
        return success;
    }

    public void IniFileEdit(string args)
    {
        var iniProcess = new Process();
        iniProcess.StartInfo.CreateNoWindow = true;
        iniProcess.StartInfo.UseShellExecute = false;
        iniProcess.StartInfo.FileName = $"{BinPath}\\ALI213\\inifile.exe";

        iniProcess.StartInfo.Arguments = args;
        iniProcess.Start();
        iniProcess.WaitForExit();
    }

    private async Task FetchDlcInfoAsync(string appId, string outputFolder)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            // Get app details from Steam Store API
            var response =
                await httpClient.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}");
            var json = JObject.Parse(response);

            var appData = json[appId]?["data"];
            if (appData == null || json[appId]?["success"]?.Value<bool>() != true)
            {
                Tit("No DLC info available for this game", Color.Yellow);
                return;
            }

            if (appData["dlc"] is not JArray dlcArray || dlcArray.Count == 0)
            {
                Tit("No DLCs found for this game", Color.Yellow);
                return;
            }

            Tit($"Found {dlcArray.Count} DLCs, fetching names...", Color.Cyan);

            var dlcLines = new List<string>();
            int successCount = 0;

            foreach (var dlcId in dlcArray)
            {
                try
                {
                    var dlcResponse =
                        await httpClient.GetStringAsync(
                            $"https://store.steampowered.com/api/appdetails?appids={dlcId}");
                    var dlcJson = JObject.Parse(dlcResponse);

                    var dlcData = dlcJson[dlcId.ToString()]?["data"];
                    if (dlcData != null && dlcJson[dlcId.ToString()]?["success"]?.Value<bool>() == true)
                    {
                        string dlcName = dlcData["name"]?.Value<string>() ?? "Unknown";
                        dlcLines.Add($"{dlcId}={dlcName}");
                        successCount++;
                    }

                    // Rate limit to avoid Steam throttling
                    await Task.Delay(100);
                }
                catch
                {
                    // Skip DLCs that fail to fetch
                    dlcLines.Add($"{dlcId}=DLC_{dlcId}");
                }
            }

            // Write DLC.txt
            string dlcPath = Path.Combine(outputFolder, "DLC.txt");
            File.WriteAllLines(dlcPath, dlcLines);

            Tit($"Saved {successCount}/{dlcArray.Count} DLC entries!", Color.Green);
        }
        catch (TaskCanceledException)
        {
            throw new Exception("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    // Public method to suppress status updates when cracking from share window
    public void SetSuppressStatusUpdates(bool suppress)
    {
        _suppressStatusUpdates = suppress;
    }

    private void pictureBox2_Click(object sender, EventArgs e)
    {
        var folderSelectDialog = new FolderSelectDialog { Title = "Select the game's main folder." };

        if (_settings.LastDir.Length > 0)
        {
            folderSelectDialog.InitialDirectory = _settings.LastDir;
        }

        if (folderSelectDialog.Show(Handle))
        {
            _gameDir = folderSelectDialog.FileName;

            // Hide crack buttons when starting new game selection
            HideCrackButtons();

            // Always remember parent folder for next time
            try
            {
                var parent = Directory.GetParent(_gameDir);
                if (parent != null)
                {
                    _settings.LastDir = parent.FullName;
                    AppSettings.Default.Save();
                }
            }
            catch { }

            // Check if this is a folder containing multiple games (batch mode)
            var gamesInFolder = DetectGamesInFolder(_gameDir);
            if (gamesInFolder.Count > 1)
            {
                // Multiple games detected - batch folder
                _settings.LastDir = _gameDir;
                AppSettings.Default.Save();
                ShowBatchGameSelection(gamesInFolder);
                return;
            }

            if (gamesInFolder.Count == 1)
            {
                // Contains exactly one game subfolder - use that
                _gameDir = gamesInFolder[0];
                _gameDirName = Path.GetFileName(_gameDir);
            }
            else if (!IsGameFolder(_gameDir))
            {
                // Not a game folder, not a batch folder - check if they selected something weird
                try
                {
                    var steamApiFiles = Directory.GetFiles(_gameDir, "steam_api*.dll", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (steamApiFiles.Count > 2)
                    {
                        // Has steam_api DLLs but we couldn't detect game structure - might be root drive
                        bool continueAnyway = ShowStyledConfirmation(
                            "Unusual Folder Structure",
                            $"Found {steamApiFiles.Count} steam_api DLLs but couldn't detect game folders.\n" +
                            $"Did you accidentally select a root drive or system folder?",
                            _gameDir,
                            "Continue anyway",
                            "Cancel");

                        if (!continueAnyway)
                        {
                            return;
                        }
                    }
                }
                catch { }
            }

            // Hide OpenDir and ZipToShare when new directory selected
            OpenDir.Visible = false;
            OpenDir.SendToBack();
            ZipToShare.Visible = false;
            _parentOfSelection = Directory.GetParent(_gameDir).FullName;
            _gameDirName = Path.GetFileName(_gameDir);

            // Try to get AppID from Steam manifest files
            var manifestInfo = SteamManifestParser.GetAppIdFromManifest(_gameDir);
            if (manifestInfo.HasValue)
            {
                // We found the AppID from manifest!
                CurrentAppId = manifestInfo.Value.appId;
                string manifestGameName = manifestInfo.Value.gameName;
                long sizeOnDisk = manifestInfo.Value.sizeOnDisk;

                Debug.WriteLine("[MANIFEST] Auto-detected from Steam manifest:");
                Debug.WriteLine($"[MANIFEST] AppID: {CurrentAppId}");
                Debug.WriteLine($"[MANIFEST] Game: {manifestGameName}");
                Debug.WriteLine($"[MANIFEST] Size: {sizeOnDisk / (1024 * 1024)} MB");

                // Skip the search UI entirely - we already have the AppID!
                // Just show main panel - it will cover all the search UI elements
                searchTextBox.Enabled = false;
                mainPanel.Visible = true;
                resinstruccZip.Visible = true;
                _resinstruccZipTimer.Stop();
                _resinstruccZipTimer.Start();
                startCrackPic.Visible = true;

                // Skip the search entirely
                _isFirstClickAfterSelection = false;
                _isInitialFolderSearch = false;

                // Auto-crack if enabled
                if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
                {
                    Debug.WriteLine("[MANIFEST] Auto-crack enabled, starting crack...");
                    Tit($"✅ Auto-detected: {manifestGameName} (AppID: {CurrentAppId}) - Auto-cracking...",
                        Color.Yellow);
                    // Trigger crack just like clicking the button
                    startCrackPic_Click(null, null);
                }
                else
                {
                    // Update the title with game info
                    Tit($"✅ Auto-detected: {manifestGameName} (AppID: {CurrentAppId}) - Ready to crack!",
                        Color.LightGreen);
                }
            }
            else
            {
                // No manifest found, proceed with normal search flow
                btnManualEntry.Visible = true;
                resinstruccZip.Visible = true;
                _resinstruccZipTimer.Stop();
                _resinstruccZipTimer.Start(); // Start 30 second timer
                mainPanel.Visible = false; // Hide mainPanel so dataGridView is visible
                searchTextBox.Enabled = true; // Enable search when AppID panel shows

                startCrackPic.Visible = true;
                Tit("Please select the correct game from the list!! (if list empty do manual search!)",
                    Color.LightSkyBlue);

                // Trigger the search
                _isFirstClickAfterSelection = true; // Set before changing text
                _isInitialFolderSearch = true; // This is the initial search from folder
                searchTextBox.Text = _gameDirName;
            }

            // Stop label5 timer when game dir is selected
            _label5Timer.Stop();
            label5.Visible = false;
            _settings.LastDir = _parentOfSelection;
            AppSettings.Default.Save();
        }
        else
        {
            MessageBox.Show("You must select a folder to continue...");
        }
    }

    private async void startCrackPic_Click(object sender, EventArgs e)
    {
        if (!Cracking)
        {
            Cracking = true;

            // Hide OpenDir and ZipToShare during cracking
            OpenDir.Visible = false;
            ZipToShare.Visible = false;

            bool crackedSuccessfully = await CrackAsync();
            Cracking = false;

            // Check if we actually cracked anything
            if (crackedSuccessfully)
            {
                // Set the permanent success message
                Tit("Crack complete!\nSelect another game directory to keep the party going!", Color.LightSkyBlue);

                // Show both buttons centered below game name
                ShowCrackButtons();
            }
            else
            {
                Tit("No files to crack found!", Color.Red);
                await Task.Delay(3000);
                Tit("Click folder & select game's parent directory.", Color.Cyan);

                // Show only Open Dir button centered
                ShowCrackButtons(false);
            }

            startCrackPic.Visible = false;
            donePic.Visible = true;
            donePic.Visible = false;
        }
    }

    private void lanMultiplayerCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        EnableLanMultiplayer = lanMultiplayerCheckBox.Checked;
        AppSettings.Default.LanMultiplayer = EnableLanMultiplayer;
        AppSettings.Default.Save();
    }

    private void dllSelect_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (dllSelect.SelectedIndex == 0)
        {
            Goldy = true;
            _settings.Goldy = true;
            // Show LAN checkbox for Goldberg
            lanMultiplayerCheckBox.Visible = true;
        }
        else if (dllSelect.SelectedIndex == 1)
        {
            Goldy = false;
            _settings.Goldy = false;
            // Hide LAN checkbox for Ali213 (not supported)
            lanMultiplayerCheckBox.Visible = false;
            lanMultiplayerCheckBox.Checked = false;
            EnableLanMultiplayer = false;
        }

        AppSettings.Default.Save();
    }

    private void selectDir_MouseEnter(object sender, EventArgs e)
    {
        selectDir.Image = Resources.hoveradd;
    }

    private void selectDir_MouseHover(object sender, EventArgs e)
    {
        selectDir.Image = Resources.hoveradd;
    }

    private void selectDir_MouseDown(object sender, MouseEventArgs e)
    {
        selectDir.Image = Resources.clickadd;
    }

    private void startCrackPic_MouseDown(object sender, MouseEventArgs e)
    {
        startCrackPic.Image = Resources.clickr2c;
    }

    private void startCrackPic_MouseEnter(object sender, EventArgs e)
    {
        startCrackPic.Image = Resources.hoverr2c;
    }

    private void startCrackPic_MouseHover(object sender, EventArgs e)
    {
        startCrackPic.Image = Resources.hoverr2c;
    }

    private void selectDir_MouseLeave(object sender, EventArgs e)
    {
        selectDir.Image = Resources.add;
    }

    private void startCrackPic_MouseLeave(object sender, EventArgs e)
    {
        startCrackPic.Image = Resources.r2c;
    }

    private void mainPanel_DragEnter(object sender, DragEventArgs e)
    {
        drgdropText.BringToFront();
        drgdropText.Visible = true;
        e.Effect = DragDropEffects.Copy;
    }

    private void mainPanel_DragLeave(object sender, EventArgs e)
    {
        drgdropText.SendToBack();
        drgdropText.Visible = false;
    }

    private void mainPanel_DragDrop(object sender, DragEventArgs e)
    {
        string[] drops = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (string d in drops)
        {
            FileAttributes attr = File.GetAttributes(d);
            //detect whether its a directory or file
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                // Check if it's a root drive - reject it
                if (Path.GetPathRoot(d) == d || d.Length <= 3)
                {
                    Tit("You can't select a root drive! Pick a game folder.", Color.OrangeRed);
                    MessageBox.Show("Nope! You can't crack an entire drive. Please select a game folder instead.",
                        "Nice Try", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                //DIR
                _gameDir = d;

                // Hide OpenDir and ZipToShare when new directory selected
                OpenDir.Visible = false;
                ZipToShare.Visible = false;
                _parentOfSelection = Directory.GetParent(_gameDir).FullName;
                _gameDirName = Path.GetFileName(_gameDir);

                // Try to get AppID from Steam manifest files
                var manifestInfo = SteamManifestParser.GetAppIdFromManifest(_gameDir);
                if (manifestInfo.HasValue)
                {
                    // We found the AppID from manifest!
                    CurrentAppId = manifestInfo.Value.appId;
                    string manifestGameName = manifestInfo.Value.gameName;
                    long sizeOnDisk = manifestInfo.Value.sizeOnDisk;

                    Debug.WriteLine("[MANIFEST] Auto-detected from Steam manifest:");
                    Debug.WriteLine($"[MANIFEST] AppID: {CurrentAppId}");
                    Debug.WriteLine($"[MANIFEST] Game: {manifestGameName}");
                    Debug.WriteLine($"[MANIFEST] Size: {sizeOnDisk / (1024 * 1024)} MB");

                    // Skip the search UI entirely - we already have the AppID!
                    // Just show main panel - it will cover all the search UI elements
                    searchTextBox.Enabled = false;
                    mainPanel.Visible = true;
                    resinstruccZip.Visible = true;
                    _resinstruccZipTimer.Stop();
                    _resinstruccZipTimer.Start();
                    startCrackPic.Visible = true;

                    // Skip the search entirely
                    _isInitialFolderSearch = false;
                    _isFirstClickAfterSelection = false;

                    // Auto-crack if enabled
                    if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
                    {
                        Debug.WriteLine("[MANIFEST] Auto-crack enabled, starting crack...");
                        Tit($"✅ Auto-detected: {manifestGameName} (AppID: {CurrentAppId}) - Auto-cracking...",
                            Color.Yellow);
                        // Trigger crack just like clicking the button
                        startCrackPic_Click(null, null);
                    }
                    else
                    {
                        // Update the title with game info
                        Tit($"✅ Auto-detected: {manifestGameName} (AppID: {CurrentAppId}) - Ready to crack!",
                            Color.LightGreen);
                    }
                }
                else
                {
                    // No manifest found, proceed with normal search flow
                    mainPanel.Visible = false; // Hide mainPanel so dataGridView is visible
                    searchTextBox.Enabled = true; // Enable search when AppID panel shows
                    btnManualEntry.Visible = true;
                    resinstruccZip.Visible = true; // Show this too!
                    _resinstruccZipTimer.Stop();
                    _resinstruccZipTimer.Start(); // Start 30 second timer
                    startCrackPic.Visible = true;
                    Tit("Please select the correct game from the list!! (if list empty do manual search!)",
                        Color.LightSkyBlue);

                    // Trigger the search
                    _isInitialFolderSearch = true; // This is the initial search
                    searchTextBox.Text = _gameDirName;
                    _isFirstClickAfterSelection = true; // Set AFTER changing text to avoid race condition
                }

                // Stop label5 timer when game dir is selected
                _label5Timer.Stop();
                label5.Visible = false;

                _isInitialFolderSearch = true; // This is the initial search
                searchTextBox.Text = _gameDirName;
                _isFirstClickAfterSelection = true; // Set AFTER changing text to avoid race condition
                _settings.LastDir = _parentOfSelection;
                AppSettings.Default.Save();
            }
            else
            {
                //FILE - reject all files
                Tit("Please drag and drop a FOLDER, not a file!", Color.LightSkyBlue);
                MessageBox.Show("Drag and drop a game folder, not individual files!");
            }
        }

        drgdropText.SendToBack();
        drgdropText.Visible = false;
    }

    private void unPin_Click(object sender, EventArgs e)
    {
        TopMost = false;
        pin.BringToFront();
        AppSettings.Default.Pinned = false;
        AppSettings.Default.Save();

        // Unpin share window too if it's open
        if (_shareWindow is { IsDisposed: false })
        {
            _shareWindow.TopMost = false;
        }
    }

    private void pin_Click(object sender, EventArgs e)
    {
        TopMost = true;
        unPin.BringToFront();
        AppSettings.Default.Pinned = true;
        AppSettings.Default.Save();

        // Pin share window too if it's open
        if (_shareWindow is { IsDisposed: false })
        {
            _shareWindow.TopMost = true;
        }
    }

    private void autoCrackOff_Click(object sender, EventArgs e)
    {
        AutoCrackEnabled = true;
        autoCrackOn.BringToFront();
        AppSettings.Default.AutoCrack = true;
        AppSettings.Default.Save();
    }

    private void autoCrackOn_Click(object sender, EventArgs e)
    {
        AutoCrackEnabled = false;
        autoCrackOff.BringToFront();
        AppSettings.Default.AutoCrack = false;
        AppSettings.Default.Save();
    }

    private void SteamAppId_FormClosing(object sender, FormClosingEventArgs e)
    {
        Program.CmdKill("APPID");
    }

    private void OpenDir_Click(object sender, EventArgs e)
    {
        Process.Start("explorer.exe", _gameDir);
    }

    // Override KeyProcessCmdKey to catch Ctrl+S
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            // Force show compression settings dialog
            AppSettings.Default.ZipDontAsk = false;
            AppSettings.Default.Save();

            if (ZipToShare.Visible && ZipToShare.Enabled)
            {
                ZipToShare_Click(null, null);
            }
            else
            {
                // Show compression dialog even without a completed crack for testing
                using var compressionForm = new CompressionSettingsForm();
                compressionForm.Owner = this;
                compressionForm.StartPosition = FormStartPosition.CenterParent;
                compressionForm.TopMost = true;
                compressionForm.BringToFront();
                compressionForm.ShowDialog(this);
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task ShareGameAsync(string gameName, string gamePath, bool crack, Form parentForm)
    {
        try
        {
            // Store original gameDir and restore after
            var originalGameDir = _gameDir;
            _gameDir = gamePath;
            _gameDirName = gameName;

            if (crack)
            {
                // Crack the game first
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        Tit($"Cracking {gameName}...", Color.Yellow);
                    });
                }

                bool crackSuccess = await CrackAsync();
                if (!crackSuccess)
                {
                    MessageBox.Show($"Failed to crack {gameName}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _gameDir = originalGameDir;
                    return;
                }
            }

            // Now show compression form with upload options
            using (var compressionForm = new CompressionSettingsFormExtended(gameName, crack))
            {
                compressionForm.Owner = parentForm;
                compressionForm.StartPosition = FormStartPosition.CenterParent;
                compressionForm.TopMost = true;

                if (compressionForm.ShowDialog() != DialogResult.OK)
                {
                    _gameDir = originalGameDir;
                    return;
                }

                // Handle compression and upload
                await CompressAndUploadAsync(
                    gamePath,
                    gameName,
                    crack,
                    compressionForm.SelectedFormat,
                    compressionForm.SelectedLevel,
                    compressionForm.UploadToBackend,
                    compressionForm.EncryptForRin,
                    parentForm
                );
            }

            _gameDir = originalGameDir;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error sharing game: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CompressAndUploadAsync(string gamePath, string gameName, bool isCracked,
        string format, string level, bool upload, bool encryptForRin, Form parentForm)
    {
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string prefix = isCracked ? "[CRACKED]" : "[CLEAN]";
            string zipName = $"{prefix} {gameName}.{format.ToLower()}";
            string zipPath = Path.Combine(desktopPath, zipName);

            if (parentForm.IsHandleCreated)
            {
                parentForm.Invoke(() =>
                {
                    Tit($"Compressing {gameName}...", Color.Cyan);
                });
            }

            // Perform compression
            bool compressionSuccess = await CompressGameAsync(gamePath, zipPath, format, level, encryptForRin);

            if (!compressionSuccess)
            {
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        Tit("Compression failed!", Color.Red);
                    });
                }

                return;
            }

            if (upload)
            {
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        Tit("Uploading to YSG/HFP backend (6 month expiry)...", Color.Magenta);
                    });
                }

                string uploadUrl = await UploadToBackend(zipPath, parentForm);

                if (!string.IsNullOrEmpty(uploadUrl))
                {
                    // Show success with copy button
                    ShowUploadSuccess(uploadUrl, gameName, isCracked, parentForm);
                }
            }
            else
            {
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        Tit($"Saved to Desktop: {zipName}", Color.Green);
                    });
                }

                Process.Start("explorer.exe", desktopPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Compression/Upload failed: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task<bool> CompressGameAsync(string sourcePath, string outputPath, string format, string level,
        bool encryptForRin)
    {
        try
        {
            // Build 7-zip command
            string sevenZipPath = @"C:\Program Files\7-Zip\7z.exe";
            if (!File.Exists(sevenZipPath))
            {
                sevenZipPath = @"C:\Program Files (x86)\7-Zip\7z.exe";
                if (!File.Exists(sevenZipPath))
                {
                    // Fall back to System.IO.Compression for basic zip without password
                    if (format.ToLower() == "zip" && !encryptForRin)
                    {
                        await Task.Run(() =>
                        {
                            ZipFile.CreateFromDirectory(sourcePath, outputPath,
                                CompressionLevel.Optimal, false);
                        });
                        return true;
                    }

                    MessageBox.Show("7-Zip not found! Please install 7-Zip for advanced compression.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            // Map compression level
            string compressionSwitch = "";
            switch (level.ToLower())
            {
                case "no compression":
                    compressionSwitch = "-mx0";
                    break;
                case "fast":
                    compressionSwitch = "-mx1";
                    break;
                case "normal":
                    compressionSwitch = "-mx5";
                    break;
                case "maximum":
                    compressionSwitch = "-mx9";
                    break;
                case "ultra":
                    compressionSwitch = "-mx9 -mfb=273 -ms=on";
                    break;
            }

            // Build command arguments
            string archiveType = format.ToLower() == "7z" ? "7z" : "zip";
            string passwordArg = encryptForRin ? "-p\"cs.rin.ru\" -mhe=on" : "";

            // Add -bsp1 to get progress percentage output
            string arguments =
                $"a -t{archiveType} {compressionSwitch} {passwordArg} -bsp1 \"{outputPath}\" \"{sourcePath}\\*\" -r";

            // Execute 7-zip
            var processInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            // Read output asynchronously to capture progress
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // 7-Zip outputs progress like "5%" or " 42%"
                    var match = Regex.Match(e.Data, @"(\d+)%");
                    if (match.Success)
                    {
                        int percentage = int.Parse(match.Groups[1].Value);
                        try
                        {
                            // Update the RGB text with actual progress
                            Invoke(() =>
                            {
                                Tit($"Compressing... {percentage}%", Color.Cyan);
                            });
                        }
                        catch { }
                    }
                }
            };

            process.BeginOutputReadLine();
            await Task.Run(() => process.WaitForExit());
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Compression error: {ex.Message}");
            return false;
        }
    }

    private async Task<string> UploadToBackend(string filePath, Form parentForm)
    {
        Debug.WriteLine("[UPLOAD] === Starting upload process ===");
        Debug.WriteLine($"[UPLOAD] File path: {filePath}");
        Debug.WriteLine($"[UPLOAD] File exists: {File.Exists(filePath)}");

        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            Debug.WriteLine($"[UPLOAD] File size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
        }

        Debug.WriteLine($"[UPLOAD] Parent form handle created: {parentForm.IsHandleCreated}");

        try
        {
            // Check if the form handle is created before invoking
            if (!parentForm.IsHandleCreated)
            {
                // If handle not created, we can't show UI updates
                Debug.WriteLine("[UPLOAD] Warning: Form handle not created, UI updates disabled");
                // Continue with upload anyway
            }

            Debug.WriteLine("[UPLOAD] Creating HttpClient...");
            using var client = new HttpClient();
            // SACGUI backend configuration - no auth headers needed
            Debug.WriteLine("[UPLOAD] Configuring client for SACGUI...");
            client.Timeout = TimeSpan.FromHours(2); // Allow for large files
            Debug.WriteLine("[UPLOAD] Timeout set to 2 hours");

            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;

            // Use multipart form for upload
            Debug.WriteLine("[UPLOAD] Creating multipart form content...");
            using var content = new MultipartFormDataContent();
            // Read file in chunks to avoid memory issues with large files
            Debug.WriteLine("[UPLOAD] Opening file stream...");
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            Debug.WriteLine("[UPLOAD] File stream opened successfully");

            // Check file size before upload
            var uploadFileInfo = new FileInfo(filePath);
            var fileSizeMb = uploadFileInfo.Length / (1024.0 * 1024.0);
            Debug.WriteLine($"[UPLOAD] File size: {fileSizeMb:F2} MB");

            if (fileSizeMb > 500)
            {
                Debug.WriteLine("[UPLOAD] WARNING: File is larger than 500MB, may exceed server limits!");

                // Show warning to user
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"File is {fileSizeMb:F0}MB. The server may reject files over 500MB.\n\nConsider using higher compression settings.",
                            "Large File Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
            }

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            // Add required form fields for SACGUI
            content.Add(new StringContent("anonymous"), "hwid"); // Anonymous sharing
            content.Add(new StringContent("SACGUI-2.3"), "version");

            // Extract game name from filename (remove prefix and extension)
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string gameName = fileName.Replace("[CRACKED] ", "").Replace("[CLEAN] ", "")
                .Replace("[SACGUI] ", "");
            content.Add(new StringContent(gameName), "game_name");

            // Get client IP - use local IP for now
            string clientIp = "127.0.0.1";
            try
            {
                using var ipClient = new HttpClient();
                ipClient.Timeout = TimeSpan.FromSeconds(5);
                var ipResponse = await ipClient.GetStringAsync("https://api.ipify.org");
                if (!string.IsNullOrEmpty(ipResponse))
                {
                    clientIp = ipResponse.Trim();
                }
            }
            catch { }

            content.Add(new StringContent(clientIp), "client_ip");
            Debug.WriteLine("[UPLOAD] Added Version: SACGUI-2.3");
            Debug.WriteLine($"[UPLOAD] Added Game Name: {gameName}");
            Debug.WriteLine($"[UPLOAD] Added Client IP: {clientIp}");

            // Upload with progress tracking
            var progressHandler = new ProgressMessageHandler();
            progressHandler.HttpSendProgress += (s, e) =>
            {
                int percentage = (int)(e.BytesTransferred * 100 / fileSize);
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        Tit($"Uploading... {percentage}%", Color.Magenta);
                    });
                }
            };

            using var progressClient = new HttpClient(progressHandler);
            // Set timeout and user agent
            progressClient.Timeout = TimeSpan.FromHours(2);
            progressClient.DefaultRequestHeaders.Add("User-Agent", "SACGUI-Uploader/2.3");

            // Use 1fichier upload
            Debug.WriteLine("[UPLOAD] Using 1fichier for file upload");
            Debug.WriteLine("[UPLOAD] Request starting...");

            // Create progress handler for 1fichier upload
            var progress = new Progress<double>(value =>
            {
                int percentage = (int)(value * 100);
                if (parentForm.IsHandleCreated)
                {
                    parentForm.Invoke(() =>
                    {
                        Tit($"Uploading to 1fichier... {percentage}%", Color.Magenta);
                    });
                }
            });

            // Use 1fichier uploader
            using var uploader = new OneFichierUploader();
            var uploadResult = await uploader.UploadFileAsync(filePath, progress);

            Debug.WriteLine("[UPLOAD] 1fichier upload successful!");

            if (!string.IsNullOrEmpty(uploadResult.DownloadUrl))
            {
                string shareUrl = uploadResult.DownloadUrl;
                Debug.WriteLine($"[UPLOAD] Download URL: {shareUrl}");
                Debug.WriteLine($"[UPLOAD] Extracted share URL: {shareUrl}");
                return shareUrl;
            }

            Debug.WriteLine("[UPLOAD] WARNING: Upload succeeded but no URL was returned");
            throw new Exception("Upload succeeded but no download URL was returned");
        }
        catch (HttpRequestException httpEx)
        {
            Debug.WriteLine($"[UPLOAD] HTTP Request Exception: {httpEx.Message}");
            Debug.WriteLine($"[UPLOAD] Inner Exception: {httpEx.InnerException?.Message}");
            Debug.WriteLine($"[UPLOAD] Stack Trace:\n{httpEx.StackTrace}");

            if (parentForm.IsHandleCreated)
            {
                parentForm.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Upload failed (HTTP Error):\n{httpEx.Message}\n\nInner: {httpEx.InnerException?.Message}",
                        "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }
        catch (TaskCanceledException tcEx)
        {
            Debug.WriteLine($"[UPLOAD] Task Cancelled/Timeout: {tcEx.Message}");

            if (parentForm.IsHandleCreated)
            {
                parentForm.Invoke(() =>
                {
                    MessageBox.Show("Upload failed: Request timed out (file too large or slow connection)",
                        "Upload Timeout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UPLOAD] General Exception: {ex.GetType().Name}");
            Debug.WriteLine($"[UPLOAD] Message: {ex.Message}");
            Debug.WriteLine($"[UPLOAD] Inner Exception: {ex.InnerException?.Message}");
            Debug.WriteLine($"[UPLOAD] Stack Trace:\n{ex.StackTrace}");

            if (parentForm.IsHandleCreated)
            {
                parentForm.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Upload failed:\n{ex.Message}\n\nType: {ex.GetType().Name}\n\nInner: {ex.InnerException?.Message}",
                        "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }

        Debug.WriteLine("[UPLOAD] === Upload process ended with failure ===");
        return null;
    }

    private void ShowUploadSuccess(string url, string gameName, bool isCracked, Form parentForm)
    {
        var successForm = new Form();
        successForm.Text = "Upload Complete!";
        successForm.Size = new Size(600, 200);
        successForm.StartPosition = FormStartPosition.CenterParent;
        successForm.BackColor = Color.FromArgb(0, 20, 50);
        successForm.Owner = parentForm;

        var label = new Label();
        label.Text = $"{(isCracked ? "CRACKED" : "CLEAN")} {gameName}\nUploaded Successfully!\nLink valid for 6 months";
        label.AutoSize = false;
        label.Size = new Size(580, 60);
        label.Location = new Point(10, 20);
        label.ForeColor = Color.FromArgb(192, 255, 255);
        label.TextAlign = ContentAlignment.MiddleCenter;

        var urlTextBox = new TextBox();
        urlTextBox.Text = url;
        urlTextBox.ReadOnly = true;
        urlTextBox.Size = new Size(400, 25);
        urlTextBox.Location = new Point(50, 90);
        urlTextBox.BackColor = Color.FromArgb(0, 0, 100);
        urlTextBox.ForeColor = Color.White;

        var copyButton = new Button();
        copyButton.Text = "📋 Copy";
        copyButton.Size = new Size(100, 25);
        copyButton.Location = new Point(460, 90);
        copyButton.FlatStyle = FlatStyle.Flat;
        copyButton.ForeColor = Color.FromArgb(192, 255, 255);
        copyButton.Click += (s, e) =>
        {
            Clipboard.SetText(url);
            copyButton.Text = "✓ Copied!";
            Task.Delay(2000).ContinueWith(t =>
            {
                if (copyButton.IsHandleCreated)
                {
                    copyButton.Invoke(new Action(() => copyButton.Text = "📋 Copy"));
                }
            });
        };

        successForm.Controls.Add(label);
        successForm.Controls.Add(urlTextBox);
        successForm.Controls.Add(copyButton);
        successForm.ShowDialog();
    }

    private async void ZipToShare_Click(object sender, EventArgs e)
    {
        // If button says "Show Zip", reveal the file instead
        if (ZipToShare.Text == "Show Zip" && ZipToShare.Tag != null)
        {
            string savedZipPath = ZipToShare.Tag.ToString();
            if (File.Exists(savedZipPath))
            {
                Process.Start("explorer.exe", $"/select,\"{savedZipPath}\"");
            }

            // Don't reset - stays as "Show Zip" until next crack
            return;
        }

        // If button says "Cancel", cancel the compression
        if (ZipToShare.Text == "Cancel")
        {
            _zipCancellationTokenSource?.Cancel();
            ZipToShare.Text = "Zip Dir";
            // Reset button appearance (remove orange glow)
            ZipToShare.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
            ZipToShare.FlatAppearance.BorderSize = 1;
            ZipToShare.ForeColor = Color.FromArgb(220, 220, 225);
            Tit("Compression cancelled", Color.Orange);
            return;
        }

        string zipPath = ""; // Declare outside try block so finally can access it
        bool compressionCancelled = false;

        try
        {
            string gameName = _gameDirName;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            string compressionType = AppSettings.Default.ZipFormat;
            string compressionLevelStr = AppSettings.Default.ZipLevel;
            bool skipDialog = AppSettings.Default.ZipDontAsk;

            // Show dialog if not saved or if user wants to be asked (or Ctrl+S was pressed)
            if (!skipDialog || string.IsNullOrEmpty(compressionType))
            {
                // Use the custom CompressionSettingsForm
                using var compressionForm = new CompressionSettingsForm();
                compressionForm.Owner = this;
                compressionForm.StartPosition = FormStartPosition.CenterParent;
                compressionForm.TopMost = true;
                compressionForm.BringToFront();
                if (compressionForm.ShowDialog(this) != DialogResult.OK)
                {
                    ZipToShare.Enabled = true;
                    // Keep both buttons visible and centered if user cancels
                    ShowCrackButtons();
                    return;
                }

                compressionType = compressionForm.SelectedFormat;
                compressionLevelStr = compressionForm.SelectedLevel;

                // Save preferences if requested
                if (compressionForm.RememberChoice)
                {
                    AppSettings.Default.ZipFormat = compressionType;
                    AppSettings.Default.ZipLevel = compressionLevelStr;
                    AppSettings.Default.ZipDontAsk = true;
                    AppSettings.Default.Save();
                }
            }

            // Prepare cancellation token but DON'T change button yet
            _zipCancellationTokenSource = new CancellationTokenSource();

            bool use7Z = true; // Always use 7z for both .zip and .7z
            CompressionLevel compressionLevel;

            // Parse compression level from string
            int levelNum = 0;
            if (!string.IsNullOrEmpty(compressionLevelStr))
            {
                // Try to parse the number from the string
                var match = Regex.Match(compressionLevelStr, @"\d+");
                if (match.Success)
                {
                    int.TryParse(match.Value, out levelNum);
                }
            }

            // Map the level number to compression levels
            if (levelNum == 0)
            {
                compressionLevel = CompressionLevel.NoCompression;
            }
            else if (levelNum <= 3)
            {
                compressionLevel = CompressionLevel.Fastest;
            }
            else if (levelNum >= 7)
            {
                compressionLevel = CompressionLevel.Optimal;
            }
            else
            {
                compressionLevel = CompressionLevel.Fastest; // Medium
            }

            // Sanitize game name for filename
            string safeGameName = gameName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeGameName = safeGameName.Replace(c.ToString(), "");
            }

            // Determine file extension based on compression type
            bool is7ZFormat = compressionType.StartsWith("7Z");
            string zipName = is7ZFormat ? $"[SACGUI] {safeGameName}.7z" : $"[SACGUI] {safeGameName}.zip";
            zipPath = Path.Combine(desktopPath, zipName);

            // HSL to RGB converter for smooth color transitions
            Color HslToRgb(double h, double s, double l)
            {
                h = h / 360.0;
                double r, g, b;

                if (s == 0)
                {
                    r = g = b = l;
                }
                else
                {
                    double HueToRgb(double pVal, double qVal, double t)
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
                            return pVal + (qVal - pVal) * 6 * t;
                        }

                        if (t < 1.0 / 2)
                        {
                            return qVal;
                        }

                        if (t < 2.0 / 3)
                        {
                            return pVal + (qVal - pVal) * (2.0 / 3 - t) * 6;
                        }

                        return pVal;
                    }

                    var qHsl = l < 0.5 ? l * (1 + s) : l + s - l * s;
                    var pHsl = 2 * l - qHsl;
                    r = HueToRgb(pHsl, qHsl, h + 1.0 / 3);
                    g = HueToRgb(pHsl, qHsl, h);
                    b = HueToRgb(pHsl, qHsl, h - 1.0 / 3);
                }

                return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
            }

            if (use7Z)
            {
                // Show initial progress
                Tit($"Starting 7z compression (level {levelNum})...", HslToRgb(0, 1.0, 0.75));

                // Use 7zip with selected compression level
                await Task.Run(async () =>
                {
                    double colorHue = 0;

                    // Check if cancelled before starting
                    if (_zipCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        compressionCancelled = true;
                        return;
                    }

                    // NOW change button to Cancel with orange glow (compression is actually starting)
                    Invoke(() =>
                    {
                        ZipToShare.Enabled = true;
                        ZipToShare.Text = "Cancel";
                        ZipToShare.FlatAppearance.BorderColor = Color.FromArgb(255, 150, 0);
                        ZipToShare.FlatAppearance.BorderSize = 2;
                        ZipToShare.ForeColor = Color.FromArgb(255, 180, 100);
                    });

                    // Use 7za.exe from _bin folder
                    string sevenZipPath = $"{BinPath}\\7z\\7za.exe";

                    // Build 7z command with actual compression level and progress output to stdout
                    string formatType = is7ZFormat ? "7z" : "zip";

                    // Smart RAM detection - adapt dictionary size to available memory
                    string memParams = "";
                    if (formatType == "7z" && levelNum > 0)
                    {
                        // Get available physical memory using PerformanceCounter
                        ulong availRam = 1024; // Default 1GB if detection fails
                        try
                        {
                            using var pc = new PerformanceCounter("Memory", "Available MBytes");
                            availRam = (ulong)pc.NextValue();
                        }
                        catch
                        {
                            // Fallback to conservative default
                            availRam = 1024;
                        }

                        // Use 10% of available RAM for dictionary, with limits for 32-bit exe
                        ulong targetDict = availRam / 10;

                        // Set dictionary based on available RAM and compression level
                        // 64-bit 7z.exe can use MUCH more memory!
                        int dictSize;
                        if (availRam < 2048) // Less than 2GB available
                        {
                            dictSize = 64;
                        }
                        else if (availRam < 4096) // 2-4GB available
                        {
                            dictSize = 128;
                        }
                        else if (availRam < 8192) // 4-8GB available
                        {
                            dictSize = 256;
                        }
                        else if (availRam < 16384) // 8-16GB available
                        {
                            dictSize = 512;
                        }
                        else if (availRam < 32768) // 16-32GB available
                        {
                            dictSize = 1024; // 1GB dictionary
                        }
                        else if (availRam < 65536) // 32-64GB available
                        {
                            dictSize = 1536; // 1.5GB dictionary
                        }
                        else // 64GB+ available - USE THAT SHIT
                        {
                            dictSize = 2048; // 2GB dictionary!
                        }

                        // Cap based on compression level (but be aggressive if they have RAM)
                        if (levelNum <= 3 && dictSize > 256)
                        {
                            dictSize = 256;
                        }
                        else if (levelNum <= 5 && dictSize > 512)
                        {
                            dictSize = 512;
                        }
                        else if (levelNum <= 7 && dictSize > 1024)
                        {
                            dictSize = 1024;
                        }

                        // No need to cap for 64-bit exe!

                        memParams = $" -md={dictSize}m";
                        Debug.WriteLine(
                            $"[7Z] Available RAM: {availRam}MB, Using dictionary: {dictSize}MB for level {levelNum}");
                    }

                    string args = $"a -mx{levelNum} -t{formatType}{memParams} -bsp1 \"{zipPath}\" \"{_gameDir}\"\\* -r";

                    var psi = new ProcessStartInfo
                    {
                        FileName = sevenZipPath,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    string lastProgressText = $"Compressing with 7z (level {levelNum})...";

                    // Read stderr asynchronously for progress
                    var stderrTask = Task.Run(() =>
                    {
                        while (p.StandardError.ReadLine() is { } line)
                        {
                            Debug.WriteLine($"[7Z STDERR] {line}");

                            // Parse percentage from 7z output if available
                            var percentMatch = Regex.Match(line, @"(\d+)%");
                            if (percentMatch.Success)
                            {
                                lastProgressText = $"Compressing with 7z... {percentMatch.Groups[1].Value}%";

                                // Smooth HSL cycling for pastel colors
                                colorHue = (colorHue + 3) % 360;
                                Color currentColor = HslToRgb(colorHue, 1.0, 0.75);

                                Invoke(() =>
                                {
                                    Tit(lastProgressText, currentColor);
                                });
                            }
                        }
                    });

                    // Check stdout for progress too
                    var stdoutTask = Task.Run(() =>
                    {
                        while (p.StandardOutput.ReadLine() is { } line)
                        {
                            Debug.WriteLine($"[7Z STDOUT] {line}");

                            // Parse percentage from stdout as well
                            var percentMatch = Regex.Match(line, @"(\d+)%");
                            if (percentMatch.Success)
                            {
                                lastProgressText = $"Compressing with 7z... {percentMatch.Groups[1].Value}%";

                                // Smooth HSL cycling for pastel colors
                                colorHue = (colorHue + 3) % 360;
                                Color currentColor = HslToRgb(colorHue, 1.0, 0.75);

                                Invoke(() =>
                                {
                                    Tit(lastProgressText, currentColor);
                                });
                            }
                        }
                    });

                    // Wait for process with cancellation support and show progress
                    while (!p.HasExited)
                    {
                        if (_zipCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            p.Kill();
                            compressionCancelled = true;
                            break;
                        }

                        // Show smooth HSL progress even without percentage
                        colorHue = (colorHue + 3) % 360;
                        Color currentColor = HslToRgb(colorHue, 1.0, 0.75);
                        Invoke(() =>
                        {
                            Tit(lastProgressText, currentColor);
                        });

                        await Task.Delay(50);
                    }

                    if (!compressionCancelled)
                    {
                        p.WaitForExit();
                        await stderrTask;
                        await stdoutTask;
                    }
                });
            }
            else
            {
                // Use standard .NET zip (this shouldn't run since use7z is always true)
                await Task.Run(async () =>
                {
                    var allFiles = Directory.GetFiles(_gameDir, "*", SearchOption.AllDirectories);
                    int totalFiles = allFiles.Length;
                    int currentFile = 0;
                    double colorHue = 0;

                    await using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    foreach (string file in allFiles)
                    {
                        currentFile++;
                        int percentage = currentFile * 100 / totalFiles;

                        // Smooth HSL cycling
                        colorHue = (colorHue + 3) % 360;
                        Color currentColor = HslToRgb(colorHue, 1.0, 0.75);

                        Invoke(() =>
                        {
                            string compressionText = compressionLevel == CompressionLevel.NoCompression
                                ? "Zipping (fast)..."
                                : "Compressing...";
                            Tit($"{compressionText} {percentage}%", currentColor);
                        });

                        string relativePath = file.Replace(_gameDir + Path.DirectorySeparatorChar, "");
                        var entry = archive.CreateEntry(Path.Combine(gameName, relativePath), compressionLevel);
                        await using (var entryStream = entry.Open())
                        await using (var fileStream = File.OpenRead(file))
                        {
                            fileStream.CopyTo(entryStream);
                        }

                        // Update color every 500ms or every 10 files, whichever comes first
                        if (currentFile % 10 == 0)
                        {
                            await Task.Delay(50);
                        }
                    }
                });
            }

            // Final RGB flash
            double finalHue = 0;
            for (int i = 0; i < 10; i++)
            {
                finalHue = (finalHue + 36) % 360;
                Tit($"Saved to Desktop: {zipName}", HslToRgb(finalHue, 1.0, 0.75));
                await Task.Delay(100);
            }

            Process.Start("explorer.exe", desktopPath);
        }
        catch (Exception ex)
        {
            Tit($"Zip failed: {ex.Message}", Color.Red);
        }
        finally
        {
            ZipToShare.Enabled = true;

            // Reset button appearance (remove orange glow)
            ZipToShare.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
            ZipToShare.FlatAppearance.BorderSize = 1;
            ZipToShare.ForeColor = Color.FromArgb(220, 220, 225);

            // If cancelled, reset to Zip Dir
            if (compressionCancelled)
            {
                ZipToShare.Text = "Zip Dir";
                ZipToShare.Tag = null;
                // Delete partial zip file if it exists
                if (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); }
                    catch { }
                }
            }
            // Only change to "Show Zip" if the file actually exists
            else if (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
            {
                ZipToShare.Text = "Show Zip";
                ZipToShare.Tag = zipPath;

                // Show Upload button on top of Zip button after successful zip
                ShowUploadButton();
            }
            else
            {
                // Compression failed, keep it as Zip Dir
                ZipToShare.Text = "Zip Dir";
                ZipToShare.Tag = null;

                // Keep both buttons visible and centered after failed zip
                ShowCrackButtons();
            }
        }
    }

    private void ManAppPanel_Paint(object sender, PaintEventArgs e)
    {
        // Draw modern border with subtle glow
        using var borderPen = new Pen(Color.FromArgb(180, 100, 150, 200), 2);
        e.Graphics.DrawRectangle(borderPen, 0, 0, ManAppPanel.Width - 1, ManAppPanel.Height - 1);
    }

    private void ManAppBtn_Click(object sender, EventArgs e)
    {
        if (ManAppBox.Text.Length > 2 && Isnumeric)
        {
            CurrentAppId = ManAppBox.Text;
            Appname = "";

            // Try to fetch game name from Steam API
            FetchGameNameFromSteamApi(CurrentAppId);

            searchTextBox.Clear();
            ManAppBox.Clear();
            ManAppPanel.Visible = false;
            searchTextBox.Enabled = false;
            mainPanel.Visible = true;
            btnManualEntry.Visible = false;
            startCrackPic.Visible = true;
            Tit("READY! Click skull folder above to perform crack!", Color.LightSkyBlue);

            resinstruccZip.Visible = false;

            // Auto-crack if enabled
            if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
            {
                // Trigger crack just like clicking the button
                startCrackPic_Click(null, null);
            }
        }
        else
        {
            MessageBox.Show("Enter APPID or press ESC to cancel!", "No APPID entered!");
        }
    }

    private void ManAppBox_TextChanged(object sender, EventArgs e)
    {
        double parsedValue;

        if (!double.TryParse(ManAppBox.Text, out parsedValue))
        {
            ManAppBox.Text = "";
            Isnumeric = false;
        }

        if (ManAppBox.Text.Length > 0)
        {
            ManAppBtn.Enabled = true;
            Isnumeric = true;
        }
        else
        {
            Isnumeric = false;
            ManAppBtn.Enabled = false;
        }
    }

    private async void FetchGameNameFromSteamApi(string appId)
    {
        try
        {
            using var client = new WebClient();
            string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
            string json = await client.DownloadStringTaskAsync(url);

            // Simple JSON parsing without external libraries
            if (json.Contains($"\"{appId}\":{{\"success\":true"))
            {
                int nameIndex = json.IndexOf("\"name\":\"", StringComparison.Ordinal);
                if (nameIndex != -1)
                {
                    nameIndex += 8; // Length of "name":"
                    int endIndex = json.IndexOf("\"", nameIndex, StringComparison.Ordinal);
                    if (endIndex != -1)
                    {
                        Appname = json.Substring(nameIndex, endIndex - nameIndex);
                        Tat($"{Appname} ({appId})");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If API lookup fails, just show the AppID
            Debug.WriteLine($"Failed to fetch game name: {ex.Message}");
        }

        // If we couldn't get the name, just show the AppID
        Tat($"AppID: {appId}");
    }

    private void btnManualEntry_Click(object sender, EventArgs e)
    {
        ManAppPanel.Visible = true;
        ManAppPanel.BringToFront(); // Make sure it's on top!
        ManAppBox.Focus();
        ManAppBtn.Visible = true;
    }


    private void ManAppBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (ManAppBox.Text.Length > 2 && Isnumeric)
            {
                CurrentAppId = ManAppBox.Text;
                Appname = "";
                btnManualEntry.Visible = false;
                // Try to fetch game name from Steam API
                FetchGameNameFromSteamApi(CurrentAppId);

                searchTextBox.Clear();
                searchTextBox.Enabled = false;
                ManAppBox.Clear();
                ManAppPanel.Visible = false;
                mainPanel.Visible = true;
                btnManualEntry.Visible = false;
                startCrackPic.Visible = true;
                Tit("READY! Click skull folder above to perform crack!", Color.HotPink);

                resinstruccZip.Visible = false;

                // Auto-crack if enabled
                if (AutoCrackEnabled && !string.IsNullOrEmpty(_gameDir))
                {
                    // Trigger crack just like clicking the button
                    startCrackPic_Click(null, null);
                }
            }
            else
            {
                MessageBox.Show("Enter APPID or press ESC to cancel!");
            }
        }

        if (e.KeyCode == Keys.Escape)
        {
            CloseManAppPanel();
        }
    }

    private void CloseManAppPanel()
    {
        ManAppBox.Clear();
        ManAppPanel.Visible = false;
        searchTextBox.Focus();
    }

    private void Form_Click(object sender, EventArgs e)
    {
        if (ManAppPanel.Visible)
        {
            CloseManAppPanel();
        }
    }

    private void searchTextBox_MouseClick(object sender, MouseEventArgs e)
    {
        // Only clear on FIRST click after folder/file selection
        if (_isFirstClickAfterSelection)
        {
            searchTextBox.Clear();
            _isFirstClickAfterSelection = false; // Reset flag
        }
    }

    private void ShareButton_Click(object sender, EventArgs e)
    {
        // Remove focus from button to prevent highlight border
        ActiveControl = null;

        // Prevent multiple share windows - reuse existing one
        if (_shareWindow is { IsDisposed: false })
        {
            _shareWindow.BringToFront();
            _shareWindow.Activate();
            return;
        }

        // Show the enhanced share window with your Steam games
        _shareWindow = new EnhancedShareWindow(this);
        _shareWindow.FormClosed += (s, args) =>
        {
            // Reposition main form to center on where child was
            if (s is Form childForm)
            {
                int newX = childForm.Location.X + (childForm.Width - Width) / 2;
                int newY = childForm.Location.Y + (childForm.Height - Height) / 2;
                Location = new Point(Math.Max(0, newX), Math.Max(0, newY));
            }

            _shareWindow = null;
            Show();
        };

        // Position share window centered on main form's location
        _shareWindow.StartPosition = FormStartPosition.Manual;
        int centerX = Location.X + (Width - _shareWindow.Width) / 2;
        int centerY = Location.Y + (Height - _shareWindow.Height) / 2;
        _shareWindow.Location = new Point(Math.Max(0, centerX), Math.Max(0, centerY));

        // Sync window state - restoring either window restores both
        _shareWindow.Resize += ShareWindow_Resize;

        // Sync pin state - use actual saved setting, not the form's TopMost which might be wrong
        _shareWindow.TopMost = AppSettings.Default.Pinned;

        // Show share window first, then hide main (no delay)
        _shareWindow.Show();
        Hide();
        _shareWindow.BringToFront();
        _shareWindow.Activate();
    }

    private void ShareWindow_Resize(object sender, EventArgs e)
    {
        // When share window is restored, restore main window too
        if (_shareWindow is { WindowState: FormWindowState.Normal } &&
            WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
    }


    // All RIN scraping code removed - no longer needed

    private void resinstruccZip_Click(object sender, EventArgs e)
    {
    }

    private void label5_Click(object sender, EventArgs e)
    {
    }

    /// <summary>
    ///     Shows a styled confirmation dialog matching the app's dark theme
    /// </summary>
    private bool ShowStyledConfirmation(string title, string message, string path, string yesText, string noText)
    {
        bool result = false;

        using var dialog = new Form();
        dialog.Text = title;
        dialog.Size = new Size(500, 280);
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.FormBorderStyle = FormBorderStyle.None;
        dialog.BackColor = Color.FromArgb(25, 28, 40);
        dialog.ForeColor = Color.White;
        dialog.ShowInTaskbar = false;

        // Add a subtle border
        dialog.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(60, 65, 80), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, dialog.Width - 1, dialog.Height - 1);
        };

        // Title bar with icon
        var titleLabel = new Label
        {
            Text = "⚠️ " + title,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 200, 100),
            AutoSize = false,
            Size = new Size(480, 35),
            Location = new Point(15, 15),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Message
        var messageLabel = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.White,
            AutoSize = false,
            Size = new Size(470, 50),
            Location = new Point(15, 55)
        };

        // Path display with dark background
        var pathPanel = new Panel
        {
            BackColor = Color.FromArgb(15, 18, 25), Size = new Size(470, 45), Location = new Point(15, 110)
        };

        var pathLabel = new Label
        {
            Text = path,
            Font = new Font("Consolas", 9),
            ForeColor = Color.FromArgb(150, 180, 255),
            AutoSize = false,
            Size = new Size(460, 35),
            Location = new Point(5, 5),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pathPanel.Controls.Add(pathLabel);

        // Question
        var questionLabel = new Label
        {
            Text = "Is this actually a single game's directory?",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Size = new Size(470, 25),
            Location = new Point(15, 165),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Yes button (primary - green tint)
        var yesBtn = new Button
        {
            Text = yesText,
            Size = new Size(200, 40),
            Location = new Point(35, 200),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 80, 60),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        yesBtn.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 80);
        yesBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 100, 70);
        yesBtn.Click += (s, e) =>
        {
            result = true;
            dialog.Close();
        };

        // No button (secondary - purple tint)
        var noBtn = new Button
        {
            Text = noText,
            Size = new Size(200, 40),
            Location = new Point(265, 200),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 40, 80),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        noBtn.FlatAppearance.BorderColor = Color.FromArgb(100, 60, 140);
        noBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 50, 110);
        noBtn.Click += (s, e) =>
        {
            result = false;
            dialog.Close();
        };

        // Allow dragging the dialog
        bool dragging = false;
        Point dragStart = Point.Empty;
        dialog.MouseDown += (s, e) =>
        {
            dragging = true;
            dragStart = e.Location;
        };
        dialog.MouseMove += (s, e) =>
        {
            if (dragging)
            {
                dialog.Location = new Point(dialog.Location.X + e.X - dragStart.X,
                    dialog.Location.Y + e.Y - dragStart.Y);
            }
        };
        dialog.MouseUp += (s, e) => { dragging = false; };

        dialog.Controls.AddRange(titleLabel, messageLabel, pathPanel, questionLabel, yesBtn, noBtn);
        dialog.ShowDialog(this);

        return result;
    }

    // Progress handler for upload tracking
    private class ProgressMessageHandler : HttpClientHandler
    {
        public event EventHandler<HttpProgressEventArgs> HttpSendProgress;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // For now, just send without progress tracking
            // Full implementation would require tracking the request stream
            var response = await base.SendAsync(request, cancellationToken);

            // Simulate progress complete
            HttpSendProgress?.Invoke(this, new HttpProgressEventArgs(0, 100, 100));

            return response;
        }
    }

    public class HttpProgressEventArgs(int progressPercentage, long bytesTransferred, long? totalBytes)
        : EventArgs
    {
        public long BytesTransferred { get; } = bytesTransferred;
        public long? TotalBytes { get; } = totalBytes;
        public int ProgressPercentage { get; } = progressPercentage;
    }

    #region Windows API for Acrylic Blur

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

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

    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicblurbehind = 4;

    // Fix borderless form taskbar
    private const int WsMinimizebox = 0x20000;
    private const int CsDblclks = 0x8;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.Style |= WsMinimizebox;
            cp.ClassStyle |= CsDblclks;
            return cp;
        }
    }

    #endregion

    #region Crack Details Tracking

    /// <summary>
    ///     Current crack details being populated during a crack operation
    /// </summary>
    public CrackDetails CurrentCrackDetails { get; private set; }

    /// <summary>
    ///     History of crack operations for the current session
    /// </summary>
    public List<CrackDetails> CrackHistory { get; } = [];

    #endregion

    #region Batch Progress Indicator

    private PictureBox _batchIndicator;
    private BatchGameSelectionForm _activeBatchForm;
    private Image _batchIconBase;
    private ToolTip _batchIndicatorTooltip;

    #endregion

    #region Batch Game Detection

    /// <summary>
    ///     Checks if a game folder has multiple steam_api DLLs (suspicious structure)
    ///     Returns the count of steam_api DLLs found (excluding .bak backups)
    /// </summary>
    public static int CountSteamApiDlls(string gamePath)
    {
        try
        {
            var count = Directory.GetFiles(gamePath, "steam_api*.dll", SearchOption.AllDirectories)
                .Count(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
                            (f.EndsWith("steam_api.dll", StringComparison.OrdinalIgnoreCase) ||
                             f.EndsWith("steam_api64.dll", StringComparison.OrdinalIgnoreCase)));
            return count;
        }
        catch { return 0; }
    }

    /// <summary>
    ///     Detects if a folder is a game folder (has steam_api DLL or other game indicators)
    /// </summary>
    private bool IsGameFolder(string path)
    {
        // Delegate to game detection service
        return _gameDetection.IsGameFolder(path);
    }

    /// <summary>
    ///     Detects games in subfolders when user selects a folder containing multiple games
    /// </summary>
    private List<string> DetectGamesInFolder(string path)
    {
        // Delegate to game detection service
        return _gameDetection.DetectGamesInFolder(path);
    }

    #region Batch Progress Indicator Methods

    /// <summary>
    ///     Initialize the batch progress indicator (hidden by default)
    /// </summary>
    private void InitializeBatchIndicator()
    {
        try { _batchIconBase = Resources.batch_icon; }
        catch { }

        _batchIndicator = new PictureBox
        {
            Size = new Size(48, 48),
            Location = new Point(10, ClientSize.Height - 58),
            Cursor = Cursors.Hand,
            Visible = false,
            BackColor = Color.Transparent,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _batchIndicatorTooltip = new ToolTip();
        _batchIndicatorTooltip.SetToolTip(_batchIndicator, "Click to restore batch window");

        _batchIndicator.Click += (s, e) =>
        {
            if (_activeBatchForm is { IsDisposed: false })
            {
                _activeBatchForm.Show();
                _activeBatchForm.WindowState = FormWindowState.Normal;
                _activeBatchForm.BringToFront();
                _activeBatchForm.Activate();
                _batchIndicator.Visible = false;
                Hide(); // Hide Form1 when batch form is restored
            }
        };

        Controls.Add(_batchIndicator);
        _batchIndicator.BringToFront();
    }

    /// <summary>
    ///     Update the batch indicator with current progress percentage
    /// </summary>
    public void UpdateBatchIndicator(int percent)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateBatchIndicator(percent)); }
            catch { }

            return;
        }

        if (_batchIconBase == null)
        {
            return;
        }

        // Create a new image with the percentage drawn on it
        var bmp = new Bitmap(_batchIconBase.Width, _batchIconBase.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.DrawImage(_batchIconBase, 0, 0);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Draw percentage centered in the front window's main area
            string text = percent.ToString();
            using (var font = new Font("Segoe UI", _batchIconBase.Width / 5, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            using (var outline = new Pen(Color.FromArgb(200, 20, 25, 45), 3))
            {
                // Front window area is roughly: X 35-98%, Y 58-95%
                var textRect = new RectangleF(
                    _batchIconBase.Width * 0.35f,
                    _batchIconBase.Height * 0.58f,
                    _batchIconBase.Width * 0.63f,
                    _batchIconBase.Height * 0.37f);

                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
                };

                // Draw outline by drawing text multiple times offset
                using (var outlineBrush = new SolidBrush(Color.FromArgb(200, 20, 25, 45)))
                {
                    for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        if (dx != 0 || dy != 0)
                        {
                            g.DrawString(text, font, outlineBrush,
                                textRect with { X = textRect.X + dx, Y = textRect.Y + dy }, sf);
                        }
                    }
                }

                g.DrawString(text, font, brush, textRect, sf);
            }
        }

        _batchIndicator.Image?.Dispose();
        _batchIndicator.Image = bmp;
        _batchIndicatorTooltip.SetToolTip(_batchIndicator, $"Batch: {percent}% - Click to restore");

        // Update user's designed label
        if (batchProgressLabel != null)
        {
            batchProgressLabel.Text = percent + "%";
        }
    }

    /// <summary>
    ///     Show the batch indicator when batch window is minimized
    /// </summary>
    public void ShowBatchIndicator()
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(ShowBatchIndicator); }
            catch { }

            return;
        }

        // Show user's designed controls
        if (batchProgressIcon != null)
        {
            batchProgressIcon.Visible = true;
        }

        if (batchProgressLabel != null)
        {
            if (string.IsNullOrEmpty(batchProgressLabel.Text) || !batchProgressLabel.Text.Contains("%"))
            {
                batchProgressLabel.Text = "0%";
            }

            batchProgressLabel.Visible = true;
            batchProgressLabel.BringToFront();
        }

        if (arrowBatchConversionLabel != null)
        {
            arrowBatchConversionLabel.Visible = true;
        }
    }

    /// <summary>
    ///     Hide the batch indicator
    /// </summary>
    public void HideBatchIndicator()
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(HideBatchIndicator); }
            catch { }

            return;
        }

        if (batchProgressIcon != null)
        {
            batchProgressIcon.Visible = false;
        }

        if (batchProgressLabel != null)
        {
            batchProgressLabel.Visible = false;
        }

        if (arrowBatchConversionLabel != null)
        {
            arrowBatchConversionLabel.Visible = false;
        }
    }

    /// <summary>
    ///     Shows and centers the Open Dir and Zip Dir buttons below the game name text
    /// </summary>
    private void ShowCrackButtons(bool showZip = true)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(() => ShowCrackButtons(showZip)); }
            catch { }

            return;
        }

        // Position buttons centered below currDIrText
        int gap = 10;
        int buttonY = currDIrText.Bottom + 6;
        int totalWidth = showZip ? ZipToShare.Width + gap + OpenDir.Width : OpenDir.Width;
        int startX = (mainPanel.ClientSize.Width - totalWidth) / 2;

        if (showZip)
        {
            ZipToShare.Location = new Point(startX, buttonY);
            ZipToShare.Visible = true;
            ZipToShare.Text = "Zip Dir";
            ZipToShare.BringToFront();

            OpenDir.Location = new Point(startX + ZipToShare.Width + gap, buttonY);
        }
        else
        {
            OpenDir.Location = new Point(startX, buttonY);
            ZipToShare.Visible = false;
        }

        OpenDir.Visible = true;
        OpenDir.BringToFront();

        // Hide upload button when showing crack buttons (new crack cycle)
        if (UploadZipButton != null)
        {
            UploadZipButton.Visible = false;
        }
    }

    /// <summary>
    ///     Hides all crack-related buttons (Zip, Upload, Open Dir)
    ///     Call this when a new game directory is selected
    /// </summary>
    private void HideCrackButtons()
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(HideCrackButtons); }
            catch { }

            return;
        }

        if (ZipToShare != null)
        {
            ZipToShare.Visible = false;
        }

        if (OpenDir != null)
        {
            OpenDir.Visible = false;
        }

        if (UploadZipButton != null)
        {
            UploadZipButton.Visible = false;
        }
    }

    /// <summary>
    ///     Shows the Upload button on top of Zip Dir button after successful zip
    /// </summary>
    private void ShowUploadButton()
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(ShowUploadButton); }
            catch { }

            return;
        }

        if (UploadZipButton != null && ZipToShare != null)
        {
            // Position Upload button exactly where Zip button is
            UploadZipButton.Location = ZipToShare.Location;
            UploadZipButton.Size = ZipToShare.Size;
            UploadZipButton.Visible = true;
            UploadZipButton.BringToFront();
        }
    }

    /// <summary>
    ///     Opens batch conversion with custom paths (called from share sheet)
    /// </summary>
    public void OpenBatchConversionWithPaths(List<string> gamePaths)
    {
        if (gamePaths == null || gamePaths.Count == 0)
        {
            return;
        }

        ShowBatchGameSelection(gamePaths);
    }

    #endregion

    /// <summary>
    ///     Shows batch game selection form (non-modal)
    /// </summary>
    private void ShowBatchGameSelection(List<string> gamePaths)
    {
        var form = new BatchGameSelectionForm(gamePaths);
        form.Owner = this; // Set owner so icon can be copied
        _activeBatchForm = form; // Track the active batch form

        // Position batch form centered on where main form was
        form.StartPosition = FormStartPosition.Manual;
        int centerX = Location.X + (Width - form.Width) / 2;
        int centerY = Location.Y + (Height - form.Height) / 2;
        form.Location = new Point(Math.Max(0, centerX), Math.Max(0, centerY));

        // Set up minimize-to-indicator behavior
        form.Resize += (s, e) =>
        {
            if (form is { WindowState: FormWindowState.Minimized, IsProcessing: true })
            {
                form.Hide();
                Show(); // Show main form so indicator is visible
                BringToFront();
                Activate(); // Bring to foreground
                ShowBatchIndicator();
            }
        };

        // When form closes, clear the reference, hide indicator, and show main form again
        form.FormClosed += (s, e) =>
        {
            // Reposition main form to center on where child was
            if (s is Form childForm)
            {
                int newX = childForm.Location.X + (childForm.Width - Width) / 2;
                int newY = childForm.Location.Y + (childForm.Height - Height) / 2;
                Location = new Point(Math.Max(0, newX), Math.Max(0, newY));
            }

            _activeBatchForm = null;
            HideBatchIndicator();
            Show();
        };

        form.ProcessRequested += async (games, format, level, usePassword) =>
        {
            await ProcessBatchGames(games, format, level, usePassword, form);
        };

        // Show batch form first, then hide main (no delay/gap)
        form.Show();
        Hide();
    }

    /// <summary>
    ///     Processes multiple games in batch with per-game actions
    /// </summary>
    private async Task ProcessBatchGames(List<BatchGameItem> games, string compressionFormat, string compressionLevel,
        bool usePassword, BatchGameSelectionForm batchForm)
    {
        batchForm.SetProcessingMode(true);
        await Task.Delay(50);

        // ========== PHASE 0: CLEAN UP OLD CRACK ARTIFACTS (Always, before any processing) ==========
        foreach (var game in games)
        {
            try
            {
                string installPath = game.Path;
                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    continue;
                }

                // Restore .bak files (original Steam DLLs and EXEs)
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

                // Delete _[ prefixed files (LAN shortcuts), lobby_connect files, shortcuts - ALL directories
                foreach (var f in Directory.GetFiles(installPath, "_[*", SearchOption.AllDirectories))
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

                foreach (var f in Directory.GetFiles(installPath, "*.lnk", SearchOption.AllDirectories))
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
            catch { }
        }

        int success = 0;
        int failed = 0;
        int zipped = 0;
        int zipFailed = 0;
        int uploaded = 0;
        int uploadFailed = 0;

        var failureReasons = new List<(string gameName, string reason)>();
        var uploadResults = new List<(string gameName, string oneFichierUrl, string pydriveUrl)>();
        var crackResults = new Dictionary<string, bool>(); // Track which games cracked successfully
        var archivePaths = new Dictionary<string, string>(); // Track archive paths for upload

        // Time-based progress tracking with live adjustment (never decreases)
        // Load persisted rates from last session, fall back to defaults
        double zipRate;
        try
        {
            zipRate = compressionLevel == "0"
                ? Settings.Default.LastZipRateLevel0
                : Settings.Default.LastZipRateCompressed;
            if (zipRate <= 0)
            {
                zipRate = compressionLevel == "0" ? 50_000_000.0 : 30_000_000.0;
            }
        }
        catch { zipRate = compressionLevel == "0" ? 50_000_000.0 : 30_000_000.0; }

        double uploadRate;
        try
        {
            uploadRate = Settings.Default.LastUploadRate;
            if (uploadRate <= 0)
            {
                uploadRate = 5_000_000.0;
            }
        }
        catch { uploadRate = 5_000_000.0; }

        double conversionTimePerFile = 45.0;
        double retryBuffer = 1.3;

        // Calculate folder sizes for time estimation
        var folderSizes = new Dictionary<string, long>();
        long totalBytesToZip = 0;
        long totalBytesToUpload = 0;
        foreach (var game in games)
        {
            try
            {
                long size = Directory.GetFiles(game.Path, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
                folderSizes[game.Path] = size;
                if (game.ShouldZip)
                {
                    totalBytesToZip += size;
                }

                if (game.ShouldUpload)
                {
                    totalBytesToUpload += size;
                }
            }
            catch { folderSizes[game.Path] = 1_000_000_000; }
        }

        // Initial time estimates
        double estCrackTime = games.Count(g => g.ShouldCrack) * 3.0;
        double estZipTime = totalBytesToZip / zipRate;
        double estUploadTime = totalBytesToUpload / uploadRate;
        double estConversionTime = games.Count(g => g.ShouldUpload) * conversionTimePerFile;
        double totalEstimatedSeconds = (estCrackTime + estZipTime + estUploadTime + estConversionTime) * retryBuffer;
        totalEstimatedSeconds = Math.Max(totalEstimatedSeconds, 1.0);

        // Track completed work and actual speeds
        var batchStartTime = DateTime.Now;
        double completedWork = 0; // 0-1 representing portion of work done
        int lastPercent = 0; // Never go below this
        long bytesZippedSoFar = 0;
        long bytesUploadedSoFar = 0;
        double actualZipRate = zipRate; // Will be updated with measured rate
        double actualUploadRate = uploadRate; // Will be updated during uploads

        // Work weights (what portion of total work each phase represents)
        double crackWeight = estCrackTime / totalEstimatedSeconds;
        double zipWeight = estZipTime / totalEstimatedSeconds;
        double uploadWeight = estUploadTime / totalEstimatedSeconds;
        double conversionWeight = estConversionTime / totalEstimatedSeconds;

        void UpdateProgress(string phase, double phaseProgress)
        {
            // Recalculate based on actual speeds if we have data
            double remainingZipTime = (totalBytesToZip - bytesZippedSoFar) / actualZipRate;
            double remainingUploadTime = (totalBytesToUpload - bytesUploadedSoFar) / actualUploadRate;
            double remainingConvTime = Math.Max(0,
                estConversionTime * (1.0 - (bytesUploadedSoFar / (double)Math.Max(1, totalBytesToUpload))));
            double remainingCrackTime = estCrackTime; // Already decremented per crack
            double totalRemaining = remainingCrackTime + remainingZipTime + remainingUploadTime + remainingConvTime;

            double elapsed = (DateTime.Now - batchStartTime).TotalSeconds;
            double estTotalTime = elapsed + totalRemaining;

            int percent = estTotalTime > 0 ? (int)(elapsed / estTotalTime * 100) : 0;
            percent = Math.Max(lastPercent, Math.Min(99, percent)); // Never decrease, cap at 99
            lastPercent = percent;

            batchForm.UpdateTitleProgress(percent);
            batchForm.UpdateProgressWithEta(percent, totalRemaining);
            UpdateBatchIndicator(percent);
        }

        // Initial progress update
        UpdateProgress("init", 0);

        // Periodic timer to update progress every 20 seconds during long operations
        Timer progressTimer = null;
        progressTimer = new Timer { Interval = 20000 };
        progressTimer.Tick += (s, e) =>
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateProgress("timer", 0));
            }
            else
            {
                UpdateProgress("timer", 0);
            }
        };
        progressTimer.Start();

        try
        {
            // ========== PHASE 1: CRACK (Sequential due to shared state) ==========
            int crackIndex = 0;
            foreach (var game in games.Where(g => g.ShouldCrack))
            {
                batchForm.UpdateStatus(game.Path, "Cracking...", Color.Yellow);

                if (string.IsNullOrEmpty(game.AppId))
                {
                    batchForm.UpdateStatus(game.Path, "No AppID", Color.Orange);
                    failureReasons.Add((game.Name, "No AppID found"));
                    crackResults[game.Path] = false;
                    failed++;
                    continue;
                }

                try
                {
                    _gameDir = game.Path;
                    _gameDirName = game.Name;
                    _parentOfSelection = Directory.GetParent(game.Path).FullName;
                    CurrentAppId = game.AppId;

                    _suppressStatusUpdates = true;
                    bool crackSucceeded = await CrackAsync();
                    _suppressStatusUpdates = false;

                    crackResults[game.Path] = crackSucceeded;

                    // Store crack details for this game
                    if (CurrentCrackDetails != null)
                    {
                        batchForm.StoreCrackDetails(game.Path, CurrentCrackDetails);
                    }

                    if (crackSucceeded)
                    {
                        success++;
                        batchForm.UpdateStatus(game.Path, "Cracked ✓", Color.LightGreen);
                    }
                    else
                    {
                        failed++;
                        batchForm.UpdateStatus(game.Path, "Crack Failed", Color.Red);
                        failureReasons.Add((game.Name, "Crack returned false"));
                    }
                }
                catch (Exception ex)
                {
                    crackResults[game.Path] = false;
                    failed++;
                    batchForm.UpdateStatus(game.Path, "Crack Error", Color.Red);
                    failureReasons.Add((game.Name, $"Crack exception: {ex.Message}"));
                }

                // Reduce remaining crack estimate
                estCrackTime = Math.Max(0, estCrackTime - 3.0);
                UpdateProgress("crack", 0);
            }

            // Games that didn't need cracking
            foreach (var game in games.Where(g => !g.ShouldCrack))
            {
                crackResults[game.Path] = true;
            }

            _suppressStatusUpdates = false;

            // ========== PHASE 2 & 3: ZIP + UPLOAD PIPELINE ==========
            // Pipeline: zip one game, then upload it while zipping the next
            // Games that only need zip (no upload) are done first

            var gamesToZipOnly = games
                .Where(g => g is { ShouldZip: true, ShouldUpload: false } && crackResults.ContainsKey(g.Path) &&
                            crackResults[g.Path])
                .ToList();
            var gamesToZipAndUpload =
                games.Where(g =>
                        g is { ShouldZip: true, ShouldUpload: true } && crackResults.ContainsKey(g.Path) &&
                        crackResults[g.Path])
                    .ToList();
            var gamesToUpload = gamesToZipAndUpload.ToList(); // Will upload after zipping

            var zipStartTime = DateTime.Now;
            long totalBytesActuallyZipped = 0;
            string sevenZipPath = ResourceExtractor.GetBinFilePath(Path.Combine("7z", "7za.exe"));
            string password = usePassword ? "rin" : null;

            // Helper function to zip a single game
            async Task<(BatchGameItem game, bool success, string error, string archivePath)> ZipOneGame(
                BatchGameItem game)
            {
                string ext = compressionFormat == "7Z" ? ".7z" : ".zip";
                string parent = Directory.GetParent(game.Path).FullName;
                string archivePath = Path.Combine(parent, game.Name + ext);
                archivePaths[game.Path] = archivePath;

                string zipError = null;
                bool zipSuccess = await Task.Run(() =>
                {
                    try
                    {
                        string formatArg = compressionFormat == "7Z" ? "-t7z" : "-tzip";
                        // Add -bsp1 for progress to stdout
                        string args =
                            $"a {formatArg} -mx={compressionLevel} -bsp1 \"{archivePath}\" \"{game.Path}\\*\"";
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
                        // Read stdout async to get progress updates
                        var lastUpdate = DateTime.MinValue;
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (e.Data != null && e.Data.Contains("%"))
                            {
                                // Throttle updates to max 4/sec
                                if ((DateTime.Now - lastUpdate).TotalMilliseconds < 250)
                                {
                                    return;
                                }

                                lastUpdate = DateTime.Now;

                                // Parse percentage from output like "  5% 3 + file.dll"
                                var match = Regex.Match(e.Data, @"(\d+)%");
                                if (match.Success)
                                {
                                    string pct = match.Groups[1].Value;
                                    try
                                    {
                                        batchForm.BeginInvoke(() =>
                                            batchForm.UpdateStatus(game.Path, $"Zipping {pct}%", Color.Cyan));
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        };
                        proc.BeginOutputReadLine();

                        string stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();
                        if (proc.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
                        {
                            zipError = stderr.Length > 100 ? stderr.Substring(0, 100) + "..." : stderr;
                        }

                        return proc.ExitCode == 0;
                    }
                    catch (Exception ex)
                    {
                        zipError = ex.Message;
                        return false;
                    }
                });

                return (game, zipSuccess, zipError, archivePath);
            }

            // STEP 1: Zip games that only need zipping (no upload) - can be parallel
            if (gamesToZipOnly.Count > 0)
            {
                foreach (var game in gamesToZipOnly)
                {
                    batchForm.UpdateStatus(game.Path, "Zipping...", Color.Cyan);
                }

                var zipOnlyTasks = gamesToZipOnly.Select(ZipOneGame).ToList();
                var zipOnlyResults = await Task.WhenAll(zipOnlyTasks);

                foreach (var result in zipOnlyResults)
                {
                    if (result.success)
                    {
                        zipped++;
                        batchForm.UpdateStatus(result.game.Path, "Zipped ✓", Color.LightGreen);
                        batchForm.UpdateZipStatus(result.game.Path, true, result.archivePath);
                        totalBytesActuallyZipped += folderSizes.GetValueOrDefault(result.game.Path, 0);
                    }
                    else
                    {
                        zipFailed++;
                        batchForm.UpdateStatus(result.game.Path, "Zip Failed", Color.Red);
                        batchForm.UpdateZipStatus(result.game.Path, false, result.archivePath,
                            result.error ?? "Unknown");
                        failureReasons.Add((result.game.Name, $"Zip failed: {result.error ?? "Unknown"}"));
                    }

                    bytesZippedSoFar += folderSizes.GetValueOrDefault(result.game.Path, 0);
                    UpdateProgress("zip", 0);
                }
            }

            // STEP 2: Pipeline - zip sequentially, fire off uploads (max 3 concurrent)
            var conversionTasks = new List<Task>();
            var uploadTasks = new List<Task<bool>>(); // Track success/failure
            const int maxRetries = 3;
            var uploadSemaphore = new SemaphoreSlim(3, 3); // Max 3 concurrent uploads
            batchForm.ResetSkipCancelState();

            // Helper to upload a single game (runs in background, returns true if success)
            async Task<bool> UploadOneGame(BatchGameItem game, string archivePath)
            {
                // Wait for a slot to become available
                await uploadSemaphore.WaitAsync();
                int slotIndex = -1;

                try
                {
                    if (batchForm.ShouldCancelAll())
                    {
                        batchForm.UpdateStatus(game.Path, "Cancelled", Color.Orange);
                        return false;
                    }

                    if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
                    {
                        batchForm.UpdateStatus(game.Path, "No Archive", Color.Orange);
                        batchForm.UpdateUploadStatus(game.Path, false, null, "Archive not found");
                        lock (failureReasons)
                        {
                            failureReasons.Add((game.Name, "Archive not found"));
                        }

                        return false;
                    }

                    long fileSize = new FileInfo(archivePath).Length;

                    // Claim a visual upload slot
                    slotIndex = batchForm.ClaimUploadSlot(game.Path, game.Name, fileSize);
                    var cancellationToken = slotIndex >= 0
                        ? batchForm.GetSlotCancellationToken(slotIndex)
                        : CancellationToken.None;

                    bool uploadSuccess = false;
                    string lastError = null;
                    int attempt = 0;

                    while (!uploadSuccess && attempt < maxRetries && !batchForm.ShouldCancelAll() &&
                           !cancellationToken.IsCancellationRequested)
                    {
                        attempt++;
                        string statusPrefix = attempt > 1 ? $"Retry {attempt}: " : "";
                        batchForm.UpdateStatus(game.Path, $"{statusPrefix}⬆ 0% | Starting...", Color.Magenta);

                        try
                        {
                            using var uploader = new OneFichierUploader();
                            var gamePath = game.Path;
                            int currentSlot = slotIndex;
                            double lastProgress = 0;
                            DateTime lastProgressTime = DateTime.Now;
                            double smoothedSpeed = 0;

                            var progress = new Progress<double>(p =>
                            {
                                int pct = (int)(p * 100);
                                var now = DateTime.Now;
                                double progressDelta = p - lastProgress;
                                double timeDelta = (now - lastProgressTime).TotalSeconds;

                                if (timeDelta > 0.1 && progressDelta > 0)
                                {
                                    long uploadedBytes = (long)(p * fileSize);
                                    double bytesDelta = progressDelta * fileSize;
                                    double currentSpeed = bytesDelta / timeDelta;
                                    smoothedSpeed = smoothedSpeed > 0
                                        ? smoothedSpeed * 0.7 + currentSpeed * 0.3
                                        : currentSpeed;
                                    if (smoothedSpeed > 0)
                                    {
                                        actualUploadRate = smoothedSpeed;
                                    }

                                    // Calculate ETA
                                    long remaining = fileSize - uploadedBytes;
                                    double etaSec = smoothedSpeed > 0 ? remaining / smoothedSpeed : 0;
                                    string eta = etaSec < 60
                                        ? $"{(int)etaSec}s"
                                        : $"{(int)(etaSec / 60)}m{(int)(etaSec % 60)}s";
                                    string speed = smoothedSpeed > 1_000_000
                                        ? $"{smoothedSpeed / 1_000_000:F1}MB/s"
                                        : $"{smoothedSpeed / 1_000:F0}KB/s";

                                    // Update grid status column
                                    batchForm.UpdateStatus(gamePath, $"⬆ {pct}% | {speed} | {eta}", Color.Magenta);

                                    // Update visual slot progress bar
                                    if (currentSlot >= 0)
                                    {
                                        batchForm.UpdateSlotProgress(currentSlot, pct, uploadedBytes, fileSize,
                                            smoothedSpeed);
                                    }

                                    lastProgress = p;
                                    lastProgressTime = now;
                                }
                            });

                            var result = await Task.Run(() =>
                                uploader.UploadFileAsync(archivePath, progress, null, cancellationToken));

                            if (result != null && !string.IsNullOrEmpty(result.DownloadUrl))
                            {
                                string oneFichierUrl = result.DownloadUrl;
                                uploadSuccess = true;
                                batchForm.UpdateUploadStatus(game.Path, true, oneFichierUrl, null, attempt - 1);
                                try
                                {
                                    BeginInvoke(() =>
                                    {
                                        try
                                        {
                                            Clipboard.SetText(oneFichierUrl);
                                        }
                                        catch
                                        {
                                        }
                                    });
                                }
                                catch
                                {
                                }

                                // Check if PyDrive conversion is disabled
                                if (Settings.Default.SkipPyDriveConversion)
                                {
                                    // Skip PyDrive conversion, just use 1fichier link
                                    lock (uploadResults)
                                    {
                                        uploadResults.Add((game.Name, oneFichierUrl, null));
                                    }

                                    batchForm.UpdateStatus(game.Path, "1fichier ✓", Color.LightGreen);
                                    batchForm.SetFinalUrl(game.Path, oneFichierUrl);
                                }
                                else
                                {
                                    batchForm.UpdateStatus(game.Path, "Converting...", Color.Yellow);
                                    batchForm.SetConvertingUrl(game.Path, oneFichierUrl);
                                    var conversionTask = Task.Run(async () =>
                                    {
                                        string pydriveUrl = await ConvertOneFichierToPydrive(oneFichierUrl, fileSize,
                                            status => batchForm.UpdateStatus(game.Path, status, Color.Yellow));
                                        batchForm.ClearConvertingUrl(game.Path);

                                        if (!string.IsNullOrEmpty(pydriveUrl))
                                        {
                                            lock (uploadResults)
                                            {
                                                uploadResults.Add((game.Name, oneFichierUrl, pydriveUrl));
                                            }

                                            batchForm.UpdateStatus(game.Path, "PyDrive ✓", Color.LightGreen);
                                            batchForm.SetFinalUrl(game.Path, pydriveUrl);
                                            batchForm.UpdatePyDriveUrl(game.Path, pydriveUrl);
                                        }
                                        else
                                        {
                                            lock (uploadResults)
                                            {
                                                uploadResults.Add((game.Name, oneFichierUrl, null));
                                            }

                                            batchForm.UpdateStatus(game.Path, "1fichier ✓", Color.LightGreen);
                                            batchForm.SetFinalUrl(game.Path, oneFichierUrl);
                                        }
                                    });
                                    lock (conversionTasks)
                                    {
                                        conversionTasks.Add(conversionTask);
                                    }
                                }
                            }
                            else
                            {
                                lastError = "No URL returned";
                            }
                        }
                        catch (Exception ex)
                        {
                            lastError = ex.Message;
                            if (attempt < maxRetries)
                            {
                                batchForm.UpdateStatus(game.Path, $"Retry in {attempt * 2}s...", Color.Yellow);
                                await Task.Delay(attempt * 2000);
                            }
                        }
                    }

                    // Check if skipped
                    if (cancellationToken.IsCancellationRequested && !batchForm.ShouldCancelAll())
                    {
                        batchForm.UpdateStatus(game.Path, "Skipped", Color.Orange);
                        lock (failureReasons)
                        {
                            failureReasons.Add((game.Name, "Skipped by user"));
                        }

                        return false;
                    }

                    if (!uploadSuccess)
                    {
                        string shortError = lastError?.Length > 30 ? lastError.Substring(0, 30) + "..." : lastError;
                        batchForm.UpdateStatus(game.Path, $"Failed: {shortError}", Color.Red);
                        batchForm.UpdateUploadStatus(game.Path, false, null, lastError, attempt - 1);
                        lock (failureReasons)
                        {
                            failureReasons.Add((game.Name, $"Upload failed: {lastError}"));
                        }
                    }

                    lock (this)
                    {
                        bytesUploadedSoFar += folderSizes.GetValueOrDefault(game.Path, 0);
                    }

                    UpdateProgress("upload", 0);
                    return uploadSuccess;
                }
                finally
                {
                    // Always release slot and semaphore
                    if (slotIndex >= 0)
                    {
                        batchForm.ReleaseUploadSlot(slotIndex);
                    }

                    uploadSemaphore.Release();
                }
            }

            // Zip each game sequentially, fire off upload immediately after each zip
            foreach (var game in gamesToZipAndUpload)
            {
                if (batchForm.ShouldCancelAll())
                {
                    break;
                }

                batchForm.UpdateStatus(game.Path, "Zipping...", Color.Cyan);
                var zipResult = await ZipOneGame(game);

                if (zipResult.success)
                {
                    zipped++;
                    batchForm.UpdateStatus(game.Path, "Queued for upload", Color.Yellow);
                    batchForm.UpdateZipStatus(game.Path, true, zipResult.archivePath);
                    totalBytesActuallyZipped += folderSizes.GetValueOrDefault(game.Path, 0);

                    // Fire off upload immediately - don't wait
                    var uploadTask = UploadOneGame(game, zipResult.archivePath);
                    uploadTasks.Add(uploadTask);
                }
                else
                {
                    zipFailed++;
                    batchForm.UpdateStatus(game.Path, "Zip Failed", Color.Red);
                    batchForm.UpdateZipStatus(game.Path, false, zipResult.archivePath, zipResult.error ?? "Unknown");
                    failureReasons.Add((game.Name, $"Zip failed: {zipResult.error ?? "Unknown"}"));
                }

                bytesZippedSoFar += folderSizes.GetValueOrDefault(game.Path, 0);
                UpdateProgress("zip", 0);
            }

            // Save zip rate
            double zipElapsed = (DateTime.Now - zipStartTime).TotalSeconds;
            if (zipElapsed > 1 && totalBytesActuallyZipped > 0)
            {
                double measuredZipRate = totalBytesActuallyZipped / zipElapsed;
                actualZipRate = measuredZipRate;
                try
                {
                    if (compressionLevel == "0")
                    {
                        Settings.Default.LastZipRateLevel0 = measuredZipRate;
                    }
                    else
                    {
                        Settings.Default.LastZipRateCompressed = measuredZipRate;
                    }

                    Settings.Default.Save();
                }
                catch { }
            }

            // Wait for all uploads to complete and count results
            if (uploadTasks.Count > 0)
            {
                var uploadResults2 = await Task.WhenAll(uploadTasks);
                uploaded = uploadResults2.Count(r => r);
                uploadFailed = uploadResults2.Count(r => !r);
            }

            batchForm.HideUploadDetails();

            // Save upload rate
            if (actualUploadRate > 0 && actualUploadRate != uploadRate)
            {
                try
                {
                    Settings.Default.LastUploadRate = actualUploadRate;
                    Settings.Default.Save();
                }
                catch { }
            }

            // Wait for all conversions to complete
            if (conversionTasks.Count > 0)
            {
                await Task.WhenAll(conversionTasks);
            }
        } // end try
        finally
        {
            // Stop the progress timer
            progressTimer?.Stop();
            progressTimer?.Dispose();
        }

        // ========== DONE ==========
        batchForm.SetProcessingMode(false);
        batchForm.ResetTitle("Complete ✓");
        UpdateBatchIndicator(100);
        HideBatchIndicator();

        // Auto-restore batch window when all tasks complete
        if (batchForm.WindowState == FormWindowState.Minimized)
        {
            batchForm.WindowState = FormWindowState.Normal;
            batchForm.Show();
            batchForm.BringToFront();
            batchForm.Activate();
        }

        // Build summary
        var summaryParts = new List<string>();
        if (success > 0)
        {
            summaryParts.Add($"{success} cracked");
        }

        if (zipped > 0)
        {
            summaryParts.Add($"{zipped} zipped");
        }

        if (uploaded > 0)
        {
            summaryParts.Add($"{uploaded} uploaded");
        }

        if (failed > 0)
        {
            summaryParts.Add($"{failed} crack failed");
        }

        if (zipFailed > 0)
        {
            summaryParts.Add($"{zipFailed} zip failed");
        }

        if (uploadFailed > 0)
        {
            summaryParts.Add($"{uploadFailed} upload failed");
        }

        string summary = string.Join(", ", summaryParts);
        if (string.IsNullOrEmpty(summary))
        {
            summary = "No actions performed";
        }

        Tit($"Batch complete! {summary}", Color.LightGreen);

        // Build all links in phpBB format for cs.rin.ru: [url=link]Game Name[/url]
        string allLinksText = "";
        if (uploadResults.Count > 0)
        {
            allLinksText = string.Join("\n", uploadResults.Select(r =>
            {
                string url = string.IsNullOrEmpty(r.pydriveUrl) ? r.oneFichierUrl : r.pydriveUrl;
                return $"[url={url}]{r.gameName}[/url]";
            }));
        }

        // Show Copy All button if there are links
        batchForm.ShowCopyAllButton(allLinksText);
    }

    /// <summary>
    ///     Converts a 1fichier link to a pydrive link
    /// </summary>
    private async Task<string> ConvertOneFichierToPydrive(string oneFichierUrl, long fileSizeBytes = 0,
        Action<string> statusUpdate = null)
    {
        if (oneFichierUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            oneFichierUrl = "https://" + oneFichierUrl.Substring(7);
        }

        // Calculate wait time based on file size (12 seconds per GB, capped at 30 hours)
        int initialWaitSeconds = 30;
        if (fileSizeBytes > 5L * 1024 * 1024 * 1024) // 5GB+
        {
            long sizeInGb = fileSizeBytes / (1024 * 1024 * 1024);
            initialWaitSeconds = (int)(sizeInGb * 12);
            initialWaitSeconds = Math.Min(initialWaitSeconds, 108000); // Cap at 30 hours
            initialWaitSeconds = Math.Max(initialWaitSeconds, 30);
        }

        // Calculate retries: enough to cover estimated wait time plus buffer
        int maxRetries = Math.Max(10, (initialWaitSeconds / 30) + 5);
        int retryDelay = 30000;

        double sizeGb = fileSizeBytes / (1024.0 * 1024.0 * 1024.0);
        Console.WriteLine($"[CONVERT] Starting conversion for {oneFichierUrl}");
        Console.WriteLine(
            $"[CONVERT] File size: {sizeGb:F2} GB, Initial wait: {initialWaitSeconds}s, Max retries: {maxRetries}");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                int remainingSeconds = Math.Max(0, initialWaitSeconds - ((attempt - 1) * 30));
                string timeStr = remainingSeconds > 60 ? $"~{remainingSeconds / 60}m" : $"~{remainingSeconds}s";
                statusUpdate?.Invoke($"Converting {attempt}/{maxRetries} ({timeStr})");

                Console.WriteLine($"[CONVERT] Attempt {attempt}/{maxRetries} - estimated {timeStr} remaining");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30); // Match EnhancedShareWindow

                var requestBody = new { link = oneFichierUrl };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client
                    .PostAsync("https://pydrive.harryeffingpotter.com/convert-1fichier", content)
                    .ConfigureAwait(false);

                Console.WriteLine($"[CONVERT] Response status: {(int)response.StatusCode} {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine($"[CONVERT] Response body: {responseJson}");

                    // API returns {"link": "..."} - same as EnhancedShareWindow
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("link", out var linkProp))
                    {
                        string pydriveUrl = linkProp.GetString();
                        Console.WriteLine($"[CONVERT] SUCCESS! PyDrive URL: {pydriveUrl}");
                        return pydriveUrl;
                    }

                    Console.WriteLine("[CONVERT] No 'link' in response, will retry...");
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine($"[CONVERT] Error response: {errorBody}");

                    // Check if it's a "still processing" error - keep retrying
                    if (errorBody.Contains("LINK_DOWN") || errorBody.Contains("wait"))
                    {
                        Console.WriteLine("[CONVERT] 1fichier still scanning file, waiting...");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONVERT] Exception on attempt {attempt}: {ex.Message}");
            }

            if (attempt < maxRetries)
            {
                Console.WriteLine($"[CONVERT] Waiting {retryDelay / 1000}s before retry...");
                await Task.Delay(retryDelay).ConfigureAwait(false);
            }
        }

        Console.WriteLine($"[CONVERT] FAILED after {maxRetries} attempts, will use 1fichier link");
        return null; // Conversion failed, will use 1fichier link
    }

    #endregion
}
