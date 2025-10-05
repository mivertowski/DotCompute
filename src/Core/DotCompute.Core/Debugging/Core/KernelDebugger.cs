// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Performance;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using DotCompute.Abstractions.Interfaces.Kernels;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using DebugValidationSeverity = DotCompute.Abstractions.Validation.ValidationSeverity;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Core kernel debugging functionality for cross-backend validation and analysis.
/// </summary>
public sealed partial class KernelDebugger(ILogger<KernelDebugger> logger, DebugServiceOptions? options = null) : IDisposable
{
    private readonly ILogger<KernelDebugger> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DebugServiceOptions? _options = options;
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators = new();
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory = new();
    private bool _disposed;

    /// <summary>
    /// Adds an accelerator for cross-backend debugging.
    /// </summary>
    /// <param name="name">The accelerator name/identifier.</param>
    /// <param name="accelerator">The accelerator instance.</param>
    public void AddAccelerator(string name, IAccelerator accelerator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(accelerator);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_accelerators.TryAdd(name, accelerator))
        {
            LogAcceleratorAdded(name, accelerator.Type.ToString());
        }
        else
        {
            LogAcceleratorAlreadyExists(name);
        }
    }

    /// <summary>
    /// Removes an accelerator from debugging.
    /// </summary>
    /// <param name="name">The accelerator name to remove.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public bool RemoveAccelerator(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_accelerators.TryRemove(name, out var accelerator))
        {
            LogAcceleratorRemoved(name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all registered accelerators.
    /// </summary>
    /// <returns>Dictionary of accelerator names and instances.</returns>
    public IReadOnlyDictionary<string, IAccelerator> GetAccelerators()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _accelerators.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Validates a kernel across multiple backends for correctness.
    /// </summary>
    /// <param name="kernel">The kernel to validate.</param>
    /// <param name="inputs">Test input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cross-validation results.</returns>
    public async Task<CrossValidationResult> ValidateKernelAsync(
        IKernel kernel,
        object[] inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogValidationStarted(kernel.Name);

        var results = new List<KernelExecutionResult>();
        var validationIssues = new List<DebugValidationIssue>();

        try
        {
            // Execute kernel on all available accelerators
            foreach (var (name, accelerator) in _accelerators)
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var kernelResult = await ExecuteKernelSafelyAsync(kernel, accelerator, inputs, cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();

                    var executionResult = new KernelExecutionResult
                    {
                        KernelName = kernel.Name,
                        BackendType = accelerator.Type.ToString(),
                        Result = kernelResult,
                        ExecutionTime = stopwatch.Elapsed,
                        MemoryUsed = 0, // TODO: Add memory tracking
                        Success = true,
                        ErrorMessage = null,
                        ExecutedAt = DateTime.UtcNow
                    };

                    results.Add(executionResult);
                    _executionHistory.Enqueue(executionResult);

                    LogKernelExecuted(kernel.Name, name, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    var failureResult = new KernelExecutionResult
                    {
                        KernelName = kernel.Name,
                        BackendType = accelerator.Type.ToString(),
                        Result = null,
                        ExecutionTime = TimeSpan.Zero,
                        MemoryUsed = 0,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ExecutedAt = DateTime.UtcNow
                    };

                    results.Add(failureResult);
                    _executionHistory.Enqueue(failureResult);

                    validationIssues.Add(new DebugValidationIssue
                    {
                        Severity = DebugValidationSeverity.Error,
                        Message = $"Execution failed on {name}: {ex.Message}",
                        Context = $"Accelerator: {name}, Kernel: {kernel.Name}"
                    });

                    LogKernelExecutionFailed(kernel.Name, name, ex.Message);
                }
            }

            // Analyze results for consistency
            var consistencyIssues = AnalyzeResultConsistency(results);
            validationIssues.AddRange(consistencyIssues);

            var result = new CrossValidationResult
            {
                KernelName = kernel.Name,
                ExecutionResults = results,
                ValidationIssues = validationIssues,
                IsValid = validationIssues.All(i => i.Severity != DebugValidationSeverity.Error),
                ValidationTime = DateTime.UtcNow
            };

            LogValidationCompleted(kernel.Name, result.IsValid, validationIssues.Count);

            return result;
        }
        catch (Exception ex)
        {
            LogValidationFailed(kernel.Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Debugs a specific kernel execution with detailed analysis.
    /// </summary>
    /// <param name="kernel">The kernel to debug.</param>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="inputs">Input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed debug information.</returns>
    public async Task<KernelDebugInfo> DebugKernelAsync(
        IKernel kernel,
        IAccelerator accelerator,
        object[] inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogDebugStarted(kernel.Name, accelerator.Type.ToString());

        var startTime = DateTime.UtcNow;
        var memoryBefore = GC.GetTotalMemory(false);
        var inputValidation = ValidateInputs(kernel, inputs);

        try
        {
            // Execute with instrumentation
            var stopwatch = Stopwatch.StartNew();
            var executionResult = await ExecuteKernelSafelyAsync(kernel, accelerator, inputs, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var memoryAfter = GC.GetTotalMemory(false);
            var endTime = DateTime.UtcNow;

            var debugInfo = new KernelDebugInfo
            {
                KernelName = kernel.Name,
                AcceleratorType = accelerator.Type,
                StartTime = startTime,
                EndTime = endTime,
                InputValidation = inputValidation,
                MemoryUsageBefore = memoryBefore,
                MemoryUsageAfter = memoryAfter,
                MemoryAllocated = memoryAfter - memoryBefore,
                ExecutionTime = stopwatch.Elapsed,
                Output = executionResult,
                Success = true,
                PerformanceMetrics = null, // Will be set after creation
                ResourceUsage = null, // Will be set after creation
                Error = null,
                ErrorAnalysis = null
            };

            // Additional analysis - must create new object due to init-only properties
            var performanceMetrics = AnalyzePerformance(debugInfo);
            var resourceUsage = AnalyzeResourceUsage(debugInfo);

            debugInfo = new KernelDebugInfo
            {
                KernelName = debugInfo.KernelName,
                AcceleratorType = debugInfo.AcceleratorType,
                StartTime = debugInfo.StartTime,
                EndTime = debugInfo.EndTime,
                InputValidation = debugInfo.InputValidation,
                MemoryUsageBefore = debugInfo.MemoryUsageBefore,
                MemoryUsageAfter = debugInfo.MemoryUsageAfter,
                MemoryAllocated = debugInfo.MemoryAllocated,
                ExecutionTime = debugInfo.Timings,
                Output = debugInfo.Output,
                Success = debugInfo.Success,
                PerformanceMetrics = performanceMetrics,
                ResourceUsage = resourceUsage,
                Error = debugInfo.Error,
                ErrorAnalysis = debugInfo.ErrorAnalysis
            };

            LogDebugCompleted(kernel.Name, stopwatch.ElapsedMilliseconds);

            return debugInfo;
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            var errorAnalysis = AnalyzeError(ex);

            var debugInfo = new KernelDebugInfo
            {
                KernelName = kernel.Name,
                AcceleratorType = accelerator.Type,
                StartTime = startTime,
                EndTime = endTime,
                InputValidation = inputValidation,
                MemoryUsageBefore = memoryBefore,
                MemoryUsageAfter = GC.GetTotalMemory(false),
                MemoryAllocated = 0,
                ExecutionTime = TimeSpan.Zero,
                Output = null,
                Success = false,
                PerformanceMetrics = null,
                ResourceUsage = null,
                Error = ex,
                ErrorAnalysis = errorAnalysis
            };

            LogDebugFailed(kernel.Name, ex.Message);

            return debugInfo;
        }
    }

    /// <summary>
    /// Gets execution history for analysis.
    /// </summary>
    /// <param name="kernelName">Optional kernel name filter.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <returns>Collection of execution results.</returns>
    public IEnumerable<KernelExecutionResult> GetExecutionHistory(string? kernelName = null, int limit = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var history = _executionHistory.ToArray();

        if (!string.IsNullOrEmpty(kernelName))
        {
            history = history.Where(r => r.KernelName.Equals(kernelName, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        return history.OrderByDescending(r => r.ExecutedAt).Take(limit);
    }

    /// <summary>
    /// Clears execution history.
    /// </summary>
    public void ClearExecutionHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (_executionHistory.TryDequeue(out _))
        {
            // Clear all items
        }

        LogHistoryCleared();
    }

    /// <summary>
    /// Safely executes a kernel with error handling.
    /// </summary>
    private static async Task<object?> ExecuteKernelSafelyAsync(
        IKernel kernel,
        IAccelerator accelerator,
        object[] inputs,
        CancellationToken cancellationToken)
    {
        // Set timeout based on debug options
        using var timeoutCts = new CancellationTokenSource(_options.ExecutionTimeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // Execute kernel
        return await kernel.ExecuteAsync(accelerator, inputs, combinedCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Analyzes result consistency across accelerators.
    /// </summary>
    private static List<DebugValidationIssue> AnalyzeResultConsistency(IReadOnlyList<KernelExecutionResult> results)
    {
        var issues = new List<DebugValidationIssue>();

        if (results.Count < 2)
        {
            return issues; // Need at least 2 results to compare
        }

        var successfulResults = results.Where(r => r.Success).ToList();
        if (successfulResults.Count < 2)
        {
            return issues; // Need at least 2 successful results to compare
        }

        // Compare outputs (simplified - real implementation would need deep comparison)
        var firstOutput = successfulResults[0].Output;
        for (var i = 1; i < successfulResults.Count; i++)
        {
            var otherOutput = successfulResults[i].Output;

            if (!AreOutputsEqual(firstOutput, otherOutput))
            {
                issues.Add(new DebugValidationIssue
                {
                    Severity = DebugValidationSeverity.Error,
                    Message = $"Output mismatch between {successfulResults[0].BackendType} and {successfulResults[i].BackendType}",
                    Context = "Cross-backend consistency check"
                });
            }
        }

        // Compare execution times for performance anomalies
        var executionTimes = successfulResults.Select(r => r.Timings.TotalMilliseconds).ToList();
        var averageTime = executionTimes.Average();
        var maxTime = executionTimes.Max();

        if (maxTime > averageTime * 3) // If any execution is 3x slower than average
        {
            var slowResult = successfulResults.First(r => r.Timings.TotalMilliseconds == maxTime);
            issues.Add(new DebugValidationIssue
            {
                Severity = DebugValidationSeverity.Warning,
                Message = $"Performance anomaly detected on {slowResult.AcceleratorName}: {maxTime:F2}ms vs avg {averageTime:F2}ms",
                Context = "Performance consistency check"
            });
        }

        return issues;
    }

    /// <summary>
    /// Validates kernel inputs.
    /// </summary>
    private static InputValidationResult ValidateInputs(IKernel kernel, object[] inputs)
    {
        var result = new InputValidationResult { IsValid = true };
        var issues = new List<string>();

        // Basic validation
        if (inputs == null || inputs.Length == 0)
        {
            issues.Add("No inputs provided");
            result.IsValid = false;
        }

        // Check for null inputs
        for (var i = 0; i < inputs.Length; i++)
        {
            if (inputs[i] == null)
            {
                issues.Add($"Input at index {i} is null");
                result.IsValid = false;
            }
        }

        result.Issues = issues;
        return result;
    }

    /// <summary>
    /// Analyzes performance metrics.
    /// </summary>
    private static PerformanceMetrics AnalyzePerformance(KernelDebugInfo debugInfo)
    {
        return new PerformanceMetrics
        {
            ExecutionTimeMs = debugInfo.Timings.TotalMilliseconds,
            MemoryAllocatedBytes = debugInfo.MemoryAllocated,
            ThroughputOpsPerSec = CalculateThroughput(debugInfo),
            EfficiencyScore = CalculateEfficiencyScore(debugInfo)
        };
    }

    /// <summary>
    /// Analyzes resource usage.
    /// </summary>
    private static ResourceUsage AnalyzeResourceUsage(KernelDebugInfo debugInfo)
    {
        return new ResourceUsage
        {
            PeakMemoryUsage = debugInfo.MemoryUsageAfter,
            MemoryAllocationCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
            CpuTimeMs = debugInfo.Timings.TotalMilliseconds // Simplified
        };
    }

    /// <summary>
    /// Analyzes error information.
    /// </summary>
    private static ErrorAnalysis AnalyzeError(Exception exception)
    {
        return new ErrorAnalysis
        {
            ErrorType = exception.GetType().Name,
            ErrorMessage = exception.Message,
            IsTransient = IsTransientError(exception),
            Severity = DetermineErrorSeverity(exception),
            SuggestedActions = GetErrorSuggestions(exception)
        };
    }

    /// <summary>
    /// Compares two outputs for equality.
    /// </summary>
    private static bool AreOutputsEqual(object? output1, object? output2)
    {
        if (output1 == null && output2 == null)
        {
            return true;
        }


        if (output1 == null || output2 == null)
        {
            return false;
        }

        // For arrays/collections, compare element by element

        if (output1 is Array array1 && output2 is Array array2)
        {
            if (array1.Length != array2.Length)
            {
                return false;
            }


            for (var i = 0; i < array1.Length; i++)
            {
                if (!Equals(array1.GetValue(i), array2.GetValue(i)))
                {
                    return false;
                }
            }
            return true;
        }

        return Equals(output1, output2);
    }

    /// <summary>
    /// Calculates throughput based on execution info.
    /// </summary>
    private static double CalculateThroughput(KernelDebugInfo debugInfo)
    {
        // Simplified calculation - real implementation would depend on operation type
        if (debugInfo.Timings.TotalSeconds > 0)
        {
            return 1.0 / debugInfo.Timings.TotalSeconds;
        }
        return 0.0;
    }

    /// <summary>
    /// Calculates efficiency score.
    /// </summary>
    private static double CalculateEfficiencyScore(KernelDebugInfo debugInfo)
    {
        // Simplified scoring based on execution time and memory usage
        var timeScore = Math.Max(0, 100 - debugInfo.Timings.TotalMilliseconds / 10);
        var memoryScore = Math.Max(0, 100 - debugInfo.MemoryAllocated / (1024 * 1024)); // MB
        return (timeScore + memoryScore) / 2;
    }

    /// <summary>
    /// Determines if an error is transient.
    /// </summary>
    private static bool IsTransientError(Exception exception)
    {
        return exception is TimeoutException ||
               exception is HttpRequestException ||
               exception is SocketException ||
               (exception is IOException ioEx && ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines error severity.
    /// </summary>
    private static DebugValidationSeverity DetermineErrorSeverity(Exception exception)
    {
        return exception switch
        {
            ArgumentException => DebugValidationSeverity.Error,
            InvalidOperationException => DebugValidationSeverity.Error,
            NotSupportedException => DebugValidationSeverity.Warning,
            TimeoutException => DebugValidationSeverity.Warning,
            _ => DebugValidationSeverity.Error
        };
    }

    /// <summary>
    /// Gets error suggestions.
    /// </summary>
    private static List<string> GetErrorSuggestions(Exception exception)
    {
        return exception switch
        {
            ArgumentException => ["Check input parameters", "Validate argument types", "Ensure non-null values"],
            TimeoutException => ["Increase timeout duration", "Check system performance", "Reduce input data size"],
            OutOfMemoryException => ["Reduce input data size", "Check for memory leaks", "Increase available memory"],
            _ => ["Check logs for details", "Verify system requirements", "Contact support if issue persists"]
        };
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
            while (_executionHistory.TryDequeue(out _)) { }
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Accelerator {Name} ({Type}) added to debugger")]
    private partial void LogAcceleratorAdded(string name, string type);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Accelerator {Name} already exists")]
    private partial void LogAcceleratorAlreadyExists(string name);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Accelerator {Name} removed from debugger")]
    private partial void LogAcceleratorRemoved(string name);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Starting cross-validation for kernel {KernelName}")]
    private partial void LogValidationStarted(string kernelName);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Kernel {KernelName} executed on {AcceleratorName} in {ElapsedMs}ms")]
    private partial void LogKernelExecuted(string kernelName, string acceleratorName, long elapsedMs);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Kernel {KernelName} execution failed on {AcceleratorName}: {Error}")]
    private partial void LogKernelExecutionFailed(string kernelName, string acceleratorName, string error);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Cross-validation completed for kernel {KernelName}: Valid={IsValid}, Issues={IssueCount}")]
    private partial void LogValidationCompleted(string kernelName, bool isValid, int issueCount);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Cross-validation failed for kernel {KernelName}: {Error}")]
    private partial void LogValidationFailed(string kernelName, string error);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Starting debug session for kernel {KernelName} on {AcceleratorType}")]
    private partial void LogDebugStarted(string kernelName, string acceleratorType);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Debug session completed for kernel {KernelName} in {ElapsedMs}ms")]
    private partial void LogDebugCompleted(string kernelName, long elapsedMs);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Debug session failed for kernel {KernelName}: {Error}")]
    private partial void LogDebugFailed(string kernelName, string error);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Execution history cleared")]
    private partial void LogHistoryCleared();

    #endregion
}
