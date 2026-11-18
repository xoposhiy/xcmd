// @implements ReadWorkingFileDiff
using System.Globalization;
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed record ViewerGitFile(ViewerGitStatus Status, string Root = "", string Path = "", string Head = "", string? PreviousPath = null)
{
    public bool IsTracked => Status is ViewerGitStatus.Unchanged or ViewerGitStatus.Changed or ViewerGitStatus.Staged
        or ViewerGitStatus.PartiallyStaged or ViewerGitStatus.Conflicted;
}

public sealed record ViewerGitComparison(IReadOnlyList<ViewerDiffRow> Rows, long[] WorkingLineOffsets, long WorkingLength, bool EndsWithNewline,
    string HeadText, string IndexText, string WorkingText);

public sealed class ViewerGitDiff
{
    private readonly GitStatusReader repositories = new();

    public async Task<ViewerGitFile> StatusAsync(string path, CancellationToken token)
    {
        var root = await repositories.FindRootAsync(Path.GetDirectoryName(path)!, token).ConfigureAwait(false);
        if (root == null) return new(ViewerGitStatus.OutsideRepository);

        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        var output = await GitHistoryProcess.TextAsync(root,
            ["status", "--porcelain=v2", "--branch", "-z", "--untracked-files=all", "--ignored=traditional", "--", ":(literal)" + relative],
            token).ConfigureAwait(false);
        var records = output.Split('\0');
        const string headPrefix = "# branch.oid ";
        var head = records.FirstOrDefault(record => record.StartsWith(headPrefix, StringComparison.Ordinal))?[headPrefix.Length..];
        var (changes, ignored) = GitStatusPaths.Parse(records);
        var entry = changes.FirstOrDefault(change => change.Unstaged != GitChange.Untracked) ?? changes.FirstOrDefault();
        var status = StatusOf(entry, ignored.Length > 0);
        var previous = entry?.OriginalPath;
        if (entry?.Staged == GitChange.Added && head is not null and not "(initial)")
            previous = await FindRenameAsync(root, relative, token).ConfigureAwait(false);
        return new(status, root, relative, head is "(initial)" ? "" : head ?? "", previous);
    }

    private static ViewerGitStatus StatusOf(GitPathStatus? entry, bool ignored) => entry switch
    {
        null => ignored ? ViewerGitStatus.Ignored : ViewerGitStatus.Unchanged,
        { Staged: GitChange.Conflict } or { Unstaged: GitChange.Conflict } => ViewerGitStatus.Conflicted,
        { Staged: GitChange.None, Unstaged: GitChange.Untracked } => ViewerGitStatus.NotTracked,
        { Staged: GitChange.None } => ViewerGitStatus.Changed,
        { Unstaged: GitChange.None } => ViewerGitStatus.Staged,
        _ => ViewerGitStatus.PartiallyStaged
    };

    private static async Task<string?> FindRenameAsync(string root, string relative, CancellationToken token)
    {
        var output = await GitHistoryProcess.TextAsync(root, ["diff", "--cached", "--name-status", "-z", "-M", "--diff-filter=R", "--"], token).ConfigureAwait(false);
        var fields = output.Split('\0');
        for (var index = 0; index + 2 < fields.Length; index += 3)
            if (StringComparer.Ordinal.Equals(fields[index + 2], relative)) return fields[index + 1];
        return null;
    }

    public async Task<ViewerGitComparison> CompareAsync(ViewerGitFile file, string workingPath, int codePage, CancellationToken token)
    {
        var conflict = file.Status == ViewerGitStatus.Conflicted;
        var headTask = ReadHeadAsync(file, token);
        var indexTask = conflict ? headTask : ReadIndexAsync(file, token);
        var workingTask = ReadWorkingAsync(workingPath, token);
        await Task.WhenAll(headTask, indexTask, workingTask).ConfigureAwait(false);

        var head = DiffText.Decode(await headTask.ConfigureAwait(false), codePage);
        var index = conflict ? head : DiffText.Decode(await indexTask.ConfigureAwait(false), codePage);
        var bytes = await workingTask.ConfigureAwait(false);
        var working = DiffText.Decode(bytes, codePage, mapOffsets: true);
        var (staged, unstaged) = await CompareTextAsync(head.Text, index.Text, working.Text, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var rows = ViewerDiff.Merge(ViewerDiff.Lines(head.Text), ViewerDiff.Lines(index.Text),
            ViewerDiff.Lines(working.Text), staged, unstaged, conflict);
        return new(rows, working.LineOffsets, bytes.LongLength, working.Text.EndsWith('\n'), head.Text, index.Text, working.Text);
    }

    private static async Task<byte[]> ReadHeadAsync(ViewerGitFile file, CancellationToken token)
    {
        if (file.Head.Length == 0) return [];
        var tree = await GitHistoryProcess.TextAsync(file.Root,
            ["ls-tree", "-z", file.Head, "--", ":(literal)" + (file.PreviousPath ?? file.Path)], token).ConfigureAwait(false);
        var fields = tree.Split([' ', '\t'], 4);
        return fields.Length == 4 && fields[1] == "blob" ? await ReadBlobAsync(file.Root, fields[2], token).ConfigureAwait(false) : [];
    }

    private static async Task<byte[]> ReadIndexAsync(ViewerGitFile file, CancellationToken token)
    {
        var entry = await GitHistoryProcess.TextAsync(file.Root,
            ["ls-files", "--stage", "-z", "--", ":(literal)" + file.Path], token).ConfigureAwait(false);
        var fields = entry.Split([' ', '\t'], 4);
        return fields.Length == 4 && fields[2] == "0" && fields[0] != "160000"
            ? await ReadBlobAsync(file.Root, fields[1], token).ConfigureAwait(false) : [];
    }

    private static async Task<byte[]> ReadBlobAsync(string root, string id, CancellationToken token)
    {
        using var stream = new MemoryStream();
        await GitHistoryProcess.RunAsync(root, ["cat-file", "blob", id], stream, token).ConfigureAwait(false);
        return stream.ToArray();
    }

    private static async Task<byte[]> ReadWorkingAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            FileSlice.PageSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > Array.MaxLength) throw new IOException("This file is too large to compare.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return bytes;
    }

    private static async Task<(ViewerDiffRange[] Staged, ViewerDiffRange[] Unstaged)> CompareTextAsync(
        string head, string index, string working, CancellationToken token)
    {
        if (head == index && index == working) return ([], []);
        using var snapshots = new DiffSnapshots();
        await snapshots.WriteAsync(head, index, working, token).ConfigureAwait(false);
        var staged = head == index ? Task.FromResult<ViewerDiffRange[]>([]) : snapshots.CompareAsync("head", "index", token);
        var unstaged = index == working ? Task.FromResult<ViewerDiffRange[]>([]) : snapshots.CompareAsync("index", "working", token);
        await Task.WhenAll(staged, unstaged).ConfigureAwait(false);
        return (await staged.ConfigureAwait(false), await unstaged.ConfigureAwait(false));
    }

    private sealed class DiffSnapshots : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("xcmd-viewer-diff-");

        public Task WriteAsync(string head, string index, string working, CancellationToken token) => Task.WhenAll(
            File.WriteAllTextAsync(Path.Combine(directory.FullName, "head"), head, token),
            File.WriteAllTextAsync(Path.Combine(directory.FullName, "index"), index, token),
            File.WriteAllTextAsync(Path.Combine(directory.FullName, "working"), working, token));

        public async Task<ViewerDiffRange[]> CompareAsync(string before, string after, CancellationToken token)
        {
            using var output = new DiffRangeStream();
            await GitHistoryProcess.RunAsync(directory.FullName,
                ["diff", "--no-index", "--no-ext-diff", "--no-textconv", "--no-color", "--text", "--unified=0",
                    "--inter-hunk-context=0", "--diff-algorithm=myers", "--", before, after],
                output, token, allowExitOne: true).ConfigureAwait(false);
            return output.Ranges.ToArray();
        }

        public void Dispose() => directory.Delete(recursive: true);
    }

    private sealed class DiffRangeStream : Stream
    {
        private readonly StringBuilder header = new();
        private bool atStart = true;
        public List<ViewerDiffRange> Ranges { get; } = [];

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            foreach (var value in buffer)
            {
                if (value == '\n')
                {
                    if (header.Length > 0) ReadHeader();
                    header.Clear();
                    atStart = true;
                }
                else if ((atStart && value == '@') || (header.Length > 0 && header.Length < 120)) header.Append((char)value);
                if (value != '\n') atStart = false;
            }
        }

        private void ReadHeader()
        {
            var fields = header.ToString().Split(' ', 5);
            if (fields.Length < 4 || fields[0] != "@@" || fields[3] != "@@") return;
            var (before, oldCount) = ParseRange(fields[1]);
            var (after, newCount) = ParseRange(fields[2]);
            Ranges.Add(new(before, oldCount, after, newCount));
        }

        private static (int Start, int Count) ParseRange(string value)
        {
            var fields = value[1..].Split(',', 2);
            var start = int.Parse(fields[0], CultureInfo.InvariantCulture);
            var count = fields.Length == 1 ? 1 : int.Parse(fields[1], CultureInfo.InvariantCulture);
            return (count == 0 ? start : start - 1, count);
        }
    }
}
