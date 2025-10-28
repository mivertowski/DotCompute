// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Abstractions.
/// Uses source-generated LoggerMessage for 2-3x performance improvement over direct ILogger calls.
/// </summary>
internal static partial class LoggerMessages
{
    [LoggerMessage(EventId = 100000, Level = LogLevel.Debug, Message = "Abstraction layer initialized")]
    public static partial void AbstractionInitialized(this ILogger logger);

    [LoggerMessage(EventId = 100001, Level = LogLevel.Debug, Message = "Interface {InterfaceName} implementation registered: {ImplementationType}")]
    public static partial void InterfaceRegistered(this ILogger logger, string interfaceName, string implementationType);

    [LoggerMessage(EventId = 100002, Level = LogLevel.Warning, Message = "Abstraction warning: {WarningMessage}")]
    public static partial void AbstractionWarning(this ILogger logger, string warningMessage);

    [LoggerMessage(EventId = 100003, Level = LogLevel.Error, Message = "Abstraction error: {ErrorMessage}")]
    public static partial void AbstractionError(this ILogger logger, string errorMessage, Exception? exception = null);
}
