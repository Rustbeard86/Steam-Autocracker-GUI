namespace APPID.Utilities.UI;

/// <summary>
///     Manages the visibility, positioning, and state of crack-related buttons
///     (Zip Dir, Open Dir, Upload).
/// </summary>
public class CrackButtonManager
{
    private readonly Label _currentDirLabel;
    private readonly Panel _mainPanel;
    private readonly Button _openDirButton;
    private readonly Button _uploadButton;
    private readonly Button _zipButton;

    public CrackButtonManager(
        Button zipButton,
        Button openDirButton,
        Button uploadButton,
        Label currentDirLabel,
        Panel mainPanel)
    {
        _zipButton = zipButton ?? throw new ArgumentNullException(nameof(zipButton));
        _openDirButton = openDirButton ?? throw new ArgumentNullException(nameof(openDirButton));
        _uploadButton = uploadButton;
        _currentDirLabel = currentDirLabel ?? throw new ArgumentNullException(nameof(currentDirLabel));
        _mainPanel = mainPanel ?? throw new ArgumentNullException(nameof(mainPanel));
    }

    /// <summary>
    ///     Shows crack buttons centered below the current directory label.
    /// </summary>
    /// <param name="showZip">Whether to show the Zip button alongside Open Dir.</param>
    public void ShowCrackButtons(bool showZip = true)
    {
        int gap = 10;
        int buttonY = _currentDirLabel.Bottom + 6;
        int totalWidth = showZip
            ? _zipButton.Width + gap + _openDirButton.Width
            : _openDirButton.Width;
        int startX = (_mainPanel.ClientSize.Width - totalWidth) / 2;

        if (showZip)
        {
            _zipButton.Location = new Point(startX, buttonY);
            _zipButton.Visible = true;
            _zipButton.Text = "Zip Dir";
            _zipButton.BringToFront();

            _openDirButton.Location = new Point(startX + _zipButton.Width + gap, buttonY);
        }
        else
        {
            _openDirButton.Location = new Point(startX, buttonY);
            _zipButton.Visible = false;
        }

        _openDirButton.Visible = true;
        _openDirButton.BringToFront();

        // Hide upload button when showing crack buttons (new crack cycle)
        _uploadButton?.SetVisible(false);
    }

    /// <summary>
    ///     Hides all crack-related buttons.
    /// </summary>
    public void HideCrackButtons()
    {
        _zipButton?.SetVisible(false);
        _openDirButton?.SetVisible(false);
        _uploadButton?.SetVisible(false);
    }

    /// <summary>
    ///     Shows the Upload button positioned exactly where the Zip button is.
    /// </summary>
    public void ShowUploadButton()
    {
        if (_uploadButton != null && _zipButton != null)
        {
            _uploadButton.Location = _zipButton.Location;
            _uploadButton.Size = _zipButton.Size;
            _uploadButton.Visible = true;
            _uploadButton.BringToFront();
        }
    }

    /// <summary>
    ///     Sets the Zip button text and associated zip path tag.
    /// </summary>
    public void SetZipButtonState(string text, string zipPath = null)
    {
        _zipButton.Text = text;
        _zipButton.Tag = zipPath;
        ResetButtonAppearance();
    }

    /// <summary>
    ///     Sets the Zip button to "Cancel" state with orange glow.
    /// </summary>
    public void SetCancelState()
    {
        _zipButton.Text = "Cancel";
        _zipButton.FlatAppearance.BorderColor = Color.FromArgb(255, 150, 0);
        _zipButton.FlatAppearance.BorderSize = 2;
        _zipButton.ForeColor = Color.FromArgb(255, 180, 100);
    }

    /// <summary>
    ///     Resets the Zip button appearance to default styling.
    /// </summary>
    public void ResetButtonAppearance()
    {
        _zipButton.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
        _zipButton.FlatAppearance.BorderSize = 1;
        _zipButton.ForeColor = Color.FromArgb(220, 220, 225);
    }
}

/// <summary>
///     Extension methods for WinForms controls to simplify null-safe operations.
/// </summary>
internal static class ControlExtensions
{
    public static void SetVisible(this Control control, bool visible)
    {
        if (control != null)
        {
            control.Visible = visible;
        }
    }
}
