// @tests ChooseViewMode
using System.Text;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.StructuredJsonProbe;

namespace Xcmd.Probe.Scenarios;

public static class StructuredJsonPeerScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("viewer-structured-peer-navigation-and-layout", "Peer commands keep the nearest array scope and apply the current record layout", async c =>
        {
            var permissions = new string('p', 180);
            var source = $$$"""
                {"users":[
                  {"address":{"city":"Ada","zip":1},"orders":[{"sku":"a"},{"sku":"b"}],"permissions":["{{{permissions}}}"]},
                  {"nickname":"skip"},
                  {"address":{"city":"Lin","zip":2},"orders":[{"sku":"c"}],"permissions":["{{{permissions}}}"]},
                  {"address":5}
                ]}
                """;
            var viewer = await OpenAsync(c, "peers.json", source);
            Check(viewer.Reader.ReadingStatus.Contains("Group commands unavailable", StringComparison.Ordinal), "The root has no containing group");
            for (var level = 0; level < 4; level++) await MoveAsync(c, viewer, Key.Right);
            CheckPath(viewer, "$.users[0].address.city");
            Check(viewer.Reader.ReadingStatus.Contains("Group: $.users[*].address.city", StringComparison.Ordinal), "A field groups by the path within a user");
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.users[2].address.city");
            Check(viewer.Reader.ReadingStatus.Contains("Skipped 1 peer", StringComparison.Ordinal), "Peer movement reports the missing address");
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.users[2].address.city");
            await MoveAsync(c, viewer, Key.Left);
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.users[3].address");
            await MoveAsync(c, viewer, Key.Up, Peer);
            CheckPath(viewer, "$.users[2].address");

            await MoveAsync(c, viewer, Key.Left, Peer);
            await MoveAsync(c, viewer, Key.Left, Peer);
            Check(Text(viewer).Split("\"address\": {\"city\":", StringSplitOptions.None).Length == 3, "Both address containers are compact after repeated collapse");
            await MoveAsync(c, viewer, Key.Right, Peer);
            CheckPath(viewer, "$.users[2].address");
            Check(Text(viewer).Split("\"address\": {\n", StringSplitOptions.None).Length == 3, "Expand opens both address containers");
            await MoveAsync(c, viewer, Key.Left, Peer);
            await MoveAsync(c, viewer, Key.Up, Peer);
            await c.KeyAsync(Key.Space, target: viewer);
            await MoveAsync(c, viewer, Key.Left);
            await MoveAsync(c, viewer, Key.Space, RawInputModifiers.Control);
            CheckPath(viewer, "$.users[0]");
            Check(Text(viewer).Split("\"address\": {\n", StringSplitOptions.None).Length == 3, "Copying the record layout expands both matching addresses");
            Check(Text(viewer).Split("\"permissions\": [⟪", StringSplitOptions.None).Length == 3, "Permissions remain compact on both records");

            await MoveAsync(c, viewer, Key.Right);
            await MoveAsync(c, viewer, Key.Down);
            Check(viewer.Reader.ReadingStatus.Contains("Group: $.users[*].orders", StringComparison.Ordinal), "An orders field groups the users' arrays");
            await MoveAsync(c, viewer, Key.Right);
            Check(viewer.Reader.ReadingStatus.Contains("Group: $.users[0].orders[*]", StringComparison.Ordinal), "An order groups only orders in its containing array");
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.users[0].orders[1]");
        }),
        new("viewer-structured-object-peer-navigation", "Peer arrows visit the same field under neighboring object keys", async c =>
        {
            const string source = """
                {"files": {
                  "src/Z.cs": {"complexity": 12, "tags": ["core"]},
                  "count": 3,
                  "src/NoMetrics.cs": {"tags": ["tests"]},
                  "src/A.cs": {"complexity": "n/a", "tags": ["ui"]}
                }}
                """;
            var viewer = await OpenAsync(c, "object-peers.json", source);
            for (var level = 0; level < 3; level++) await MoveAsync(c, viewer, Key.Right);
            CheckPath(viewer, "$.files[\"src/Z.cs\"].complexity");
            Check(viewer.Reader.ReadingStatus.Contains("Navigation group: $.files.*.complexity", StringComparison.Ordinal), "The group shows the wildcard in place of the record key");

            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.files[\"src/A.cs\"].complexity");
            Check(viewer.Reader.ReadingStatus.Split('\n')[1].StartsWith("Skipped 2 peers", StringComparison.Ordinal), "The skip count is visible before the group path");
            Check(Text(viewer).Contains("    \"src/A.cs\": {\n      \"complexity\": \"n/a\"", StringComparison.Ordinal), "The matching field is revealed even when its value type differs");
            await c.ScreenshotAsync("object-peer-navigation", viewer);
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.files[\"src/A.cs\"].complexity");
            Check(viewer.Reader.ReadingStatus.Contains("No further matching record", StringComparison.Ordinal), "The final record reports the group boundary");
            await MoveAsync(c, viewer, Key.Up, Peer);
            CheckPath(viewer, "$.files[\"src/Z.cs\"].complexity");
            await MoveAsync(c, viewer, Key.Up, Peer);
            CheckPath(viewer, "$.files[\"src/Z.cs\"].complexity");

            await MoveAsync(c, viewer, Key.Left);
            var before = Text(viewer);
            await MoveAsync(c, viewer, Key.Left, Peer);
            Equal(before, Text(viewer), "Fold commands still require an array group");
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.files[\"src/NoMetrics.cs\"]");
        }),
        new("viewer-structured-object-peers-in-array", "Peer navigation uses a nearby object map inside an array", async c =>
        {
            const string source = """
                {"runs": [
                  {"files": {"z": {"score": 1}, "a": {"score": 2}}},
                  {"files": {"z": {"score": 3}, "a": {"score": 4}}}
                ]}
                """;
            var viewer = await OpenAsync(c, "nested-object-peers.json", source);
            for (var level = 0; level < 5; level++) await MoveAsync(c, viewer, Key.Right);
            CheckPath(viewer, "$.runs[0].files.z.score");
            var status = viewer.Reader.ReadingStatus;
            Check(status.Contains("Navigation group: $.runs[0].files.*.score", StringComparison.Ordinal), "The closest named records are the navigation group");
            Check(status.Contains("Layout group: $.runs[*].files.z.score", StringComparison.Ordinal), "The enclosing array remains the layout group");
            await MoveAsync(c, viewer, Key.Down, Peer);
            CheckPath(viewer, "$.runs[0].files.a.score");
            viewer.SetScaleForTest(1.5);
            await c.ScreenshotAsync("nested-object-peer-navigation-150", viewer);
        }),
        new("viewer-structured-peer-exact-paths", "Layouts preserve duplicate occurrences, nested indexes, and target-only fields", async c =>
        {
            const string source = """
                [
                  {"entry":{"left":{"n":1}},"\u0065ntry":{"right":{"n":2}},"nested":[{"child":{"n":1}},{"child":{"n":2}}]},
                  {"entry":{"left":{"n":3}},"entry":{"right":{"n":4},"extra":{"n":5}},"nested":[{"child":[1]},{"child":{"n":6}},{"child":{"n":7}}]},
                  ["incompatible"]
                ]
                """;
            var path = Path.Combine(c.LeftPath, "layout.json");
            await File.WriteAllTextAsync(path, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var index = await StructuredJsonSource.ReadAsync(new ViewerFiles(), path, Encoding.UTF8, CancellationToken.None);
            Equal(3L, index.Bytes.At(0), "Source positions account for the UTF-8 byte order mark");
            var tree = index.Tree;
            var first = tree[0].Children[0];
            var second = tree[0].Children[1];
            var firstEntry = tree[first].Children[0];
            var repeatedEntry = tree[first].Children[1];
            var secondEntry = tree[second].Children[1];
            var sourceNested = tree[tree[first].Children[2]].Children;
            var targetNested = tree[tree[second].Children[2]].Children;
            var sourceOne = tree[sourceNested[1]].Children[0];
            var targetZero = tree[targetNested[0]].Children[0];
            var targetOne = tree[targetNested[1]].Children[0];
            var targetTwo = tree[targetNested[2]].Children[0];
            var extra = tree[secondEntry].Children[1];
            var folds = StructuredJsonFolds.Initial.Set(first, true).Set(repeatedEntry, true).Set(sourceOne, true)
                .Set(targetZero, true).Set(targetTwo, true).Set(extra, true);
            var peers = folds.Apply(tree, first, JsonPeerAction.ApplyLayout, CancellationToken.None);
            Equal(2, peers.Skipped, "The result reports a partial peer and an incompatible peer");
            Check(peers.Folds.IsExpanded(second) && peers.Folds.IsExpanded(secondEntry), "The second occurrence receives the matching layout");
            Check(!peers.Folds.IsExpanded(tree[second].Children[0]) && !peers.Folds.IsExpanded(firstEntry), "The first occurrence stays compact");
            Check(peers.Folds.IsExpanded(targetOne), "The matching nested index receives the layout");
            Check(peers.Folds.IsExpanded(targetZero) && peers.Folds.IsExpanded(targetTwo) && peers.Folds.IsExpanded(extra), "Incompatible types and target-only paths keep their state");
            var result = peers.Folds.Apply(tree, repeatedEntry, JsonPeerAction.ApplyLayout, CancellationToken.None);
            Check(result.Folds.IsExpanded(extra) && result.Folds.IsExpanded(targetOne), "Applying a field layout preserves the rest of the record");
        })
    ];
}
