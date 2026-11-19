// @implements SharedControls
using Xcmd.Core;

namespace Xcmd.Desktop.Shared.Ui;

internal static class PathText
{
    public static string Display(string path)
    {
        var text = ArchivePath.Display(path).Replace('/', '\\');
        return text.Length > 1 && text[1] == ':' ? char.ToLowerInvariant(text[0]) + text[1..] : text;
    }
}
