// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Backends.OpenCL.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    [LoggerMessage(EventId = 95000, Level = LogLevel.Information, Message = "OpenCL backend initialized: {PlatformName} v{Version}")]
    public static partial void OpenClInitialized(this ILogger logger, string platformName, string version);

    [LoggerMessage(EventId = 95001, Level = LogLevel.Warning, Message = "OpenCL backend not implemented: {Feature}")]
    public static partial void OpenClNotImplemented(this ILogger logger, string feature);

    [LoggerMessage(EventId = 95002, Level = LogLevel.Error, Message = "OpenCL backend error: {ErrorMessage}")]
    public static partial void OpenClError(this ILogger logger, string errorMessage, Exception? exception = null);
}