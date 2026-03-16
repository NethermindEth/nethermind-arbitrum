// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumUserWasmsRecorder
{
    public Dictionary<ValueHash256, IReadOnlyDictionary<string, byte[]>>? UserWasms { get; private set; }

    public void RecordUserWasm(ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
        UserWasms ??= new();
        UserWasms[moduleHash] = asmMap;
    }
}
