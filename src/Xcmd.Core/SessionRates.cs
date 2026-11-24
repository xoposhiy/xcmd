// @implements InspectAgentSession
namespace Xcmd.Core;

public sealed record SessionRates(decimal Input, decimal CacheRead, IReadOnlyDictionary<string, decimal> CacheWrites, decimal Output)
{
    public SessionRates Multiply(decimal input, decimal cache, decimal output) =>
        new(Input * input, CacheRead * cache, CacheWrites.ToDictionary(pair => pair.Key, pair => pair.Value * cache), Output * output);

    public ResponseCost Price(string model, string snapshot, long unit, ResponseUsage usage)
    {
        var parts = new List<ResponseCostPart>
        {
            Part("Uncached input", usage.Input, Input),
            Part("Cache read", usage.CacheRead, CacheRead)
        };
        foreach (var (duration, tokens) in usage.CacheWrites.Where(pair => pair.Value > 0))
        {
            if (!CacheWrites.TryGetValue(duration, out var rate))
                return new(model, snapshot, usage, [], duration.Length == 0 ? "Cache write duration unavailable" : $"No rate for {duration} cache writes");
            parts.Add(Part($"Cache write ({duration})", tokens, rate));
        }
        parts.Add(Part("Output", usage.Output, Output));
        return new(model, snapshot, usage, parts);

        ResponseCostPart Part(string name, long tokens, decimal rate) => new(name, tokens, tokens * rate / unit);
    }
}
