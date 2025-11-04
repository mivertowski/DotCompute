// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Xunit;
using ComputeBackend = DotCompute.Linq.Compilation.ComputeBackend;

namespace DotCompute.Linq.Tests.CodeGeneration;

/// <summary>
/// Comprehensive unit tests for <see cref="KernelCache"/> in the CodeGeneration context.
/// Tests cover cache storage, retrieval, LRU eviction, TTL expiration, thread safety,
/// statistics tracking, key generation, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Caching")]
public sealed class KernelCacheTests : IDisposable
{
    private readonly KernelCache _cache;
    private const int DefaultMaxEntries = 100;
    private const long DefaultMaxMemoryBytes = 10 * 1024 * 1024; // 10MB

    public KernelCacheTests()
    {
        _cache = new KernelCache(maxEntries: DefaultMaxEntries, maxMemoryBytes: DefaultMaxMemoryBytes);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    #region Basic Cache Operations

    [Fact]
    public void GetCached_WithNonExistentKey_ReturnsNull()
    {
        // Act
        var result = _cache.GetCached("nonexistent-key");

        // Assert
        result.Should().BeNull();
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
        result.Should().NotBeNull();
        result.Should().BeSameAs(testDelegate);
    }

    [Fact]
    public void Store_WithMultipleDifferentKeys_StoresAllDelegates()
    {
        // Arrange
        Func<int, int> delegate1 = x => x * 2;
        Func<int, int> delegate2 = x => x + 1;
        Func<int, int> delegate3 = x => x - 5;
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        _cache.Store("key1", delegate1, ttl);
        _cache.Store("key2", delegate2, ttl);
        _cache.Store("key3", delegate3, ttl);

        // Assert
        _cache.GetCached("key1").Should().BeSameAs(delegate1);
        _cache.GetCached("key2").Should().BeSameAs(delegate2);
        _cache.GetCached("key3").Should().BeSameAs(delegate3);

        var stats = _cache.GetStatistics();
        stats.CurrentEntries.Should().Be(3);
    }

    [Fact]
    public void Store_WithSameKey_OverwritesPreviousValue()
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
        _cache.GetCached(key).Should().BeSameAs(delegate2);
        _cache.GetStatistics().CurrentEntries.Should().Be(1);
    }

    [Fact]
    public void Remove_WithExistingKey_RemovesEntryAndReturnsTrue()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "test-key";
        _cache.Store(key, testDelegate, TimeSpan.FromMinutes(5));

        // Act
        var removed = _cache.Remove(key);

        // Assert
        removed.Should().BeTrue();
        _cache.GetCached(key).Should().BeNull();
    }

    [Fact]
    public void Remove_WithNonExistentKey_ReturnsFalse()
    {
        // Act
        var removed = _cache.Remove("nonexistent-key");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllCachedEntries()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            _cache.Store($"key{i}", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        }

        // Act
        _cache.Clear();

        // Assert
        for (var i = 0; i < 10; i++)
        {
            _cache.GetCached($"key{i}").Should().BeNull();
        }

        var stats = _cache.GetStatistics();
        stats.CurrentEntries.Should().Be(0);
    }

    #endregion

    #region TTL Expiration Tests

    [Fact]
    public void GetCached_AfterTTLExpires_ReturnsNull()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "expiring-key";
        var ttl = TimeSpan.FromMilliseconds(100);

        _cache.Store(key, testDelegate, ttl);

        // Act - Wait for expiration (with grace period)
        Thread.Sleep(150);
        var result = _cache.GetCached(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCached_BeforeTTLExpires_ReturnsDelegate()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        const string key = "valid-key";
        var ttl = TimeSpan.FromSeconds(10);

        _cache.Store(key, testDelegate, ttl);

        // Act - Access immediately
        Thread.Sleep(50);
        var result = _cache.GetCached(key);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(testDelegate);
    }

    [Fact]
    public void GetCached_WithExpiredEntry_IncrementsMissCounter()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMilliseconds(50));
        Thread.Sleep(100);

        // Act
        _cache.GetCached("key1");
        var stats = _cache.GetStatistics();

        // Assert
        stats.Misses.Should().Be(1);
        stats.Hits.Should().Be(0);
    }

    [Fact]
    public void Store_WithZeroTTL_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => _cache.Store("key", (Func<int, int>)(x => x), TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*TTL must be positive*");
    }

    #endregion

    #region LRU Eviction Policy Tests

    [Fact]
    public void Store_WhenCacheFullWithMaxEntries_EvictsLeastRecentlyUsed()
    {
        // Arrange - Create cache with small limit
        using var smallCache = new KernelCache(maxEntries: 5, maxMemoryBytes: DefaultMaxMemoryBytes);
        var ttl = TimeSpan.FromMinutes(5);

        // Act - Fill cache completely
        for (var i = 0; i < 5; i++)
        {
            smallCache.Store($"key{i}", (Func<int, int>)(x => x * i), ttl);
            Thread.Sleep(10); // Ensure different access times
        }

        // Access key1 and key2 to make them more recently used
        smallCache.GetCached("key1");
        Thread.Sleep(10);
        smallCache.GetCached("key2");
        Thread.Sleep(10);

        // Store new entry - should evict key0 (least recently used)
        smallCache.Store("key5", (Func<int, int>)(x => x * 5), ttl);

        // Assert
        smallCache.GetCached("key0").Should().BeNull("it should be evicted as LRU");
        smallCache.GetCached("key1").Should().NotBeNull("it was recently accessed");
        smallCache.GetCached("key2").Should().NotBeNull("it was recently accessed");
        smallCache.GetCached("key3").Should().NotBeNull("it's newer than key0");
        smallCache.GetCached("key4").Should().NotBeNull("it's newer than key0");
        smallCache.GetCached("key5").Should().NotBeNull("it's the newest entry");
    }

    [Fact]
    public void Store_WhenEvicting_IncrementsEvictionCounter()
    {
        // Arrange
        using var smallCache = new KernelCache(maxEntries: 3, maxMemoryBytes: DefaultMaxMemoryBytes);
        var ttl = TimeSpan.FromMinutes(5);

        // Act - Fill cache and trigger eviction
        for (var i = 0; i < 5; i++)
        {
            smallCache.Store($"key{i}", (Func<int, int>)(x => x), ttl);
            Thread.Sleep(5);
        }

        var stats = smallCache.GetStatistics();

        // Assert
        stats.EvictionCount.Should().BeGreaterThan(0);
        stats.CurrentEntries.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Store_EvictsMultipleEntriesWhen10PercentThresholdReached()
    {
        // Arrange - 10 max entries means evicting 1 entry (10%) when full
        using var smallCache = new KernelCache(maxEntries: 10, maxMemoryBytes: DefaultMaxMemoryBytes);
        var ttl = TimeSpan.FromMinutes(5);

        // Act - Fill cache to capacity
        for (var i = 0; i < 10; i++)
        {
            smallCache.Store($"key{i}", (Func<int, int>)(x => x), ttl);
            Thread.Sleep(5);
        }

        var statsBefore = smallCache.GetStatistics();

        // Store one more to trigger eviction
        smallCache.Store("key10", (Func<int, int>)(x => x), ttl);

        var statsAfter = smallCache.GetStatistics();

        // Assert
        statsAfter.EvictionCount.Should().BeGreaterThan(statsBefore.EvictionCount);
        statsAfter.CurrentEntries.Should().BeLessThanOrEqualTo(10);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ConcurrentStoreAndGet_IsThreadSafe()
    {
        // Arrange
        const int threadCount = 20;
        const int operationsPerThread = 50;
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act - Multiple threads performing operations concurrently
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < operationsPerThread; j++)
                    {
                        var key = $"key-{threadId}-{j}";
                        _cache.Store(key, (Func<int, int>)(x => x * threadId), TimeSpan.FromMinutes(5));
                        var result = _cache.GetCached(key);
                        result.Should().NotBeNull();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        exceptions.Should().BeEmpty("no exceptions should occur during concurrent operations");

        var stats = _cache.GetStatistics();
        stats.CurrentEntries.Should().BeLessThanOrEqualTo(DefaultMaxEntries);
        stats.Hits.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConcurrentReads_FromSameKey_AreThreadSafe()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;
        _cache.Store("shared-key", testDelegate, TimeSpan.FromMinutes(5));

        const int threadCount = 50;
        const int readsPerThread = 100;
        var tasks = new List<Task>();
        var successfulReads = 0;

        // Act - Multiple threads reading the same key
        for (var i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < readsPerThread; j++)
                {
                    var result = _cache.GetCached("shared-key");
                    if (result is not null)
                    {
                        Interlocked.Increment(ref successfulReads);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        successfulReads.Should().Be(threadCount * readsPerThread);

        var stats = _cache.GetStatistics();
        stats.Hits.Should().Be(threadCount * readsPerThread);
        stats.Misses.Should().Be(0);
    }

    [Fact]
    public void ConcurrentStoreWithSameKey_LastWriteWins()
    {
        // Arrange
        const string sharedKey = "shared-key";
        const int threadCount = 10;
        var tasks = new List<Task<int>>();

        // Act - Multiple threads writing to the same key
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() =>
            {
                _cache.Store(sharedKey, (Func<int, int>)(x => x * threadId), TimeSpan.FromMinutes(5));
                return threadId;
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - One delegate should be stored
        var result = _cache.GetCached(sharedKey);
        result.Should().NotBeNull();
        _cache.GetStatistics().CurrentEntries.Should().Be(1);
    }

    [Fact]
    public void ParallelOperations_WithDifferentKeys_MaintainsCorrectState()
    {
        // Arrange
        const int operationCount = 1000;
        var keys = Enumerable.Range(0, operationCount).Select(i => $"key{i}").ToArray();

        // Act
        Parallel.For(0, operationCount, i =>
        {
            _cache.Store(keys[i], (Func<int, int>)(x => x * i), TimeSpan.FromMinutes(5));
        });

        // Assert
        var stats = _cache.GetStatistics();
        stats.CurrentEntries.Should().BeLessThanOrEqualTo(DefaultMaxEntries);
        stats.EstimatedMemoryBytes.Should().BeLessThanOrEqualTo(DefaultMaxMemoryBytes);
    }

    #endregion

    #region Cache Statistics Tests

    [Fact]
    public void GetStatistics_InitialState_ReturnsZeroValues()
    {
        // Act
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.CurrentEntries.Should().Be(0);
        stats.EstimatedMemoryBytes.Should().Be(0);
        stats.EvictionCount.Should().Be(0);
        stats.HitRatio.Should().Be(0.0);
        stats.MissRatio.Should().Be(0.0);
    }

    [Fact]
    public void GetStatistics_AfterSuccessfulGet_IncrementsHits()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        _cache.GetCached("key1");
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(0);
        stats.HitRatio.Should().Be(1.0);
    }

    [Fact]
    public void GetStatistics_AfterMiss_IncrementsMisses()
    {
        // Act
        _cache.GetCached("nonexistent");
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(1);
        stats.MissRatio.Should().Be(1.0);
    }

    [Fact]
    public void GetStatistics_AfterMultipleOperations_CalculatesCorrectRatios()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.Store("key2", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act - 3 hits, 2 misses
        _cache.GetCached("key1"); // Hit
        _cache.GetCached("key2"); // Hit
        _cache.GetCached("key1"); // Hit
        _cache.GetCached("nonexistent1"); // Miss
        _cache.GetCached("nonexistent2"); // Miss

        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(3);
        stats.Misses.Should().Be(2);
        stats.HitRatio.Should().BeApproximately(0.6, 0.01);
        stats.MissRatio.Should().BeApproximately(0.4, 0.01);
        stats.CurrentEntries.Should().Be(2);
    }

    [Fact]
    public void GetStatistics_ReturnsEstimatedMemoryBytes()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.Store("key2", (Func<int, int>)(x => x * 2), TimeSpan.FromMinutes(5));

        // Act
        var stats = _cache.GetStatistics();

        // Assert
        stats.EstimatedMemoryBytes.Should().BeGreaterThan(0);
        stats.CurrentEntries.Should().Be(2);
    }

    [Fact]
    public void Clear_ResetsAllStatistics()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        _cache.GetCached("key1");
        _cache.GetCached("nonexistent");

        // Act
        _cache.Clear();
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.CurrentEntries.Should().Be(0);
        stats.EvictionCount.Should().Be(0);
        stats.EstimatedMemoryBytes.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_IsSnapshotInTime()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        var stats1 = _cache.GetStatistics();
        _cache.GetCached("key1");
        var stats2 = _cache.GetStatistics();

        // Assert
        stats1.Hits.Should().Be(0);
        stats2.Hits.Should().Be(1);
    }

    #endregion

    #region Cache Key Generation Tests

    [Fact]
    public void GenerateCacheKey_WithIdenticalInputs_ProducesSameKey()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options);

        // Assert
        key1.Should().Be(key2);
        key1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentOperationTypes_ProducesDifferentKeys()
    {
        // Arrange
        var graph1 = CreateTestOperationGraph(OperationType.Map);
        var graph2 = CreateTestOperationGraph(OperationType.Filter);
        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph1, metadata, options);
        var key2 = KernelCache.GenerateCacheKey(graph2, metadata, options);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentInputTypes_ProducesDifferentKeys()
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
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentResultTypes_ProducesDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata1 = CreateTestTypeMetadata(typeof(int), typeof(int));
        var metadata2 = CreateTestTypeMetadata(typeof(int), typeof(double));
        var options = CreateTestCompilationOptions();

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata1, options);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata2, options);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentBackends_ProducesDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options1 = CreateTestCompilationOptions(backend: ComputeBackend.Cpu);
        var options2 = CreateTestCompilationOptions(backend: ComputeBackend.Cuda);

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options1);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentOptimizationLevels_ProducesDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options1 = CreateTestCompilationOptions(optimizationLevel: OptimizationLevel.None);
        var options2 = CreateTestCompilationOptions(optimizationLevel: OptimizationLevel.Aggressive);

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options1);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentKernelFusionSettings_ProducesDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options1 = CreateTestCompilationOptions(enableKernelFusion: true);
        var options2 = CreateTestCompilationOptions(enableKernelFusion: false);

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options1);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentDebugInfoSettings_ProducesDifferentKeys()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();
        var options1 = CreateTestCompilationOptions(generateDebugInfo: true);
        var options2 = CreateTestCompilationOptions(generateDebugInfo: false);

        // Act
        var key1 = KernelCache.GenerateCacheKey(graph, metadata, options1);
        var key2 = KernelCache.GenerateCacheKey(graph, metadata, options2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithNullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act
        Action act = () => KernelCache.GenerateCacheKey(null!, metadata, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("graph");
    }

    [Fact]
    public void GenerateCacheKey_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var options = CreateTestCompilationOptions();

        // Act
        Action act = () => KernelCache.GenerateCacheKey(graph, null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadata");
    }

    [Fact]
    public void GenerateCacheKey_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = CreateTestOperationGraph();
        var metadata = CreateTestTypeMetadata();

        // Act
        Action act = () => KernelCache.GenerateCacheKey(graph, metadata, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void GenerateCacheKey_WithComplexOperationGraph_ProducesConsistentKeys()
    {
        // Arrange - Create graph with multiple operations and dependencies
        var operation1 = new Operation
        {
            Id = "op1",
            Type = OperationType.Map,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = new Dictionary<string, object>(),
            EstimatedCost = 1.0
        };

        var operation2 = new Operation
        {
            Id = "op2",
            Type = OperationType.Filter,
            Dependencies = new System.Collections.ObjectModel.Collection<string> { "op1" },
            Metadata = new Dictionary<string, object>(),
            EstimatedCost = 2.0
        };

        var operation3 = new Operation
        {
            Id = "op3",
            Type = OperationType.Reduce,
            Dependencies = new System.Collections.ObjectModel.Collection<string> { "op1", "op2" },
            Metadata = new Dictionary<string, object>(),
            EstimatedCost = 3.0
        };

        var graph = new OperationGraph
        {
            Operations = new System.Collections.ObjectModel.Collection<Operation> { operation1, operation2, operation3 },
            Root = operation3
        };

        var metadata = CreateTestTypeMetadata();
        var options = CreateTestCompilationOptions();

        // Act - Generate key multiple times
        var keys = Enumerable.Range(0, 5)
            .Select(_ => KernelCache.GenerateCacheKey(graph, metadata, options))
            .ToArray();

        // Assert - All keys should be identical
        keys.Should().AllBe(keys[0]);
    }

    #endregion

    #region Edge Cases and Parameter Validation

    [Fact]
    public void Constructor_WithZeroMaxEntries_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => new KernelCache(maxEntries: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxEntries")
            .WithMessage("*Must be at least 1*");
    }

    [Fact]
    public void Constructor_WithNegativeMaxEntries_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => new KernelCache(maxEntries: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxEntries");
    }

    [Fact]
    public void Constructor_WithZeroMaxMemory_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => new KernelCache(maxMemoryBytes: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxMemoryBytes")
            .WithMessage("*Must be at least 1*");
    }

    [Fact]
    public void Constructor_WithNegativeMaxMemory_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => new KernelCache(maxMemoryBytes: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxMemoryBytes");
    }

    [Fact]
    public void Store_WithNullKey_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _cache.Store(null!, (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public void Store_WithNullDelegate_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _cache.Store("key", null!, TimeSpan.FromMinutes(5));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("compiled");
    }

    [Fact]
    public void Store_WithNegativeTTL_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => _cache.Store("key", (Func<int, int>)(x => x), TimeSpan.FromMinutes(-1));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ttl");
    }

    [Fact]
    public void GetCached_WithNullKey_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _cache.GetCached(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public void Remove_WithNullKey_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _cache.Remove(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public void Store_WithEmptyKey_StoresSuccessfully()
    {
        // Arrange
        Func<int, int> testDelegate = x => x * 2;

        // Act & Assert - Empty string is valid key
        _cache.Store(string.Empty, testDelegate, TimeSpan.FromMinutes(5));
        _cache.GetCached(string.Empty).Should().BeSameAs(testDelegate);
    }

    [Fact]
    public void Store_WithVeryLongKey_StoresSuccessfully()
    {
        // Arrange
        var longKey = new string('a', 10000);
        Func<int, int> testDelegate = x => x * 2;

        // Act
        _cache.Store(longKey, testDelegate, TimeSpan.FromMinutes(5));

        // Assert
        _cache.GetCached(longKey).Should().BeSameAs(testDelegate);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var cache = new KernelCache();

        // Act & Assert - Should not throw
        cache.Dispose();
        cache.Dispose();
        cache.Dispose();
    }

    [Fact]
    public void Dispose_ClearsAllCachedDelegates()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        cache.Store("key2", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        cache.Dispose();

        // Assert
        Action act1 = () => cache.GetCached("key1");
        Action act2 = () => cache.GetStatistics();

        act1.Should().Throw<ObjectDisposedException>();
        act2.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Store_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Dispose();

        // Act
        Action act = () => cache.Store("key", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetCached_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Store("key", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));
        cache.Dispose();

        // Act
        Action act = () => cache.GetCached("key");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Remove_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Dispose();

        // Act
        Action act = () => cache.Remove("key");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Clear_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Dispose();

        // Act
        Action act = () => cache.Clear();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetStatistics_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new KernelCache();
        cache.Dispose();

        // Act
        Action act = () => cache.GetStatistics();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public void Store_WithLargeDelegates_RespectsMemoryLimit()
    {
        // Arrange - Create cache with very small memory limit
        using var memoryConstrainedCache = new KernelCache(
            maxEntries: 1000,
            maxMemoryBytes: 1024); // 1KB

        // Act - Store many delegates (should trigger memory-based eviction)
        for (var i = 0; i < 50; i++)
        {
            memoryConstrainedCache.Store($"key{i}", (Func<int, int>)(x => x * i), TimeSpan.FromMinutes(5));
        }

        var stats = memoryConstrainedCache.GetStatistics();

        // Assert - Memory limit should be respected
        stats.EstimatedMemoryBytes.Should().BeLessThanOrEqualTo(1024 * 2); // Allow some overhead
        stats.EvictionCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetStatistics_ReturnsReasonableMemoryEstimates()
    {
        // Arrange
        _cache.Store("key1", (Func<int, int>)(x => x), TimeSpan.FromMinutes(5));

        // Act
        var stats = _cache.GetStatistics();

        // Assert - Estimates should be in reasonable range (64-1024 bytes per delegate)
        stats.EstimatedMemoryBytes.Should().BeInRange(64, 1024);
    }

    #endregion

    #region Performance and Load Tests

    [Fact]
    public void Store_WithManyEntries_MaintainsPerformance()
    {
        // Arrange
        const int entryCount = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Store many entries
        for (var i = 0; i < entryCount; i++)
        {
            _cache.Store($"key{i}", (Func<int, int>)(x => x * i), TimeSpan.FromMinutes(5));
        }

        stopwatch.Stop();

        // Assert - Should complete reasonably fast (< 500ms for 1000 entries)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);

        var stats = _cache.GetStatistics();
        stats.CurrentEntries.Should().BeLessThanOrEqualTo(DefaultMaxEntries);
    }

    [Fact]
    public void GetCached_UnderHighLoad_MaintainsPerformance()
    {
        // Arrange - Populate cache
        for (var i = 0; i < 100; i++)
        {
            _cache.Store($"key{i}", (Func<int, int>)(x => x * i), TimeSpan.FromMinutes(5));
        }

        var stopwatch = Stopwatch.StartNew();
        const int readCount = 10000;

        // Act - Perform many reads
        for (var i = 0; i < readCount; i++)
        {
            _cache.GetCached($"key{i % 100}");
        }

        stopwatch.Stop();

        // Assert - Should complete reasonably fast (< 100ms for 10,000 reads)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);

        var stats = _cache.GetStatistics();
        stats.Hits.Should().Be(readCount);
    }

    #endregion

    #region Helper Methods

    private static OperationGraph CreateTestOperationGraph(OperationType operationType = OperationType.Map)
    {
        var operation = new Operation
        {
            Id = "test-operation",
            Type = operationType,
            Dependencies = new System.Collections.ObjectModel.Collection<string>(),
            Metadata = new Dictionary<string, object>(),
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
        DotCompute.Linq.Compilation.ComputeBackend backend = DotCompute.Linq.Compilation.ComputeBackend.Cpu,
        OptimizationLevel optimizationLevel = OptimizationLevel.Balanced,
        bool enableKernelFusion = true,
        bool generateDebugInfo = false)
    {
        return new CompilationOptions
        {
            TargetBackend = backend,
            OptimizationLevel = optimizationLevel,
            EnableKernelFusion = enableKernelFusion,
            GenerateDebugInfo = generateDebugInfo,
            CacheTtl = TimeSpan.FromMinutes(30)
        };
    }

    #endregion
}
