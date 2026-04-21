// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Backends.CUDA.Hopper;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Hopper;

/// <summary>
/// Unit tests for <see cref="DsmemConfig"/> — no GPU required.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HopperDsmem")]
public sealed class DsmemConfigTests
{
    [Fact]
    public void Create_PopulatesFields()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 16 * 1024, clusterBlockCount: 4);

        cfg.PerBlockBytes.Should().Be(16u * 1024);
        cfg.ClusterBlockCount.Should().Be(4u);
        cfg.TotalDsmemBytes.Should().Be(64u * 1024);
    }

    [Fact]
    public void Validate_DefaultHopperLimit_DoesNotThrow()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 16 * 1024, clusterBlockCount: 4);
        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AtMaxPortableClusterSize_DoesNotThrow()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 8 * 1024, clusterBlockCount: 8);
        var act = () => cfg.Validate();
        act.Should().NotThrow();
        cfg.TotalDsmemBytes.Should().Be(64u * 1024);
    }

    [Fact]
    public void Validate_ClusterExceedsPortableMax_Throws()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 1024, clusterBlockCount: 9);
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*portable*");
    }

    [Fact]
    public void Validate_ZeroPerBlockBytes_Throws()
    {
        var cfg = new DsmemConfig { PerBlockBytes = 0, ClusterBlockCount = 4 };
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*PerBlockBytes*");
    }

    [Fact]
    public void Validate_ZeroClusterBlockCount_Throws()
    {
        var cfg = new DsmemConfig { PerBlockBytes = 4096, ClusterBlockCount = 0 };
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*ClusterBlockCount*");
    }

    [Fact]
    public void Validate_PerBlockExceedsDeviceLimit_Throws()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 300 * 1024, clusterBlockCount: 2);
        var act = () => cfg.Validate(maxSharedMemoryPerBlock: 228 * 1024);
        act.Should().Throw<ArgumentException>().WithMessage("*MaxSharedMemoryPerBlockOptin*");
    }

    [Fact]
    public void Validate_PerBlockWithLargerDeviceLimit_DoesNotThrow()
    {
        // Suppose a future device with 256 KiB / block.
        var cfg = DsmemConfig.Create(perBlockBytes: 240 * 1024, clusterBlockCount: 2);
        var act = () => cfg.Validate(maxSharedMemoryPerBlock: 256 * 1024);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_TotalExceedsCap_Throws()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 16 * 1024, clusterBlockCount: 8);
        // cap at 64 KiB — total = 128 KiB.
        var act = () => cfg.Validate(maxTotalDsmemBytes: 64UL * 1024);
        act.Should().Throw<ArgumentException>().WithMessage("*TotalDsmemBytes*");
    }

    [Fact]
    public void Validate_TotalUnderCap_DoesNotThrow()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 4 * 1024, clusterBlockCount: 4);
        // cap = 1 MiB; total = 16 KiB.
        var act = () => cfg.Validate(maxTotalDsmemBytes: 1024UL * 1024);
        act.Should().NotThrow();
    }

    [Fact]
    public void TotalDsmemBytes_MultiplicationIsCorrect()
    {
        var cfg = DsmemConfig.Create(perBlockBytes: 12345, clusterBlockCount: 4);
        cfg.TotalDsmemBytes.Should().Be(12345u * 4u);
    }
}
