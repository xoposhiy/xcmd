using System.Text.Json;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    public string? FileTypeSettingsPath { get; set; }
    public async Task ReloadFileTypesAsync()
    {
        if (FileTypeSettingsPath == null) return;
        try
        {
            var types = await FileTypeSettings.LoadAsync(FileTypeSettingsPath);
            Left.SetFileTypes(types); Right.SetFileTypes(types);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { SetUpdateMessage("Could not load file type settings. Keeping the current colors."); Trace.Add(error.ToString()); }
    }
}
