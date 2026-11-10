using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Xcmd.Desktop;

internal static class DialogActions
{
    public static void FollowFocus(Window window, Button primary, params Button[] otherActions)
    {
        var actions = otherActions.Prepend(primary).ToArray();
        primary.IsDefault = true;
        window.AddHandler(InputElement.GotFocusEvent, (_, _) =>
        {
            var focused = window.FocusManager?.GetFocusedElement();
            var selected = focused is Button and not RadioButton ? actions.FirstOrDefault(action => action == focused) : primary;
            foreach (var action in actions) action.IsDefault = action == selected;
        }, RoutingStrategies.Bubble);
    }
}
