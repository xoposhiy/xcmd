using System.Diagnostics;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class GitFixture
{
    public static void AllowCleanup(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(directory, ".git"), "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
    }

    public static async Task RunAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        if (process.ExitCode != 0) throw new IOException(await error);
        await error;
    }

    public static async Task RefreshAsync(Xcmd.Desktop.GitStatusView view)
    {
        await view.PendingWork;
        view.Refresh();
        await view.PendingWork.WaitAsync(TimeSpan.FromSeconds(10));
    }
}

public sealed class DelayedGitReader : IGitStatusReader
{
    public TaskCompletionSource<GitStatus[]>? Children { get; set; }
    public TaskCompletionSource ChildReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<GitStatus[]> ReadChildRepositoriesAsync(string directory, CancellationToken cancellationToken)
    {
        ChildReadStarted.TrySetResult();
        return Children?.Task ?? Task.FromResult<GitStatus[]>([]);
    }
    public List<TaskCompletionSource<GitStatus?>> Requests { get; } = [];
    public Task<GitStatus?> ReadAsync(string directory, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<GitStatus?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Requests.Add(completion);
        return completion.Task;
    }
}

public sealed class ChildRepositoryFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "xcmd-child-repositories-" + Guid.NewGuid().ToString("N"));
    public string Repository => Path.Combine(Root, "left");

    public ChildRepositoryFixture()
    {
        Directory.CreateDirectory(Repository);
        Directory.CreateDirectory(Path.Combine(Root, "right"));
        File.WriteAllText(Path.Combine(Repository, "alpha.txt"), "initial");
    }

    public void Dispose()
    {
        if (Directory.Exists(Path.Combine(Repository, ".git"))) GitFixture.AllowCleanup(Repository);
        Directory.Delete(Root, recursive: true);
    }
}
