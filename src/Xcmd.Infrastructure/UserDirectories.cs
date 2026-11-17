// @implements DirectoryMonitoring
namespace Xcmd.Infrastructure;

public static class UserDirectories
{
    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
