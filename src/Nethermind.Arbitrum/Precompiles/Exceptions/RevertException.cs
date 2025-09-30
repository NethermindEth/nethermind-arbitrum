// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using static Nethermind.Arbitrum.Precompiles.Exceptions.RevertException;

namespace Nethermind.Arbitrum.Precompiles.Exceptions;

public class RevertException(string message, During during = During.PrecompileExecution) : Exception(message)
{
    public During When { get; } = during;

    public enum During
    {
        Encoding,
        Decoding,
        PrecompileExecution
    }
}
