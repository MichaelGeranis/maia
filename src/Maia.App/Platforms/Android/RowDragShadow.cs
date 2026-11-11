using APoint = Android.Graphics.Point;
using AView = Android.Views.View;

namespace Maia.App.Platforms.Android;

/// <summary>
/// The picture Android drags under her finger: a whole row (a question on Questions) rather than the small handle she
/// holds, kept under her finger where she took hold of it, so the row itself follows her finger.
/// </summary>
public sealed class RowDragShadow : AView.DragShadowBuilder
{
    private readonly int _touchX;
    private readonly int _touchY;

    /// <param name="row">The row to draw.</param>
    /// <param name="handle">The view she holds, inside the row.</param>
    /// <param name="x">Where she touched the handle, in its own pixels.</param>
    /// <param name="y">Where she touched the handle, in its own pixels.</param>
    public RowDragShadow(AView row, AView handle, float x, float y)
        : base(row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(handle);
        var rowAt = new int[2];
        var handleAt = new int[2];
        row.GetLocationInWindow(rowAt);
        handle.GetLocationInWindow(handleAt);
        _touchX = Math.Clamp(handleAt[0] - rowAt[0] + (int)x, 0, Math.Max(row.Width - 1, 0));
        _touchY = Math.Clamp(handleAt[1] - rowAt[1] + (int)y, 0, Math.Max(row.Height - 1, 0));
    }

    public override void OnProvideShadowMetrics(APoint? outShadowSize, APoint? outShadowTouchPoint)
    {
        var row = View;
        if (row is null || row.Width <= 0 || row.Height <= 0)
        {
            base.OnProvideShadowMetrics(outShadowSize, outShadowTouchPoint);
            return;
        }

        outShadowSize?.Set(row.Width, row.Height);
        outShadowTouchPoint?.Set(_touchX, _touchY);
    }
}
