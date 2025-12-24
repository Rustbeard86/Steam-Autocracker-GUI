namespace APPID;

internal static class SingleInstanceManager
{
    public static Mutex? ApplicationMutex { get; private set; }

    public static bool EnsureSingleInstance()
    {
        ApplicationMutex = new Mutex(false, "APPID.exe", out bool mutexCreated);

        if (!mutexCreated)
        {
            MessageBox.Show(
                new Form { TopMost = true },
                "Steam APPID finder is already running!",
                "Already Running!",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            ApplicationMutex.Close();
            return false;
        }

        return true;
    }
}
