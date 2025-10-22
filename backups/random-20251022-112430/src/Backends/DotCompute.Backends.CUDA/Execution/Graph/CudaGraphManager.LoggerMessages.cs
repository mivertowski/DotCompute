// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Logger message definitions for CudaGraphManager using source-generated logging.
    /// Event ID range: 26021-26040
    /// </summary>
    public sealed partial class CudaGraphManager
    {
        // Graph Creation (26021-26025)

        [LoggerMessage(
            EventId = 26021,
            Level = LogLevel.Information,
            Message = "Created CUDA graph '{GraphName}'")]
        private partial void LogGraphCreated(string graphName);

        [LoggerMessage(
            EventId = 26022,
            Level = LogLevel.Debug,
            Message = "Beginning stream capture for graph '{GraphName}' with mode {Mode}")]
        private partial void LogBeginCapture(string graphName, string mode);

        [LoggerMessage(
            EventId = 26023,
            Level = LogLevel.Information,
            Message = "Captured CUDA graph '{GraphName}'")]
        private partial void LogGraphCaptured(string graphName);

        // Node Operations (26026-26032)

        [LoggerMessage(
            EventId = 26026,
            Level = LogLevel.Debug,
            Message = "Added kernel node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogKernelNodeAdded(string graphName, int nodeCount);

        [LoggerMessage(
            EventId = 26027,
            Level = LogLevel.Debug,
            Message = "Added memcpy node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogMemcpyNodeAdded(string graphName, int nodeCount);

        [LoggerMessage(
            EventId = 26028,
            Level = LogLevel.Debug,
            Message = "Added memset node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogMemsetNodeAdded(string graphName, int nodeCount);

        [LoggerMessage(
            EventId = 26029,
            Level = LogLevel.Debug,
            Message = "Added host node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogHostNodeAdded(string graphName, int nodeCount);

        [LoggerMessage(
            EventId = 26030,
            Level = LogLevel.Debug,
            Message = "Added event record node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogEventRecordNodeAdded(string graphName, int nodeCount);

        [LoggerMessage(
            EventId = 26031,
            Level = LogLevel.Debug,
            Message = "Added event wait node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogEventWaitNodeAdded(string graphName, int nodeCount);

        [LoggerMessage(
            EventId = 26032,
            Level = LogLevel.Debug,
            Message = "Added child graph node to graph '{GraphName}' (node count: {NodeCount})")]
        private partial void LogChildGraphNodeAdded(string graphName, int nodeCount);

        // Graph Instantiation and Execution (26033-26035)

        [LoggerMessage(
            EventId = 26033,
            Level = LogLevel.Information,
            Message = "Instantiated CUDA graph '{GraphName}' for execution")]
        private partial void LogGraphInstantiated(string graphName);

        [LoggerMessage(
            EventId = 26034,
            Level = LogLevel.Debug,
            Message = "Graph '{GraphName}' executed in {ExecutionTimeMs}ms")]
        private partial void LogGraphExecuted(string graphName, float executionTimeMs);

        [LoggerMessage(
            EventId = 26035,
            Level = LogLevel.Error,
            Message = "Failed to execute graph '{GraphName}'")]
        private partial void LogGraphExecutionFailed(string graphName, Exception exception);

        // Graph Update and Optimization (26036-26038)

        [LoggerMessage(
            EventId = 26036,
            Level = LogLevel.Information,
            Message = "Updated CUDA graph executable '{GraphName}'")]
        private partial void LogGraphUpdated(string graphName);

        [LoggerMessage(
            EventId = 26037,
            Level = LogLevel.Warning,
            Message = "Failed to update graph executable for '{GraphName}': {Result}")]
        private partial void LogGraphUpdateFailed(string graphName, string result);

        [LoggerMessage(
            EventId = 26038,
            Level = LogLevel.Information,
            Message = "Optimized graph '{GraphName}' with {OptimizationCount} optimizations applied")]
        private partial void LogGraphOptimized(string graphName, int optimizationCount);

        // Graph Cleanup (26039-26040)

        [LoggerMessage(
            EventId = 26039,
            Level = LogLevel.Information,
            Message = "Destroyed CUDA graph '{GraphName}'")]
        private partial void LogGraphDestroyed(string graphName);

        [LoggerMessage(
            EventId = 26040,
            Level = LogLevel.Debug,
            Message = "Destroyed CUDA graph executable '{GraphName}'")]
        private partial void LogGraphExecutableDestroyed(string graphName);

        // Additional logging methods for optimization phases

        [LoggerMessage(
            EventId = 26024,
            Level = LogLevel.Debug,
            Message = "Applying kernel fusion optimization to graph '{GraphName}'")]
        private partial void LogKernelFusionOptimization(string graphName);

        [LoggerMessage(
            EventId = 26025,
            Level = LogLevel.Debug,
            Message = "Applying memory access optimization to graph '{GraphName}'")]
        private partial void LogMemoryOptimization(string graphName);

        [LoggerMessage(
            EventId = 26041,
            Level = LogLevel.Debug,
            Message = "Maximizing parallelism for graph '{GraphName}'")]
        private partial void LogParallelismOptimization(string graphName);
    }
}
