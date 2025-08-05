// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// LoggerMessage delegates for CudaKernelCompiler
/// </summary>
public sealed partial class CudaKernelCompiler
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Successfully compiled kernel '{KernelName}' with optimization level {OptimizationLevel} in {ElapsedMs}ms")]
    private static partial void LogKernelCompilationSuccess(ILogger logger, string kernelName, string optimizationLevel, long elapsedMs);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Failed to compile kernel '{KernelName}' with NVRTC")]
    private static partial void LogKernelCompilationError(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Starting CUDA kernel compilation: {KernelName}")]
    private static partial void LogKernelCompilationStart(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' found in cache")]
    private static partial void LogKernelCacheHit(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Loaded kernel '{KernelName}' from cache in {ElapsedMs}ms")]
    private static partial void LogKernelCacheLoad(ILogger logger, string kernelName, long elapsedMs);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Kernel cache miss for '{KernelName}'")]
    private static partial void LogKernelCacheMiss(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Warning,
        Message = "Failed to persist kernel '{KernelName}' to cache: {Error}")]
    private static partial void LogKernelCachePersistError(ILogger logger, string kernelName, string error);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Generated PTX code ({PtxSize} bytes) for kernel '{KernelName}'")]
    private static partial void LogPtxGeneration(ILogger logger, int ptxSize, string kernelName);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Error,
        Message = "NVRTC compilation failed for kernel '{KernelName}' with error: {Error}")]
    private static partial void LogNvrtcCompilationError(ILogger logger, string kernelName, string error);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Cleared kernel cache, removed {RemovedCount} entries")]
    private static partial void LogCacheClear(ILogger logger, int removedCount);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Debug,
        Message = "Validated CUDA source for kernel '{KernelName}' - {WarningCount} warnings")]
    private static partial void LogSourceValidation(ILogger logger, string kernelName, int warningCount);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Error,
        Message = "Error during CUDA kernel compiler disposal")]
    private static partial void LogDisposalError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Error,
        Message = "Compiled code is null or empty for kernel: {KernelName}")]
    private static partial void LogEmptyCompiledCode(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Information,
        Message = "Loaded {Count} cached kernels from disk")]
    private static partial void LogLoadedCachedKernels(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Warning,
        Message = "Failed to load persistent kernel cache")]
    private static partial void LogFailedToLoadCache(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1015,
        Level = LogLevel.Debug,
        Message = "Persisted kernel cache to disk: {File}")]
    private static partial void LogPersistedKernelCache(ILogger logger, string file);

    [LoggerMessage(
        EventId = 1016,
        Level = LogLevel.Warning,
        Message = "Failed to persist kernel cache for: {CacheKey}")]
    private static partial void LogFailedToPersistCache(ILogger logger, Exception ex, string cacheKey);

    [LoggerMessage(
        EventId = 1017,
        Level = LogLevel.Warning,
        Message = "Failed to load cached kernel from: {File}")]
    private static partial void LogFailedToLoadCachedKernel(ILogger logger, Exception ex, string file);
}