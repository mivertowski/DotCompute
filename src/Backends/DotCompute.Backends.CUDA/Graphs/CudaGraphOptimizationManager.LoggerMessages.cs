// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Graphs
{
    /// <summary>
    /// Logger message definitions for CudaGraphOptimizationManager using source-generated logging.
    /// Event ID range: 25041-25060
    /// </summary>
    public sealed partial class CudaGraphOptimizationManager
    {
        [LoggerMessage(EventId = 25041, Level = LogLevel.Information,
            Message = "CUDA Graph Optimization Manager initialized")]
        private partial void LogManagerInitialized();

        [LoggerMessage(EventId = 25042, Level = LogLevel.Debug,
            Message = "Beginning graph capture for {GraphName}")]
        private partial void LogBeginningGraphCapture(string graphName);

        [LoggerMessage(EventId = 25043, Level = LogLevel.Error,
            Message = "Error during graph capture operations")]
        private partial void LogGraphCaptureError(Exception exception);

        [LoggerMessage(EventId = 25044, Level = LogLevel.Information,
            Message = "Successfully captured graph {GraphName} with {NodeCount} nodes and {EdgeCount} edges in {CaptureTime:F2}ms")]
        private partial void LogGraphCaptureSuccess(string graphName, int nodeCount, int edgeCount, double captureTime);

        [LoggerMessage(EventId = 25045, Level = LogLevel.Debug,
            Message = "Launched graph {GraphName} in {LaunchTime:F3}ms (avg: {AvgTime:F3}ms)")]
        private partial void LogGraphLaunched(string graphName, double launchTime, double avgTime);

        [LoggerMessage(EventId = 25046, Level = LogLevel.Debug,
            Message = "Updating graph {GraphName}")]
        private partial void LogUpdatingGraph(string graphName);

        [LoggerMessage(EventId = 25047, Level = LogLevel.Information,
            Message = "Successfully updated graph {GraphName} in-place")]
        private partial void LogGraphUpdateSuccess(string graphName);

        [LoggerMessage(EventId = 25048, Level = LogLevel.Warning,
            Message = "In-place update failed for graph {GraphName} (result: {UpdateResult}), recreating")]
        private partial void LogUpdateFailed(string graphName, string updateResult);

        [LoggerMessage(EventId = 25049, Level = LogLevel.Information,
            Message = "Successfully cloned graph {SourceGraph} to {TargetGraph}")]
        private partial void LogGraphCloned(string sourceGraph, string targetGraph);

        [LoggerMessage(EventId = 25050, Level = LogLevel.Warning,
            Message = "Failed to get graph node count: {Error}")]
        private partial void LogGetNodeCountFailed(string error);

        [LoggerMessage(EventId = 25051, Level = LogLevel.Warning,
            Message = "Failed to get graph edge count: {Error}")]
        private partial void LogGetEdgeCountFailed(string error);

        [LoggerMessage(EventId = 25052, Level = LogLevel.Information,
            Message = "Exported graph visualization to {DotFile}")]
        private partial void LogGraphVisualizationExported(string dotFile);

        [LoggerMessage(EventId = 25053, Level = LogLevel.Warning,
            Message = "Failed to export graph visualization: {Error}")]
        private partial void LogGraphVisualizationFailed(string error);

        [LoggerMessage(EventId = 25054, Level = LogLevel.Error,
            Message = "Error exporting graph visualization")]
        private partial void LogGraphVisualizationError(Exception exception);

        [LoggerMessage(EventId = 25055, Level = LogLevel.Warning,
            Message = "Graph {GraphName} showing performance degradation. Last: {LastTime:F3}ms, Avg: {AvgTime:F3}ms")]
        private partial void LogPerformanceDegradation(string graphName, double lastTime, double avgTime);

        [LoggerMessage(EventId = 25056, Level = LogLevel.Warning,
            Message = "Graph {GraphName} has high failure rate: {FailureRate:P}")]
        private partial void LogHighFailureRate(string graphName, double failureRate);

        [LoggerMessage(EventId = 25057, Level = LogLevel.Information,
            Message = "Removed unused graph {GraphName} (last used: {LastUsed})")]
        private partial void LogUnusedGraphRemoved(string graphName, string lastUsed);

        [LoggerMessage(EventId = 25058, Level = LogLevel.Information,
            Message = "Graph optimization complete. Active graphs: {ActiveCount}, Removed: {RemovedCount}")]
        private partial void LogOptimizationComplete(int activeCount, int removedCount);

        [LoggerMessage(EventId = 25059, Level = LogLevel.Error,
            Message = "Error during graph optimization")]
        private partial void LogGraphOptimizationError(Exception exception);

        [LoggerMessage(EventId = 25060, Level = LogLevel.Information,
            Message = "Removed graph {GraphName}")]
        private partial void LogGraphRemoved(string graphName);

        [LoggerMessage(EventId = 25061, Level = LogLevel.Error,
            Message = "Error disposing graph instance")]
        private partial void LogGraphDisposeError(Exception exception);
    }
}
