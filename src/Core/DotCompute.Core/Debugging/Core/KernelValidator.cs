// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Interfaces.Kernels;
using Microsoft.Extensions.Logging;

// Using aliases to resolve ValidationIssue conflicts
using DebugValidationIssue = DotCompute.Abstractions.Debugging.DebugValidationIssue;
using DebugValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;
using KernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Core kernel validation logic for cross-backend verification.
/// Handles kernel execution across multiple backends and result comparison.
/// </summary>
public sealed partial class KernelValidator(
    ILogger<KernelValidator> logger,
    DebugServiceOptions options) : IDisposable
{
    private readonly ILogger<KernelValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DebugServiceOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators = new();
    private bool _disposed;

    #region LoggerMessage Delegates - Event ID range 11200-11206

    private static readonly Action<ILogger, string, Exception?> _logValidationStarting =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(11200, nameof(LogValidationStarting)),
            "Starting cross-backend validation for kernel {KernelName}");

    private static void LogValidationStarting(ILogger logger, string kernelName) => _logValidationStarting(logger, kernelName, null);

    private static readonly Action<ILogger, string, Exception?> _logValidationFailed =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(11201, nameof(LogValidationFailed)),
            "Error during kernel validation for {KernelName}");

    private static void LogValidationFailed(ILogger logger, string kernelName, Exception ex) => _logValidationFailed(logger, kernelName, ex);

    private static readonly Action<ILogger, string, Exception?> _logAcceleratorRegistered =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Debug,
            new EventId(11202, nameof(LogAcceleratorRegistered)),
            "Registered accelerator for backend: {BackendType}");

    private static void LogAcceleratorRegistered(ILogger logger, string backendType) => _logAcceleratorRegistered(logger, backendType, null);

    private static readonly Action<ILogger, string, Exception?> _logAcceleratorUnregistered =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Debug,
            new EventId(11203, nameof(LogAcceleratorUnregistered)),
            "Unregistered accelerator for backend: {BackendType}");

    private static void LogAcceleratorUnregistered(ILogger logger, string backendType) => _logAcceleratorUnregistered(logger, backendType, null);

    private static readonly Action<ILogger, string, Exception?> _logAcceleratorHealthCheckFailed =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(11204, nameof(LogAcceleratorHealthCheckFailed)),
            "Accelerator {BackendType} failed health check");

    private static void LogAcceleratorHealthCheckFailed(ILogger logger, string backendType, Exception ex) => _logAcceleratorHealthCheckFailed(logger, backendType, ex);

    private static readonly Action<ILogger, string, Exception?> _logAcceleratorCreationFailed =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            new EventId(11205, nameof(LogAcceleratorCreationFailed)),
            "Failed to create accelerator for backend: {BackendType}");

    private static void LogAcceleratorCreationFailed(ILogger logger, string backendType, Exception ex) => _logAcceleratorCreationFailed(logger, backendType, ex);

    private static readonly Action<ILogger, string, string, Exception?> _logKernelExecutionFailed =
        LoggerMessage.Define<string, string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(11206, nameof(LogKernelExecutionFailed)),
            "Kernel execution failed for {KernelName} on {BackendType}");

    private static void LogKernelExecutionFailed(ILogger logger, string kernelName, string backendType, Exception ex) => _logKernelExecutionFailed(logger, kernelName, backendType, ex);

    #endregion

    /// <summary>
    /// Validates a kernel across multiple backends with comprehensive comparison.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to validate.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="tolerance">Tolerance for numerical comparison.</param>
    /// <returns>Validation result with cross-backend comparison.</returns>
    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        LogValidationStarting(_logger, kernelName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get all available accelerators
            var availableAccelerators = await GetAvailableAcceleratorsAsync();
            var executionTasks = new List<Task<KernelExecutionResult>>();

            // Execute kernel on all available backends
            foreach (var accelerator in availableAccelerators)
            {
                var task = ExecuteKernelSafelyAsync(kernelName, accelerator.Key, inputs, accelerator.Value);
                executionTasks.Add(task);
            }

            var results = await Task.WhenAll(executionTasks);
            var successfulResults = results.Where(r => r.Success).ToArray();

            if (successfulResults.Length == 0)
            {
                return new KernelValidationResult
                {
                    KernelName = kernelName,
                    IsValid = false,
                    BackendsTested = [.. availableAccelerators.Keys],
                    Issues = [
                        new DebugValidationIssue
                        {
                            Severity = DebugValidationSeverity.Critical,
                            Message = "Kernel failed to execute on all available backends",
                            BackendAffected = "All"
                        }
                    ]
                };
            }

            // Compare results between backends
            var comparisonReport = await CompareResultsAsync(successfulResults, ComparisonStrategy.Tolerance);
            var issues = new List<DebugValidationIssue>();
            var maxDifference = 0f;

            if (!comparisonReport.ResultsMatch)
            {
                maxDifference = comparisonReport.Differences.Max(d => d.Difference);

                if (maxDifference > tolerance)
                {
                    issues.Add(new DebugValidationIssue
                    {
                        Severity = DebugValidationSeverity.Error,
                        Message = $"Results differ beyond tolerance ({maxDifference:F6} > {tolerance:F6})",
                        BackendAffected = string.Join(", ", comparisonReport.BackendsCompared),
                        Suggestion = "Check kernel implementation for numerical precision issues or backend-specific behavior"
                    });
                }
                else
                {
                    issues.Add(new DebugValidationIssue
                    {
                        Severity = DebugValidationSeverity.Warning,
                        Message = $"Minor differences detected ({maxDifference:F6})",
                        BackendAffected = string.Join(", ", comparisonReport.BackendsCompared),
                        Suggestion = "Consider if this level of precision is acceptable for your use case"
                    });
                }
            }

            // Analyze performance characteristics
            var performanceIssues = AnalyzePerformanceCharacteristics(successfulResults);
            issues.AddRange(performanceIssues);

            // Determine recommended backend
            var recommendedBackend = DetermineRecommendedBackend(successfulResults);

            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = issues.All(i => i.Severity != DebugValidationSeverity.Critical),
                BackendsTested = [.. successfulResults.Select(r => r.BackendType ?? "Unknown")],
                Results = [],  // Not using Results as IReadOnlyList<KernelExecutionResult>
                Issues = new Collection<DebugValidationIssue>(issues),
                ExecutionTime = stopwatch.Elapsed,
                MaxDifference = maxDifference,
                RecommendedBackend = recommendedBackend
            };
        }
        catch (Exception ex)
        {
            LogValidationFailed(_logger, kernelName, ex);

            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = false,
                Issues = [
                    new DebugValidationIssue
                    {
                        Severity = DebugValidationSeverity.Critical,
                        Message = $"Validation failed with exception: {ex.Message}",
                        BackendAffected = "All"
                    }
                ]
            };
        }
    }

    /// <summary>
    /// Executes a kernel on a specific backend.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute.</param>
    /// <param name="backendType">Type of backend to execute on.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <returns>Execution result.</returns>
    public async Task<KernelExecutionResult> ExecuteOnBackendAsync(
        string kernelName,
        string backendType,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentException.ThrowIfNullOrEmpty(backendType);
        ArgumentNullException.ThrowIfNull(inputs);

        var accelerator = await GetOrCreateAcceleratorAsync(backendType);
        if (accelerator == null)
        {
            // Create a placeholder handle for failed execution
            var handle = new KernelExecutionHandle
            {
                Id = Guid.NewGuid(),
                KernelName = kernelName,
                SubmittedAt = DateTimeOffset.UtcNow,
                IsCompleted = true,
                CompletedAt = DateTimeOffset.UtcNow
            };

            return new KernelExecutionResult
            {
                Handle = handle,
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                ErrorMessage = $"Backend '{backendType}' is not available",
                ExecutedAt = DateTime.UtcNow
            };
        }

        return await ExecuteKernelSafelyAsync(kernelName, backendType, inputs, accelerator);
    }

    /// <summary>
    /// Compares results from multiple kernel executions.
    /// </summary>
    /// <param name="results">Collection of execution results to compare.</param>
    /// <param name="comparisonStrategy">Strategy to use for comparison.</param>
    /// <returns>Comparison report.</returns>
    public async Task<ResultComparisonReport> CompareResultsAsync(
        IEnumerable<KernelExecutionResult> results,
        ComparisonStrategy comparisonStrategy = ComparisonStrategy.Tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        if (resultsList.Count < 2)
        {
            throw new ArgumentException("At least two results are required for comparison", nameof(results));
        }

        var kernelName = resultsList[0].KernelName ?? "Unknown";
        var backendNames = resultsList.Select(r => r.BackendType).ToArray();
        var differences = new List<ResultDifference>();
        var performanceComparison = new Dictionary<string, PerformanceMetrics>();

        // Build performance comparison
        foreach (var result in resultsList)
        {
            var executionTimeMs = result.Timings?.TotalTimeMs ?? 0;
            var memoryUsage = result.PerformanceCounters?.TryGetValue("MemoryUsed", out var mem) == true && mem is long l ? l : 0L;

            performanceComparison[result.BackendType ?? "Unknown"] = new PerformanceMetrics
            {
                ExecutionTimeMs = (long)Math.Round(executionTimeMs),
                MemoryUsageBytes = memoryUsage,
                OperationsPerSecond = (long)Math.Round(CalculateThroughput(result))
            };
        }

        // Compare results based on strategy
        var resultsMatch = await CompareResultsByStrategyAsync(resultsList, comparisonStrategy, differences);

        return new ResultComparisonReport
        {
            KernelName = kernelName,
            ResultsMatch = resultsMatch,
            BackendsCompared = (backendNames ?? Array.Empty<string?>()).Select(s => s ?? "Unknown").ToArray(),
            Differences = new Collection<ResultDifference>(differences),
            Strategy = comparisonStrategy,
            Tolerance = _options.VerbosityLevel == AbstractionsMemory.Debugging.LogLevel.Trace ? 1e-8f : 1e-6f,
            PerformanceComparison = performanceComparison
        };
    }

    /// <summary>
    /// Registers an accelerator for use in validation.
    /// </summary>
    /// <param name="backendType">Type of the backend.</param>
    /// <param name="accelerator">Accelerator instance.</param>
    public void RegisterAccelerator(string backendType, IAccelerator accelerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(backendType);
        ArgumentNullException.ThrowIfNull(accelerator);

        _ = _accelerators.TryAdd(backendType, accelerator);
        LogAcceleratorRegistered(_logger, backendType);
    }

    /// <summary>
    /// Unregisters an accelerator.
    /// </summary>
    /// <param name="backendType">Type of the backend to unregister.</param>
    /// <returns>True if successfully unregistered; otherwise, false.</returns>
    public bool UnregisterAccelerator(string backendType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(backendType);

        if (_accelerators.TryRemove(backendType, out _))
        {
            LogAcceleratorUnregistered(_logger, backendType);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets available accelerators for validation.
    /// </summary>
    private async Task<Dictionary<string, IAccelerator>> GetAvailableAcceleratorsAsync()
    {
        var availableAccelerators = new Dictionary<string, IAccelerator>();

        foreach (var accelerator in _accelerators)
        {
            try
            {
                // Basic health check for the accelerator
                if (await IsAcceleratorHealthyAsync(accelerator.Value))
                {
                    availableAccelerators[accelerator.Key] = accelerator.Value;
                }
            }
            catch (Exception ex)
            {
                LogAcceleratorHealthCheckFailed(_logger, accelerator.Key, ex);
            }
        }

        return availableAccelerators;
    }

    /// <summary>
    /// Gets or creates an accelerator for the specified backend type.
    /// </summary>
    private async Task<IAccelerator?> GetOrCreateAcceleratorAsync(string backendType)
    {
        if (_accelerators.TryGetValue(backendType, out var accelerator))
        {
            return accelerator;
        }

        // Try to create accelerator dynamically (simplified for example)
        try
        {
            accelerator = await CreateAcceleratorAsync(backendType);
            if (accelerator != null)
            {
                RegisterAccelerator(backendType, accelerator);
                return accelerator;
            }
        }
        catch (Exception ex)
        {
            LogAcceleratorCreationFailed(_logger, backendType, ex);
        }

        return null;
    }

    /// <summary>
    /// Creates an accelerator for the specified backend type.
    /// </summary>
    private static async Task<IAccelerator?> CreateAcceleratorAsync(string backendType)
    {
        // This would typically involve factory patterns or dependency injection
        // For now, return null as accelerators should be registered externally
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Checks if an accelerator is healthy and available for use.
    /// </summary>
    private static async Task<bool> IsAcceleratorHealthyAsync(IAccelerator accelerator)
    {
        try
        {
            // Basic health check - this could be expanded based on accelerator capabilities
            await Task.CompletedTask;
            return accelerator != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes a kernel safely with comprehensive error handling.
    /// </summary>
    private async Task<KernelExecutionResult> ExecuteKernelSafelyAsync(
        string kernelName,
        string backendType,
        object[] inputs,
        IAccelerator accelerator)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // TODO: Implement actual kernel execution based on accelerator type
            // This is a placeholder for the actual execution logic
            var result = await ExecuteKernelOnAcceleratorAsync(kernelName, inputs, accelerator);

            stopwatch.Stop();

            var handle = new KernelExecutionHandle
            {
                Id = Guid.NewGuid(),
                KernelName = kernelName,
                SubmittedAt = DateTimeOffset.UtcNow,
                IsCompleted = true,
                CompletedAt = DateTimeOffset.UtcNow
            };

            var executionTime = stopwatch.Elapsed.TotalMilliseconds;
            var memoryUsage = GetMemoryUsage(accelerator);

            return new KernelExecutionResult
            {
                Handle = handle,
                KernelName = kernelName,
                BackendType = backendType,
                Success = true,
                Output = result,
                Timings = new KernelExecutionTimings
                {
                    KernelTimeMs = executionTime,
                    TotalTimeMs = executionTime
                },
                PerformanceCounters = new Dictionary<string, object>
                {
                    ["MemoryUsed"] = memoryUsage,
                    ["ExecutionTimeMs"] = executionTime
                },
                ExecutedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            LogKernelExecutionFailed(_logger, kernelName, backendType, ex);

            var handle = new KernelExecutionHandle
            {
                Id = Guid.NewGuid(),
                KernelName = kernelName,
                SubmittedAt = DateTimeOffset.UtcNow,
                IsCompleted = true,
                CompletedAt = DateTimeOffset.UtcNow
            };

            return new KernelExecutionResult
            {
                Handle = handle,
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                ErrorMessage = ex.Message,
                Error = ex,
                Timings = new KernelExecutionTimings
                {
                    KernelTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds
                },
                ExecutedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Executes kernel on a specific accelerator.
    /// </summary>
    private static async Task<object> ExecuteKernelOnAcceleratorAsync(string kernelName, object[] inputs, IAccelerator accelerator)
    {
        // Placeholder implementation - this would be implemented based on accelerator interface
        await Task.Delay(10); // Simulate execution time
        return new { Success = true, KernelName = kernelName, Backend = accelerator.Type };
    }

    /// <summary>
    /// Gets memory usage for an accelerator.
    /// </summary>
    private static long GetMemoryUsage(IAccelerator accelerator)
        // Placeholder - would get actual memory usage from accelerator
        => 1024 * 1024; // 1MB placeholder

    /// <summary>
    /// Compares results using the specified strategy.
    /// </summary>
    private async Task<bool> CompareResultsByStrategyAsync(
        List<KernelExecutionResult> results,
        ComparisonStrategy strategy,
        List<ResultDifference> differences)
    {
        await Task.CompletedTask; // Make async for consistency

        switch (strategy)
        {
            case ComparisonStrategy.Tolerance:
                return CompareWithTolerance(results, differences);
            case ComparisonStrategy.Exact:
                return CompareExact(results, differences);
            case ComparisonStrategy.Statistical:
                return CompareStatistical(results, differences);
            default:
                return CompareWithTolerance(results, differences);
        }
    }

    /// <summary>
    /// Compares results with tolerance-based comparison.
    /// </summary>
    private bool CompareWithTolerance(List<KernelExecutionResult> results, IList<ResultDifference> differences)
    {
        // Simplified comparison logic - would be more sophisticated in practice
        var baseline = results[0];
        var tolerance = _options.VerbosityLevel == AbstractionsMemory.Debugging.LogLevel.Trace ? 1e-8f : 1e-6f;

        for (var i = 1; i < results.Count; i++)
        {
            var difference = CalculateDifference(baseline.Output, results[i].Output);
            differences.Add(new ResultDifference
            {
                Backend1 = baseline.BackendType ?? "Unknown",
                Backend2 = results[i].BackendType ?? "Unknown",
                Difference = difference,
                Location = $"Comparison_{i}"
            });

            if (difference > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares results with exact comparison.
    /// </summary>
    private static bool CompareExact(List<KernelExecutionResult> results, IList<ResultDifference> differences)
    {
        var baseline = results[0];

        for (var i = 1; i < results.Count; i++)
        {
            var isExactMatch = AreResultsExactlyEqual(baseline.Output, results[i].Output);
            differences.Add(new ResultDifference
            {
                Backend1 = baseline.BackendType ?? "Unknown",
                Backend2 = results[i].BackendType ?? "Unknown",
                Difference = isExactMatch ? 0f : 1f,
                Location = $"ExactMatch_{i}"
            });

            if (!isExactMatch)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares results with statistical comparison.
    /// </summary>
    private bool CompareStatistical(List<KernelExecutionResult> results, IList<ResultDifference> differences)
        // Simplified statistical comparison
        => CompareWithTolerance(results, differences);

    /// <summary>
    /// Calculates numerical difference between two results.
    /// </summary>
    private static float CalculateDifference(object? result1, object? result2)
    {
        // Simplified difference calculation - would handle various data types
        if (result1 == null || result2 == null)
        {

            return result1 == result2 ? 0f : 1f;
        }


        if (result1.Equals(result2))
        {

            return 0f;
        }

        // For numeric types, calculate relative difference

        if (result1 is float f1 && result2 is float f2)
        {
            return Math.Abs(f1 - f2) / Math.Max(Math.Abs(f1), Math.Abs(f2));
        }

        return 0.1f; // Placeholder difference for non-numeric types
    }

    /// <summary>
    /// Checks if two results are exactly equal.
    /// </summary>
    private static bool AreResultsExactlyEqual(object? result1, object? result2) => Equals(result1, result2);

    /// <summary>
    /// Calculates throughput for a kernel execution result.
    /// </summary>
    private static double CalculateThroughput(KernelExecutionResult result)
    {
        var executionTimeMs = result.Timings?.TotalTimeMs ?? 0;
        if (executionTimeMs == 0)
        {
            return 0;
        }

        // Simplified throughput calculation

        return 1000.0 / executionTimeMs;
    }

    /// <summary>
    /// Analyzes performance characteristics and identifies potential issues.
    /// </summary>
    private static List<DebugValidationIssue> AnalyzePerformanceCharacteristics(KernelExecutionResult[] results)
    {
        var issues = new List<DebugValidationIssue>();

        if (results.Length < 2)
        {
            return issues;
        }

        // Find performance outliers

        var executionTimes = results.Select(r => r.Timings?.TotalTimeMs ?? 0).ToArray();
        var avgTime = executionTimes.Average();
        var maxTime = executionTimes.Max();
        var minTime = executionTimes.Min();

        if (maxTime > avgTime * 2)
        {
            var slowestBackend = results.First(r => (r.Timings?.TotalTimeMs ?? 0) == maxTime).BackendType ?? "Unknown";
            issues.Add(new DebugValidationIssue
            {
                Severity = DebugValidationSeverity.Warning,
                Message = $"Backend {slowestBackend} is significantly slower ({maxTime:F2}ms vs avg {avgTime:F2}ms)",
                BackendAffected = slowestBackend,
                Suggestion = "Consider optimizing kernel for this backend or using an alternative"
            });
        }

        return issues;
    }

    /// <summary>
    /// Determines the recommended backend based on performance characteristics.
    /// </summary>
    private static string DetermineRecommendedBackend(KernelExecutionResult[] results)
    {
        if (results.Length == 0)
        {

            return "None";
        }

        // Simple logic: fastest execution time wins

        var fastest = results.OrderBy(r => r.Timings?.TotalTimeMs ?? double.MaxValue).First();
        return fastest.BackendType ?? "Unknown";
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _accelerators.Clear();
        }
    }
}