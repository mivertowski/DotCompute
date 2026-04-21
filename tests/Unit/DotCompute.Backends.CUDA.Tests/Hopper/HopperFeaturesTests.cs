// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Backends.CUDA.Hopper;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Hopper;

/// <summary>
/// Unit tests for <see cref="HopperFeatures"/> capability gates — no GPU required.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HopperFeatures")]
public sealed class HopperFeaturesTests
{
    [Theory]
    [InlineData(9, 0, true)]   // Hopper H100
    [InlineData(9, 1, true)]
    [InlineData(10, 0, true)]  // Blackwell
    [InlineData(8, 9, false)]  // Ada Lovelace
    [InlineData(8, 0, false)]  // Ampere
    [InlineData(7, 5, false)]  // Turing
    [InlineData(5, 0, false)]  // Maxwell
    public void IsClusterLaunchSupported_MatchesHopperThreshold(int major, int minor, bool expected)
    {
        HopperFeatures.IsClusterLaunchSupported(major, minor).Should().Be(expected);
    }

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(10, 0, true)]
    [InlineData(8, 9, false)]
    [InlineData(8, 6, false)]
    public void IsTmaSupported_MatchesHopperThreshold(int major, int minor, bool expected)
    {
        HopperFeatures.IsTmaSupported(major, minor).Should().Be(expected);
    }

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(8, 0, false)]
    public void IsDsmemSupported_MatchesHopperThreshold(int major, int minor, bool expected)
    {
        HopperFeatures.IsDsmemSupported(major, minor).Should().Be(expected);
    }

    [Theory]
    [InlineData(6, 0, true)]   // Pascal
    [InlineData(7, 0, true)]   // Volta
    [InlineData(8, 0, true)]   // Ampere
    [InlineData(9, 0, true)]   // Hopper
    [InlineData(5, 2, false)]  // Maxwell (no mem pools)
    [InlineData(3, 5, false)]  // Kepler
    public void IsAsyncMemPoolSupported_MatchesPascalThreshold(int major, int minor, bool expected)
    {
        HopperFeatures.IsAsyncMemPoolSupported(major, minor).Should().Be(expected);
    }

    [Fact]
    public void EnsureClusterLaunchSupported_Hopper_DoesNotThrow()
    {
        var act = () => HopperFeatures.EnsureClusterLaunchSupported(9, 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureClusterLaunchSupported_Pre9_Throws()
    {
        var act = () => HopperFeatures.EnsureClusterLaunchSupported(8, 9);
        act.Should().Throw<NotSupportedException>().WithMessage("*Cluster*9.0*");
    }

    [Fact]
    public void EnsureTmaSupported_Pre9_Throws()
    {
        var act = () => HopperFeatures.EnsureTmaSupported(8, 0);
        act.Should().Throw<NotSupportedException>().WithMessage("*Tensor Memory Accelerator*");
    }

    [Fact]
    public void EnsureDsmemSupported_Pre9_Throws()
    {
        var act = () => HopperFeatures.EnsureDsmemSupported(7, 5);
        act.Should().Throw<NotSupportedException>().WithMessage("*DSMEM*");
    }

    [Fact]
    public void EnsureAsyncMemPoolSupported_Pascal_DoesNotThrow()
    {
        var act = () => HopperFeatures.EnsureAsyncMemPoolSupported(6, 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureAsyncMemPoolSupported_Maxwell_Throws()
    {
        var act = () => HopperFeatures.EnsureAsyncMemPoolSupported(5, 2);
        act.Should().Throw<NotSupportedException>().WithMessage("*memory pool*");
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        HopperFeatures.HopperMajor.Should().Be(9);
        HopperFeatures.AsyncMemPoolMajor.Should().Be(6);
        HopperFeatures.MaxPortableClusterSize.Should().Be(8);
        HopperFeatures.MaxBlackwellClusterSize.Should().Be(16);
    }
}
