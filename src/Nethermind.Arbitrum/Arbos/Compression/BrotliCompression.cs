using System.Buffers;
using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Arbos.Compression;

public static class BrotliCompression
{
    public enum Dictionary
    {
        EmptyDictionary = 0,
        StylusProgramDictionary = 1
    }

    public const ulong LevelWell = 11; // arbcompress.LEVEL_WELL
    public const int WindowSize = 22; // arbcompress.WINDOW_SIZE, BROTLI_DEFAULT_WINDOW

    public static byte[] Compress(ReadOnlySpan<byte> input, ulong compressionLevel)
    {
        return Compress(input, (int)compressionLevel, Dictionary.EmptyDictionary);
    }

    public static byte[] Compress(ReadOnlySpan<byte> input, int compressionLevel, Dictionary dictionary)
    {
        BrotliDictionary brotliDictionary = ToBrotliDictionary(dictionary);

        int maxBufferSize = StylusNative.GetCompressedBufferSize(input.Length);
        byte[] output = ArrayPool<byte>.Shared.Rent(maxBufferSize);

        try
        {
            BrotliStatus status = StylusNative.BrotliCompress(input, output, (uint)compressionLevel, brotliDictionary, out int written);
            if (status != BrotliStatus.Success)
                throw new InvalidOperationException("Failed to compress data, status: " + status);

            return output[..written];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(output);
        }
    }

    public static byte[] Decompress(ReadOnlySpan<byte> input, uint maxSize, Dictionary dictionary = Dictionary.EmptyDictionary)
    {
        if (maxSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxSize), $"maxSize must be <= {int.MaxValue}");

        BrotliDictionary brotliDictionary = ToBrotliDictionary(dictionary);
        byte[] output = ArrayPool<byte>.Shared.Rent((int)maxSize);

        try
        {
            BrotliStatus status = StylusNative.BrotliDecompress(input, output, brotliDictionary, out int written);
            if (status != BrotliStatus.Success)
                throw new InvalidOperationException("Failed to decompress data, status: " + status);

            return output[..written];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(output);
        }
    }

    private static BrotliDictionary ToBrotliDictionary(Dictionary dictionary)
    {
        BrotliDictionary brotliDictionary = (BrotliDictionary)dictionary;
        return !Enum.IsDefined(brotliDictionary)
            ? throw new ArgumentException($"Unknown Brotli dictionary {dictionary}", nameof(dictionary))
            : brotliDictionary;
    }
}
