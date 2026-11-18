// @tests RecoverOperations, WindowsFileActions
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Xcmd.Probe.Infrastructure;

public sealed class LockingProcessFixture : IAsyncDisposable
{
    private readonly Process process;
    public int Id => process.Id;

    private LockingProcessFixture(Process process) => this.process = process;

    public static Task<LockingProcessFixture> StartExecutableInsideAsync(ProbeContext context, string directory)
    {
        var executableDirectory = Directory.CreateDirectory(Path.Combine(directory, "tools")).FullName;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "launch-probe")))
            File.Copy(file, Path.Combine(executableDirectory, Path.GetFileName(file)));
        var trigger = Path.Combine(context.FixturePath, "process-trigger.txt");
        File.WriteAllText(trigger, "");
        return StartAsync(context, trigger, executable: Path.Combine(executableDirectory, "Xcmd.LaunchProbe.exe"));
    }

    public static async Task<LockingProcessFixture> StartAsync(ProbeContext context, string path, bool directory = false, string? executable = null)
    {
        var ready = Path.Combine(context.OutputPath, "lock-ready-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo(executable ?? Path.Combine(AppContext.BaseDirectory, "launch-probe", "Xcmd.LaunchProbe.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            WorkingDirectory = context.FixturePath
        };
        foreach (var argument in new[] { directory ? "--hold-directory" : "--hold-file", path, ready }) start.ArgumentList.Add(argument);
        var fixture = new LockingProcessFixture(Process.Start(start)!);
        try
        {
            await FileOperationProbe.WaitUntilAsync(() => File.Exists(ready) || fixture.process.HasExited);
            ProbeContext.Check(!fixture.process.HasExited, "The helper holds the requested path without a visible window");
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task ReleaseAsync()
    {
        if (process.HasExited) return;
        await process.StandardInput.WriteLineAsync("release");
        await process.StandardInput.FlushAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    public static string ErrorText(ProbeContext context) => string.Join("\n",
        context.Window.OperationDialog!.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));

    public async ValueTask DisposeAsync()
    {
        try { await ReleaseAsync(); }
        finally
        {
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); }
            process.Dispose();
        }
    }
}
