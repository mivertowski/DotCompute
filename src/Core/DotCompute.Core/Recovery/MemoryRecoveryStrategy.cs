// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Core.Recovery.Models;
using DotCompute.Core.Recovery.Memory;
using DotCompute.Core.Recovery.Types;
using DotCompute.Core.Recovery.Exceptions;
using DotCompute.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive memory error recovery strategy with allocation retry,
/// defragmentation, and emergency memory reserve management.
/// Consolidated using BaseRecoveryStrategy to eliminate duplicate patterns.
/// </summary>
public sealed class MemoryRecoveryStrategy : BaseRecoveryStrategy<Models.MemoryRecoveryContext>
{
    private readonly ConcurrentDictionary<string, Models.MemoryPoolState> _memoryPools;
    private readonly Models.MemoryRecoveryConfiguration _config;
    private readonly Timer _defragmentationTimer;
    private readonly SemaphoreSlim _recoveryLock;
    private readonly Models.MemoryPressureMonitor _pressureMonitor;
    private bool _disposed;

    // Emergency reserve
    private volatile byte[]? _emergencyReserve;
    private readonly object _reserveLock = new();

    public override RecoveryCapability Capability => RecoveryCapability.MemoryErrors;
    public override int Priority => 100;

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

        Logger.LogInformation("Memory Recovery Strategy initialized with {ReserveSize}MB emergency reserve",
            _config.EmergencyReserveSizeMB);
    }

    public override bool CanHandle(Exception error, Models.MemoryRecoveryContext context)
    {
        return error switch
        {
            OutOfMemoryException => true,
            MemoryException => true,
            AcceleratorException accelEx when accelEx.Message.Contains("memory", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    public override async Task<RecoveryResult> RecoverAsync(
        Exception error,
        Models.MemoryRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();


        Logger.LogWarning("Memory error detected: {Error}. Available memory: {AvailableMemory}MB",
            error.Message, GC.GetTotalMemory(false) / 1024 / 1024);

        await _recoveryLock.WaitAsync(cancellationToken);


        try
        {
            // Determine recovery strategy based on error type and system state
            var strategy = DetermineMemoryRecoveryStrategy(error, context);
            Logger.LogInformation("Using memory recovery strategy: {Strategy}", strategy);

            var result = await ExecuteMemoryRecoveryAsync(strategy, context, options, cancellationToken);


            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;


            if (result.Success)
            {
                Logger.LogInformation("Memory recovery successful using {Strategy} in {Duration}ms. Memory after recovery: {MemoryAfter}MB",
                    strategy, stopwatch.ElapsedMilliseconds, GC.GetTotalMemory(false) / 1024 / 1024);
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
                Logger.LogWarning("Memory allocation failed on attempt {Attempt}/{MaxRetries}, retrying after {Delay}ms",
                    attempt, maxRetries, delay.TotalMilliseconds);

                // Perform emergency memory recovery
                await PerformEmergencyMemoryRecoveryAsync(cancellationToken);

                // Exponential backoff with jitter

                var actualDelay = TimeSpan.FromTicks((long)(delay.Ticks * Math.Pow(2, attempt - 1) * (0.8 + Random.Shared.NextDouble() * 0.4)));
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
            Logger.LogError("Memory allocation failed after {MaxRetries} attempts", maxRetries);
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
        Logger.LogInformation("Starting memory defragmentation for pool: {PoolId}", poolId ?? "all");


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

            Logger.LogInformation("Memory defragmentation completed in {Duration}ms. Freed {MemoryFreed}MB",
                stopwatch.ElapsedMilliseconds, memoryFreed / 1024 / 1024);

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
            Logger.LogError(ex, "Memory defragmentation failed");

            // Ensure emergency reserve is restored even on failure

            try { InitializeEmergencyReserve(); } catch { }

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
    public Memory.MemoryPressureInfo GetMemoryPressureInfo() => _pressureMonitor.GetCurrentPressure();

    /// <summary>
    /// Registers a memory pool for monitoring and recovery
    /// </summary>
    public void RegisterMemoryPool(string poolId, Models.IMemoryPool pool)
    {
        var poolState = new Models.MemoryPoolState(poolId, pool);
        _ = _memoryPools.TryAdd(poolId, poolState);


        Logger.LogDebug("Registered memory pool {PoolId} for recovery monitoring", poolId);
    }

    /// <summary>
    /// Unregisters a memory pool from monitoring
    /// </summary>
    public bool UnregisterMemoryPool(string poolId)
    {

        if (_memoryPools.TryRemove(poolId, out _))
        {
            Logger.LogDebug("Unregistered memory pool {PoolId}", poolId);
            return true;
        }


        return false;
    }

    private Models.MemoryRecoveryStrategyType DetermineMemoryRecoveryStrategy(Exception error, Models.MemoryRecoveryContext context)
    {
        var pressure = _pressureMonitor.GetCurrentPressure();


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
        Logger.LogWarning("Performing aggressive memory cleanup due to high memory pressure");

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
                Logger.LogWarning(ex, "Failed to cleanup memory pool {PoolId}", poolState.PoolId);
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
        Logger.LogCritical("Performing emergency memory recovery due to critical memory pressure");


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
                        Logger.LogError(ex, "Emergency cleanup failed for pool {PoolId}", pool.PoolId);
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
            Logger.LogCritical(ex, "Emergency memory recovery failed");
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
            try { InitializeEmergencyReserve(); }
            catch { /* Ignore during emergency */ }
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
        Logger.LogInformation("Performing GPU memory defragmentation for pool {PoolId}", poolId);

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
                Logger.LogDebug("Emergency memory reserve initialized: {Size}MB", targetSize);
            }
            catch (OutOfMemoryException)
            {
                Logger.LogWarning("Could not initialize emergency memory reserve of {Size}MB", targetSize);
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
                Logger.LogInformation("Released emergency memory reserve: {Size}MB", sizeMB);

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
            var pressure = _pressureMonitor.GetCurrentPressure();

            // Only defragment if memory pressure is elevated

            if (pressure.Level >= MemoryPressureLevel.Medium)
            {
                Logger.LogInformation("Performing scheduled memory defragmentation (pressure: {Level})", pressure.Level);


                _ = Task.Run(async () =>
                {
                    try
                    {
                        _ = await DefragmentMemoryAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Scheduled memory defragmentation failed");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during periodic defragmentation check");
        }
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            _defragmentationTimer?.Dispose();
            _recoveryLock?.Dispose();
            _pressureMonitor?.Dispose();
            ReleaseEmergencyReserve();
            _disposed = true;


            Logger.LogInformation("Memory Recovery Strategy disposed");
        }
    }
}




// Additional supporting types continue in next file due to length...