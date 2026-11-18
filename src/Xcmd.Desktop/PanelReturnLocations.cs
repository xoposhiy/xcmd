// @implements BrowseHistory, SearchResultPanel, RestoreWorkspace
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed record PanelReturnPoint(PanelState State, double Width);

internal sealed class PanelReturnLocations
{
    private enum View { History, Search }
    private (View View, PanelReturnPoint Point)? current;

    public PanelReturnPoint? History => For(View.History);
    public PanelReturnPoint? Search => For(View.Search);

    private PanelReturnPoint? For(View view) => current is { } saved && saved.View == view ? saved.Point : null;

    public void EnterHistory(PanelReturnPoint point) => current = (View.History, point);

    public void LeaveHistory()
    {
        if (History != null) current = null;
    }

    public void EnterSearch(Func<PanelReturnPoint> capture)
    {
        if (Search == null) current = (View.Search, capture());
    }

    public void ApplyDirectory(bool history, PanelReturnPoint? startingPoint)
    {
        if (history && (startingPoint ?? History) is { } saved) EnterHistory(saved);
        else current = null;
    }
}
