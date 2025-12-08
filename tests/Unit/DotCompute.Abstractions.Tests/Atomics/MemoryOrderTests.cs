// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Atomics;

namespace DotCompute.Abstractions.Tests.Atomics;

/// <summary>
/// Unit tests for <see cref="MemoryOrder"/> enum.
/// </summary>
public class MemoryOrderTests
{
    [Fact]
    public void MemoryOrder_HasExpectedValues()
    {
        // Assert - verify enum values are as documented
        ((int)MemoryOrder.Relaxed).Should().Be(0);
        ((int)MemoryOrder.Acquire).Should().Be(1);
        ((int)MemoryOrder.Release).Should().Be(2);
        ((int)MemoryOrder.AcquireRelease).Should().Be(3);
        ((int)MemoryOrder.SequentiallyConsistent).Should().Be(4);
    }

    [Fact]
    public void MemoryOrder_HasFiveValues()
    {
        // Arrange
        var values = Enum.GetValues<MemoryOrder>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(MemoryOrder.Relaxed, "Relaxed")]
    [InlineData(MemoryOrder.Acquire, "Acquire")]
    [InlineData(MemoryOrder.Release, "Release")]
    [InlineData(MemoryOrder.AcquireRelease, "AcquireRelease")]
    [InlineData(MemoryOrder.SequentiallyConsistent, "SequentiallyConsistent")]
    public void MemoryOrder_ToString_ReturnsCorrectName(MemoryOrder order, string expectedName)
    {
        // Act
        var name = order.ToString();

        // Assert
        name.Should().Be(expectedName);
    }

    [Fact]
    public void MemoryOrder_Default_IsRelaxed()
    {
        // Arrange
        var defaultOrder = default(MemoryOrder);

        // Assert
        defaultOrder.Should().Be(MemoryOrder.Relaxed);
    }

    [Theory]
    [InlineData("Relaxed", MemoryOrder.Relaxed)]
    [InlineData("Acquire", MemoryOrder.Acquire)]
    [InlineData("Release", MemoryOrder.Release)]
    [InlineData("AcquireRelease", MemoryOrder.AcquireRelease)]
    [InlineData("SequentiallyConsistent", MemoryOrder.SequentiallyConsistent)]
    public void MemoryOrder_Parse_ReturnsCorrectValue(string name, MemoryOrder expected)
    {
        // Act
        var parsed = Enum.Parse<MemoryOrder>(name);

        // Assert
        parsed.Should().Be(expected);
    }

    [Fact]
    public void MemoryOrder_RelaxedIsLeastStrict()
    {
        // Relaxed has the lowest value, indicating least strict ordering
        ((int)MemoryOrder.Relaxed).Should().BeLessThan((int)MemoryOrder.Acquire);
        ((int)MemoryOrder.Relaxed).Should().BeLessThan((int)MemoryOrder.Release);
        ((int)MemoryOrder.Relaxed).Should().BeLessThan((int)MemoryOrder.AcquireRelease);
        ((int)MemoryOrder.Relaxed).Should().BeLessThan((int)MemoryOrder.SequentiallyConsistent);
    }

    [Fact]
    public void MemoryOrder_SequentiallyConsistentIsMostStrict()
    {
        // SequentiallyConsistent has the highest value, indicating strictest ordering
        ((int)MemoryOrder.SequentiallyConsistent).Should().BeGreaterThan((int)MemoryOrder.Relaxed);
        ((int)MemoryOrder.SequentiallyConsistent).Should().BeGreaterThan((int)MemoryOrder.Acquire);
        ((int)MemoryOrder.SequentiallyConsistent).Should().BeGreaterThan((int)MemoryOrder.Release);
        ((int)MemoryOrder.SequentiallyConsistent).Should().BeGreaterThan((int)MemoryOrder.AcquireRelease);
    }

    [Fact]
    public void MemoryOrder_AcquireReleaseCombinesBoth()
    {
        // AcquireRelease should be stronger than both Acquire and Release individually
        ((int)MemoryOrder.AcquireRelease).Should().BeGreaterThan((int)MemoryOrder.Acquire);
        ((int)MemoryOrder.AcquireRelease).Should().BeGreaterThan((int)MemoryOrder.Release);
    }
}
