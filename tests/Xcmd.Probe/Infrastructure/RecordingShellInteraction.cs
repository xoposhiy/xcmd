// @implements HeadlessVerification
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public sealed class RecordingShellInteraction : IShellInteraction
{
    public List<string> Menus { get; } = [];
    public List<string[]> MenuGroups { get; } = [];
    public List<string> Properties { get; } = [];
    public bool Extended { get; private set; }
    public (int X, int Y) Position { get; private set; }
    public bool Fail { get; set; }
    public ShellMenuAction NextAction { get; set; }
    public ShellMenuAction ShowContextMenu(string[] paths, nint owner, int x, int y, bool extended)
    {
        if (Fail) throw new IOException("Shell fixture failure.");
        MenuGroups.Add(paths); Menus.Add(paths[0]); Extended = extended; Position = (x, y);
        var action = NextAction; NextAction = ShellMenuAction.None; return action;
    }
    public void ShowProperties(string path, nint owner)
    {
        if (Fail) throw new IOException("Shell fixture failure.");
        Properties.Add(path);
    }
    public bool HandleMessage(uint message, nint wParam, nint lParam, out nint result) { result = 0; return false; }
}
