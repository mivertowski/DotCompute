// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Interfaces.Kernels;

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

