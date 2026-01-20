// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Reflection;
using FluentAssertions;

namespace Nethermind.Arbitrum.Test;

public class TestNamingConventionTests
{
    /// <summary>
    /// Validates that all test methods follow the naming convention:
    /// SystemUnderTest_StateUnderTest_ExpectedBehavior
    /// Example: ArbInfo_GetVersion_ReturnsCorrectVersion
    /// </summary>
    [Test]
    public void AllTests_Always_FollowNamingConvention()
    {
        Assembly testAssembly = typeof(TestNamingConventionTests).Assembly;

        List<(string ClassName, string MethodName)> invalidTests = testAssembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
.Where(method => (method.GetCustomAttribute<TestAttribute>() is not null
                          || method.GetCustomAttributes<TestCaseAttribute>().Any()) && !IsValidTestName(method.Name)).Select(method => (method.DeclaringType!.Name, method.Name))
            .ToList();

        invalidTests.Should().BeEmpty(
            "all test methods must follow naming convention: SystemUnderTest_StateUnderTest_ExpectedBehavior\n" +
            $"Invalid tests:\n{string.Join('\n', invalidTests.Select(t => $"  - {t.ClassName}.{t.MethodName}"))}");
    }

    private static bool IsValidTestName(string methodName)
    {
        string[] parts = methodName.Split('_');

        // Must have at least 3 parts: SystemUnderTest_StateUnderTest_ExpectedBehavior
        if (parts.Length < 3)
            return false;

        // Each part must start with an uppercase letter or digit (for cases like "3Nodes")
        return parts.All(part =>
            !string.IsNullOrEmpty(part) &&
            (char.IsUpper(part[0]) || char.IsDigit(part[0])));
    }
}
