// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Debugging.Services;
using DotCompute.Core.Debugging.Types;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Production-grade kernel debugging service that validates kernels across multiple backends.
/// Provides comprehensive cross-validation, performance analysis, and diagnostic capabilities.
///
/// This service now uses a modular architecture with specialized components for different debugging operations:
/// - KernelDebugValidator: Cross-backend validation and verification
/// - KernelDebugProfiler: Performance profiling and execution tracing
/// - KernelDebugAnalyzer: Memory analysis and determinism checking
/// - KernelDebugReporter: Report generation and result comparison
/// - KernelDebugOrchestrator: Coordinates all debugging components
/// </summary>
public class KernelDebugService : IKernelDebugService, IDisposable
{
    private readonly KernelDebugOrchestrator _orchestrator;
    private readonly ILogger<KernelDebugService> _logger;
    private bool _disposed;

    public KernelDebugService(
        ILogger<KernelDebugService> logger,
        IAccelerator? primaryAccelerator = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create the orchestrator with specialized components
        // Use NullLoggerFactory to create the required logger type
        var loggerFactory = NullLoggerFactory.Instance;
        var orchestratorLogger = loggerFactory.CreateLogger<KernelDebugOrchestrator>();
        _orchestrator = new KernelDebugOrchestrator(orchestratorLogger, primaryAccelerator);

        _logger.LogInformation("KernelDebugService initialized with modular architecture");
    }

    /// <summary>
    /// Validates kernel execution across multiple backends for consistency.
    /// </summary>
    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.ValidateKernelAsync(kernelName, inputs, tolerance);
    }

    /// <summary>
    /// Executes a kernel on a specific backend with profiling.
    /// </summary>
    public async Task<KernelExecutionResult> ExecuteOnBackendAsync(
        string kernelName,
        string backendType,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.ExecuteOnBackendAsync(kernelName, backendType, inputs);
    }

    /// <summary>
    /// Compares results from multiple kernel executions.
    /// </summary>
    public async Task<ResultComparisonReport> CompareResultsAsync(
        IEnumerable<KernelExecutionResult> results,
        ComparisonStrategy comparisonStrategy = ComparisonStrategy.Tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.CompareResultsAsync(results, comparisonStrategy);
    }

    /// <summary>
    /// Traces kernel execution with detailed performance metrics.
    /// </summary>
    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        object[] inputs,
        string[] tracePoints)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);
    }

    /// <summary>
    /// Validates kernel determinism across multiple executions.
    /// </summary>
    public async Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        int iterations = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.ValidateDeterminismAsync(kernelName, inputs, iterations);
    }

    /// <summary>
    /// Analyzes memory access patterns and optimization opportunities.
    /// </summary>
    public async Task<MemoryAnalysisReport> AnalyzeMemoryPatternsAsync(
        string kernelName,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.AnalyzeMemoryPatternsAsync(kernelName, inputs);
    }

    /// <summary>
    /// Gets information about available debugging backends.
    /// </summary>
    public async Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.GetAvailableBackendsAsync();
    }

    /// <summary>
    /// Configures the debug service options.
    /// </summary>
    public void Configure(DebugServiceOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _orchestrator.Configure(options);
        _logger.LogDebug("Debug service options configured");
    }

    // Additional convenience methods that leverage the modular architecture

    // TODO: Implement comprehensive debug report
    // /// <summary>
    // /// Generates a comprehensive debug report combining all analysis types.
    // /// </summary>
    // public async Task<ComprehensiveDebugReport> RunComprehensiveDebugAsync(
    //     string kernelName,
    //     object[] inputs,
    //     float tolerance = 1e-6f,
    //     int determinismIterations = 5)
    // {
    //     ObjectDisposedException.ThrowIf(_disposed, this);
    //     return await _orchestrator.RunComprehensiveDebugAsync(kernelName, inputs, tolerance, determinismIterations);
    // }

    /// <summary>
    /// Generates a detailed textual report from validation results.
    /// </summary>
    public async Task<string> GenerateDetailedReportAsync(KernelValidationResult validationResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // TODO: Implement GenerateDetailedReportAsync in KernelDebugOrchestrator
        await Task.CompletedTask;
        return $"Debug Report for {validationResult.KernelName}:\n" +
               $"Status: {(validationResult.IsValid ? "VALID" : "INVALID")}\n" +
               $"Execution Time: {validationResult.ExecutionTime.TotalMilliseconds:F2}ms\n" +
               $"Backends Tested: {string.Join(", ", validationResult.BackendsTested)}\n" +
               $"Issues Found: {validationResult.Issues.Count}";
    }

    /// <summary>
    /// Exports a debug report in the specified format (JSON, XML, CSV, Text).
    /// </summary>
    public async Task<string> ExportReportAsync(object report, ReportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // TODO: Implement ExportReportAsync in KernelDebugOrchestrator
        await Task.CompletedTask;
        return format switch
        {
            ReportFormat.Json => JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            ReportFormat.PlainText => report.ToString() ?? "No data available",
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Generates a performance report for a specific kernel over a time window.
    /// </summary>
    public async Task<PerformanceReport> GeneratePerformanceReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // TODO: Implement GeneratePerformanceReportAsync in KernelDebugOrchestrator
        await Task.CompletedTask;
        return new PerformanceReport
        {
            KernelName = kernelName,
            TimeWindow = timeWindow ?? TimeSpan.FromHours(1),
            Summary = $"Performance report for {kernelName} - not yet implemented"
        };
    }

    // TODO: Implement resource utilization analysis
    // /// <summary>
    // /// Analyzes resource utilization (CPU, memory, GPU) for a kernel.
    // /// </summary>
    // public async Task<ResourceUtilizationReport> AnalyzeResourceUtilizationAsync(
    //     string kernelName,
    //     object[] inputs,
    //     TimeSpan? analysisWindow = null)
    // {
    //     ObjectDisposedException.ThrowIf(_disposed, this);
    //     return await _orchestrator.AnalyzeResourceUtilizationAsync(kernelName, inputs, analysisWindow);
    // }

    /// <summary>
    /// Adds an accelerator for use in debugging operations.
    /// </summary>
    public void AddAccelerator(string name, IAccelerator accelerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _orchestrator.RegisterAccelerator(name, accelerator);
    }

    /// <summary>
    /// Removes an accelerator from debugging operations.
    /// </summary>
    public bool RemoveAccelerator(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.UnregisterAccelerator(name);
    }

    // TODO: Implement debug service statistics
    // /// <summary>
    // /// Gets current debug service statistics.
    // /// </summary>
    // public DebugServiceStatistics GetStatistics()
    // {
    //     ObjectDisposedException.ThrowIf(_disposed, this);
    //     return _orchestrator.GetStatistics();
    // }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _orchestrator?.Dispose();
            _disposed = true;
            _logger.LogInformation("KernelDebugService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during KernelDebugService disposal");
        }
    }
}