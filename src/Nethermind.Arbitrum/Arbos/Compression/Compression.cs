namespace Nethermind.Arbitrum.Arbos.Compression;

public static class Utils
{
    public enum Dictionary
    {
        EmptyDictionary,
        StylusProgramDictionary,
    }

    public const ulong LevelWell = 11; // arbcompress.LEVEL_WELL
    public const int WindowSize = 22; // arbcompress.WINDOW_SIZE, BROTLI_DEFAULT_WINDOW
}
