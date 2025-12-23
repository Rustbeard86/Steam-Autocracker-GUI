using System.Data;
using System.Runtime.InteropServices;
using APPID;

namespace SteamAutocrackGUI;

/// <summary>
///     Dialog for searching and selecting an AppID
/// </summary>
public class AppIdSearchDialog : Form
{
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private DataGridView resultsGrid;

    private TextBox searchBox;
    private DataTable? steamGamesTable;

    public AppIdSearchDialog(string gameName, string currentAppId)
    {
        InitializeForm(gameName, currentAppId);
        Load += (s, e) =>
        {
            ApplyAcrylicEffect();
            LoadSteamGamesData();
            if (!string.IsNullOrEmpty(gameName))
            {
                searchBox.Text = gameName;
                PerformSearch(gameName);
            }
        };
        MouseDown += Form_MouseDown;
    }

    public string? SelectedAppId { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private void InitializeForm(string gameName, string currentAppId)
    {
        Text = "Search AppID";
        Size = new Size(450, 400);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(5, 8, 20);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;

        // Title
        var titleLabel = new Label
        {
            Text = "Search AppID",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 200, 255),
            Location = new Point(15, 12),
            Size = new Size(200, 25),
            BackColor = Color.Transparent
        };
        Controls.Add(titleLabel);

        // Game name label
        var gameLabel = new Label
        {
            Text = $"Game: {gameName}",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(180, 180, 185),
            Location = new Point(15, 40),
            Size = new Size(400, 20),
            BackColor = Color.Transparent
        };
        Controls.Add(gameLabel);

        // Search box
        searchBox = new TextBox
        {
            Location = new Point(15, 65),
            Size = new Size(330, 25),
            BackColor = Color.FromArgb(25, 28, 40),
            ForeColor = Color.FromArgb(220, 255, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10)
        };
        searchBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                PerformSearch(searchBox.Text);
            }
        };
        Controls.Add(searchBox);

        // Search button
        var searchBtn = new Button
        {
            Text = "Search",
            Location = new Point(355, 65),
            Size = new Size(75, 25),
            BackColor = Color.FromArgb(38, 38, 42),
            ForeColor = Color.FromArgb(220, 220, 225),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        searchBtn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
        searchBtn.Click += (s, e) => PerformSearch(searchBox.Text);
        Controls.Add(searchBtn);

        // Results grid
        resultsGrid = new DataGridView
        {
            Location = new Point(15, 100),
            Size = new Size(415, 210),
            BackgroundColor = Color.FromArgb(5, 8, 20),
            ForeColor = Color.FromArgb(220, 255, 255),
            GridColor = Color.FromArgb(40, 40, 45),
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9)
        };

        resultsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 28, 40);
        resultsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(150, 200, 255);
        resultsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        resultsGrid.ColumnHeadersHeight = 28;
        resultsGrid.DefaultCellStyle.BackColor = Color.FromArgb(15, 18, 30);
        resultsGrid.DefaultCellStyle.ForeColor = Color.FromArgb(220, 255, 255);
        resultsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 80, 120);
        resultsGrid.RowTemplate.Height = 24;

        resultsGrid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                SelectCurrentRow();
            }
        };
        Controls.Add(resultsGrid);

        // Manual AppID entry
        var manualLabel = new Label
        {
            Text = "Or enter AppID manually:",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(140, 140, 145),
            Location = new Point(15, 320),
            Size = new Size(160, 20),
            BackColor = Color.Transparent
        };
        Controls.Add(manualLabel);

        var manualBox = new TextBox
        {
            Location = new Point(175, 318),
            Size = new Size(100, 25),
            BackColor = Color.FromArgb(25, 28, 40),
            ForeColor = Color.FromArgb(220, 255, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            Text = currentAppId
        };
        Controls.Add(manualBox);

        // OK button
        var okBtn = new Button
        {
            Text = "OK",
            Location = new Point(290, 315),
            Size = new Size(65, 30),
            BackColor = Color.FromArgb(0, 100, 70),
            ForeColor = Color.FromArgb(220, 220, 225),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        okBtn.FlatAppearance.BorderColor = Color.FromArgb(0, 80, 60);
        okBtn.Click += (s, e) =>
        {
            // Use manual entry if provided, otherwise use selected row
            if (!string.IsNullOrWhiteSpace(manualBox.Text) && manualBox.Text.All(char.IsDigit))
            {
                SelectedAppId = manualBox.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (resultsGrid.SelectedRows.Count > 0)
            {
                SelectCurrentRow();
            }
            else
            {
                MessageBox.Show("Please select a game from the results or enter an AppID manually.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        Controls.Add(okBtn);

        // Cancel button
        var cancelBtn = new Button
        {
            Text = "Cancel",
            Location = new Point(365, 315),
            Size = new Size(65, 30),
            BackColor = Color.FromArgb(38, 38, 42),
            ForeColor = Color.FromArgb(220, 220, 225),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
        cancelBtn.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(cancelBtn);
    }

    private void LoadSteamGamesData()
    {
        try
        {
            // Try to load the Steam games table from the main form's data
            var dataGen = new DataTableGeneration();
            steamGamesTable = dataGen.DataTableToGenerate;
            resultsGrid.DataSource = steamGamesTable;

            // Configure columns
            if (resultsGrid.Columns.Count >= 2)
            {
                resultsGrid.Columns[0].HeaderText = "Name";
                resultsGrid.Columns[0].Width = 300;
                resultsGrid.Columns[1].HeaderText = "AppID";
                resultsGrid.Columns[1].Width = 80;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load Steam games data: {ex.Message}");
        }
    }

    private void PerformSearch(string searchTerm)
    {
        if (steamGamesTable is null)
        {
            return;
        }

        try
        {
            string clean = searchTerm.Trim()
                .Replace("'", "''") // Escape single quotes
                .Replace("[", "")
                .Replace("]", "")
                .Replace("*", "")
                .Replace("%", "");

            if (string.IsNullOrEmpty(clean))
            {
                return;
            }

            // Try exact match first
            steamGamesTable.DefaultView.RowFilter = $"Name LIKE '{clean}'";
            if (steamGamesTable.DefaultView.Count > 0)
            {
                return;
            }

            // Try contains search
            string[] words = clean.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                string filter = string.Join("%' AND Name LIKE '%", words);
                steamGamesTable.DefaultView.RowFilter = $"Name LIKE '%{filter}%'";
                if (steamGamesTable.DefaultView.Count > 0)
                {
                    return;
                }
            }

            // Simple contains
            steamGamesTable.DefaultView.RowFilter = $"Name LIKE '%{clean}%'";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Search error: {ex.Message}");
            steamGamesTable.DefaultView.RowFilter = "";
        }
    }

    private void SelectCurrentRow()
    {
        if (resultsGrid.SelectedRows.Count > 0)
        {
            var row = resultsGrid.SelectedRows[0];
            if (row.Cells.Count >= 2)
            {
                SelectedAppId = row.Cells[1].Value?.ToString();
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

    private void ApplyAcrylicEffect()
    {
        try
        {
            AcrylicHelper.ApplyAcrylic(this, disableShadow: true);
        }
        catch { }
    }

    private void Form_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var pen = new Pen(Color.FromArgb(60, 60, 65), 1))
        {
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
