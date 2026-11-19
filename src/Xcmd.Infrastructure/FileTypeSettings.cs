// @implements RecognizeFileTypes
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class FileTypeSettings
{
    private static string BundledPath => Path.Combine(AppContext.BaseDirectory, "config", "file-types.json");
    public static FileTypes Bundled() => Parse(File.ReadAllText(BundledPath));
    public static FileTypes Parse(string json) => new(JsonSerializer.Deserialize(json, FileTypesJsonContext.Default.FileTypeConfiguration)
        ?? throw new InvalidDataException("File type settings are empty."));

    public static async Task<FileTypes> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(BundledPath, temporary); File.Move(temporary, path, overwrite: false); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return Parse(await File.ReadAllTextAsync(path));
    }
}
