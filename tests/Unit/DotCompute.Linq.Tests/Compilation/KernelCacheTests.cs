// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using Xunit;
using ComputeBackend = DotCompute.Linq.Compilation.ComputeBackend;

namespace DotCompute.Linq.Tests.Compilation;

/// <summary>
/// Unit tests for <see cref="KernelCache"/>.
/// </summary>
public sealed class KernelCacheTests : IDisposable
{
    private readonly KernelCache _cache;

    public KernelCacheTests()
    {
        _cache = new KernelCache(maxEntries: 100, maxMemoryBytes: 10 * 1024 * 1024);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    #region Basic Operations

    [Fact]
    public void GetCached_WhenKeyNotPresent_ReturnsNull()
    {
        // Act
        var result = _cache.GetCached("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Store_ThenGetCached_ReturnsStoredDelegate()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "test-key";
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        _cache.Store(key, testDelegate, ttl);
        var result = _cache.GetCached(key);

        // Assert
        Assert.NotNull(result);
        Assert.Same(testDelegate, result);
    }

    [Fact]
    public void Store_WithDifferentKeys_StoresMultipleDelegates()
    {
        // Arrange
        Func<int, int> delegate1 = x => x * 2;
        Func<int, int> delegate2 = x => x + 1;
        const string key1 = "key1";
        const string key2 = "key2";
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        _cache.Store(key1, delegate1, ttl);
        _cache.Store(key2, delegate2, ttl);

        // Assert
        Assert.Same(delegate1, _cache.GetCached(key1));
        Assert.Same(delegate2, _cache.GetCached(key2));
    }

    [Fact]
    public void Store_WithSameKey_OverwritesExistingEntry()
    {
        // Arrange
        Func<int, int> delegate1 = x => x * 2;
        Func<int, int> delegate2 = x => x + 1;
        const string key = "test-key";
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        _cache.Store(key, delegate1, ttl);
        _cache.Store(key, delegate2, ttl);

        // Assert
        Assert.Same(delegate2, _cache.GetCached(key));
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "test-key";
        _cache.Store(key, testDelegate, TimeSpan.FromMinutes(5));

        // Act
        var result = _cache.Remove(key);

        // Assert
        Assert.True(result);
        Assert.Null(_cache.GetCached(key));
    }

    [Fact]
    public void Remove_NonexistentKey_ReturnsFalse()
    {
        // Act
        var result = _cache.Remove("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.Store("key2", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.Store("key3", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        _cache.Clear();

        // Assert
        Assert.Null(_cache.GetCached("key1"));
        Assert.Null(_cache.GetCached("key2"));
        Assert.Null(_cache.GetCached("key3"));
    }

    #endregion

    #region TTL Expiration

    [Fact]
    public void GetCached_AfterTTLExpires_ReturnsNull()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "test-key";
        var ttl = TimeSpan.FromMilliseconds(100);

        _cache.Store(key, testDelegate, ttl);

        // Act - Wait for expiration
        Thread.Sleep(200);
        var result = _cache.GetCached(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCached_BeforeTTLExpires_ReturnsDelegate()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "test-key";
        var ttl = TimeSpan.FromSeconds(10);

        _cache.Store(key, testDelegate, ttl);

        // Act - Access immediately
        Thread.Sleep(50);
        var result = _cache.GetCached(key);

        // Assert
        Assert.NotNull(result);
        Assert.Same(testDelegate, result);
    }

    #endregion

    #region Statistics

    [Fact]
    public void GetStatistics_InitialState_ReturnsZeroStatistics()
    {
        // Act
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.Hits);
        Assert.Equal(0, stats.Misses);
        Assert.Equal(0, stats.CurrentEntries);
        Assert.Equal(0, stats.EvictionCount);
    }

    [Fact]
    public void GetStatistics_AfterHit_IncrementsHits()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        _cache.GetCached("key1");
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(1, stats.Hits);
        Assert.Equal(0, stats.Misses);
    }

    [Fact]
    public void GetStatistics_AfterMiss_IncrementsMisses()
    {
        // Act
        _cache.GetCached("nonexistent");
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.Hits);
        Assert.Equal(1, stats.Misses);
    }

    [Fact]
    public void GetStatistics_AfterMultipleOperations_TracksCorrectly()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.Store("key2", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        _cache.GetCached("key1"); // Hit
        _cache.GetCached("key1"); // Hit
        _cache.GetCached("nonexistent"); // Miss
        _cache.GetCached("key2"); // Hit

        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(3, stats.Hits);
        Assert.Equal(1, stats.Misses);
        Assert.Equal(2, stats.CurrentEntries);
        Assert.Equal(0.75, stats.HitRatio);
    }

    [Fact]
    public void Clear_ResetsStatistics()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.GetCached("key1");

        // Act
        _cache.Clear();
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.Hits);
        Assert.Equal(0, stats.Misses);
        Assert.Equal(0, stats.CurrentEntries);
    }

    #endregion

    #region LRU Eviction

    [Fact]
    public void Store_WhenCacheFull_EvictsLeastRecentlyUsed()
    {
        // Arrange - Create cache with small limit
        using var smallCache = new KernelCache(maxEntries: 3, maxMemoryBytes: 10 * 1024 * 1024);

        // Act - Fill cache
        smallCache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        Thread.Sleep(10); // Ensure different access times
        smallCache.Store("key2", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        Thread.Sleep(10);
        smallCache.Store("key3", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        Thread.Sleep(10);

        // Access key2 to make it more recently used than key1
        smallCache.GetCached("key2");
        Thread.Sleep(10);

        // Store new entry - should evict key1 (least recently used)
        smallCache.Store("key4", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Assert
        Assert.Null(smallCache.GetCached("key1")); // Evicted
        Assert.NotNull(smallCache.GetCached("key2")); // Kept
        Assert.NotNull(smallCache.GetCached("key3")); // Kept
        Assert.NotNull(smallCache.GetCached("key4")); // New entry
    }

    [Fact]
    public void Store_WhenEvictionOccurs_IncrementsEvictionCount()
    {
        // Arrange
        using var smallCache = new KernelCache(maxEntries: 2, maxMemoryBytes: 10 * 1024 * 1024);

        // Act
        smallCache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        smallCache.Store("key2", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        smallCache.Store("key3", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        var stats = smallCache.GetStatistics();

        // Assert
        Assert.True(stats.EvictionCount > 0);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public void ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var tasks = new List<Task>();

        // Act - Multiple threads performing operations concurrently
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < operationsPerThread; j++)
                {
                    var key = $"key-{threadId}-{j}";
                    _cache.Store(key, (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
                    _cache.GetCached(key);

                    if (j % 10 == 0)
                    {
                        _cache.GetStatistics();
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - No exceptions and cache is in valid state
        var stats = _cache.GetStatistics();
        Assert.True(stats.CurrentEntries <= 100); // Max entries limit respected
        Assert.True(stats.Hits > 0);
    }

    [Fact]
    public void ConcurrentReadOperations_ThreadSafe()
    {
        // Arrange
        _cache.Store("shared-key", (Func<int, int>)(x => x * 2), TimeSpan.FromMinutes(5));
        const int threadCount = 20;
        var tasks = new List<Task>();

        // Act - Multiple threads reading concurrently
        for (var i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < 100; j++)
                {
                    var result = _cache.GetCached("shared-key");
                    Assert.NotNull(result);
                }
            }));
        }

        // Assert - No exceptions
        Task.WaitAll(tasks.ToArray());

        var stats = _cache.GetStatistics();
        Assert.Equal(threadCount * 100, stats.Hits);
    }

    #endregion

    #region Key Generation

    [Fact]
    public void GenerateCacheKey_WithSameInputs_ReturnsSameKey()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentGraphs_ReturnsDifferentKeys()
    {
        // Arrange
        var graph1 = CreateTestOperationGraph();
        var graph2 = CreateTestOperationGraph(operationType: OperationType.Filter);
        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph1, metadata, options);
        var key2 = KernelCache.GenerateCacheKey(graph2, metadata, options);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentMetadata_ReturnsDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata1 = CreateTestTypeMetadata(typeof(int), typeof(int));
        var metadata2 = CreateTestTypeMetadata(typeof(string), typeof(string));
        var options = CreateTestCompilationOptions();

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata1, options);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata2, options);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentOptions_ReturnsDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options1 = CreateTestCompilationOptions(OptimizationLevel.None);
        var options2 = CreateTestCompilationOptions(OptimizationLevel.Aggressive);

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options1);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_WithNullParameter_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            KernelCache.GenerateCacheKey(null!, metadata, options));
        Assert.Throws<ArgumentNullException>(() =>
            KernelCache.GenerateCacheKey(graph, null!, options));
        Assert.Throws<ArgumentNullException>(() =>
            KernelCache.GenerateCacheKey(graph, metadata, null!));
    }

    #endregion

    #region Parameter Validation

    [Fact]
    public void Constructor_WithNegativeMaxEntries_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KernelCache(maxEntries: 0));
    }

    [Fact]
    public void Constructor_WithNegativeMaxMemory_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KernelCache(maxMemoryBytes: 0));
    }

    [Fact]
    public void Store_WithNullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _cache.Store(null!, (Func<int, int>)(x => x), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Store_WithNullDelegate_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _cache.Store("key", null!, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Store_WithNegativeTTL_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _cache.Store("key", (Func<int, int>)(x => x), TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void GetCached_WithNullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cache.GetCached(null!));
    }

    [Fact]
    public void Remove_WithNullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _cache.Remove(null!));
    }

    #endregion

    #region Disposal

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var cache = new KernelCache();

        // Act & Assert - Should not throw
        cache.Dispose();
        cache.Dispose();
    }

    [Fact]
    public void Operations_AfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            cache.Store("key", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5)));
        Assert.Throws<ObjectDisposedException>(() => cache.GetCached("key"));
        Assert.Throws<ObjectDisposedException>(() => cache.Remove("key"));
        Assert.Throws<ObjectDisposedException>(() => cache.Clear());
        Assert.Throws<ObjectDisposedException>(() => cache.GetStatistics());
    }

    #endregion

    #region Helper Methods

    private static OperationGraph CreateTestOperationGraph(OperationType operationType = OperationType.Map)
    {
        var operation = new Operation
        {
            Id = "op1",
            Type = operationType,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = 
                new Dictionary<string, object>(),
            EstimatedCost = 1.0
        };

        return new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation },
            Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>()),
            Root = operation
        };
    }

    private static TypeMetadata CreateTestTypeMetadata(Type? inputType = null, Type? resultType = null)
    {
        return new TypeMetadata
        {
            InputType = inputType ?? typeof(int),
            ResultType = resultType ?? typeof(int),
            IntermediateTypes = new Dictionary<string, Type>(),
            RequiresUnsafe = false,
            IsSimdCompatible = true,
            HasNullableTypes = false
        };
    }

    private static CompilationOptions CreateTestCompilationOptions(
        OptimizationLevel optimizationLevel = OptimizationLevel.Balanced)
    {
        return new CompilationOptions
        {
            TargetBackend = ComputeBackend.Cpu,
            OptimizationLevel = optimizationLevel,
            EnableKernelFusion = true,
            GenerateDebugInfo = false,
            CacheTtl = TimeSpan.FromMinutes(30)
        };
    }

    #endregion
}
