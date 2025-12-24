using System.Drawing;
using System.Windows.Forms;

namespace APPID.Utilities.UI;

/// <summary>
/// Handles window dragging functionality for borderless forms.
/// </summary>
public class WindowDragHandler
{
    private Point _mouseDownPoint = Point.Empty;
    private readonly Form _form;

    public WindowDragHandler(Form form)
    {
        _form = form ?? throw new ArgumentNullException(nameof(form));
    }

    /// <summary>
    /// Attaches drag handlers to the specified control.
    /// </summary>
    /// <param name="control">The control to make draggable (typically title bar or main panel).</param>
    public void AttachToControl(Control control)
    {
        if (control == null)
        {
            throw new ArgumentNullException(nameof(control));
        }

        control.MouseDown += OnMouseDown;
        control.MouseMove += OnMouseMove;
        control.MouseUp += OnMouseUp;
    }

    /// <summary>
    /// Detaches drag handlers from the specified control.
    /// </summary>
    public void DetachFromControl(Control control)
    {
        if (control == null)
        {
            return;
        }

        control.MouseDown -= OnMouseDown;
        control.MouseMove -= OnMouseMove;
        control.MouseUp -= OnMouseUp;
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // Don't drag if clicking on DataGridView (allows column resizing)
            var clickedControl = _form.GetChildAtPoint(_form.PointToClient(Cursor.Position));
            if (clickedControl is DataGridView)
            {
                return;
            }

            _mouseDownPoint = new Point(e.X, e.Y);
            _form.Cursor = Cursors.SizeAll;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _mouseDownPoint != Point.Empty)
        {
            _form.Location = new Point(
                _form.Location.X + e.X - _mouseDownPoint.X,
                _form.Location.Y + e.Y - _mouseDownPoint.Y);
        }
    }

    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        _form.Cursor = Cursors.Default;
        _mouseDownPoint = Point.Empty;
    }
}
