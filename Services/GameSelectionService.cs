using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Handles the game selection workflow including folder validation, manifest detection, and search triggering.
/// </summary>
public class GameSelectionService(
    IGameFolderService gameFolderService,
    IManifestParsingService manifestParsing,
    IGameSearchService gameSearch)
    : IGameSelectionService
{
    public GameSelectionResult ProcessGameFolder(string selectedPath)
    {
        // Auto-correct to game root if subfolder was selected
        string gameDir = gameFolderService.FindGameRootFolder(selectedPath);
        string gameDirName = gameFolderService.GetGameName(gameDir);

        var result = new GameSelectionResult
        {
            GameDirectory = gameDir, GameDirectoryName = gameDirName, WasAutoCorrected = gameDir != selectedPath
        };

        // Try to get AppID from Steam manifest files
        var manifestInfo = manifestParsing.GetAppIdFromManifest(gameDir);
        if (manifestInfo.HasValue)
        {
            result.ManifestDetected = true;
            result.AppId = manifestInfo.Value.appId;
            result.ManifestGameName = manifestInfo.Value.gameName;
            result.SizeOnDisk = manifestInfo.Value.sizeOnDisk;
            result.FlowType = GameSelectionFlowType.ManifestAutoDetect;
        }
        else
        {
            // No manifest - need to trigger search
            result.FlowType = GameSelectionFlowType.ManualSearch;
            result.NormalizedSearchText = gameSearch.NormalizeGameNameForSearch(gameDirName);
        }

        return result;
    }
}

public interface IGameSelectionService
{
    GameSelectionResult ProcessGameFolder(string selectedPath);
}

/// <summary>
///     Result of game folder selection processing.
/// </summary>
public class GameSelectionResult
{
    public string GameDirectory { get; set; }
    public string GameDirectoryName { get; set; }
    public bool WasAutoCorrected { get; set; }
    public GameSelectionFlowType FlowType { get; set; }

    // Manifest detection results
    public bool ManifestDetected { get; set; }
    public string AppId { get; set; }
    public string ManifestGameName { get; set; }
    public long SizeOnDisk { get; set; }

    // Search flow results
    public string NormalizedSearchText { get; set; }
}

public enum GameSelectionFlowType
{
    ManifestAutoDetect,
    ManualSearch,
    BatchFolder
}
