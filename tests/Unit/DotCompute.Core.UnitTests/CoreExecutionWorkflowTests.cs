using DotCompute.Core.Execution;
using DotCompute.Core.Kernels;
using DotCompute.Abstractions;
using Xunit;
using NSubstitute;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Tests for Core execution workflow and caching components.
/// Note: Cache functionality tests removed due to sealed ManagedCompiledKernel class.
/// These would be better suited for integration tests with real compiled kernels.
/// </summary>
public class CoreExecutionWorkflowTests
{
    [Fact]
    public async Task CompiledKernelCache_Creation_ShouldInitializeEmpty()
    {
        // Act
        await using var cache = new CompiledKernelCache();
        
        // Assert
        Assert.Equal(0, cache.Count);
        Assert.True(cache.IsEmpty);
    }
    
    [Fact]
    public async Task CompiledKernelCache_GetCachedDeviceIds_EmptyCache_ShouldReturnEmpty()
    {
        // Arrange
        await using var cache = new CompiledKernelCache();
        
        // Act
        var deviceIds = cache.GetCachedDeviceIds();
        
        // Assert
        Assert.Empty(deviceIds);
    }
    
    [Fact]
    public async Task CompiledKernelCache_GetStatistics_EmptyCache_ShouldReturnZeros()
    {
        // Arrange
        await using var cache = new CompiledKernelCache();
        
        // Act
        var stats = cache.GetStatistics();
        
        // Assert
        Assert.Equal(0, stats.TotalKernels);
        Assert.Equal(0, stats.TotalAccessCount);
        Assert.Empty(stats.DeviceTypes);
        Assert.Null(stats.MostAccessedKernel);
        Assert.Null(stats.LeastAccessedKernel);
    }
    
    [Fact]
    public async Task CompiledKernelCache_GetStaleKernels_EmptyCache_ShouldReturnEmpty()
    {
        // Arrange
        await using var cache = new CompiledKernelCache();
        
        // Act
        var staleKernels = cache.GetStaleKernels(TimeSpan.FromMinutes(5));
        
        // Assert
        Assert.Empty(staleKernels);
    }
    
    [Fact]
    public async Task CompiledKernelCache_RemoveStaleKernelsAsync_EmptyCache_ShouldReturnZero()
    {
        // Arrange
        await using var cache = new CompiledKernelCache();
        
        // Act
        var removedCount = await cache.RemoveStaleKernelsAsync(TimeSpan.FromMinutes(5));
        
        // Assert
        Assert.Equal(0, removedCount);
    }
    
    [Fact]
    public async Task CompiledKernelCache_EvictLeastRecentlyUsedAsync_EmptyCache_Should_NotThrow()
    {
        // Arrange
        await using var cache = new CompiledKernelCache();
        
        // Act & Assert - Should not throw
        await cache.EvictLeastRecentlyUsedAsync(10);
    }

    [Fact]
    public void KernelMetadata_Creation_ShouldSetRequiredProperties()
    {
        // Arrange & Act
        var metadata = new KernelMetadata
        {
            DeviceId = "gpu-0",
            DeviceType = "GPU", 
            KernelName = "test_kernel",
            CachedAt = DateTimeOffset.UtcNow
        };
        
        // Assert
        Assert.Equal("gpu-0", metadata.DeviceId);
        Assert.Equal("GPU", metadata.DeviceType);
        Assert.Equal("test_kernel", metadata.KernelName);
        Assert.Equal(0, metadata.AccessCount);
        Assert.True(metadata.CachedAt <= DateTimeOffset.UtcNow);
    }
    
    [Fact]
    public void KernelMetadata_AccessCount_ShouldBeSettable()
    {
        // Arrange
        var metadata = new KernelMetadata
        {
            DeviceId = "gpu-0",
            DeviceType = "GPU", 
            KernelName = "test_kernel",
            CachedAt = DateTimeOffset.UtcNow
        };
        
        // Act
        metadata.AccessCount = 42;
        metadata.LastAccessed = DateTimeOffset.UtcNow.AddMinutes(10);
        
        // Assert
        Assert.Equal(42, metadata.AccessCount);
        Assert.True(metadata.LastAccessed > metadata.CachedAt);
    }

    [Fact]
    public void KernelCacheStatistics_Creation_ShouldInitializeCorrectly()
    {
        // Act
        var stats = new DotCompute.Core.Execution.KernelCacheStatistics();
        
        // Assert
        Assert.Equal(0, stats.TotalKernels);
        Assert.Equal(0, stats.TotalAccessCount);
        Assert.NotNull(stats.DeviceTypes);
        Assert.Empty(stats.DeviceTypes);
        Assert.Null(stats.MostAccessedKernel);
        Assert.Null(stats.LeastAccessedKernel);
        Assert.Equal(default, stats.OldestCacheTime);
        Assert.Equal(default, stats.NewestCacheTime);
    }
    
    [Fact]
    public void KernelCacheStatistics_SetProperties_ShouldWork()
    {
        // Arrange
        var stats = new DotCompute.Core.Execution.KernelCacheStatistics();
        var now = DateTimeOffset.UtcNow;
        
        // Act
        stats.TotalKernels = 5;
        stats.TotalAccessCount = 100;
        stats.DeviceTypes["GPU"] = 3;
        stats.DeviceTypes["CPU"] = 2;
        stats.MostAccessedKernel = "vector_add";
        stats.LeastAccessedKernel = "test_kernel";
        stats.OldestCacheTime = now.AddHours(-2);
        stats.NewestCacheTime = now;
        
        // Assert
        Assert.Equal(5, stats.TotalKernels);
        Assert.Equal(100, stats.TotalAccessCount);
        Assert.Equal(2, stats.DeviceTypes.Count);
        Assert.Equal(3, stats.DeviceTypes["GPU"]);
        Assert.Equal(2, stats.DeviceTypes["CPU"]);
        Assert.Equal("vector_add", stats.MostAccessedKernel);
        Assert.Equal("test_kernel", stats.LeastAccessedKernel);
        Assert.Equal(now.AddHours(-2), stats.OldestCacheTime);
        Assert.Equal(now, stats.NewestCacheTime);
    }

    [Fact]
    public async Task GlobalKernelCacheManager_Creation_ShouldInitializeCorrectly()
    {
        // Act
        await using var manager = new GlobalKernelCacheManager();
        var stats = manager.GetStatistics();
        
        // Assert
        Assert.Equal(0, stats.TotalCaches);
        Assert.Equal(0, stats.TotalKernels);
        Assert.Empty(stats.KernelNames);
        Assert.Equal(0, stats.CleanupCount);
    }
    
    [Fact]
    public async Task GlobalKernelCacheManager_GetOrCreateCache_ShouldCreateCache()
    {
        // Arrange
        await using var manager = new GlobalKernelCacheManager();
        
        // Act
        var cache1 = manager.GetOrCreateCache("test_kernel");
        var cache2 = manager.GetOrCreateCache("test_kernel");
        var cache3 = manager.GetOrCreateCache("other_kernel");
        
        // Assert
        Assert.NotNull(cache1);
        Assert.Same(cache1, cache2); // Should return same instance for same kernel name
        Assert.NotSame(cache1, cache3); // Should return different instance for different kernel
        
        var stats = manager.GetStatistics();
        Assert.Equal(2, stats.TotalCaches);
        Assert.Contains("test_kernel", stats.KernelNames);
        Assert.Contains("other_kernel", stats.KernelNames);
    }
    
    [Fact]
    public async Task GlobalKernelCacheManager_ClearAllAsync_ShouldClearCaches()
    {
        // Arrange
        await using var manager = new GlobalKernelCacheManager();
        manager.GetOrCreateCache("test_kernel");
        manager.GetOrCreateCache("other_kernel");
        
        // Act
        await manager.ClearAllAsync();
        
        // Assert
        var stats = manager.GetStatistics();
        Assert.Equal(0, stats.TotalCaches);
        Assert.Equal(0, stats.TotalKernels);
        Assert.Empty(stats.KernelNames);
    }

    [Fact]
    public void GlobalCacheStatistics_Creation_ShouldInitializeCorrectly()
    {
        // Act
        var stats = new GlobalCacheStatistics();
        
        // Assert
        Assert.Equal(0, stats.TotalCaches);
        Assert.Equal(0, stats.TotalKernels);
        Assert.Empty(stats.KernelNames);
        Assert.Equal(default, stats.LastCleanupTime);
        Assert.Equal(0, stats.CleanupCount);
    }
    
    [Fact]
    public void GlobalCacheStatistics_SetProperties_ShouldWork()
    {
        // Arrange
        var stats = new GlobalCacheStatistics();
        var kernelNames = new[] { "kernel1", "kernel2", "kernel3" };
        var cleanupTime = DateTimeOffset.UtcNow;
        
        // Act
        stats.TotalCaches = 3;
        stats.TotalKernels = 15;
        stats.KernelNames = kernelNames;
        stats.LastCleanupTime = cleanupTime;
        stats.CleanupCount = 5;
        
        // Assert
        Assert.Equal(3, stats.TotalCaches);
        Assert.Equal(15, stats.TotalKernels);
        Assert.Equal(kernelNames, stats.KernelNames);
        Assert.Equal(cleanupTime, stats.LastCleanupTime);
        Assert.Equal(5, stats.CleanupCount);
    }

    [Theory]
    [InlineData("vector_add")]
    [InlineData("matrix_multiply")]
    [InlineData("convolution_2d")]
    [InlineData("fft_transform")]
    [InlineData("reduction_sum")]
    public async Task GlobalKernelCacheManager_DifferentKernelNames_ShouldCreateSeparateCaches(string kernelName)
    {
        // Arrange
        await using var manager = new GlobalKernelCacheManager();
        
        // Act
        var cache = manager.GetOrCreateCache(kernelName);
        
        // Assert
        Assert.NotNull(cache);
        var stats = manager.GetStatistics();
        Assert.Contains(kernelName, stats.KernelNames);
    }
}
}
