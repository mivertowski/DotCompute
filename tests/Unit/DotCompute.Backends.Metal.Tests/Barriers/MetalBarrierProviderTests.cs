// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.Metal.Barriers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Barriers;

/// <summary>
/// Unit tests for Metal barrier provider implementation.
/// </summary>
public sealed class MetalBarrierProviderTests : IDisposable
{
    private readonly MetalBarrierProvider _provider;

    public MetalBarrierProviderTests()
    {
        _provider = new MetalBarrierProvider(NullLogger.Instance);
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CreateBarrier_ThreadBlock_Success()
    {
        // Arrange
        var scope = BarrierScope.ThreadBlock;
        var capacity = 256;

        // Act
        using var barrier = _provider.CreateBarrier(scope, capacity);

        // Assert
        Assert.NotNull(barrier);
        Assert.Equal(scope, barrier.Scope);
        Assert.Equal(capacity, barrier.Capacity);
        Assert.True(barrier.BarrierId > 0);
        Assert.Equal(1, _provider.ActiveBarrierCount);
    }

    [Fact]
    public void CreateBarrier_Warp_Success()
    {
        // Arrange
        var scope = BarrierScope.Warp;
        var capacity = 32;

        // Act
        using var barrier = _provider.CreateBarrier(scope, capacity);

        // Assert
        Assert.NotNull(barrier);
        Assert.Equal(scope, barrier.Scope);
        Assert.Equal(capacity, barrier.Capacity);
        Assert.Equal(1, _provider.ActiveBarrierCount);
    }

    [Fact]
    public void CreateBarrier_Grid_ThrowsNotSupported()
    {
        // Arrange
        var scope = BarrierScope.Grid;
        var capacity = 1024;

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => _provider.CreateBarrier(scope, capacity));
        Assert.Contains("Grid-wide barriers are not supported", ex.Message);
    }

    [Fact]
    public void CreateBarrier_WithName_Success()
    {
        // Arrange
        var scope = BarrierScope.ThreadBlock;
        var capacity = 512;
        var name = "test_barrier";

        // Act
        using var barrier = _provider.CreateBarrier(scope, capacity, name);

        // Assert
        Assert.NotNull(barrier);
        Assert.Equal(1, _provider.ActiveBarrierCount);

        var retrieved = _provider.GetBarrier(name);
        Assert.NotNull(retrieved);
        Assert.Equal(barrier.BarrierId, retrieved.BarrierId);
    }

    [Fact]
    public void CreateBarrier_ExceedsThreadgroupSize_Throws()
    {
        // Arrange
        var scope = BarrierScope.ThreadBlock;
        var capacity = 2048; // Exceeds max threadgroup size of 1024

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _provider.CreateBarrier(scope, capacity));
        Assert.Contains("exceeds maximum threadgroup size", ex.Message);
    }

    [Fact]
    public void CreateBarrier_NegativeCapacity_Throws()
    {
        // Arrange
        var scope = BarrierScope.ThreadBlock;
        var capacity = -1;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _provider.CreateBarrier(scope, capacity));
        Assert.Contains("capacity must be positive", ex.Message);
    }

    [Fact]
    public void CreateBarrier_ZeroCapacity_Throws()
    {
        // Arrange
        var scope = BarrierScope.ThreadBlock;
        var capacity = 0;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _provider.CreateBarrier(scope, capacity));
        Assert.Contains("capacity must be positive", ex.Message);
    }

    [Fact]
    public void GetBarrier_ById_ReturnsCorrectBarrier()
    {
        // Arrange
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var barrierId = barrier.BarrierId;

        // Act
        var retrieved = _provider.GetBarrier(barrierId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(barrierId, retrieved.BarrierId);
    }

    [Fact]
    public void GetBarrier_ByName_ReturnsCorrectBarrier()
    {
        // Arrange
        var name = "named_barrier";
        using var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256, name);

        // Act
        var retrieved = _provider.GetBarrier(name);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(barrier.BarrierId, retrieved.BarrierId);
    }

    [Fact]
    public void GetBarrier_NonExistent_ReturnsNull()
    {
        // Act
        var retrieved = _provider.GetBarrier(999);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void GetBarrier_ByNonExistentName_ReturnsNull()
    {
        // Act
        var retrieved = _provider.GetBarrier("non_existent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void DestroyBarrier_RemovesFromActive()
    {
        // Arrange
        var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        var barrierId = barrier.BarrierId;
        Assert.Equal(1, _provider.ActiveBarrierCount);

        // Act
        _provider.DestroyBarrier(barrierId);

        // Assert
        Assert.Equal(0, _provider.ActiveBarrierCount);
        var retrieved = _provider.GetBarrier(barrierId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void DestroyBarrier_NamedBarrier_RemovesFromBoth()
    {
        // Arrange
        var name = "test_barrier";
        var barrier = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256, name);
        var barrierId = barrier.BarrierId;

        // Act
        _provider.DestroyBarrier(barrierId);

        // Assert
        Assert.Equal(0, _provider.ActiveBarrierCount);
        Assert.Null(_provider.GetBarrier(barrierId));
        Assert.Null(_provider.GetBarrier(name));
    }

    [Fact]
    public void ResetAllBarriers_ResetsAll()
    {
        // Arrange
        using var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        using var barrier2 = _provider.CreateBarrier(BarrierScope.Warp, 32);

        barrier1.Sync();
        barrier2.Sync();

        // Act
        _provider.ResetAllBarriers();

        // Assert - barriers still exist but are reset
        Assert.Equal(2, _provider.ActiveBarrierCount);
        // Note: Cannot directly test reset state without accessing internal implementation
    }

    [Fact]
    public void EnableCooperativeLaunch_ThrowsNotSupported()
    {
        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => _provider.EnableCooperativeLaunch(true));
        Assert.Contains("Cooperative launch is not supported", ex.Message);
    }

    [Fact]
    public void IsCooperativeLaunchEnabled_AlwaysFalse()
    {
        // Act
        var result = _provider.IsCooperativeLaunchEnabled;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetMaxCooperativeGridSize_ReturnsZero()
    {
        // Act
        var result = _provider.GetMaxCooperativeGridSize();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ActiveBarrierCount_TracksCorrectly()
    {
        // Arrange
        Assert.Equal(0, _provider.ActiveBarrierCount);

        // Act - Create barriers
        var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        Assert.Equal(1, _provider.ActiveBarrierCount);

        var barrier2 = _provider.CreateBarrier(BarrierScope.Warp, 32);
        Assert.Equal(2, _provider.ActiveBarrierCount);

        // Act - Destroy one
        _provider.DestroyBarrier(barrier1.BarrierId);
        Assert.Equal(1, _provider.ActiveBarrierCount);

        // Cleanup
        _provider.DestroyBarrier(barrier2.BarrierId);
        Assert.Equal(0, _provider.ActiveBarrierCount);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        using var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        using var barrier2 = _provider.CreateBarrier(BarrierScope.Warp, 32);

        // Act
        var stats = _provider.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.TotalBarriersCreated);
        Assert.Equal(2, stats.ActiveBarriers);
        Assert.Equal(1, stats.ThreadgroupBarriers);
        Assert.Equal(1, stats.SimdgroupBarriers);
    }

    [Fact]
    public void CreateBarrier_CustomConfiguration_RespectsFenceFlags()
    {
        // Arrange
        var config = new MetalBarrierConfiguration
        {
            DefaultThreadgroupFenceFlags = MetalMemoryFenceFlags.Device,
            ValidateBarrierParameters = true
        };

        using var provider = new MetalBarrierProvider(NullLogger.Instance, config);

        // Act
        using var barrier = provider.CreateBarrier(BarrierScope.ThreadBlock, 256);

        // Assert
        Assert.NotNull(barrier);
        // Fence flags are internal but barrier should be created successfully
    }

    [Fact]
    public void CreateBarrier_ValidationDisabled_AllowsLargerCapacity()
    {
        // Arrange
        var config = new MetalBarrierConfiguration
        {
            ValidateBarrierParameters = false // Disable validation
        };

        using var provider = new MetalBarrierProvider(NullLogger.Instance, config);

        // Act - This would normally throw, but validation is disabled
        using var barrier = provider.CreateBarrier(BarrierScope.ThreadBlock, 2048);

        // Assert
        Assert.NotNull(barrier);
        Assert.Equal(2048, barrier.Capacity);
    }

    [Fact]
    public void MultipleBarriers_DifferentIds()
    {
        // Act
        using var barrier1 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        using var barrier2 = _provider.CreateBarrier(BarrierScope.ThreadBlock, 512);
        using var barrier3 = _provider.CreateBarrier(BarrierScope.Warp, 32);

        // Assert
        Assert.NotEqual(barrier1.BarrierId, barrier2.BarrierId);
        Assert.NotEqual(barrier2.BarrierId, barrier3.BarrierId);
        Assert.NotEqual(barrier1.BarrierId, barrier3.BarrierId);
    }

    [Fact]
    public void Dispose_CleansUpAllBarriers()
    {
        // Arrange
        using var provider = new MetalBarrierProvider(NullLogger.Instance);
        provider.CreateBarrier(BarrierScope.ThreadBlock, 256);
        provider.CreateBarrier(BarrierScope.Warp, 32);
        Assert.Equal(2, provider.ActiveBarrierCount);

        // Act
        provider.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => provider.ActiveBarrierCount);
    }

    [Fact]
    public void Dispose_Multiple_NoError()
    {
        // Arrange
        var provider = new MetalBarrierProvider(NullLogger.Instance);

        // Act & Assert
        provider.Dispose();
        provider.Dispose(); // Should not throw
    }
}
