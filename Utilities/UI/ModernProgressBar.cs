using System.Drawing.Drawing2D;

namespace APPID.Utilities.UI;

// Modern Progress Bar with cool blue gradient
public class ModernProgressBar : ProgressBar
{
    public ModernProgressBar()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw dark background
        e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 15)), e.ClipRectangle);

        // Calculate progress bar fill width
        int progressWidth = (int)(e.ClipRectangle.Width * ((double)Value / Maximum));

        if (progressWidth > 0)
        {
            // Create gradient brush for sky blue effect
            var progressRect = new Rectangle(0, 0, progressWidth, e.ClipRectangle.Height);
            using (var brush = new LinearGradientBrush(
                       progressRect,
                       Color.FromArgb(135, 206, 250), // Light sky blue
                       Color.FromArgb(100, 175, 220), // Darker sky blue
                       LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, progressRect);
            }

            // Add subtle highlight on top for depth
            var highlightRect = new Rectangle(0, 0, progressWidth, e.ClipRectangle.Height / 3);
            using (var highlightBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            {
                e.Graphics.FillRectangle(highlightBrush, highlightRect);
            }
        }
    }
}
