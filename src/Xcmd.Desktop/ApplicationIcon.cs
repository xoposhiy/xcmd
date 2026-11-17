// @implements PackageInputs
using Avalonia.Controls;
using Avalonia.Platform;

namespace Xcmd.Desktop;

internal static class ApplicationIcon
{
    public static WindowIcon Load()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Xcmd.Desktop/Assets/app.ico"));
        return new WindowIcon(stream);
    }
}
