// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;

// Using aliases to resolve type conflicts
using DebugValidationIssue = DotCompute.Abstractions.Debugging.DebugValidationIssue;
using DebugValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;
using KernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;
using ResultComparison = DotCompute.Abstractions.Debugging.ResultComparison;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles cross-backend kernel validation and verification.
/// Provides comprehensive validation logic for ensuring kernel consistency across different backends.
/// </summary>
internal sealed partial class KernelDebugValidator(
    ILogger<KernelDebugValidator> logger,
    ConcurrentDictionary<string, IAccelerator> accelerators,
    KernelDebugProfiler profiler) : IDisposable
{
    private readonly ILogger<KernelDebugValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators = accelerators ?? throw new ArgumentNullException(nameof(accelerators));
#pragma warning disable CA2213 // Disposable fields should be disposed - Injected dependency, not owned by this class
    private readonly KernelDebugProfiler _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
#pragma warning restore CA2213
    private DebugServiceOptions _options = new();
    private bool _disposed;

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 11001,
        Level = MsLogLevel.Information,
        Message = "Starting cross-backend validation for kernel {KernelName}")]
    private static partial void LogValidationStarted(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 11002,
        Level = MsLogLevel.Error,
        Message = "Error during kernel validation")]
    private static partial void LogValidationError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 11003,
        Level = MsLogLevel.Error,
        Message = "Error executing kernel {KernelName} on {BackendType}")]
    private static partial void LogKernelExecutionError(ILogger logger, Exception ex, string kernelName, string backendType);

    [LoggerMessage(
        EventId = 11004,
        Level = MsLogLevel.Warning,
        Message = "Accelerator {BackendType} not available")]
    private static partial void LogAcceleratorNotAvailable(ILogger logger, string backendType);

    [LoggerMessage(
        EventId = 11005,
        Level = MsLogLevel.Error,
        Message = "Failed to create accelerator for backend {BackendType}")]
    private static partial void LogAcceleratorCreationFailed(ILogger logger, Exception ex, string backendType);

    #endregion
    /// <summary>
    /// Performs configure.
    /// </summary>
    /// <param name="options">The options.</param>

    public void Configure(DebugServiceOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));
    /// <summary>
    /// Validates the kernel async.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="inputs">The inputs.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        LogValidationStarted(_logger, kernelName);
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
                            Details = new Dictionary<string, object>
                            {
                                ["errors"] = string.Join("; ", results.Select(r => $"{r.BackendType}: {r.ErrorMessage}"))
                            }
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
                            Details = new Dictionary<string, object>
                            {
                                ["difference"] = comparison.Difference,
                                ["tolerance"] = tolerance,
                                ["backend1"] = successfulResults[i].BackendType ?? "Unknown",
                                ["backend2"] = successfulResults[j].BackendType ?? "Unknown"
                            }
                        });
                    }
                }
            }

            // Check performance consistency
            var performanceIssues = AnalyzePerformanceConsistency(successfulResults);
            issues.AddRange(performanceIssues);

            var isValid = issues.All(i => i.Severity != DebugValidationSeverity.Error);

            var issuesCollection = new Collection<DebugValidationIssue>();
            foreach (var issue in issues)
            {
                issuesCollection.Add(issue);
            }

            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = isValid,
                BackendsTested = successfulResults.Select(r => r.BackendType ?? "Unknown").ToArray(),
                Results = successfulResults,
                Comparisons = comparisonResults,
                Issues = issuesCollection,
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            LogValidationError(_logger, ex);
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
                        Details = new Dictionary<string, object>
                        {
                            ["exception"] = ex.Message,
                            ["exceptionType"] = ex.GetType().Name
                        }
                    }
                ],
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }
    /// <summary>
    /// Gets execute on backend asynchronously.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backendType">The backend type.</param>
    /// <param name="inputs">The inputs.</param>
    /// <returns>The result of the operation.</returns>

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
                ErrorMessage = $"Backend {backendType} not available",
                ExecutedAt = DateTime.UtcNow
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
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogKernelExecutionError(_logger, ex, kernelName, backendType);

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

    private static ResultComparison CompareResults(KernelExecutionResult result1, KernelExecutionResult result2, float tolerance)
    {
        if (result1.Output == null || result2.Output == null)
        {
            var nullMatch = result1.Output == result2.Output;
            return new ResultComparison
            {
                Backend1 = result1.BackendType ?? "Unknown",
                Backend2 = result2.BackendType ?? "Unknown",
                IsMatch = nullMatch,
                Difference = nullMatch ? 0f : 1f
            };
        }

        var (isMatch, difference, _) = CompareObjects(result1.Output, result2.Output, tolerance);
        return new ResultComparison
        {
            Backend1 = result1.BackendType ?? "Unknown",
            Backend2 = result2.BackendType ?? "Unknown",
            IsMatch = isMatch,
            Difference = difference
        };
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

    private static List<DebugValidationIssue> AnalyzePerformanceConsistency(KernelExecutionResult[] results)
    {
        var issues = new List<DebugValidationIssue>();

        if (results.Length < 2)
        {
            return issues;
        }


        var executionTimes = results.Select(r => r.Timings?.TotalTimeMs ?? 0).ToArray();
        var average = executionTimes.Average();
        var maxDeviation = executionTimes.Max(t => Math.Abs(t - average));

        // Flag significant performance variations (>50% deviation from average)
        if (maxDeviation > average * 0.5 && average > 1.0) // Only if average > 1ms
        {
            issues.Add(new DebugValidationIssue
            {
                Severity = DebugValidationSeverity.Warning,
                Message = "Significant performance variation detected across backends",
                Details = new Dictionary<string, object>
                {
                    ["averageMs"] = average,
                    ["maxDeviationMs"] = maxDeviation,
                    ["deviationPercent"] = (maxDeviation / average) * 100.0
                }
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

    private Task<IAccelerator?> GetOrCreateAcceleratorAsync(string backendType)
    {
        if (_accelerators.TryGetValue(backendType, out var existing))
        {
            return Task.FromResult<IAccelerator?>(existing);
        }

        try
        {
            // This is a simplified implementation - in practice, you'd use a factory
            // For now, we'll return null and let the caller handle missing accelerators
            LogAcceleratorNotAvailable(_logger, backendType);
            return Task.FromResult<IAccelerator?>(null);
        }
        catch (Exception ex)
        {
            LogAcceleratorCreationFailed(_logger, ex, backendType);
            return Task.FromResult<IAccelerator?>(null);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}