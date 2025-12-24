using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace APPID.Utilities.UI;

/// <summary>
///     Custom ComboBox control with transparent background and modern dark theme styling.
/// </summary>
public class TransparentComboBox : ComboBox
{
    private Color _backColor = Color.FromArgb(8, 8, 12);
    private Color _borderColor = Color.FromArgb(30, 35, 45);

    public TransparentComboBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
            true);
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
    }

    /// <summary>
    ///     Gets or sets the background color of the control.
    /// </summary>
    [DefaultValue(typeof(Color), "8, 8, 12")]
    public new Color BackColor
    {
        get => _backColor;
        set
        {
            _backColor = value;
            Invalidate();
        }
    }

    /// <summary>
    ///     Gets or sets the border color of the control.
    /// </summary>
    [DefaultValue(typeof(Color), "30, 35, 45")]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw solid background matching theme
        using var bgBrush = new SolidBrush(_backColor);
        e.Graphics.FillRectangle(bgBrush, 0, 0, Width, Height);

        // Draw border
        using var borderPen = new Pen(_borderColor, 1);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // Draw text
        if (SelectedIndex >= 0)
        {
            using var textBrush = new SolidBrush(ForeColor);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            e.Graphics.DrawString(Text, Font, textBrush, new Rectangle(5, 0, Width - 20, Height), sf);
        }

        // Draw dropdown arrow
        using var arrowBrush = new SolidBrush(_borderColor);
        PointF[] arrow =
        [
            new(Width - 15, Height / 2 - 3),
            new(Width - 10, Height / 2 + 2),
            new(Width - 5, Height / 2 - 3)
        ];
        e.Graphics.FillPolygon(arrowBrush, arrow);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        e.DrawBackground();

        // Custom dropdown item colors
        Color itemBackColor = e.State.HasFlag(DrawItemState.Selected)
            ? Color.FromArgb(20, 25, 35)
            : Color.FromArgb(8, 8, 12);

        Color itemForeColor = e.State.HasFlag(DrawItemState.Selected)
            ? Color.FromArgb(192, 255, 255)
            : Color.White;

        using var bgBrush = new SolidBrush(itemBackColor);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        using var textBrush = new SolidBrush(itemForeColor);
        e.Graphics.DrawString(Items[e.Index].ToString(), e.Font, textBrush, e.Bounds.Left + 5, e.Bounds.Top + 2);

        e.DrawFocusRectangle();
    }
}
