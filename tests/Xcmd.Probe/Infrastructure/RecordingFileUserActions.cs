// @tests RecoverOperations, WindowsFileActions
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public sealed class RecordingFileUserActions : IFileUserActions
{
    public List<(string Action, FileUser User)> Calls { get; } = [];
    public string? EndError { get; set; }
    public int? UnavailableProcessId { get; set; }
    public Task ShowWindowAsync(FileUser user) { Calls.Add(("window", user)); return Task.CompletedTask; }
    public Task EndProcessAsync(FileUser user, CancellationToken cancellationToken = default)
    {
        Calls.Add(("end", user));
        return EndError == null ? new WindowsFileUserActions().EndProcessAsync(user, cancellationToken) : Task.FromException(new IOException(EndError));
    }
    public Task WaitForExitAsync(FileUser user, CancellationToken cancellationToken) => user.ProcessId == UnavailableProcessId
        ? Task.FromException(new IOException("Process monitoring was denied."))
        : new WindowsFileUserActions().WaitForExitAsync(user, cancellationToken);
}
