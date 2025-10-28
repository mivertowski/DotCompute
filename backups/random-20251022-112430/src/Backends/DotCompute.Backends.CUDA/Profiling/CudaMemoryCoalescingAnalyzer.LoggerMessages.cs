// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Analysis
{
    /// <summary>
    /// LoggerMessage delegates for CudaMemoryCoalescingAnalyzer
    /// Event IDs: 25061-25080
    /// </summary>
    public sealed partial class CudaMemoryCoalescingAnalyzer
    {
        [LoggerMessage(EventId = 25061, Level = LogLevel.Information,
            Message = "CUDA Memory Coalescing Analyzer initialized")]
        private static partial void LogInitialized(ILogger logger);

        [LoggerMessage(EventId = 25062, Level = LogLevel.Debug,
            Message = "Analyzing memory access pattern for {KernelName}")]
        private static partial void LogAnalyzingPattern(ILogger logger, string kernelName);

        [LoggerMessage(EventId = 25063, Level = LogLevel.Information,
            Message = "Coalescing analysis for {KernelName}: Efficiency={Efficiency:P}, Wasted BW={WastedBW:F2} GB/s")]
        private static partial void LogAnalysisComplete(ILogger logger, string kernelName, double efficiency, double wastedBW);

        [LoggerMessage(EventId = 25064, Level = LogLevel.Debug,
            Message = "Strided access analysis: Stride={Stride}, Efficiency={Efficiency:P}, Transactions={Trans}")]
        private static partial void LogStridedAnalysis(ILogger logger, int stride, double efficiency, int trans);

        [LoggerMessage(EventId = 25065, Level = LogLevel.Information,
            Message = "2D access analysis: {Order} access, Efficiency={Efficiency:P}, Optimal={Optimal}")]
        private static partial void Log2DAnalysis(ILogger logger, object order, double efficiency, bool optimal);

        [LoggerMessage(EventId = 25066, Level = LogLevel.Debug,
            Message = "Profiling runtime memory access for {KernelName}")]
        private static partial void LogProfilingRuntime(ILogger logger, string kernelName);

        [LoggerMessage(EventId = 25067, Level = LogLevel.Information,
            Message = "Runtime profile for {KernelName}: Avg={AvgTime:F3}ms, Bandwidth={BW:F2} GB/s, Coalescing={Coal:P}")]
        private static partial void LogRuntimeProfile(ILogger logger, string kernelName, double avgTime, double bw, double coal);
    }
}
