// @implements InspectAgentSession
namespace Xcmd.Core;

public readonly record struct ResponseKey(string Thread, string Id);

public sealed record ResponseUsage(long Input, long CacheRead, IReadOnlyDictionary<string, long> CacheWrites,
    long Output, long? Reasoning = null);

public sealed record ResponseCostPart(string Name, long Tokens, decimal Usd);

public sealed record ResponseCost(string? Model, string? Snapshot, ResponseUsage? Usage,
    IReadOnlyList<ResponseCostPart> Parts, string? Unavailable = null)
{
    public decimal? Usd => Unavailable == null ? Parts.Sum(part => part.Usd) : null;

    public static ResponseCost Missing(string? model = null, string? snapshot = null) =>
        new(model, snapshot, null, [], "Usage unavailable");
}

public sealed record ResponseRecord(ResponseKey Key, string Agent, long At, string? Message, ResponseCost Cost);

public sealed record SessionResponse(int Index, ResponseRecord Record, IReadOnlyList<TraceCall> Calls, int Task)
{
    public string Agent => Record.Agent;
    public long At => Record.At;
    public ResponseCost Cost => Record.Cost;
    public string Title => Record.Message ?? (Calls.Count switch
    {
        0 => "Model response",
        1 => Calls[0].Classification?.Summary ?? Calls[0].Title,
        _ => $"{Calls.Count} tool calls"
    });

    public bool Joins(SessionResponse previous) => Index == previous.Index + 1 && Agent == previous.Agent && Task == previous.Task
        && Cost.Usd.HasValue && previous.Cost.Usd.HasValue;
}
