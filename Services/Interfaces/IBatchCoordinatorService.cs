using APPID.Dialogs;
using APPID.Models;

namespace APPID.Services.Interfaces;

/// <summary>
///     Service for coordinating batch game processing operations.
/// </summary>
public interface IBatchCoordinatorService
{
    /// <summary>
    ///     Processes a batch of games with the specified settings and updates UI accordingly.
    /// </summary>
    /// <param name="games">List of games to process</param>
    /// <param name="compressionFormat">Compression format (7z or zip)</param>
    /// <param name="compressionLevel">Compression level</param>
    /// <param name="usePassword">Whether to use password encryption</param>
    /// <param name="useGoldberg">Whether to use Goldberg emulator</param>
    /// <param name="convertToPyDrive">Whether to convert 1fichier URLs to PyDrive</param>
    /// <param name="batchForm">The batch form for UI updates</param>
    /// <param name="statusUpdateCallback">Callback for status messages</param>
    /// <param name="batchIndicatorUpdateCallback">Callback for batch indicator updates</param>
    Task ProcessBatchGamesAsync(
        List<BatchGameItem> games,
        string compressionFormat,
        string compressionLevel,
        bool usePassword,
        bool useGoldberg,
        bool convertToPyDrive,
        BatchGameSelectionForm batchForm,
        Action<string, Color>? statusUpdateCallback = null,
        Action<int>? batchIndicatorUpdateCallback = null);
}
