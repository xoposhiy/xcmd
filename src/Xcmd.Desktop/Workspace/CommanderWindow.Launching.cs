// @implements ExternalLaunch
using Xcmd.Core;

namespace Xcmd.Desktop.Workspace;

public sealed partial class CommanderWindow
{
    private readonly IExternalLauncher externalLauncher;
    private bool launching;

    internal async Task LaunchExternalAsync(ExternalLaunch request)
    {
        if (launching) throw new IOException("A program is already opening. Try again when it finishes.");
        launching = true;
        try { await externalLauncher.LaunchAsync(request); SetError(null); }
        finally { launching = false; }
    }
}
