// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Service for debugging kernels across different backends with cross-validation capabilities.
/// Enables comparison of results between backends to ensure correctness and identify issues.
/// </summary>
public interface IKernelDebugService
{
    /// <summary>
    /// Validates a kernel by executing it on multiple backends and comparing results.
    /// This is the primary debugging method for ensuring kernel correctness.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to validate</param>
    /// <param name="inputs">Input parameters for the kernel</param>
    /// <param name="tolerance">Numerical tolerance for floating-point comparisons (default: 1e-6f)</param>
    /// <returns>Validation result containing comparison data and potential issues</returns>
    public Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,

        object[] inputs,

        float tolerance = 1e-6f);

    /// <summary>
    /// Executes a kernel on a specific backend with detailed profiling information.
    /// Useful for performance analysis and identifying backend-specific issues.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute</param>
    /// <param name="backendType">Specific backend to use for execution</param>
    /// <param name="inputs">Input parameters for the kernel</param>
    /// <returns>Execution result with timing, memory usage, and output data</returns>
    public Task<KernelExecutionResult> ExecuteOnBackendAsync(
        string kernelName,

        string backendType,

        object[] inputs);

    /// <summary>
    /// Compares outputs from different backend executions to identify discrepancies.
    /// Supports various comparison strategies (exact, tolerance-based, statistical).
    /// </summary>
    /// <param name="results">Collection of execution results from different backends</param>
    /// <param name="comparisonStrategy">Strategy for comparing the results</param>
    /// <returns>Comparison result highlighting differences and similarities</returns>
    public Task<ResultComparisonReport> CompareResultsAsync(
        IEnumerable<KernelExecutionResult> results,
        ComparisonStrategy comparisonStrategy = ComparisonStrategy.Tolerance);

    /// <summary>
    /// Captures intermediate state during kernel execution for step-by-step debugging.
    /// Useful for complex kernels where you need to examine internal calculations.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to trace</param>
    /// <param name="inputs">Input parameters for the kernel</param>
    /// <param name="tracePoints">Specific points in the kernel to capture state</param>
    /// <returns>Execution trace with intermediate values</returns>
    public Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        object[] inputs,
        string[] tracePoints);

    /// <summary>
    /// Validates that a kernel produces deterministic results across multiple executions.
    /// Important for ensuring reproducibility in scientific computing applications.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to test</param>
    /// <param name="inputs">Input parameters for the kernel</param>
    /// <param name="iterations">Number of executions to perform (default: 10)</param>
    /// <returns>Determinism report showing consistency across executions</returns>
    public Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        int iterations = 10);

    /// <summary>
    /// Analyzes memory access patterns and identifies potential performance issues.
    /// Helps optimize kernels by detecting memory coalescing problems, bank conflicts, etc.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze</param>
    /// <param name="inputs">Input parameters for the kernel</param>
    /// <returns>Memory analysis report with optimization suggestions</returns>
    public Task<MemoryAnalysisReport> AnalyzeMemoryPatternsAsync(
        string kernelName,
        object[] inputs);

    /// <summary>
    /// Gets detailed information about available backends and their capabilities.
    /// Useful for understanding why certain backends might be selected or rejected.
    /// </summary>
    /// <returns>Information about all available backends and their current status</returns>
    public Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync();

    /// <summary>
    /// Configures debugging options such as verbosity level, output formats, etc.
    /// </summary>
    /// <param name="options">Debugging configuration options</param>
    public void Configure(DebugServiceOptions options);
}

/// <summary>
/// Result of validating a kernel across multiple backends.
/// </summary>
public class KernelValidationResult
{
    public string KernelName { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string[] BackendsTested { get; init; } = Array.Empty<string>();
    public Dictionary<string, object> Results { get; init; } = new();
    public List<ValidationIssue> Issues { get; init; } = new();
    public TimeSpan TotalValidationTime { get; init; }
    public float MaxDifference { get; init; }
    public string RecommendedBackend { get; init; } = string.Empty;
}

/// <summary>
/// Result of executing a kernel on a specific backend.
/// </summary>
public record KernelExecutionResult
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public object? Result { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryUsed { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Report comparing results from multiple backend executions.
/// </summary>
public class ResultComparisonReport
{
    public string KernelName { get; init; } = string.Empty;
    public bool ResultsMatch { get; init; }
    public string[] BackendsCompared { get; init; } = Array.Empty<string>();
    public List<ResultDifference> Differences { get; init; } = new();
    public ComparisonStrategy Strategy { get; init; }
    public float Tolerance { get; init; }
    public Dictionary<string, PerformanceMetrics> PerformanceComparison { get; init; } = new();
}

/// <summary>
/// Trace of kernel execution showing intermediate values.
/// </summary>
public class KernelExecutionTrace
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public List<TracePoint> TracePoints { get; init; } = new();
    public TimeSpan TotalExecutionTime { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Report on kernel determinism across multiple executions.
/// </summary>
public class DeterminismReport
{
    public string KernelName { get; init; } = string.Empty;
    public bool IsDeterministic { get; init; }
    public int ExecutionCount { get; init; }
    public List<object> AllResults { get; init; } = new();
    public float MaxVariation { get; init; }
    public string? NonDeterminismSource { get; init; }
    public List<string> Recommendations { get; init; } = new();
}

/// <summary>
/// Analysis of kernel memory access patterns.
/// </summary>
public class MemoryAnalysisReport
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public List<MemoryAccessPattern> AccessPatterns { get; init; } = new();
    public List<PerformanceOptimization> Optimizations { get; init; } = new();
    public long TotalMemoryAccessed { get; init; }
    public float MemoryEfficiency { get; init; }
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Information about an available backend.
/// </summary>
public class BackendInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string[] Capabilities { get; init; } = Array.Empty<string>();
    public Dictionary<string, object> Properties { get; init; } = new();
    public string? UnavailabilityReason { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// Configuration options for the debug service.
/// </summary>
public class DebugServiceOptions
{
    public LogLevel VerbosityLevel { get; set; } = LogLevel.Information;
    public bool EnableProfiling { get; set; } = true;
    public bool EnableMemoryAnalysis { get; set; } = true;
    public bool SaveExecutionLogs { get; set; } = false;
    public string LogOutputPath { get; set; } = "./debug-logs";
    public int MaxConcurrentExecutions { get; set; } = Environment.ProcessorCount;
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Represents a validation issue found during kernel testing.
/// </summary>
public class ValidationIssue
{
    public ValidationSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public string BackendAffected { get; init; } = string.Empty;
    public string? Suggestion { get; init; }
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Represents a difference between execution results.
/// </summary>
public class ResultDifference
{
    public string Location { get; init; } = string.Empty;
    public object ExpectedValue { get; init; } = new();
    public object ActualValue { get; init; } = new();
    public float Difference { get; init; }
    public string[] BackendsInvolved { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A specific point in kernel execution trace.
/// </summary>
public class TracePoint
{
    public string Name { get; init; } = string.Empty;
    public int ExecutionOrder { get; init; }
    public Dictionary<string, object> Values { get; init; } = new();
    public TimeSpan TimestampFromStart { get; init; }
}

/// <summary>
/// Performance metrics for a specific execution.
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryUsage { get; init; }
    public float CpuUtilization { get; init; }
    public float GpuUtilization { get; init; }
    public int ThroughputOpsPerSecond { get; init; }
}

/// <summary>
/// Memory access pattern information.
/// </summary>
public class MemoryAccessPattern
{
    public string PatternType { get; init; } = string.Empty;
    public long AccessCount { get; init; }
    public float CoalescingEfficiency { get; init; }
    public List<string> Issues { get; init; } = new();
}

/// <summary>
/// Performance optimization suggestion.
/// </summary>
public class PerformanceOptimization
{
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public float PotentialSpeedup { get; init; }
    public string Implementation { get; init; } = string.Empty;
}

/// <summary>
/// Strategies for comparing execution results.
/// </summary>
public enum ComparisonStrategy
{
    Exact,
    Tolerance,
    Statistical,
    Relative
}

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Logging levels for debug output.
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}