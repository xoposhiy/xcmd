// @tests InspectAgentSession
using System.Net;
using System.Text.Json;
using Xcmd.Core;
using Xcmd.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

internal static class CoverageClientFixture
{
    internal sealed class Transport : HttpMessageHandler
    {
        internal int Requests;
        internal bool Fail;
        internal string Answer = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var data = json.RootElement;
            Equal("gpt-6-luna", data.GetProperty("model").GetString(), "Use the selected model");
            Equal("none", data.GetProperty("reasoning").GetProperty("effort").GetString(), "Use zero reasoning effort");
            Check(!data.GetProperty("store").GetBoolean(), "Request no response storage");
            Check(data.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(), "Require structured file reads");
            var content = new[] { new { type = "output_text", text = Answer } };
            var body = JsonSerializer.Serialize(new { status = "completed", output = new[] { new { content } } });
            return new(Fail ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private static string Reply(string path, int count, string evidence, int operationLine = 1) => JsonSerializer.Serialize(new
    {
        reads = new[] { new { path, operationLine, lines = count, lineEvidence = evidence } },
        unresolved = false
    });

    internal static async Task CheckAsync(ProbeContext c)
    {
        var folder = Path.Combine(c.FixturePath, "luna-cache");
        var transport = new Transport { Answer = Reply("src/menu.cs", 4, "4") };
        using var http = new HttpClient(transport);
        var client = new LunaCoverageClient(folder, () => "fixture", http);
        var input = new CoverageInput("pwsh", "C:/test", "C:/test", "[IO.File]::ReadAllLines('src/menu.cs').Length", "4\n", "exit 0");
        var both = await Task.WhenAll(client.ReviewAsync(input, default), client.ReviewAsync(input, default));
        Equal(1, transport.Requests, "Concurrent viewers share one request");
        Equal(4, both[0].Reads.Single().Lines, "The output contains the reported line count");
        Equal(0, both[0].Reads.Single().FirstLine, "A count alone has no start position");
        var reopened = new LunaCoverageClient(folder, () => null, http);
        await reopened.ReviewAsync(input, default);
        Equal(1, transport.Requests, "Reopening a viewer can use a cached answer offline");
        transport.Answer = Reply("src/other.cs", 4, "4");
        var missing = await client.ReviewAsync(input with { Result = "4\nSee src/other.cs for details." }, default);
        Check(missing.Unresolved && missing.Reads.Length == 0, "A file mentioned only in prose has no read evidence");
        transport.Answer = Reply("src/menu.cs", 400, "4");
        var wrongCount = await client.ReviewAsync(input with { Result = "4\r\n" }, default);
        Equal(0, wrongCount.Reads.Single().Lines, "An unsupported count stays unknown");
        transport.Answer = Reply("src/menu.cs", 0, "", 27);
        var wrongLine = await client.ReviewAsync(input with { Status = "completed" }, default);
        Check(wrongLine.Unresolved && wrongLine.Reads.Length == 0, "An absent operation cannot support a file");
        var retry = input with { Result = "14\n" };
        transport.Fail = true;
        try { await client.ReviewAsync(retry, default); throw new InvalidDataException("Expected an HTTP failure."); }
        catch (HttpRequestException) { }
        transport.Fail = false;
        transport.Answer = Reply("src/menu.cs", 14, "14");
        Equal(14, (await client.ReviewAsync(retry, default)).Reads.Single().Lines, "Retry a failed request without caching the failure");
        await CheckResumeAsync(client, input);
    }

    private static async Task CheckResumeAsync(ICoverageReviewer reviewer, CoverageInput input)
    {
        var file = new TraceFile("src/Menu.cs", "Read", 1, 4, 4);
        var call = new TraceCall("Main agent", "Read files", 1, 2, 10, [file]) { Review = input };
        var other = call with { End = 3, Files = [] };
        var trace = new SessionTrace(0, 4, [call, other], [], []);
        var review = new CoverageReview();
        using var stop = new CancellationTokenSource();
        try { await review.RunAsync(trace, reviewer, stop.Cancel, stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        Equal(1, review.Completed, "Cancel keeps finished reviews");
        await review.RunAsync(trace, reviewer, () => { }, default);
        Equal(2, review.Completed, "Resume finishes the remaining call");
        Equal(1, review.AddedReads, "Preserve a local read and add the other inferred read");
    }
}
