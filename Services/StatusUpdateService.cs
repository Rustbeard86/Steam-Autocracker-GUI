namespace APPID.Services;

/// <summary>
///     Service for updating status messages in the UI
/// </summary>
public interface IStatusUpdateService
{
    /// <summary>
    ///     Updates the main status label with a message and color
    /// </summary>
    void UpdateStatus(string message, Color color);

    /// <summary>
    ///     Updates the current directory/game text
    /// </summary>
    void UpdateCurrentText(string message);

    /// <summary>
    ///     Enables or disables status updates (useful for batch operations)
    /// </summary>
    void SetSuppressUpdates(bool suppress);
}

/// <summary>
///     Implementation of IStatusUpdateService that updates Form1 labels
/// </summary>
public class StatusUpdateService : IStatusUpdateService
{
    private readonly Label _currentDirLabel;
    private readonly Form _form;
    private readonly Label _statusLabel;
    private bool _suppressUpdates;

    public StatusUpdateService(Form form, Label statusLabel, Label currentDirLabel)
    {
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
        _currentDirLabel = currentDirLabel ?? throw new ArgumentNullException(nameof(currentDirLabel));
    }

    public void UpdateStatus(string message, Color color)
    {
        if (_suppressUpdates)
        {
            return;
        }

        // Handle cross-thread calls
        if (_form.InvokeRequired)
        {
            _form.BeginInvoke(() => UpdateStatus(message, color));
            return;
        }

        // Auto-detect color based on message content if needed
        string messageLow = message.ToLower();
        if (messageLow.Contains("ready to crack"))
        {
            color = Color.HotPink;
        }
        else if (messageLow.Contains("complete"))
        {
            color = Color.MediumSpringGreen;
        }
        else if (messageLow.Contains("no steam"))
        {
            color = Color.Crimson;
        }

        _statusLabel.ForeColor = color;
        _statusLabel.Text = message;
        _form.Text = message.Replace("&&", "&");
    }

    public void UpdateCurrentText(string message)
    {
        if (_suppressUpdates)
        {
            return;
        }

        // Handle cross-thread calls
        if (_form.InvokeRequired)
        {
            _form.BeginInvoke(() => UpdateCurrentText(message));
            return;
        }

        _currentDirLabel.Text = message;
    }

    public void SetSuppressUpdates(bool suppress)
    {
        _suppressUpdates = suppress;
    }
}
