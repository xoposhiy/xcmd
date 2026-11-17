// @implements HeadlessVerification, IntegrationVerification
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using Xcmd.Probe.Scenarios;

namespace Xcmd.Probe;

public static class ProbeAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--startup-host") return StartupProcessHost.Run(args.Skip(1).ToArray());
        if (args.FirstOrDefault() == "--lifecycle-host") return LifecycleProcessHost.Run(args.Skip(1).ToArray());
        return RunScenarios(args);
    }

    private static int RunScenarios(string[] args)
    {
        var scenarios = PanelScenarios.All.Concat(HistoryScenarios.All).Concat(WorkspaceScenarios.All).Concat(RefinementScenarios.All).Concat(FavoriteScenarios.All).Concat(NavigationAndColorsScenarios.All).Concat(DirectorySizeScenarios.All).Concat(CommandScenarios.All).Concat(CommandLineScenarios.All).Concat(RuleScenarios.All).Concat(LauncherScenarios.All).Concat(FileOperationScenarios.All).Concat(OperationRefinementScenarios.All).Concat(SelectionAndDialogScenarios.All).Concat(PanelCursorScenarios.All).Concat(RangeAndFeedbackScenarios.All).Concat(OperationUsabilityScenarios.All).ToArray();
        scenarios = scenarios.Concat(ViewerScenarios.All).ToArray();
        scenarios = scenarios.Concat(SyntaxScenarios.All).ToArray();
        scenarios = scenarios.Concat(SyntaxRegressionScenarios.All).ToArray();
        scenarios = scenarios.Concat(FeedbackInteractionScenarios.All).ToArray();
        scenarios = scenarios.Concat(AppearanceScenarios.All).ToArray();
        scenarios = scenarios.Concat(ArchiveScenarios.All).ToArray();
        scenarios = scenarios.Concat(ArchiveInteractionScenarios.All).ToArray();
        scenarios = scenarios.Concat(SearchScenarios.All).ToArray();
        scenarios = scenarios.Concat(FeedbackRemarksScenarios.All).ToArray();
        scenarios = scenarios.Concat(SearchFeedbackScenarios.All).ToArray();
        scenarios = scenarios.Concat(SearchInteractionScenarios.All).ToArray();
        scenarios = scenarios.Concat(SearchPanelScenarios.All).ToArray();
        scenarios = scenarios.Concat(OpenFeedbackScenarios.All).ToArray();
        scenarios = scenarios.Concat(DesignSystemScenarios.All).ToArray();
        scenarios = scenarios.Concat(ShellScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitRowsScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitIgnoreScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitNavigationScenarios.All).ToArray();
        scenarios = scenarios.Concat(IconPresentationScenarios.All).ToArray();
        scenarios = scenarios.Concat(FileLockScenarios.All).ToArray();
        scenarios = scenarios.Concat(MainKeyboardScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitCommitScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitHistoryScenarios.All).ToArray();
        scenarios = scenarios.Concat(GitUsabilityFixScenarios.All).ToArray();
        scenarios = scenarios.Concat(ClipboardScenarios.All).ToArray();
        scenarios = scenarios.Concat(ComparisonScenarios.All).ToArray();
        scenarios = scenarios.Concat(FileDragScenarios.All).ToArray();
        scenarios = scenarios.Concat(PriorityBatchScenarios.All).ToArray();
        scenarios = scenarios.Concat(ZipPackingScenarios.All).ToArray();
        scenarios = scenarios.Concat(IssueStoreScenarios.All).ToArray();
        scenarios = scenarios.Concat(AccumulatedFeedbackScenarios.All).ToArray();
        scenarios = scenarios.Concat(NavigationFeedbackScenarios.All).ToArray();
        if (args.Contains("--list"))
        {
            foreach (var scenario in scenarios) Console.WriteLine($"{scenario.Id}: {scenario.Requirement}");
            return 0;
        }
        var filter = Option(args, "--filter");
        var filters = filter?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var selected = scenarios.Where(scenario => filters == null || filters.Any(value => scenario.Id.Contains(value, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (selected.Length == 0) { Console.Error.WriteLine("No matching scenarios."); return 2; }
        var output = Path.GetFullPath(Option(args, "--output") ?? Path.Combine("artifacts", "probe", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        var results = new List<object>();
        var failures = 0;
        using var session = HeadlessUnitTestSession.StartNew(typeof(ProbeAppBuilder));
        foreach (var scenario in selected)
        {
            var watch = Stopwatch.StartNew();
            var folder = Path.Combine(output, scenario.Id);
            Directory.CreateDirectory(folder);
            string? failure = null;
            Dictionary<string, object>? measurements = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try
            {
                session.Dispatch(async () =>
                {
                    using var context = new ProbeContext(folder);
                    try
                    {
                        await context.StartAsync();
                        await scenario.Run(context);
                        context.SaveSnapshot();
                        measurements = context.Measurements;
                    }
                    catch
                    {
                        context.SaveSnapshot();
                        context.CaptureFailure("failure");
                        if (context.Window.ActiveNavigationDialog is { } history) context.CaptureFailure("failure-history", history);
                        if (context.Window.FileSearch.Window is { } search) context.CaptureFailure("failure-search", search);
                        throw;
                    }
                    return true;
                }, timeout.Token).WaitAsync(TimeSpan.FromSeconds(95)).GetAwaiter().GetResult();
                if (!args.Contains("--keep-fixtures"))
                {
                    var retries = FixtureCleanup.Remove(folder);
                    if (retries > 0 && measurements != null) measurements["fixtureCleanupRetries"] = retries;
                }
            }
            catch (Exception error)
            {
                failure = error.ToString(); failures++;
                File.WriteAllText(Path.Combine(folder, "failure.txt"), failure);
            }
            results.Add(new { scenario.Id, scenario.Requirement, passed = failure == null, elapsedMs = watch.Elapsed.TotalMilliseconds, measurements, failure });
            Console.WriteLine($"{(failure == null ? "PASS" : "FAIL")} {scenario.Id} ({watch.ElapsedMilliseconds} ms){(failure == null ? "" : ": " + failure.Split('\n')[0])}");
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
            {
                framework = "Avalonia 12.1.6",
                runtime = Environment.Version.ToString(),
                operatingSystem = Environment.OSVersion.ToString(),
                backend = "Avalonia.Headless + Skia software rendering",
                nativeApplicationWindows = 0,
                total = results.Count,
                failures,
                scenarios = results
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"{results.Count - failures}/{results.Count} passed. Results: {Path.Combine(output, "results.json")}");
        return failures == 0 ? 0 : 1;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
