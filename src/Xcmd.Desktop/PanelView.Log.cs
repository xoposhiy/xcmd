// @implements BrowseHistory
namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private GitLogPresentation logPresentation = null!;

    private void ConfigureLogPresentation()
    {
        columns.ShowLocation(Session);
        headersConfigured = false;
    }
}
