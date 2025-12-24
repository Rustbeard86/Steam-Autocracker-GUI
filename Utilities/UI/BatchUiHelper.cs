using System.Drawing;
using System.Windows.Forms;

namespace APPID.Utilities.UI;

/// <summary>
/// Helper for managing batch processing UI elements
/// </summary>
public class BatchUiHelper
{
    private readonly Form _form;
    private readonly Label _countLabel;
    private readonly Label _suffixLabel;
    private readonly Label _prefixLabel;
    private readonly Button _processButton;
    private readonly Button _settingsButton;

    public BatchUiHelper(Form form, Label countLabel, Label suffixLabel, Label prefixLabel, 
        Button processButton, Button settingsButton)
    {
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _countLabel = countLabel ?? throw new ArgumentNullException(nameof(countLabel));
        _suffixLabel = suffixLabel ?? throw new ArgumentNullException(nameof(suffixLabel));
        _prefixLabel = prefixLabel ?? throw new ArgumentNullException(nameof(prefixLabel));
        _processButton = processButton ?? throw new ArgumentNullException(nameof(processButton));
        _settingsButton = settingsButton ?? throw new ArgumentNullException(nameof(settingsButton));
    }

    /// <summary>
    /// Updates the UI to reflect the current selection count
    /// </summary>
    /// <param name="selectedCount">Number of selected items</param>
    public void UpdateSelectedCount(int selectedCount)
    {
        bool hasSelection = selectedCount > 0;

        // Update labels: "X" (green) + " selected" (gray)
        _countLabel.Text = selectedCount.ToString();
        _countLabel.Visible = hasSelection;

        _suffixLabel.Text = " selected";
        _suffixLabel.Visible = hasSelection;

        // Position elements in a row: count -> suffix -> Process -> Settings
        if (hasSelection)
        {
            _suffixLabel.Location = new Point(_countLabel.Right, _countLabel.Top);
            _processButton.Location = new Point(_suffixLabel.Right + 8, _processButton.Top);
            _settingsButton.Location = new Point(_processButton.Right + 5, _settingsButton.Top);
        }

        // Hide prefix - not needed anymore
        _prefixLabel.Visible = false;

        _settingsButton.Visible = hasSelection;
        _processButton.Visible = hasSelection;
    }

    /// <summary>
    /// Animates a button with a blink effect
    /// </summary>
    /// <param name="button">The button to blink</param>
    /// <param name="blinkColor">Color to use for blink effect</param>
    /// <param name="blinkCount">Number of times to blink</param>
    /// <param name="blinkDurationMs">Duration of each blink in milliseconds</param>
    public static async Task BlinkButtonAsync(Button button, Color blinkColor, int blinkCount = 3, int blinkDurationMs = 150)
    {
        var originalBg = button.BackColor;

        for (int i = 0; i < blinkCount; i++)
        {
            button.BackColor = blinkColor;
            await Task.Delay(blinkDurationMs);
            button.BackColor = originalBg;
            await Task.Delay(blinkDurationMs);
        }
    }
}
