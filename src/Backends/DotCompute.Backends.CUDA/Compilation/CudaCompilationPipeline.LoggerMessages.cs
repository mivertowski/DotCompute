// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

internal sealed partial class CudaCompilationPipeline
{
    [LoggerMessage(EventId = 21000, Level = LogLevel.Debug,
        Message = "Starting compilation pipeline for kernel {KernelName}")]
    private static partial void LogPipelineStart(ILogger logger, string kernelName);

    [LoggerMessage(EventId = 21001, Level = LogLevel.Debug,
        Message = "Using cached kernel {KernelName} (access count: {AccessCount})")]
    private static partial void LogCachedKernel(ILogger logger, string kernelName, int accessCount);

    [LoggerMessage(EventId = 21002, Level = LogLevel.Warning,
        Message = "Source warning in kernel '{KernelName}': {Warning}")]
    private static partial void LogSourceWarning(ILogger logger, string kernelName, string warning);

    [LoggerMessage(EventId = 21003, Level = LogLevel.Debug,
        Message = "Selected compilation target: {Target} for kernel {KernelName}")]
    private static partial void LogCompilationTarget(ILogger logger, CompilationTarget target, string kernelName);

    [LoggerMessage(EventId = 21004, Level = LogLevel.Warning,
        Message = "Compiled code verification failed for kernel '{KernelName}'")]
    private static partial void LogVerificationFailed(ILogger logger, string kernelName);

    [LoggerMessage(EventId = 21005, Level = LogLevel.Information,
        Message = "Successfully compiled kernel {KernelName} in {ElapsedMs}ms using {Target}")]
    private static partial void LogCompilationSuccess(ILogger logger, string kernelName, long elapsedMs, CompilationTarget target);

    [LoggerMessage(EventId = 21006, Level = LogLevel.Error,
        Message = "Compilation pipeline failed for kernel {KernelName} after {ElapsedMs}ms")]
    private static partial void LogCompilationFailure(ILogger logger, Exception ex, string kernelName, long elapsedMs);

    [LoggerMessage(EventId = 21007, Level = LogLevel.Information,
        Message = "Starting batch compilation of {KernelCount} kernels")]
    private static partial void LogBatchStart(ILogger logger, int kernelCount);

    [LoggerMessage(EventId = 21008, Level = LogLevel.Information,
        Message = "Completed batch compilation of {KernelCount} kernels in {ElapsedMs}ms")]
    private static partial void LogBatchSuccess(ILogger logger, int kernelCount, long elapsedMs);

    [LoggerMessage(EventId = 21009, Level = LogLevel.Error,
        Message = "Batch compilation failed after {ElapsedMs}ms")]
    private static partial void LogBatchFailure(ILogger logger, Exception ex, long elapsedMs);

    [LoggerMessage(EventId = 21010, Level = LogLevel.Warning,
        Message = "Failed to determine optimal compilation target, defaulting to PTX")]
    private static partial void LogTargetSelectionFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 21011, Level = LogLevel.Warning,
        Message = "Failed to clean up compilation pipeline resources")]
    private static partial void LogCleanupFailed(ILogger logger, Exception ex);
}
