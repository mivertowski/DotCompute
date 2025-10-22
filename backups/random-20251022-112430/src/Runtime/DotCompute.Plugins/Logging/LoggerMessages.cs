// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Plugins.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    // Plugin Loading Messages
    [LoggerMessage(
        EventId = 60000,
        Level = LogLevel.Information,
        Message = "Loading plugin from {PluginPath}")]
    public static partial void PluginLoading(this ILogger logger, string pluginPath);

    [LoggerMessage(
        EventId = 60001,
        Level = LogLevel.Information,
        Message = "Plugin {PluginName} v{Version} loaded successfully")]
    public static partial void PluginLoaded(this ILogger logger, string pluginName, string version);

    [LoggerMessage(
        EventId = 60002,
        Level = LogLevel.Warning,
        Message = "Plugin {PluginName} validation failed: {Reason}")]
    public static partial void PluginValidationFailed(this ILogger logger, string pluginName, string reason);

    [LoggerMessage(
        EventId = 60003,
        Level = LogLevel.Error,
        Message = "Failed to load plugin from {PluginPath}: {ErrorMessage}")]
    public static partial void PluginLoadFailed(this ILogger logger, string pluginPath, string errorMessage, Exception? exception = null);

    // Hot Reload Messages
    [LoggerMessage(
        EventId = 61000,
        Level = LogLevel.Information,
        Message = "Hot reload detected for plugin {PluginName}")]
    public static partial void HotReloadDetected(this ILogger logger, string pluginName);

    [LoggerMessage(
        EventId = 61001,
        Level = LogLevel.Debug,
        Message = "Unloading plugin {PluginName} for reload")]
    public static partial void PluginUnloading(this ILogger logger, string pluginName);

    [LoggerMessage(
        EventId = 61002,
        Level = LogLevel.Information,
        Message = "Plugin {PluginName} reloaded successfully")]
    public static partial void PluginReloaded(this ILogger logger, string pluginName);

    // Dependency Resolution Messages
    [LoggerMessage(
        EventId = 62000,
        Level = LogLevel.Debug,
        Message = "Resolving dependencies for plugin {PluginName}")]
    public static partial void ResolvingDependencies(this ILogger logger, string pluginName);

    [LoggerMessage(
        EventId = 62001,
        Level = LogLevel.Warning,
        Message = "Missing dependency {DependencyName} v{Version} for plugin {PluginName}")]
    public static partial void MissingDependency(this ILogger logger, string dependencyName, string version, string pluginName);

    // Security Messages
    [LoggerMessage(
        EventId = 63000,
        Level = LogLevel.Information,
        Message = "Security validation passed for plugin {PluginName}")]
    public static partial void SecurityValidationPassed(this ILogger logger, string pluginName);

    [LoggerMessage(
        EventId = 63001,
        Level = LogLevel.Warning,
        Message = "Security warning for plugin {PluginName}: {Warning}")]
    public static partial void SecurityWarning(this ILogger logger, string pluginName, string warning);

    [LoggerMessage(
        EventId = 63002,
        Level = LogLevel.Error,
        Message = "Security violation in plugin {PluginName}: {Violation}")]
    public static partial void SecurityViolation(this ILogger logger, string pluginName, string violation);

    // Plugin Execution Messages
    [LoggerMessage(
        EventId = 64000,
        Level = LogLevel.Debug,
        Message = "Executing plugin {PluginName} method {MethodName}")]
    public static partial void PluginExecuting(this ILogger logger, string pluginName, string methodName);

    [LoggerMessage(
        EventId = 64001,
        Level = LogLevel.Debug,
        Message = "Plugin {PluginName} execution completed in {ElapsedMs}ms")]
    public static partial void PluginExecutionCompleted(this ILogger logger, string pluginName, double elapsedMs);

    // Error Messages
    [LoggerMessage(
        EventId = 65000,
        Level = LogLevel.Error,
        Message = "Plugin system error: {ErrorMessage}")]
    public static partial void PluginSystemError(this ILogger logger, string errorMessage, Exception? exception = null);

    // Generic Messages for common logging patterns
    [LoggerMessage(EventId = 66000, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 66001, Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void LogDebugMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 66002, Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 66003, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 66004, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogErrorMessage(this ILogger logger, Exception ex, string message);

    [LoggerMessage(EventId = 66005, Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void LogTraceMessage(this ILogger logger, string message);

    [LoggerMessage(EventId = 66006, Level = LogLevel.Critical, Message = "{Message}")]
    public static partial void LogCriticalMessage(this ILogger logger, string message);
}