// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class SessionApiPrices
{
    private static readonly Lazy<JsonElement> Prices = new(Load);
    internal static string Snapshot => Prices.Value.Str("asOf");

    private static JsonElement Load()
    {
        using var stream = typeof(SessionApiPrices).Assembly.GetManifestResourceStream("Xcmd.SessionApiPrices")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    internal static ResponseCost Price(string provider, string? model, ResponseUsage? usage)
    {
        if (usage == null) return ResponseCost.Missing(model, Snapshot);
        var prices = Prices.Value.Field(provider);
        var name = model ?? "";
        var alias = prices.Field("aliases").Str(name);
        var rates = prices.Field("models").Field(alias.Length > 0 ? alias : name);
        if (rates.ValueKind != JsonValueKind.Object)
            return new(model, Snapshot, usage, [], name.Length == 0 ? "Model unavailable" : $"No API rates for {name}");

        var input = usage.Input + (decimal)usage.CacheRead + usage.CacheWrites.Values.Sum(value => (decimal)value);
        var limits = rates.Field("longContext");
        if (limits.ValueKind == JsonValueKind.Object && input > limits.Number("aboveInputTokens")) rates = limits;
        var tariff = ReadRates(rates);
        var longContext = prices.Field("longContext");
        if (rates.Field("hasLongContextRates").ValueKind == JsonValueKind.True && input > longContext.Number("aboveInputTokens"))
            tariff = tariff.Multiply(Rate(longContext, "inputMultiplier"), Rate(longContext, "cacheMultiplier"), Rate(longContext, "outputMultiplier"));
        return tariff.Price(name, Snapshot, Prices.Value.Number("tokensPerUnit"), usage);
    }

    private static SessionRates ReadRates(JsonElement rates) =>
        new(Rate(rates, "input"), Rate(rates, "cacheRead"),
            rates.Field("cacheWrite").ValueKind == JsonValueKind.Object
                ? rates.Field("cacheWrite").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetDecimal())
                : new Dictionary<string, decimal>(), Rate(rates, "output"));

    private static decimal Rate(JsonElement rates, string name) => rates.GetProperty(name).GetDecimal();
}
