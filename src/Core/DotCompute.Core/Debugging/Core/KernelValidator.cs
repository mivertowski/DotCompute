// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

// Using aliases to resolve ValidationIssue conflicts
using DebugValidationIssue = DotCompute.Abstractions.Debugging.DebugValidationIssue;
using DebugValidationSeverity = DotCompute.Abstractions.Debugging.ValidationSeverity;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Core kernel validation logic for cross-backend verification.
/// Handles kernel execution across multiple backends and result comparison.
/// </summary>
public sealed class KernelValidator : IDisposable
{
    private readonly ILogger<KernelValidator> _logger;
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators;
    private readonly DebugServiceOptions _options;
    private bool _disposed;

    public KernelValidator(
        ILogger<KernelValidator> logger,
        DebugServiceOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _accelerators = new ConcurrentDictionary<string, IAccelerator>();
    }

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

        _logger.LogInformation("Starting cross-backend validation for kernel {kernelName}", kernelName);
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
                    BackendsTested = availableAccelerators.Keys.ToArray(),
                    Issues = [
                        new DebugValidationIssue
                        {
                            Severity = DebugValidationSeverity.Critical,
                            Message = "Kernel failed to execute on all available backends",
                            BackendAffected = "All"
                        }
                    ],
                    TotalValidationTime = stopwatch.Elapsed
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
                BackendsTested = successfulResults.Select(r => r.BackendType).ToArray(),
                Results = successfulResults.ToDictionary(r => r.BackendType, r => r.Result ?? new object()),
                Issues = issues,
                TotalValidationTime = stopwatch.Elapsed,
                MaxDifference = maxDifference,
                RecommendedBackend = recommendedBackend
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during kernel validation for {kernelName}", kernelName);

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
                ],
                TotalValidationTime = stopwatch.Elapsed
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
            return new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                ErrorMessage = $"Backend '{backendType}' is not available"
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

        var kernelName = resultsList[0].KernelName;
        var backendNames = resultsList.Select(r => r.BackendType).ToArray();
        var differences = new List<ResultDifference>();
        var performanceComparison = new Dictionary<string, PerformanceMetrics>();

        // Build performance comparison
        foreach (var result in resultsList)
        {
            performanceComparison[result.BackendType] = new PerformanceMetrics
            {
                ExecutionTime = result.ExecutionTime,
                MemoryUsage = result.MemoryUsed,
                ThroughputOpsPerSecond = (int)CalculateThroughput(result)
            };
        }

        // Compare results based on strategy
        var resultsMatch = await CompareResultsByStrategyAsync(resultsList, comparisonStrategy, differences);

        return new ResultComparisonReport
        {
            KernelName = kernelName,
            ResultsMatch = resultsMatch,
            BackendsCompared = backendNames,
            Differences = differences,
            Strategy = comparisonStrategy,
            Tolerance = _options.VerbosityLevel == Abstractions.Debugging.LogLevel.Trace ? 1e-8f : 1e-6f,
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

        _accelerators.TryAdd(backendType, accelerator);
        _logger.LogDebug("Registered accelerator for backend: {BackendType}", backendType);
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
            _logger.LogDebug("Unregistered accelerator for backend: {BackendType}", backendType);
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
                _logger.LogWarning(ex, "Accelerator {BackendType} failed health check", accelerator.Key);
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
            _logger.LogWarning(ex, "Failed to create accelerator for backend: {BackendType}", backendType);
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

            return new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = true,
                Result = result,
                ExecutionTime = stopwatch.Elapsed,
                MemoryUsed = GetMemoryUsage(accelerator)
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Kernel execution failed for {KernelName} on {BackendType}", kernelName, backendType);

            return new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = stopwatch.Elapsed
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
    {
        // Placeholder - would get actual memory usage from accelerator
        return 1024 * 1024; // 1MB placeholder
    }

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
    private bool CompareWithTolerance(List<KernelExecutionResult> results, List<ResultDifference> differences)
    {
        // Simplified comparison logic - would be more sophisticated in practice
        var baseline = results[0];
        var tolerance = _options.VerbosityLevel == Abstractions.Debugging.LogLevel.Trace ? 1e-8f : 1e-6f;

        for (var i = 1; i < results.Count; i++)
        {
            var difference = CalculateDifference(baseline.Result, results[i].Result);
            differences.Add(new ResultDifference
            {
                Backend1 = baseline.BackendType,
                Backend2 = results[i].BackendType,
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
    private bool CompareExact(List<KernelExecutionResult> results, List<ResultDifference> differences)
    {
        var baseline = results[0];

        for (var i = 1; i < results.Count; i++)
        {
            var isExactMatch = AreResultsExactlyEqual(baseline.Result, results[i].Result);
            differences.Add(new ResultDifference
            {
                Backend1 = baseline.BackendType,
                Backend2 = results[i].BackendType,
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
    private bool CompareStatistical(List<KernelExecutionResult> results, List<ResultDifference> differences)
    {
        // Simplified statistical comparison
        return CompareWithTolerance(results, differences);
    }

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
    private static bool AreResultsExactlyEqual(object? result1, object? result2)
    {
        return Equals(result1, result2);
    }

    /// <summary>
    /// Calculates throughput for a kernel execution result.
    /// </summary>
    private static double CalculateThroughput(KernelExecutionResult result)
    {
        if (result.ExecutionTime.TotalSeconds == 0)
        {
            return 0;
        }

        // Simplified throughput calculation

        return 1000.0 / result.ExecutionTime.TotalMilliseconds;
    }

    /// <summary>
    /// Analyzes performance characteristics and identifies potential issues.
    /// </summary>
    private List<DebugValidationIssue> AnalyzePerformanceCharacteristics(KernelExecutionResult[] results)
    {
        var issues = new List<DebugValidationIssue>();

        if (results.Length < 2)
        {
            return issues;
        }

        // Find performance outliers

        var executionTimes = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
        var avgTime = executionTimes.Average();
        var maxTime = executionTimes.Max();
        var minTime = executionTimes.Min();

        if (maxTime > avgTime * 2)
        {
            var slowestBackend = results.First(r => r.ExecutionTime.TotalMilliseconds == maxTime).BackendType;
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

        var fastest = results.OrderBy(r => r.ExecutionTime).First();
        return fastest.BackendType;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _accelerators.Clear();
        }
    }
}