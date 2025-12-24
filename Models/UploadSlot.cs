using APPID.Utilities.UI;

namespace APPID.Models;

/// <summary>
///     Represents an upload slot for concurrent upload operations in the batch processing form.
/// </summary>
internal sealed class UploadSlot
{
    public Button? BtnSkip { get; set; }
    public CancellationTokenSource? Cancellation { get; set; }
    public string? GamePath { get; set; }
    public bool InUse { get; set; }
    public Label? LblEta { get; set; }
    public Label? LblGame { get; set; }
    public Label? LblSize { get; set; }
    public Label? LblSpeed { get; set; }
    public Panel? Panel { get; set; }
    public NeonProgressBar? ProgressBar { get; set; }
}
