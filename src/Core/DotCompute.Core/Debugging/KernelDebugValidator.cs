// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions;

// Using aliases to resolve ValidationIssue conflicts
using DebugValidationIssue = DotCompute.Abstractions.Debugging.DebugValidationIssue;
using DebugValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles cross-backend kernel validation and verification.
/// Provides comprehensive validation logic for ensuring kernel consistency across different backends.
/// </summary>
internal sealed class KernelDebugValidator(
    ILogger<KernelDebugValidator> logger,
    ConcurrentDictionary<string, IAccelerator> accelerators,
    KernelDebugProfiler profiler) : IDisposable
{
    private readonly ILogger<KernelDebugValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators = accelerators ?? throw new ArgumentNullException(nameof(accelerators));
    private readonly KernelDebugProfiler _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
    private DebugServiceOptions _options = new();
    private bool _disposed;

    public void Configure(DebugServiceOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));

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
                    BackendsTested = [.. availableAccelerators.Keys],
                    Issues =
                    [
                        new()
                        {
                            Severity = DebugValidationSeverity.Error,
                            Message = "No backends successfully executed the kernel",
                            Details = string.Join("; ", results.Select(r => $"{r.BackendType}: {r.ErrorMessage}"))
                        }
                    ],
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            // Compare results across backends
            var comparisonResults = new List<ResultComparison>();
            var issues = new List<DebugValidationIssue>();

            for (var i = 0; i < successfulResults.Length; i++)
            {
                for (var j = i + 1; j < successfulResults.Length; j++)
                {
                    var comparison = CompareResults(successfulResults[i], successfulResults[j], tolerance);
                    comparisonResults.Add(comparison);

                    if (!comparison.IsMatch)
                    {
                        issues.Add(new DebugValidationIssue
                        {
                            Severity = DebugValidationSeverity.Error,
                            Message = $"Results differ between {successfulResults[i].BackendType} and {successfulResults[j].BackendType}",
                            Details = $"Difference: {comparison.Difference:F6}, Tolerance: {tolerance:F6}"
                        });
                    }
                }
            }

            // Check performance consistency
            var performanceIssues = AnalyzePerformanceConsistency(successfulResults);
            issues.AddRange(performanceIssues);

            var isValid = issues.All(i => i.Severity != DebugValidationSeverity.Error);

            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = isValid,
                BackendsTested = successfulResults.Select(r => r.BackendType).ToArray(),
                Results = successfulResults,
                Comparisons = comparisonResults,
                Issues = issues,
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during kernel validation");
            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = false,
                BackendsTested = [],
                Issues =
                [
                    new()
                    {
                        Severity = DebugValidationSeverity.Error,
                        Message = "Validation failed with exception",
                        Details = ex.Message
                    }
                ],
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

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
                ErrorMessage = $"Backend {backendType} not available"
            };
        }

        return await ExecuteKernelSafelyAsync(kernelName, backendType, inputs, accelerator);
    }

    private async Task<KernelExecutionResult> ExecuteKernelSafelyAsync(
        string kernelName,
        string backendType,
        object[] inputs,
        IAccelerator accelerator)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Execute with profiling
            var result = await _profiler.ExecuteWithProfilingAsync(kernelName, backendType, inputs, accelerator);

            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorMessage(ex, "Error executing kernel {kernelName} on {backendType}", kernelName, backendType);

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

    private ResultComparison CompareResults(KernelExecutionResult result1, KernelExecutionResult result2, float tolerance)
    {
        var comparison = new ResultComparison
        {
            Backend1 = result1.BackendType,
            Backend2 = result2.BackendType,
            IsMatch = false,
            Difference = float.MaxValue
        };

        if (result1.Result == null || result2.Result == null)
        {
            comparison.IsMatch = result1.Result == result2.Result;
            comparison.Difference = comparison.IsMatch ? 0f : 1f;
            return comparison;
        }

        var (isMatch, difference, _) = CompareObjects(result1.Result, result2.Result, tolerance);
        comparison.IsMatch = isMatch;
        comparison.Difference = difference;

        return comparison;
    }

    private static (bool isMatch, float difference, string details) CompareObjects(object obj1, object obj2, float tolerance)
    {
        if (obj1.GetType() != obj2.GetType())
        {
            return (false, 1.0f, "Type mismatch");
        }

        // Numeric comparison with tolerance
        if (obj1 is float f1 && obj2 is float f2)
        {
            var diff = Math.Abs(f1 - f2);
            return (diff <= tolerance, diff, $"Float difference: {diff}");
        }

        if (obj1 is double d1 && obj2 is double d2)
        {
            var diff = Math.Abs(d1 - d2);
            return (diff <= tolerance, (float)diff, $"Double difference: {diff}");
        }

        // Array comparison
        if (obj1 is Array arr1 && obj2 is Array arr2)
        {
            if (arr1.Length != arr2.Length)
            {
                return (false, 1.0f, "Array length mismatch");
            }

            var totalDifference = 0f;
            var elementCount = 0;

            for (var i = 0; i < arr1.Length; i++)
            {
                var (isMatch, diff, _) = CompareObjects(arr1.GetValue(i)!, arr2.GetValue(i)!, tolerance);
                totalDifference += diff;
                elementCount++;

                if (!isMatch && diff > tolerance)
                {
                    return (false, diff, $"Array element {i} differs by {diff}");
                }
            }

            var avgDifference = elementCount > 0 ? totalDifference / elementCount : 0f;
            return (avgDifference <= tolerance, avgDifference, $"Average difference: {avgDifference}");
        }

        // String comparison
        if (obj1 is string str1 && obj2 is string str2)
        {
            if (str1 == str2)
            {
                return (true, 0f, "");
            }

            var maxLength = Math.Max(str1.Length, str2.Length);
            var diff = maxLength > 0 ? Math.Abs(str1.Length - str2.Length) / (float)maxLength : 0f;
            return (false, diff, "String difference");
        }

        // Default: use ToString comparison
        var toString1 = obj1.ToString();
        var toString2 = obj2.ToString();
        return (toString1 == toString2, toString1 == toString2 ? 0f : 0.5f, "Object difference");
    }

    private List<DebugValidationIssue> AnalyzePerformanceConsistency(KernelExecutionResult[] results)
    {
        var issues = new List<DebugValidationIssue>();

        if (results.Length < 2)
        {
            return issues;
        }


        var executionTimes = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
        var average = executionTimes.Average();
        var maxDeviation = executionTimes.Max(t => Math.Abs(t - average));

        // Flag significant performance variations (>50% deviation from average)
        if (maxDeviation > average * 0.5 && average > 1.0) // Only if average > 1ms
        {
            issues.Add(new DebugValidationIssue
            {
                Severity = DebugValidationSeverity.Warning,
                Message = "Significant performance variation detected across backends",
                Details = $"Average: {average:F2}ms, Max deviation: {maxDeviation:F2}ms"
            });
        }

        return issues;
    }

    private async Task<Dictionary<string, IAccelerator>> GetAvailableAcceleratorsAsync()
    {
        var available = new Dictionary<string, IAccelerator>();

        foreach (var kvp in _accelerators)
        {
            if (kvp.Value != null)
            {
                available[kvp.Key] = kvp.Value;
            }
        }

        // Try to add default accelerators if none are available
        if (available.Count == 0)
        {
            var cpuAccelerator = await GetOrCreateAcceleratorAsync("CPU");
            if (cpuAccelerator != null)
            {
                available["CPU"] = cpuAccelerator;
            }
        }

        return available;
    }

    private async Task<IAccelerator?> GetOrCreateAcceleratorAsync(string backendType)
    {
        if (_accelerators.TryGetValue(backendType, out var existing))
        {
            return existing;
        }

        try
        {
            // This is a simplified implementation - in practice, you'd use a factory
            // For now, we'll return null and let the caller handle missing accelerators
            _logger.LogWarning("Accelerator {backendType} not available", backendType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to create accelerator for backend {backendType}", backendType);
            return null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents the result of comparing two kernel execution results.
/// </summary>
internal class ResultComparison
{
    /// <summary>
    /// Gets or sets the first backend type.
    /// </summary>
    public string Backend1 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the second backend type.
    /// </summary>
    public string Backend2 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the results match within tolerance.
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Gets or sets the measured difference between results.
    /// </summary>
    public float Difference { get; set; }
}