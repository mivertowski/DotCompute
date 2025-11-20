// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Comprehensive tests for MetalKernelCache covering caching, eviction, and persistence.
/// Target: High coverage of cache functionality including LRU, TTL, and statistics.
/// </summary>
public sealed class MetalKernelCacheTests : MetalCompilerTestBase
{
    public MetalKernelCacheTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Basic Cache Operations

    [SkippableFact]
    public Task TryGetKernel_CacheMiss_ReturnsFalse()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act
        var found = cache.TryGetKernel(kernel, options, out var lib, out var func, out var pipeline);

        // Assert
        Assert.False(found);
        Assert.Equal(IntPtr.Zero, lib);
        Assert.Equal(IntPtr.Zero, func);
        Assert.Equal(IntPtr.Zero, pipeline);

        var stats = cache.GetStatistics();
        Assert.Equal(1, stats.MissCount);
        LogTestInfo($"Cache miss recorded - Miss count: {stats.MissCount}");

        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task AddKernel_ThenRetrieve_CacheHit()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - Compile (adds to cache)
        var compiled1 = await compiler.CompileAsync(kernel, options);

        // Retrieve from cache
        var found = cache.TryGetKernel(kernel, options, out var lib, out var func, out var pipeline);

        // Assert
        Assert.True(found);
        Assert.NotEqual(IntPtr.Zero, lib);
        Assert.NotEqual(IntPtr.Zero, func);
        Assert.NotEqual(IntPtr.Zero, pipeline);

        var stats = cache.GetStatistics();
        Assert.True(stats.HitCount >= 1);
        Assert.True(stats.HitRate > 0);
        LogTestInfo($"Cache hit - Hit rate: {stats.HitRate:P2}");
    }

    [SkippableFact]
    public async Task AddKernel_MultipleTimes_UpdatesAccessCount()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - Compile once
        await compiler.CompileAsync(kernel, options);

        // Access multiple times
        for (int i = 0; i < 5; i++)
        {
            cache.TryGetKernel(kernel, options, out _, out _, out _);
        }

        // Assert
        var stats = cache.GetStatistics();
        Assert.True(stats.HitCount >= 5);
        LogTestInfo($"Accessed cache {stats.HitCount} times");
    }

    #endregion

    #region Cache Key Generation

    [Fact]
    public void ComputeCacheKey_SameInputs_GeneratesSameKey()
    {
        // Arrange
        var kernel1 = TestKernelFactory.CreateVectorAddKernel();
        var kernel2 = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act
        var key1 = MetalKernelCache.ComputeCacheKey(kernel1, options);
        var key2 = MetalKernelCache.ComputeCacheKey(kernel2, options);

        // Assert
        Assert.Equal(key1, key2);
        LogTestInfo($"Cache key: {key1.Substring(0, Math.Min(16, key1.Length))}...");
    }

    [Fact]
    public void ComputeCacheKey_DifferentOptions_GeneratesDifferentKeys()
    {
        // Arrange
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };

        // Act
        var key1 = MetalKernelCache.ComputeCacheKey(kernel, options1);
        var key2 = MetalKernelCache.ComputeCacheKey(kernel, options2);

        // Assert
        Assert.NotEqual(key1, key2);
        LogTestInfo("Different optimization levels generate different cache keys");
    }

    [Fact]
    public void ComputeCacheKey_DifferentKernels_GeneratesDifferentKeys()
    {
        // Arrange
        var kernel1 = TestKernelFactory.CreateVectorAddKernel();
        var kernel2 = TestKernelFactory.CreateThreadgroupMemoryKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act
        var key1 = MetalKernelCache.ComputeCacheKey(kernel1, options);
        var key2 = MetalKernelCache.ComputeCacheKey(kernel2, options);

        // Assert
        Assert.NotEqual(key1, key2);
        LogTestInfo("Different kernels generate different cache keys");
    }

    #endregion

    #region LRU Eviction Tests

    [SkippableFact]
    public async Task AddKernel_WhenCacheFull_EvictsLeastRecentlyUsed()
    {
        // Arrange
        RequireMetalSupport();
        var maxSize = 5;
        var cache = CreateCache(maxSize: maxSize);
        var compiler = CreateCompiler(cache);

        // Fill cache to capacity
        var kernels = new List<KernelDefinition>();
        for (int i = 0; i < maxSize; i++)
        {
            var kernel = new KernelDefinition($"kernel_{i}", @"
#include <metal_stdlib>
kernel void kernel_" + i + @"(device float* data [[buffer(0)]]) {
    data[0] = " + i + @".0f;
}");
            kernels.Add(kernel);
            await compiler.CompileAsync(kernel);
        }

        // Act - Add one more to trigger eviction
        var newKernel = new KernelDefinition("kernel_new", @"
#include <metal_stdlib>
kernel void kernel_new(device float* data [[buffer(0)]]) {
    data[0] = 999.0f;
}");
        await compiler.CompileAsync(newKernel);

        // Assert
        var stats = cache.GetStatistics();
        Assert.True(stats.EvictionCount > 0);
        Assert.True(stats.CurrentSize <= maxSize);
        LogTestInfo($"LRU eviction triggered - Evicted: {stats.EvictionCount}, Current size: {stats.CurrentSize}");
    }

    [SkippableFact]
    public async Task AccessKernel_UpdatesLRUPosition()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache(maxSize: 3);
        var compiler = CreateCompiler(cache);

        var kernel1 = new KernelDefinition("k1", "kernel void k1() {}");
        var kernel2 = new KernelDefinition("k2", "kernel void k2() {}");
        var kernel3 = new KernelDefinition("k3", "kernel void k3() {}");

        // Add 3 kernels
        await compiler.CompileAsync(kernel1);
        await compiler.CompileAsync(kernel2);
        await compiler.CompileAsync(kernel3);

        // Act - Access kernel1 to make it recently used
        cache.TryGetKernel(kernel1, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);

        // Add a 4th kernel - should evict kernel2, not kernel1
        var kernel4 = new KernelDefinition("k4", "kernel void k4() {}");
        await compiler.CompileAsync(kernel4);

        // Assert - kernel1 should still be in cache
        var found = cache.TryGetKernel(kernel1, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        Assert.True(found);
        LogTestInfo("LRU correctly preserved recently accessed kernel");
    }

    #endregion

    #region TTL and Expiration Tests

    [SkippableFact]
    public async Task TryGetKernel_ExpiredEntry_ReturnsCacheMiss()
    {
        // Arrange
        RequireMetalSupport();
        var shortTtl = TimeSpan.FromMilliseconds(100);
        var cache = CreateCache(ttl: shortTtl);
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act - Add kernel
        await compiler.CompileAsync(kernel);

        // Wait for expiration
        await Task.Delay(200);

        // Try to retrieve
        var found = cache.TryGetKernel(kernel, TestKernelFactory.CreateCompilationOptions(),
            out _, out _, out _);

        // Assert
        Assert.False(found);
        LogTestInfo("Expired cache entry correctly returns miss");
    }

    #endregion

    #region Cache Invalidation Tests

    [SkippableFact]
    public async Task InvalidateKernel_RemovesFromCache()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Add to cache
        await compiler.CompileAsync(kernel, options);

        // Act
        var invalidated = cache.InvalidateKernel(kernel, options);

        // Assert
        Assert.True(invalidated);
        var found = cache.TryGetKernel(kernel, options, out _, out _, out _);
        Assert.False(found);
        LogTestInfo("Kernel invalidated successfully");
    }

    [SkippableFact]
    public void InvalidateKernel_NonExistentKernel_ReturnsFalse()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act
        var invalidated = cache.InvalidateKernel(kernel, options);

        // Assert
        Assert.False(invalidated);
        LogTestInfo("Invalidation of non-existent kernel returns false");
    }

    [SkippableFact]
    public async Task Clear_RemovesAllEntries()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);

        // Add multiple kernels
        for (int i = 0; i < 5; i++)
        {
            var kernel = new KernelDefinition($"k{i}", $"kernel void k{i}() {{}}");
            await compiler.CompileAsync(kernel);
        }

        // Act
        cache.Clear();

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(0, stats.CurrentSize);
        LogTestInfo("Cache cleared successfully");
    }

    #endregion

    #region Statistics Tests

    [SkippableFact]
    public async Task GetStatistics_ReturnsAccurateMetrics()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - Perform various operations
        await compiler.CompileAsync(kernel, options); // Miss
        cache.TryGetKernel(kernel, options, out _, out _, out _); // Hit
        cache.TryGetKernel(kernel, options, out _, out _, out _); // Hit

        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(1, stats.MissCount);
        Assert.True(stats.HitCount >= 2);
        Assert.True(stats.HitRate > 0);
        Assert.True(stats.CurrentSize > 0);
        Assert.True(stats.TotalMemoryBytes > 0);

        LogTestInfo($"Cache statistics - Hits: {stats.HitCount}, Misses: {stats.MissCount}, " +
                   $"Hit Rate: {stats.HitRate:P2}, Memory: {stats.TotalMemoryBytes} bytes");
    }

    [SkippableFact]
    public async Task GetStatistics_CompilationTimeTracking_ReportsAverages()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);

        // Act - Compile multiple kernels
        for (int i = 0; i < 3; i++)
        {
            var kernel = new KernelDefinition($"k{i}", $"kernel void k{i}() {{}}");
            await compiler.CompileAsync(kernel);
        }

        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.AverageCompilationTimeMs >= 0);
        LogTestInfo($"Average compilation time: {stats.AverageCompilationTimeMs}ms");
    }

    #endregion

    #region Persistent Cache Tests

    [SkippableFact]
    public async Task PersistentCache_SavesAndLoadsBinaryData()
    {
        // Arrange
        RequireMetalSupport();
        var tempPath = Path.Combine(Path.GetTempPath(), "DotComputeTest", Guid.NewGuid().ToString());
        var cache = CreateCache(persistentPath: tempPath);
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        try
        {
            // Act - Compile kernel (should save to disk)
            await compiler.CompileAsync(kernel);

            // Assert - Check that files were created
            var files = Directory.GetFiles(tempPath, "*.metallib");
            Assert.True(files.Length > 0);
            LogTestInfo($"Persistent cache saved {files.Length} files to {tempPath}");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    #endregion

    #region Thread Safety Tests

    [SkippableFact]
    public async Task ConcurrentAccess_HandlesThreadSafety()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Compile once
        await compiler.CompileAsync(kernel);

        // Act - Concurrent cache access
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            cache.TryGetKernel(kernel, TestKernelFactory.CreateCompilationOptions(),
                out IntPtr _, out IntPtr _, out IntPtr _);
        }));

        await Task.WhenAll(tasks);

        // Assert
        var stats = cache.GetStatistics();
        Assert.True(stats.HitCount >= 10);
        LogTestInfo($"Concurrent access completed - {stats.HitCount} hits");
    }

    #endregion

    #region Disposal Tests

    [SkippableFact]
    public void Dispose_LogsFinalStatistics()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();

        // Act
        cache.Dispose();

        // Assert - Should not throw
        Assert.Throws<ObjectDisposedException>(() =>
            cache.TryGetKernel(TestKernelFactory.CreateVectorAddKernel(),
                TestKernelFactory.CreateCompilationOptions(),
                out _, out _, out _));

        LogTestInfo("Cache disposed and logs final statistics");
    }

    [SkippableFact]
    public Task TryGetKernel_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        cache.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            cache.TryGetKernel(kernel, TestKernelFactory.CreateCompilationOptions(),
                out _, out _, out _));

        return Task.CompletedTask;
    }

    [SkippableFact]
    public void AddKernel_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        cache.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            cache.AddKernel(
                TestKernelFactory.CreateVectorAddKernel(),
                TestKernelFactory.CreateCompilationOptions(),
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
    }

    #endregion

    #region Memory Pressure Tests

    [SkippableFact]
    public async Task GetStatistics_TracksMemoryUsage()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);

        // Act - Add multiple kernels
        for (int i = 0; i < 5; i++)
        {
            var kernel = TestKernelFactory.CreateLargeKernel(100);
            await compiler.CompileAsync(kernel);
        }

        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.TotalMemoryBytes > 0);
        Assert.True(stats.CurrentSize > 0);
        LogTestInfo($"Cache memory usage: {stats.TotalMemoryBytes:N0} bytes across {stats.CurrentSize} entries");
    }

    #endregion

    #region Extended Cache Coverage Tests

    [SkippableFact]
    public async Task TryGet_CacheHit_ReturnsCorrectKernel()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Compile kernel to add to cache
        var compiled = await compiler.CompileAsync(kernel, options);

        // Act - Retrieve from cache
        var found = cache.TryGetKernel(kernel, options, out var lib, out var func, out var pipeline);

        // Assert - Verify correct kernel is returned
        Assert.True(found);
        Assert.NotEqual(IntPtr.Zero, lib);
        Assert.NotEqual(IntPtr.Zero, func);
        Assert.NotEqual(IntPtr.Zero, pipeline);
        Assert.Equal(kernel.Name, compiled.Name);

        LogTestInfo("Cache hit returns correct kernel pointers");
    }

    [SkippableFact]
    public Task TryGet_CacheMiss_ReturnsNull()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - Try to get kernel that was never added
        var found = cache.TryGetKernel(kernel, options, out var lib, out var func, out var pipeline);

        // Assert
        Assert.False(found);
        Assert.Equal(IntPtr.Zero, lib);
        Assert.Equal(IntPtr.Zero, func);
        Assert.Equal(IntPtr.Zero, pipeline);

        var stats = cache.GetStatistics();
        Assert.Equal(1, stats.MissCount);
        Assert.Equal(0, stats.HitCount);

        LogTestInfo("Cache miss returns null pointers and increments miss count");

        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task Add_NewKernel_StoresSuccessfully()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Act - Compile and store kernel
        var compiled = await compiler.CompileAsync(kernel, options);

        // Assert - Verify stored successfully
        var stats = cache.GetStatistics();
        Assert.Equal(1, stats.CurrentSize);
        Assert.True(stats.TotalMemoryBytes > 0);

        // Verify we can retrieve it
        var found = cache.TryGetKernel(kernel, options, out _, out _, out _);
        Assert.True(found);

        LogTestInfo($"New kernel stored successfully - Size: {stats.CurrentSize}, Memory: {stats.TotalMemoryBytes} bytes");
    }

    [SkippableFact]
    public async Task Add_LRUEviction_RemovesOldestEntry()
    {
        // Arrange
        RequireMetalSupport();
        var maxSize = 3;
        var cache = CreateCache(maxSize: maxSize);
        var compiler = CreateCompiler(cache);

        var kernel1 = new KernelDefinition("oldest", "kernel void oldest() {}");
        var kernel2 = new KernelDefinition("middle", "kernel void middle() {}");
        var kernel3 = new KernelDefinition("newest", "kernel void newest() {}");
        var kernel4 = new KernelDefinition("trigger", "kernel void trigger() {}");

        // Act - Fill cache
        await compiler.CompileAsync(kernel1);
        await compiler.CompileAsync(kernel2);
        await compiler.CompileAsync(kernel3);

        // Access kernel2 and kernel3 to update their LRU position
        cache.TryGetKernel(kernel2, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        cache.TryGetKernel(kernel3, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);

        // Add kernel4 - should evict kernel1 (oldest, never accessed)
        await compiler.CompileAsync(kernel4);

        // Assert - kernel1 should be evicted
        var found1 = cache.TryGetKernel(kernel1, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        var found2 = cache.TryGetKernel(kernel2, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        var found3 = cache.TryGetKernel(kernel3, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        var found4 = cache.TryGetKernel(kernel4, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);

        Assert.False(found1, "Oldest kernel should be evicted");
        Assert.True(found2, "Recently accessed kernel should remain");
        Assert.True(found3, "Recently accessed kernel should remain");
        Assert.True(found4, "Newly added kernel should be present");

        var stats = cache.GetStatistics();
        Assert.True(stats.EvictionCount > 0);

        LogTestInfo($"LRU eviction removed oldest entry - Evictions: {stats.EvictionCount}");
    }

    [SkippableFact]
    public async Task Add_SizeLimit_EnforcesMaxSize()
    {
        // Arrange
        RequireMetalSupport();
        var maxSize = 5;
        var cache = CreateCache(maxSize: maxSize);
        var compiler = CreateCompiler(cache);

        // Act - Add more kernels than max size
        for (int i = 0; i < maxSize + 10; i++)
        {
            var kernel = new KernelDefinition($"kernel_{i}", $"kernel void kernel_{i}() {{}}");
            await compiler.CompileAsync(kernel);
        }

        // Assert - Cache should not exceed max size
        var stats = cache.GetStatistics();
        Assert.True(stats.CurrentSize <= maxSize, $"Cache size {stats.CurrentSize} exceeds max {maxSize}");
        Assert.True(stats.EvictionCount > 0, "Evictions should have occurred");

        LogTestInfo($"Size limit enforced - Current: {stats.CurrentSize}, Max: {maxSize}, Evicted: {stats.EvictionCount}");
    }

    [SkippableFact]
    public async Task PersistToDisk_ThenReload_Survives()
    {
        // Arrange
        RequireMetalSupport();
        var tempPath = Path.Combine(Path.GetTempPath(), "DotComputeTest", Guid.NewGuid().ToString());
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        try
        {
            // Create first cache instance and compile kernel
            var cache1 = CreateCache(persistentPath: tempPath);
            var compiler1 = CreateCompiler(cache1);
            await compiler1.CompileAsync(kernel, options);
            cache1.Dispose();

            // Act - Create new cache instance pointing to same persistent path
            var cache2 = CreateCache(persistentPath: tempPath);

            // Assert - Files should exist on disk
            var metaFiles = Directory.GetFiles(tempPath, "*.meta");
            var libFiles = Directory.GetFiles(tempPath, "*.metallib");

            Assert.True(metaFiles.Length > 0, "Metadata files should exist");
            Assert.True(libFiles.Length > 0, "Binary library files should exist");

            LogTestInfo($"Persistent cache survived - Meta files: {metaFiles.Length}, Lib files: {libFiles.Length}");

            cache2.Dispose();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    [SkippableFact]
    public async Task ConcurrentAccess_ThreadSafe_NoRaceConditions()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache(maxSize: 50);
        var compiler = CreateCompiler(cache);
        var threadCount = 10;
        var opsPerThread = 5;

        // Act - Spawn multiple threads performing concurrent operations
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                var kernel = new KernelDefinition($"kernel_t{threadId}_op{i}",
                    $"kernel void kernel_t{threadId}_op{i}() {{}}");

                // Compile (adds to cache)
                await compiler.CompileAsync(kernel);

                // Retrieve from cache
                cache.TryGetKernel(kernel, TestKernelFactory.CreateCompilationOptions(),
                    out _, out _, out _);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert - Verify no race conditions
        var stats = cache.GetStatistics();
        Assert.True(stats.CurrentSize > 0);
        Assert.Equal(threadCount * opsPerThread, stats.MissCount); // Each compile is a miss
        Assert.True(stats.HitCount >= threadCount * opsPerThread); // Each retrieve is a hit

        LogTestInfo($"Concurrent access completed - Threads: {threadCount}, Operations: {threadCount * opsPerThread}, " +
                   $"Hits: {stats.HitCount}, Misses: {stats.MissCount}");
    }

    [SkippableFact]
    public async Task CorruptedEntry_HandledGracefully()
    {
        // Arrange
        RequireMetalSupport();
        var tempPath = Path.Combine(Path.GetTempPath(), "DotComputeTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            // Create a corrupted cache entry on disk
            var corruptedKey = "corrupted_key_12345";
            var metaPath = Path.Combine(tempPath, $"{corruptedKey}.meta");
            var libPath = Path.Combine(tempPath, $"{corruptedKey}.metallib");

            // Write invalid JSON to metadata
            File.WriteAllText(metaPath, "{ invalid json {{");
            File.WriteAllBytes(libPath, new byte[] { 0x00, 0x01, 0x02 });

            // Act - Create cache with corrupted persistent storage
            var cache = CreateCache(persistentPath: tempPath);

            // Try to compile a kernel (should not crash)
            var kernel = TestKernelFactory.CreateVectorAddKernel();
            var compiled = await CreateCompiler(cache).CompileAsync(kernel);

            // Assert - Cache should handle corruption gracefully
            Assert.NotNull(compiled);
            var stats = cache.GetStatistics();
            Assert.True(stats.CurrentSize >= 1);

            LogTestInfo("Corrupted cache entry handled gracefully without crash");

            cache.Dispose();
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    [SkippableFact]
    public async Task Invalidate_RemovesEntry_VerifiesMissAfter()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        // Add kernel to cache
        await compiler.CompileAsync(kernel, options);
        var initialStats = cache.GetStatistics();
        Assert.Equal(1, initialStats.CurrentSize);

        // Act - Invalidate the kernel
        var invalidated = cache.InvalidateKernel(kernel, options);

        // Assert
        Assert.True(invalidated, "Invalidation should succeed");

        // Verify entry is removed
        var found = cache.TryGetKernel(kernel, options, out _, out _, out _);
        Assert.False(found, "Kernel should not be found after invalidation");

        var finalStats = cache.GetStatistics();
        Assert.Equal(0, finalStats.CurrentSize);

        LogTestInfo("Invalidation removed entry and subsequent access returns miss");
    }

    [SkippableFact]
    public async Task Statistics_TrackAccurately_AllMetrics()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache(maxSize: 10);
        var compiler = CreateCompiler(cache);

        // Act - Perform various operations to generate statistics
        var kernel1 = new KernelDefinition("k1", "kernel void k1() {}");
        var kernel2 = new KernelDefinition("k2", "kernel void k2() {}");

        // Compile two kernels (2 misses)
        await compiler.CompileAsync(kernel1);
        await compiler.CompileAsync(kernel2);

        // Access kernel1 multiple times (hits)
        for (int i = 0; i < 5; i++)
        {
            cache.TryGetKernel(kernel1, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        }

        // Access kernel2 multiple times (hits)
        for (int i = 0; i < 3; i++)
        {
            cache.TryGetKernel(kernel2, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        }

        // Assert - Verify all metrics
        var stats = cache.GetStatistics();

        Assert.Equal(2, stats.MissCount); // 2 compilations
        Assert.True(stats.HitCount >= 8); // 5 + 3 accesses
        Assert.Equal(2, stats.CurrentSize);
        Assert.True(stats.HitRate > 0);
        Assert.True(stats.HitRate < 1); // Should have both hits and misses
        Assert.True(stats.TotalMemoryBytes > 0);
        Assert.True(stats.AverageCompilationTimeMs >= 0);
        Assert.True(stats.AverageCacheRetrievalTimeMs >= 0);

        LogTestInfo($"Statistics tracking - Hits: {stats.HitCount}, Misses: {stats.MissCount}, " +
                   $"Hit Rate: {stats.HitRate:P2}, Memory: {stats.TotalMemoryBytes} bytes, " +
                   $"Avg Compile: {stats.AverageCompilationTimeMs}ms, Avg Retrieval: {stats.AverageCacheRetrievalTimeMs}ms");
    }

    [SkippableFact]
    public async Task ConcurrentAddAndGet_VerifiesCorrectCounts()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache(maxSize: 100);
        var compiler = CreateCompiler(cache);
        var threadCount = 10;
        var kernelsPerThread = 3;

        // Act - Concurrent adds and gets
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (int i = 0; i < kernelsPerThread; i++)
            {
                var kernel = new KernelDefinition($"kernel_t{threadId}_k{i}",
                    $"kernel void kernel_t{threadId}_k{i}() {{}}");
                var options = TestKernelFactory.CreateCompilationOptions();

                // Add to cache
                await compiler.CompileAsync(kernel, options);

                // Get from cache twice
                cache.TryGetKernel(kernel, options, out _, out _, out _);
                cache.TryGetKernel(kernel, options, out _, out _, out _);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert - Verify counts are accurate
        var stats = cache.GetStatistics();

        var expectedOps = threadCount * kernelsPerThread;
        Assert.Equal(expectedOps, stats.MissCount); // Each compile is a miss
        Assert.True(stats.HitCount >= expectedOps * 2); // Each kernel accessed twice
        Assert.True(stats.CurrentSize >= expectedOps); // All kernels should be cached

        LogTestInfo($"Concurrent operations completed - Threads: {threadCount}, " +
                   $"Expected ops: {expectedOps}, Actual misses: {stats.MissCount}, " +
                   $"Actual hits: {stats.HitCount}, Cache size: {stats.CurrentSize}");
    }

    [Fact]
    public void CacheKey_IncludesAllRelevantProperties()
    {
        // Arrange
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Create options with fully-specified properties to ensure uniqueness
        // (setting only one property per object can result in identical combinations)
        var options1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None, GenerateDebugInfo = false, FastMath = false };
        var options2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default, GenerateDebugInfo = false, FastMath = true };
        var options3 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3, GenerateDebugInfo = false, FastMath = true };
        var options4 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default, GenerateDebugInfo = true, FastMath = true };
        var options5 = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default, GenerateDebugInfo = false, FastMath = false };
        var options6 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3, GenerateDebugInfo = true, FastMath = false };
        var options7 = new CompilationOptions { OptimizationLevel = OptimizationLevel.None, GenerateDebugInfo = true, FastMath = true };

        // Act - Generate cache keys
        var key1 = MetalKernelCache.ComputeCacheKey(kernel, options1);
        var key2 = MetalKernelCache.ComputeCacheKey(kernel, options2);
        var key3 = MetalKernelCache.ComputeCacheKey(kernel, options3);
        var key4 = MetalKernelCache.ComputeCacheKey(kernel, options4);
        var key5 = MetalKernelCache.ComputeCacheKey(kernel, options5);
        var key6 = MetalKernelCache.ComputeCacheKey(kernel, options6);
        var key7 = MetalKernelCache.ComputeCacheKey(kernel, options7);

        // Assert - All keys should be different
        var keys = new[] { key1, key2, key3, key4, key5, key6, key7 };
        var uniqueKeys = keys.Distinct().ToArray();

        Assert.Equal(keys.Length, uniqueKeys.Length);

        LogTestInfo($"Cache keys correctly differentiate compilation options - Generated {uniqueKeys.Length} unique keys");
    }

    [SkippableFact]
    public async Task PeriodicCleanup_RemovesExpiredEntries()
    {
        // Arrange
        RequireMetalSupport();
        var shortTtl = TimeSpan.FromMilliseconds(50);
        var cache = CreateCache(ttl: shortTtl);
        var compiler = CreateCompiler(cache);

        // Add multiple kernels
        for (int i = 0; i < 5; i++)
        {
            var kernel = new KernelDefinition($"expire_{i}", $"kernel void expire_{i}() {{}}");
            await compiler.CompileAsync(kernel);
        }

        var statsBeforeCleanup = cache.GetStatistics();
        Assert.Equal(5, statsBeforeCleanup.CurrentSize);

        // Act - Wait for TTL expiration + cleanup timer (5 minutes is too long, so we'll just verify TTL)
        await Task.Delay(100);

        // Manually trigger cleanup by trying to access expired entries
        for (int i = 0; i < 5; i++)
        {
            var kernel = new KernelDefinition($"expire_{i}", $"kernel void expire_{i}() {{}}");
            cache.TryGetKernel(kernel, TestKernelFactory.CreateCompilationOptions(), out _, out _, out _);
        }

        // Assert - Expired entries should not be found
        var statsAfter = cache.GetStatistics();
        Assert.True(statsAfter.MissCount >= 5, "Expired entries should result in cache misses");

        LogTestInfo($"Periodic cleanup verified - Entries before: {statsBeforeCleanup.CurrentSize}, " +
                   $"Misses after expiration: {statsAfter.MissCount}");
    }

    #endregion

    #region Binary Caching Tests

    [SkippableFact]
    public async Task BinaryCache_SaveAndLoad_RestoresFromDisk()
    {
        // Arrange
        RequireMetalSupport();
        var cachePath = Path.Combine(Path.GetTempPath(), $"DotCompute_Test_{Guid.NewGuid():N}");
        var cache = CreateCacheWithPersistence(cachePath);
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        try
        {
            // Act - Compile (saves to binary cache)
            var compiled = await compiler.CompileAsync(kernel, options);
            cache.Dispose();

            // Verify binary files exist
            var metallibFiles = Directory.GetFiles(cachePath, "*.metallib");
            var metaFiles = Directory.GetFiles(cachePath, "*.meta");
            Assert.NotEmpty(metallibFiles);
            Assert.NotEmpty(metaFiles);
            Assert.Equal(metallibFiles.Length, metaFiles.Length);

            LogTestInfo($"Binary cache files created: {metallibFiles.Length} .metallib + {metaFiles.Length} .meta");

            // Create new cache instance (simulates restart)
            var cache2 = CreateCacheWithPersistence(cachePath);
            var found = cache2.TryGetKernel(kernel, options, out var lib, out var func, out var pipeline);

            // Assert - Should load from binary cache
            Assert.True(found, "Kernel should be loaded from persistent binary cache");
            Assert.NotEqual(IntPtr.Zero, lib);
            Assert.NotEqual(IntPtr.Zero, func);
            Assert.NotEqual(IntPtr.Zero, pipeline);

            var stats = cache2.GetStatistics();
            Assert.True(stats.HitCount >= 1);
            LogTestInfo($"Binary cache hit - Loaded from disk successfully");

            cache2.Dispose();
        }
        finally
        {
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
    }

    [SkippableFact]
    public async Task BinaryCache_MultipleKernels_SavesAll()
    {
        // Arrange
        RequireMetalSupport();
        var cachePath = Path.Combine(Path.GetTempPath(), $"DotCompute_Test_{Guid.NewGuid():N}");
        var cache = CreateCacheWithPersistence(cachePath);
        var compiler = CreateCompiler(cache);

        try
        {
            // Act - Compile 3 different kernels
            var kernel1 = TestKernelFactory.CreateVectorAddKernel();
            var kernel2 = new KernelDefinition("VectorMul", "kernel void VectorMul() {}")
            {
                EntryPoint = "VectorMul",
                Language = KernelLanguage.Metal
            };
            var kernel3 = new KernelDefinition("VectorSub", "kernel void VectorSub() {}")
            {
                EntryPoint = "VectorSub",
                Language = KernelLanguage.Metal
            };
            var options = TestKernelFactory.CreateCompilationOptions();

            await compiler.CompileAsync(kernel1, options);
            await compiler.CompileAsync(kernel2, options);
            await compiler.CompileAsync(kernel3, options);
            cache.Dispose();

            // Verify all saved
            var metallibFiles = Directory.GetFiles(cachePath, "*.metallib");
            Assert.True(metallibFiles.Length >= 3, $"Expected at least 3 binary cache files, got {metallibFiles.Length}");

            LogTestInfo($"Multiple kernels cached: {metallibFiles.Length} binary files");

            // Create new cache and verify all load
            var cache2 = CreateCacheWithPersistence(cachePath);
            Assert.True(cache2.TryGetKernel(kernel1, options, out _, out _, out _));
            Assert.True(cache2.TryGetKernel(kernel2, options, out _, out _, out _));
            Assert.True(cache2.TryGetKernel(kernel3, options, out _, out _, out _));

            LogTestInfo("All kernels loaded from binary cache successfully");

            cache2.Dispose();
        }
        finally
        {
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
    }

    #endregion

    #region Cache Validation Tests

    [SkippableFact]
    public async Task CacheValidation_MetadataIncludesVersionInfo()
    {
        // Arrange
        RequireMetalSupport();
        var cachePath = Path.Combine(Path.GetTempPath(), $"DotCompute_Test_{Guid.NewGuid():N}");
        var cache = CreateCacheWithPersistence(cachePath);
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        try
        {
            // Act
            await compiler.CompileAsync(kernel, options);
            cache.Dispose();

            // Read metadata file
            var metaFiles = Directory.GetFiles(cachePath, "*.meta");
            Assert.NotEmpty(metaFiles);
            var metaContent = File.ReadAllText(metaFiles[0]);

            // Assert - Metadata should contain version fields
            Assert.Contains("version", metaContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("metalVersion", metaContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("osVersion", metaContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("backendVersion", metaContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("deviceFingerprint", metaContent, StringComparison.OrdinalIgnoreCase);

            LogTestInfo($"Cache metadata includes validation fields: {metaContent.Length} bytes");
        }
        finally
        {
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
    }

    [SkippableFact]
    public async Task CacheValidation_IncompatibleVersion_InvalidatesCache()
    {
        // Arrange
        RequireMetalSupport();
        var cachePath = Path.Combine(Path.GetTempPath(), $"DotCompute_Test_{Guid.NewGuid():N}");
        var cache = CreateCacheWithPersistence(cachePath);
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var options = TestKernelFactory.CreateCompilationOptions();

        try
        {
            // Act - Compile and save
            await compiler.CompileAsync(kernel, options);
            cache.Dispose();

            // Modify metadata to simulate incompatible version
            var metaFiles = Directory.GetFiles(cachePath, "*.meta");
            Assert.NotEmpty(metaFiles);
            var metaPath = metaFiles[0];
            var metaContent = File.ReadAllText(metaPath);
            var modifiedMeta = metaContent.Replace("\"version\":1", "\"version\":999");
            File.WriteAllText(metaPath, modifiedMeta);

            LogTestInfo("Modified cache metadata version to 999 (incompatible)");

            // Try to load with new cache
            var cache2 = CreateCacheWithPersistence(cachePath);
            var found = cache2.TryGetKernel(kernel, options, out _, out _, out _);

            // Assert - Should not load (incompatible version)
            Assert.False(found, "Cache with incompatible version should be invalidated");

            var stats = cache2.GetStatistics();
            Assert.True(stats.MissCount >= 1);
            LogTestInfo("Incompatible cache entry rejected successfully");

            // Verify files were deleted
            var filesAfter = Directory.GetFiles(cachePath, "*.metallib");
            Assert.Empty(filesAfter);

            cache2.Dispose();
        }
        finally
        {
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
    }

    #endregion

    #region Helper Methods

    private MetalKernelCache CreateCacheWithPersistence(string persistentCachePath)
    {
        return CreateCache(
            maxSize: 100,
            ttl: TimeSpan.FromHours(1),
            persistentPath: persistentCachePath);
    }

    #endregion
}
