// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution;

public sealed partial class CudaStreamManagerProduction
{
    [LoggerMessage(EventId = 28400, Level = LogLevel.Warning,
        Message = "Failed to pre-allocate stream {Index}")]
    private static partial void LogPreAllocateStreamFailed(ILogger logger, Exception ex, int index);

    [LoggerMessage(EventId = 28401, Level = LogLevel.Warning,
        Message = "Failed to synchronize pooled stream")]
    private static partial void LogPooledStreamSyncFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 28402, Level = LogLevel.Error,
        Message = "Failed to synchronize graph capture stream")]
    private static partial void LogGraphCaptureSyncFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 28403, Level = LogLevel.Warning,
        Message = "Error destroying stream")]
    private static partial void LogStreamDestroyError(ILogger logger, Exception ex);
}
