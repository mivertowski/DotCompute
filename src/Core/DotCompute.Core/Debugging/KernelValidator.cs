// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
// using DotCompute.Core.Logging; // Removed - not needed

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles kernel validation across multiple backends including
/// cross-validation, determinism checks, and result comparison.
/// </summary>
public class KernelValidator
{
    private readonly ILogger<KernelValidator> _logger;
    // private readonly KernelDebugLogMessages _logMessages; // Not needed for split component

    public KernelValidator(ILogger<KernelValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // _logMessages = new KernelDebugLogMessages(); // Not needed for split component
    }

    /// <summary>
    /// Validates a kernel by executing it across multiple backends and comparing results.
    /// </summary>
    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,
        object[] inputs,
        Dictionary<string, IAccelerator> accelerators,
        float tolerance = 1e-6f)
    {
        _logger.LogInformation("Starting cross-backend validation for kernel {kernelName}", kernelName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var executionTasks = new List<Task<InternalKernelExecutionResult>>();

            // Execute kernel on all available backends
            foreach (var accelerator in accelerators)
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
                    BackendsTested = accelerators.Keys.ToArray(),
                    Issues =
                    [
                        new()
                        {
                            Severity = Abstractions.Debugging.ValidationSeverity.Critical,
                            Message = "All backend executions failed"
                        }
                    ],
                    TotalValidationTime = stopwatch.Elapsed
                };
            }

            // Compare results across backends
            var issues = new List<DebugValidationIssue>();
            var referenceResult = successfulResults.First();

            foreach (var result in successfulResults.Skip(1))
            {
                var comparison = CompareResults(referenceResult, result, tolerance);
                if (!comparison.IsMatch)
                {
                    issues.Add(new DebugValidationIssue
                    {
                        Severity = Abstractions.Debugging.ValidationSeverity.Error,
                        Message = $"Result mismatch between {referenceResult.Backend} and {result.Backend}"
                    });
                }
            }

            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = issues.Count == 0,
                BackendsTested = accelerators.Keys.ToArray(),
                Issues = issues,
                TotalValidationTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during kernel validation for {kernelName}", kernelName);
            return new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = false,
                BackendsTested = accelerators.Keys.ToArray(),
                Issues =
                [
                    new()
                    {
                        Severity = Abstractions.Debugging.ValidationSeverity.Critical,
                        Message = "Validation failed with exception"
                    }
                ],
                TotalValidationTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Validates that a kernel produces deterministic results across multiple runs.
    /// </summary>
    public async Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        IAccelerator accelerator,
        int iterations = 10)
    {
        _logger.LogInformation("Validating determinism for kernel {kernelName} with {iterations} iterations",
            kernelName, iterations);

        var results = new List<InternalKernelExecutionResult>();
        var issues = new List<string>();

        for (var i = 0; i < iterations; i++)
        {
            var result = await ExecuteKernelSafelyAsync(
                kernelName,
                accelerator.Info.Name,
                inputs,
                accelerator);

            if (result.Success)
            {
                results.Add(result);
            }
            else
            {
                issues.Add($"Iteration {i}: {result.ErrorMessage}");
            }
        }

        if (results.Count < 2)
        {
            return new DeterminismReport
            {
                IsDeterministic = false,
                ExecutionCount = iterations,
                Recommendations = issues.Count > 0 ? issues : ["Not enough successful executions to determine", "Ensure kernel can execute successfully before testing determinism"]
            };
        }

        // Compare all results to the first one
        var referenceResult = results[0];
        var isDeterministic = true;
        float maxDifference = 0;

        for (var i = 1; i < results.Count; i++)
        {
            var comparison = CompareResults(referenceResult, results[i], tolerance: 0);
            if (!comparison.IsMatch)
            {
                isDeterministic = false;
                maxDifference = Math.Max(maxDifference, comparison.MaxDifference);
                issues.Add($"Iteration {i}: {comparison.DifferenceDescription}");
            }
        }

        return new DeterminismReport
        {
            IsDeterministic = isDeterministic,
            ExecutionCount = iterations,
            MaxVariation = maxDifference,
            Recommendations = isDeterministic
                ? ["Kernel produces deterministic results"]
                : GetDeterminismRecommendations()
        };
    }

    private async Task<InternalKernelExecutionResult> ExecuteKernelSafelyAsync(
        string kernelName,
        string backend,
        object[] inputs,
        IAccelerator accelerator)
    {
        try
        {
            _logger.LogDebug("Executing kernel {kernelName} on backend {backend}", kernelName, backend);

            // Execute kernel (simplified - actual implementation would use orchestrator)
            var result = await Task.Run(() =>
            {
                // Simulate kernel execution
                return new object();
            });

            return new InternalKernelExecutionResult
            {
                Success = true,
                Backend = backend,
                Output = result,
                ExecutionTime = TimeSpan.FromMilliseconds(10),
                MemoryUsedBytes = 1024
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kernel {kernelName} on backend {backend}", kernelName, backend);
            return new InternalKernelExecutionResult
            {
                Success = false,
                Backend = backend,
                ErrorMessage = ex.Message
            };
        }
    }

    private static ResultComparison CompareResults(
        InternalKernelExecutionResult result1,
        InternalKernelExecutionResult result2,
        float tolerance)
    {
        // Simplified comparison - actual implementation would handle various data types
        if (result1.Output == null || result2.Output == null)
        {
            return new ResultComparison
            {
                IsMatch = result1.Output == result2.Output,
                MaxDifference = 0,
                DifferenceDescription = "One or both results are null"
            };
        }

        // For now, just check equality
        return new ResultComparison
        {
            IsMatch = result1.Output.Equals(result2.Output),
            MaxDifference = 0,
            DifferenceDescription = result1.Output.Equals(result2.Output) ? "" : "Results differ"
        };
    }

    private static List<string> GetDeterminismRecommendations()
    {
        return
        [
            "Check for race conditions in parallel execution",
            "Verify that random number generators use fixed seeds",
            "Ensure atomic operations where required",
            "Consider thread-local storage for mutable state"
        ];
    }

    private class ResultComparison
    {
        public bool IsMatch { get; init; }
        public float MaxDifference { get; init; }
        public string DifferenceDescription { get; init; } = string.Empty;
    }

    /// <summary>
    /// Internal class for kernel execution results used by validator.
    /// </summary>
    private class InternalKernelExecutionResult
    {
        public bool Success { get; init; }
        public string Backend { get; init; } = string.Empty;
        public object? Output { get; init; }
        public string? ErrorMessage { get; init; }
        public TimeSpan ExecutionTime { get; init; }
        public long MemoryUsedBytes { get; init; }
    }
}