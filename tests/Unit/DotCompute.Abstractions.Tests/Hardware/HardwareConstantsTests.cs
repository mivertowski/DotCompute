// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Hardware;

namespace DotCompute.Abstractions.Tests.Hardware;

/// <summary>
/// Invariant tests for <see cref="HardwareConstants"/>. These guard against
/// accidental changes to values that must remain physically consistent with the
/// hardware they model.
/// </summary>
public class HardwareConstantsTests
{
    [Fact]
    public void CacheLine_ModernBytes_IsPowerOfTwo()
    {
        HardwareConstants.CacheLine.Bytes.Should().Be(128);
        IsPowerOfTwo(HardwareConstants.CacheLine.Bytes).Should().BeTrue();
    }

    [Fact]
    public void CacheLine_Legacy64_IsPowerOfTwo()
    {
        HardwareConstants.CacheLine.Legacy64.Should().Be(64);
        IsPowerOfTwo(HardwareConstants.CacheLine.Legacy64).Should().BeTrue();
    }

    [Fact]
    public void Alignment_GpuIsMultipleOfModernCacheLine()
    {
        // Coalesced GPU access must not straddle a cache line.
        (HardwareConstants.Alignment.Gpu % HardwareConstants.CacheLine.Bytes)
            .Should().Be(0, "GPU alignment must be a multiple of the cache line size");
    }

    [Fact]
    public void Alignment_SimdSizesArePowersOfTwo()
    {
        IsPowerOfTwo(HardwareConstants.Alignment.SimdAvx2).Should().BeTrue();
        IsPowerOfTwo(HardwareConstants.Alignment.SimdAvx512).Should().BeTrue();
    }

    [Fact]
    public void Alignment_Avx512DoubleOfAvx2()
    {
        // 512-bit SIMD lane = 2 x 256-bit SIMD lane.
        HardwareConstants.Alignment.SimdAvx512
            .Should().Be(HardwareConstants.Alignment.SimdAvx2 * 2);
    }

    [Fact]
    public void Alignment_PageSizesArePowersOfTwo()
    {
        IsPowerOfTwo(HardwareConstants.Alignment.Page).Should().BeTrue();
        IsPowerOfTwo(HardwareConstants.Alignment.LargePage).Should().BeTrue();
        (HardwareConstants.Alignment.LargePage % HardwareConstants.Alignment.Page)
            .Should().Be(0);
    }

    [Fact]
    public void Cuda_MaxThreadsPerBlockIs1024()
    {
        // Fixed by CUDA architecture for all compute capabilities 2.0+.
        HardwareConstants.Cuda.MaxThreadsPerBlock.Should().Be(1024);
    }

    [Fact]
    public void Cuda_WarpSizeIs32()
    {
        HardwareConstants.Cuda.WarpSize.Should().Be(32);
    }

    [Fact]
    public void Cuda_MaxThreadsPerBlockIsMultipleOfWarpSize()
    {
        (HardwareConstants.Cuda.MaxThreadsPerBlock % HardwareConstants.Cuda.WarpSize)
            .Should().Be(0);
    }

    [Fact]
    public void CpuCache_HierarchyIsMonotonic()
    {
        HardwareConstants.CpuCache.L1Bytes.Should()
            .BeLessThan(HardwareConstants.CpuCache.L2Bytes);
        HardwareConstants.CpuCache.L2Bytes.Should()
            .BeLessThan(HardwareConstants.CpuCache.L3BytesPerCore);
    }

    [Fact]
    public void CpuCache_SizesArePowersOfTwo()
    {
        IsPowerOfTwo(HardwareConstants.CpuCache.L1Bytes).Should().BeTrue();
        IsPowerOfTwo(HardwareConstants.CpuCache.L2Bytes).Should().BeTrue();
        IsPowerOfTwo(HardwareConstants.CpuCache.L3BytesPerCore).Should().BeTrue();
    }

    [Fact]
    public void MemoryPool_MinClassIsPowerOfTwo()
    {
        IsPowerOfTwo(HardwareConstants.MemoryPool.MinClassBytes).Should().BeTrue();
    }

    [Fact]
    public void MemoryPool_MaxClassIsPowerOfTwo()
    {
        IsPowerOfTwo(HardwareConstants.MemoryPool.MaxClassBytes).Should().BeTrue();
    }

    [Fact]
    public void MemoryPool_MaxClassReachableFromMinWithMultiplier()
    {
        var size = HardwareConstants.MemoryPool.MinClassBytes;
        var steps = 0;
        while (size < HardwareConstants.MemoryPool.MaxClassBytes && steps < 64)
        {
            size *= HardwareConstants.MemoryPool.SizeClassMultiplier;
            steps++;
        }
        size.Should().Be(
            HardwareConstants.MemoryPool.MaxClassBytes,
            "MaxClassBytes must be reachable from MinClassBytes via SizeClassMultiplier");
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}
