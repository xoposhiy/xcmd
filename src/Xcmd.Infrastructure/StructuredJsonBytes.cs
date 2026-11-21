// @implements StreamingReads, ReadLargeFiles
using System.Text;

namespace Xcmd.Infrastructure;

public sealed class StructuredJsonBytes
{
    private const int Block = 2048;
    private readonly string source;
    private readonly Encoding encoding;
    private readonly long[] offsets;

    public StructuredJsonBytes(string source, Encoding encoding, int bomLength, CancellationToken token)
    {
        this.source = source;
        this.encoding = encoding;
        offsets = new long[source.Length / Block + 1];
        offsets[0] = bomLength;
        var start = 0;
        for (var block = 1; block < offsets.Length; block++)
        {
            token.ThrowIfCancellationRequested();
            var end = CharacterBoundary(block * Block);
            offsets[block] = offsets[block - 1] + encoding.GetByteCount(source.AsSpan(start, end - start));
            start = end;
        }
        Length = offsets[^1] + encoding.GetByteCount(source.AsSpan(start));
    }

    public long Length { get; }

    public long At(int character)
    {
        character = CharacterBoundary(Math.Clamp(character, 0, source.Length));
        var block = character / Block;
        var start = CharacterBoundary(block * Block);
        return offsets[block] + encoding.GetByteCount(source.AsSpan(start, character - start));
    }

    public int CharacterAt(long offset)
    {
        var block = Array.BinarySearch(offsets, offset);
        if (block >= 0) return CharacterBoundary(block * Block);
        block = Math.Max(0, ~block - 1);
        var low = CharacterBoundary(block * Block);
        var high = block + 1 < offsets.Length ? CharacterBoundary((block + 1) * Block) : source.Length;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (At(middle) <= offset) low = middle;
            else high = middle - 1;
        }
        return CharacterBoundary(low);
    }

    private int CharacterBoundary(int character) => character > 0 && character < source.Length && char.IsLowSurrogate(source[character])
        ? character - 1 : character;
}
