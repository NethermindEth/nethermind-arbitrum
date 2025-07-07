using System.IO.Compression;

namespace Nethermind.Arbitrum.Arbos.Compression;

public class Native
{
    private static int CompressedBufferSizeFor(int length) =>
        length + (length >> 10) * 8 + 64; // actual limit is: length + (length >> 14) * 4 + 6

    public static byte[] Compress(byte[] input, ulong compressionLevel)
    {
        return Compress(input, (int)compressionLevel, Utils.Dictionary.EmptyDictionary);
    }

    private static byte[] Compress(byte[] input, int compressionLevel, Utils.Dictionary dictionary)
    {
        Span<byte> result = new byte[CompressedBufferSizeFor(input.Length)];

        bool successful = BrotliEncoder.TryCompress(input, result, out int bytesWritten, compressionLevel, Utils.WindowSize);
        if (!successful)
        {
            throw new Exception("Failed to compress data");
        }

        return result[0..bytesWritten].ToArray();
    }
}
