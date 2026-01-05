// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.TxPool;

namespace Nethermind.Arbitrum.Execution.Transactions;

public sealed class ArbitrumInternalTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return transaction.SenderAddress != ArbosAddresses.ArbosAddress ? "ArbitrumInternal: SenderAddress must be ArbosAddress"
            : transaction.Data.Length < 4 ? "ArbitrumInternal: Data must be more than 3 bytes"
            : ValidationResult.Success;
    }
}

public sealed class ArbitrumSubmitRetryableTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return ValidationResult.Success;
    }
}

public sealed class ArbitrumRetryTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return ValidationResult.Success;
    }
}

public sealed class ArbitrumDepositTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return transaction.To is null ? "ArbitrumDeposit: To cannot be null"
            : ValidationResult.Success;
    }
}

public sealed class ArbitrumUnsignedTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return ValidationResult.Success;
    }
}

public sealed class ArbitrumContractTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return ValidationResult.Success;
    }
}
