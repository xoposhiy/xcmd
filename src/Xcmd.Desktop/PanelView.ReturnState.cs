// @implements PanelReturnState
namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private readonly PanelReturnLocations returnLocations = new();

    internal PanelReturnPoint CaptureReturnPoint() => new(CaptureState(), Table.Bounds.Width);

    private async Task RestoreTemporaryViewAsync(PanelReturnPoint saved)
    {
        if (!await RestoreStateAsync(saved.State)) return;
        columns.FitNameAfterResize(saved.Width);
        RestoreScroll(saved.State);
        FocusCurrent(scrollIntoView: false);
    }
}
