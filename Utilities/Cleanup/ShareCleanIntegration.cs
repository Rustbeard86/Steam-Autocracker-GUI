namespace APPID.Utilities.Cleanup;

// Integration with share window
public static class ShareCleanIntegration
{
    public static async Task<bool> PrepareCleanShare(
        string gamePath,
        string gameName,
        string appId,
        Form parentForm)
    {
        // First check if it's clean
        var verification = CleanFilesVerifier.VerifyCleanInstallation(gamePath);

        if (verification.IsClean)
        {
            return true; // Already clean, good to go
        }

        // Not clean - offer automatic cleanup
        return await AutoCleanupSystem.HandleContaminatedGame(gamePath, gameName, parentForm);
    }
}
