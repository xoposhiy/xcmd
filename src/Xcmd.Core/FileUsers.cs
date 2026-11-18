// @implements FileSystemAccess, WindowsFileActions
namespace Xcmd.Core;

public sealed record FileUser(int ProcessId, string Name, string Path, DateTime StartedAtUtc)
{
    public string? ExecutableName { get; init; }
    public string? ApplicationName { get; init; }
    public string? WindowTitle { get; init; }
    public string? WindowKey { get; init; }
}

public sealed class FileInUseException(string message, FileUser[] users, Exception inner) : IOException(message, inner)
{
    public FileUser[] Users { get; } = users;
}

public interface IFileUserActions
{
    Task ShowWindowAsync(FileUser user);
    Task EndProcessAsync(FileUser user, CancellationToken cancellationToken = default);
    Task WaitForExitAsync(FileUser user, CancellationToken cancellationToken);
}
