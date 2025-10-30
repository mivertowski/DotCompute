// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Advanced
{
    /// <summary>
    /// LoggerMessage delegates for CudaKernelProfiler
    /// Event IDs: 25001-25025
    /// </summary>
    public sealed partial class CudaKernelProfiler
    {
        [LoggerMessage(EventId = 25001, Level = LogLevel.Information,
            Message = "Starting profiling of kernel '{KernelName}' for {Iterations} iterations")]
        private static partial void LogProfilingStart(ILogger logger, string kernelName, int iterations);
    }
}
