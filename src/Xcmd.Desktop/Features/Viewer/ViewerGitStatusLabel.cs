// @implements ReadWorkingFileDiff
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class ViewerGitStatusLabel : TextBlock
{
    private static readonly Cursor LinkCursor = new(StandardCursorType.Hand);
    private bool canToggle;
    internal event Action? ToggleRequested;

    internal ViewerGitStatusLabel()
    {
        Name = "ViewerGitStatus";
        Margin = new Thickness(8, 2);
        FontSize = UiPalette.MetaSize;
        FontWeight = FontWeight.SemiBold;
        HorizontalAlignment = HorizontalAlignment.Left;
        TextTrimming = TextTrimming.CharacterEllipsis;
        IsVisible = false;
        AutomationProperties.SetAutomationId(this, "viewer.git-status");
        Tapped += (_, e) =>
        {
            if (!canToggle) return;
            ToggleRequested?.Invoke();
            e.Handled = true;
        };
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    internal void SetAction(string? action)
    {
        canToggle = action != null;
        Cursor = canToggle ? LinkCursor : null;
        TextDecorations = canToggle ? Avalonia.Media.TextDecorations.Underline : null;
        ToolTip.SetTip(this, canToggle ? $"{Text} · {action} (Ctrl+D)" : Text);
    }
}
