// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Debugging.Analytics;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Core orchestrator for kernel debugging operations, coordinating all debugging components.
/// Implements the IKernelDebugService interface and delegates to specialized components.
/// </summary>
internal sealed class CoreKernelDebugOrchestrator : IKernelDebugService, IDisposable
{
    private readonly ILogger<CoreKernelDebugOrchestrator> _logger;
    private readonly IAccelerator? _primaryAccelerator;
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators;
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory;

    // Specialized components
    private readonly KernelDebugValidator _validator;
    private readonly KernelDebugProfiler _profiler;
    private readonly Analytics.KernelDebugAnalyzer _analyzer;
    private readonly KernelDebugReporter _reporter;

    private DebugServiceOptions _options;
    private bool _disposed;

    public CoreKernelDebugOrchestrator(
        ILogger<CoreKernelDebugOrchestrator> logger,
        IAccelerator? primaryAccelerator = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _primaryAccelerator = primaryAccelerator;
        _accelerators = new ConcurrentDictionary<string, IAccelerator>();
        _executionHistory = new ConcurrentQueue<KernelExecutionResult>();
        _options = new DebugServiceOptions();

        // Initialize specialized components
        var profilerLogger = logger.CreateLogger<KernelDebugProfiler>();
        _profiler = new KernelDebugProfiler(profilerLogger, _executionHistory);

        var validatorLogger = logger.CreateLogger<KernelDebugValidator>();
        _validator = new KernelDebugValidator(validatorLogger, _accelerators, _profiler);

        var analyzerLogger = logger.CreateLogger<Analytics.KernelDebugAnalyzer>();
        _analyzer = new Analytics.KernelDebugAnalyzer(analyzerLogger, _accelerators, _profiler);

        var reporterLogger = logger.CreateLogger<KernelDebugReporter>();
        _reporter = new KernelDebugReporter(reporterLogger);

        // Add primary accelerator if provided
        if (_primaryAccelerator != null)
        {
            _accelerators.TryAdd(_primaryAccelerator.GetType().Name, _primaryAccelerator);
        }

        _logger.LogInformation("KernelDebugOrchestrator initialized with {componentCount} specialized components", 4);
    }

    public void Configure(DebugServiceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Configure all components
        _validator.Configure(options);
        _profiler.Configure(options);
        _analyzer.Configure(options);
        _reporter.Configure(options);

        _logger.LogInformation("Debug service configured with options");
    }

    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Starting kernel validation orchestration for {kernelName}", kernelName);

        try
        {
            return await _validator.ValidateKernelAsync(kernelName, inputs, tolerance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in kernel validation orchestration");
            throw;
        }
    }

    public async Task<KernelExecutionResult> ExecuteOnBackendAsync(
        string kernelName,
        string backendType,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Orchestrating backend execution: {kernelName} on {backendType}", kernelName, backendType);

        try
        {
            return await _validator.ExecuteOnBackendAsync(kernelName, backendType, inputs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in backend execution orchestration");
            throw;
        }
    }

    public async Task<ResultComparisonReport> CompareResultsAsync(
        KernelExecutionResult result1,
        KernelExecutionResult result2,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Orchestrating result comparison between {backend1} and {backend2}",
            result1.BackendType, result2.BackendType);

        try
        {
            return await _reporter.CompareResultsAsync(result1, result2, tolerance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in result comparison orchestration");
            throw;
        }
    }

    // Interface-compliant method for comparing multiple results
    public async Task<ResultComparisonReport> CompareResultsAsync(
        IEnumerable<KernelExecutionResult> results,
        ComparisonStrategy comparisonStrategy = ComparisonStrategy.Tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Orchestrating multi-result comparison with strategy {strategy}", comparisonStrategy);

        try
        {
            return await _reporter.CompareResultsAsync(results, comparisonStrategy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in multi-result comparison orchestration");
            throw;
        }
    }

    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        string backendType,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Orchestrating execution trace for {kernelName} on {backendType}", kernelName, backendType);

        try
        {
            return await _profiler.TraceKernelExecutionAsync(kernelName, backendType, inputs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in execution trace orchestration");
            throw;
        }
    }

    // Interface-compliant method for tracing with trace points
    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        object[] inputs,
        string[] tracePoints)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Orchestrating execution trace for {kernelName} with {tracePointCount} trace points",
            kernelName, tracePoints.Length);

        try
        {
            return await _profiler.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in trace execution orchestration");
            throw;
        }
    }

    public async Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        int iterations = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Orchestrating determinism validation for {kernelName} with {iterations} iterations",
            kernelName, iterations);

        try
        {
            return await _analyzer.ValidateDeterminismAsync(kernelName, inputs, iterations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in determinism validation orchestration");
            throw;
        }
    }

    public async Task<MemoryAnalysisReport> AnalyzeMemoryPatternsAsync(
        string kernelName,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Orchestrating memory analysis for {kernelName}", kernelName);

        try
        {
            return await _analyzer.AnalyzeMemoryPatternsAsync(kernelName, inputs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in memory analysis orchestration");
            throw;
        }
    }

    public async Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await KernelDebugReporter.GetAvailableBackendsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available backends");
            throw;
        }
    }

    // Additional orchestration methods not in the original interface

    /// <summary>
    /// Generates a comprehensive debug report for a kernel validation.
    /// </summary>
    public async Task<string> GenerateDetailedReportAsync(KernelValidationResult validationResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Generating detailed report for kernel {kernelName}", validationResult.KernelName);

        try
        {
            return await _reporter.GenerateDetailedReportAsync(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating detailed report");
            throw;
        }
    }

    /// <summary>
    /// Exports a debug report in the specified format.
    /// </summary>
    public async Task<string> ExportReportAsync(object report, ReportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Exporting report in {format} format", format);

        try
        {
            return await _reporter.ExportReportAsync(report, format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report");
            throw;
        }
    }

    /// <summary>
    /// Generates a performance report for a specific kernel.
    /// </summary>
    public async Task<PerformanceReport> GeneratePerformanceReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Generating performance report for kernel {kernelName}", kernelName);

        try
        {
            return await _profiler.GeneratePerformanceReportAsync(kernelName, timeWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
            throw;
        }
    }

    /// <summary>
    /// Analyzes resource utilization for a kernel.
    /// </summary>
    public async Task<ResourceUtilizationReport> AnalyzeResourceUtilizationAsync(
        string kernelName,
        object[] inputs,
        TimeSpan? analysisWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Analyzing resource utilization for kernel {kernelName}", kernelName);

        try
        {
            return await _analyzer.AnalyzeResourceUtilizationAsync(kernelName, inputs, analysisWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing resource utilization");
            throw;
        }
    }

    /// <summary>
    /// Runs a comprehensive debug suite combining validation, profiling, and analysis.
    /// </summary>
    public async Task<ComprehensiveDebugReport> RunComprehensiveDebugAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f,
        int determinismIterations = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Running comprehensive debug suite for kernel {kernelName}", kernelName);

        try
        {
            var report = new ComprehensiveDebugReport
            {
                KernelName = kernelName,
                StartTime = DateTimeOffset.UtcNow
            };

            // Run validation
            report.ValidationResult = await ValidateKernelAsync(kernelName, inputs, tolerance);

            // Run determinism check
            report.DeterminismReport = await ValidateDeterminismAsync(kernelName, inputs, determinismIterations);

            // Run memory analysis
            report.MemoryAnalysis = await AnalyzeMemoryPatternsAsync(kernelName, inputs);

            // Generate performance report
            report.PerformanceReport = await GeneratePerformanceReportAsync(kernelName, TimeSpan.FromMinutes(5));

            // Analyze resource utilization
            report.ResourceUtilization = await AnalyzeResourceUtilizationAsync(kernelName, inputs, TimeSpan.FromMinutes(5));

            report.EndTime = DateTimeOffset.UtcNow;
            report.TotalDuration = report.EndTime - report.StartTime;

            _logger.LogInformation("Comprehensive debug suite completed for {kernelName} in {duration}ms",
                kernelName, report.TotalDuration.TotalMilliseconds);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in comprehensive debug suite");
            throw;
        }
    }

    /// <summary>
    /// Adds an accelerator for debugging operations.
    /// </summary>
    public void AddAccelerator(string name, IAccelerator accelerator)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(accelerator);

        _accelerators.TryAdd(name, accelerator);
        _logger.LogDebug("Added accelerator {name} for debugging", name);
    }

    /// <summary>
    /// Removes an accelerator from debugging operations.
    /// </summary>
    public bool RemoveAccelerator(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var removed = _accelerators.TryRemove(name, out _);
        if (removed)
        {
            _logger.LogDebug("Removed accelerator {name} from debugging", name);
        }

        return removed;
    }

    /// <summary>
    /// Gets the current debug service statistics.
    /// </summary>
    public DebugServiceStatistics GetStatistics()
    {
        return new DebugServiceStatistics
        {
            TotalExecutions = _executionHistory.Count,
            RegisteredAccelerators = _accelerators.Count,
            MaxHistorySize = _options.MaxExecutionHistorySize,
            OptionsConfigured = _options != null
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _validator?.Dispose();
            _profiler?.Dispose();
            _analyzer?.Dispose();
            _reporter?.Dispose();

            foreach (var accelerator in _accelerators.Values)
            {
                try
                {
                    (accelerator as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing accelerator");
                }
            }

            _accelerators.Clear();
            _disposed = true;

            _logger.LogInformation("KernelDebugOrchestrator disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during KernelDebugOrchestrator disposal");
        }
    }
}

// Supporting classes for comprehensive debugging
public class ComprehensiveDebugReport
{
    public required string KernelName { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public KernelValidationResult? ValidationResult { get; set; }
    public DeterminismReport? DeterminismReport { get; set; }
    public MemoryAnalysisReport? MemoryAnalysis { get; set; }
    public PerformanceReport? PerformanceReport { get; set; }
    public ResourceUtilizationReport? ResourceUtilization { get; set; }
}

public class DebugServiceStatistics
{
    public int TotalExecutions { get; set; }
    public int RegisteredAccelerators { get; set; }
    public int MaxHistorySize { get; set; }
    public bool OptionsConfigured { get; set; }
}