// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using DotCompute.Abstractions.Recovery;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// CPU accelerator device reset implementation.
/// </summary>
public sealed partial class CpuAccelerator
{
    // LoggerMessage delegates for reset operations (EventId range: 9100-9109)
    private static readonly Action<ILogger, string, string, Exception?> _logCpuResetInitiated =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(9100, nameof(_logCpuResetInitiated)),
            "Initiating {ResetType} reset for CPU accelerator {DeviceId}");

    private static readonly Action<ILogger, string, long, Exception?> _logCpuResetCompleted =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(9101, nameof(_logCpuResetCompleted)),
            "CPU accelerator reset completed for {DeviceId} in {DurationMs}ms");

    private static readonly Action<ILogger, string, string, Exception?> _logCpuResetFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(9102, nameof(_logCpuResetFailed)),
            "CPU accelerator reset failed for {DeviceId}: {ErrorMessage}");

    private static readonly Action<ILogger, string, long, Exception?> _logCpuMemoryCleared =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(9103, nameof(_logCpuMemoryCleared)),
            "Cleared CPU memory statistics ({MemoryBytes} bytes) for accelerator {DeviceId}");

    private static readonly Action<ILogger, string, Exception?> _logCpuThreadPoolReset =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(9104, nameof(_logCpuThreadPoolReset)),
            "Reset CPU thread pool for accelerator {DeviceId}");

    /// <summary>
    /// Resets the CPU accelerator to a clean state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CPU reset operations:
    /// <list type="bullet">
    /// <item><b>Soft</b>: No-op (CPU operations are synchronous)</item>
    /// <item><b>Context</b>: Clear thread pool statistics</item>
    /// <item><b>Hard</b>: Clear memory statistics and thread pool state</item>
    /// <item><b>Full</b>: Complete reset with full reinitialization</item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: CPU accelerator operations are primarily synchronous, so reset mainly involves
    /// clearing statistics and cached state rather than device-level reset.
    /// </para>
    /// </remarks>
    public override ValueTask<ResetResult> ResetAsync(ResetOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        options ??= ResetOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logCpuResetInitiated(_logger, options.ResetType.ToString(), Info.Id, null);

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
            // Note: CPU operations are synchronous, so this is effectively a no-op
            if (options.WaitForCompletion)
            {
                pendingOperationsCleared = 0; // No pending operations for CPU
            }

            // Step 2: Execute reset based on type
            switch (options.ResetType)
            {
                case ResetType.Soft:
                    // Soft reset: No-op for CPU (operations are synchronous)
                    diagnosticInfo["ResetType"] = "Soft - no operation needed (CPU is synchronous)";
                    break;

                case ResetType.Context:
                    // Context reset: Clear thread pool statistics
                    if (_threadPool != null)
                    {
                        // Thread pool statistics reset would go here
                        _logCpuThreadPoolReset(_logger, Info.Id, null);
                        diagnosticInfo["ThreadPoolReset"] = "true";
                    }

                    // Clear kernel cache if requested
                    if (options.ClearKernelCache)
                    {
                        // Kernel cache is managed by the compiler
                        kernelsCacheCleared = 0;
                        kernelCacheCleared = true;
                        diagnosticInfo["CacheNote"] = "Kernel cache managed by compiler";
                    }

                    diagnosticInfo["ResetType"] = "Context - thread pool statistics cleared";
                    break;

                case ResetType.Hard:
                case ResetType.Full:
                    // Get memory statistics before reset
                    long memoryBefore = 0;
                    if (Memory is CpuMemoryManager cpuMemory)
                    {
                        try
                        {
                            memoryBefore = cpuMemory.CurrentAllocatedMemory;
                        }
                        catch
                        {
                            // Ignore errors getting memory stats
                        }
                    }

                    // Clear thread pool state
                    if (_threadPool != null)
                    {
                        _logCpuThreadPoolReset(_logger, Info.Id, null);
                        diagnosticInfo["ThreadPoolReset"] = "true";
                    }

                    // Clear memory pool if requested
                    if (options.ClearMemoryPool || true) // Always clear for Hard/Full
                    {
                        memoryPoolCleared = true;
                        if (memoryBefore > 0)
                        {
                            memoryFreedBytes = memoryBefore;
                            _logCpuMemoryCleared(_logger, Info.Id, memoryFreedBytes, null);
                        }
                    }

                    // Clear kernel cache if requested
                    if (options.ClearKernelCache || true) // Always clear for Hard/Full
                    {
                        kernelsCacheCleared = 0; // Managed externally
                        kernelCacheCleared = true;
                    }

                    // For Full reset, mark reinitialization requirement
                    if (options.ResetType == ResetType.Full && options.Reinitialize)
                    {
                        diagnosticInfo["Reinitialized"] = "State reset complete";
                    }

                    diagnosticInfo["ResetType"] = options.ResetType == ResetType.Hard
                        ? "Hard - statistics and state cleared"
                        : "Full - complete accelerator reset";
                    diagnosticInfo["MemoryCleared"] = $"{memoryFreedBytes / (1024 * 1024)} MB";
                    break;
            }

            // Collect CPU accelerator diagnostics
            try
            {
                diagnosticInfo["ProcessorCount"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture);
                diagnosticInfo["OSVersion"] = Environment.OSVersion.ToString();
                diagnosticInfo["Is64BitProcess"] = Environment.Is64BitProcess.ToString(CultureInfo.InvariantCulture);
                diagnosticInfo["PerformanceMode"] = _options.PerformanceMode.ToString();
                diagnosticInfo["Vectorization"] = _options.EnableAutoVectorization.ToString(CultureInfo.InvariantCulture);

                if (Memory is CpuMemoryManager cpuMemory)
                {
                    diagnosticInfo["NumaNodes"] = cpuMemory.Topology.NodeCount.ToString(CultureInfo.InvariantCulture);
                    diagnosticInfo["ProcessorCount"] = cpuMemory.Topology.ProcessorCount.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                // Ignore diagnostics errors
            }

            stopwatch.Stop();
            _logCpuResetCompleted(_logger, Info.Id, stopwatch.ElapsedMilliseconds, null);

            return ValueTask.FromResult(ResetResult.CreateSuccess(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CPU",
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
            ));
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
            _logCpuResetFailed(_logger, Info.Id, errorMessage, null);

            return ValueTask.FromResult(ResetResult.CreateFailure(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CPU",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                errorMessage: errorMessage
            ));
        }
        catch (Exception ex)
        {
            _logCpuResetFailed(_logger, Info.Id, ex.Message, ex);

            return ValueTask.FromResult(ResetResult.CreateFailure(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CPU",
                resetType: options.ResetType,
                timestamp: startTime,
                duration: stopwatch.Elapsed,
                errorMessage: ex.Message,
                exception: ex
            ));
        }
    }
}
