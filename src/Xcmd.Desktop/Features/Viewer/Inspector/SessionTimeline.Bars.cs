// @implements InspectAgentSession
using Avalonia;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class SessionTimeline
{
    private int LaneFor(string agent, string? target) =>
        FirstTrack + Array.IndexOf(tracks, SessionActivityTrack.For(agent, target, CostAxis));

    private IEnumerable<(int Lane, TimelineCategory[] Categories)> CallTracks(SessionCallBlock block, string agent)
    {
        if (block.Categories.Count == 0) return [(LaneFor(agent, null), [])];
        return block.Categories.GroupBy(category => LaneFor(agent, category.Target)).Select(group => (group.Key, group.ToArray()));
    }

    private void AddResponses()
    {
        foreach (var response in Trace.Responses.Skip(selection.ZoomFirstResponse).Take(selection.ZoomLastResponse - selection.ZoomFirstResponse))
        {
            if (response.Calls.Count == 0)
            {
                Add(response, LaneFor(response.Agent, null), [], null, 0, 1);
                continue;
            }
            var rows = response.Calls.Select(SessionCallBlock.For)
                .SelectMany(block => CallTracks(block, response.Agent)
                    .Select(track => (track.Lane, track.Categories, Block: block))).ToLookup(row => row.Lane);
            foreach (var row in rows)
            {
                var share = 1d / row.Count();
                var index = 0;
                foreach (var segment in row)
                    Add(response, row.Key, segment.Categories, segment.Block, index++ * share, share);
            }
        }

        void Add(SessionResponse response, int lane, TimelineCategory[] categories, SessionCallBlock? block, double offset, double share)
        {
            responseRows[(lane, response.Index)] = response;
            var title = block?.Calls[0] is { IsStandaloneJavaScript: true } call ? call.Title : response.Title;
            bars.Add(new(lane, response.Index, response.Index + 1, title,
                response.Calls.Count == 0 ? MessageFill : UiPalette.ControlBorder, block, categories, response, offset, share));
        }
    }

    private void AddCalls()
    {
        var rows = Trace.Calls.Where(call => call.End >= selection.ZoomFrom && call.Start <= selection.ZoomTo)
            .Select(SessionCallBlock.For)
            .SelectMany(block => CallTracks(block, block.Calls[0].Agent).Select(track => (track, block)))
            .ToLookup(row => row.track.Lane);
        foreach (var row in rows)
        {
            var startIndex = bars.Count;
            var bands = new List<long>();
            foreach (var (track, block) in row.OrderBy(entry => entry.block.Calls[0].Start))
            {
                var call = block.Calls[0];
                var band = bands.FindIndex(end => end <= call.Start);
                if (band < 0) { band = bands.Count; bands.Add(call.End); }
                else bands[band] = call.End;
                bars.Add(new(track.Lane, call.Start, call.End, call.DisplayTitle,
                    UiPalette.ControlBorder, block, track.Categories, Band: band));
            }
            for (var i = startIndex; i < bars.Count; i++) bars[i] = bars[i] with { Bands = bands.Count };
        }
    }

    private bool JoinsNext(Bar bar) => bar.Response is { } response &&
        responseRows.TryGetValue((bar.Lane, response.Index + 1), out var next) && next.Joins(response);

    private void DrawMessageCaptions(DrawingContext context)
    {
        foreach (var bar in bars.Where(bar => bar.Message))
        {
            var response = bar.Response!;
            if (responseRows.TryGetValue((bar.Lane, response.Index - 1), out var previous) &&
                previous.Calls.Count == 0 && response.Joins(previous)) continue;
            var last = response;
            while (responseRows.TryGetValue((bar.Lane, last.Index + 1), out var next) &&
                next.Calls.Count == 0 && next.Joins(last)) last = next;
            var left = CostX(response.Index);
            var width = CostX(last.Index + 1) - left;
            if (width >= 42) ShortLabel(context, last == response ? "Message" : "Messages", left + 3,
                LaneTop(bar.Lane), width - 6, UiPalette.Text);
        }
    }
}
