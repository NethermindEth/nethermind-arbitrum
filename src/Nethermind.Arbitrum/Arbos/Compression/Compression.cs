using System.Buffers;
using System.IO.Compression;

namespace Nethermind.Arbitrum.Arbos.Compression;

public static class BrotliCompression
{
    public enum Dictionary
    {
        EmptyDictionary,
        StylusProgramDictionary,
    }

    public const ulong LevelWell = 11; // arbcompress.LEVEL_WELL
    public const int WindowSize = 22; // arbcompress.WINDOW_SIZE, BROTLI_DEFAULT_WINDOW

    private static int CompressedBufferSizeFor(int length) =>
        length + (length >> 10) * 8 + 64; // actual limit is: length + (length >> 14) * 4 + 6

    public static ReadOnlySpan<byte> Compress(byte[] input, ulong compressionLevel)
    {
        return Compress(input, (int)compressionLevel, Dictionary.EmptyDictionary);
    }

    private static ReadOnlySpan<byte> Compress(byte[] input, int compressionLevel, Dictionary dictionary)
    {
        byte[] result = ArrayPool<byte>.Shared.Rent(CompressedBufferSizeFor(input.Length));

        try
        {
            bool successful = BrotliEncoder.TryCompress(input, result, out int bytesWritten, compressionLevel, WindowSize);
            if (!successful)
            {
                throw new InvalidOperationException("Failed to compress data");
            }

            return result[0..bytesWritten].AsSpan();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(result);
        }
    }
}
