// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

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

// KernelValidationResult moved to DotCompute.Abstractions.Validation namespace

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
    public Dictionary<string, object> Metadata { get; init; } = [];
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
    public List<ResultDifference> Differences { get; init; } = [];
    public ComparisonStrategy Strategy { get; init; }
    public float Tolerance { get; init; }
    public Dictionary<string, PerformanceMetrics> PerformanceComparison { get; init; } = [];
}

/// <summary>
/// Trace of kernel execution showing intermediate values.
/// </summary>
public class KernelExecutionTrace
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public List<TracePoint> TracePoints { get; init; } = [];
    public TimeSpan TotalExecutionTime { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Execution result if successful.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Memory profiling information.
    /// </summary>
    public MemoryProfile? MemoryProfile { get; init; }

    /// <summary>
    /// Performance metrics for this execution.
    /// </summary>
    public PerformanceMetrics? PerformanceMetrics { get; init; }
}


/// <summary>
/// Analysis of kernel memory access patterns.
/// </summary>
public class MemoryAnalysisReport
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public List<MemoryAccessPattern> AccessPatterns { get; init; } = [];
    public List<PerformanceOptimization> Optimizations { get; init; } = [];
    public long TotalMemoryAccessed { get; init; }
    public float MemoryEfficiency { get; init; }
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Allocation efficiency score (0-1).
    /// </summary>
    public double AllocationEfficiency { get; init; }

    /// <summary>
    /// Probability of memory leaks (0-1).
    /// </summary>
    public double LeakProbability { get; init; }

    /// <summary>
    /// Cache efficiency score (0-100).
    /// </summary>
    public double CacheEfficiency { get; init; }
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
    public Dictionary<string, object> Properties { get; init; } = [];
    public string? UnavailabilityReason { get; init; }
    public int Priority { get; init; }

    /// <summary>Gets the backend type (e.g., "CPU", "CUDA", "Metal").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the maximum memory available on this backend in bytes.</summary>
    public long MaxMemory { get; init; }
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

    /// <summary>
    /// Enables detailed execution tracing.
    /// </summary>
    public bool EnableDetailedTracing { get; set; } = false;

    /// <summary>
    /// Enables memory profiling during execution.
    /// </summary>
    public bool EnableMemoryProfiling { get; set; } = true;

    /// <summary>
    /// Enables performance analysis and reporting.
    /// </summary>
    public bool EnablePerformanceAnalysis { get; set; } = true;

    /// <summary>
    /// Maximum number of historical entries to keep.
    /// </summary>
    public int MaxHistoricalEntries { get; set; } = 1000;

    /// <summary>
    /// Maximum number of metric points to collect.
    /// </summary>
    public int MaxMetricPoints { get; set; } = 10000;

    /// <summary>
    /// Threshold for considering an array as large (in elements).
    /// </summary>
    public int LargeArrayThreshold { get; set; } = 1000000;

    /// <summary>
    /// Memory efficiency threshold (percentage 0-100).
    /// </summary>
    public double MemoryEfficiencyThreshold { get; set; } = 85.0;

    /// <summary>
    /// Threshold for long-running kernels (in milliseconds).
    /// </summary>
    public int LongRunningThreshold { get; set; } = 5000;

    /// <summary>
    /// Memory pressure threshold (percentage 0-100).
    /// </summary>
    public double MemoryPressureThreshold { get; set; } = 90.0;

    /// <summary>
    /// Development profile configuration.
    /// </summary>
    public static DebugServiceOptions Development => new()
    {
        VerbosityLevel = LogLevel.Debug,
        EnableProfiling = true,
        EnableMemoryAnalysis = true,
        EnableDetailedTracing = true,
        SaveExecutionLogs = true,
        MaxHistoricalEntries = 5000,
        MaxMetricPoints = 50000
    };
}

/// <summary>
/// Represents a validation issue found during kernel testing.
/// </summary>
public class DebugValidationIssue
{
    public ValidationSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public string BackendAffected { get; init; } = string.Empty;
    public string? Suggestion { get; init; }
    public Dictionary<string, object> Details { get; init; } = [];
    public string? Context { get; init; }

    /// <summary>
    /// Creates a new debug validation issue.
    /// </summary>
    public DebugValidationIssue(ValidationSeverity severity, string message, string backendAffected = "", string? suggestion = null)
    {
        Severity = severity;
        Message = message;
        BackendAffected = backendAffected;
        Suggestion = suggestion;
    }

    /// <summary>
    /// Parameterless constructor for object initialization.
    /// </summary>
    public DebugValidationIssue() { }
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

    /// <summary>
    /// First backend involved in the comparison.
    /// </summary>
    public string Backend1 { get; init; } = string.Empty;

    /// <summary>
    /// Second backend involved in the comparison.
    /// </summary>
    public string Backend2 { get; init; } = string.Empty;
}

/// <summary>
/// A specific point in kernel execution trace.
/// </summary>
public class TracePoint
{
    public string Name { get; init; } = string.Empty;
    public int ExecutionOrder { get; init; }
    public Dictionary<string, object> Values { get; init; } = [];
    public TimeSpan TimestampFromStart { get; init; }

    /// <summary>
    /// Timestamp when this trace point was captured.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Memory usage at this trace point in bytes.
    /// </summary>
    public long MemoryUsage { get; init; }

    /// <summary>
    /// Additional data for this trace point.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];
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

    // Additional properties for compatibility with Performance namespace
    public long ExecutionTimeMs => (long)ExecutionTime.TotalMilliseconds;
    public long MemoryAllocatedBytes => MemoryUsage;
    public long ThroughputOpsPerSec => ThroughputOpsPerSecond;
    public double EfficiencyScore => CpuUtilization * 100; // Simple efficiency metric
}

/// <summary>
/// Memory access pattern information.
/// </summary>
public class MemoryAccessPattern
{
    public string PatternType { get; init; } = string.Empty;
    public long AccessCount { get; init; }
    public float CoalescingEfficiency { get; init; }
    public List<string> Issues { get; init; } = [];
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

// ValidationSeverity moved to DotCompute.Abstractions.Validation.ValidationSeverity
// Use: using DotCompute.Abstractions.Validation;

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

/// <summary>
/// Memory profiling information.
/// </summary>
public class MemoryProfile
{
    /// <summary>
    /// Peak memory usage during execution in bytes.
    /// </summary>
    public long PeakMemory { get; init; }

    /// <summary>
    /// Memory allocations during execution.
    /// </summary>
    public long Allocations { get; init; }

    /// <summary>
    /// Memory efficiency score (0-1).
    /// </summary>
    public float Efficiency { get; init; }
}