using System.ComponentModel;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class FileLockDiagnostics
{
    public static async Task RunAsync(Func<Task> action, string[] paths, CancellationToken token = default, bool inspectUsersOnFailure = false)
    {
        await RunAsync(async () => { await action().ConfigureAwait(false); return true; }, paths, token, inspectUsersOnFailure).ConfigureAwait(false);
    }

    public static async Task<T> RunAsync<T>(Func<Task<T>> action, string[] paths, CancellationToken token = default, bool inspectUsersOnFailure = false)
    {
        try { return await action().ConfigureAwait(false); }
        catch (Exception error) when (IsSharingError(error) || inspectUsersOnFailure && error is IOException or UnauthorizedAccessException)
        {
            var users = await WindowsFileUsers.FindAsync(paths, token).ConfigureAwait(false);
            if (users.Length == 0 && !IsSharingError(error)) throw;
            var detail = users.Length == 0 ? "The process using this item could not be identified. " : "";
            throw new FileInUseException(detail + error.Message, users, error);
        }
    }

    private static bool IsSharingError(Exception error) =>
        (error is Win32Exception native ? native.NativeErrorCode : error.HResult & 0xFFFF) is 32 or 33 ||
        error.InnerException is { } inner && IsSharingError(inner);
}
