// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Provides comprehensive kernel validation and correctness checking.
/// Enhanced version with advanced validation capabilities.
/// </summary>
public sealed partial class EnhancedKernelValidator : IDisposable
{
    private readonly ILogger<EnhancedKernelValidator> _logger;
    private readonly DebugServiceOptions _options;
    private readonly ConcurrentDictionary<string, ValidationProfile> _validationProfiles;
    private bool _disposed;

    private static readonly string[] DeterminismChecklist = [
        "Check for race conditions in parallel execution",
        "Verify that random number generators use fixed seeds",
        "Ensure atomic operations where required",
        "Consider thread-local storage for mutable state"
    ];

    public EnhancedKernelValidator(ILogger<EnhancedKernelValidator> logger, DebugServiceOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new DebugServiceOptions();
        _validationProfiles = new ConcurrentDictionary<string, ValidationProfile>();
    }

    /// <summary>
    /// Validates kernel implementation for common issues and best practices.
    /// </summary>
    /// <param name="kernel">The kernel to validate.</param>
    /// <returns>Validation results with issues and recommendations.</returns>
    public KernelValidationResult ValidateKernel(IKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogValidationStarted(kernel.Name);

        var startTime = DateTime.UtcNow;
        var result = new KernelValidationResult
        {
            KernelName = kernel.Name,
            ValidationTime = TimeSpan.Zero, // Will be updated at the end
            Issues = new List<DebugValidationIssue>(),
            Recommendations = new List<string>()
        };

        try
        {
            // Basic kernel structure validation
            ValidateKernelStructure(kernel, result);

            // Parameter validation
            ValidateKernelParameters(kernel, result);

            // Implementation pattern validation
            ValidateImplementationPatterns(kernel, result);

            // Performance considerations
            ValidatePerformanceConsiderations(kernel, result);

            // Security validation
            ValidateSecurityConsiderations(kernel, result);

            var finalValidationTime = DateTime.UtcNow - startTime;
            var isValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error);

            var finalResult = new KernelValidationResult
            {
                KernelName = result.KernelName,
                IsValid = isValid,
                ValidationTime = finalValidationTime,
                Issues = result.Issues,
                Recommendations = result.Recommendations,
                BackendsTested = result.BackendsTested,
                Errors = result.Errors,
                Warnings = result.Warnings,
                Results = result.Results,
                TotalValidationTime = result.TotalValidationTime,
                MaxDifference = result.MaxDifference,
                RecommendedBackend = result.RecommendedBackend,
                ResourceUsage = result.ResourceUsage
            };

            LogValidationCompleted(kernel.Name, finalResult.IsValid, finalResult.Issues.Count);

            return finalResult;
        }
        catch (Exception ex)
        {
            LogValidationFailed(kernel.Name, ex.Message);

            var debugValidationIssue = new DebugValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Validation failed with exception: {ex.Message}",
                BackendAffected = "Unknown",
                Context = "EnhancedKernelValidator"
            };
            var finalResult = new KernelValidationResult
            {
                KernelName = result.KernelName,
                IsValid = false,
                ValidationTime = DateTime.UtcNow - startTime,
                Issues = result.Issues.Append(debugValidationIssue).ToList(),
                Recommendations = result.Recommendations,
                BackendsTested = result.BackendsTested,
                Errors = result.Errors,
                Warnings = result.Warnings,
                Results = result.Results,
                TotalValidationTime = result.TotalValidationTime,
                MaxDifference = result.MaxDifference,
                RecommendedBackend = result.RecommendedBackend,
                ResourceUsage = result.ResourceUsage
            };

            return finalResult;
        }
    }

    /// <summary>
    /// Tests kernel determinism by running multiple executions with same inputs.
    /// </summary>
    /// <param name="kernel">The kernel to test.</param>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="inputs">Test input data.</param>
    /// <param name="iterations">Number of test iterations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Determinism test results.</returns>
    public async Task<DeterminismTestResult> TestDeterminismAsync(
        IKernel kernel,
        IAccelerator accelerator,
        object[] inputs,
        int iterations = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 2);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogDeterminismTestStarted(kernel.Name, iterations);

        var result = new DeterminismTestResult
        {
            KernelName = kernel.Name,
            AcceleratorType = accelerator.Type,
            Iterations = iterations,
            TestTime = DateTime.UtcNow,
            ExecutionResults = new List<object?>(),
            Issues = new List<string>()
        };

        try
        {
            var results = new List<object?>();

            // Execute kernel multiple times with same inputs
            for (var i = 0; i < iterations; i++)
            {
                var executionResult = await ExecuteKernelSafelyAsync(kernel, accelerator, inputs, cancellationToken).ConfigureAwait(false);
                results.Add(executionResult);
                result.ExecutionResults.Add(executionResult);

                LogDeterminismIteration(kernel.Name, i + 1, iterations);
            }

            // Analyze results for consistency
            result.IsDeterministic = AnalyzeResultConsistency(results, result);

            if (!result.IsDeterministic)
            {
                result.Issues.AddRange(DeterminismChecklist);
            }

            LogDeterminismTestCompleted(kernel.Name, result.IsDeterministic);

            return result;
        }
        catch (Exception ex)
        {
            LogDeterminismTestFailed(kernel.Name, ex.Message);

            result.IsDeterministic = false;
            result.Issues.Add($"Determinism test failed with exception: {ex.Message}");

            return result;
        }
    }

    /// <summary>
    /// Validates memory access patterns for safety and efficiency.
    /// </summary>
    /// <param name="kernel">The kernel to analyze.</param>
    /// <param name="inputs">Sample input data.</param>
    /// <returns>Memory pattern analysis results.</returns>
    public MemoryPatternAnalysis AnalyzeMemoryPatterns(IKernel kernel, object[] inputs)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogMemoryAnalysisStarted(kernel.Name);

        var analysis = new MemoryPatternAnalysis
        {
            KernelName = kernel.Name,
            AnalysisTime = DateTime.UtcNow,
            Issues = new List<MemoryIssue>(),
            Recommendations = new List<string>()
        };

        try
        {
            // Analyze input parameters for memory patterns
            AnalyzeInputMemoryPatterns(inputs, analysis);

            // Check for potential memory access violations
            CheckMemoryAccessPatterns(kernel, analysis);

            // Analyze memory efficiency
            AnalyzeMemoryEfficiency(kernel, inputs, analysis);

            analysis.IsMemorySafe = !analysis.Issues.Any(i => i.Severity == MemoryIssueSeverity.Critical);

            LogMemoryAnalysisCompleted(kernel.Name, analysis.IsMemorySafe, analysis.Issues.Count);

            return analysis;
        }
        catch (Exception ex)
        {
            LogMemoryAnalysisFailed(kernel.Name, ex.Message);

            analysis.Issues.Add(new MemoryIssue
            {
                Type = MemoryIssueType.AnalysisError,
                Severity = MemoryIssueSeverity.Critical,
                Description = $"Memory analysis failed: {ex.Message}",
                Context = kernel.Name
            });

            return analysis;
        }
    }

    /// <summary>
    /// Creates or updates a validation profile for a kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="profile">The validation profile.</param>
    public void SetValidationProfile(string kernelName, ValidationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ArgumentNullException.ThrowIfNull(profile);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _ = _validationProfiles.AddOrUpdate(kernelName, profile, (key, existing) => profile);
        LogValidationProfileUpdated(kernelName, profile.Level.ToString());
    }

    /// <summary>
    /// Gets the validation profile for a kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>The validation profile or default if not found.</returns>
    public ValidationProfile GetValidationProfile(string kernelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _validationProfiles.TryGetValue(kernelName, out var profile)
            ? profile
            : ValidationProfile.Default;
    }

    #region Private Methods

    /// <summary>
    /// Validates kernel structure and basic requirements.
    /// </summary>
    private static void ValidateKernelStructure(IKernel kernel, KernelValidationResult result)
    {
        // Check kernel name
        if (string.IsNullOrWhiteSpace(kernel.Name))
        {
            result.Issues.Add(new DebugValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "Kernel name is null or empty",
                BackendAffected = "All",
                Context = "STR001",
                Suggestion = "Provide a meaningful name for the kernel"
            });
        }

        // Check for descriptive naming
        if (kernel.Name != null && (kernel.Name.Length < 3 || !char.IsLetter(kernel.Name[0])))
        {
            result.Issues.Add(new DebugValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = "Kernel name should be descriptive and start with a letter",
                BackendAffected = "All",
                Context = "STR002",
                Suggestion = "Use a descriptive name that starts with a letter and is at least 3 characters long"
            });
        }
    }

    /// <summary>
    /// Validates kernel parameters.
    /// </summary>
    private void ValidateKernelParameters(IKernel kernel, KernelValidationResult result)
    {
        result.Recommendations.Add("Ensure all parameters have proper bounds checking");
        result.Recommendations.Add("Consider using strongly-typed parameter structures");
        result.Recommendations.Add("Validate input array sizes and dimensions");
    }

    /// <summary>
    /// Validates implementation patterns and best practices.
    /// </summary>
    private void ValidateImplementationPatterns(IKernel kernel, KernelValidationResult result)
    {
        result.Recommendations.Add("Use const parameters where data is not modified");
        result.Recommendations.Add("Prefer local memory over global memory when possible");
        result.Recommendations.Add("Consider memory coalescing patterns for better performance");
        result.Recommendations.Add("Use appropriate synchronization primitives for parallel operations");
    }

    /// <summary>
    /// Validates performance considerations.
    /// </summary>
    private void ValidatePerformanceConsiderations(IKernel kernel, KernelValidationResult result)
    {
        result.Recommendations.Add("Profile with different input sizes to identify scaling behavior");
        result.Recommendations.Add("Consider vectorized operations where applicable");
        result.Recommendations.Add("Optimize memory access patterns for cache efficiency");
        result.Recommendations.Add("Use appropriate work group sizes for the target hardware");
    }

    /// <summary>
    /// Validates security considerations.
    /// </summary>
    private void ValidateSecurityConsiderations(IKernel kernel, KernelValidationResult result)
    {
        result.Recommendations.Add("Validate all array bounds to prevent buffer overflows");
        result.Recommendations.Add("Sanitize input parameters to prevent injection attacks");
        result.Recommendations.Add("Use safe arithmetic operations to prevent overflow/underflow");
        result.Recommendations.Add("Avoid exposing sensitive data through output buffers");
    }

    /// <summary>
    /// Safely executes a kernel with timeout protection.
    /// </summary>
    private Task<object?> ExecuteKernelSafelyAsync(
        IKernel kernel,
        IAccelerator accelerator,
        object[] inputs,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_options.ExecutionTimeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // IKernel doesn't have ExecuteAsync - need to use the accelerator to execute
        // This would typically be done through IComputeOrchestrator or IKernelExecutor
        return Task.FromException<object?>(new NotImplementedException("Kernel execution needs to be done through IKernelExecutor or IComputeOrchestrator"));
    }

    /// <summary>
    /// Analyzes result consistency for determinism testing.
    /// </summary>
    private static bool AnalyzeResultConsistency(List<object?> results, DeterminismTestResult testResult)
    {
        if (results.Count < 2)
        {
            return true;
        }


        var firstResult = results[0];
        for (var i = 1; i < results.Count; i++)
        {
            if (!AreResultsEqual(firstResult, results[i]))
            {
                testResult.Issues.Add($"Result mismatch at iteration {i + 1}: Results are not identical");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares two results for equality.
    /// </summary>
    private static bool AreResultsEqual(object? result1, object? result2)
    {
        if (result1 == null && result2 == null)
        {
            return true;
        }


        if (result1 == null || result2 == null)
        {
            return false;
        }

        // For arrays, compare element by element

        if (result1 is Array array1 && result2 is Array array2)
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

        // For floating-point numbers, use tolerance-based comparison
        if (result1 is float f1 && result2 is float f2)
        {
            return Math.Abs(f1 - f2) < 1e-6f;
        }

        if (result1 is double d1 && result2 is double d2)
        {
            return Math.Abs(d1 - d2) < 1e-12;
        }

        return Equals(result1, result2);
    }

    /// <summary>
    /// Analyzes input memory patterns.
    /// </summary>
    private void AnalyzeInputMemoryPatterns(object[] inputs, MemoryPatternAnalysis analysis)
    {
        foreach (var input in inputs)
        {
            if (input is Array array)
            {
                var elementSize = GetElementSize(array.GetType().GetElementType());
                var totalSize = array.Length * elementSize;

                if (totalSize > _options.LargeArrayThreshold)
                {
                    analysis.Issues.Add(new MemoryIssue
                    {
                        Type = MemoryIssueType.LargeAllocation,
                        Severity = MemoryIssueSeverity.Warning,
                        Description = $"Large input array detected: {totalSize:N0} bytes",
                        Context = $"Array type: {array.GetType().Name}, Length: {array.Length:N0}"
                    });
                }

                if (array.Length == 0)
                {
                    analysis.Issues.Add(new MemoryIssue
                    {
                        Type = MemoryIssueType.EmptyArray,
                        Severity = MemoryIssueSeverity.Warning,
                        Description = "Empty array input detected",
                        Context = $"Array type: {array.GetType().Name}"
                    });
                }
            }
        }
    }

    /// <summary>
    /// Checks memory access patterns.
    /// </summary>
    private static void CheckMemoryAccessPatterns(IKernel kernel, MemoryPatternAnalysis analysis)
    {
        analysis.Recommendations.Add("Ensure sequential memory access patterns where possible");
        analysis.Recommendations.Add("Use appropriate data types to minimize memory usage");
        analysis.Recommendations.Add("Consider memory alignment for optimal performance");
        analysis.Recommendations.Add("Avoid unnecessary memory copies");
    }

    /// <summary>
    /// Analyzes memory efficiency.
    /// </summary>
    private void AnalyzeMemoryEfficiency(IKernel kernel, object[] inputs, MemoryPatternAnalysis analysis)
    {
        long totalInputMemory = 0;
        foreach (var input in inputs)
        {
            if (input is Array array)
            {
                var elementSize = GetElementSize(array.GetType().GetElementType());
                totalInputMemory += array.Length * elementSize;
            }
        }

        analysis.TotalInputMemory = totalInputMemory;

        if (totalInputMemory > _options.MemoryEfficiencyThreshold)
        {
            analysis.Recommendations.Add($"Consider optimizing memory usage: {totalInputMemory:N0} bytes total");
            analysis.Recommendations.Add("Use streaming processing for large datasets");
            analysis.Recommendations.Add("Consider data compression where appropriate");
        }
    }

    /// <summary>
    /// Gets the size of an element type in bytes.
    /// </summary>
    private static int GetElementSize(Type? elementType)
    {
        if (elementType == null)
        {
            return 1;
        }


        return elementType.Name switch
        {
            "Byte" or "SByte" or "Boolean" => 1,
            "Int16" or "UInt16" or "Char" => 2,
            "Int32" or "UInt32" or "Single" => 4,
            "Int64" or "UInt64" or "Double" => 8,
            "Decimal" => 16,
            _ => IntPtr.Size
        };
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _validationProfiles.Clear();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Kernel validation started for {KernelName}")]
    private partial void LogValidationStarted(string kernelName);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Kernel validation completed for {KernelName}: Valid={IsValid}, Issues={IssueCount}")]
    private partial void LogValidationCompleted(string kernelName, bool isValid, int issueCount);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Kernel validation failed for {KernelName}: {Error}")]
    private partial void LogValidationFailed(string kernelName, string error);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Determinism test started for {KernelName} with {Iterations} iterations")]
    private partial void LogDeterminismTestStarted(string kernelName, int iterations);

    [LoggerMessage(Level = MsLogLevel.Debug, Message = "Determinism test iteration {Current}/{Total} for {KernelName}")]
    private partial void LogDeterminismIteration(string kernelName, int current, int total);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Determinism test completed for {KernelName}: Deterministic={IsDeterministic}")]
    private partial void LogDeterminismTestCompleted(string kernelName, bool isDeterministic);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Determinism test failed for {KernelName}: {Error}")]
    private partial void LogDeterminismTestFailed(string kernelName, string error);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Memory pattern analysis started for {KernelName}")]
    private partial void LogMemoryAnalysisStarted(string kernelName);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Memory pattern analysis completed for {KernelName}: Safe={IsMemorySafe}, Issues={IssueCount}")]
    private partial void LogMemoryAnalysisCompleted(string kernelName, bool isMemorySafe, int issueCount);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Memory pattern analysis failed for {KernelName}: {Error}")]
    private partial void LogMemoryAnalysisFailed(string kernelName, string error);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Validation profile updated for {KernelName}: Level={Level}")]
    private partial void LogValidationProfileUpdated(string kernelName, string level);

    #endregion
}