// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using DotCompute.Core;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Simple memory tests that verify basic functionality without external dependencies.
/// </summary>
public class SimpleMemoryTests
{
    [Fact]
    public void MemoryFlags_HasCorrectValues()
    {
        // Assert
        MemoryFlags.None.Should().Be(0);
        MemoryFlags.ReadOnly.Should().Be(1);
        MemoryFlags.WriteOnly.Should().Be(2);
        MemoryFlags.HostVisible.Should().Be(4);
        MemoryFlags.Cached.Should().Be(8);
        MemoryFlags.Atomic.Should().Be(16);
    }

    [Fact]
    public void MemoryFlags_SupportsCombination()
    {
        // Act
        var combined = MemoryFlags.ReadOnly | MemoryFlags.Cached;

        // Assert
        combined.Should().Be((MemoryFlags)9);
        combined.HasFlag(MemoryFlags.ReadOnly).Should().BeTrue();
        combined.HasFlag(MemoryFlags.Cached).Should().BeTrue();
        combined.HasFlag(MemoryFlags.WriteOnly).Should().BeFalse();
    }

    [Fact]
    public void MemoryOptions_HasCorrectValues()
    {
        // Assert
        MemoryOptions.None.Should().Be(0);
        MemoryOptions.ReadOnly.Should().Be(1);
        MemoryOptions.WriteOnly.Should().Be(2);
        MemoryOptions.HostVisible.Should().Be(4);
        MemoryOptions.Cached.Should().Be(8);
        MemoryOptions.Atomic.Should().Be(16);
    }

    [Fact]
    public void MemoryOptions_SupportsCombination()
    {
        // Act
        var combined = MemoryOptions.ReadOnly | MemoryOptions.Cached;

        // Assert
        combined.Should().Be((MemoryOptions)9);
        combined.HasFlag(MemoryOptions.ReadOnly).Should().BeTrue();
        combined.HasFlag(MemoryOptions.Cached).Should().BeTrue();
        combined.HasFlag(MemoryOptions.WriteOnly).Should().BeFalse();
    }

    [Fact]
    public void MemoryFlags_And_MemoryOptions_AreCompatible()
    {
        // Both enums should have the same underlying values
        ((int)MemoryFlags.ReadOnly).Should().Be((int)MemoryOptions.ReadOnly);
        ((int)MemoryFlags.WriteOnly).Should().Be((int)MemoryOptions.WriteOnly);
        ((int)MemoryFlags.HostVisible).Should().Be((int)MemoryOptions.HostVisible);
        ((int)MemoryFlags.Cached).Should().Be((int)MemoryOptions.Cached);
        ((int)MemoryFlags.Atomic).Should().Be((int)MemoryOptions.Atomic);
    }

    [Theory]
    [InlineData(MemoryFlags.None, false, false, false, false, false)]
    [InlineData(MemoryFlags.ReadOnly, true, false, false, false, false)]
    [InlineData(MemoryFlags.WriteOnly, false, true, false, false, false)]
    [InlineData(MemoryFlags.HostVisible, false, false, true, false, false)]
    [InlineData(MemoryFlags.Cached, false, false, false, true, false)]
    [InlineData(MemoryFlags.Atomic, false, false, false, false, true)]
    public void MemoryFlags_IndividualFlags_Work(MemoryFlags flags, bool readOnly, bool writeOnly, bool hostVisible, bool cached, bool atomic)
    {
        // Assert
        flags.HasFlag(MemoryFlags.ReadOnly).Should().Be(readOnly);
        flags.HasFlag(MemoryFlags.WriteOnly).Should().Be(writeOnly);
        flags.HasFlag(MemoryFlags.HostVisible).Should().Be(hostVisible);
        flags.HasFlag(MemoryFlags.Cached).Should().Be(cached);
        flags.HasFlag(MemoryFlags.Atomic).Should().Be(atomic);
    }

    [Fact]
    public void MemoryFlags_ComplexCombination_Works()
    {
        // Arrange
        var flags = MemoryFlags.ReadOnly | MemoryFlags.HostVisible | MemoryFlags.Cached;

        // Assert
        flags.HasFlag(MemoryFlags.ReadOnly).Should().BeTrue();
        flags.HasFlag(MemoryFlags.HostVisible).Should().BeTrue();
        flags.HasFlag(MemoryFlags.Cached).Should().BeTrue();
        flags.HasFlag(MemoryFlags.WriteOnly).Should().BeFalse();
        flags.HasFlag(MemoryFlags.Atomic).Should().BeFalse();
    }
}