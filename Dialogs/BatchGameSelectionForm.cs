using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using APPID.Models;
using APPID.Properties;
using APPID.Services;
using APPID.Services.Interfaces;
using APPID.Utilities.UI;
using Timer = System.Windows.Forms.Timer;

namespace APPID.Dialogs;

public class BatchGameSelectionForm : Form
{
    private const int WmNclbuttondown = 0xA1;
    private const int Htcaption = 0x2;
    private const int MaxUploadSlots = 3;

    // Service dependencies
    private readonly IAppIdDetectionService _appIdDetection;
    private readonly Dictionary<string, string> _convertingUrls = []; // gamePath -> 1fichier URL during conversion
    private readonly Dictionary<string, CrackDetails> _crackDetailsMap = []; // gamePath -> crack details
    private readonly Dictionary<int, string> _detectedAppIds = [];
    private readonly Dictionary<string, string> _finalUrls = []; // gamePath -> final URL (pydrive or 1fichier)
    private readonly IFormattingService _formatting;
    private readonly IBatchGameDataService _gameData;
    private readonly List<string> _gamePaths;
    private readonly UploadSlot[] _uploadSlots = new UploadSlot[MaxUploadSlots];

    private string _allLinksMarkdown = string.Empty;
    private string _allLinksPhpBb = string.Empty;
    private string? _allLinksPlaintext;

    private ToolTip? _batchToolTip;
    private Button? _btnCancelAll;
    private bool _cancelAllRemaining;
    private Button? _copyDiscordBtn;
    private Button? _copyPlaintextBtn;
    private Button? _copyRinBtn;

    private DataGridView? _gameGrid;
    private Panel? _uploadSlotsContainer;

    public BatchGameSelectionForm(List<string> paths, IAppIdDetectionService? appIdDetection = null,
        IBatchGameDataService? gameData = null, IFormattingService? formatting = null)
    {
        _gamePaths = paths;

        // Initialize services - use provided or create defaults
        var fileSystem = new FileSystemService();
        var manifestParsing = new ManifestParsingService(fileSystem);
        var cache = new SteamCacheManager();
        _appIdDetection = appIdDetection ?? new AppIdDetectionService(fileSystem, manifestParsing, cache);
        _gameData = gameData ?? new BatchGameDataService(fileSystem);
        _formatting = formatting ?? new FormattingService();

        InitializeForm();

        Load += (s, e) =>
        {
            ApplyAcrylicEffect();
            CenterToParentWithScreenClamp();
            // Load icon from resources
            try
            {
                Icon = Resources.sac_icon;
            }
            catch
            {
                // ignored
            }
        };
        MouseDown += Form_MouseDown;

        // ESC key minimizes form (same as minimize button)
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                WindowState = FormWindowState.Minimized;
                e.Handled = true;
            }
        };
    }

    /// <summary>
    ///     Gets the list of games selected for batch processing.
    /// </summary>
    public List<BatchGameItem> SelectedGames { get; } = [];

    /// <summary>
    ///     Gets or sets the compression format to use (ZIP or 7Z).
    /// </summary>
    public string CompressionFormat { get; private set; } = "ZIP";

    /// <summary>
    ///     Gets or sets the compression level.
    /// </summary>
    public string CompressionLevel { get; private set; } = "0";

    /// <summary>
    ///     Gets or sets whether to use Rin password for compression.
    /// </summary>
    public bool UseRinPassword { get; private set; }

    /// <summary>
    ///     Track if batch processing is active
    /// </summary>
    public bool IsProcessing { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    // Event for when Process is clicked
    public event Action<List<BatchGameItem>, string, string, bool> ProcessRequested;

    private void CenterToParentWithScreenClamp()
    {
        if (Owner != null)
        {
            // Center relative to parent
            int x = Owner.Location.X + (Owner.Width - Width) / 2;
            int y = Owner.Location.Y + (Owner.Height - Height) / 2;

            // Clamp to screen bounds
            var screen = Screen.FromControl(Owner).WorkingArea;
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - Width));
            y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - Height));

            Location = new Point(x, y);
        }
    }

    private void InitializeForm()
    {
        Text = "Batch Process - Select Games";
        Size = new Size(760, 580);
        MinimumSize = new Size(760, 300);
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(5, 8, 20);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true; // Show in taskbar since main form hides

        // Title label
        var titleLabel = new Label
        {
            Name = "titleLabel",
            Text = "Batch Process",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 200, 255),
            Location = new Point(15, 15),
            Size = new Size(200, 30),
            BackColor = Color.Transparent
        };
        Controls.Add(titleLabel);

        // Minimize button (custom, top right)
        var minimizeBtn = new Label
        {
            Text = "─",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(150, 150, 155),
            Location = new Point(ClientSize.Width - 35, 10),
            Size = new Size(30, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        minimizeBtn.Click += (s, e) =>
        {
            WindowState = FormWindowState.Minimized;
        };
        minimizeBtn.MouseEnter += (s, e) => minimizeBtn.ForeColor = Color.FromArgb(100, 200, 255);
        minimizeBtn.MouseLeave += (s, e) => minimizeBtn.ForeColor = Color.FromArgb(150, 150, 155);
        Controls.Add(minimizeBtn);
        minimizeBtn.BringToFront();

        // Subtitle
        var subtitleLabel = new Label
        {
            Text = $"Found {_gamePaths.Count} games. Select actions for each:",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(180, 180, 185),
            Location = new Point(15, 48),
            Size = new Size(480, 20),
            BackColor = Color.Transparent
        };
        Controls.Add(subtitleLabel);

        // DataGridView for games - anchored to resize with form
        _gameGrid = new DataGridView
        {
            Location = new Point(10, 75),
            Size = new Size(735, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackgroundColor = Color.FromArgb(15, 18, 30),
            ForeColor = Color.FromArgb(220, 255, 255),
            GridColor = Color.FromArgb(50, 55, 70),
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            Font = new Font("Segoe UI", 9)
        };

        // Style headers - semi-transparent dark
        _gameGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 25, 40);
        _gameGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(150, 200, 255);
        _gameGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _gameGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(20, 25, 40);
        _gameGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _gameGrid.ColumnHeadersHeight = 40;

        // Style rows - semi-transparent dark
        _gameGrid.DefaultCellStyle.BackColor = Color.FromArgb(12, 15, 28);
        _gameGrid.DefaultCellStyle.ForeColor = Color.FromArgb(220, 255, 255);
        _gameGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 70, 110);
        _gameGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        _gameGrid.RowTemplate.Height = 28;

        // Alternate row style - slightly different shade
        _gameGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(18, 22, 38);

        // Add columns
        var nameCol = new DataGridViewTextBoxColumn
        {
            Name = "GameName", HeaderText = "Game", Width = 200, ReadOnly = true
        };
        _gameGrid.Columns.Add(nameCol);

        var appIdCol = new DataGridViewTextBoxColumn
        {
            Name = "AppId",
            HeaderText = "AppID",
            Width = 70,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(100, 200, 255)
            }
        };
        _gameGrid.Columns.Add(appIdCol);

        var sizeCol = new DataGridViewTextBoxColumn
        {
            Name = "Size",
            HeaderText = "Size",
            Width = 60,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        };
        _gameGrid.Columns.Add(sizeCol);

        var crackCol = new DataGridViewCheckBoxColumn { Name = "Crack", HeaderText = "Crack", Width = 55 };
        _gameGrid.Columns.Add(crackCol);

        var zipCol = new DataGridViewCheckBoxColumn { Name = "Zip", HeaderText = "Zip", Width = 45 };
        _gameGrid.Columns.Add(zipCol);

        var uploadCol = new DataGridViewCheckBoxColumn { Name = "Upload", HeaderText = "Upload", Width = 55 };
        _gameGrid.Columns.Add(uploadCol);

        // Add tooltips to checkbox column headers
        crackCol.ToolTipText = "Click header to select/deselect all";
        zipCol.ToolTipText = "Click header to select/deselect all";
        uploadCol.ToolTipText = "Click header to select/deselect all";

        var statusCol = new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.Gray
            }
        };
        _gameGrid.Columns.Add(statusCol);

        var detailsCol = new DataGridViewTextBoxColumn
        {
            Name = "Details", HeaderText = "", Width = 40, ReadOnly = true
        };
        _gameGrid.Columns.Add(detailsCol);

        // Track header checkbox states
        Dictionary<string, bool> headerCheckStates = new() { ["Crack"] = true, ["Zip"] = false, ["Upload"] = false };

        // Custom paint for cells and headers
        Image? infoIcon = null;
        Image? zipperIcon = null;
        try { infoIcon = Resources.info_icon; }
        catch
        {
            // ignored
        }

        try { zipperIcon = Resources.zipper_icon; }
        catch
        {
            // ignored
        }

        _gameGrid.CellPainting += (s, e) =>
        {
            // Paint checkbox column HEADERS with actual checkbox
            if (e is { RowIndex: -1, ColumnIndex: >= 0 })
            {
                string colName = _gameGrid.Columns[e.ColumnIndex].Name;
                if (colName == "Crack" || colName == "Zip" || colName == "Upload")
                {
                    e.PaintBackground(e.ClipBounds, true);

                    // Draw text above checkbox
                    using (var brush = new SolidBrush(Color.FromArgb(150, 200, 255)))
                    using (var font = new Font("Segoe UI", 8, FontStyle.Bold))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center };
                        e.Graphics.DrawString(colName, font, brush, e.CellBounds.X + e.CellBounds.Width / 2,
                            e.CellBounds.Y + 3, sf);
                    }

                    // Draw checkbox below text (16px)
                    int boxSize = 16;
                    int boxX = e.CellBounds.X + (e.CellBounds.Width - boxSize) / 2;
                    int boxY = e.CellBounds.Y + 18;
                    var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    bool isChecked = headerCheckStates.ContainsKey(colName) && headerCheckStates[colName];

                    if (isChecked)
                    {
                        // Filled rounded rectangle - bright blue like cell checkboxes
                        using (var path = new GraphicsPath())
                        {
                            int r = 4; // corner radius
                            path.AddArc(boxRect.X, boxRect.Y, r * 2, r * 2, 180, 90);
                            path.AddArc(boxRect.Right - r * 2, boxRect.Y, r * 2, r * 2, 270, 90);
                            path.AddArc(boxRect.Right - r * 2, boxRect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                            path.AddArc(boxRect.X, boxRect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                            path.CloseFigure();

                            using (var brush = new SolidBrush(Color.FromArgb(60, 150, 220)))
                            {
                                e.Graphics.FillPath(brush, path);
                            }
                        }

                        // White checkmark (scaled for 16px)
                        using (var pen = new Pen(Color.White, 2f))
                        {
                            e.Graphics.DrawLine(pen, boxX + 3, boxY + 8, boxX + 6, boxY + 11);
                            e.Graphics.DrawLine(pen, boxX + 6, boxY + 11, boxX + 12, boxY + 4);
                        }
                    }
                    else
                    {
                        // Empty rounded rectangle - light gray outline like cell checkboxes
                        using var path = new GraphicsPath();
                        int r = 4;
                        path.AddArc(boxRect.X, boxRect.Y, r * 2, r * 2, 180, 90);
                        path.AddArc(boxRect.Right - r * 2, boxRect.Y, r * 2, r * 2, 270, 90);
                        path.AddArc(boxRect.Right - r * 2, boxRect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                        path.AddArc(boxRect.X, boxRect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                        path.CloseFigure();

                        using var pen = new Pen(Color.FromArgb(180, 180, 190), 1.5f);
                        e.Graphics.DrawPath(pen, path);
                    }

                    e.Handled = true;
                }
            }

            if (e is { ColumnIndex: >= 0, RowIndex: >= 0 })
            {
                string colName = _gameGrid.Columns[e.ColumnIndex].Name;

                // Custom paint checkbox cells (Crack, Zip, Upload) - styled
                if (colName == "Crack" || colName == "Zip" || colName == "Upload")
                {
                    e.PaintBackground(e.ClipBounds, true);

                    int boxSize = 15;
                    int boxX = e.CellBounds.X + (e.CellBounds.Width - boxSize) / 2;
                    int boxY = e.CellBounds.Y + (e.CellBounds.Height - boxSize) / 2;
                    var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    bool isChecked = (bool)(e.Value ?? false);

                    if (isChecked)
                    {
                        // Filled rounded rectangle - bright blue
                        using (var path = new GraphicsPath())
                        {
                            int r = 3; // corner radius
                            path.AddArc(boxRect.X, boxRect.Y, r * 2, r * 2, 180, 90);
                            path.AddArc(boxRect.Right - r * 2, boxRect.Y, r * 2, r * 2, 270, 90);
                            path.AddArc(boxRect.Right - r * 2, boxRect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                            path.AddArc(boxRect.X, boxRect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                            path.CloseFigure();

                            using (var brush = new SolidBrush(Color.FromArgb(60, 150, 220)))
                            {
                                e.Graphics.FillPath(brush, path);
                            }
                        }

                        // White checkmark (scaled for 15px)
                        using (var pen = new Pen(Color.White, 1.8f))
                        {
                            e.Graphics.DrawLine(pen, boxX + 3, boxY + 7, boxX + 5, boxY + 10);
                            e.Graphics.DrawLine(pen, boxX + 5, boxY + 10, boxX + 11, boxY + 4);
                        }
                    }
                    else
                    {
                        // Empty rounded rectangle - light gray outline
                        using var path = new GraphicsPath();
                        int r = 3;
                        path.AddArc(boxRect.X, boxRect.Y, r * 2, r * 2, 180, 90);
                        path.AddArc(boxRect.Right - r * 2, boxRect.Y, r * 2, r * 2, 270, 90);
                        path.AddArc(boxRect.Right - r * 2, boxRect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                        path.AddArc(boxRect.X, boxRect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                        path.CloseFigure();

                        using var pen = new Pen(Color.FromArgb(180, 180, 190), 1.5f);
                        e.Graphics.DrawPath(pen, path);
                    }

                    e.Handled = true;
                }
                // Details column - info icon
                else if (colName == "Details")
                {
                    e.PaintBackground(e.ClipBounds, true);
                    if (infoIcon != null)
                    {
                        int iconSize = Math.Min(e.CellBounds.Width - 8, e.CellBounds.Height - 8);
                        iconSize = Math.Min(iconSize, 20);
                        int iconX = e.CellBounds.X + (e.CellBounds.Width - iconSize) / 2;
                        int iconY = e.CellBounds.Y + (e.CellBounds.Height - iconSize) / 2;
                        e.Graphics.DrawImage(infoIcon, iconX, iconY, iconSize, iconSize);
                    }
                    else
                    {
                        using var textBrush = new SolidBrush(Color.FromArgb(150, 200, 255));
                        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
                        };
                        e.Graphics.DrawString("ⓘ", font, textBrush, e.CellBounds, sf);
                    }

                    e.Handled = true;
                }
                // Status column - zipper icon when zipping
                else if (colName == "Status" && zipperIcon != null)
                {
                    string status = e.Value?.ToString() ?? "";
                    if (status.StartsWith("Zipping"))
                    {
                        e.PaintBackground(e.ClipBounds, true);

                        // Draw zipper icon on left - maintain actual aspect ratio
                        int maxHeight = e.CellBounds.Height - 6;
                        float aspectRatio = (float)zipperIcon.Width / zipperIcon.Height;
                        int iconHeight = maxHeight;
                        int iconWidth = (int)(iconHeight * aspectRatio);
                        int iconX = e.CellBounds.X + 4;
                        int iconY = e.CellBounds.Y + 3;
                        e.Graphics.DrawImage(zipperIcon, iconX, iconY, iconWidth, iconHeight);

                        // Draw percentage text next to icon
                        string pctText = status.Replace("Zipping", "").Trim();
                        if (!string.IsNullOrEmpty(pctText))
                        {
                            using var textBrush = new SolidBrush(Color.Cyan);
                            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                            var textRect = new RectangleF(iconX + iconWidth + 4, e.CellBounds.Y,
                                e.CellBounds.Width - iconWidth - 12, e.CellBounds.Height);
                            var sf = new StringFormat
                            {
                                Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center
                            };
                            e.Graphics.DrawString(pctText, font, textBrush, textRect, sf);
                        }

                        e.Handled = true;
                    }
                }
            }
        };

        // Load games asynchronously to avoid UI freeze
        LoadGamesAsync();

        // Handle clicks on cells
        _gameGrid.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            string colName = _gameGrid.Columns[e.ColumnIndex].Name;

            // Single-click on AppID opens search dialog (works for any value)
            if (colName == "AppId")
            {
                string gameName = _gameGrid.Rows[e.RowIndex].Cells["GameName"].Value?.ToString() ?? "";
                string currentAppId = _gameGrid.Rows[e.RowIndex].Cells["AppId"].Value?.ToString() ?? "";
                if (currentAppId == "?")
                {
                    currentAppId = "";
                }

                string newAppId = ShowAppIdSearchDialog(gameName, currentAppId);
                if (newAppId != null)
                {
                    _detectedAppIds[e.RowIndex] = newAppId;
                    _gameGrid.Rows[e.RowIndex].Cells["AppId"].Value = string.IsNullOrEmpty(newAppId) ? "?" : newAppId;

                    var cell = _gameGrid.Rows[e.RowIndex].Cells["AppId"];
                    if (string.IsNullOrEmpty(newAppId))
                    {
                        cell.Style.ForeColor = Color.Orange;
                        cell.Style.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                    }
                    else
                    {
                        cell.Style.ForeColor = Color.FromArgb(100, 200, 255);
                        cell.Style.Font = new Font("Segoe UI", 9, FontStyle.Regular);
                    }
                }

                return;
            }

            // Single-click on Status column - copy URL if available
            if (colName == "Status")
            {
                string gamePath = _gameGrid.Rows[e.RowIndex].Tag?.ToString();
                string status = _gameGrid.Rows[e.RowIndex].Cells["Status"].Value?.ToString() ?? "";
                string urlToCopy = null;

                // Check for converting URL (1fichier link during conversion)
                if (status.StartsWith("Converting") && !string.IsNullOrEmpty(gamePath) &&
                    _convertingUrls.TryGetValue(gamePath, out string? url))
                {
                    urlToCopy = url;
                }
                // Check for final URL (PyDrive or 1fichier after completion)
                else if ((status.Contains("PyDrive") || status.Contains("1fichier")) &&
                         !string.IsNullOrEmpty(gamePath) && _finalUrls.TryGetValue(gamePath, out string? finalUrl))
                {
                    urlToCopy = finalUrl;
                }

                if (!string.IsNullOrEmpty(urlToCopy))
                {
                    try
                    {
                        Clipboard.SetText(urlToCopy);
                        // Visual feedback - show "Copied! ✓" for 3 seconds
                        string originalStatus = status;
                        Color originalColor = _gameGrid.Rows[e.RowIndex].Cells["Status"].Style.ForeColor;
                        _gameGrid.Rows[e.RowIndex].Cells["Status"].Value = "Copied! ✓";
                        _gameGrid.Rows[e.RowIndex].Cells["Status"].Style.ForeColor = Color.Cyan;

                        var timer = new Timer { Interval = 3000 };
                        int rowIdx = e.RowIndex;
                        timer.Tick += (ts, te) =>
                        {
                            timer.Stop();
                            timer.Dispose();
                            if (!IsDisposed && rowIdx < _gameGrid.Rows.Count)
                            {
                                _gameGrid.Rows[rowIdx].Cells["Status"].Value = originalStatus;
                                _gameGrid.Rows[rowIdx].Cells["Status"].Style.ForeColor = originalColor;
                            }
                        };
                        timer.Start();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return;
            }

            // Handle checkbox columns
            if (colName == "Crack" || colName == "Zip" || colName == "Upload")
            {
                var cell = _gameGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                bool currentValue = (bool)(cell.Value ?? false);
                bool newValue = !currentValue;
                cell.Value = newValue;

                // Dependency logic - Updated to allow zip/share without crack
                // Valid combos: crack, crack+zip, crack+zip+upload, zip, zip+upload
                // Invalid: upload without zip (1fichier requires zipped file)
                if (colName == "Upload" && newValue)
                {
                    // Upload requires Zip only (not Crack - can share uncracked games)
                    _gameGrid.Rows[e.RowIndex].Cells["Zip"].Value = true;
                }
                else if (colName == "Zip" && !newValue)
                {
                    // Unchecking Zip unchecks Upload (can't upload without zip)
                    _gameGrid.Rows[e.RowIndex].Cells["Upload"].Value = false;
                }
                // Note: Crack is now fully independent - unchecking it does NOT affect Zip/Upload

                UpdateCountLabel();
            }

            // Handle Details button click
            if (colName == "Details")
            {
                string gamePath = _gameGrid.Rows[e.RowIndex].Tag?.ToString();
                if (!string.IsNullOrEmpty(gamePath) &&
                    _crackDetailsMap.TryGetValue(gamePath, out CrackDetails? value))
                {
                    ShowCrackDetails(value);
                }
                else
                {
                    MessageBox.Show("No crack details available yet.\nDetails are populated after cracking.",
                        "No Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        };

        Controls.Add(_gameGrid);

        // Initialize tooltip
        _batchToolTip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 300, ReshowDelay = 200 };

        // Compression settings button - bottom left, anchored (custom painted with image)
        var settingsBtn = new Button
        {
            Location = new Point(15, 490),
            Size = new Size(35, 35),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        settingsBtn.FlatAppearance.BorderSize = 0;
        settingsBtn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        settingsBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;

        Image settingsIcon = null;
        try { settingsIcon = Resources.settings_icon; }
        catch
        {
            // ignored
        }

        settingsBtn.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, settingsBtn.Width - 1, settingsBtn.Height - 1);

            // Background
            Color bgColor = settingsBtn.ClientRectangle.Contains(settingsBtn.PointToClient(Cursor.Position))
                ? Color.FromArgb(50, 50, 55)
                : Color.FromArgb(38, 38, 42);
            using (var path = CreateRoundedRectPath(rect, 8))
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            // Draw icon centered with proper aspect ratio
            if (settingsIcon != null)
            {
                int padding = 6;
                int availableW = settingsBtn.Width - padding * 2;
                int availableH = settingsBtn.Height - padding * 2;

                // Calculate scaled size maintaining aspect ratio
                float scale = Math.Min((float)availableW / settingsIcon.Width, (float)availableH / settingsIcon.Height);
                int drawW = (int)(settingsIcon.Width * scale);
                int drawH = (int)(settingsIcon.Height * scale);

                // Center it
                int drawX = padding + (availableW - drawW) / 2;
                int drawY = padding + (availableH - drawH) / 2;

                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(settingsIcon, drawX, drawY, drawW, drawH);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, "⚙", new Font("Segoe UI", 12), rect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
        settingsBtn.MouseEnter += (s, e) => settingsBtn.Invalidate();
        settingsBtn.MouseLeave += (s, e) => settingsBtn.Invalidate();
        settingsBtn.Click += (s, e) => OpenCompressionSettings();
        _batchToolTip.SetToolTip(settingsBtn, "Compression settings");
        Controls.Add(settingsBtn);

        // compressionLabel removed - user didn't want it

        // Column header click toggles all/none for checkbox columns
        _gameGrid.ColumnHeaderMouseClick += (s, e) =>
        {
            string colName = _gameGrid.Columns[e.ColumnIndex].Name;
            if (colName == "Crack" || colName == "Zip" || colName == "Upload")
            {
                // Toggle based on header state: if header checked, uncheck all; if unchecked, check all
                bool headerChecked = headerCheckStates.ContainsKey(colName) && headerCheckStates[colName];
                bool newValue = !headerChecked;
                foreach (DataGridViewRow row in _gameGrid.Rows)
                {
                    row.Cells[colName].Value = newValue;
                    // Dependency: Upload requires Zip
                    if (colName == "Upload" && newValue)
                    {
                        row.Cells["Zip"].Value = true;
                    }
                    else if (colName == "Zip" && !newValue)
                    {
                        row.Cells["Upload"].Value = false;
                    }
                }

                // Update header checkbox state and refresh
                headerCheckStates[colName] = newValue;
                if (colName == "Upload" && newValue)
                {
                    headerCheckStates["Zip"] = true;
                }
                else if (colName == "Zip" && !newValue)
                {
                    headerCheckStates["Upload"] = false;
                }

                _gameGrid.InvalidateColumn(_gameGrid.Columns[colName].Index);
                if (colName == "Upload" || colName == "Zip")
                {
                    _gameGrid.InvalidateColumn(_gameGrid.Columns["Zip"].Index);
                    _gameGrid.InvalidateColumn(_gameGrid.Columns["Upload"].Index);
                }

                UpdateCountLabel();
            }
        };

        // Commit checkbox edits immediately so CellValueChanged fires right away
        _gameGrid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_gameGrid.IsCurrentCellDirty && _gameGrid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _gameGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        // When individual row checkboxes change, update header checkbox state
        _gameGrid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0)
            {
                return; // Ignore header row
            }

            string colName = _gameGrid.Columns[e.ColumnIndex].Name;
            if (colName == "Crack" || colName == "Zip" || colName == "Upload")
            {
                // Check if ALL rows are checked for this column
                bool allChecked = true;
                foreach (DataGridViewRow row in _gameGrid.Rows)
                {
                    if (!(bool)(row.Cells[colName].Value ?? false))
                    {
                        allChecked = false;
                        break;
                    }
                }

                // Update header state only if it changed
                if (headerCheckStates[colName] != allChecked)
                {
                    headerCheckStates[colName] = allChecked;
                    _gameGrid.InvalidateColumn(e.ColumnIndex);
                }

                UpdateCountLabel();
            }
        };

        // Upload slots container (hidden by default, shown during uploads)
        // Docked to bottom of form
        _uploadSlotsContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 114, // 3 slots * 38px
            BackColor = Color.FromArgb(5, 8, 20),
            Visible = false,
            Padding = new Padding(15, 0, 15, 0)
        };

        // Create 3 upload slots (added in reverse order for proper Dock.Top stacking)
        for (int i = MaxUploadSlots - 1; i >= 0; i--)
        {
            var slot = new UploadSlot
            {
                Panel =
                    new Panel
                    {
                        Dock = DockStyle.Top,
                        Height = 36,
                        BackColor = Color.FromArgb(15, 18, 28),
                        Visible = false,
                        Margin = new Padding(0, 2, 0, 0)
                    },
                LblGame = new Label
                {
                    Location = new Point(5, 2),
                    Size = new Size(280, 14),
                    ForeColor = Color.FromArgb(100, 200, 255),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Text = ""
                }
            };

            slot.Panel.Controls.Add(slot.LblGame);

            slot.ProgressBar = new NeonProgressBar
            {
                Location = new Point(5, 18), Size = new Size(280, 12), Maximum = 100
            };
            slot.Panel.Controls.Add(slot.ProgressBar);

            slot.LblSize = new Label
            {
                Location = new Point(290, 18),
                Size = new Size(100, 14),
                ForeColor = Color.FromArgb(180, 180, 185),
                Font = new Font("Segoe UI", 7.5f),
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft
            };
            slot.Panel.Controls.Add(slot.LblSize);

            slot.LblSpeed = new Label
            {
                Location = new Point(395, 18),
                Size = new Size(75, 14),
                ForeColor = Color.FromArgb(100, 255, 150),
                Font = new Font("Segoe UI", 7.5f),
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft
            };
            slot.Panel.Controls.Add(slot.LblSpeed);

            slot.LblEta = new Label
            {
                Location = new Point(475, 18),
                Size = new Size(70, 14),
                ForeColor = Color.FromArgb(255, 200, 100),
                Font = new Font("Segoe UI", 7.5f),
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft
            };
            slot.Panel.Controls.Add(slot.LblEta);

            // Skip button for this slot - anchored to right
            int slotIndex = i;
            slot.BtnSkip = new Button
            {
                Size = new Size(55, 28),
                Text = "Skip",
                BackColor = Color.FromArgb(80, 70, 20),
                ForeColor = Color.FromArgb(255, 220, 120),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            slot.BtnSkip.FlatAppearance.BorderColor = Color.FromArgb(150, 120, 40);
            slot.BtnSkip.Location = new Point(660, 4); // Initial position, will adjust on resize
            slot.Panel.Resize += (s, e) =>
            {
                slot.BtnSkip.Location = new Point(slot.Panel.ClientSize.Width - slot.BtnSkip.Width - 10, 4);
            };
            slot.BtnSkip.Click += (s, e) =>
            {
                _uploadSlots[slotIndex].Cancellation?.Cancel();
                slot.BtnSkip.Text = "...";
                slot.BtnSkip.Enabled = false;
            };
            slot.Panel.Controls.Add(slot.BtnSkip);

            _uploadSlotsContainer.Controls.Add(slot.Panel);
            _uploadSlots[i] = slot;
        }

        Controls.Add(_uploadSlotsContainer);

        // Cancel All button - above upload slots on right, shown during uploads
        _btnCancelAll = CreateStyledButton("Cancel All", new Point(ClientSize.Width - 125, 380), new Size(110, 28),
            Color.FromArgb(140, 50, 50));
        _btnCancelAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _btnCancelAll.Visible = false;
        _batchToolTip.SetToolTip(_btnCancelAll, "Cancel all remaining uploads");
        _btnCancelAll.Click += (s, e) =>
        {
            _cancelAllRemaining = true;
            foreach (var slot in _uploadSlots)
            {
                slot.Cancellation?.Cancel();
                if (slot.BtnSkip != null)
                {
                    slot.BtnSkip.Enabled = false;
                }
            }

            _btnCancelAll.Text = "Cancelling...";
            _btnCancelAll.Enabled = false;
        };
        Controls.Add(_btnCancelAll);

        // Start button - bottom right, anchored
        var processBtn = CreateStyledButton("Start", new Point(573, 485), new Size(80, 40), Color.FromArgb(0, 100, 70));
        processBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        processBtn.Click += (s, e) =>
        {
            // Check for missing AppIDs on games that need cracking
            List<string> missingAppIds = [];
            for (int i = 0; i < _gameGrid.Rows.Count; i++)
            {
                var row = _gameGrid.Rows[i];
                bool crack = (bool)(row.Cells["Crack"].Value ?? false);
                string appId = row.Cells["AppId"].Value?.ToString();

                if (crack && (string.IsNullOrEmpty(appId) || appId == "?"))
                {
                    missingAppIds.Add(row.Cells["GameName"].Value?.ToString() ?? "Unknown");
                }
            }

            if (missingAppIds.Count > 0)
            {
                string msg = "The following games are missing AppIDs:\n\n" +
                             string.Join("\n", missingAppIds.Take(5));
                if (missingAppIds.Count > 5)
                {
                    msg += $"\n...and {missingAppIds.Count - 5} more";
                }

                msg +=
                    "\n\nDouble-click on the AppID column to set them.\nContinue anyway? (games without AppID will be skipped)";

                if (MessageBox.Show(msg, "Missing AppIDs", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) !=
                    DialogResult.Yes)
                {
                    return;
                }
            }

            // Clear previous selections before adding new ones
            SelectedGames.Clear();

            for (int i = 0; i < _gameGrid.Rows.Count; i++)
            {
                var row = _gameGrid.Rows[i];
                bool crack = (bool)(row.Cells["Crack"].Value ?? false);
                bool zip = (bool)(row.Cells["Zip"].Value ?? false);
                bool upload = (bool)(row.Cells["Upload"].Value ?? false);
                string appId = row.Cells["AppId"].Value?.ToString();
                if (appId == "?")
                {
                    appId = "";
                }

                if (crack || zip || upload)
                {
                    SelectedGames.Add(new BatchGameItem
                    {
                        Name = Path.GetFileName(_gamePaths[i]),
                        Path = _gamePaths[i],
                        AppId = appId,
                        ShouldCrack = crack,
                        ShouldZip = zip,
                        ShouldUpload = upload
                    });
                }
            }

            if (SelectedGames.Count == 0)
            {
                MessageBox.Show("Please select at least one action for at least one game.",
                    "No Actions Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Fire the event - form stays open
            ProcessRequested?.Invoke(SelectedGames, CompressionFormat, CompressionLevel, UseRinPassword);
        };
        _batchToolTip.SetToolTip(processBtn, "Start processing selected games (crack, zip, upload)");
        Controls.Add(processBtn);

        // Cancel button - bottom right, anchored (closer to Start)
        var cancelBtn = CreateStyledButton("Cancel", new Point(658, 485), new Size(70, 40));
        cancelBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        cancelBtn.Click += (s, e) =>
        {
            Close();
        };
        _batchToolTip.SetToolTip(cancelBtn, "Close this window without processing");
        Controls.Add(cancelBtn);
    }

    /// <summary>
    ///     Updates the status for a game by its path
    /// </summary>
    public void UpdateStatus(string gamePath, string status, Color? color = null)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateStatus(gamePath, status, color)); }
            catch
            {
                // ignored
            }

            return;
        }

        for (int i = 0; i < _gameGrid.Rows.Count; i++)
        {
            if (_gameGrid.Rows[i].Tag?.ToString() == gamePath)
            {
                _gameGrid.Rows[i].Cells["Status"].Value = status;
                if (color.HasValue)
                {
                    _gameGrid.Rows[i].Cells["Status"].Style.ForeColor = color.Value;
                }

                break;
            }
        }
    }

    /// <summary>
    ///     Updates the status for a game by row index
    /// </summary>
    public void UpdateStatusByIndex(int rowIndex, string status, Color? color = null)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateStatusByIndex(rowIndex, status, color)); }
            catch
            {
                // ignored
            }

            return;
        }

        if (rowIndex >= 0 && rowIndex < _gameGrid.Rows.Count)
        {
            _gameGrid.Rows[rowIndex].Cells["Status"].Value = status;
            if (color.HasValue)
            {
                _gameGrid.Rows[rowIndex].Cells["Status"].Style.ForeColor = color.Value;
            }
        }
    }

    /// <summary>
    ///     Stores the 1fichier URL for a game during conversion (allows click-to-copy)
    /// </summary>
    public void SetConvertingUrl(string gamePath, string oneFichierUrl)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => SetConvertingUrl(gamePath, oneFichierUrl)); }
            catch
            {
                // ignored
            }

            return;
        }

        if (!string.IsNullOrEmpty(gamePath) && !string.IsNullOrEmpty(oneFichierUrl))
        {
            _convertingUrls[gamePath] = oneFichierUrl;
        }
    }

    /// <summary>
    ///     Clears the converting URL for a game (after conversion completes)
    /// </summary>
    public void ClearConvertingUrl(string gamePath)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => ClearConvertingUrl(gamePath)); }
            catch
            {
                // ignored
            }

            return;
        }

        _convertingUrls.Remove(gamePath);
    }

    /// <summary>
    ///     Stores the final URL for a game (for click-to-copy after completion)
    /// </summary>
    public void SetFinalUrl(string gamePath, string finalUrl)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => SetFinalUrl(gamePath, finalUrl)); }
            catch
            {
                // ignored
            }

            return;
        }

        if (!string.IsNullOrEmpty(gamePath) && !string.IsNullOrEmpty(finalUrl))
        {
            _finalUrls[gamePath] = finalUrl;
        }
    }

    /// <summary>
    ///     Claim an upload slot for a game. Returns slot index (-1 if none available) and sets up cancellation.
    /// </summary>
    public int ClaimUploadSlot(string gamePath, string gameName, long totalBytes)
    {
        if (IsDisposed)
        {
            return -1;
        }

        if (InvokeRequired)
        {
            return Invoke(() => ClaimUploadSlot(gamePath, gameName, totalBytes));
        }

        // Find a free slot
        for (int i = 0; i < MaxUploadSlots; i++)
        {
            if (!_uploadSlots[i].InUse)
            {
                var slot = _uploadSlots[i];
                slot.InUse = true;
                slot.GamePath = gamePath;
                slot.Cancellation?.Dispose();
                slot.Cancellation = new CancellationTokenSource();

                slot.LblGame.Text = gameName;
                slot.LblSize.Text = $"0 / {_gameData.FormatFileSize(totalBytes)}";
                slot.LblSpeed.Text = "";
                slot.LblEta.Text = "";
                slot.ProgressBar.Value = 0;
                slot.BtnSkip.Text = "Skip";
                slot.BtnSkip.Enabled = !_cancelAllRemaining;
                slot.Panel.Visible = true;

                // Show container, cancel button, and reposition visible slots
                _uploadSlotsContainer.Visible = true;
                _uploadSlotsContainer.BringToFront();
                _btnCancelAll.Visible = true;
                _btnCancelAll.BringToFront();
                RepositionUploadSlots();

                return i;
            }
        }

        return -1; // No slot available
    }

    /// <summary>
    ///     Get the cancellation token for a specific slot
    /// </summary>
    public CancellationToken GetSlotCancellationToken(int slotIndex)
    {
        if (slotIndex is >= 0 and < MaxUploadSlots && _uploadSlots[slotIndex].Cancellation != null)
        {
            return _uploadSlots[slotIndex].Cancellation.Token;
        }

        return CancellationToken.None;
    }

    /// <summary>
    ///     Update progress for a specific upload slot
    /// </summary>
    public void UpdateSlotProgress(int slotIndex, int percent, long uploadedBytes, long totalBytes,
        double bytesPerSecond)
    {
        if (IsDisposed || slotIndex < 0 || slotIndex >= MaxUploadSlots)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => UpdateSlotProgress(slotIndex, percent, uploadedBytes, totalBytes, bytesPerSecond));
            }
            catch
            {
                // ignored
            }

            return;
        }

        var slot = _uploadSlots[slotIndex];
        if (!slot.InUse)
        {
            return;
        }

        slot.ProgressBar.Value = Math.Min(percent, 100);
        slot.ProgressBar.Invalidate();
        slot.LblSize.Text = $"{_gameData.FormatFileSize(uploadedBytes)} / {_gameData.FormatFileSize(totalBytes)}";

        if (bytesPerSecond > 0)
        {
            slot.LblSpeed.Text = $"{_gameData.FormatFileSize((long)bytesPerSecond)}/s";
            long remainingBytes = totalBytes - uploadedBytes;
            if (remainingBytes > 0)
            {
                double secondsRemaining = remainingBytes / bytesPerSecond;
                slot.LblEta.Text = _formatting.FormatEta(secondsRemaining);
            }
            else
            {
                slot.LblEta.Text = "";
            }
        }
    }

    /// <summary>
    ///     Release an upload slot when done
    /// </summary>
    public void ReleaseUploadSlot(int slotIndex)
    {
        if (IsDisposed || slotIndex < 0 || slotIndex >= MaxUploadSlots)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => ReleaseUploadSlot(slotIndex)); }
            catch
            {
                // ignored
            }

            return;
        }

        var slot = _uploadSlots[slotIndex];
        slot.InUse = false;
        slot.GamePath = null;
        slot.Panel.Visible = false;
        slot.Cancellation?.Dispose();
        slot.Cancellation = null;

        // Reposition remaining slots and hide container if all empty
        RepositionUploadSlots();

        bool anyInUse = false;
        foreach (var s in _uploadSlots)
        {
            if (s.InUse)
            {
                anyInUse = true;
            }
        }

        if (!anyInUse)
        {
            _uploadSlotsContainer.Visible = false;
            _btnCancelAll.Visible = false;
        }
    }

    /// <summary>
    ///     Reposition visible upload slots to stack vertically with no gaps, and Cancel button below
    /// </summary>
    private void RepositionUploadSlots()
    {
        int y = 0;
        foreach (var slot in _uploadSlots)
        {
            if (slot.InUse && slot.Panel.Visible)
            {
                slot.Panel.Location = new Point(0, y);
                y += 38;
            }
        }

        // Resize container to fit active slots
        int containerHeight = Math.Max(y, 38);
        _uploadSlotsContainer.Size = new Size(735, containerHeight);
    }

    /// <summary>
    ///     Reset cancel state - call before starting a batch
    /// </summary>
    public void ResetSkipCancelState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(ResetSkipCancelState); }
            catch
            {
                // ignored
            }

            return;
        }

        _cancelAllRemaining = false;
        _btnCancelAll.Text = "Cancel All";
        _btnCancelAll.Enabled = true;

        foreach (var slot in _uploadSlots)
        {
            slot.InUse = false;
            slot.Panel.Visible = false;
            slot.BtnSkip.Text = "Skip";
            slot.BtnSkip.Enabled = true;
        }
    }

    /// <summary>
    ///     Check if user clicked Cancel All
    /// </summary>
    public bool ShouldCancelAll() => _cancelAllRemaining;

    /// <summary>
    ///     Check if a specific slot was skipped
    /// </summary>
    public bool WasSlotSkipped(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxUploadSlots)
        {
            return false;
        }

        return _uploadSlots[slotIndex].Cancellation?.IsCancellationRequested ?? false;
    }

    /// <summary>
    ///     Hides all upload slots and cancel button
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
            catch
            {
                // ignored
            }

            return;
        }

        _uploadSlotsContainer.Visible = false;
        _btnCancelAll.Visible = false;
        _btnCancelAll.Text = "Cancel All";
        _btnCancelAll.Enabled = true;
        _cancelAllRemaining = false;
        foreach (var slot in _uploadSlots)
        {
            slot.InUse = false;
            slot.Panel.Visible = false;
        }
    }

    /// <summary>
    ///     Shows Copy All buttons after processing completes
    /// </summary>
    public void ShowCopyAllButton(string phpBbLinks)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => ShowCopyAllButton(phpBbLinks)); }
            catch
            {
                // ignored
            }

            return;
        }

        _allLinksPhpBb = phpBbLinks;

        // Build markdown version from finalUrls
        List<string> mdLinks = [];
        List<string> plainLinks = [];
        foreach (var kvp in _finalUrls)
        {
            string gameName = Path.GetFileName(kvp.Key);
            mdLinks.Add($"[{gameName}]({kvp.Value})");

            // Determine if cracked or clean based on details
            string suffix = "(Cracked)";
            if (_crackDetailsMap.TryGetValue(kvp.Key, out CrackDetails? details))
            {
                if (details.DllsReplaced.Count == 0 && details.ExesUnpacked.Count == 0)
                {
                    suffix = "(Clean)";
                }
            }

            plainLinks.Add($"{gameName} {suffix}: {kvp.Value}");
        }

        _allLinksMarkdown = string.Join("\n", mdLinks);
        _allLinksPlaintext = string.Join("\n", plainLinks);

        if (string.IsNullOrEmpty(_allLinksPhpBb) && string.IsNullOrEmpty(_allLinksMarkdown))
        {
            return;
        }

        // Calculate Y position - same row as Cancel All button
        int buttonY = _btnCancelAll.Top;

        // Create Copy for Rin button - same row as Cancel All, anchored bottom-left
        _copyRinBtn = CreateStyledButton("Copy for Rin", new Point(10, buttonY), new Size(100, 28),
            Color.FromArgb(50, 80, 120));
        _copyRinBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _copyRinBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_allLinksPhpBb))
            {
                try
                {
                    Clipboard.SetText(_allLinksPhpBb);
                    _copyRinBtn.Text = "Copied! ✓";
                    var timer = new Timer { Interval = 2000 };
                    timer.Tick += (ts, te) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        _copyRinBtn.Text = "Copy for Rin";
                    };
                    timer.Start();
                }
                catch
                {
                    // ignored
                }
            }
        };
        Controls.Add(_copyRinBtn);

        // Create Copy for Discord button - same row as Cancel All, anchored bottom-left
        _copyDiscordBtn = CreateStyledButton("Copy for Discord", new Point(118, buttonY), new Size(120, 28),
            Color.FromArgb(88, 101, 242));
        _copyDiscordBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _copyDiscordBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_allLinksMarkdown))
            {
                try
                {
                    Clipboard.SetText(_allLinksMarkdown);
                    _copyDiscordBtn.Text = "Copied! ✓";
                    var timer = new Timer { Interval = 2000 };
                    timer.Tick += (ts, te) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        _copyDiscordBtn.Text = "Copy for Discord";
                    };
                    timer.Start();
                }
                catch
                {
                    // ignored
                }
            }
        };
        Controls.Add(_copyDiscordBtn);

        // Create Copy Plaintext button - same row as Cancel All, anchored bottom-left
        _copyPlaintextBtn = CreateStyledButton("Copy Plaintext", new Point(246, buttonY), new Size(110, 28),
            Color.FromArgb(60, 60, 70));
        _copyPlaintextBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _copyPlaintextBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_allLinksPlaintext))
            {
                try
                {
                    Clipboard.SetText(_allLinksPlaintext);
                    _copyPlaintextBtn.Text = "Copied! ✓";
                    var timer = new Timer { Interval = 2000 };
                    timer.Tick += (ts, te) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        _copyPlaintextBtn.Text = "Copy Plaintext";
                    };
                    timer.Start();
                }
                catch
                {
                    // ignored
                }
            }
        };
        Controls.Add(_copyPlaintextBtn);

        // Make sure they're on top
        _copyRinBtn.BringToFront();
        _copyDiscordBtn.BringToFront();
        _copyPlaintextBtn.BringToFront();
    }

    /// <summary>
    ///     Update the form title with progress percentage
    /// </summary>
    public void UpdateTitleProgress(int percent, string phase = null)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateTitleProgress(percent, phase)); }
            catch
            {
                // ignored
            }

            return;
        }

        string title = $"{percent}%";
        if (!string.IsNullOrEmpty(phase))
        {
            title = phase;
        }

        if (Controls["titleLabel"] is Label titleLabel)
        {
            titleLabel.Text = title;
        }
    }

    /// <summary>
    ///     Update progress with ETA display - replaces title text
    /// </summary>
    public void UpdateProgressWithEta(int percent, double etaSeconds)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateProgressWithEta(percent, etaSeconds)); }
            catch
            {
                // ignored
            }

            return;
        }

        string etaStr = percent >= 99 ? "a few seconds..." : _formatting.FormatEtaLong(etaSeconds);
        string text = $"{percent}% - ETA {etaStr}";

        if (Controls["titleLabel"] is Label titleLabel)
        {
            titleLabel.Text = text;
        }

        // Update Form1's batch indicator if it exists and form is minimized
        try
        {
            if (Owner is SteamAppId { IsDisposed: false } mainForm)
            {
                mainForm.UpdateBatchIndicator(percent);
            }
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    ///     Reset title to default or show completion message
    /// </summary>
    public void ResetTitle(string text = "Batch Process")
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => ResetTitle(text)); }
            catch
            {
                // ignored
            }

            return;
        }

        if (Controls["titleLabel"] is Label titleLabel)
        {
            titleLabel.Text = text;
        }
    }

    /// <summary>
    ///     Disables checkboxes and buttons during processing
    /// </summary>
    public void SetProcessingMode(bool processing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => SetProcessingMode(processing)); }
            catch
            {
                // ignored
            }

            return;
        }

        IsProcessing = processing;

        // Disable/enable grid interactions
        _gameGrid.ReadOnly = processing;

        // Hide count label and buttons during processing
        if (Controls["countLabel"] is Label countLabel)
        {
            countLabel.Visible = !processing;
        }

        // Find and disable/enable buttons
        foreach (Control c in Controls)
        {
            if (c is Button btn)
            {
                btn.Visible = !processing;
            }
        }
    }

    private void UpdateCountLabel()
    {
        int crackCount = 0;
        int zipCount = 0;
        int uploadCount = 0;

        foreach (DataGridViewRow row in _gameGrid.Rows)
        {
            if ((bool)(row.Cells["Crack"].Value ?? false))
            {
                crackCount++;
            }

            if ((bool)(row.Cells["Zip"].Value ?? false))
            {
                zipCount++;
            }

            if ((bool)(row.Cells["Upload"].Value ?? false))
            {
                uploadCount++;
            }
        }

        if (Controls["countLabel"] is Label countLabel)
        {
            string text = $"{crackCount} crack";
            if (zipCount > 0)
            {
                text += $", {zipCount} zip";
            }

            if (uploadCount > 0)
            {
                text += $", {uploadCount} upload";
            }

            countLabel.Text = text;
        }
    }

    private void OpenCompressionSettings()
    {
        using var form = new CompressionSettingsForm();
        form.Owner = this;
        Hide(); // Hide batch form while settings open
        if (form.ShowDialog() == DialogResult.OK)
        {
            CompressionFormat = form.SelectedFormat;
            CompressionLevel = form.SelectedLevel;
            UseRinPassword = form.UseRinPassword;
            UpdateCompressionLabel();
        }

        Show(); // Show batch form again
    }

    private void UpdateCompressionLabel()
    {
        // Label was removed - method kept for compatibility
    }

    /// <summary>
    ///     Loads games asynchronously to prevent UI freeze
    /// </summary>
    private async void LoadGamesAsync()
    {
        // Prepare game info in background
        var validGames = await Task.Run(() =>
        {
            List<(string path, string name, string appId, string size, int steamApiCount)> results = [];

            foreach (string path in _gamePaths)
            {
                if (!_gameData.ValidateGameFolder(path))
                {
                    continue;
                }

                try
                {
                    int steamApiCount = SteamAppId.CountSteamApiDlls(path);
                    string gameName = Path.GetFileName(path);
                    string? appId = _appIdDetection.DetectAppId(path);
                    string sizeStr = _gameData.GetFolderSizeString(path);

                    results.Add((path, gameName, appId ?? string.Empty, sizeStr, steamApiCount));
                }
                catch
                {
                    // ignored
                }
            }

            return results;
        });

        // Update UI on main thread
        if (IsDisposed)
        {
            return;
        }

        foreach (var game in validGames)
        {
            int rowIndex = _gameGrid.Rows.Add();
            string displayName = game.steamApiCount > 2 ? "⚠️ " + game.name : game.name;

            if (game.steamApiCount > 2)
            {
                _gameGrid.Rows[rowIndex].Cells["GameName"].ToolTipText =
                    $"Warning: Found {game.steamApiCount} steam_api DLLs in this folder.\nThis might contain multiple games or have an unusual structure.";
            }

            _gameGrid.Rows[rowIndex].Cells["GameName"].Value = displayName;

            _detectedAppIds[rowIndex] = game.appId;
            _gameGrid.Rows[rowIndex].Cells["AppId"].Value = string.IsNullOrEmpty(game.appId) ? "?" : game.appId;

            if (string.IsNullOrEmpty(game.appId))
            {
                _gameGrid.Rows[rowIndex].Cells["AppId"].Style.ForeColor = Color.Orange;
                _gameGrid.Rows[rowIndex].Cells["AppId"].Style.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            }

            _gameGrid.Rows[rowIndex].Cells["Size"].Value = game.size;
            _gameGrid.Rows[rowIndex].Cells["Crack"].Value = true;
            _gameGrid.Rows[rowIndex].Cells["Zip"].Value = false;
            _gameGrid.Rows[rowIndex].Cells["Upload"].Value = false;
            _gameGrid.Rows[rowIndex].Cells["Status"].Value = game.steamApiCount > 2 ? "⚠️ Check structure" : "Pending";
            _gameGrid.Rows[rowIndex].Tag = game.path;
        }

        // Update subtitle with actual count
        var subtitleLabel = Controls.OfType<Label>().FirstOrDefault(l => l.Text.Contains("games"));
        if (subtitleLabel != null)
        {
            subtitleLabel.Text = $"Found {_gameGrid.Rows.Count} games. Select actions for each:";
        }
    }

    private string ShowAppIdSearchDialog(string gameName, string currentAppId)
    {
        using var dialog = new AppIdSearchDialog(gameName, currentAppId);
        dialog.Owner = this;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            return dialog.SelectedAppId;
        }

        return null; // User cancelled
    }

    private Button CreateStyledButton(string text, Point location, Size size, Color? customColor = null)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(220, 220, 225),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btn.FlatAppearance.MouseDownBackColor = Color.Transparent;

        // Store the actual button color for painting
        Color buttonColor = customColor ?? Color.FromArgb(38, 38, 42);

        // Custom paint for smooth rounded corners with anti-aliasing
        btn.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            int radius = 8;
            var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);

            // Determine color based on hover state
            Color bgColor = buttonColor;
            if (btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
            {
                bgColor = Color.FromArgb(50, 50, 55);
            }

            using (var path = CreateRoundedRectPath(rect, radius))
            {
                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillPath(brush, path);
                }

                using (var pen = new Pen(Color.FromArgb(55, 55, 60), 1))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }

            // Draw text centered
            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, rect, btn.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        // Refresh on mouse enter/leave for hover effect
        btn.MouseEnter += (s, e) => btn.Invalidate();
        btn.MouseLeave += (s, e) => btn.Invalidate();

        return btn;
    }

    private GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    ///     Store crack details for a game path - called by Form1 after cracking
    /// </summary>
    public void StoreCrackDetails(string gamePath, CrackDetails details)
    {
        if (string.IsNullOrEmpty(gamePath) || details == null)
        {
            return;
        }

        _crackDetailsMap[gamePath] = details;
    }

    /// <summary>
    ///     Update zip status for a game's crack details
    /// </summary>
    public void UpdateZipStatus(string gamePath, bool success, string zipPath = null, string error = null)
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            return;
        }

        if (!_crackDetailsMap.ContainsKey(gamePath))
        {
            // Create a minimal CrackDetails if one doesn't exist
            _crackDetailsMap[gamePath] =
                new CrackDetails { GameName = Path.GetFileName(gamePath), GamePath = gamePath };
        }

        var details = _crackDetailsMap[gamePath];
        details.ZipAttempted = true;
        details.ZipSuccess = success;
        details.ZipPath = zipPath;
        details.ZipError = error;
    }

    /// <summary>
    ///     Update upload status for a game's crack details
    /// </summary>
    public void UpdateUploadStatus(string gamePath, bool success, string url = null, string error = null,
        int retryCount = 0)
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            return;
        }

        if (!_crackDetailsMap.ContainsKey(gamePath))
        {
            // Create a minimal CrackDetails if one doesn't exist
            _crackDetailsMap[gamePath] =
                new CrackDetails { GameName = Path.GetFileName(gamePath), GamePath = gamePath };
        }

        var details = _crackDetailsMap[gamePath];
        details.UploadAttempted = true;
        details.UploadSuccess = success;
        details.UploadUrl = url;
        details.UploadError = error;
        details.UploadRetryCount = retryCount;
    }

    /// <summary>
    ///     Update PyDrive URL for a game's details (called when conversion completes)
    /// </summary>
    public void UpdatePyDriveUrl(string gamePath, string pydriveUrl)
    {
        if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(pydriveUrl))
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdatePyDriveUrl(gamePath, pydriveUrl)); }
            catch
            {
                // ignored
            }

            return;
        }

        if (!_crackDetailsMap.ContainsKey(gamePath))
        {
            _crackDetailsMap[gamePath] =
                new CrackDetails { GameName = Path.GetFileName(gamePath), GamePath = gamePath };
        }

        _crackDetailsMap[gamePath].PyDriveUrl = pydriveUrl;
    }

    /// <summary>
    ///     Show crack details in a popup dialog
    /// </summary>
    private void ShowCrackDetails(CrackDetails details)
    {
        if (details == null)
        {
            return;
        }

        var detailForm = new Form
        {
            Text = $"Crack Details - {details.GameName}",
            Size = new Size(600, 500),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(25, 28, 40),
            ForeColor = Color.White,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
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
        textBox.AppendText($"Crack Details for {details.GameName}\n");
        textBox.SelectionFont = new Font("Consolas", 9);
        textBox.SelectionColor = Color.Gray;
        textBox.AppendText("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
        textBox.AppendText($"Path: {details.GamePath}\n");
        textBox.AppendText($"AppID: {details.AppId}\n");
        textBox.AppendText($"Time: {details.Timestamp:yyyy-MM-dd HH:mm:ss}\n");

        textBox.SelectionColor = details.Success ? Color.LightGreen : Color.Red;
        textBox.AppendText($"Success: {(details.Success ? "✓ Yes" : "✗ No")}\n\n");

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
            textBox.AppendText($"Zip: {(details.ZipSuccess ? "Success" : "Failed")}\n");
            if (!string.IsNullOrEmpty(details.ZipPath))
            {
                textBox.SelectionColor = Color.Gray;
                textBox.AppendText($"  Path: {details.ZipPath}\n");
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
            textBox.AppendText($"Upload: {(details.UploadSuccess ? "Success" : "Failed")}\n");
            if (details.UploadRetryCount > 0)
            {
                textBox.SelectionColor = Color.Yellow;
                textBox.AppendText($"  Retries: {details.UploadRetryCount}\n");
            }

            if (!string.IsNullOrEmpty(details.UploadUrl))
            {
                textBox.SelectionColor = Color.Cyan;
                textBox.AppendText($"  1fichier: {details.UploadUrl}\n");
            }

            textBox.AppendText("\n");

            // Conversion status (only if PyDrive conversion was attempted)
            if (!string.IsNullOrEmpty(details.PyDriveUrl))
            {
                textBox.SelectionColor = Color.LightGreen;
                textBox.AppendText("Conversion: Success\n");
                textBox.SelectionColor = Color.Cyan;
                textBox.AppendText($"  pydrive (debrid DDL): {details.PyDriveUrl}\n");
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
        }

        // Copy button
        var copyBtn = new Button
        {
            Text = "Copy to Clipboard",
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = Color.FromArgb(40, 45, 55),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        copyBtn.FlatAppearance.BorderColor = Color.FromArgb(60, 65, 75);
        copyBtn.Click += (s, e) =>
        {
            try
            {
                Clipboard.SetText(details.GetSummary());
                MessageBox.Show("Details copied to clipboard!", "Copied", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch
            {
                // ignored
            }
        };

        detailForm.Controls.Add(textBox);
        detailForm.Controls.Add(copyBtn);
        detailForm.ShowDialog(this);
    }

    [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
        int nWidthEllipse, int nHeightEllipse);

    private void ApplyAcrylicEffect()
    {
        try
        {
            // Use the shared AcrylicHelper for consistent styling
            AcrylicHelper.ApplyAcrylic(this, disableShadow: true);
        }
        catch
        {
            // ignored
        }
    }

    private void Form_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WmNclbuttondown, Htcaption, 0);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Draw border
        using var pen = new Pen(Color.FromArgb(60, 60, 65), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
