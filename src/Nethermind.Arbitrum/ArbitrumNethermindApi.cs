// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Api;
using static Nethermind.Api.NethermindApi;

// Overrides moved to IOC
public class ArbitrumNethermindApi(Dependencies dependencies) : NethermindApi(dependencies)
{
}
