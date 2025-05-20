using System.Text;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Exceptions;

public class StylusEvmApiNotRegistered(nuint id)
    : InvalidOperationException($"Stylus No registered IEvmApi for id {id}");
public class StylusTargetSetFailedException(string target, byte[] output)
    : InvalidOperationException($"Stylus: failed setting compilation target {target}: {output.ToHexString()}");
    
public class StylusCompilationFailedException(string target, byte[] output)
    : InvalidOperationException($"Stylus: invalid compilation target {target}: {Encoding.UTF8.GetString(output)}");
    
public class StylusWat2WasmFailedException(byte[] output)
    : InvalidOperationException($"Stylus: Failed to compile WAT to WASM: {Encoding.UTF8.GetString(output)}");

public class StylusCallFailedException(byte[] output)
    : InvalidOperationException($"Stylus: Call failed: {Encoding.UTF8.GetString(output)}");