// @implements PanelReturnState
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels;

internal sealed record PanelReturnPoint(PanelState State, double Width);

internal sealed class PanelReturnLocations
{
    private enum View { History, Search }
    private (View View, PanelReturnPoint Point)? current;

    public PanelReturnPoint? History => For(View.History);
    public PanelReturnPoint? Search => For(View.Search);

    private PanelReturnPoint? For(View view) => current is { } saved && saved.View == view ? saved.Point : null;

    public void ApplyDirectory(string path, PanelReturnPoint? historyReturn)
    {
        if (GitPath.IsEntry(path) && (historyReturn ?? History) is { } point) current = (View.History, point);
        else Clear();
    }
    public void SaveSearch(PanelReturnPoint point) => current = (View.Search, point);

    public void ClearHistory()
    {
        if (History != null) Clear();
    }

    public void Clear() => current = null;
}
