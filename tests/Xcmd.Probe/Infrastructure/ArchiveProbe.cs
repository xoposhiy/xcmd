// @tests BrowseArchives, InspectArchives, ExtractEntries, ArchiveAccess
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace Xcmd.Probe.Infrastructure;

public static class ArchiveProbe
{
    public static void AcceptReplace(ProbeContext context)
    {
        var dialog = context.Window.OperationDialog!;
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<Avalonia.Controls.Button>().Single(button => button.Name == "ConflictReplace").Focus();
        context.PressKey(Avalonia.Input.Key.Enter, target: dialog);
    }

    public static void Gzip(string path)
    {
        using var file = File.Create(path);
        using var output = new GZipStream(file, CompressionMode.Compress);
        output.Write(Encoding.UTF8.GetBytes("Compressed text"));
    }

    public static void EmptyTar(string path)
    {
        using var output = File.Create(path);
        output.Write(new byte[1024]);
    }

    public static async Task ViewFirstFileAsync(ProbeContext context)
    {
        var file = await FindFileAsync(context.Window.Left.Session.FileSystem, context.Window.Left.Session.DirectoryPath)
            ?? throw new InvalidOperationException("The archive sample has no files.");
        await context.Window.Left.LoadAsync(Xcmd.Core.ArchivePath.Parent(file.FullPath)!);
        await context.SelectAsync(file.Name);
        await context.KeyAsync(Avalonia.Input.Key.F3);
    }

    private static async Task<Xcmd.Core.FileEntry?> FindFileAsync(Xcmd.Core.IFileSystem files, string directory)
    {
        var entries = await files.ReadDirectoryAsync(directory, CancellationToken.None);
        if (entries.FirstOrDefault(entry => !entry.IsDirectory && !entry.IsLink) is { } file) return file;
        foreach (var folder in entries.Where(entry => entry.IsDirectory && !entry.IsLink))
            if (await FindFileAsync(files, folder.FullPath) is { } child) return child;
        return null;
    }

    public static void CompressedSamples(ProbeContext context)
    {
        (string Name, string Data)[] samples =
        [
            ("sample.txt.bz2", "QlpoOTFBWSZTWVFCB3QAAACVgEAACAAGAtxAIAAxANNNBADESrSogrR3T4u5IpwoSCihA7oA"),
            ("sample.txt.xz", "/Td6WFoAAATm1rRGAgAhARYAAAB0L+WjAQAOQ29tcHJlc3NlZCB0ZXh0AABt6XLlspxPJgABJw/fGvxqH7bzfQEAAAAABFla"),
            ("sample.tar.bz2", "QlpoOTFBWSZTWdOetAIAAHN/gMqACABAAeWACABAAGcH3kAICCAAdRBT0QDygGTR6m1BJQmjQZAANB9OwahA1yEIl3KodXClAhgMNJwi8T2CM7BDYTXPeMIuYaToiwUuGCXnbMyfEhVcDypV9b+RFB+LuSKcKEhpz1oBAA=="),
            ("sample.tar.xz", "/Td6WFoAAATm1rRGAgAhARYAAAB0L+Wj4Cf/AHJdADMbyc2KqWBCzUmPliJ6isaO+6zWWENzwN74/qy8E89UW5KfGGyXYhrVvlpCy51YsGrMo0nnmXZUqyVpZUfyZRofIwmio+oCY9es8bBszYbb6pQEZBLjrs9Paz9B62wyTxqH5xXRJIUhpVKAbr51MaQAAAAAAHnx7uF7/pVmAAGOAYBQAABOEGpbscRn+wIAAAAABFla")
        ];
        foreach (var sample in samples) File.WriteAllBytes(Path.Combine(context.LeftPath, sample.Name), Convert.FromBase64String(sample.Data));
    }

    public static void Zip(string path, params (string Name, string Text)[] files)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in files)
        {
            var entry = archive.CreateEntry(name);
            using var output = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            output.Write(text);
        }
    }

    public static void Tar(string path, bool gzip)
    {
        using var output = File.Create(path);
        using var compressed = gzip ? new GZipStream(output, CompressionMode.Compress, true) : null;
        using var writer = new TarWriter(compressed ?? (Stream)output, leaveOpen: true);
        using var data = new MemoryStream(Encoding.UTF8.GetBytes("Archived text"));
        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "folder/notes.txt") { DataStream = data });
        writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, "empty/"));
    }

    public static string Sample(ProbeContext context, string name)
    {
        var path = Path.Combine(context.LeftPath, name);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Archives", name), path);
        return path;
    }

    public static void Nested(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var output = archive.CreateEntry("inner.zip").Open();
        using var nested = new ZipArchive(output, ZipArchiveMode.Create);
        using var text = new StreamWriter(nested.CreateEntry("nested.txt").Open());
        text.Write("Nested archive text");
    }
}
