// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Backends.CUDA.Hopper;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Hopper;

/// <summary>
/// Unit tests for <see cref="ClusterLaunchConfig"/> — no GPU required.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HopperCluster")]
public sealed class ClusterLaunchConfigTests
{
    [Fact]
    public void Create_ValidInput_PopulatesAllFields()
    {
        var cfg = ClusterLaunchConfig.Create(
            grid: (16u, 1u, 1u),
            block: (256u, 1u, 1u),
            cluster: (4u, 1u, 1u),
            sharedMemBytes: 1024,
            stream: (IntPtr)0x42);

        cfg.GridDimX.Should().Be(16);
        cfg.GridDimY.Should().Be(1);
        cfg.GridDimZ.Should().Be(1);
        cfg.BlockDimX.Should().Be(256);
        cfg.BlockDimY.Should().Be(1);
        cfg.BlockDimZ.Should().Be(1);
        cfg.ClusterDimX.Should().Be(4);
        cfg.ClusterDimY.Should().Be(1);
        cfg.ClusterDimZ.Should().Be(1);
        cfg.SharedMemBytes.Should().Be(1024);
        cfg.Stream.Should().Be((IntPtr)0x42);

        cfg.BlocksPerCluster.Should().Be(4);
        cfg.ClusterCount.Should().Be(4);
    }

    [Fact]
    public void Validate_ValidConfig_DoesNotThrow()
    {
        var cfg = ClusterLaunchConfig.Create(
            grid: (16u, 1u, 1u),
            block: (256u, 1u, 1u),
            cluster: (4u, 1u, 1u));

        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DefaultCluster1x1x1_DoesNotThrow()
    {
        var cfg = ClusterLaunchConfig.Create(
            grid: (32u, 4u, 2u),
            block: (256u, 1u, 1u),
            cluster: (1u, 1u, 1u));

        cfg.BlocksPerCluster.Should().Be(1);
        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MaxPortableClusterSize_DoesNotThrow()
    {
        // 2x2x2 = 8 = max portable.
        var cfg = ClusterLaunchConfig.Create(
            grid: (8u, 4u, 2u),
            block: (128u, 1u, 1u),
            cluster: (2u, 2u, 2u));

        cfg.BlocksPerCluster.Should().Be(8);
        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ClusterTooLarge_ThrowsWithMaxMessage()
    {
        // 4x4x1 = 16 > 8 portable max.
        var cfg = ClusterLaunchConfig.Create(
            grid: (16u, 16u, 1u),
            block: (128u, 1u, 1u),
            cluster: (4u, 4u, 1u));

        cfg.BlocksPerCluster.Should().Be(16);
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*portable*");
    }

    [Theory]
    [InlineData(3u, 1u, 1u)]
    [InlineData(1u, 5u, 1u)]
    [InlineData(1u, 1u, 6u)]
    public void Validate_ClusterDimNotPowerOfTwo_Throws(uint cx, uint cy, uint cz)
    {
        var cfg = ClusterLaunchConfig.Create(
            grid: (cx * 4, cy * 4, cz * 4),
            block: (64u, 1u, 1u),
            cluster: (cx, cy, cz));

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*power of 2*");
    }

    [Fact]
    public void Validate_GridNotDivisibleByCluster_Throws()
    {
        var cfg = ClusterLaunchConfig.Create(
            grid: (15u, 1u, 1u),
            block: (128u, 1u, 1u),
            cluster: (4u, 1u, 1u));

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*divisible*");
    }

    [Theory]
    [InlineData(0u, 1u, 1u)]
    [InlineData(1u, 0u, 1u)]
    [InlineData(1u, 1u, 0u)]
    public void Validate_ZeroClusterDim_Throws(uint cx, uint cy, uint cz)
    {
        var cfg = new ClusterLaunchConfig
        {
            GridDimX = 4,
            GridDimY = 4,
            GridDimZ = 4,
            BlockDimX = 64,
            BlockDimY = 1,
            BlockDimZ = 1,
            ClusterDimX = cx,
            ClusterDimY = cy,
            ClusterDimZ = cz,
        };

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*Cluster dimensions*");
    }

    [Theory]
    [InlineData(0u, 1u, 1u)]
    [InlineData(1u, 0u, 1u)]
    [InlineData(1u, 1u, 0u)]
    public void Validate_ZeroBlockDim_Throws(uint bx, uint by, uint bz)
    {
        var cfg = new ClusterLaunchConfig
        {
            GridDimX = 4,
            GridDimY = 1,
            GridDimZ = 1,
            BlockDimX = bx,
            BlockDimY = by,
            BlockDimZ = bz,
            ClusterDimX = 1,
            ClusterDimY = 1,
            ClusterDimZ = 1,
        };

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*Block dimensions*");
    }

    [Theory]
    [InlineData(0u, 1u, 1u)]
    [InlineData(1u, 0u, 1u)]
    [InlineData(1u, 1u, 0u)]
    public void Validate_ZeroGridDim_Throws(uint gx, uint gy, uint gz)
    {
        var cfg = new ClusterLaunchConfig
        {
            GridDimX = gx,
            GridDimY = gy,
            GridDimZ = gz,
            BlockDimX = 64,
            BlockDimY = 1,
            BlockDimZ = 1,
            ClusterDimX = 1,
            ClusterDimY = 1,
            ClusterDimZ = 1,
        };

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*Grid dimensions*");
    }

    [Fact]
    public void ClusterCount_MultiDim_Computed()
    {
        var cfg = ClusterLaunchConfig.Create(
            grid: (8u, 8u, 2u),
            block: (64u, 1u, 1u),
            cluster: (2u, 2u, 1u));

        // (8/2)*(8/2)*(2/1) = 4*4*2 = 32
        cfg.ClusterCount.Should().Be(32u);
        cfg.Validate();
    }
}
