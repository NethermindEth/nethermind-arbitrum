namespace Nethermind.Arbitrum.NativeHandler;

public enum RequestType: int
{
    GetBytes32 = 0,
    SetTrieSlots = 1,
    GetTransientBytes32 = 2,
    SetTransientBytes32 = 3,
    ContractCall = 4,
    DelegateCall = 5,
    StaticCall = 6,
    Create1 = 7,
    Create2 = 8,
    EmitLog = 9,
    AccountBalance = 10,
    AccountCode = 11,
    AccountCodeHash = 12,
    AddPages = 13,
    CaptureHostIo = 14,
}

public enum ApiStatus : byte
{
    Success = 0,
    Failure = 1,
    OutOfGas = 2,
    WriteProtection = 3
}

public interface INativeApi: IDisposable
{
    /// <summary>
    /// Handles a specific Stylus request.
    /// </summary>
    /// <param name="requestType">Type of request.</param>
    /// <param name="input">Input byte array encoded as per expected format for the request.</param>
    /// <returns>
    /// Tuple with:
    /// - first: result bytes (primary return data),
    /// - second: raw data (auxiliary),
    /// - third: gas cost incurred.
    /// </returns>
    (byte[] result, byte[] rawData, ulong gasCost) Handle(RequestType requestType, byte[] input);
    GoSliceData AllocateGoSlice(byte[]? bytes);
}





