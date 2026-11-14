namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private GitLogPresentation logPresentation = null!;

    private void ConfigureLogPresentation()
    {
        logPresentation.Update(Session);
        headersConfigured = false;
    }
}
