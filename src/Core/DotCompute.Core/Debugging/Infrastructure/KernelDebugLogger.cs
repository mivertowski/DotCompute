// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.Json;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Core.Debugging.Core;
using Microsoft.Extensions.Logging;

// Using aliases to resolve LogLevel conflicts
using DebugLogLevel = DotCompute.Abstractions.Debugging.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using DebugValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;
using AbstractionsPerformanceAnalysisResult = DotCompute.Abstractions.Debugging.PerformanceAnalysisResult;

namespace DotCompute.Core.Debugging.Infrastructure;

/// <summary>
/// Specialized logging infrastructure for kernel debugging operations.
/// Provides structured logging, performance metrics tracking, and debug output formatting.
/// </summary>
public sealed class KernelDebugLogger(
    ILogger<KernelDebugLogger> logger,
    DebugServiceOptions options) : IDisposable
{
    private readonly ILogger<KernelDebugLogger> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DebugServiceOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly List<DebugLogEntry> _debugHistory = [];
    private readonly object _lock = new();
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
            _logger.LogDebug("Starting validation for kernel {KernelName} with {InputCount} inputs and tolerance {Tolerance}",
                kernelName, inputs.Length, tolerance);
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

        _logger.Log(logLevel, "Validation completed for kernel {KernelName}: {IsValid} ({BackendCount} backends tested, {IssueCount} issues)",
            kernelName, result.IsValid, result.BackendsTested.Count, result.Issues.Count);

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

        _logger.LogError(exception, "Validation failed for kernel {KernelName}", kernelName);
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
            _logger.LogDebug("Starting execution of kernel {KernelName} on {BackendType}", kernelName, backendType);
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
                ["ExecutionTime"] = result.ExecutionTime.TotalMilliseconds,
                ["MemoryUsed"] = result.MemoryUsed,
                ["ErrorMessage"] = result.ErrorMessage ?? string.Empty
            }
        };

        var logLevel = result.Success ? MsLogLevel.Debug : MsLogLevel.Warning;
        await LogEntryAsync(logEntry, logLevel);

        if (_options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            if (result.Success)
            {
                _logger.LogDebug("Execution completed for kernel {KernelName} on {BackendType} in {ExecutionTime}ms",
                    kernelName, backendType, result.ExecutionTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Execution failed for kernel {KernelName} on {BackendType}: {ErrorMessage}",
                    kernelName, backendType, result.ErrorMessage);
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

        _logger.LogError(exception, "Execution failed for kernel {KernelName} on {BackendType}", kernelName, backendType);
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

        _logger.Log(logLevel, "Result comparison completed for kernel {KernelName}: {ResultsMatch} ({BackendCount} backends, {DifferenceCount} differences)",
            comparisonReport.KernelName, comparisonReport.ResultsMatch, comparisonReport.BackendsCompared.Count, comparisonReport.Differences.Count);

        // Log significant differences
        if (!comparisonReport.ResultsMatch && _options.VerbosityLevel >= DebugLogLevel.Warning)
        {
            foreach (var difference in comparisonReport.Differences.Take(5)) // Limit to first 5 differences
            {
                _logger.LogWarning("Difference between {Backend1} and {Backend2}: {Difference:F6}",
                    difference.Backend1, difference.Backend2, difference.Difference);
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
            _logger.LogDebug("Starting execution trace for kernel {KernelName} on {BackendType} with {TracePointCount} trace points",
                kernelName, backendType, tracePoints.Length);
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
            _logger.LogInformation("Execution trace completed for kernel {KernelName}: {TracePointCount} trace points captured in {TotalTime}ms",
                kernelName, trace.TracePoints.Count, trace.TotalExecutionTime.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning("Execution trace failed for kernel {KernelName}: {ErrorMessage}",
                kernelName, trace.ErrorMessage);
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

        _logger.LogError(exception, "Execution trace failed for kernel {KernelName}", kernelName);
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

        _logger.LogInformation("Performance analysis completed for kernel {KernelName}: {ExecutionCount} executions, {SuccessRate:P1} success rate, {PerformanceScore:F2} performance score",
            kernelName, analysisResult.ExecutionStatistics.TotalExecutions, analysisResult.ExecutionStatistics.SuccessRate, analysisResult.BottleneckAnalysis.OverallPerformanceScore);

        // Log significant bottlenecks
        foreach (var bottleneck in analysisResult.BottleneckAnalysis.Bottlenecks.Where(b => (int)b.Severity >= (int)CoreBottleneckSeverity.Medium))
        {
            _logger.LogWarning("Performance bottleneck detected in kernel {KernelName}: {Description} (Severity: {Severity})",
                kernelName, bottleneck.Description, bottleneck.Severity);
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
            _logger.LogInformation("Determinism analysis completed for kernel {KernelName}: Deterministic across {RunCount} runs",
                kernelName, analysisResult.RunCount);
        }
        else
        {
            _logger.LogWarning("Determinism analysis completed for kernel {KernelName}: Non-deterministic behavior detected (Variability: {VariabilityScore:F3})",
                kernelName, analysisResult.VariabilityScore);
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
        _ = report.AppendLine($"# Debug Report for Kernel: {kernelName}");
        _ = report.AppendLine($"Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = report.AppendLine($"Time window: {timeWindow?.ToString() ?? "24 hours"}");
        _ = report.AppendLine($"Total log entries: {relevantEntries.Count}");
        _ = report.AppendLine();

        // Group entries by operation
        var operationGroups = relevantEntries.GroupBy(e => e.Operation).ToList();

        foreach (var group in operationGroups)
        {
            _ = report.AppendLine($"## {group.Key} Operations ({group.Count()})");

            foreach (var entry in group.OrderBy(e => e.Timestamp))
            {
                _ = report.AppendLine($"- {entry.Timestamp:HH:mm:ss.fff}: {FormatLogEntryData(entry.Data)}");
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

        var exportData = new
        {
            KernelName = kernelName,
            ExportedAt = DateTime.UtcNow,
            TimeWindow = timeWindow?.ToString() ?? "24 hours",
            EntryCount = relevantEntries.Count,
            Entries = relevantEntries.Select(e => new
            {
                e.Timestamp,
                e.Operation,
                e.KernelName,
                Data = e.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString())
            })
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
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

        _logger.LogDebug("Cleared debug history older than {MaxAge}", maxAge);
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

        _logger.Log(logLevel, "Validation issue for kernel {KernelName} ({Severity}): {Message} [Backend: {BackendAffected}]",
            kernelName, issue.Severity, issue.Message, issue.BackendAffected);

        if (!string.IsNullOrEmpty(issue.Suggestion) && _options.VerbosityLevel >= DebugLogLevel.Debug)
        {
            _logger.LogDebug("Suggestion for kernel {KernelName}: {Suggestion}", kernelName, issue.Suggestion);
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
            _logger.Log(logLevel, message);
        }
    }

    /// <summary>
    /// Determines if a log level should be logged based on options.
    /// </summary>
    private static bool ShouldLog(MsLogLevel logLevel)
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