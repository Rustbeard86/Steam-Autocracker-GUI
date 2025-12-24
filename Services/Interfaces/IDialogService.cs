namespace APPID.Services.Interfaces;

/// <summary>
///     Service for displaying custom dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    ///     Shows a styled confirmation dialog with Yes/No options.
    /// </summary>
    /// <param name="parent">The parent form to center the dialog on.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The main message to display.</param>
    /// <param name="path">The path to display (typically a file or folder path).</param>
    /// <param name="yesText">The text for the Yes button.</param>
    /// <param name="noText">The text for the No button.</param>
    /// <returns>True if Yes was clicked, false if No was clicked.</returns>
    bool ShowStyledConfirmation(Form parent, string title, string message, string path, string yesText, string noText);
}
