// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using FluentAssertions.Equivalency;
using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public static class AssertionExtensions
{
    public static EquivalencyAssertionOptions<StylusNativeResult<T>> ForErrorResult<T>(this EquivalencyAssertionOptions<StylusNativeResult<T>> options)
    {
        return options
            .Using<string>(context => context.Subject.Should().StartWith(context.Expectation)).WhenTypeIs<string>()
            .Excluding(t => t.Value);
    }
}
