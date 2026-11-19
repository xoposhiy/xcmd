// @implements DiagnoseFailures
namespace Xcmd.Infrastructure;

public static class StartupDiagnostics
{
    public static async Task SaveAsync(string sessionPath, Exception error)
    {
        var path = Path.Combine(Path.GetDirectoryName(sessionPath)!, "startup-error.txt");
        try { await File.WriteAllTextAsync(path, error.ToString()); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) { }
    }
}
