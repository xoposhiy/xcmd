namespace Xcmd.Infrastructure;

public enum ShellMenuAction { None, AddToZip }

public interface IShellInteraction
{
    ShellMenuAction ShowContextMenu(string[] paths, nint owner, int x, int y, bool extended);
    void ShowProperties(string path, nint owner);
    bool HandleMessage(uint message, nint wParam, nint lParam, out nint result);
}
