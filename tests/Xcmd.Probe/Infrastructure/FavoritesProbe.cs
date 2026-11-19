// @tests FavoriteLocations
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Desktop.Features.Navigation;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class FavoritesProbe
{
    public static async Task<DirectoryNavigationDialog> OpenAsync(ProbeContext context)
    {
        await context.KeyAsync(Key.D, RawInputModifiers.Control);
        var dialog = context.Window.ActiveNavigationDialog;
        Check(dialog != null && dialog.Favorites.IsKeyboardFocusWithin, "Ctrl+D opens favorites with keyboard focus in the list.");
        return dialog!;
    }

    public static Button Button(DirectoryNavigationDialog dialog, string name) => dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    public static async Task RenameAsync(ProbeContext context, DirectoryNavigationDialog dialog, string name)
    {
        await context.KeyAsync(Key.F2, target: dialog);
        Check(dialog.NameEditor.IsFocused, "F2 focuses the favorite name editor.");
        await context.KeyAsync(Key.A, RawInputModifiers.Control, dialog);
        await context.TextAsync(name, dialog);
        await context.KeyAsync(Key.Enter, target: dialog);
    }
}
