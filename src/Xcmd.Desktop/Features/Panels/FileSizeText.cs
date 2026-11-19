// @implements ViewSelectionSize
namespace Xcmd.Desktop.Features.Panels;

public static class FileSizeText
{
    public static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes:N0} bytes";
        var units = new[] { "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };
        decimal size = bytes;
        var unit = -1;
        do { size /= 1024; unit++; } while (Math.Round(size, 1) >= 1024 && unit < units.Length - 1);
        return $"{size:0.#} {units[unit]}";
    }
}
