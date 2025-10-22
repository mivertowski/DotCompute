// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using PipelineExecutionMetrics = DotCompute.Abstractions.Models.Pipelines.PipelineExecutionMetrics;

namespace DotCompute.Abstractions.Pipelines.Results
{
    /// <summary>
    /// Comprehensive result of pipeline execution with detailed metrics, stage results, and performance data.
    /// This class provides complete information about pipeline execution including success status,
    /// output data, timing metrics, and detailed stage-by-stage results.
    /// </summary>
    public sealed class PipelineExecutionResult
    {
        /// <summary>
        /// Gets whether the pipeline execution was successful.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the pipeline identifier that was executed.
        /// </summary>
        public required string PipelineId { get; init; }

        /// <summary>
        /// Gets the pipeline name.
        /// </summary>
        public required string PipelineName { get; init; }

        /// <summary>
        /// Gets the output data produced by the pipeline execution.
        /// Key represents the output name, value contains the actual data.
        /// </summary>
        public required IReadOnlyDictionary<string, object> Outputs { get; init; }

        /// <summary>
        /// Gets comprehensive execution metrics for the pipeline.
        /// </summary>
        public required PipelineExecutionMetrics Metrics { get; init; }

        /// <summary>
        /// Gets any errors that occurred during pipeline execution.
        /// </summary>
        public IReadOnlyList<PipelineError>? Errors { get; init; }

        /// <summary>
        /// Gets detailed results for each stage in the pipeline.
        /// </summary>
        public required IReadOnlyList<StageExecutionResult> StageResults { get; init; }

        /// <summary>
        /// Gets the timestamp when pipeline execution started.
        /// </summary>
        public DateTime? StartTime { get; init; }

        /// <summary>
        /// Gets the timestamp when pipeline execution completed.
        /// </summary>
        public DateTime? EndTime { get; init; }

        /// <summary>
        /// Gets the total execution time for the pipeline.
        /// </summary>
        public TimeSpan? TotalExecutionTime
            => (StartTime.HasValue && EndTime.HasValue) ? EndTime.Value - StartTime.Value : null;

        /// <summary>
        /// Gets the execution context that was used for the pipeline.
        /// </summary>
        public string? ExecutionContext { get; init; }

        /// <summary>
        /// Gets any warnings generated during pipeline execution.
        /// </summary>
        public IReadOnlyList<string>? Warnings { get; init; }

        /// <summary>
        /// Gets performance optimization recommendations based on the execution.
        /// </summary>
        public IReadOnlyList<string>? OptimizationRecommendations { get; init; }

        /// <summary>
        /// Gets resource usage information for the pipeline execution.
        /// </summary>
        public PipelineResourceUsage? ResourceUsage { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the execution.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }

        /// <summary>
        /// Gets the number of stages that executed successfully.
        /// </summary>
        public int SuccessfulStages => StageResults.Count(s => s.Success);

        /// <summary>
        /// Gets the number of stages that failed during execution.
        /// </summary>
        public int FailedStages => StageResults.Count(s => !s.Success);

        /// <summary>
        /// Gets the total number of stages in the pipeline.
        /// </summary>
        public int TotalStages => StageResults.Count;

        /// <summary>
        /// Gets whether any stages were skipped due to errors or conditions.
        /// </summary>
        public bool HasSkippedStages => StageResults.Any(s => s.Skipped);

        /// <summary>
        /// Creates a successful pipeline execution result.
        /// </summary>
        /// <param name="pipelineId">Pipeline identifier</param>
        /// <param name="pipelineName">Pipeline name</param>
        /// <param name="outputs">Output data from execution</param>
        /// <param name="metrics">Execution metrics</param>
        /// <param name="stageResults">Results from each stage</param>
        /// <param name="startTime">Optional start time</param>
        /// <param name="endTime">Optional end time</param>
        /// <param name="warnings">Optional warnings</param>
        /// <param name="optimizationRecommendations">Optional optimization recommendations</param>
        /// <param name="resourceUsage">Optional resource usage information</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Successful pipeline execution result</returns>
        public static PipelineExecutionResult CreateSuccess(
            string pipelineId,
            string pipelineName,
            IReadOnlyDictionary<string, object> outputs,
            PipelineExecutionMetrics metrics,
            IReadOnlyList<StageExecutionResult> stageResults,
            DateTime? startTime = null,
            DateTime? endTime = null,
            IReadOnlyList<string>? warnings = null,
            IReadOnlyList<string>? optimizationRecommendations = null,
            PipelineResourceUsage? resourceUsage = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => new()
            {
                Success = true,
                PipelineId = pipelineId,
                PipelineName = pipelineName,
                Outputs = outputs,
                Metrics = metrics,
                StageResults = stageResults,
                StartTime = startTime ?? DateTime.UtcNow,
                EndTime = endTime ?? DateTime.UtcNow,
                Warnings = warnings,
                OptimizationRecommendations = optimizationRecommendations,
                ResourceUsage = resourceUsage,
                Metadata = metadata
            };

        /// <summary>
        /// Creates a failed pipeline execution result.
        /// </summary>
        /// <param name="pipelineId">Pipeline identifier</param>
        /// <param name="pipelineName">Pipeline name</param>
        /// <param name="errors">Errors that occurred during execution</param>
        /// <param name="metrics">Execution metrics</param>
        /// <param name="stageResults">Results from stages that were executed</param>
        /// <param name="partialOutputs">Any partial outputs that were produced</param>
        /// <param name="startTime">Optional start time</param>
        /// <param name="endTime">Optional end time</param>
        /// <param name="warnings">Optional warnings</param>
        /// <param name="resourceUsage">Optional resource usage information</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Failed pipeline execution result</returns>
        public static PipelineExecutionResult CreateFailure(
            string pipelineId,
            string pipelineName,
            IReadOnlyList<PipelineError> errors,
            PipelineExecutionMetrics metrics,
            IReadOnlyList<StageExecutionResult> stageResults,
            IReadOnlyDictionary<string, object>? partialOutputs = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            IReadOnlyList<string>? warnings = null,
            PipelineResourceUsage? resourceUsage = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => new()
            {
                Success = false,
                PipelineId = pipelineId,
                PipelineName = pipelineName,
                Outputs = partialOutputs ?? new Dictionary<string, object>(),
                Metrics = metrics,
                Errors = errors,
                StageResults = stageResults,
                StartTime = startTime ?? DateTime.UtcNow,
                EndTime = endTime ?? DateTime.UtcNow,
                Warnings = warnings,
                ResourceUsage = resourceUsage,
                Metadata = metadata
            };

        /// <summary>
        /// Creates a failed pipeline execution result from a single exception.
        /// </summary>
        /// <param name="pipelineId">Pipeline identifier</param>
        /// <param name="pipelineName">Pipeline name</param>
        /// <param name="exception">Exception that caused the failure</param>
        /// <param name="metrics">Execution metrics</param>
        /// <param name="stageResults">Results from stages that were executed</param>
        /// <param name="partialOutputs">Any partial outputs that were produced</param>
        /// <param name="startTime">Optional start time</param>
        /// <param name="endTime">Optional end time</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Failed pipeline execution result</returns>
        public static PipelineExecutionResult CreateFailure(
            string pipelineId,
            string pipelineName,
            Exception exception,
            PipelineExecutionMetrics metrics,
            IReadOnlyList<StageExecutionResult> stageResults,
            IReadOnlyDictionary<string, object>? partialOutputs = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            IReadOnlyDictionary<string, object>? metadata = null)
        {
            var error = new PipelineError
            {
                ErrorType = PipelineErrorType.ExecutionError,
                Message = exception.Message,
                Exception = exception,
                Timestamp = DateTime.UtcNow,
                StageId = string.Empty
            };

            return CreateFailure(
                pipelineId,
                pipelineName,
                new[] { error },
                metrics,
                stageResults,
                partialOutputs,
                startTime,
                endTime,
                metadata: metadata);
        }

        /// <summary>
        /// Gets a summary report of the pipeline execution.
        /// </summary>
        /// <returns>Human-readable execution summary</returns>
        public string GetSummaryReport()
        {
            var report = new System.Text.StringBuilder();
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Pipeline: {PipelineName} ({PipelineId})");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Status: {(Success ? "SUCCESS" : "FAILED")}");
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Stages: {SuccessfulStages}/{TotalStages} successful");

            if (TotalExecutionTime.HasValue)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"Total Time: {TotalExecutionTime.Value.TotalMilliseconds:F2} ms");
            }

            _ = report.AppendLine(CultureInfo.InvariantCulture, $"Outputs: {Outputs.Count} items");

            if (Errors?.Count > 0)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"Errors: {Errors.Count}");
                foreach (var error in Errors.Take(3))
                {
                    _ = report.AppendLine(CultureInfo.InvariantCulture, $"  - {error.Message}");
                }
                if (Errors.Count > 3)
                {
                    _ = report.AppendLine(CultureInfo.InvariantCulture, $"  ... and {Errors.Count - 3} more");
                }
            }

            if (Warnings?.Count > 0)
            {
                _ = report.AppendLine(CultureInfo.InvariantCulture, $"Warnings: {Warnings.Count}");
            }

            return report.ToString();
        }

        /// <summary>
        /// Gets the first error that occurred during pipeline execution.
        /// </summary>
        /// <returns>First error or null if no errors occurred</returns>
        public PipelineError? GetFirstError() => Errors is { Count: > 0 } ? Errors[0] : null;

        /// <summary>
        /// Gets all errors of a specific type.
        /// </summary>
        /// <param name="errorType">The error type to filter by</param>
        /// <returns>Collection of errors of the specified type</returns>
        public IEnumerable<PipelineError> GetErrorsByType(PipelineErrorType errorType)
            => Errors?.Where(e => e.ErrorType == errorType) ?? Enumerable.Empty<PipelineError>();

        /// <summary>
        /// Gets the performance score for the pipeline execution (0-100 scale).
        /// </summary>
        /// <returns>Performance score based on success rate, execution time, and resource usage</returns>
        public double GetPerformanceScore()
        {
            if (!Success)
            {
                return 0.0;
            }

            // Base score from stage success rate

            var successRate = TotalStages > 0 ? (double)SuccessfulStages / TotalStages : 0.0;
            var baseScore = successRate * 60.0; // Up to 60 points for success

            // Time efficiency score (arbitrary scale, faster = higher score)
            var timeScore = TotalExecutionTime?.TotalMilliseconds > 0
                ? Math.Max(0, 20.0 - Math.Log10(TotalExecutionTime.Value.TotalMilliseconds))
                : 20.0;

            // Resource efficiency score
            var resourceScore = ResourceUsage?.GetEfficiencyScore() ?? 10.0;

            // Penalty for warnings
            var warningPenalty = (Warnings?.Count ?? 0) * 0.5;

            return Math.Max(0, Math.Min(100, baseScore + timeScore + resourceScore - warningPenalty));
        }
    }

    /// <summary>
    /// Resource usage information for pipeline execution.
    /// </summary>
    public sealed class PipelineResourceUsage
    {
        /// <summary>
        /// Gets the peak CPU usage percentage during pipeline execution.
        /// </summary>
        public double? PeakCpuUsage { get; init; }

        /// <summary>
        /// Gets the peak memory usage in bytes during pipeline execution.
        /// </summary>
        public long? PeakMemoryUsage { get; init; }

        /// <summary>
        /// Gets the peak GPU memory usage in bytes (if applicable).
        /// </summary>
        public long? PeakGpuMemoryUsage { get; init; }

        /// <summary>
        /// Gets the number of CPU cores utilized.
        /// </summary>
        public int? CpuCoresUsed { get; init; }

        /// <summary>
        /// Gets the number of GPU devices utilized.
        /// </summary>
        public int? GpuDevicesUsed { get; init; }

        /// <summary>
        /// Gets the total number of compute operations performed.
        /// </summary>
        public long? TotalComputeOperations { get; init; }

        /// <summary>
        /// Gets the average resource utilization percentage.
        /// </summary>
        public double? AverageResourceUtilization { get; init; }

        /// <summary>
        /// Gets detailed resource usage by stage.
        /// </summary>
        public IReadOnlyDictionary<string, object>? StageResourceUsage { get; init; }

        /// <summary>
        /// Gets an efficiency score for resource usage (0-20 scale).
        /// </summary>
        /// <returns>Efficiency score based on resource utilization</returns>
        public double GetEfficiencyScore()
        {
            var score = 10.0; // Base score

            // Bonus for high resource utilization
            if (AverageResourceUtilization.HasValue)
            {
                score += Math.Min(10.0, AverageResourceUtilization.Value / 10.0);
            }

            // Bonus for using multiple cores/devices
            if (CpuCoresUsed > 1)
            {
                score += 2.0;
            }


            if (GpuDevicesUsed > 0)
            {
                score += 3.0;
            }

            return Math.Min(20.0, score);
        }
    }
}
