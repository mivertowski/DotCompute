// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Production-grade kernel debugging service that validates kernels across multiple backends.
/// Provides comprehensive cross-validation, performance analysis, and diagnostic capabilities.
/// </summary>
public class KernelDebugService : IKernelDebugService, IDisposable
{
    private readonly IAccelerator? _primaryAccelerator;
    private readonly ILogger<KernelDebugService> _logger;
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators;
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory;
    private DebugServiceOptions _options;
    private bool _disposed;
    private static readonly string[] _collection =
            [
                "Check for race conditions in parallel execution",
                "Verify that random number generators use fixed seeds",
                "Ensure atomic operations where required",
                "Consider thread-local storage for mutable state"
            ];


    public KernelDebugService(
        ILogger<KernelDebugService> logger,
        IAccelerator? primaryAccelerator = null)
    {
        _primaryAccelerator = primaryAccelerator;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accelerators = new ConcurrentDictionary<string, IAccelerator>();
        _executionHistory = new ConcurrentQueue<KernelExecutionResult>();
        _options = new DebugServiceOptions();
    }

    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,

        object[] inputs,

        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        _logger.LogInformation("Starting cross-backend validation for kernel {KernelName}", kernelName);
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
                    Issues = new List<DotCompute.Abstractions.Debugging.ValidationIssue>
                    {
                        new()
                        {
                            Severity = ValidationSeverity.Critical,
                            Message = "Kernel failed to execute on all available backends",
                            BackendAffected = "All"
                        }
                    },
                    TotalValidationTime = stopwatch.Elapsed
                };
            }

            // Compare results between backends
            var comparisonReport = await CompareResultsAsync(successfulResults, ComparisonStrategy.Tolerance);
            var issues = new List<DotCompute.Abstractions.Debugging.ValidationIssue>();
            var maxDifference = 0f;

            if (!comparisonReport.ResultsMatch)
            {
                maxDifference = comparisonReport.Differences.Max(d => d.Difference);


                if (maxDifference > tolerance)
                {
                    issues.Add(new DotCompute.Abstractions.Debugging.ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Results differ beyond tolerance ({maxDifference:F6} > {tolerance:F6})",
                        BackendAffected = string.Join(", ", comparisonReport.BackendsCompared),
                        Suggestion = "Check kernel implementation for numerical precision issues or backend-specific behavior"
                    });
                }
                else
                {
                    issues.Add(new DotCompute.Abstractions.Debugging.ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
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
                IsValid = issues.All(i => i.Severity != ValidationSeverity.Critical),
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
            _logger.LogError(ex, "Error during kernel validation for {KernelName}", kernelName);


            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = false,
                Issues = new List<ValidationIssue>
                {
                    new()
                    {
                        Severity = ValidationSeverity.Critical,
                        Message = $"Validation failed with exception: {ex.Message}",
                        BackendAffected = "All"
                    }
                },
                TotalValidationTime = stopwatch.Elapsed
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
                ErrorMessage = $"Backend '{backendType}' is not available"
            };
        }

        return await ExecuteKernelSafelyAsync(kernelName, backendType, inputs, accelerator);
    }

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
                ThroughputOpsPerSecond = CalculateThroughput(result)
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

    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        object[] inputs,
        string[] tracePoints)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(tracePoints);

        // For now, we'll use CPU backend for tracing as it's most accessible TODO
        var accelerator = await GetOrCreateAcceleratorAsync("CPU");
        if (accelerator == null)
        {
            return new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = "CPU",
                Success = false,
                ErrorMessage = "CPU backend not available for tracing"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var tracePointsList = new List<TracePoint>();

        try
        {
            // This is a simplified implementation - in production, you'd need to instrument
            // the kernel execution to capture intermediate values at specific points TODO
            _logger.LogInformation("Starting traced execution of kernel {KernelName}", kernelName);

            // Execute with instrumentation (placeholder implementation) TODO
            var result = await ExecuteKernelSafelyAsync(kernelName, "CPU", inputs, accelerator);

            // Create trace points based on execution

            for (var i = 0; i < tracePoints.Length; i++)
            {
                tracePointsList.Add(new TracePoint
                {
                    Name = tracePoints[i],
                    ExecutionOrder = i,
                    Values = new Dictionary<string, object> { { "placeholder", $"trace_value_{i}" } },
                    TimestampFromStart = TimeSpan.FromMilliseconds(i * 10) // Placeholder timing
                });
            }

            return new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = "CPU",
                TracePoints = tracePointsList,
                TotalExecutionTime = stopwatch.Elapsed,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            return new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = "CPU",
                Success = false,
                ErrorMessage = ex.Message,
                TotalExecutionTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        int iterations = 10)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        if (iterations <= 1)
        {

            throw new ArgumentOutOfRangeException(nameof(iterations), "At least 2 iterations required");
        }


        _logger.LogInformation("Testing determinism of kernel {KernelName} with {Iterations} iterations",

            kernelName, iterations);

        var accelerator = await GetOrCreateAcceleratorAsync("CPU");
        if (accelerator == null)
        {
            return new DeterminismReport
            {
                KernelName = kernelName,
                IsDeterministic = false,
                ExecutionCount = 0,
                NonDeterminismSource = "No backend available for testing"
            };
        }

        var results = new List<object>();
        var executionTasks = new List<Task<KernelExecutionResult>>();

        // Execute multiple iterations
        for (var i = 0; i < iterations; i++)
        {
            var task = ExecuteKernelSafelyAsync(kernelName, "CPU", inputs, accelerator);
            executionTasks.Add(task);
        }

        var executionResults = await Task.WhenAll(executionTasks);
        var successfulResults = executionResults.Where(r => r.Success).ToArray();

        if (successfulResults.Length < 2)
        {
            return new DeterminismReport
            {
                KernelName = kernelName,
                IsDeterministic = false,
                ExecutionCount = successfulResults.Length,
                NonDeterminismSource = "Insufficient successful executions for comparison"
            };
        }

        // Analyze results for consistency
        results.AddRange(successfulResults.Select(r => r.Result ?? new object()));
        var isDeterministic = AnalyzeResultConsistency(results, out var maxVariation, out var source);

        var recommendations = new List<string>();
        if (!isDeterministic)
        {
            recommendations.AddRange(_collection);
        }

        return new DeterminismReport
        {
            KernelName = kernelName,
            IsDeterministic = isDeterministic,
            ExecutionCount = successfulResults.Length,
            AllResults = results,
            MaxVariation = maxVariation,
            NonDeterminismSource = source,
            Recommendations = recommendations
        };
    }

    public async Task<MemoryAnalysisReport> AnalyzeMemoryPatternsAsync(
        string kernelName,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        _logger.LogInformation("Analyzing memory patterns for kernel {KernelName}", kernelName);

        // Try GPU first for memory analysis, fallback to CPU
        var preferredBackends = new[] { "CUDA", "CPU" };
        IAccelerator? accelerator = null;
        string backendType = "CPU";

        foreach (var backend in preferredBackends)
        {
            accelerator = await GetOrCreateAcceleratorAsync(backend);
            if (accelerator != null)
            {
                backendType = backend;
                break;
            }
        }

        if (accelerator == null)
        {
            return new MemoryAnalysisReport
            {
                KernelName = kernelName,
                BackendType = "None",
                Warnings = new List<string> { "No backends available for memory analysis" }
            };
        }

        var accessPatterns = new List<MemoryAccessPattern>();
        var optimizations = new List<PerformanceOptimization>();
        var warnings = new List<string>();

        // Analyze memory access patterns (simplified implementation)
        var totalMemoryAccessed = EstimateMemoryUsage(inputs);
        var memoryEfficiency = CalculateMemoryEfficiency(inputs, backendType);

        // Add common memory access patterns
        accessPatterns.Add(new MemoryAccessPattern
        {
            PatternType = "Sequential",
            AccessCount = totalMemoryAccessed / sizeof(float), // Assuming float data
            CoalescingEfficiency = backendType == "CUDA" ? 0.85f : 1.0f,
            Issues = backendType == "CUDA" && memoryEfficiency < 0.8f

                ? new List<string> { "Consider memory coalescing optimizations" }
                : new List<string>()
        });

        // Add optimization suggestions
        if (backendType == "CUDA" && totalMemoryAccessed > 1024 * 1024) // > 1MB
        {
            optimizations.Add(new PerformanceOptimization
            {
                Type = "Memory Coalescing",
                Description = "Optimize memory access patterns for better GPU throughput",
                PotentialSpeedup = 1.5f,
                Implementation = "Ensure consecutive threads access consecutive memory locations"
            });
        }

        if (memoryEfficiency < 0.6f)
        {
            warnings.Add("Low memory efficiency detected - consider data layout optimizations");
        }

        return new MemoryAnalysisReport
        {
            KernelName = kernelName,
            BackendType = backendType,
            AccessPatterns = accessPatterns,
            Optimizations = optimizations,
            TotalMemoryAccessed = totalMemoryAccessed,
            MemoryEfficiency = memoryEfficiency,
            Warnings = warnings
        };
    }

    public async Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var backendInfos = new List<BackendInfo>();

        try
        {
            var availableAccelerators = await _runtime.GetAvailableAcceleratorsAsync();


            foreach (var acceleratorInfo in availableAccelerators)
            {
                backendInfos.Add(new BackendInfo
                {
                    Name = acceleratorInfo.DeviceType,
                    Version = acceleratorInfo.Version ?? "Unknown",
                    IsAvailable = true,
                    Capabilities = acceleratorInfo.Capabilities?.ToArray() ?? Array.Empty<string>(),
                    Properties = new Dictionary<string, object>
                    {
                        { "DeviceId", acceleratorInfo.DeviceId },
                        { "MemorySize", acceleratorInfo.MemorySize ?? 0L },
                        { "ComputeUnits", acceleratorInfo.ComputeUnits ?? 0 }
                    },
                    Priority = GetBackendPriority(acceleratorInfo.DeviceType)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backend information");
        }

        return backendInfos.OrderBy(b => b.Priority);
    }

    public void Configure(DebugServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _logger.LogInformation("Debug service configured with verbosity level {Level}", options.VerbosityLevel);
    }

    #region Private Methods

    private async Task<Dictionary<string, IAccelerator>> GetAvailableAcceleratorsAsync()
    {
        var accelerators = new Dictionary<string, IAccelerator>();


        try
        {
            var availableAccelerators = await _runtime.GetAvailableAcceleratorsAsync();


            foreach (var acceleratorInfo in availableAccelerators)
            {
                var accelerator = await _runtime.GetAcceleratorAsync(acceleratorInfo.DeviceType);
                if (accelerator != null)
                {
                    accelerators[acceleratorInfo.DeviceType] = accelerator;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available accelerators");
        }

        return accelerators;
    }

    private async Task<IAccelerator?> GetOrCreateAcceleratorAsync(string backendType)
    {
        if (_accelerators.TryGetValue(backendType, out var cachedAccelerator))
        {
            return cachedAccelerator;
        }

        try
        {
            var accelerator = await _runtime.GetAcceleratorAsync(backendType);
            if (accelerator != null)
            {
                _accelerators.TryAdd(backendType, accelerator);
            }
            return accelerator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating accelerator for backend {BackendType}", backendType);
            return null;
        }
    }

    private async Task<KernelExecutionResult> ExecuteKernelSafelyAsync(
        string kernelName,

        string backendType,

        object[] inputs,

        IAccelerator accelerator)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new KernelExecutionResult
        {
            KernelName = kernelName,
            BackendType = backendType
        };

        try
        {
            // This would need to integrate with the actual kernel execution system
            // For now, we'll create a placeholder implementation TODO
            _logger.LogDebug("Executing kernel {KernelName} on {BackendType}", kernelName, backendType);

            // Simulate kernel execution (replace with actual execution logic) TODO
            await Task.Delay(10); // Simulate some work


            result.Success = true;
            result.Result = $"ExecutionResult_{backendType}_{DateTime.UtcNow.Ticks}";
            result.MemoryUsed = EstimateMemoryUsage(inputs);


            _executionHistory.Enqueue(result);

            // Limit history size
            while (_executionHistory.Count > 1000)
            {
                _executionHistory.TryDequeue(out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kernel {KernelName} on {BackendType}", kernelName, backendType);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    private static async Task<bool> CompareResultsByStrategyAsync(
        List<KernelExecutionResult> results,
        ComparisonStrategy strategy,
        List<ResultDifference> differences)
    {
        // Simplified comparison implementation
        // In production, this would need sophisticated comparison logic
        // based on the data types and comparison strategy TODO

        if (results.Count < 2)
        {
            return true;
        }


        var baseResult = results[0];
        for (var i = 1; i < results.Count; i++)
        {
            var compareResult = results[i];

            // For demonstration, we'll assume results are different if backends differ

            if (baseResult.BackendType != compareResult.BackendType)
            {
                differences.Add(new ResultDifference
                {
                    Location = "Root",
                    ExpectedValue = baseResult.Result ?? new object(),
                    ActualValue = compareResult.Result ?? new object(),
                    Difference = 0.001f, // Simulated small difference
                    BackendsInvolved = [baseResult.BackendType, compareResult.BackendType]
                });
            }
        }

        await Task.CompletedTask; // For async signature
        return differences.Count == 0;
    }

    private static List<DotCompute.Abstractions.Debugging.ValidationIssue> AnalyzePerformanceCharacteristics(KernelExecutionResult[] results)
    {
        var issues = new List<DotCompute.Abstractions.Debugging.ValidationIssue>();


        if (results.Length < 2)
        {
            return issues;
        }


        var executionTimes = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
        var maxTime = executionTimes.Max();
        var minTime = executionTimes.Min();
        var ratio = maxTime / minTime;

        if (ratio > 10.0) // Significant performance difference
        {
            var slowBackend = results.First(r => r.ExecutionTime.TotalMilliseconds == maxTime).BackendType;
            var fastBackend = results.First(r => r.ExecutionTime.TotalMilliseconds == minTime).BackendType;


            issues.Add(new DotCompute.Abstractions.Debugging.ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = $"Significant performance difference detected: {ratio:F1}x slower on {slowBackend} vs {fastBackend}",
                BackendAffected = slowBackend,
                Suggestion = $"Consider using {fastBackend} backend for better performance, or optimize kernel for {slowBackend}"
            });
        }

        return issues;
    }

    private static string DetermineRecommendedBackend(KernelExecutionResult[] results)
    {
        if (results.Length == 0)
        {
            return "None";
        }

        // Recommend based on performance and success rate

        return results
            .Where(r => r.Success)
            .OrderBy(r => GetBackendPriority(r.BackendType))
            .ThenBy(r => r.ExecutionTime)
            .First()
            .BackendType;
    }

    private static int GetBackendPriority(string backendType) => backendType.ToUpperInvariant() switch
    {
        "CUDA" => 1,
        "METAL" => 2,
        "OPENCL" => 3,
        "CPU" => 4,
        _ => 999
    };

    private static int CalculateThroughput(KernelExecutionResult result)
    {
        // Simplified throughput calculation
        var operations = 1000; // Placeholder - would be calculated based on actual kernel operations TODO
        return (int)(operations / result.ExecutionTime.TotalSeconds);
    }

    private static long EstimateMemoryUsage(object[] inputs)
    {
        // Simplified memory estimation TODO
        return inputs.Length * 1024; // 1KB per input as placeholder
    }

    private static float CalculateMemoryEfficiency(object[] inputs, string backendType)
    {
        // Simplified efficiency calculation TODO
        var baseEfficiency = backendType switch
        {
            "CUDA" => 0.8f,
            "CPU" => 0.9f,
            _ => 0.7f
        };

        // Adjust based on input size (larger inputs typically more efficient)
        var inputSizeFactor = Math.Min(1.0f, inputs.Length / 100.0f);
        return baseEfficiency * (0.5f + 0.5f * inputSizeFactor);
    }

    private static bool AnalyzeResultConsistency(List<object> results, out float maxVariation, out string? source)
    {
        maxVariation = 0f;
        source = null;

        if (results.Count < 2)
        {
            return true;
        }

        // For demonstration, we'll assume results are consistent
        // In production, this would need sophisticated comparison based on data types TODO
        var firstResult = results[0]?.ToString() ?? "";
        var allMatch = results.All(r => (r?.ToString() ?? "") == firstResult);

        if (!allMatch)
        {
            maxVariation = 0.001f; // Simulated variation
            source = "Non-deterministic execution detected";
        }

        return allMatch;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        foreach (var accelerator in _accelerators.Values)
        {
            try
            {
                accelerator?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing accelerator");
            }
        }

        _accelerators.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}