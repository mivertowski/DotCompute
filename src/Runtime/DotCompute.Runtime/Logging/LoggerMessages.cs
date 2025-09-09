// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Runtime.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // Kernel Compilation Messages
    [LoggerMessage(
        EventId = 11000,
        Level = LogLevel.Information,
        Message = "Compiling kernel {KernelName} for backend {BackendType}")]
    public static partial void KernelCompilationStarted(this ILogger logger, string kernelName, string backendType);

    [LoggerMessage(
        EventId = 11001,
        Level = LogLevel.Information,
        Message = "Kernel {KernelName} compilation completed in {ElapsedMilliseconds}ms")]
    public static partial void KernelCompilationCompleted(this ILogger logger, string kernelName, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 11002,
        Level = LogLevel.Error,
        Message = "Failed to compile kernel {KernelName}: {ErrorMessage}")]
    public static partial void KernelCompilationFailed(this ILogger logger, string kernelName, string errorMessage, Exception? exception = null);

    [LoggerMessage(
        EventId = 11003,
        Level = LogLevel.Debug,
        Message = "Using {BackendType} compiler for kernel {KernelName}")]
    public static partial void CompilerSelected(this ILogger logger, string backendType, string kernelName);

    [LoggerMessage(
        EventId = 11004,
        Level = LogLevel.Information,
        Message = "Registered compiler for backend: {BackendType}")]
    public static partial void CompilerRegistered(this ILogger logger, string backendType);

    // Kernel Cache Messages
    [LoggerMessage(
        EventId = 12000,
        Level = LogLevel.Debug,
        Message = "Cache hit for key: {CacheKey}")]
    public static partial void CacheHit(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 12001,
        Level = LogLevel.Debug,
        Message = "Cache miss for key: {CacheKey}")]
    public static partial void CacheMiss(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 12002,
        Level = LogLevel.Debug,
        Message = "Cached kernel with key: {CacheKey}")]
    public static partial void CacheStored(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 12003,
        Level = LogLevel.Debug,
        Message = "Invalidated cache entry: {CacheKey}")]
    public static partial void CacheInvalidated(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 12004,
        Level = LogLevel.Information,
        Message = "Cleared {Count} cache entries")]
    public static partial void CacheCleared(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 12005,
        Level = LogLevel.Debug,
        Message = "Evicted LRU cache entry: {CacheKey}")]
    public static partial void CacheEvicted(this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 12006,
        Level = LogLevel.Debug,
        Message = "Cleaned up {Count} expired cache entries")]
    public static partial void CacheExpiredCleaned(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 12007,
        Level = LogLevel.Debug,
        Message = "Kernel {KernelName} needs pre-warming for {AcceleratorType}")]
    public static partial void CachePrewarmNeeded(this ILogger logger, string kernelName, string acceleratorType);

    [LoggerMessage(
        EventId = 12008,
        Level = LogLevel.Warning,
        Message = "Failed to pre-warm kernel {KernelName}")]
    public static partial void CachePrewarmFailed(this ILogger logger, string kernelName, Exception exception);

    // Profiling Messages
    [LoggerMessage(
        EventId = 13000,
        Level = LogLevel.Debug,
        Message = "Started profiling session {SessionId}: {SessionName}")]
    public static partial void ProfilingSessionStarted(this ILogger logger, Guid sessionId, string sessionName);

    [LoggerMessage(
        EventId = 13001,
        Level = LogLevel.Debug,
        Message = "Completed profiling session {SessionId}")]
    public static partial void ProfilingSessionCompleted(this ILogger logger, Guid sessionId);

    [LoggerMessage(
        EventId = 13002,
        Level = LogLevel.Information,
        Message = "Exported {Count} profiling sessions to {Path}")]
    public static partial void ProfilingDataExported(this ILogger logger, int count, string path);

    [LoggerMessage(
        EventId = 13003,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} old profiling sessions")]
    public static partial void ProfilingDataCleaned(this ILogger logger, int count);

    // Kernel Discovery Messages
    [LoggerMessage(
        EventId = 14000,
        Level = LogLevel.Information,
        Message = "Discovered {Count} generated kernels in assembly {AssemblyName}")]
    public static partial void KernelsDiscovered(this ILogger logger, int count, string assemblyName);

    [LoggerMessage(
        EventId = 14001,
        Level = LogLevel.Debug,
        Message = "Registered kernel: {KernelName} from type {TypeName}")]
    public static partial void KernelRegistered(this ILogger logger, string kernelName, string typeName);

    [LoggerMessage(
        EventId = 14002,
        Level = LogLevel.Warning,
        Message = "Failed to load generated kernel type {TypeName}")]
    public static partial void KernelLoadFailed(this ILogger logger, string typeName, Exception exception);

    // Kernel Execution Service Messages
    [LoggerMessage(
        EventId = 15000,
        Level = LogLevel.Information,
        Message = "Executing kernel {KernelName} with {ArgumentCount} arguments")]
    public static partial void KernelExecutionRequested(this ILogger logger, string kernelName, int argumentCount);

    [LoggerMessage(
        EventId = 15001,
        Level = LogLevel.Debug,
        Message = "Kernel {KernelName} execution completed successfully")]
    public static partial void KernelExecutionSuccess(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 15002,
        Level = LogLevel.Error,
        Message = "Kernel {KernelName} not found in registry")]
    public static partial void KernelNotFound(this ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 15003,
        Level = LogLevel.Warning,
        Message = "Kernel {KernelName} validation failed: {ValidationMessage}")]
    public static partial void KernelValidationFailed(this ILogger logger, string kernelName, string validationMessage);

    // Service Registration Messages
    [LoggerMessage(
        EventId = 16000,
        Level = LogLevel.Information,
        Message = "Registered DotCompute runtime services")]
    public static partial void RuntimeServicesRegistered(this ILogger logger);

    [LoggerMessage(
        EventId = 16001,
        Level = LogLevel.Information,
        Message = "Registered production optimization services with profile: {Profile}")]
    public static partial void OptimizationServicesRegistered(this ILogger logger, string profile);

    [LoggerMessage(
        EventId = 16002,
        Level = LogLevel.Information,
        Message = "Registered production debugging services with profile: {Profile}")]
    public static partial void DebuggingServicesRegistered(this ILogger logger, string profile);

    // Plugin Messages
    [LoggerMessage(
        EventId = 17000,
        Level = LogLevel.Information,
        Message = "Loaded plugin {PluginName} version {Version}")]
    public static partial void PluginLoaded(this ILogger logger, string pluginName, string version);

    [LoggerMessage(
        EventId = 17001,
        Level = LogLevel.Warning,
        Message = "Plugin {PluginName} validation failed: {Reason}")]
    public static partial void PluginValidationFailed(this ILogger logger, string pluginName, string reason);

    // General Messages
    [LoggerMessage(
        EventId = 18000,
        Level = LogLevel.Error,
        Message = "Runtime error in {Component}: {ErrorMessage}")]
    public static partial void RuntimeError(this ILogger logger, string component, string errorMessage, Exception? exception = null);

    [LoggerMessage(
        EventId = 18001,
        Level = LogLevel.Warning,
        Message = "Runtime warning in {Component}: {WarningMessage}")]
    public static partial void RuntimeWarning(this ILogger logger, string component, string warningMessage);
}