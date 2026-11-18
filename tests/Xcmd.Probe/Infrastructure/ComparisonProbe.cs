// @tests ComparePanelFiles, ExternalLaunch
using Xcmd.Core;
using Xcmd.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class ComparisonProbe
{
    public static async Task VerifyNativeLaunchAsync(ProbeContext context)
    {
        var fixture = CommandProbe.Prepare(context);
        await File.WriteAllTextAsync(Path.Combine(fixture.Directory, "editor-probe.mode"), "");
        var left = Path.Combine(context.LeftPath, "left & 日本語 %.txt");
        var right = Path.Combine(context.RightPath, "right & отчёт %.txt");
        await File.WriteAllTextAsync(left, "Before");
        await File.WriteAllTextAsync(right, "After");
        var request = await new VisualStudioCodeComparison(fixture.Executable).PrepareAsync(new(left, right));
        await new WindowsExternalLauncher(showWindows: false).LaunchAsync(request);
        var report = await CommandProbe.ReadReportAsync(fixture.Report);
        Check(report.Arguments.SequenceEqual(new[] { "--new-window", "--diff", left, right }), "The native process receives two distinct paths in panel order");
        Equal(context.LeftPath, report.WorkingDirectory, "The comparison starts in the left file's directory");
        Equal("Before", await File.ReadAllTextAsync(left), "Launching comparison preserves the left file");
        Equal("After", await File.ReadAllTextAsync(right), "Launching comparison preserves the right file");
        try
        {
            await new VisualStudioCodeComparison(Path.Combine(fixture.Directory, "missing.exe")).PrepareAsync(new(left, right));
            throw new InvalidOperationException("A missing VS Code installation must be reported.");
        }
        catch (IOException error) { Check(error.Message.Contains("Install VS Code", StringComparison.Ordinal), "A missing tool explains how to recover"); }
    }
}
