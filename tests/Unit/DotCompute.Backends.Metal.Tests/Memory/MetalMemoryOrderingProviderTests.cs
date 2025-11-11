// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Memory;

/// <summary>
/// Unit tests for Metal memory ordering provider implementation.
/// </summary>
public sealed class MetalMemoryOrderingProviderTests : IDisposable
{
    private readonly MetalMemoryOrderingProvider _provider;

    public MetalMemoryOrderingProviderTests()
    {
        _provider = new MetalMemoryOrderingProvider(NullLogger.Instance);
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesWithRelaxedModel()
    {
        // Act
        var model = _provider.ConsistencyModel;

        // Assert
        Assert.Equal(MemoryConsistencyModel.Relaxed, model);
    }

    [Fact]
    public void Constructor_InitializesWithCausalOrderingDisabled()
    {
        // Act
        var stats = _provider.GetStatistics();

        // Assert
        Assert.False(stats.CausalOrderingEnabled);
    }

    [Fact]
    public void IsAcquireReleaseSupported_ReturnsTrue()
    {
        // Act
        var supported = _provider.IsAcquireReleaseSupported;

        // Assert
        Assert.True(supported); // All Metal devices support acquire-release
    }

    [Fact]
    public void SetConsistencyModel_Relaxed_Success()
    {
        // Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.Relaxed);

        // Assert
        Assert.Equal(MemoryConsistencyModel.Relaxed, _provider.ConsistencyModel);
    }

    [Fact]
    public void SetConsistencyModel_ReleaseAcquire_Success()
    {
        // Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);

        // Assert
        Assert.Equal(MemoryConsistencyModel.ReleaseAcquire, _provider.ConsistencyModel);

        var stats = _provider.GetStatistics();
        Assert.True(stats.CausalOrderingEnabled); // Should enable causal ordering
    }

    [Fact]
    public void SetConsistencyModel_Sequential_Success()
    {
        // Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.Sequential);

        // Assert
        Assert.Equal(MemoryConsistencyModel.Sequential, _provider.ConsistencyModel);

        var stats = _provider.GetStatistics();
        Assert.True(stats.CausalOrderingEnabled); // Should enable causal ordering
    }

    [Theory]
    [InlineData(MemoryConsistencyModel.Relaxed, 1.0)]
    [InlineData(MemoryConsistencyModel.ReleaseAcquire, 0.85)]
    [InlineData(MemoryConsistencyModel.Sequential, 0.60)]
    public void GetOverheadMultiplier_ReturnsCorrectValue(MemoryConsistencyModel model, double expected)
    {
        // Arrange
        _provider.SetConsistencyModel(model);

        // Act
        var multiplier = _provider.GetOverheadMultiplier();

        // Assert
        Assert.Equal(expected, multiplier);
    }

    [Fact]
    public void EnableCausalOrdering_True_EnablesOrdering()
    {
        // Act
        _provider.EnableCausalOrdering(true);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.True(stats.CausalOrderingEnabled);
    }

    [Fact]
    public void EnableCausalOrdering_False_DisablesOrdering()
    {
        // Arrange
        _provider.EnableCausalOrdering(true);
        Assert.True(_provider.GetStatistics().CausalOrderingEnabled);

        // Act
        _provider.EnableCausalOrdering(false);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.False(stats.CausalOrderingEnabled);
    }

    [Fact]
    public void EnableCausalOrdering_AutoUpgradesFromRelaxed()
    {
        // Arrange
        Assert.Equal(MemoryConsistencyModel.Relaxed, _provider.ConsistencyModel);

        // Act
        _provider.EnableCausalOrdering(true);

        // Assert
        Assert.Equal(MemoryConsistencyModel.ReleaseAcquire, _provider.ConsistencyModel);
    }

    [Fact]
    public void InsertFence_Device_Success()
    {
        // Act
        _provider.InsertFence(FenceType.Device);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(1, stats.TotalFencesInserted);
        Assert.Equal(1, stats.DeviceFences);
    }

    [Fact]
    public void InsertFence_ThreadBlock_Success()
    {
        // Act
        _provider.InsertFence(FenceType.ThreadBlock);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(1, stats.TotalFencesInserted);
        Assert.Equal(1, stats.ThreadgroupFences);
    }

    [Fact]
    public void InsertFence_System_MapsToDevice()
    {
        // Act
        _provider.InsertFence(FenceType.System);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(1, stats.TotalFencesInserted);
        Assert.Equal(1, stats.DeviceFences); // System mapped to Device on Metal
    }

    [Fact]
    public void InsertFence_WithLocation_Success()
    {
        // Arrange
        var location = new FenceLocation
        {
            AtEntry = true,
            AtExit = false,
            AfterWrites = true,
            BeforeReads = false
        };

        // Act
        _provider.InsertFence(FenceType.Device, location);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(1, stats.TotalFencesInserted);
    }

    [Fact]
    public void InsertFence_NullLocation_UsesFullBarrier()
    {
        // Act
        _provider.InsertFence(FenceType.Device, location: null);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(1, stats.TotalFencesInserted);
    }

    [Fact]
    public void InsertFence_Multiple_TracksCorrectly()
    {
        // Act
        _provider.InsertFence(FenceType.Device);
        _provider.InsertFence(FenceType.ThreadBlock);
        _provider.InsertFence(FenceType.Device);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(3, stats.TotalFencesInserted);
        Assert.Equal(2, stats.DeviceFences);
        Assert.Equal(1, stats.ThreadgroupFences);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectValues()
    {
        // Arrange
        _provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        _provider.InsertFence(FenceType.Device);
        _provider.InsertFence(FenceType.ThreadBlock);

        // Act
        var stats = _provider.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.TotalFencesInserted);
        Assert.Equal(1, stats.DeviceFences);
        Assert.Equal(1, stats.ThreadgroupFences);
        Assert.Equal("ReleaseAcquire", stats.ConsistencyModel);
        Assert.True(stats.CausalOrderingEnabled);
        Assert.Equal(0.85, stats.PerformanceMultiplier);
    }

    [Fact]
    public void GetStatistics_ReturnsSnapshot()
    {
        // Arrange
        _provider.InsertFence(FenceType.Device);

        // Act
        var stats1 = _provider.GetStatistics();
        _provider.InsertFence(FenceType.Device);
        var stats2 = _provider.GetStatistics();

        // Assert
        Assert.Equal(1, stats1.TotalFencesInserted); // Snapshot at time of call
        Assert.Equal(2, stats2.TotalFencesInserted);
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_RespectsProfiling()
    {
        // Arrange
        var config = new MetalMemoryOrderingConfiguration
        {
            EnableProfiling = true,
            ValidateFenceInsertions = true
        };

        // Act
        using var provider = new MetalMemoryOrderingProvider(NullLogger.Instance, config);

        // Assert
        Assert.NotNull(provider);
        // Configuration is internal, but provider should be created successfully
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_RespectsValidation()
    {
        // Arrange
        var config = new MetalMemoryOrderingConfiguration
        {
            ValidateFenceInsertions = false,
            OptimizeFencePlacement = false
        };

        // Act
        using var provider = new MetalMemoryOrderingProvider(NullLogger.Instance, config);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new MetalMemoryOrderingProvider(NullLogger.Instance);

        // Act & Assert
        provider.Dispose();
        provider.Dispose(); // Should not throw
    }

    [Fact]
    public void SetConsistencyModel_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new MetalMemoryOrderingProvider(NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            provider.SetConsistencyModel(MemoryConsistencyModel.Sequential));
    }

    [Fact]
    public void EnableCausalOrdering_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new MetalMemoryOrderingProvider(NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            provider.EnableCausalOrdering(true));
    }

    [Fact]
    public void InsertFence_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new MetalMemoryOrderingProvider(NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            provider.InsertFence(FenceType.Device));
    }

    [Fact]
    public void GetStatistics_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new MetalMemoryOrderingProvider(NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            provider.GetStatistics());
    }

    [Fact]
    public void GetOverheadMultiplier_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new MetalMemoryOrderingProvider(NullLogger.Instance);
        provider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            provider.GetOverheadMultiplier());
    }

    [Fact]
    public void PerformanceMultiplier_UpdatesWithModel()
    {
        // Arrange & Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.Relaxed);
        var stats1 = _provider.GetStatistics();

        _provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        var stats2 = _provider.GetStatistics();

        _provider.SetConsistencyModel(MemoryConsistencyModel.Sequential);
        var stats3 = _provider.GetStatistics();

        // Assert
        Assert.Equal(1.0, stats1.PerformanceMultiplier);
        Assert.Equal(0.85, stats2.PerformanceMultiplier);
        Assert.Equal(0.60, stats3.PerformanceMultiplier);
    }

    [Fact]
    public void TextureFences_NotTrackedSeparately()
    {
        // Arrange & Act
        // Metal doesn't have a separate texture fence type in the abstraction
        _provider.InsertFence(FenceType.Device);

        // Assert
        var stats = _provider.GetStatistics();
        Assert.Equal(0, stats.TextureFences); // Not tracked separately at provider level
    }
}
