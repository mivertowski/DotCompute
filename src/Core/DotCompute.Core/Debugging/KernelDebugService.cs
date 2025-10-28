// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using AbstractionsComparisonStrategy = DotCompute.Abstractions.Debugging.ComparisonStrategy;
using KernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;

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
public sealed partial class KernelDebugService : IKernelDebugService, IDisposable
{
    private readonly Services.KernelDebugOrchestrator _orchestrator;
    private readonly ILogger<KernelDebugService> _logger;
    private bool _disposed;

    // LoggerMessage delegates (Event IDs: 11000-11999 for Debugging)
    [LoggerMessage(EventId = 11000, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "KernelDebugService initialized with modular architecture")]
    private static partial void LogServiceInitialized(ILogger logger);

    [LoggerMessage(EventId = 11001, Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Debug service options configured")]
    private static partial void LogOptionsConfigured(ILogger logger);

    [LoggerMessage(EventId = 11002, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "KernelDebugService disposed successfully")]
    private static partial void LogServiceDisposed(ILogger logger);

    [LoggerMessage(EventId = 11003, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Error during KernelDebugService disposal")]
    private static partial void LogDisposalError(ILogger logger, Exception exception);
    /// <summary>
    /// Initializes a new instance of the KernelDebugService class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="loggerFactory">The logger factory for creating component loggers.</param>
    /// <param name="primaryAccelerator">The primary accelerator.</param>

    public KernelDebugService(
        ILogger<KernelDebugService> logger,
        ILoggerFactory loggerFactory,
        IAccelerator? primaryAccelerator = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(loggerFactory);

        // Create the orchestrator with specialized components
        var orchestratorLogger = loggerFactory.CreateLogger<Services.KernelDebugOrchestrator>();
        _orchestrator = new Services.KernelDebugOrchestrator(orchestratorLogger, primaryAccelerator);

        LogServiceInitialized(_logger);
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
        AbstractionsComparisonStrategy comparisonStrategy = AbstractionsComparisonStrategy.Tolerance)
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
        LogOptionsConfigured(_logger);
    }

    // Additional convenience methods that leverage the modular architecture

    /// <summary>
    /// Generates a comprehensive debug report combining all analysis types.
    /// </summary>
    public async Task<ComprehensiveDebugReport> RunComprehensiveDebugAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f,
        int determinismIterations = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.RunComprehensiveDebugAsync(kernelName, inputs, tolerance, determinismIterations);
    }

    /// <summary>
    /// Generates a detailed textual report from validation results.
    /// </summary>
    public async Task<string> GenerateDetailedReportAsync(KernelValidationResult validationResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.GenerateDetailedReportAsync(validationResult);
    }

    /// <summary>
    /// Exports a debug report in the specified format (JSON, XML, CSV, Text).
    /// </summary>
    public async Task<string> ExportReportAsync(object report, ReportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.ExportReportAsync(report, format);
    }

    /// <summary>
    /// Generates a performance report for a specific kernel over a time window.
    /// </summary>
    public async Task<PerformanceReport> GeneratePerformanceReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.GeneratePerformanceReportAsync(kernelName, timeWindow);
    }

    /// <summary>
    /// Analyzes resource utilization (CPU, memory, GPU) for a kernel.
    /// </summary>
    public async Task<ResourceUtilizationReport> AnalyzeResourceUtilizationAsync(
        string kernelName,
        object[] inputs,
        TimeSpan? analysisWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _orchestrator.AnalyzeResourceUtilizationAsync(kernelName, inputs, analysisWindow);
    }

    /// <summary>
    /// Adds an accelerator for use in debugging operations.
    /// </summary>
    public void AddAccelerator(string name, IAccelerator accelerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _orchestrator.AddAccelerator(name, accelerator);
    }

    /// <summary>
    /// Removes an accelerator from debugging operations.
    /// </summary>
    public bool RemoveAccelerator(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.RemoveAccelerator(name);
    }

    /// <summary>
    /// Gets current debug service statistics.
    /// </summary>
    public DebugServiceStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _orchestrator.GetStatistics();
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
            LogServiceDisposed(_logger);
        }
        catch (Exception ex)
        {
            LogDisposalError(_logger, ex);
        }
    }
}