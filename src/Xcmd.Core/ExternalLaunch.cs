// @implements DomainBoundaries
namespace Xcmd.Core;

public enum ExternalLaunchKind { File, Command }
public sealed record ExternalLaunch(ExternalLaunchKind Kind, string Target, string WorkingDirectory, string Arguments = "");
public interface IExternalLauncher { Task LaunchAsync(ExternalLaunch request); }

public static class LaunchPlans
{
    public static ExternalLaunch OpenFile(string path, string directory) => new(ExternalLaunchKind.File, path, directory);
    public static ExternalLaunch RunCommand(CommandLineAction command, string directory) => new(ExternalLaunchKind.Command, command.Target, directory, command.Arguments);
}
