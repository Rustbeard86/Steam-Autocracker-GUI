namespace APPID.Utilities.UI;

/// <summary>
///     Safe MessageBox wrapper that handles TopMost forms correctly
/// </summary>
public static class SafeMessageBox
{
    /// <summary>
    ///     Shows a message box that properly appears above TopMost forms
    /// </summary>
    public static DialogResult Show(
        Form? owner,
        string text,
        string caption = "",
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None)
    {
        // Critical: Temporarily disable TopMost on the owner
        bool wasTopMost = owner?.TopMost ?? false;

        if (owner != null && wasTopMost)
        {
            owner.TopMost = false;
        }

        DialogResult result;

        try
        {
            // Show with proper owner
            if (owner != null)
            {
                result = MessageBox.Show(owner, text, caption, buttons, icon);
            }
            else
            {
                // Fallback if no owner
                result = MessageBox.Show(text, caption, buttons, icon);
            }
        }
        finally
        {
            // Restore TopMost state
            if (owner != null && wasTopMost)
            {
                owner.TopMost = true;
                owner.BringToFront();
                owner.Activate();
            }
        }

        return result;
    }

    /// <summary>
    ///     Shows an error message box
    /// </summary>
    public static void ShowError(Form owner, string message, string title = "Error")
    {
        Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    ///     Shows a warning message box (returns DialogResult for Yes/No dialogs)
    /// </summary>
    public static DialogResult ShowWarning(
        Form owner,
        string message,
        string title = "Warning",
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Warning)
    {
        return Show(owner, message, title, buttons, icon);
    }

    /// <summary>
    ///     Shows an info message box
    /// </summary>
    public static void ShowInfo(Form owner, string message, string title = "Information")
    {
        Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    ///     Shows a confirmation dialog
    /// </summary>
    public static bool Confirm(Form owner, string message, string title = "Confirm")
    {
        return Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
