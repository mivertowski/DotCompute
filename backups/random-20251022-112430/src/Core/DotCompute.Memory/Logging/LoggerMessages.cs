// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Memory.Logging;

/// <summary>
/// High-performance logger message delegates for DotCompute.Memory.
/// </summary>
internal static partial class LoggerMessages
{
    [LoggerMessage(EventId = 31000, Level = LogLevel.Debug, Message = "Allocated unified buffer: {BufferId} with size {SizeBytes} bytes")]
    public static partial void BufferAllocated(this ILogger logger, string bufferId, long sizeBytes);

    [LoggerMessage(EventId = 31001, Level = LogLevel.Debug, Message = "Released unified buffer: {BufferId}")]
    public static partial void BufferReleased(this ILogger logger, string bufferId);

    [LoggerMessage(EventId = 31002, Level = LogLevel.Information, Message = "Memory pool created with {InitialSize} bytes capacity")]
    public static partial void PoolCreated(this ILogger logger, long initialSize);

    [LoggerMessage(EventId = 31003, Level = LogLevel.Warning, Message = "Memory pool expansion: {OldSize} -> {NewSize} bytes")]
    public static partial void PoolExpanded(this ILogger logger, long oldSize, long newSize);

    [LoggerMessage(EventId = 31004, Level = LogLevel.Debug, Message = "P2P transfer initiated: {SourceDevice} -> {TargetDevice}, {SizeBytes} bytes")]
    public static partial void P2PTransfer(this ILogger logger, int sourceDevice, int targetDevice, long sizeBytes);

    [LoggerMessage(EventId = 31005, Level = LogLevel.Error, Message = "Memory allocation failed: {ErrorMessage}")]
    public static partial void AllocationFailed(this ILogger logger, string errorMessage, Exception? exception = null);
}