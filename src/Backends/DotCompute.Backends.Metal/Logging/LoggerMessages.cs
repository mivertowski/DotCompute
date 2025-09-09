// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Backends.Metal.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    [LoggerMessage(EventId = 90000, Level = LogLevel.Information, Message = "Metal backend initialized on device {DeviceName}")]
    public static partial void MetalInitialized(this ILogger logger, string deviceName);

    [LoggerMessage(EventId = 90001, Level = LogLevel.Warning, Message = "Metal backend not implemented: {Feature}")]
    public static partial void MetalNotImplemented(this ILogger logger, string feature);

    [LoggerMessage(EventId = 90002, Level = LogLevel.Error, Message = "Metal backend error: {ErrorMessage}")]
    public static partial void MetalError(this ILogger logger, string errorMessage, Exception? exception = null);
}