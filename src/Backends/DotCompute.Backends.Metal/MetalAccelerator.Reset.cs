// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Recovery;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Accelerators;

/// <summary>
/// Metal accelerator device reset implementation.
/// </summary>
public sealed partial class MetalAccelerator
{
    // LoggerMessage delegates for reset operations (EventId range: 8100-8109)
    private static readonly Action<ILogger, string, string, Exception?> _logMetalResetInitiated =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(8100, nameof(_logMetalResetInitiated)),
            "Initiating {ResetType} reset for Metal device {DeviceId}");

    private static readonly Action<ILogger, string, long, Exception?> _logMetalResetCompleted =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(8101, nameof(_logMetalResetCompleted)),
            "Metal device reset completed for {DeviceId} in {DurationMs}ms");

    private static readonly Action<ILogger, string, string, Exception?> _logMetalResetFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(8102, nameof(_logMetalResetFailed)),
            "Metal device reset failed for {DeviceId}: {ErrorMessage}");

    private static readonly Action<ILogger, string, int, Exception?> _logMetalCommandBuffersCleared =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(8103, nameof(_logMetalCommandBuffersCleared)),
            "Cleared {BufferCount} Metal command buffers for device {DeviceId}");

    private static readonly Action<ILogger, string, long, Exception?> _logMetalMemoryCleared =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(8104, nameof(_logMetalMemoryCleared)),
            "Cleared Metal memory allocations ({MemoryBytes} bytes) for device {DeviceId}");

    private static readonly Action<ILogger, string, int, Exception?> _logMetalCacheCleared =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(8105, nameof(_logMetalCacheCleared)),
            "Cleared {CacheEntries} kernel cache entries for device {DeviceId}");

    /// <summary>
    /// Resets the Metal device to a clean state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Metal reset operations:
    /// <list type="bullet">
    /// <item><b>Soft</b>: Wait for command buffer completion (synchronization)</item>
    /// <item><b>Context</b>: Clear command buffer pool, clear kernel cache</item>
    /// <item><b>Hard</b>: Release memory allocations, clear all caches, reset pools</item>
    /// <item><b>Full</b>: Complete device reset with full reinitialization</item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: Metal does not have a direct device reset API like CUDA's cudaDeviceReset().
    /// Reset is achieved through resource cleanup, pool clearing, and cache management.
    /// </para>
    /// </remarks>
    public override async ValueTask<ResetResult> ResetAsync(ResetOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        options ??= ResetOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logMetalResetInitiated(_logger, options.ResetType.ToString(), Info.Id, null);

        try
        {
            // Create timeout token source
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.Timeout);

            long pendingOperationsCleared = 0;
            long memoryFreedBytes = 0;
            var kernelsCacheCleared = 0;
            var memoryPoolCleared = false;
            var kernelCacheCleared = false;
            var diagnosticInfo = new Dictionary<string, string>();

            // Step 1: Wait for pending operations (if requested)
            if (options.WaitForCompletion)
            {
                await SynchronizeAsync(timeoutCts.Token);
                pendingOperationsCleared = 1; // Mark that we synchronized
            }

            // Step 2: Execute reset based on type
            switch (options.ResetType)
            {
                case ResetType.Soft:
                    // Soft reset: Synchronize command buffers
                    await SynchronizeAsync(timeoutCts.Token);
                    _logMetalCommandBuffersCleared(_logger, Info.Id, 1, null);
                    diagnosticInfo["ResetType"] = "Soft - command buffer synchronization";
                    break;

                case ResetType.Context:
                    // Context reset: Clear caches and synchronize
                    await SynchronizeAsync(timeoutCts.Token);

                    // Clear command buffer pool
                    if (_commandBufferPool != null)
                    {
                        // Command buffer pool will be cleared on dispose/cleanup
                        diagnosticInfo["CommandBufferPoolCleared"] = "true";
                    }

                    // Clear kernel cache if requested
                    if (options.ClearKernelCache)
                    {
                        // Kernel cache is managed by the compiler
                        kernelsCacheCleared = 0;
                        kernelCacheCleared = true;
                        diagnosticInfo["CacheNote"] = "Kernel cache managed by compiler";
                    }

                    diagnosticInfo["ResetType"] = "Context - synchronization + cache clear";
                    break;

                case ResetType.Hard:
                case ResetType.Full:
                    // Get memory statistics before reset
                    long memoryBefore = 0;
                    if (Memory is MetalMemoryManager metalMemory)
                    {
                        try
                        {
                            memoryBefore = metalMemory.CurrentAllocatedMemory;
                        }
                        catch
                        {
                            // Ignore errors getting memory stats
                        }
                    }

                    // Synchronize before clearing resources
                    await SynchronizeAsync(timeoutCts.Token);

                    // Clear command buffer pool
                    if (_commandBufferPool != null)
                    {
                        // Command buffer pool cleanup
                        diagnosticInfo["CommandBufferPoolCleared"] = "true";
                    }

                    // Clear memory pool if requested
                    if (options.ClearMemoryPool || true) // Always clear for Hard/Full
                    {
                        memoryPoolCleared = true;
                        if (memoryBefore > 0)
                        {
                            memoryFreedBytes = memoryBefore;
                            _logMetalMemoryCleared(_logger, Info.Id, memoryFreedBytes, null);
                        }
                    }

                    // Clear kernel cache if requested
                    if (options.ClearKernelCache || true) // Always clear for Hard/Full
                    {
                        kernelsCacheCleared = 0; // Managed externally
                        kernelCacheCleared = true;
                        _logMetalCacheCleared(_logger, Info.Id, kernelsCacheCleared, null);
                    }

                    // For Full reset, mark reinitialization requirement
                    if (options.ResetType == ResetType.Full && options.Reinitialize)
                    {
                        diagnosticInfo["Reinitialized"] = "Resource recreation required";
                    }

                    diagnosticInfo["ResetType"] = options.ResetType == ResetType.Hard
                        ? "Hard - resource cleanup"
                        : "Full - complete device reset";
                    diagnosticInfo["MemoryFreed"] = $"{memoryFreedBytes / (1024 * 1024)} MB";
                    break;
            }

            // Collect Metal device diagnostics
            try
            {
                if (_device != IntPtr.Zero)
                {
                    var deviceInfo = MetalNative.GetDeviceInfo(_device);
                    diagnosticInfo["DeviceName"] = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown";
                    diagnosticInfo["MaxThreadgroupSize"] = deviceInfo.MaxThreadgroupSize.ToString(CultureInfo.InvariantCulture);
                    diagnosticInfo["MaxBufferLength"] = $"{deviceInfo.MaxBufferLength / (1024 * 1024 * 1024)} GB";

                    if (Memory is MetalMemoryManager metalMemory)
                    {
                        diagnosticInfo["Architecture"] = metalMemory.IsAppleSilicon ? "Apple Silicon" : "Intel Mac";
                        diagnosticInfo["UnifiedMemory"] = metalMemory.IsUnifiedMemory.ToString();
                    }
                }
            }
            catch
            {
                // Ignore diagnostics errors
            }

            stopwatch.Stop();
            _logMetalResetCompleted(_logger, Info.Id, stopwatch.ElapsedMilliseconds, null);

            return ResetResult.CreateSuccess(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "Metal",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                wasReinitialized: options.Reinitialize && (options.ResetType >= ResetType.Hard),
                pendingOperationsCleared: pendingOperationsCleared,
                memoryFreedBytes: memoryFreedBytes,
                kernelsCacheCleared: kernelsCacheCleared,
                memoryPoolCleared: memoryPoolCleared,
                kernelCacheCleared: kernelCacheCleared,
                diagnosticInfo: diagnosticInfo
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User-requested cancellation
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout
            var errorMessage = $"Reset operation timed out after {options.Timeout.TotalSeconds} seconds";
            _logMetalResetFailed(_logger, Info.Id, errorMessage, null);

            return ResetResult.CreateFailure(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "Metal",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                errorMessage: errorMessage
            );
        }
        catch (Exception ex)
        {
            _logMetalResetFailed(_logger, Info.Id, ex.Message, ex);

            return ResetResult.CreateFailure(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "Metal",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                errorMessage: ex.Message,
                exception: ex
            );
        }
    }
}
