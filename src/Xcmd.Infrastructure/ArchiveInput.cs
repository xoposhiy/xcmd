namespace Xcmd.Infrastructure;

internal sealed class ArchiveInput : Stream
{
    private readonly FileStream file;
    private readonly long start;
    private static readonly byte[][] signatures = [[0x50, 0x4B, 3, 4], [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], [0x52, 0x61, 0x72, 0x21, 0x1A, 7]];

    public ArchiveInput(string path, CancellationToken token)
    {
        file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try { start = FindStart(file, token); file.Position = start; }
        catch { file.Dispose(); throw; }
    }

    private static long FindStart(FileStream file, CancellationToken token)
    {
        Span<byte> prefix = stackalloc byte[2];
        if (file.Read(prefix) != 2 || prefix[0] != 'M' || prefix[1] != 'Z') return 0;
        file.Position = 0;
        var buffer = new byte[65536 + 8];
        var carry = 0;
        long offset = 0;
        while (offset < Math.Min(file.Length, 32 * 1024 * 1024))
        {
            token.ThrowIfCancellationRequested();
            var count = file.Read(buffer, carry, 65536) + carry;
            if (count == carry) break;
            for (var index = 0; index <= count - 8; index++)
                if (IsHeader(buffer.AsSpan(index, count - index))) return offset + index;
            carry = Math.Min(8, count);
            buffer.AsSpan(count - carry, carry).CopyTo(buffer);
            offset += count - carry;
        }
        return 0;
    }

    private static bool IsHeader(ReadOnlySpan<byte> bytes)
    {
        foreach (var signature in signatures) if (bytes.StartsWith(signature)) return true;
        return false;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => file.Length - start;
    public override long Position { get => file.Position - start; set => file.Position = start + value; }
    public override int Read(byte[] buffer, int offset, int count) => file.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => file.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => file.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => origin switch
    {
        SeekOrigin.Begin => file.Seek(start + offset, SeekOrigin.Begin) - start,
        SeekOrigin.Current => file.Seek(offset, SeekOrigin.Current) - start,
        _ => file.Seek(offset, SeekOrigin.End) - start
    };
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) file.Dispose(); base.Dispose(disposing); }
}
