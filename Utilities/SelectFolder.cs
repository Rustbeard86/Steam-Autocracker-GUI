namespace APPID;

/// <summary>
///     Folder selection dialog wrapper
/// </summary>
public class FolderSelectDialog
{
    private string _initialDirectory;
    private string _title;

    public string InitialDirectory
    {
        get
        {
            return string.IsNullOrEmpty(_initialDirectory)
                ? Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory
                : _initialDirectory;
        }
        set { _initialDirectory = value; }
    }

    public string Title
    {
        get { return _title ?? "Select a folder"; }
        set { _title = value; }
    }

    public string FileName { get; private set; } = "";

    public bool Show() { return Show(IntPtr.Zero); }

    public bool Show(IntPtr hWndOwner)
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = Title;
            dialog.InitialDirectory = InitialDirectory;
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;

            var owner = hWndOwner != IntPtr.Zero ? new WindowWrapper(hWndOwner) : null;
            if (dialog.ShowDialog(owner) == DialogResult.OK)
            {
                FileName = dialog.SelectedPath;
                return true;
            }
        }

        return false;
    }

    internal void Show(Action resetText)
    {
        throw new NotImplementedException();
    }

    private class WindowWrapper : IWin32Window
    {
        public WindowWrapper(IntPtr handle) { Handle = handle; }
        public IntPtr Handle { get; }
    }
}
