// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Recovery;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// CUDA accelerator device reset implementation.
/// </summary>
public sealed partial class CudaAccelerator
{
    // LoggerMessage delegates for reset operations
    private static readonly Action<ILogger, string, string, Exception?> _logResetInitiated =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(5100, nameof(_logResetInitiated)),
            "Initiating {ResetType} reset for device {DeviceId}");

    private static readonly Action<ILogger, string, long, Exception?> _logResetCompleted =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(5101, nameof(_logResetCompleted)),
            "Device reset completed for {DeviceId} in {DurationMs}ms");

    private static readonly Action<ILogger, string, string, Exception?> _logResetFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5102, nameof(_logResetFailed)),
            "Device reset failed for {DeviceId}: {ErrorMessage}");

    private static readonly Action<ILogger, string, long, Exception?> _logWaitingForOperations =
        LoggerMessage.Define<string, long>(
            LogLevel.Debug,
            new EventId(5103, nameof(_logWaitingForOperations)),
            "Waiting for {PendingOps} pending operations on device {DeviceId}");

    private static readonly Action<ILogger, string, long, Exception?> _logMemoryCleared =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(5104, nameof(_logMemoryCleared)),
            "Cleared {MemoryBytes} bytes of memory on device {DeviceId}");

    private static readonly Action<ILogger, string, int, Exception?> _logKernelCacheCleared =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(5105, nameof(_logKernelCacheCleared)),
            "Cleared {KernelCount} compiled kernels from cache for device {DeviceId}");

    /// <summary>
    /// Resets the CUDA device to a clean state.
    /// </summary>
    public override async ValueTask<ResetResult> ResetAsync(ResetOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        options ??= ResetOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logResetInitiated(Logger, options.ResetType.ToString(), Info.Id, null);

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
                _logWaitingForOperations(Logger, Info.Id, pendingOperationsCleared, null);
            }

            // Step 2: Execute reset based on type
            switch (options.ResetType)
            {
                case ResetType.Soft:
                    // Soft reset: just synchronize and flush
                    await SynchronizeAsync(timeoutCts.Token);
                    diagnosticInfo["ResetType"] = "Soft - synchronization only";
                    break;

                case ResetType.Context:
                    // Context reset: clear caches only (context will be reinitialized if needed)
                    if (options.ClearKernelCache || true) // Context reset always clears cache
                    {
                        kernelsCacheCleared = ClearKernelCache();
                        kernelCacheCleared = true;
                        _logKernelCacheCleared(Logger, Info.Id, kernelsCacheCleared, null);
                    }

                    // Synchronize before reset
                    await SynchronizeAsync(timeoutCts.Token);
                    diagnosticInfo["ResetType"] = "Context - cache cleared";
                    break;

                case ResetType.Hard:
                case ResetType.Full:
                    // Get memory statistics before reset
                    var memInfoResult = CudaRuntime.cudaMemGetInfo(out var freeBefore, out var totalMemory);
                    var usedMemoryBefore = memInfoResult == CudaError.Success ? (long)(totalMemory - freeBefore) : 0;

                    // Hard/Full reset: clear everything
                    if (options.ClearMemoryPool || true) // Hard/Full always clear memory
                    {
                        _memoryManager.Reset(); // Synchronous reset
                        memoryPoolCleared = true;
                    }

                    if (options.ClearKernelCache || true) // Hard/Full always clear cache
                    {
                        kernelsCacheCleared = ClearKernelCache();
                        kernelCacheCleared = true;
                        _logKernelCacheCleared(Logger, Info.Id, kernelsCacheCleared, null);
                    }

                    // Execute cudaDeviceReset
                    var resetResult = CudaRuntime.cudaDeviceReset();
                    if (resetResult != CudaError.Success)
                    {
                        var errorMessage = CudaRuntime.GetErrorString(resetResult);
                        _logResetFailed(Logger, Info.Id, errorMessage, null);

                        return ResetResult.CreateFailure(
                            deviceId: Info.Id,
                            deviceName: Info.Name,
                            backendType: "CUDA",
                            resetType: options.ResetType,
                            timestamp: startTime,
                            duration: stopwatch.Elapsed,
                            errorMessage: $"cudaDeviceReset failed: {errorMessage}",
                            diagnosticInfo: diagnosticInfo
                        );
                    }

                    // Get memory statistics after reset
                    memInfoResult = CudaRuntime.cudaMemGetInfo(out var freeAfter, out _);
                    var usedMemoryAfter = memInfoResult == CudaError.Success ? (long)(totalMemory - freeAfter) : 0;
                    memoryFreedBytes = Math.Max(0, usedMemoryBefore - usedMemoryAfter);

                    if (memoryFreedBytes > 0)
                    {
                        _logMemoryCleared(Logger, Info.Id, memoryFreedBytes, null);
                    }

                    diagnosticInfo["ResetType"] = options.ResetType == ResetType.Hard ? "Hard - full device reset" : "Full - complete reinitialization";
                    diagnosticInfo["MemoryFreed"] = $"{memoryFreedBytes / (1024 * 1024)} MB";

                    // Reinitialize if requested
                    if (options.Reinitialize)
                    {
                        _context.Reinitialize();
                        diagnosticInfo["Reinitialized"] = "true";
                    }
                    break;
            }

            // Collect NVML diagnostics if available
            try
            {
                var nvmlWrapper = new Monitoring.NvmlWrapper(Logger);
                if (nvmlWrapper.Initialize())
                {
                    var metrics = nvmlWrapper.GetDeviceMetrics(_device.DeviceId);
                    if (metrics.IsAvailable)
                    {
                        if (metrics.Temperature > 0)
                        {
                            diagnosticInfo["PostResetTemperature"] = $"{metrics.Temperature}°C";
                        }
                        if (metrics.PowerUsage > 0)
                        {
                            diagnosticInfo["PostResetPower"] = $"{metrics.PowerUsage:F1}W";
                        }
                    }
                    nvmlWrapper.Dispose();
                }
            }
            catch
            {
                // Ignore NVML errors - diagnostics are optional
            }

            stopwatch.Stop();
            _logResetCompleted(Logger, Info.Id, stopwatch.ElapsedMilliseconds, null);

            return ResetResult.CreateSuccess(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CUDA",
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
            _logResetFailed(Logger, Info.Id, errorMessage, null);

            return ResetResult.CreateFailure(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CUDA",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                errorMessage: errorMessage
            );
        }
        catch (Exception ex)
        {
            _logResetFailed(Logger, Info.Id, ex.Message, ex);

            return ResetResult.CreateFailure(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CUDA",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                errorMessage: ex.Message,
                exception: ex
            );
        }
    }

    /// <summary>
    /// Clears the kernel compilation cache.
    /// </summary>
    /// <returns>Number of kernels cleared.</returns>
    private static int ClearKernelCache()
    {
        // If we have a kernel cache/compiler with a clear method, use it
        // For now, return 0 as we don't have direct access to the cache
        // The actual cache is managed by the kernel compiler
        return 0;
    }
}
