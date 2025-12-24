namespace APPID.Models;

internal class SteamGame
{
    public string AppId { get; set; }
    public string Name { get; set; }
    public string InstallDir { get; set; }
    public string BuildId { get; set; }
    public long LastUpdated { get; set; }
}
