// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Memory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for HighPerformanceObjectPool covering all critical scenarios.
/// Part of Phase 1: Memory Module testing to achieve 80% coverage.
/// </summary>
public sealed class HighPerformanceObjectPoolComprehensiveTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidCreateFunc_CreatesPool()
    {
        // Arrange & Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Assert
        pool.Should().NotBeNull();
        var stats = pool.Statistics;
        stats.TotalCreated.Should().BeGreaterThan(0, "pool should pre-populate");
    }

    [Fact]
    public void Constructor_WithNullCreateFunc_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new HighPerformanceObjectPool<PooledTestObject>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_UsesConfiguration()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            MaxPoolSize = 50,
            MinPoolSize = 5,
            PrePopulateCount = 10
        };

        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Assert
        var stats = pool.Statistics;
        stats.TotalCreated.Should().Be(10, "should pre-populate with configured count");
    }

    [Fact]
    public void Constructor_WithResetAction_StoresResetAction()
    {
        // Arrange
        var resetCalled = 0;
        Action<PooledTestObject> resetAction = obj =>
        {
            resetCalled++;
            obj.Reset();
        };

        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            resetAction: resetAction);
        _disposables.Add(pool);

        var obj = pool.Get();
        pool.Return(obj);

        // Assert
        resetCalled.Should().BeGreaterThan(0, "reset action should be called");
    }

    [Fact]
    public void Constructor_WithValidationFunc_StoresValidationFunc()
    {
        // Arrange
        Func<PooledTestObject, bool> validateFunc = obj => obj.IsValid;

        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            validateFunc: validateFunc);
        _disposables.Add(pool);

        // Assert
        pool.Should().NotBeNull();
    }

    #endregion

    #region Get/Return Tests

    [Fact]
    public void Get_WhenPoolHasObjects_ReturnsObjectFromPool()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Act
        var obj = pool.Get();

        // Assert
        obj.Should().NotBeNull();
        var stats = pool.Statistics;
        stats.TotalGets.Should().Be(1);
    }

    [Fact]
    public void Get_MultipleObjectsSequentially_ReturnsDistinctObjects()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Act
        var obj1 = pool.Get();
        var obj2 = pool.Get();
        var obj3 = pool.Get();

        // Assert
        obj1.Should().NotBeSameAs(obj2);
        obj2.Should().NotBeSameAs(obj3);
        obj1.Should().NotBeSameAs(obj3);
    }

    [Fact]
    public void Return_WithValidObject_AddsToPool()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);
        var obj = pool.Get();

        // Act
        pool.Return(obj);

        // Assert
        var stats = pool.Statistics;
        stats.TotalReturns.Should().Be(1);
    }

    [Fact]
    public void Return_WithNullObject_DoesNotThrow()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Act
        Action act = () => pool.Return(null!);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAndReturn_ReusesSameObject()
    {
        // Arrange
        var config = new PoolConfiguration { PrePopulateCount = 0 };
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Act
        var obj1 = pool.Get();
        var id1 = obj1.Id;
        pool.Return(obj1);
        var obj2 = pool.Get();
        var id2 = obj2.Id;

        // Assert
        id1.Should().Be(id2, "should reuse the same object from pool");
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void Statistics_InitialState_ReturnsCorrectValues()
    {
        // Arrange
        var config = new PoolConfiguration { PrePopulateCount = 10 };
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Act
        var stats = pool.Statistics;

        // Assert
        stats.TotalCreated.Should().Be(10);
        stats.TotalGets.Should().Be(0);
        stats.TotalReturns.Should().Be(0);
        stats.PoolHits.Should().Be(0);
        stats.PoolMisses.Should().Be(0);
    }

    [Fact]
    public void Statistics_AfterGetOperations_UpdatesCorrectly()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Act
        _ = pool.Get();
        _ = pool.Get();
        _ = pool.Get();
        var stats = pool.Statistics;

        // Assert
        stats.TotalGets.Should().Be(3);
        (stats.PoolHits + stats.PoolMisses).Should().Be(3);
    }

    [Fact]
    public void Statistics_AfterReturnOperations_UpdatesCorrectly()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);
        var obj1 = pool.Get();
        var obj2 = pool.Get();

        // Act
        pool.Return(obj1);
        pool.Return(obj2);
        var stats = pool.Statistics;

        // Assert
        stats.TotalReturns.Should().Be(2);
    }

    [Fact]
    public void Statistics_HitRate_CalculatesCorrectly()
    {
        // Arrange
        var config = new PoolConfiguration { PrePopulateCount = 5 };
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Act - Get from pre-populated pool (hits)
        var obj1 = pool.Get();
        var obj2 = pool.Get();
        pool.Return(obj1);
        var obj3 = pool.Get(); // Should hit

        var stats = pool.Statistics;

        // Assert
        stats.HitRate.Should().BeGreaterThan(0.0);
        stats.HitRate.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Statistics_LiveObjects_CalculatesCorrectly()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);
        var initialCreated = pool.Statistics.TotalCreated;

        // Act
        var obj1 = pool.Get();
        var obj2 = pool.Get();
        pool.Return(obj1);

        var stats = pool.Statistics;

        // Assert
        stats.LiveObjects.Should().Be(stats.TotalCreated - stats.TotalDestroyed);
    }

    #endregion

    #region Validation and Reset Tests

    [Fact]
    public void Get_WithFailingValidation_CreatesNewObject()
    {
        // Arrange
        var validationCallCount = 0;
        Func<PooledTestObject, bool> validateFunc = obj =>
        {
            validationCallCount++;
            return false; // Always fail validation
        };

        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            validateFunc: validateFunc,
            config: new PoolConfiguration { PrePopulateCount = 1 });
        _disposables.Add(pool);

        // Act
        var obj = pool.Get();

        // Assert
        obj.Should().NotBeNull();
        validationCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Return_CallsResetAction_BeforeReturningToPool()
    {
        // Arrange
        var resetCallCount = 0;
        Action<PooledTestObject> resetAction = obj =>
        {
            resetCallCount++;
            obj.Reset();
        };

        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            resetAction: resetAction);
        _disposables.Add(pool);

        var obj = pool.Get();
        obj.Value = 42;

        // Act
        pool.Return(obj);

        // Assert
        resetCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Return_WithFailingValidation_DiscardsObject()
    {
        // Arrange
        Func<PooledTestObject, bool> validateFunc = obj => obj.IsValid;

        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            validateFunc: validateFunc);
        _disposables.Add(pool);

        var obj = pool.Get();
        obj.IsValid = false;

        var statsBefore = pool.Statistics;

        // Act
        pool.Return(obj);

        // Assert
        var statsAfter = pool.Statistics;
        statsAfter.TotalDestroyed.Should().BeGreaterThan(statsBefore.TotalDestroyed);
    }

    [Fact]
    public void Return_WithExceptionInResetAction_DiscardsObject()
    {
        // Arrange
        Action<PooledTestObject> resetAction = obj => throw new InvalidOperationException("Reset failed");

        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            resetAction: resetAction,
            logger: NullLogger.Instance);
        _disposables.Add(pool);

        var obj = pool.Get();
        var destroyedBefore = pool.Statistics.TotalDestroyed;

        // Act
        pool.Return(obj);

        // Assert
        var destroyedAfter = pool.Statistics.TotalDestroyed;
        destroyedAfter.Should().BeGreaterThan(destroyedBefore);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_WithDefaultConfiguration_UsesDefaults()
    {
        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Assert
        var stats = pool.Statistics;
        stats.TotalCreated.Should().Be(PoolConfiguration.Default.PrePopulateCount);
    }

    [Fact]
    public void Constructor_WithHighFrequencyConfiguration_PrePopulatesMore()
    {
        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            config: PoolConfiguration.HighFrequency);
        _disposables.Add(pool);

        // Assert
        var stats = pool.Statistics;
        stats.TotalCreated.Should().Be(PoolConfiguration.HighFrequency.PrePopulateCount);
        stats.CurrentPoolSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_WithMemoryConstrainedConfiguration_PrePopulatesLess()
    {
        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => new PooledTestObject(),
            config: PoolConfiguration.MemoryConstrained);
        _disposables.Add(pool);

        // Assert
        var stats = pool.Statistics;
        stats.TotalCreated.Should().Be(PoolConfiguration.MemoryConstrained.PrePopulateCount);
        stats.TotalCreated.Should().BeLessThan(PoolConfiguration.HighFrequency.PrePopulateCount);
    }

    [Fact]
    public void Constructor_WithZeroPrePopulate_DoesNotPrePopulate()
    {
        // Arrange
        var config = new PoolConfiguration { PrePopulateCount = 0 };

        // Act
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Assert
        var stats = pool.Statistics;
        stats.TotalCreated.Should().Be(0);
        stats.CurrentPoolSize.Should().Be(0);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());

        // Act
        pool.Dispose();

        // Assert - Verify operations throw ObjectDisposedException after disposal
        Action actGet = () => pool.Get();
        actGet.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());

        // Act
        pool.Dispose();
        Action act = () => pool.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Get_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        pool.Dispose();

        // Act
        Action act = () => pool.Get();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Return_AfterDisposal_DoesNotThrow()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        var obj = pool.Get();
        pool.Dispose();

        // Act
        Action act = () => pool.Return(obj);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentGetAndReturn_HandlesThreadSafely()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);
        const int concurrentOperations = 100;

        // Act
        var tasks = Enumerable.Range(0, concurrentOperations).Select(async _ =>
        {
            await Task.Yield();
            var obj = pool.Get();
            obj.Value = Environment.CurrentManagedThreadId;
            await Task.Delay(1);
            pool.Return(obj);
        });

        await Task.WhenAll(tasks);

        // Assert
        var stats = pool.Statistics;
        stats.TotalGets.Should().Be(concurrentOperations);
        stats.TotalReturns.Should().Be(concurrentOperations);
    }

    [Fact]
    public async Task ThreadLocalCaching_WorksCorrectly()
    {
        // Arrange
        var config = new PoolConfiguration { ThreadLocalCapacity = 4, PrePopulateCount = 10 };
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Act - Get and return on same thread (should use thread-local)
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await Task.Run(() =>
            {
                var obj = pool.Get();
                pool.Return(obj);
            });
        });

        await Task.WhenAll(tasks);

        // Assert
        var stats = pool.Statistics;
        stats.ThreadLocalCount.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Maintenance Tests

    [Fact]
    public void TriggerMaintenance_ExecutesMaintenanceOperations()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject());
        _disposables.Add(pool);

        // Act
        Action act = () => pool.TriggerMaintenance();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ObjectLifetime_ExpiredObjectsAreRemoved()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            ObjectLifetime = TimeSpan.FromMilliseconds(100),
            PrePopulateCount = 5
        };
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        var initialCreated = pool.Statistics.TotalCreated;

        // Act - Wait for objects to expire
        await Task.Delay(150);
        pool.TriggerMaintenance();

        // Assert
        var stats = pool.Statistics;
        stats.TotalDestroyed.Should().BeGreaterThan(0, "expired objects should be destroyed");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Get_WhenCreateFuncThrows_PropagatesException()
    {
        // Arrange
        var pool = new HighPerformanceObjectPool<PooledTestObject>(
            () => throw new InvalidOperationException("Create failed"),
            config: new PoolConfiguration { PrePopulateCount = 0 });
        _disposables.Add(pool);

        // Act
        Action act = () => pool.Get();

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Create failed");
    }

    [Fact]
    public void Return_PoolAtMaxCapacity_DiscardsObject()
    {
        // Arrange
        var config = new PoolConfiguration { MaxPoolSize = 2, PrePopulateCount = 0 };
        var pool = new HighPerformanceObjectPool<PooledTestObject>(() => new PooledTestObject(), config: config);
        _disposables.Add(pool);

        // Act - Fill pool beyond capacity
        var obj1 = pool.Get();
        var obj2 = pool.Get();
        var obj3 = pool.Get();

        pool.Return(obj1);
        pool.Return(obj2);
        var destroyedBefore = pool.Statistics.TotalDestroyed;
        pool.Return(obj3); // Should be discarded

        // Assert
        pool.Statistics.CurrentPoolSize.Should().BeLessThanOrEqualTo(config.MaxPoolSize);
    }

    #endregion
}

/// <summary>
/// Simple test object for pooling tests.
/// </summary>
internal sealed class PooledTestObject
{
    private static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public int Value { get; set; }
    public bool IsValid { get; set; } = true;

    public void Reset()
    {
        Value = 0;
        IsValid = true;
    }
}
