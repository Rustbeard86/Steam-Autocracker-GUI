using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace APPID.Utilities.UI;

/// <summary>
/// Manages search UI state transitions and visibility for game search workflow.
/// </summary>
public class SearchStateManager
{
    private readonly TextBox _searchTextBox;
    private readonly Panel _mainPanel;
    private readonly Button _manualEntryButton;
    private readonly Label _instructionLabel;
    private readonly PictureBox _startCrackPic;
    
    private bool _isFirstClickAfterSelection;
    private bool _isInitialFolderSearch;

    public bool IsFirstClickAfterSelection 
    { 
        get => _isFirstClickAfterSelection;
        set => _isFirstClickAfterSelection = value;
    }

    public bool IsInitialFolderSearch 
    { 
        get => _isInitialFolderSearch;
        set => _isInitialFolderSearch = value;
    }

    public SearchStateManager(
        TextBox searchBox, 
        Panel mainPanel, 
        Button manualEntry, 
        Label instruction, 
        PictureBox startCrack)
    {
        _searchTextBox = searchBox ?? throw new ArgumentNullException(nameof(searchBox));
        _mainPanel = mainPanel ?? throw new ArgumentNullException(nameof(mainPanel));
        _manualEntryButton = manualEntry ?? throw new ArgumentNullException(nameof(manualEntry));
        _instructionLabel = instruction ?? throw new ArgumentNullException(nameof(instruction));
        _startCrackPic = startCrack ?? throw new ArgumentNullException(nameof(startCrack));
    }

    /// <summary>
    /// Shows the search UI (hides main panel, shows search controls).
    /// </summary>
    public void ShowSearchUI()
    {
        _mainPanel.Visible = false;
        _searchTextBox.Enabled = true;
        _manualEntryButton.Visible = true;
        _instructionLabel.Visible = true;
        _startCrackPic.Visible = true;
    }

    /// <summary>
    /// Shows the main panel (hides search controls).
    /// </summary>
    public void ShowMainPanel()
    {
        _searchTextBox.Enabled = false;
        _mainPanel.Visible = true;
        _manualEntryButton.Visible = false;
        _startCrackPic.Visible = true;
    }

    /// <summary>
    /// Hides the instruction label and stops its timer.
    /// </summary>
    public void HideInstructionLabel(Timer timer)
    {
        _instructionLabel.Visible = false;
        timer?.Stop();
    }

    /// <summary>
    /// Resets search state for a new game selection.
    /// </summary>
    public void ResetForNewSelection()
    {
        _searchTextBox.Clear();
        _isFirstClickAfterSelection = false;
        _isInitialFolderSearch = false;
    }

    /// <summary>
    /// Prepares for initial folder search (sets flags and clears text).
    /// </summary>
    public void PrepareInitialFolderSearch(string folderName)
    {
        _isInitialFolderSearch = true;
        _isFirstClickAfterSelection = true;
        _searchTextBox.Text = folderName;
    }

    /// <summary>
    /// Handles the first click on search textbox after selection.
    /// </summary>
    public void HandleFirstClick()
    {
        if (_isFirstClickAfterSelection)
        {
            _searchTextBox.Clear();
            _isFirstClickAfterSelection = false;
        }
    }
}
