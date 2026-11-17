// @implements WindowsFileActions
namespace Xcmd.Infrastructure;

public sealed record ShellMenuInspection(IReadOnlyList<string> Verbs, double ParseMilliseconds,
    double BindMilliseconds, double ObjectMilliseconds, double PopulateMilliseconds)
{
    public IReadOnlyList<string> ApplicationCommands { get; init; } = [];
}
