// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// Logger message delegates for CudaKernelExecutor
/// </summary>
public sealed partial class CudaKernelExecutor
{
    // Event ID Range: 24000-24099 (Kernel Executor - Execution)

    [LoggerMessage(24000, LogLevel.Debug, "CUDA kernel execution orchestrator initialized for device {DeviceId}")]
    private partial void LogOrchestratorInitialized(int deviceId);

    [LoggerMessage(24001, LogLevel.Debug, "Executing kernel {KernelName} with {ArgumentCount} arguments")]
    private partial void LogExecutingKernel(string kernelName, int argumentCount);

    [LoggerMessage(24002, LogLevel.Debug, "Kernel {KernelName} executed in {ExecutionTime:F2}ms with success: {Success}")]
    private partial void LogKernelExecuted(string kernelName, double executionTime, bool success);

    [LoggerMessage(24003, LogLevel.Error, "Failed to execute kernel {KernelName}")]
    private partial void LogKernelExecutionFailed(Exception ex, string kernelName);

    [LoggerMessage(24004, LogLevel.Warning, "Failed to optimize configuration for kernel {KernelName}, using fallback")]
    private partial void LogConfigurationOptimizationFailed(Exception ex, string kernelName);

    [LoggerMessage(24005, LogLevel.Debug, "Using cached kernel {KernelName}")]
    private partial void LogUsingCachedKernel(string kernelName);

    [LoggerMessage(24006, LogLevel.Debug, "Compiling kernel {KernelName}")]
    private partial void LogCompilingKernel(string kernelName);

    [LoggerMessage(24007, LogLevel.Debug, "Kernel {KernelName} compiled and cached successfully")]
    private partial void LogKernelCompiledAndCached(string kernelName);

    [LoggerMessage(24008, LogLevel.Error, "Failed to compile kernel {KernelName}")]
    private partial void LogKernelCompilationFailed(Exception ex, string kernelName);

    [LoggerMessage(24009, LogLevel.Debug, "Executing batch of {KernelCount} kernels")]
    private partial void LogExecutingBatch(int kernelCount);

    [LoggerMessage(24010, LogLevel.Error, "Failed to execute kernel {Index} in batch")]
    private partial void LogBatchKernelExecutionFailed(Exception ex, int index);

    [LoggerMessage(24011, LogLevel.Debug, "Batch execution completed: {SuccessCount}/{TotalCount} kernels succeeded")]
    private partial void LogBatchExecutionCompleted(int successCount, int totalCount);

    [LoggerMessage(24012, LogLevel.Warning, "Error disposing cached kernel")]
    private partial void LogCachedKernelDisposalFailed(Exception ex);

    [LoggerMessage(24013, LogLevel.Debug, "CUDA kernel execution orchestrator disposed")]
    private partial void LogOrchestratorDisposed();
}
