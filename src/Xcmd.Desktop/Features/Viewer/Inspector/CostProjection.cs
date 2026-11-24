// @implements InspectAgentSession
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal readonly struct CostProjection
{
    private const double MissingWidth = 12;
    private const double FreeWidth = 2;
    private readonly SessionCostAxis axis;
    private readonly CostBoundary start;
    private readonly int first, last;
    private readonly double fixedScale, usdScale;

    internal CostProjection(SessionCostAxis axis, int first, int last, double width)
    {
        this.axis = axis;
        this.first = first;
        this.last = last;
        start = axis.At(first);
        var total = axis.At(last) - start;
        var fixedWidth = FixedWidth(total);
        fixedScale = fixedWidth == 0 ? 1 : Math.Min(1, width * (total.Usd > 0 ? 0.75 : 1) / fixedWidth);
        usdScale = total.Usd == 0 ? 0 : (width - fixedWidth * fixedScale) / (double)total.Usd;
    }

    internal double At(int boundary)
    {
        var offset = axis.At(Math.Clamp(boundary, first, last)) - start;
        return (double)offset.Usd * usdScale + FixedWidth(offset) * fixedScale;
    }

    internal int Nearest(double x)
    {
        var low = first;
        var high = last;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (x > (At(middle) + At(middle + 1)) / 2) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    internal decimal UsdAt(double x)
    {
        var boundary = Nearest(x);
        var point = At(boundary);
        if (point == x) return axis.At(boundary).Usd;
        var other = Math.Clamp(boundary + (x > point ? 1 : -1), first, last);
        var width = At(other) - point;
        var usd = axis.At(boundary).Usd;
        return width == 0 ? usd : usd + (axis.At(other).Usd - usd) * (decimal)((x - point) / width);
    }

    private static double FixedWidth(CostBoundary boundary) => MissingWidth * boundary.Missing + FreeWidth * boundary.Free;
}
