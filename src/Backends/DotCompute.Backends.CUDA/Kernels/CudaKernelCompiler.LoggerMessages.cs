// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation
{

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

        [LoggerMessage(
            EventId = 1018,
            Level = LogLevel.Warning,
            Message = "NVRTC is not available or version is too old. Kernel compilation may fail.")]
        private static partial void LogNvrtcNotAvailable(ILogger logger);

        [LoggerMessage(
            EventId = 1019,
            Level = LogLevel.Information,
            Message = "NVRTC version {Major}.{Minor} detected and ready for kernel compilation")]
        private static partial void LogNvrtcVersionDetected(ILogger logger, int major, int minor);

        [LoggerMessage(
            EventId = 1020,
            Level = LogLevel.Debug,
            Message = "Starting NVRTC compilation for kernel: {KernelName}")]
        private static partial void LogNvrtcCompilationStart(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1021,
            Level = LogLevel.Debug,
            Message = "NVRTC compilation options: {Options}")]
        private static partial void LogNvrtcCompilationOptions(ILogger logger, string options);

        [LoggerMessage(
            EventId = 1022,
            Level = LogLevel.Information,
            Message = "NVRTC compilation completed successfully for kernel: {KernelName}")]
        private static partial void LogNvrtcCompilationCompleted(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1023,
            Level = LogLevel.Information,
            Message = "Generated {PtxSize} bytes of PTX code for kernel: {KernelName}")]
        private static partial void LogPtxCodeGenerated(ILogger logger, int ptxSize, string kernelName);

        [LoggerMessage(
            EventId = 1024,
            Level = LogLevel.Warning,
            Message = "NVRTC compilation failed for kernel: {KernelName}")]
        private static partial void LogNvrtcCompilationFailed(ILogger logger, Exception ex, string kernelName);

        [LoggerMessage(
            EventId = 1025,
            Level = LogLevel.Warning,
            Message = "Failed to create or compile NVRTC program for kernel: {KernelName}")]
        private static partial void LogNvrtcProgramCreationFailed(ILogger logger, Exception ex, string kernelName);

        [LoggerMessage(
            EventId = 1026,
            Level = LogLevel.Information,
            Message = "NVRTC compilation log for {KernelName}: {Log}")]
        private static partial void LogNvrtcCompilationLog(ILogger logger, string kernelName, string log);

        [LoggerMessage(
            EventId = 1027,
            Level = LogLevel.Warning,
            Message = "Failed to cleanup NVRTC program for kernel: {KernelName}")]
        private static partial void LogNvrtcCleanupFailed(ILogger logger, Exception ex, string kernelName);

        [LoggerMessage(
            EventId = 1028,
            Level = LogLevel.Warning,
            Message = "Failed to retrieve NVRTC compilation log")]
        private static partial void LogFailedToRetrieveNvrtcLog(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 1029,
            Level = LogLevel.Debug,
            Message = "Starting NVRTC CUBIN compilation for kernel: {KernelName}")]
        private static partial void LogNvrtcCubinCompilationStart(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1030,
            Level = LogLevel.Debug,
            Message = "NVRTC CUBIN compilation options: {Options}")]
        private static partial void LogNvrtcCubinCompilationOptions(ILogger logger, string options);

        [LoggerMessage(
            EventId = 1031,
            Level = LogLevel.Information,
            Message = "NVRTC CUBIN compilation log for {KernelName}: {Log}")]
        private static partial void LogNvrtcCubinCompilationLog(ILogger logger, string kernelName, string log);

        [LoggerMessage(
            EventId = 1032,
            Level = LogLevel.Warning,
            Message = "Error during CUDA source validation for kernel: {KernelName}")]
        private static partial void LogCudaSourceValidationError(ILogger logger, Exception ex, string kernelName);

        [LoggerMessage(
            EventId = 1033,
            Level = LogLevel.Warning,
            Message = "PTX code does not contain .entry directive for kernel: {KernelName}")]
        private static partial void LogPtxMissingEntryDirective(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1034,
            Level = LogLevel.Debug,
            Message = "PTX verification passed for kernel: {KernelName}")]
        private static partial void LogPtxVerificationPassed(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1035,
            Level = LogLevel.Debug,
            Message = "Binary code verification passed for kernel: {KernelName} (size: {Size} bytes)")]
        private static partial void LogBinaryVerificationPassed(ILogger logger, string kernelName, int size);

        [LoggerMessage(
            EventId = 1036,
            Level = LogLevel.Warning,
            Message = "Compiled code verification inconclusive for kernel: {KernelName}")]
        private static partial void LogCodeVerificationInconclusive(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1037,
            Level = LogLevel.Warning,
            Message = "Error during compiled code verification for kernel: {KernelName}")]
        private static partial void LogCodeVerificationError(ILogger logger, Exception ex, string kernelName);

        [LoggerMessage(
            EventId = 1038,
            Level = LogLevel.Information,
            Message = "Successfully compiled kernel '{KernelName}' using NVRTC in {CompilationTime}ms. PTX size: {PtxSize} bytes")]
        private static partial void LogNvrtcKernelCompilationSuccess(ILogger logger, string kernelName, long compilationTime, int ptxSize);

        [LoggerMessage(
            EventId = 1039,
            Level = LogLevel.Information,
            Message = "Successfully compiled kernel '{KernelName}' using NVRTC CUBIN in {CompilationTime}ms. Binary size: {BinarySize} bytes")]
        private static partial void LogNvrtcCubinKernelCompilationSuccess(ILogger logger, string kernelName, long compilationTime, int binarySize);

        [LoggerMessage(
            EventId = 1040,
            Level = LogLevel.Warning,
            Message = "Failed to get current device compute capability, using default")]
        private static partial void LogFailedToGetComputeCapability(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 1041,
            Level = LogLevel.Information,
            Message = "Compiling CUDA kernel: {KernelName}")]
        private static partial void LogCompilingCudaKernel(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1042,
            Level = LogLevel.Debug,
            Message = "Using cached kernel: {KernelName} (accessed {AccessCount} times)")]
        private static partial void LogUsingCachedKernel(ILogger logger, string kernelName, int accessCount);

        [LoggerMessage(
            EventId = 1043,
            Level = LogLevel.Warning,
            Message = "CUDA source warning for '{KernelName}': {Warning}")]
        private static partial void LogCudaSourceWarning(ILogger logger, string kernelName, string warning);

        [LoggerMessage(
            EventId = 1044,
            Level = LogLevel.Debug,
            Message = "Using CUBIN compilation for kernel: {KernelName}")]
        private static partial void LogUsingCubinCompilation(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1045,
            Level = LogLevel.Debug,
            Message = "Using PTX compilation for kernel: {KernelName}")]
        private static partial void LogUsingPtxCompilation(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1046,
            Level = LogLevel.Information,
            Message = "Successfully compiled CUDA kernel: {KernelName}")]
        private static partial void LogSuccessfullyCompiledKernel(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 1047,
            Level = LogLevel.Information,
            Message = "Clearing CUDA kernel cache")]
        private static partial void LogClearingKernelCache(ILogger logger);

        [LoggerMessage(
            EventId = 1048,
            Level = LogLevel.Information,
            Message = "NVRTC supported architectures: {Architectures}")]
        private static partial void LogSupportedArchitectures(ILogger logger, string architectures);

        [LoggerMessage(
            EventId = 1049,
            Level = LogLevel.Warning,
            Message = "Failed to register name expression for function: {FunctionName} - {Error}")]
        private static partial void LogFailedToRegisterNameExpression(ILogger logger, string functionName, string error);

        [LoggerMessage(
            EventId = 1050,
            Level = LogLevel.Warning,
            Message = "Failed to get mangled name for function: {FunctionName}")]
        private static partial void LogFailedToGetMangledName(ILogger logger, string functionName);

        [LoggerMessage(
            EventId = 1051,
            Level = LogLevel.Warning,
            Message = "Failed to extract function names from CUDA source")]
        private static partial void LogFailedToExtractFunctionNames(ILogger logger, Exception ex);
    }
}
