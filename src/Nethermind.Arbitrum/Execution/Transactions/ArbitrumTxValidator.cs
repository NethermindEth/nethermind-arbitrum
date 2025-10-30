// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.TxPool;

namespace Nethermind.Arbitrum.Execution.Transactions;

public sealed class ArbitrumInternalTxValidator: ITxValidator
{
    public ValidationResult IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
    {
        return ValidationResult.Success;
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
        return ValidationResult.Success;
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
