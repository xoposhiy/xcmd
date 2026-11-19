// @implements ReportControl
using Avalonia;

namespace Xcmd.Desktop.Shared.Platform;

public static class DesktopPlatform
{
    public static AppBuilder UseDesktopPlatform(this AppBuilder builder) => builder.UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software], OverlayPopups = true });
}
