// @implements ExternalLaunch
namespace Xcmd.Core;

public enum ExternalLaunchKind { File, Command, Url }
public sealed record ExternalLaunch(ExternalLaunchKind Kind, string Target, string WorkingDirectory, string Arguments = "");
public interface IExternalLauncher { Task LaunchAsync(ExternalLaunch request); }

public static class LaunchPlans
{
    public static ExternalLaunch OpenFile(string path, string directory) => new(ExternalLaunchKind.File, path, directory);
    public static ExternalLaunch RunCommand(CommandLineAction command, string directory) => new(ExternalLaunchKind.Command, command.Target, directory, command.Arguments);
    public static ExternalLaunch OpenUrl(Uri url, string directory) => new(ExternalLaunchKind.Url, url.AbsoluteUri, directory);
}
