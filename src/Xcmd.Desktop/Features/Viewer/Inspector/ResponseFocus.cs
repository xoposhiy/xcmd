// @implements InspectAgentSession
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed record ResponseHit(SessionResponse Response, IReadOnlyList<TraceCall> Calls, string Context);

internal sealed class ResponseFocus
{
    internal SessionResponse? Hovered { get; private set; }
    internal SessionResponse? Selected { get; private set; }
    internal event Action? Changed;

    internal void Hover(SessionResponse? response)
    {
        if (Hovered == response) return;
        Hovered = response;
        Changed?.Invoke();
    }

    internal void Select(SessionResponse? response)
    {
        if (Selected == response) return;
        Selected = response;
        Changed?.Invoke();
    }
}
