// @tests InspectAgentSession
using SkiaSharp;
using Xcmd.Core;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

internal static class SessionTimelineFixture
{
    internal static SessionTrace Use(AgentSessionFixture sessions)
    {
        var main = new ToolResource("file", "src/main.cs");
        var other = new ToolResource("file", "src/other.cs");
        var tests = new ToolResource("file", "tests/verify.cs");
        var first = Key(0);
        var second = Key(1);
        var third = Key(2);
        var missing = Key(5);
        const string reviewScript = "text(load(\"review\"));";
        var trace = new SessionTrace(0, 10000,
        [
            Call(first, 1200, 1600, 3, "Run the tests", Operation("tests", "exec", tests)),
            Call(first, 1000, 2500, 1, "Read the main file", Operation("source", "read", main)),
            Call(first, 1100, 2000, 2, "Edit the other file", Operation("source", "write", other)),
            Call(second, 3000, 4100, 4, "Check source and tests", Operation("source", "read", main), Operation("tests", "read", tests)),
            Call(second, 3100, 4500, 5, "Rerun the tests", Operation("tests", "exec", tests)),
            Call(third, 5000, 6000, 6, "Confirm results", Operation("tests", "read", tests)),
            Call(missing, 9000, 9500, 7, "Review the main file", Operation("source", "read", main)) with
            {
                ToolName = "exec",
                Title = "JavaScript · " + reviewScript,
                Input = reviewScript,
                JavaScript = reviewScript
            }
        ], [new("Review and verify", 0)], [new("Inspect", 0), new("Verify", 2800)], responses:
        [
            Response(first, 1000, 0.06m), Response(second, 3000, 0.04m), Response(third, 5000, 0.02m),
            Response(Key(3), 6900, 0.02m, "Results are ready"), Response(Key(4), 7900, 0.02m, "Review complete"),
            Response(missing, 9000, null)
        ]);
        sessions.Inspector = (_, _) => Task.FromResult(trace);
        return trace;

        static TraceCall Call(ResponseKey key, long start, long end, int order, string summary, params ToolOperation[] operations) =>
            new(SessionTrace.MainAgent, summary, start, end, [])
            {
                Id = order,
                Response = key,
                ToolName = "exec_command",
                EmissionOrder = order,
                Classification = new(summary, operations, operations.SelectMany(op => op.Objects).Distinct()
                    .Select(path => new ToolResource("file", path)).ToArray())
            };
    }

    internal static SessionTrace Priced(long end, (TraceCall Call, decimal Cost)[] calls,
        TraceMarker[]? tasks = null, TraceMarker[]? phases = null)
    {
        var assigned = calls.Select((entry, index) => (Call: entry.Call with { Response = Key(index) }, entry.Cost)).ToArray();
        return new(0, end, assigned.Select(entry => entry.Call), tasks ?? [], phases ?? [], responses: assigned.Select(entry =>
            Response(entry.Call.Response!.Value, entry.Call.Start, entry.Cost, agent: entry.Call.Agent)));
    }

    internal static void UseZoomSpans(AgentSessionFixture sessions)
    {
        var calls = Enumerable.Range(0, 4).Select(index =>
            (new TraceCall(SessionTrace.MainAgent, $"Operation {index + 1}", index * 1000 + 100, index * 1000 + 900, []), 0.10m)).ToArray();
        var trace = Priced(4000, calls, [new("Review", 0), new("Publish", 2000)],
            [new("Read", 0), new("Check", 1000), new("Ship", 2000)]);
        sessions.Inspector = (_, _) => Task.FromResult(trace);
    }

    private static ResponseKey Key(int index) => new("fixture", index.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static ResponseRecord Response(ResponseKey key, long at, decimal? cost, string? message = null,
        string agent = SessionTrace.MainAgent) =>
        new(key, agent, at, message, cost is { } usd
            ? new("fixture model", "2026-11-24", new(100, 0, new Dictionary<string, long>(), 200, 100), [new("Output", 200, usd)])
            : ResponseCost.Missing(snapshot: "2026-11-24"));

    internal static void UseReleaseTrace(AgentSessionFixture sessions)
    {
        var repository = new ToolResource("repository", "mdview");
        var script = new ToolResource("file", "scripts/release.ps1");
        var gitRead = Operation("git", "read", repository);
        var gitWrite = Operation("git", "write", repository);
        var build = Operation("build_release", "exec", script);
        var test = Operation("tests", "exec", script);
        var trace = Priced(500,
        [
            ReleaseCall(100, "Read the Git status.", [gitRead], repository),
            ReleaseCall(200, "Committed changes and ran release checks.", [gitWrite, gitRead, build, test], repository, script),
            ReleaseCall(300, "Read and reran the release script.", [Operation("build_release", "read", script), build], script),
            ReleaseCall(400, "Edited and reran the release script.", [Operation("build_release", "write", script), build, test], script),
            ReleaseCall(500, "Committed the release fix.", [Operation("git", "write", script), gitRead], script, repository)
        ]);
        sessions.Inspector = (_, _) => Task.FromResult(trace);
    }

    private static ToolOperation Operation(string target, string action, ToolResource resource) =>
        new(new(target, action), [resource.Value]);

    internal static void UseResourceLists(AgentSessionFixture sessions)
    {
        var trace = Priced(3000, [
            ListCall(0, 1, 1), ListCall(1000, 2, 1), ListCall(2000, 8, 2)
        ]);
        sessions.Inspector = (_, _) => Task.FromResult(trace);

        static (TraceCall, decimal) ListCall(int start, int builds, int tests)
        {
            var buildFiles = Files("build", builds);
            var testFiles = Files("test", tests);
            return (new(SessionTrace.MainAgent, "Read build and test scripts", start, start + 900, [])
            {
                ToolName = "exec_command",
                Classification = new("Read build and test scripts",
                    [new(new("build_release", "read"), buildFiles.Select(file => file.Value).ToArray()),
                     new(new("tests", "read"), testFiles.Select(file => file.Value).ToArray())], [.. buildFiles, .. testFiles])
            }, 0.10m);
        }

        static ToolResource[] Files(string kind, int count) => Enumerable.Range(1, count)
            .Select(index => new ToolResource("file", $"scripts/{kind}-{index}.ps1")).ToArray();
    }

    private static (TraceCall, decimal) ReleaseCall(int end, string summary, ToolOperation[] operations, params ToolResource[] resources) =>
        (new(SessionTrace.MainAgent, "Run shell command", end - 100, end, [])
        {
            Id = end,
            ToolName = "exec_command",
            Classification = new(summary, operations, resources)
        }, 0.10m);

    internal static async Task CheckReleaseColorsAsync(ProbeContext context, InspectorProbe probe, string file)
    {
        await probe.Screenshot(file);
        using var image = SKBitmap.Decode(Path.Combine(context.OutputPath, file + ".png"));
        var git = Pixel(0.25, 0, 2);
        var build = Pixel(0.25, 0, 14);
        var tests = Pixel(0.25, 1, 2);
        Check(git.Green - git.Blue > 40 && build.Red - build.Green > 40 && tests.Green - tests.Red > 80,
            "Git has a gold write color, the build is orange and the tests are green");
        Equal(Pixel(0.25, 3, 2), Pixel(0.25, 2, 2), "Source is as empty as Docs at the release call");

        SKColor Pixel(double fraction, int track, int y)
        {
            var point = probe.TimelinePoint(fraction, track, y);
            var scale = probe.Window.RenderScaling;
            return image.GetPixel((int)(point.X * scale), (int)(point.Y * scale));
        }
    }

    internal static async Task CheckJoinedHoverAsync(ProbeContext context, InspectorProbe probe)
    {
        await probe.TimelineTip(0.79, 2);
        using var before = await Capture("session-cost-joined");
        await probe.TimelineTip(0.10, 0);
        using var after = await Capture("session-cost-hover");
        var first = probe.TimelinePoint(0, 0, 12);
        var last = probe.TimelinePoint(1, 0, 12);
        var boundary = first.X + (last.X - first.X - 12) * 0.375;
        for (var track = 0; track < 2; track++)
        {
            var y = probe.TimelinePoint(0, track, 12).Y;
            var column = (int)(boundary * probe.Window.RenderScaling);
            var row = (int)(y * probe.Window.RenderScaling);
            var highlight = Enumerable.Range(column - 2, 4)
                .Max(x => after.GetPixel(x, row).Red - before.GetPixel(x, row).Red);
            Check(highlight > 40, "Hover outlines the response on both tracks, including across work phases");
        }

        async Task<SKBitmap> Capture(string file)
        {
            await probe.Screenshot(file);
            return SKBitmap.Decode(Path.Combine(context.OutputPath, file + ".png"));
        }

    }
}
