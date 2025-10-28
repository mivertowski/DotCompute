// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotCompute.Abstractions.Debugging;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Kernels;

// Using aliases to resolve LogLevel conflicts
using DebugLogLevel = DotCompute.Abstractions.Debugging.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using DebugValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;
using AbstractionsPerformanceAnalysisResult = DotCompute.Abstractions.Debugging.PerformanceAnalysisResult;
using KernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;
using BottleneckSeverity = DotCompute.Abstractions.Debugging.BottleneckSeverity;

namespace DotCompute.Core.Debugging.Infrastructure;

/// <summary>
/// DTO for exporting debug history entries.
/// </summary>
internal sealed class DebugHistoryExportDto
{
    public required string KernelName { get; init; }
    public DateTime ExportedAt { get; init; }
    public required string TimeWindow { get; init; }
    public int EntryCount { get; init; }
    public required List<DebugLogEntryDto> Entries { get; init; }
}

/// <summary>
/// DTO for individual debug log entries.
/// </summary>
internal sealed class DebugLogEntryDto
{
    public DateTime Timestamp { get; init; }
    public required string Operation { get; init; }
    public required string KernelName { get; init; }
    public required Dictionary<string, string> Data { get; init; }
}

/// <summary>
/// JSON source generation context for AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DebugHistoryExportDto))]
[JsonSerializable(typeof(DebugLogEntryDto))]
internal partial class KernelDebugLoggerJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Specialized logging infrastructure for kernel debugging operations.
/// Provides structured logging, performance metrics tracking, and debug output formatting.
/// </summary>
public sealed partial class KernelDebugLogger(
    ILogger<KernelDebugLogger> logger,
    DebugServiceOptions options) : IDisposable
{
    private readonly ILogger<KernelDebugLogger> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DebugServiceOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly List<DebugLogEntry> _debugHistory = [];
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// Logs the start of kernel validation.
    /// </summary>
    /// <param name="kernelName">Name of the kernel being validated.</param>
    /// <param name="inputs">Input parameters for validation.</param>
    /// <param name="tolerance">Tolerance value for comparison.</param>
    public async Task LogValidationStartAsync(string kernelName, object[] inputs, float tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ValidationStart",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["InputCount"] = inputs.Length,
                ["Tolerance"] = tolerance,
                ["InputTypes"] = inputs.Select(i => i?.GetType().Name ?? "null").ToArray()
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Information);

        if (_options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            LogValidationStarting(_logger, kernelName, inputs.Length, tolerance);
        }
    }

    /// <summary>
    /// Logs the completion of kernel validation.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that was validated.</param>
    /// <param name="result">Validation result.</param>
    public async Task LogValidationCompletionAsync(string kernelName, KernelValidationResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ValidationComplete",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["IsValid"] = result.IsValid,
                ["BackendsTested"] = result.BackendsTested.Count,
                ["IssueCount"] = result.Issues.Count,
                ["TotalTime"] = result.TotalValidationTime.TotalMilliseconds,
                ["MaxDifference"] = result.MaxDifference,
                ["RecommendedBackend"] = result.RecommendedBackend ?? "None"
            }
        };

        var logLevel = result.IsValid ? MsLogLevel.Information : MsLogLevel.Warning;
        await LogEntryAsync(logEntry, logLevel);

        LogValidationCompleted(_logger, logLevel, kernelName, result.IsValid, result.BackendsTested.Count, result.Issues.Count);

        // Log individual issues if present
        foreach (var issue in result.Issues)
        {
            LogValidationIssue(kernelName, issue);
        }
    }

    /// <summary>
    /// Logs validation errors.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that failed validation.</param>
    /// <param name="exception">Exception that occurred during validation.</param>
    public async Task LogValidationErrorAsync(string kernelName, Exception exception)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ValidationError",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["ErrorType"] = exception.GetType().Name,
                ["ErrorMessage"] = exception.Message,
                ["StackTrace"] = exception.StackTrace ?? "No stack trace available"
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Error);

        LogValidationFailed(_logger, exception, kernelName);
    }

    /// <summary>
    /// Logs the start of kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel being executed.</param>
    /// <param name="backendType">Type of backend being used.</param>
    /// <param name="inputs">Input parameters for execution.</param>
    public async Task LogExecutionStartAsync(string kernelName, string backendType, object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ExecutionStart",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["BackendType"] = backendType,
                ["InputCount"] = inputs.Length,
                ["InputSizes"] = CalculateInputSizes(inputs)
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Debug);

        if (_options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            LogExecutionStarting(_logger, kernelName, backendType);
        }
    }

    /// <summary>
    /// Logs the completion of kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that was executed.</param>
    /// <param name="backendType">Type of backend that was used.</param>
    /// <param name="result">Execution result.</param>
    public async Task LogExecutionCompletionAsync(string kernelName, string backendType, KernelExecutionResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ExecutionComplete",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["BackendType"] = backendType,
                ["Success"] = result.Success,
                ["ExecutionTime"] = result.Timings?.TotalTimeMs ?? 0,
                ["MemoryUsed"] = result.PerformanceCounters?.ContainsKey("MemoryUsed") == true
                    ? result.PerformanceCounters["MemoryUsed"]
                    : 0,
                ["ErrorMessage"] = result.ErrorMessage ?? string.Empty
            }
        };

        var logLevel = result.Success ? MsLogLevel.Debug : MsLogLevel.Warning;
        await LogEntryAsync(logEntry, logLevel);

        if (_options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            if (result.Success)
            {
                LogExecutionCompleted(_logger, kernelName, backendType, result.Timings?.TotalTimeMs ?? 0);
            }
            else
            {
                LogExecutionFailedWithError(_logger, kernelName, backendType, result.ErrorMessage ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// Logs execution errors.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that failed execution.</param>
    /// <param name="backendType">Type of backend where execution failed.</param>
    /// <param name="exception">Exception that occurred during execution.</param>
    public async Task LogExecutionErrorAsync(string kernelName, string backendType, Exception exception)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ExecutionError",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["BackendType"] = backendType,
                ["ErrorType"] = exception.GetType().Name,
                ["ErrorMessage"] = exception.Message,
                ["StackTrace"] = exception.StackTrace ?? "No stack trace available"
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Error);

        LogExecutionError(_logger, exception, kernelName, backendType);
    }

    /// <summary>
    /// Logs the completion of result comparison.
    /// </summary>
    /// <param name="comparisonReport">Result comparison report.</param>
    public async Task LogComparisonCompletionAsync(ResultComparisonReport comparisonReport)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "ComparisonComplete",
            KernelName = comparisonReport.KernelName,
            Data = new Dictionary<string, object>
            {
                ["ResultsMatch"] = comparisonReport.ResultsMatch,
                ["BackendsCompared"] = comparisonReport.BackendsCompared.Count,
                ["DifferenceCount"] = comparisonReport.Differences.Count,
                ["Strategy"] = comparisonReport.Strategy.ToString(),
                ["Tolerance"] = comparisonReport.Tolerance
            }
        };

        var logLevel = comparisonReport.ResultsMatch ? MsLogLevel.Information : MsLogLevel.Warning;
        await LogEntryAsync(logEntry, logLevel);

        LogComparisonCompleted(_logger, logLevel, comparisonReport.KernelName, comparisonReport.ResultsMatch,
            comparisonReport.BackendsCompared.Count, comparisonReport.Differences.Count);

        // Log significant differences
        if (!comparisonReport.ResultsMatch && _options.VerbosityLevel >= DebugLogLevel.Warning)
        {
            foreach (var difference in comparisonReport.Differences.Take(5)) // Limit to first 5 differences
            {
                LogDifferenceBetweenBackends(_logger, difference.Backend1, difference.Backend2, difference.Difference);
            }
        }
    }

    /// <summary>
    /// Logs the start of kernel execution tracing.
    /// </summary>
    /// <param name="kernelName">Name of the kernel being traced.</param>
    /// <param name="backendType">Type of backend being used for tracing.</param>
    /// <param name="tracePoints">Trace points being monitored.</param>
    public async Task LogTracingStartAsync(string kernelName, string backendType, string[] tracePoints)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "TracingStart",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["BackendType"] = backendType,
                ["TracePointCount"] = tracePoints.Length,
                ["TracePoints"] = tracePoints
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Information);

        if (_options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            LogTracingStarting(_logger, kernelName, backendType, tracePoints.Length);
        }
    }

    /// <summary>
    /// Logs the completion of kernel execution tracing.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that was traced.</param>
    /// <param name="trace">Execution trace result.</param>
    public async Task LogTracingCompletionAsync(string kernelName, KernelExecutionTrace trace)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "TracingComplete",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["BackendType"] = trace.BackendType,
                ["Success"] = trace.Success,
                ["TotalTime"] = trace.TotalExecutionTime.TotalMilliseconds,
                ["TracePointCount"] = trace.TracePoints.Count,
                ["MemoryPeak"] = trace.MemoryProfile?.PeakMemory ?? 0,
                ["ErrorMessage"] = trace.ErrorMessage ?? string.Empty
            }
        };

        var logLevel = trace.Success ? MsLogLevel.Information : MsLogLevel.Warning;
        await LogEntryAsync(logEntry, logLevel);

        if (trace.Success)
        {
            LogTracingCompleted(_logger, kernelName, trace.TracePoints.Count, trace.TotalExecutionTime.TotalMilliseconds);
        }
        else
        {
            LogTracingFailedWithError(_logger, kernelName, trace.ErrorMessage ?? string.Empty);
        }
    }

    /// <summary>
    /// Logs tracing errors.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that failed tracing.</param>
    /// <param name="exception">Exception that occurred during tracing.</param>
    public async Task LogTracingErrorAsync(string kernelName, Exception exception)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "TracingError",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["ErrorType"] = exception.GetType().Name,
                ["ErrorMessage"] = exception.Message,
                ["StackTrace"] = exception.StackTrace ?? "No stack trace available"
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Error);

        LogTracingError(_logger, exception, kernelName);
    }

    /// <summary>
    /// Logs performance analysis results.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that was analyzed.</param>
    /// <param name="analysisResult">Performance analysis result.</param>
    public async Task LogPerformanceAnalysisAsync(string kernelName, AbstractionsPerformanceAnalysisResult analysisResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "PerformanceAnalysis",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["ExecutionCount"] = analysisResult.ExecutionStatistics.TotalExecutions,
                ["SuccessRate"] = analysisResult.ExecutionStatistics.SuccessRate,
                ["AvgExecutionTime"] = analysisResult.ExecutionStatistics.AverageExecutionTime,
                ["BottleneckCount"] = analysisResult.BottleneckAnalysis.Bottlenecks.Count,
                ["PerformanceScore"] = analysisResult.BottleneckAnalysis.OverallPerformanceScore,
                ["MemoryEfficiency"] = analysisResult.MemoryAnalysis.MemoryEfficiencyScore
            }
        };

        await LogEntryAsync(logEntry, MsLogLevel.Information);

        LogPerformanceAnalysisCompleted(_logger, kernelName, analysisResult.ExecutionStatistics.TotalExecutions,
            analysisResult.ExecutionStatistics.SuccessRate, (float)analysisResult.BottleneckAnalysis.OverallPerformanceScore);

        // Log significant bottlenecks
        foreach (var bottleneck in analysisResult.BottleneckAnalysis.Bottlenecks.Where(b => (int)b.Severity >= (int)BottleneckSeverity.Medium))
        {
            LogPerformanceBottleneck(_logger, kernelName, bottleneck.Description, bottleneck.Severity);
        }
    }

    /// <summary>
    /// Logs determinism analysis results.
    /// </summary>
    /// <param name="kernelName">Name of the kernel that was analyzed.</param>
    /// <param name="analysisResult">Determinism analysis result.</param>
    public async Task LogDeterminismAnalysisAsync(string kernelName, DeterminismAnalysisResult analysisResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var logEntry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = "DeterminismAnalysis",
            KernelName = kernelName,
            Data = new Dictionary<string, object>
            {
                ["IsDeterministic"] = analysisResult.IsDeterministic,
                ["RunCount"] = analysisResult.RunCount,
                ["VariabilityScore"] = analysisResult.VariabilityScore,
                ["NonDeterministicComponents"] = analysisResult.NonDeterministicComponents
            }
        };

        var logLevel = analysisResult.IsDeterministic ? MsLogLevel.Information : MsLogLevel.Warning;
        await LogEntryAsync(logEntry, logLevel);

        if (analysisResult.IsDeterministic)
        {
            LogDeterminismAnalysisCompleted(_logger, kernelName, analysisResult.RunCount);
        }
        else
        {
            LogNonDeterministicBehavior(_logger, kernelName, analysisResult.VariabilityScore);
        }
    }

    /// <summary>
    /// Generates a formatted debug report.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to generate report for.</param>
    /// <param name="timeWindow">Time window for the report.</param>
    /// <returns>Formatted debug report.</returns>
    public async Task<string> GenerateDebugReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.CompletedTask; // Make async for consistency

        var cutoffTime = DateTime.UtcNow - (timeWindow ?? TimeSpan.FromHours(24));
        var relevantEntries = GetRelevantLogEntries(kernelName, cutoffTime);

        var report = new StringBuilder();
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Debug Report for Kernel: {0}", kernelName));
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Generated at: {0:yyyy-MM-dd HH:mm:ss} UTC", DateTime.UtcNow));
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Time window: {0}", timeWindow?.ToString() ?? "24 hours"));
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Total log entries: {0}", relevantEntries.Count));
        _ = report.AppendLine();

        // Group entries by operation
        var operationGroups = relevantEntries.GroupBy(e => e.Operation).ToList();

        foreach (var group in operationGroups)
        {
            _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "## {0} Operations ({1})", group.Key, group.Count()));

            foreach (var entry in group.OrderBy(e => e.Timestamp))
            {
                _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "- {0:HH:mm:ss.fff}: {1}", entry.Timestamp, FormatLogEntryData(entry.Data)));
            }

            _ = report.AppendLine();
        }

        return report.ToString();
    }

    /// <summary>
    /// Exports debug history to JSON format.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to export history for.</param>
    /// <param name="timeWindow">Time window for the export.</param>
    /// <returns>JSON string containing debug history.</returns>
    public async Task<string> ExportDebugHistoryAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.CompletedTask; // Make async for consistency

        var cutoffTime = DateTime.UtcNow - (timeWindow ?? TimeSpan.FromHours(24));
        var relevantEntries = GetRelevantLogEntries(kernelName, cutoffTime);

        var exportData = new DebugHistoryExportDto
        {
            KernelName = kernelName,
            ExportedAt = DateTime.UtcNow,
            TimeWindow = timeWindow?.ToString() ?? "24 hours",
            EntryCount = relevantEntries.Count,
            Entries = relevantEntries.Select(e => new DebugLogEntryDto
            {
                Timestamp = e.Timestamp,
                Operation = e.Operation,
                KernelName = e.KernelName,
                Data = e.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "")
            }).ToList()
        };

        return JsonSerializer.Serialize(exportData, KernelDebugLoggerJsonContext.Default.DebugHistoryExportDto);
    }

    /// <summary>
    /// Clears debug history older than the specified age.
    /// </summary>
    /// <param name="maxAge">Maximum age of entries to keep.</param>
    public void ClearOldHistory(TimeSpan maxAge)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            _ = _debugHistory.RemoveAll(entry => entry.Timestamp < cutoffTime);
        }

        LogDebugHistoryCleared(_logger, maxAge);
    }

    /// <summary>
    /// Logs a validation issue.
    /// </summary>
    private void LogValidationIssue(string kernelName, DebugValidationIssue issue)
    {
        var logLevel = issue.Severity switch
        {
            DebugValidationSeverity.Info => MsLogLevel.Information,
            DebugValidationSeverity.Warning => MsLogLevel.Warning,
            DebugValidationSeverity.Error => MsLogLevel.Error,
            DebugValidationSeverity.Critical => MsLogLevel.Critical,
            _ => MsLogLevel.Debug
        };

        LogValidationIssueDetected(_logger, logLevel, kernelName, issue.Severity, issue.Message, issue.BackendAffected);

        if (!string.IsNullOrEmpty(issue.Suggestion) && _options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            LogValidationIssueSuggestion(_logger, kernelName, issue.Suggestion);
        }
    }

    /// <summary>
    /// Logs a debug entry asynchronously.
    /// </summary>
    private async Task LogEntryAsync(DebugLogEntry entry, MsLogLevel logLevel)
    {
        await Task.CompletedTask; // Make async for consistency

        // Store in history if enabled
        if (_options.EnableDetailedTracing)
        {
            lock (_lock)
            {
                _debugHistory.Add(entry);

                // Limit history size
                if (_debugHistory.Count > 10000)
                {
                    _debugHistory.RemoveRange(0, 1000);
                }
            }
        }

        // Log based on verbosity level
        if (ShouldLog(logLevel))
        {
            var message = $"[{entry.Operation}] {entry.KernelName}: {FormatLogEntryData(entry.Data)}";
            LogDebugEntry(_logger, logLevel, message);
        }
    }

    /// <summary>
    /// Determines if a log level should be logged based on options.
    /// </summary>
    private bool ShouldLog(MsLogLevel logLevel)
    {
        return _options.VerbosityLevel switch
        {
            DebugLogLevel.Trace => true,
            DebugLogLevel.Debug => logLevel >= MsLogLevel.Debug,
            DebugLogLevel.Information => logLevel >= MsLogLevel.Information,
            DebugLogLevel.Warning => logLevel >= MsLogLevel.Warning,
            DebugLogLevel.Error => logLevel >= MsLogLevel.Error,
            _ => logLevel >= MsLogLevel.Information
        };
    }

    /// <summary>
    /// Calculates input sizes for logging.
    /// </summary>
    private static object[] CalculateInputSizes(object[] inputs)
    {
        return inputs.Select(input => input switch
        {
            Array array => $"{array.Length} elements",
            string str => $"{str.Length} chars",
            _ => input?.GetType().Name ?? "null"
        }).ToArray();
    }

    /// <summary>
    /// Gets relevant log entries for a kernel within a time window.
    /// </summary>
    private List<DebugLogEntry> GetRelevantLogEntries(string kernelName, DateTime cutoffTime)
    {
        lock (_lock)
        {
            return [.. _debugHistory
                .Where(entry => entry.KernelName == kernelName && entry.Timestamp >= cutoffTime)
                .OrderBy(entry => entry.Timestamp)];
        }
    }

    /// <summary>
    /// Formats log entry data for display.
    /// </summary>
    private static string FormatLogEntryData(Dictionary<string, object> data)
    {
        var formattedPairs = data.Select(kvp => $"{kvp.Key}={kvp.Value}");
        return string.Join(", ", formattedPairs);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            lock (_lock)
            {
                _debugHistory.Clear();
            }
        }
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 11100,
        Level = MsLogLevel.Debug,
        Message = "Starting validation for kernel {KernelName} with {InputCount} inputs and tolerance {Tolerance}")]
    private static partial void LogValidationStarting(ILogger logger, string kernelName, int inputCount, float tolerance);

    [LoggerMessage(
        EventId = 11101,
        Message = "Validation completed for kernel {KernelName}: {IsValid} ({BackendCount} backends tested, {IssueCount} issues)")]
    private static partial void LogValidationCompleted(ILogger logger, MsLogLevel level, string kernelName, bool isValid, int backendCount, int issueCount);

    [LoggerMessage(
        EventId = 11102,
        Level = MsLogLevel.Error,
        Message = "Validation failed for kernel {KernelName}")]
    private static partial void LogValidationFailed(ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(
        EventId = 11103,
        Level = MsLogLevel.Debug,
        Message = "Starting execution of kernel {KernelName} on {BackendType}")]
    private static partial void LogExecutionStarting(ILogger logger, string kernelName, string backendType);

    [LoggerMessage(
        EventId = 11104,
        Level = MsLogLevel.Debug,
        Message = "Execution completed for kernel {KernelName} on {BackendType} in {ExecutionTime}ms")]
    private static partial void LogExecutionCompleted(ILogger logger, string kernelName, string backendType, double executionTime);

    [LoggerMessage(
        EventId = 11105,
        Level = MsLogLevel.Warning,
        Message = "Execution failed for kernel {KernelName} on {BackendType}: {ErrorMessage}")]
    private static partial void LogExecutionFailedWithError(ILogger logger, string kernelName, string backendType, string errorMessage);

    [LoggerMessage(
        EventId = 11106,
        Level = MsLogLevel.Error,
        Message = "Execution failed for kernel {KernelName} on {BackendType}")]
    private static partial void LogExecutionError(ILogger logger, Exception exception, string kernelName, string backendType);

    [LoggerMessage(
        EventId = 11107,
        Message = "Result comparison completed for kernel {KernelName}: {ResultsMatch} ({BackendCount} backends, {DifferenceCount} differences)")]
    private static partial void LogComparisonCompleted(ILogger logger, MsLogLevel level, string kernelName, bool resultsMatch, int backendCount, int differenceCount);

    [LoggerMessage(
        EventId = 11108,
        Level = MsLogLevel.Warning,
        Message = "Difference between {Backend1} and {Backend2}: {Difference:F6}")]
    private static partial void LogDifferenceBetweenBackends(ILogger logger, string backend1, string backend2, float difference);

    [LoggerMessage(
        EventId = 11109,
        Level = MsLogLevel.Debug,
        Message = "Starting execution trace for kernel {KernelName} on {BackendType} with {TracePointCount} trace points")]
    private static partial void LogTracingStarting(ILogger logger, string kernelName, string backendType, int tracePointCount);

    [LoggerMessage(
        EventId = 11110,
        Level = MsLogLevel.Information,
        Message = "Execution trace completed for kernel {KernelName}: {TracePointCount} trace points captured in {TotalTime}ms")]
    private static partial void LogTracingCompleted(ILogger logger, string kernelName, int tracePointCount, double totalTime);

    [LoggerMessage(
        EventId = 11111,
        Level = MsLogLevel.Warning,
        Message = "Execution trace failed for kernel {KernelName}: {ErrorMessage}")]
    private static partial void LogTracingFailedWithError(ILogger logger, string kernelName, string errorMessage);

    [LoggerMessage(
        EventId = 11112,
        Level = MsLogLevel.Error,
        Message = "Execution trace failed for kernel {KernelName}")]
    private static partial void LogTracingError(ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(
        EventId = 11113,
        Level = MsLogLevel.Information,
        Message = "Performance analysis completed for kernel {KernelName}: {ExecutionCount} executions, {SuccessRate:P1} success rate, {PerformanceScore:F2} performance score")]
    private static partial void LogPerformanceAnalysisCompleted(ILogger logger, string kernelName, int executionCount, double successRate, float performanceScore);

    [LoggerMessage(
        EventId = 11114,
        Level = MsLogLevel.Warning,
        Message = "Performance bottleneck detected in kernel {KernelName}: {Description} (Severity: {Severity})")]
    private static partial void LogPerformanceBottleneck(ILogger logger, string kernelName, string description, BottleneckSeverity severity);

    [LoggerMessage(
        EventId = 11115,
        Level = MsLogLevel.Information,
        Message = "Determinism analysis completed for kernel {KernelName}: Deterministic across {RunCount} runs")]
    private static partial void LogDeterminismAnalysisCompleted(ILogger logger, string kernelName, int runCount);

    [LoggerMessage(
        EventId = 11116,
        Level = MsLogLevel.Warning,
        Message = "Determinism analysis completed for kernel {KernelName}: Non-deterministic behavior detected (Variability: {VariabilityScore:F3})")]
    private static partial void LogNonDeterministicBehavior(ILogger logger, string kernelName, double variabilityScore);

    [LoggerMessage(
        EventId = 11117,
        Level = MsLogLevel.Debug,
        Message = "Cleared debug history older than {MaxAge}")]
    private static partial void LogDebugHistoryCleared(ILogger logger, TimeSpan maxAge);

    [LoggerMessage(
        EventId = 11118,
        Message = "Validation issue for kernel {KernelName} ({Severity}): {Message} [Backend: {BackendAffected}]")]
    private static partial void LogValidationIssueDetected(ILogger logger, MsLogLevel level, string kernelName, DebugValidationSeverity severity, string message, string backendAffected);

    [LoggerMessage(
        EventId = 11119,
        Level = MsLogLevel.Debug,
        Message = "Suggestion for kernel {KernelName}: {Suggestion}")]
    private static partial void LogValidationIssueSuggestion(ILogger logger, string kernelName, string suggestion);

    [LoggerMessage(
        EventId = 11120,
        Message = "{Message}")]
    private static partial void LogDebugEntry(ILogger logger, MsLogLevel level, string message);

    #endregion
}

/// <summary>
/// Represents a debug log entry.
/// </summary>
public record DebugLogEntry
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the operation.
    /// </summary>
    /// <value>The operation.</value>
    public string Operation { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    /// <value>The data.</value>
    public Dictionary<string, object> Data { get; init; } = [];
}