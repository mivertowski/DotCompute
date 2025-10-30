// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Advanced
{
    public sealed partial class CudaDynamicParallelismManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 27016,
            Level = LogLevel.Debug,
            Message = "Dynamic Parallelism Manager initialized")]
        private static partial void LogManagerInitialized(ILogger logger);

        [LoggerMessage(
            EventId = 27017,
            Level = LogLevel.Error,
            Message = "Error optimizing kernel for dynamic parallelism")]
        private static partial void LogOptimizationError(ILogger logger, Exception ex);

        #endregion
    }
}
