using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of dialog service for displaying custom styled dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    public bool ShowStyledConfirmation(Form parent, string title, string message, string path, string yesText,
        string noText)
    {
        bool result = false;

        using var dialog = new Form();
        dialog.Text = title;
        dialog.Size = new Size(500, 280);
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.FormBorderStyle = FormBorderStyle.None;
        dialog.BackColor = Color.FromArgb(25, 28, 40);
        dialog.ForeColor = Color.White;
        dialog.ShowInTaskbar = false;

        // Add a subtle border
        dialog.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(60, 65, 80), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, dialog.Width - 1, dialog.Height - 1);
        };

        // Title bar with icon
        var titleLabel = new Label
        {
            Text = "⚠️ " + title,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 200, 100),
            AutoSize = false,
            Size = new Size(480, 35),
            Location = new Point(15, 15),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Message
        var messageLabel = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.White,
            AutoSize = false,
            Size = new Size(470, 50),
            Location = new Point(15, 55)
        };

        // Path display with dark background
        var pathPanel = new Panel
        {
            BackColor = Color.FromArgb(15, 18, 25), Size = new Size(470, 45), Location = new Point(15, 110)
        };

        var pathLabel = new Label
        {
            Text = path,
            Font = new Font("Consolas", 9),
            ForeColor = Color.FromArgb(150, 180, 255),
            AutoSize = false,
            Size = new Size(460, 35),
            Location = new Point(5, 5),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pathPanel.Controls.Add(pathLabel);

        // Question
        var questionLabel = new Label
        {
            Text = "Is this actually a single game's directory?",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Size = new Size(470, 25),
            Location = new Point(15, 165),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Yes button (primary - green tint)
        var yesBtn = new Button
        {
            Text = yesText,
            Size = new Size(200, 40),
            Location = new Point(35, 200),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 80, 60),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        yesBtn.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 80);
        yesBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 100, 70);
        yesBtn.Click += (s, e) =>
        {
            result = true;
            dialog.Close();
        };

        // No button (secondary - purple tint)
        var noBtn = new Button
        {
            Text = noText,
            Size = new Size(200, 40),
            Location = new Point(265, 200),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 40, 80),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        noBtn.FlatAppearance.BorderColor = Color.FromArgb(100, 60, 140);
        noBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 50, 110);
        noBtn.Click += (s, e) =>
        {
            result = false;
            dialog.Close();
        };

        // Allow dragging the dialog
        bool dragging = false;
        Point dragStart = Point.Empty;
        dialog.MouseDown += (s, e) =>
        {
            dragging = true;
            dragStart = e.Location;
        };
        dialog.MouseMove += (s, e) =>
        {
            if (dragging)
            {
                dialog.Location = new Point(dialog.Location.X + e.X - dragStart.X,
                    dialog.Location.Y + e.Y - dragStart.Y);
            }
        };
        dialog.MouseUp += (s, e) => { dragging = false; };

        dialog.Controls.AddRange(titleLabel, messageLabel, pathPanel, questionLabel, yesBtn, noBtn);
        dialog.ShowDialog(parent);

        return result;
    }
}
