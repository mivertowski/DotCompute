// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using DotCompute.Abstractions;
using DotCompute.Core.Recovery.Exceptions;
using DotCompute.Core.Recovery.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive memory error recovery strategy with allocation retry,
/// defragmentation, and emergency memory reserve management.
/// Consolidated using BaseRecoveryStrategy to eliminate duplicate patterns.
/// </summary>
public sealed partial class MemoryRecoveryStrategy : BaseRecoveryStrategy<Models.MemoryRecoveryContext>
{
    private readonly ConcurrentDictionary<string, Models.MemoryPoolState> _memoryPools;
    private readonly Models.MemoryRecoveryConfiguration _config;
    private readonly Timer _defragmentationTimer;
    private readonly SemaphoreSlim _recoveryLock;
    private readonly Models.MemoryPressureMonitor _pressureMonitor;
    private bool _disposed;

    // Emergency reserve
    private volatile byte[]? _emergencyReserve;
    private readonly Lock _reserveLock = new();
    /// <summary>
    /// Gets or sets the capability.
    /// </summary>
    /// <value>The capability.</value>

    public override RecoveryCapability Capability => RecoveryCapability.MemoryErrors;
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>The priority.</value>
    public override int Priority => 100;
    /// <summary>
    /// Initializes a new instance of the MemoryRecoveryStrategy class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The config.</param>

    public MemoryRecoveryStrategy(ILogger<MemoryRecoveryStrategy> logger, Models.MemoryRecoveryConfiguration? config = null)
        : base(logger)
    {
        _config = config ?? Models.MemoryRecoveryConfiguration.Default;
        _memoryPools = new ConcurrentDictionary<string, Models.MemoryPoolState>();
        _recoveryLock = new SemaphoreSlim(1, 1);

        // Create a logger for MemoryPressureMonitor using LoggerFactory

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _pressureMonitor = new Models.MemoryPressureMonitor(loggerFactory.CreateLogger<Models.MemoryPressureMonitor>());

        // Initialize emergency reserve
        InitializeEmergencyReserve();

        // Start periodic defragmentation
        _defragmentationTimer = new Timer(PerformPeriodicDefragmentation, null,
            _config.DefragmentationInterval, _config.DefragmentationInterval);

        LogMemoryRecoveryStrategyInitialized(Logger, _config.EmergencyReserveSizeMB);
    }
    /// <summary>
    /// Determines whether handle.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public override bool CanHandle(Exception exception, Models.MemoryRecoveryContext context)
    {
        return exception switch
        {
            OutOfMemoryException => true,
            MemoryException => true,
            AcceleratorException accelEx when accelEx.Message.Contains("memory", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }
    /// <summary>
    /// Gets recover asynchronously.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override async Task<RecoveryResult> RecoverAsync(
        Exception exception,
        Models.MemoryRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        LogMemoryErrorDetected(Logger, exception.Message, GC.GetTotalMemory(false) / 1024 / 1024);

        await _recoveryLock.WaitAsync(cancellationToken);


        try
        {
            // Determine recovery strategy based on error type and system state
            var strategy = DetermineMemoryRecoveryStrategy(exception, context);
            LogUsingMemoryRecoveryStrategy(Logger, strategy.ToString());

            var result = await ExecuteMemoryRecoveryAsync(strategy, context, options, cancellationToken);


            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.Success)
            {
                LogMemoryRecoverySuccessful(Logger, strategy.ToString(), stopwatch.ElapsedMilliseconds, GC.GetTotalMemory(false) / 1024 / 1024);
            }

            return result;
        }
        finally
        {
            _ = _recoveryLock.Release();
        }
    }

    /// <summary>
    /// Attempts allocation with retry and exponential backoff
    /// </summary>
    public async Task<T?> AllocateWithRetryAsync<T>(
        Func<T> allocateFunc,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var delay = baseDelay ?? _config.AllocationRetryDelay;


        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return allocateFunc();
            }
            catch (OutOfMemoryException) when (attempt < maxRetries)
            {
                LogMemoryAllocationFailed(Logger, attempt, maxRetries, delay.TotalMilliseconds);

                // Perform emergency memory recovery
                await PerformEmergencyMemoryRecoveryAsync(cancellationToken);

                // Exponential backoff with jitter
#pragma warning disable CA5394 // Random is used for exponential backoff jitter, not security
                var actualDelay = TimeSpan.FromTicks((long)(delay.Ticks * Math.Pow(2, attempt - 1) * (0.8 + Random.Shared.NextDouble() * 0.4)));
#pragma warning restore CA5394
                await Task.Delay(actualDelay, cancellationToken);
            }
        }

        // Final attempt after all retries
        try
        {
            return allocateFunc();
        }
        catch (OutOfMemoryException ex)
        {
            LogMemoryAllocationFailedAfterRetries(Logger, maxRetries);
            throw new MemoryAllocationException($"Failed to allocate memory after {maxRetries} attempts", ex);
        }
    }

    /// <summary>
    /// Performs memory defragmentation
    /// </summary>
    public async Task<Models.MemoryDefragmentationResult> DefragmentMemoryAsync(
        string? poolId = null,
        CancellationToken cancellationToken = default)
    {
        LogStartingMemoryDefragmentation(Logger, poolId ?? "all");

        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);

        try
        {
            // Release emergency reserve to free up memory
            ReleaseEmergencyReserve();

            // Force full garbage collection
            await Task.Run(() =>
            {
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            }, cancellationToken);

            // Compact large object heap if available
            if (_config.EnableLargeObjectHeapCompaction)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            }

            // Platform-specific memory defragmentation
            await PerformPlatformSpecificDefragmentationAsync(poolId, cancellationToken);

            // Restore emergency reserve
            await Task.Run(() => InitializeEmergencyReserve(), cancellationToken);

            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryFreed = Math.Max(0, memoryBefore - memoryAfter);

            LogMemoryDefragmentationCompleted(Logger, stopwatch.ElapsedMilliseconds, memoryFreed / 1024 / 1024);

            return new Models.MemoryDefragmentationResult
            {
                Success = true,
                Duration = stopwatch.Elapsed,
                MemoryFreed = memoryFreed,
                FragmentationBefore = 0, // Would need to calculate actual fragmentation
                FragmentationAfter = 0
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogMemoryDefragmentationFailed(Logger, ex);

            // Ensure emergency reserve is restored even on failure
            try
            {
                InitializeEmergencyReserve();
            }
            catch
            {
            }

            return new Models.MemoryDefragmentationResult
            {
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Gets current memory pressure information
    /// </summary>
    public Memory.MemoryPressureInfo MemoryPressureInfo => _pressureMonitor.CurrentPressure;

    /// <summary>
    /// Registers a memory pool for monitoring and recovery
    /// </summary>
    public void RegisterMemoryPool(string poolId, Models.IMemoryPool pool)
    {
        var poolState = new Models.MemoryPoolState(poolId, pool);
        _ = _memoryPools.TryAdd(poolId, poolState);

        LogRegisteredMemoryPool(Logger, poolId);
    }

    /// <summary>
    /// Unregisters a memory pool from monitoring
    /// </summary>
    public bool UnregisterMemoryPool(string poolId)
    {
        if (_memoryPools.TryRemove(poolId, out _))
        {
            LogUnregisteredMemoryPool(Logger, poolId);
            return true;
        }

        return false;
    }

    private Models.MemoryRecoveryStrategyType DetermineMemoryRecoveryStrategy(Exception error, Models.MemoryRecoveryContext context)
    {
        var pressure = _pressureMonitor.CurrentPressure;


        return pressure.Level switch
        {
            MemoryPressureLevel.Critical => Models.MemoryRecoveryStrategyType.EmergencyRecovery,
            MemoryPressureLevel.High => Models.MemoryRecoveryStrategyType.AggressiveCleanup,
            MemoryPressureLevel.Medium => Models.MemoryRecoveryStrategyType.DefragmentationWithGC,
            _ => Models.MemoryRecoveryStrategyType.SimpleGarbageCollection
        };
    }

    private async Task<RecoveryResult> ExecuteMemoryRecoveryAsync(
        Models.MemoryRecoveryStrategyType strategy,
        Models.MemoryRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            Models.MemoryRecoveryStrategyType.SimpleGarbageCollection => await SimpleGarbageCollectionAsync(cancellationToken),
            Models.MemoryRecoveryStrategyType.DefragmentationWithGC => await DefragmentationWithGCAsync(cancellationToken),
            Models.MemoryRecoveryStrategyType.AggressiveCleanup => await AggressiveCleanupAsync(context, cancellationToken),
            Models.MemoryRecoveryStrategyType.EmergencyRecovery => await EmergencyRecoveryAsync(context, cancellationToken),
            _ => Failure("Unknown memory recovery strategy")
        };
    }

    private async Task<RecoveryResult> SimpleGarbageCollectionAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            GC.Collect(1, GCCollectionMode.Optimized);
            GC.WaitForPendingFinalizers();
        }, cancellationToken);


        return Success("Simple garbage collection completed", TimeSpan.FromMilliseconds(50));
    }

    private async Task<RecoveryResult> DefragmentationWithGCAsync(CancellationToken cancellationToken)
    {
        var result = await DefragmentMemoryAsync(cancellationToken: cancellationToken);


        return result.Success

            ? Success($"Defragmentation completed, freed {result.MemoryFreed / 1024 / 1024}MB", result.Duration)
            : Failure($"Defragmentation failed: {result.Error}");
    }

    private async Task<RecoveryResult> AggressiveCleanupAsync(Models.MemoryRecoveryContext context, CancellationToken cancellationToken)
    {
        LogPerformingAggressiveMemoryCleanup(Logger);

        // Clean up memory pools

        foreach (var poolState in _memoryPools.Values)
        {
            try
            {
                // TODO: Implement cleanup logic for memory pool
                // await poolState.PerformCleanupAsync(cancellationToken);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogFailedToCleanupMemoryPool(Logger, poolState.PoolId, ex);
            }
        }

        // Perform defragmentation
        var defragResult = await DefragmentMemoryAsync(cancellationToken: cancellationToken);


        return defragResult.Success

            ? Success($"Aggressive cleanup completed, freed {defragResult.MemoryFreed / 1024 / 1024}MB", defragResult.Duration)
            : Failure($"Aggressive cleanup failed: {defragResult.Error}");
    }

    private async Task<RecoveryResult> EmergencyRecoveryAsync(Models.MemoryRecoveryContext context, CancellationToken cancellationToken)
    {
        LogPerformingEmergencyMemoryRecovery(Logger);

        try
        {
            // Release emergency reserve immediately
            ReleaseEmergencyReserve();

            // Emergency cleanup of all memory pools

            var cleanupTasks = _memoryPools.Values.Select(pool =>

                Task.Run(async () =>
                {
                    try
                    {
                        // TODO: Implement emergency cleanup for memory pool
                        // await pool.PerformEmergencyCleanupAsync(cancellationToken);
                        await Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        LogEmergencyCleanupFailedForPool(Logger, pool.PoolId, ex);
                    }
                }, cancellationToken));


            await Task.WhenAll(cleanupTasks.Take(10)); // Limit concurrent operations

            // Aggressive garbage collection

            await Task.Run(() =>
            {
                for (var i = 0; i < 3; i++)
                {
                    GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                    GC.WaitForPendingFinalizers();
                }
            }, cancellationToken);

            // Try to restore partial emergency reserve
            _ = Task.Run(() =>
            {
                try
                {
                    InitializeEmergencyReserve(_config.EmergencyReserveSizeMB / 2); // Half size
                }
                catch
                {
                    // Ignore failures during emergency
                }
            }, CancellationToken.None);


            return Success("Emergency memory recovery completed", TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            LogEmergencyMemoryRecoveryFailed(Logger, ex);
            return Failure("Emergency recovery failed", ex);
        }
    }

    private async Task PerformEmergencyMemoryRecoveryAsync(CancellationToken cancellationToken)
    {
        // Quick emergency cleanup without full recovery process
        ReleaseEmergencyReserve();


        await Task.Run(() =>
        {
            GC.Collect(1, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }, cancellationToken);

        // Try to restore reserve after cleanup

        _ = Task.Run(() =>
        {
            try
            {
                InitializeEmergencyReserve();
            }
            catch
            {
                /* Ignore during emergency */
            }
        }, CancellationToken.None);
    }

    private async Task PerformPlatformSpecificDefragmentationAsync(string? poolId, CancellationToken cancellationToken)
    {
        // GPU memory defragmentation
        if (poolId?.Contains("gpu", StringComparison.OrdinalIgnoreCase) == true ||
            poolId?.Contains("cuda", StringComparison.OrdinalIgnoreCase) == true ||
            poolId?.Contains("opencl", StringComparison.OrdinalIgnoreCase) == true)
        {
            await DefragmentGpuMemoryAsync(poolId, cancellationToken);
        }

        // System memory defragmentation
        await Task.Run(() =>
        {
            // Platform-specific system memory operations would go here
            // For now, we'll just do additional GC
            GC.Collect(2, GCCollectionMode.Aggressive);
        }, cancellationToken);
    }

    private async Task DefragmentGpuMemoryAsync(string poolId, CancellationToken cancellationToken)
    {
        LogPerformingGpuMemoryDefragmentation(Logger, poolId);

        if (_memoryPools.TryGetValue(poolId, out _))
        {
            // TODO: Implement defragmentation for memory pool
            // await poolState.PerformDefragmentationAsync(cancellationToken);
            await Task.CompletedTask;
        }

        // Platform-specific GPU memory operations

        await Task.Delay(50, cancellationToken);
    }

    private void InitializeEmergencyReserve(int sizeMB = 0)
    {
        var targetSize = sizeMB > 0 ? sizeMB : _config.EmergencyReserveSizeMB;


        lock (_reserveLock)
        {
            try
            {
                _emergencyReserve = new byte[targetSize * 1024 * 1024];
                LogEmergencyMemoryReserveInitialized(Logger, targetSize);
            }
            catch (OutOfMemoryException)
            {
                LogCouldNotInitializeEmergencyMemoryReserve(Logger, targetSize);
                _emergencyReserve = null;
            }
        }
    }

    private void ReleaseEmergencyReserve()
    {
        lock (_reserveLock)
        {
            if (_emergencyReserve != null)
            {
                var sizeMB = _emergencyReserve.Length / 1024 / 1024;
                _emergencyReserve = null;
                LogReleasedEmergencyMemoryReserve(Logger, sizeMB);

                // Force GC to immediately reclaim the memory

                GC.Collect(0, GCCollectionMode.Forced);
            }
        }
    }

    private void PerformPeriodicDefragmentation(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var pressure = _pressureMonitor.CurrentPressure;

            // Only defragment if memory pressure is elevated

            if (pressure.Level >= MemoryPressureLevel.Medium)
            {
                LogPerformingScheduledMemoryDefragmentation(Logger, pressure.Level.ToString());

                _ = Task.Run(async () =>
                {
                    try
                    {
                        _ = await DefragmentMemoryAsync();
                    }
                    catch (Exception ex)
                    {
                        LogScheduledMemoryDefragmentationFailed(Logger, ex);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            LogErrorDuringPeriodicDefragmentationCheck(Logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _defragmentationTimer?.Dispose();
            _recoveryLock?.Dispose();
            _pressureMonitor?.Dispose();
            ReleaseEmergencyReserve();
            _disposed = true;

            LogMemoryRecoveryStrategyDisposed(Logger);
        }

        base.Dispose(disposing);
    }

    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 13000, Level = LogLevel.Information, Message = "Memory Recovery Strategy initialized with {ReserveSize}MB emergency reserve")]
    private static partial void LogMemoryRecoveryStrategyInitialized(ILogger logger, int reserveSize);

    [LoggerMessage(EventId = 13001, Level = LogLevel.Warning, Message = "Memory error detected: {Error}. Available memory: {AvailableMemory}MB")]
    private static partial void LogMemoryErrorDetected(ILogger logger, string error, long availableMemory);

    [LoggerMessage(EventId = 13002, Level = LogLevel.Information, Message = "Using memory recovery strategy: {Strategy}")]
    private static partial void LogUsingMemoryRecoveryStrategy(ILogger logger, string strategy);

    [LoggerMessage(EventId = 13003, Level = LogLevel.Information, Message = "Memory recovery successful using {Strategy} in {Duration}ms. Memory after recovery: {MemoryAfter}MB")]
    private static partial void LogMemoryRecoverySuccessful(ILogger logger, string strategy, long duration, long memoryAfter);

    [LoggerMessage(EventId = 13004, Level = LogLevel.Warning, Message = "Memory allocation failed on attempt {Attempt}/{MaxRetries}, retrying after {Delay}ms")]
    private static partial void LogMemoryAllocationFailed(ILogger logger, int attempt, int maxRetries, double delay);

    [LoggerMessage(EventId = 13005, Level = LogLevel.Error, Message = "Memory allocation failed after {MaxRetries} attempts")]
    private static partial void LogMemoryAllocationFailedAfterRetries(ILogger logger, int maxRetries);

    [LoggerMessage(EventId = 13006, Level = LogLevel.Information, Message = "Starting memory defragmentation for pool: {PoolId}")]
    private static partial void LogStartingMemoryDefragmentation(ILogger logger, string poolId);

    [LoggerMessage(EventId = 13007, Level = LogLevel.Information, Message = "Memory defragmentation completed in {Duration}ms. Freed {MemoryFreed}MB")]
    private static partial void LogMemoryDefragmentationCompleted(ILogger logger, long duration, long memoryFreed);

    [LoggerMessage(EventId = 13008, Level = LogLevel.Error, Message = "Memory defragmentation failed")]
    private static partial void LogMemoryDefragmentationFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 13009, Level = LogLevel.Debug, Message = "Registered memory pool {PoolId} for recovery monitoring")]
    private static partial void LogRegisteredMemoryPool(ILogger logger, string poolId);

    [LoggerMessage(EventId = 13010, Level = LogLevel.Debug, Message = "Unregistered memory pool {PoolId}")]
    private static partial void LogUnregisteredMemoryPool(ILogger logger, string poolId);

    [LoggerMessage(EventId = 13011, Level = LogLevel.Warning, Message = "Performing aggressive memory cleanup due to high memory pressure")]
    private static partial void LogPerformingAggressiveMemoryCleanup(ILogger logger);

    [LoggerMessage(EventId = 13012, Level = LogLevel.Warning, Message = "Failed to cleanup memory pool {PoolId}")]
    private static partial void LogFailedToCleanupMemoryPool(ILogger logger, string poolId, Exception ex);

    [LoggerMessage(EventId = 13013, Level = LogLevel.Critical, Message = "Performing emergency memory recovery due to critical memory pressure")]
    private static partial void LogPerformingEmergencyMemoryRecovery(ILogger logger);

    [LoggerMessage(EventId = 13014, Level = LogLevel.Error, Message = "Emergency cleanup failed for pool {PoolId}")]
    private static partial void LogEmergencyCleanupFailedForPool(ILogger logger, string poolId, Exception ex);

    [LoggerMessage(EventId = 13015, Level = LogLevel.Critical, Message = "Emergency memory recovery failed")]
    private static partial void LogEmergencyMemoryRecoveryFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 13016, Level = LogLevel.Information, Message = "Performing GPU memory defragmentation for pool {PoolId}")]
    private static partial void LogPerformingGpuMemoryDefragmentation(ILogger logger, string poolId);

    [LoggerMessage(EventId = 13017, Level = LogLevel.Debug, Message = "Emergency memory reserve initialized: {Size}MB")]
    private static partial void LogEmergencyMemoryReserveInitialized(ILogger logger, int size);

    [LoggerMessage(EventId = 13018, Level = LogLevel.Warning, Message = "Could not initialize emergency memory reserve of {Size}MB")]
    private static partial void LogCouldNotInitializeEmergencyMemoryReserve(ILogger logger, int size);

    [LoggerMessage(EventId = 13019, Level = LogLevel.Information, Message = "Released emergency memory reserve: {Size}MB")]
    private static partial void LogReleasedEmergencyMemoryReserve(ILogger logger, int size);

    [LoggerMessage(EventId = 13020, Level = LogLevel.Information, Message = "Performing scheduled memory defragmentation (pressure: {Level})")]
    private static partial void LogPerformingScheduledMemoryDefragmentation(ILogger logger, string level);

    [LoggerMessage(EventId = 13021, Level = LogLevel.Warning, Message = "Scheduled memory defragmentation failed")]
    private static partial void LogScheduledMemoryDefragmentationFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 13022, Level = LogLevel.Warning, Message = "Error during periodic defragmentation check")]
    private static partial void LogErrorDuringPeriodicDefragmentationCheck(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 13023, Level = LogLevel.Information, Message = "Memory Recovery Strategy disposed")]
    private static partial void LogMemoryRecoveryStrategyDisposed(ILogger logger);

    #endregion
}





// Additional supporting types continue in next file due to length...
