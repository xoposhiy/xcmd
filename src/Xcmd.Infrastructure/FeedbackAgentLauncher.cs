// @implements KeepFeedback, ExternalLaunch
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class FeedbackAgentLauncher
{
    public const string Prompt = "Use the Xcmd feedback process to choose one batch of related open issues that can be fixed in a single commit. Fix and verify that batch, update the issue queue, and commit the verified work. Preserve other work and do not push.";

    public static ExternalLaunch Prepare(FeedbackAgent agent, IssueStore store)
    {
        var repository = Path.GetDirectoryName(store.DirectoryPath)
            ?? throw new IOException("Cannot find the Xcmd repository for the issue store.");
        var files = agent == FeedbackAgent.ClaudeCode ? new[] { "claude.exe", "claude.cmd" } : ["codex.exe", "codex.cmd"];
        var target = FindConsoleTool(files) ?? throw new IOException($"Cannot find {Name(agent)}. Install its CLI and make it available on PATH.");
        return new(ExternalLaunchKind.Command, target, repository, CommandLine.FormatArgument(Prompt));
    }

    public static string Name(FeedbackAgent agent) => agent == FeedbackAgent.ClaudeCode ? "Claude Code" : "Codex";

    internal static string? FindConsoleTool(params string[] names)
    {
        var folders = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin") }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        return folders.Where(folder => !string.IsNullOrWhiteSpace(folder))
            .SelectMany(folder => names.Select(name => Path.Combine(folder.Trim('"'), name)))
            .FirstOrDefault(File.Exists);
    }
}
