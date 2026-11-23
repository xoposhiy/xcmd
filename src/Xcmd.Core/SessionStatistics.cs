// @implements ViewRecentAgentSessions
namespace Xcmd.Core;

public sealed record SessionStatistics(int ToolCalls, int Prompts, TimeSpan? PromptSpan, long? ContextTokens,
    long? ContextWindow, decimal? CostUsd, string? Model, long? TotalTokens = null);
