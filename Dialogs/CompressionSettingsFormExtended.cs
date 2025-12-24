using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace APPID.Dialogs;

public class CompressionSettingsFormExtended : Form
{
    private readonly string _gameName;
    private readonly bool _isCracked;
    private Button _cancelButton;
    private Label _levelDescriptionLabel;
    private TrackBar _levelTrackBar;
    private Button _okButton;
    private CheckBox _rinCheckBox;
    private RadioButton _sevenZipRadioButton;
    private Panel _sliderPanel;
    private int _sliderValue;
    private CheckBox _uploadCheckBox;

    private RadioButton _zipRadioButton;

    public CompressionSettingsFormExtended(string gameName, bool isCracked)
    {
        _gameName = gameName;
        _isCracked = isCracked;
        InitializeComponent();
        InitializeTimers();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedFormat { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedLevel { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UploadToBackend { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EncryptForRin { get; set; }

    private void InitializeComponent()
    {
        Text = $"Compression Settings - {_gameName}";
        Size = new Size(400, 350);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(0, 20, 50);

        // Title label
        var titleLabel = new Label();
        titleLabel.Text = $"{(_isCracked ? "🎮 CRACKED" : "📦 CLEAN")} - {_gameName}";
        titleLabel.Location = new Point(20, 15);
        titleLabel.Size = new Size(360, 25);
        titleLabel.ForeColor = _isCracked ? Color.FromArgb(255, 200, 100) : Color.FromArgb(150, 255, 150);
        titleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;

        // Format selection
        var formatLabel = new Label();
        formatLabel.Text = "Format:";
        formatLabel.Location = new Point(20, 50);
        formatLabel.Size = new Size(60, 25);
        formatLabel.ForeColor = Color.FromArgb(192, 255, 255);

        _zipRadioButton = new RadioButton();
        _zipRadioButton.Text = "ZIP (Universal)";
        _zipRadioButton.Location = new Point(85, 48);
        _zipRadioButton.Size = new Size(120, 20);
        _zipRadioButton.ForeColor = Color.FromArgb(220, 255, 255);
        _zipRadioButton.Checked = true;

        _sevenZipRadioButton = new RadioButton();
        _sevenZipRadioButton.Text = "7Z (Smaller)";
        _sevenZipRadioButton.Location = new Point(210, 48);
        _sevenZipRadioButton.Size = new Size(120, 20);
        _sevenZipRadioButton.ForeColor = Color.FromArgb(220, 255, 255);

        // Compression level
        var levelLabel = new Label();
        levelLabel.Text = "Level:";
        levelLabel.Location = new Point(20, 85);
        levelLabel.Size = new Size(60, 25);
        levelLabel.ForeColor = Color.FromArgb(192, 255, 255);

        _levelTrackBar = new TrackBar();
        _levelTrackBar.Location = new Point(85, 83);
        _levelTrackBar.Size = new Size(270, 45);
        _levelTrackBar.Maximum = 10;
        _levelTrackBar.Value = 0;
        _levelTrackBar.TickStyle = TickStyle.Both;
        _levelTrackBar.Visible = false; // Hidden, we use custom slider

        CreateCustomSlider();

        _levelDescriptionLabel = new Label();
        _levelDescriptionLabel.Location = new Point(85, 125);
        _levelDescriptionLabel.Size = new Size(270, 20);
        _levelDescriptionLabel.ForeColor = Color.FromArgb(192, 255, 255);
        _levelDescriptionLabel.Text = "0 - No Compression (Instant)";
        _levelDescriptionLabel.TextAlign = ContentAlignment.TopCenter;

        // Separator line
        var separator = new Panel();
        separator.Location = new Point(20, 160);
        separator.Size = new Size(350, 2);
        separator.BackColor = Color.FromArgb(50, 100, 150);

        // Upload checkbox
        _uploadCheckBox = new CheckBox();
        _uploadCheckBox.Text = "📤 Upload to YSG/HFP Backend (6 month expiry)";
        _uploadCheckBox.Location = new Point(20, 175);
        _uploadCheckBox.Size = new Size(350, 25);
        _uploadCheckBox.ForeColor = Color.FromArgb(255, 200, 100);
        _uploadCheckBox.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        // RIN encryption checkbox
        _rinCheckBox = new CheckBox();
        _rinCheckBox.Text = "🔒 This release is for RIN (encrypt with cs.rin.ru)";
        _rinCheckBox.Location = new Point(20, 210);
        _rinCheckBox.Size = new Size(350, 25);
        _rinCheckBox.ForeColor = Color.FromArgb(100, 200, 255);
        _rinCheckBox.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        // Info label
        var infoLabel = new Label();
        infoLabel.Text = "Upload creates shareable link • RIN encryption adds password";
        infoLabel.Location = new Point(20, 245);
        infoLabel.Size = new Size(350, 20);
        infoLabel.ForeColor = Color.Gray;
        infoLabel.Font = new Font("Segoe UI", 8);
        infoLabel.TextAlign = ContentAlignment.TopCenter;

        // Buttons
        _okButton = new Button();
        _okButton.Text = "✅ Process";
        _okButton.Location = new Point(100, 280);
        _okButton.Size = new Size(90, 30);
        _okButton.DialogResult = DialogResult.OK;
        _okButton.FlatStyle = FlatStyle.Flat;
        _okButton.FlatAppearance.BorderColor = Color.FromArgb(100, 200, 100);
        _okButton.BackColor = Color.FromArgb(0, 2, 10);
        _okButton.ForeColor = Color.FromArgb(192, 255, 255);

        _cancelButton = new Button();
        _cancelButton.Text = "Cancel";
        _cancelButton.Location = new Point(210, 280);
        _cancelButton.Size = new Size(90, 30);
        _cancelButton.DialogResult = DialogResult.Cancel;
        _cancelButton.FlatStyle = FlatStyle.Flat;
        _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(200, 100, 100);
        _cancelButton.BackColor = Color.FromArgb(0, 2, 10);
        _cancelButton.ForeColor = Color.FromArgb(192, 255, 255);

        // Add controls
        Controls.Add(titleLabel);
        Controls.Add(formatLabel);
        Controls.Add(_zipRadioButton);
        Controls.Add(_sevenZipRadioButton);
        Controls.Add(levelLabel);
        Controls.Add(_levelTrackBar);
        Controls.Add(_levelDescriptionLabel);
        Controls.Add(separator);
        Controls.Add(_uploadCheckBox);
        Controls.Add(_rinCheckBox);
        Controls.Add(infoLabel);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
    }

    private void CreateCustomSlider()
    {
        _sliderPanel = new Panel();
        _sliderPanel.Location = new Point(85, 83);
        _sliderPanel.Size = new Size(270, 40);
        _sliderPanel.BackColor = Color.Transparent;

        _sliderPanel.Paint += SliderPanel_Paint;
        _sliderPanel.MouseDown += SliderPanel_MouseDown;
        _sliderPanel.MouseMove += SliderPanel_MouseMove;
        Controls.Add(_sliderPanel);
    }

    private void SliderPanel_Paint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int trackY = 20;
        int trackStart = 15;
        int trackEnd = 255;
        int trackWidth = trackEnd - trackStart;

        // Draw track
        using (var trackBrush = new SolidBrush(Color.FromArgb(20, 192, 255, 255)))
        {
            g.FillRectangle(trackBrush, trackStart, trackY - 2, trackWidth, 4);
        }

        // Draw fill
        int fillWidth = (int)(_sliderValue / 10.0 * trackWidth);
        if (fillWidth > 0)
        {
            using var fillBrush = new LinearGradientBrush(
                new Point(trackStart, 0),
                new Point(trackEnd, 0),
                Color.FromArgb(0, 200, 255),
                Color.FromArgb(192, 255, 255));
            g.FillRectangle(fillBrush, trackStart, trackY - 2, fillWidth, 4);
        }

        // Draw handle
        int handleX = trackStart + (int)(_sliderValue / 10.0 * trackWidth);
        using (var handleBrush = new SolidBrush(Color.FromArgb(220, 255, 255)))
        {
            g.FillEllipse(handleBrush, handleX - 6, trackY - 6, 12, 12);
        }
    }

    private void SliderPanel_MouseDown(object sender, MouseEventArgs e)
    {
        UpdateSliderValue(e.X);
    }

    private void SliderPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            UpdateSliderValue(e.X);
        }
    }

    private void UpdateSliderValue(int mouseX)
    {
        int value = (int)Math.Round((mouseX - 15) / 24.0);
        _sliderValue = Math.Max(0, Math.Min(10, value));
        _sliderPanel.Invalidate();
        UpdateLevelDescription();
    }

    private void UpdateLevelDescription()
    {
        string description;
        switch (_sliderValue)
        {
            case 0:
                description = "0 - No Compression (Instant)";
                break;
            case 5:
                description = "5 - Medium Compression (Average)";
                break;
            case 10:
                description = "10 - Ultra Compression";
                break;
            default:
                description = _sliderValue.ToString();
                break;
        }

        _levelDescriptionLabel.Text = description;
    }

    private void InitializeTimers()
    {
        UpdateLevelDescription();
    }

    private void okButton_Click(object sender, EventArgs e)
    {
        SelectedFormat = _zipRadioButton.Checked ? "ZIP" : "7Z";
        SelectedLevel = _sliderValue.ToString();
        UploadToBackend = _uploadCheckBox.Checked;
        EncryptForRin = _rinCheckBox.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
