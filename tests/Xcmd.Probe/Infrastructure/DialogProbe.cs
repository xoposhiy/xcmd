using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class DialogProbe
{
    public static Button Primary(Window window, string name, bool isDefault = true)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
        var action = buttons.Single(button => button.Name == name);
        Equal(isDefault, action.IsDefault, "The primary action has the expected Enter behavior");
        Check(action.Background is ISolidColorBrush, "The primary action has a visible background.");
        var color = ((ISolidColorBrush)action.Background!).Color;
        var secondary = buttons.First(button => button != action && !button.IsDefault && button.Background is ISolidColorBrush);
        Check(color != ((ISolidColorBrush)secondary.Background!).Color, "The primary action differs from secondary buttons.");
        return action;
    }
}
