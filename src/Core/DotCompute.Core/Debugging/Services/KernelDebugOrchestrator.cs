// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Core.Debugging.Analytics;
using DotCompute.Core.Debugging.Infrastructure;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Kernels;

// Using aliases to resolve type conflicts
using CoreKernelValidator = DotCompute.Core.Debugging.Core.KernelValidator;
using AbstractionsComparisonStrategy = DotCompute.Abstractions.Debugging.ComparisonStrategy;
using AbstractionsExecutionStatistics = DotCompute.Abstractions.Debugging.ExecutionStatistics;
using KernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Debugging.Services;

/// <summary>
/// High-level orchestrator for kernel debugging operations.
/// Coordinates validation, profiling, analysis, and logging for comprehensive kernel debugging.
/// </summary>
public sealed partial class KernelDebugOrchestrator : IKernelDebugService, IDisposable
{
    private readonly ILogger<KernelDebugOrchestrator> _logger;
    private readonly IAccelerator? _primaryAccelerator;
    private readonly DebugServiceOptions _options;

    // Core components
    private readonly CoreKernelValidator _validator;
    private readonly KernelDebugProfiler _profiler;
    private readonly KernelDebugAnalyzer _analyzer;
    private readonly KernelDebugLogger _debugLogger;

    // State tracking
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators;
    private bool _disposed;

    #region LoggerMessage Delegates

    private static readonly Action<ILogger, int, Exception?> LogOrchestratorInitialized =
        LoggerMessage.Define<int>(
            MsLogLevel.Information,
            new EventId(11001, nameof(LogOrchestratorInitialized)),
            "Kernel debug orchestrator initialized with {AcceleratorCount} accelerators");

    private static readonly Action<ILogger, string, Exception?> LogValidationStarting =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11002, nameof(LogValidationStarting)),
            "Starting comprehensive kernel validation for {KernelName}");

    private static readonly Action<ILogger, string, bool, Exception?> LogValidationCompleted =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Information,
            new EventId(11003, nameof(LogValidationCompleted)),
            "Completed kernel validation for {KernelName}: {IsValid}");

    private static readonly Action<ILogger, string, Exception?> LogValidationOrchestrationError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11004, nameof(LogValidationOrchestrationError)),
            "Error during kernel validation orchestration for {KernelName}");

    private static readonly Action<ILogger, string, string, Exception?> LogExecutingKernel =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Debug,
            new EventId(11005, nameof(LogExecutingKernel)),
            "Executing kernel {KernelName} on {BackendType}");

    private static readonly Action<ILogger, string, string, Exception?> LogExecutionOrchestrationError =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Error,
            new EventId(11006, nameof(LogExecutionOrchestrationError)),
            "Error during kernel execution orchestration for {KernelName} on {BackendType}");

    private static readonly Action<ILogger, int, Exception?> LogComparingResults =
        LoggerMessage.Define<int>(
            MsLogLevel.Debug,
            new EventId(11007, nameof(LogComparingResults)),
            "Comparing {ResultCount} kernel execution results");

    private static readonly Action<ILogger, Exception?> LogComparisonOrchestrationError =
        LoggerMessage.Define(
            MsLogLevel.Error,
            new EventId(11008, nameof(LogComparisonOrchestrationError)),
            "Error during result comparison orchestration");

    private static readonly Action<ILogger, string, int, Exception?> LogTracingStarting =
        LoggerMessage.Define<string, int>(
            MsLogLevel.Information,
            new EventId(11009, nameof(LogTracingStarting)),
            "Starting kernel execution trace for {KernelName} with {TracePointCount} trace points");

    private static readonly Action<ILogger, string, bool, Exception?> LogTracingCompleted =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Information,
            new EventId(11010, nameof(LogTracingCompleted)),
            "Completed kernel execution trace for {KernelName}: {Success}");

    private static readonly Action<ILogger, string, Exception?> LogTracingOrchestrationError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11011, nameof(LogTracingOrchestrationError)),
            "Error during kernel execution trace orchestration for {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogPerformanceAnalysisStarting =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11012, nameof(LogPerformanceAnalysisStarting)),
            "Starting performance analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogPerformanceAnalysisCompleted =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11013, nameof(LogPerformanceAnalysisCompleted)),
            "Completed performance analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogPerformanceAnalysisOrchestrationError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11014, nameof(LogPerformanceAnalysisOrchestrationError)),
            "Error during performance analysis orchestration for {KernelName}");

    private static readonly Action<ILogger, string, int, Exception?> LogDeterminismValidationStarting =
        LoggerMessage.Define<string, int>(
            MsLogLevel.Information,
            new EventId(11015, nameof(LogDeterminismValidationStarting)),
            "Starting determinism validation for kernel {KernelName} with {Iterations} iterations");

    private static readonly Action<ILogger, string, bool, Exception?> LogDeterminismValidationCompleted =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Information,
            new EventId(11016, nameof(LogDeterminismValidationCompleted)),
            "Completed determinism validation for kernel {KernelName}: {IsDeterministic}");

    private static readonly Action<ILogger, string, Exception?> LogDeterminismValidationOrchestrationError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11017, nameof(LogDeterminismValidationOrchestrationError)),
            "Error during determinism validation orchestration for {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogAcceleratorRegistered =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(11018, nameof(LogAcceleratorRegistered)),
            "Registered accelerator for backend: {BackendType}");

    private static readonly Action<ILogger, string, Exception?> LogAcceleratorUnregistered =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(11019, nameof(LogAcceleratorUnregistered)),
            "Unregistered accelerator for backend: {BackendType}");

    private static readonly Action<ILogger, Exception?> LogOptionsUpdated =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(11020, nameof(LogOptionsUpdated)),
            "Updated debug service options");

    private static readonly Action<ILogger, string, Exception?> LogMemoryPatternAnalysisStarting =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11021, nameof(LogMemoryPatternAnalysisStarting)),
            "Starting memory pattern analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogMemoryPatternAnalysisCompleted =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11022, nameof(LogMemoryPatternAnalysisCompleted)),
            "Completed memory pattern analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogMemoryPatternAnalysisError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11023, nameof(LogMemoryPatternAnalysisError)),
            "Error during memory pattern analysis for {KernelName}");

    private static readonly Action<ILogger, int, Exception?> LogBackendInfoRetrieved =
        LoggerMessage.Define<int>(
            MsLogLevel.Debug,
            new EventId(11024, nameof(LogBackendInfoRetrieved)),
            "Retrieved information for {BackendCount} backends");

    private static readonly Action<ILogger, string, Exception?> LogComprehensiveDebugStarting =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11025, nameof(LogComprehensiveDebugStarting)),
            "Starting comprehensive debug analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogComprehensiveDebugCompleted =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11026, nameof(LogComprehensiveDebugCompleted)),
            "Completed comprehensive debug analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogComprehensiveDebugError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11027, nameof(LogComprehensiveDebugError)),
            "Error during comprehensive debug analysis for {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogResourceUtilizationAnalysisStarting =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11028, nameof(LogResourceUtilizationAnalysisStarting)),
            "Starting resource utilization analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogResourceUtilizationAnalysisCompleted =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(11029, nameof(LogResourceUtilizationAnalysisCompleted)),
            "Completed resource utilization analysis for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogResourceUtilizationAnalysisError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11030, nameof(LogResourceUtilizationAnalysisError)),
            "Error during resource utilization analysis for {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogPerformanceInsightsGenerated =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(11031, nameof(LogPerformanceInsightsGenerated)),
            "Performance insights generated for kernel {KernelName}");

    private static readonly Action<ILogger, string, Exception?> LogEnhancementFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(11032, nameof(LogEnhancementFailed)),
            "Failed to enhance validation result for {KernelName}");

    private static readonly Action<ILogger, Exception?> LogOrchestratorDisposed =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(11033, nameof(LogOrchestratorDisposed)),
            "Kernel debug orchestrator disposed");

    private static readonly Action<ILogger, Exception?> LogDisposalError =
        LoggerMessage.Define(
            MsLogLevel.Error,
            new EventId(11034, nameof(LogDisposalError)),
            "Error during kernel debug orchestrator disposal");

    #endregion
    /// <summary>
    /// Initializes a new instance of the KernelDebugOrchestrator class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="primaryAccelerator">The primary accelerator.</param>
    /// <param name="options">The options.</param>

    public KernelDebugOrchestrator(
        ILogger<KernelDebugOrchestrator> logger,
        IAccelerator? primaryAccelerator = null,
        DebugServiceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _primaryAccelerator = primaryAccelerator;
        _options = options ?? new DebugServiceOptions();
        _accelerators = new ConcurrentDictionary<string, IAccelerator>();

        // Initialize components
        var validatorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CoreKernelValidator>.Instance;
        _validator = new CoreKernelValidator(validatorLogger, _options);

        var profilerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<KernelDebugProfiler>.Instance;
        var executionHistory = new ConcurrentQueue<KernelExecutionResult>();
        _profiler = new KernelDebugProfiler(profilerLogger, executionHistory);
        _profiler.Configure(_options);

        var analyzerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<KernelDebugAnalyzer>.Instance;
        var accelerators = new ConcurrentDictionary<string, IAccelerator>();
        _analyzer = new KernelDebugAnalyzer(analyzerLogger, accelerators, _profiler);

        var debugLoggerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<KernelDebugLogger>.Instance;
        _debugLogger = new KernelDebugLogger(debugLoggerLogger, _options);

        // Register primary accelerator if available
        if (_primaryAccelerator != null)
        {
            RegisterAccelerator(_primaryAccelerator.Type.ToString(), _primaryAccelerator);
        }

        LogOrchestratorInitialized(_logger, _accelerators.Count, null);
    }

    /// <summary>
    /// Validates a kernel across multiple backends with comprehensive analysis.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to validate.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="tolerance">Tolerance for numerical comparison.</param>
    /// <returns>Comprehensive validation result.</returns>
    public async Task<KernelValidationResult> ValidateKernelAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        LogValidationStarting(_logger, kernelName, null);

        try
        {
            // Log validation start
            await _debugLogger.LogValidationStartAsync(kernelName, inputs, tolerance);

            // Perform core validation
            var validationResult = await _validator.ValidateKernelAsync(kernelName, inputs, tolerance);

            // Enhance with additional analysis
            var enhancedResult = await EnhanceValidationResultAsync(validationResult, inputs);

            // Log validation completion
            await _debugLogger.LogValidationCompletionAsync(kernelName, enhancedResult);

            LogValidationCompleted(_logger, kernelName, enhancedResult.IsValid, null);
            return enhancedResult;
        }
        catch (Exception ex)
        {
            LogValidationOrchestrationError(_logger, kernelName, ex);
            await _debugLogger.LogValidationErrorAsync(kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a kernel on a specific backend with comprehensive monitoring.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute.</param>
    /// <param name="backendType">Type of backend to execute on.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <returns>Detailed execution result.</returns>
    public async Task<KernelExecutionResult> ExecuteOnBackendAsync(
        string kernelName,
        string backendType,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentException.ThrowIfNullOrEmpty(backendType);
        ArgumentNullException.ThrowIfNull(inputs);

        LogExecutingKernel(_logger, kernelName, backendType, null);

        try
        {
            // Log execution start
            await _debugLogger.LogExecutionStartAsync(kernelName, backendType, inputs);

            // Execute through validator (which has the execution logic)
            var result = await _validator.ExecuteOnBackendAsync(kernelName, backendType, inputs);

            // Log execution completion
            await _debugLogger.LogExecutionCompletionAsync(kernelName, backendType, result);

            return result;
        }
        catch (Exception ex)
        {
            LogExecutionOrchestrationError(_logger, kernelName, backendType, ex);
            await _debugLogger.LogExecutionErrorAsync(kernelName, backendType, ex);
            throw;
        }
    }

    /// <summary>
    /// Compares results from multiple kernel executions with detailed analysis.
    /// </summary>
    /// <param name="results">Collection of execution results to compare.</param>
    /// <param name="comparisonStrategy">Strategy to use for comparison.</param>
    /// <returns>Comprehensive comparison report.</returns>
    public async Task<ResultComparisonReport> CompareResultsAsync(
        IEnumerable<KernelExecutionResult> results,
        AbstractionsComparisonStrategy comparisonStrategy = AbstractionsComparisonStrategy.Tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        LogComparingResults(_logger, resultsList.Count, null);

        try
        {
            // Perform core comparison
            var comparisonReport = await _validator.CompareResultsAsync(resultsList, comparisonStrategy);

            // Enhance with statistical analysis
            var enhancedReport = await _analyzer.EnhanceComparisonReportAsync(comparisonReport, resultsList);

            // Log comparison completion
            await _debugLogger.LogComparisonCompletionAsync(comparisonReport);

            return enhancedReport;
        }
        catch (Exception ex)
        {
            LogComparisonOrchestrationError(_logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Traces kernel execution with comprehensive profiling and analysis.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to trace.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="tracePoints">Specific points to trace during execution.</param>
    /// <returns>Detailed execution trace with analysis.</returns>
    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        object[] inputs,
        string[] tracePoints)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(tracePoints);

        LogTracingStarting(_logger, kernelName, tracePoints.Length, null);

        try
        {
            // Select optimal backend for tracing
            var (accelerator, backendType) = await SelectOptimalBackendForTracingAsync();

            if (accelerator == null)
            {
                return new KernelExecutionTrace
                {
                    KernelName = kernelName,
                    BackendType = "None",
                    Success = false,
                    ErrorMessage = "No suitable backend available for tracing"
                };
            }

            // Log tracing start
            await _debugLogger.LogTracingStartAsync(kernelName, backendType, tracePoints);

            // Perform execution tracing
            var trace = await _profiler.TraceKernelExecutionAsync(kernelName, backendType, inputs);

            // Enhance with additional analysis
            var enhancedTrace = await _analyzer.EnhanceExecutionTraceAsync(trace);

            // Log tracing completion
            await _debugLogger.LogTracingCompletionAsync(kernelName, enhancedTrace);

            LogTracingCompleted(_logger, kernelName, enhancedTrace.Success, null);
            return enhancedTrace;
        }
        catch (Exception ex)
        {
            LogTracingOrchestrationError(_logger, kernelName, ex);
            await _debugLogger.LogTracingErrorAsync(kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Analyzes kernel performance patterns and generates optimization recommendations.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <param name="timeWindow">Time window for historical analysis.</param>
    /// <returns>Comprehensive performance analysis.</returns>
    public async Task<PerformanceAnalysisResult> AnalyzePerformanceAsync(
        string kernelName,
        TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        LogPerformanceAnalysisStarting(_logger, kernelName, null);

        try
        {
            // Generate performance report
            var performanceReport = await _profiler.GeneratePerformanceReportAsync(kernelName, timeWindow);

            // Analyze memory usage
            var memoryAnalysis = await _profiler.AnalyzeMemoryUsageAsync(kernelName, TimeSpan.FromHours(1));

            // Detect bottlenecks
            var bottleneckAnalysis = await _profiler.DetectBottlenecksAsync(kernelName);

            // Get execution statistics
            var executionStats = _profiler.GetExecutionStatistics(kernelName);

            // Perform advanced analysis
            var advancedAnalysis = await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
                kernelName, performanceReport, memoryAnalysis, bottleneckAnalysis);

            // Combine all analyses
            var result = new PerformanceAnalysisResult
            {
                KernelName = kernelName,
                PerformanceReport = performanceReport,
                MemoryAnalysis = memoryAnalysis,
                BottleneckAnalysis = bottleneckAnalysis,
                ExecutionStatistics = executionStats,
                AdvancedAnalysis = advancedAnalysis,
                GeneratedAt = DateTime.UtcNow
            };

            // Log analysis completion (convert to Abstractions type)
            await _debugLogger.LogPerformanceAnalysisAsync(kernelName, ConvertToAbstractionsPerformanceAnalysisResult(result));

            LogPerformanceAnalysisCompleted(_logger, kernelName, null);
            return result;
        }
        catch (Exception ex)
        {
            LogPerformanceAnalysisOrchestrationError(_logger, kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Validates determinism of kernel execution across multiple runs.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to test.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="iterations">Number of iterations to perform.</param>
    /// <returns>Determinism analysis result.</returns>
    public async Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        int iterations = 10)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        if (iterations < 2)
        {

            throw new ArgumentException("Iteration count must be at least 2", nameof(iterations));
        }


        LogDeterminismValidationStarting(_logger, kernelName, iterations, null);

        try
        {
            // Perform determinism analysis
            var determinismResult = await _analyzer.ValidateDeterminismAsync(kernelName, inputs, iterations);

            // Log analysis completion (convert to Abstractions type)
            await _debugLogger.LogDeterminismAnalysisAsync(kernelName, ConvertToAbstractionsDeterminismAnalysisResult(determinismResult));

            // Convert to DeterminismReport
            var report = new DeterminismReport
            {
                KernelName = determinismResult.KernelName,
                IsDeterministic = determinismResult.IsDeterministic,
                ExecutionCount = determinismResult.RunCount,
                AllResults = [], // Not available in core result
                MaxVariation = 0.0f, // Not available in core result
                NonDeterminismSource = determinismResult.NonDeterministicComponents.Count > 0 ? determinismResult.NonDeterministicComponents[0] : null,
                Recommendations = determinismResult.Recommendations
            };

            LogDeterminismValidationCompleted(_logger, kernelName, determinismResult.IsDeterministic, null);
            return report;
        }
        catch (Exception ex)
        {
            LogDeterminismValidationOrchestrationError(_logger, kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Registers an accelerator for use in debugging operations.
    /// </summary>
    /// <param name="backendType">Type of the backend.</param>
    /// <param name="accelerator">Accelerator instance.</param>
    public void RegisterAccelerator(string backendType, IAccelerator accelerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(backendType);
        ArgumentNullException.ThrowIfNull(accelerator);

        _ = _accelerators.TryAdd(backendType, accelerator);
        _validator.RegisterAccelerator(backendType, accelerator);

        LogAcceleratorRegistered(_logger, backendType, null);
    }

    /// <summary>
    /// Unregisters an accelerator.
    /// </summary>
    /// <param name="backendType">Type of the backend to unregister.</param>
    /// <returns>True if successfully unregistered; otherwise, false.</returns>
    public bool UnregisterAccelerator(string backendType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(backendType);

        var removed = _accelerators.TryRemove(backendType, out _);
        if (removed)
        {
            _ = _validator.UnregisterAccelerator(backendType);
            LogAcceleratorUnregistered(_logger, backendType, null);
        }

        return removed;
    }

    /// <summary>
    /// Gets the list of available backends for debugging.
    /// </summary>
    /// <returns>Array of available backend types.</returns>
    public string[] GetAvailableBackends()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return [.. _accelerators.Keys];
    }

    /// <summary>
    /// Updates debug service options.
    /// </summary>
    /// <param name="options">New debug service options.</param>
    public void UpdateOptions(DebugServiceOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        // Update options across components
        _options.VerbosityLevel = options.VerbosityLevel;
        _options.EnableDetailedTracing = options.EnableDetailedTracing;
        _options.EnableMemoryProfiling = options.EnableMemoryProfiling;
        _options.EnablePerformanceAnalysis = options.EnablePerformanceAnalysis;

        LogOptionsUpdated(_logger, null);
    }

    /// <summary>
    /// Analyzes memory access patterns and identifies potential performance issues.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <returns>Memory analysis report with optimization suggestions.</returns>
    public async Task<MemoryAnalysisReport> AnalyzeMemoryPatternsAsync(string kernelName, object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        LogMemoryPatternAnalysisStarting(_logger, kernelName, null);

        try
        {
            // Perform memory analysis through the analyzer
            var analysis = await _analyzer.AnalyzeMemoryPatternsAsync(kernelName, inputs);

            // Convert to the interface expected format
            var report = new MemoryAnalysisReport
            {
                KernelName = kernelName,
                BackendType = "Multiple", // Analyzer determines backend
                TotalMemoryAccessed = 0, // Not available in current MemoryPatternAnalysis
                MemoryEfficiency = (float)analysis.AllocationEfficiency,
                AccessPatterns = [], // Not available in current MemoryPatternAnalysis
                Optimizations = [], // Not available in current MemoryPatternAnalysis
                Warnings = analysis.LeakProbability > 0.5 ? ["Potential memory leak detected"] : []
            };

            LogMemoryPatternAnalysisCompleted(_logger, kernelName, null);
            return report;
        }
        catch (Exception ex)
        {
            LogMemoryPatternAnalysisError(_logger, kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information about available backends and their capabilities.
    /// </summary>
    /// <returns>Information about all available backends and their current status.</returns>
    public async Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.CompletedTask; // Make async for interface compliance

        var backendInfos = new List<DotCompute.Abstractions.Debugging.Types.BackendInfo>();

        foreach (var (backendType, accelerator) in _accelerators)
        {
            var backendInfo = new DotCompute.Abstractions.Debugging.Types.BackendInfo
            {
                Name = backendType,
                Type = backendType,
                IsAvailable = true,
                Version = "1.0.0", // Could get from accelerator if available
                Capabilities = GetBackendCapabilities(accelerator),
                Priority = GetBackendPriority(backendType),
                MaxMemory = GetBackendMaxMemory(accelerator),
                Properties = GetBackendProperties(accelerator)
            };

            backendInfos.Add(backendInfo);
        }

        LogBackendInfoRetrieved(_logger, backendInfos.Count, null);
        return backendInfos;
    }

    /// <summary>
    /// Configures debugging options such as verbosity level, output formats, etc.
    /// </summary>
    /// <param name="options">Debugging configuration options.</param>
    public void Configure(DebugServiceOptions options) => UpdateOptions(options); // Delegate to existing implementation

    /// <summary>
    /// Runs a comprehensive debug analysis combining all debugging types.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="tolerance">Tolerance for numerical comparison.</param>
    /// <param name="determinismIterations">Number of iterations for determinism testing.</param>
    /// <returns>Comprehensive debug report with all analyses.</returns>
    public async Task<ComprehensiveDebugReport> RunComprehensiveDebugAsync(
        string kernelName,
        object[] inputs,
        float tolerance = 1e-6f,
        int determinismIterations = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        LogComprehensiveDebugStarting(_logger, kernelName, null);

        var report = new ComprehensiveDebugReport
        {
            KernelName = kernelName,
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            // Run all analyses
            report.ValidationResult = await ValidateKernelAsync(kernelName, inputs, tolerance);
            report.PerformanceAnalysis = await _profiler.GeneratePerformanceReportAsync(kernelName);
            report.DeterminismAnalysis = await ValidateDeterminismAsync(kernelName, inputs, determinismIterations);
            report.MemoryAnalysis = await AnalyzeMemoryPatternsAsync(kernelName, inputs);

            // Generate executive summary
            var criticalIssues = new List<string>();
            var warnings = new List<string>();
            var recommendations = new List<string>();

            if (report.ValidationResult?.Issues != null)
            {
                foreach (var issue in report.ValidationResult.Issues)
                {
                    if (issue.Severity == ValidationSeverity.Error)
                    {
                        criticalIssues.Add(issue.Message);
                    }
                    else if (issue.Severity == ValidationSeverity.Warning)
                    {
                        warnings.Add(issue.Message);
                    }
                }
            }

            if (!report.DeterminismAnalysis?.IsDeterministic == true)
            {
                criticalIssues.Add("Kernel execution is non-deterministic");
                if (report.DeterminismAnalysis?.Recommendations != null)
                {
                    recommendations.AddRange(report.DeterminismAnalysis.Recommendations);
                }
            }

            report.CriticalIssues.Clear();
            foreach (var issue in criticalIssues)
            {
                report.CriticalIssues.Add(issue);
            }

            report.Warnings.Clear();
            foreach (var warning in warnings)
            {
                report.Warnings.Add(warning);
            }

            report.Recommendations.Clear();
            foreach (var rec in recommendations)
            {
                report.Recommendations.Add(rec);
            }

            // Calculate overall health score
            var healthScore = 100.0;
            healthScore -= criticalIssues.Count * 20.0;
            healthScore -= warnings.Count * 5.0;
            report.OverallHealthScore = Math.Max(0, healthScore);

            report.IsProductionReady = criticalIssues.Count == 0 && healthScore >= 80;
            report.OverallSeverity = criticalIssues.Count > 0 ? ValidationSeverity.Error :
                                    warnings.Count > 0 ? ValidationSeverity.Warning :
                                    ValidationSeverity.Info;

            // Generate executive summary
            report.ExecutiveSummary = $"Kernel '{kernelName}' analysis: " +
                $"{criticalIssues.Count} critical issue(s), {warnings.Count} warning(s). " +
                $"Health score: {report.OverallHealthScore:F1}/100. " +
                $"Production ready: {(report.IsProductionReady ? "Yes" : "No")}";

            LogComprehensiveDebugCompleted(_logger, kernelName, null);
            return report;
        }
        catch (Exception ex)
        {
            LogComprehensiveDebugError(_logger, kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Generates a detailed textual report from validation results.
    /// </summary>
    /// <param name="validationResult">Validation result to generate report from.</param>
    /// <returns>Detailed textual report.</returns>
    public async Task<string> GenerateDetailedReportAsync(KernelValidationResult validationResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(validationResult);

        // Create a temporary reporter instance for this operation
        var reporterLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<KernelDebugReporter>.Instance;
        using var reporter = new KernelDebugReporter(reporterLogger);
        reporter.Configure(_options);

        return await reporter.GenerateDetailedReportAsync(validationResult);
    }

    /// <summary>
    /// Exports a debug report in the specified format.
    /// </summary>
    /// <param name="report">Report object to export.</param>
    /// <param name="format">Target export format.</param>
    /// <returns>Exported report as string.</returns>
    public async Task<string> ExportReportAsync(object report, ReportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(report);

        // Create a temporary reporter instance for this operation
        var reporterLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<KernelDebugReporter>.Instance;
        using var reporter = new KernelDebugReporter(reporterLogger);
        reporter.Configure(_options);

        return await reporter.ExportReportAsync(report, format);
    }

    /// <summary>
    /// Generates a performance report for a specific kernel.
    /// </summary>
    /// <param name="kernelName">Name of the kernel.</param>
    /// <param name="timeWindow">Time window for analysis.</param>
    /// <returns>Performance report.</returns>
    public async Task<PerformanceReport> GeneratePerformanceReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        return await _profiler.GeneratePerformanceReportAsync(kernelName, timeWindow);
    }

    /// <summary>
    /// Analyzes resource utilization for a kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="analysisWindow">Time window for resource analysis.</param>
    /// <returns>Resource utilization report.</returns>
    public async Task<ResourceUtilizationReport> AnalyzeResourceUtilizationAsync(
        string kernelName,
        object[] inputs,
        TimeSpan? analysisWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        LogResourceUtilizationAnalysisStarting(_logger, kernelName, null);

        var window = analysisWindow ?? TimeSpan.FromMinutes(5);

        try
        {
            // Get performance and memory data
            var performanceReport = await _profiler.GeneratePerformanceReportAsync(kernelName, window);
            var memoryAnalysis = await _profiler.AnalyzeMemoryUsageAsync(kernelName, window);

            var report = new ResourceUtilizationReport
            {
                KernelName = kernelName,
                GeneratedAt = DateTime.UtcNow,
                AnalysisTimeWindow = window,
                SampleCount = performanceReport.ExecutionCount,
                // Populate CPU utilization
                CpuUtilization = new CpuUtilizationStats
                {
                    AverageCpuUtilization = 0, // Would need actual CPU monitoring
                    PeakCpuUtilization = 0,
                    MinCpuUtilization = 0,
                    AverageCpuTime = performanceReport.AverageExecutionTime,
                    CoresUtilized = Environment.ProcessorCount,
                    ParallelEfficiency = performanceReport.SuccessRate * 100.0,
                    AverageThreadCount = Environment.ProcessorCount
                },

                // Populate memory utilization
                MemoryUtilization = new MemoryUtilizationStats
                {
                    AverageMemoryUsage = memoryAnalysis.AverageMemoryUsage,
                    PeakMemoryUsage = memoryAnalysis.PeakMemoryUsage,
                    MinMemoryUsage = memoryAnalysis.MinimumMemoryUsage,
                    AverageMemoryBandwidth = 0, // Would need actual memory bandwidth monitoring
                    PeakMemoryBandwidth = 0,
                    AllocationEfficiency = 0.8, // Placeholder
                    CacheHitRate = 90.0, // Placeholder
                    AverageGCCollections = 0
                },

                // Calculate efficiency score
                OverallEfficiencyScore = performanceReport.SuccessRate * 100.0
            };

            // Add recommendations
            if (report.OverallEfficiencyScore < 80)
            {
                report.Recommendations.Add("Consider optimizing kernel for better resource utilization");
            }

            if (memoryAnalysis.PeakMemoryUsage > memoryAnalysis.AverageMemoryUsage * 2)
            {
                report.ResourceBottlenecks.Add("High memory usage variability detected");
                report.Recommendations.Add("Review memory allocation patterns for optimization opportunities");
            }

            LogResourceUtilizationAnalysisCompleted(_logger, kernelName, null);
            return report;
        }
        catch (Exception ex)
        {
            LogResourceUtilizationAnalysisError(_logger, kernelName, ex);
            throw;
        }
    }

    /// <summary>
    /// Adds an accelerator for debugging operations.
    /// </summary>
    /// <param name="name">Name/identifier for the accelerator.</param>
    /// <param name="accelerator">Accelerator instance to add.</param>
    public void AddAccelerator(string name, IAccelerator accelerator) => RegisterAccelerator(name, accelerator);

    /// <summary>
    /// Removes an accelerator from debugging operations.
    /// </summary>
    /// <param name="name">Name/identifier of the accelerator to remove.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public bool RemoveAccelerator(string name) => UnregisterAccelerator(name);

    /// <summary>
    /// Gets current debug service statistics.
    /// </summary>
    /// <returns>Debug service statistics.</returns>
    public DebugServiceStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stats = new DebugServiceStatistics
        {
            RegisteredAccelerators = _accelerators.Count,
            ServiceStartTime = DateTime.UtcNow - TimeSpan.FromMinutes(10), // Placeholder - would need actual service start tracking
            ActiveProfilingSessions = 0, // Would need actual session tracking
            CacheHitRate = 0.85, // Placeholder
            AverageMemoryUsage = GC.GetTotalMemory(false),
            OperationsPerSecond = 0 // Would need actual operation tracking
        };

        // Populate per-backend statistics
        foreach (var (backendType, _) in _accelerators)
        {
            var executionStats = _profiler.GetExecutionStatistics($"*_{backendType}"); // Would need proper kernel name tracking

            stats.BackendStatistics[backendType] = new BackendDebugStats
            {
                BackendType = backendType,
                TotalExecutions = executionStats.TotalExecutions,
                SuccessfulExecutions = executionStats.SuccessfulExecutions,
                FailedExecutions = executionStats.FailedExecutions,
                SuccessRate = executionStats.TotalExecutions > 0
                    ? (double)executionStats.SuccessfulExecutions / executionStats.TotalExecutions
                    : 0,
                AverageExecutionTime = TimeSpan.FromMilliseconds(executionStats.AverageExecutionTimeMs),
                IsAvailable = true,
                LastExecutionTime = executionStats.LastExecutionTime
            };
        }

        return stats;
    }

    /// <summary>
    /// Gets capabilities for a specific backend accelerator.
    /// </summary>
    private static string[] GetBackendCapabilities(IAccelerator accelerator)
    {
        var capabilities = new List<string> { "Kernel Execution" };

        // Add more capabilities based on accelerator type
        if (accelerator.GetType().Name.Contains("Cuda", StringComparison.Ordinal))
        {
            capabilities.AddRange(["GPU Compute", "CUDA Kernels", "Unified Memory"]);
        }
        else if (accelerator.GetType().Name.Contains("Cpu", StringComparison.Ordinal))
        {
            capabilities.AddRange(["Multi-threading", "SIMD", "Vector Operations"]);
        }

        return [.. capabilities];
    }

    /// <summary>
    /// Gets priority for a backend type.
    /// </summary>
    private static int GetBackendPriority(string backendType)
    {
        return backendType.ToUpperInvariant() switch
        {
            "CUDA" => 1,
            "CPU" => 2,
            "METAL" => 3,
            _ => 10
        };
    }

    /// <summary>
    /// Gets maximum memory for a backend accelerator.
    /// </summary>
    private static long GetBackendMaxMemory(IAccelerator accelerator)
        // Default implementation - could be enhanced with accelerator-specific queries

        => Environment.WorkingSet; // Fallback to current working set

    /// <summary>
    /// Gets properties for a backend accelerator.
    /// </summary>
    private static Dictionary<string, object> GetBackendProperties(IAccelerator accelerator)
    {
        return new Dictionary<string, object>
        {
            ["Type"] = accelerator.GetType().Name,
            ["Assembly"] = accelerator.GetType().Assembly.GetName().Name ?? "Unknown"
        };
    }

    /// <summary>
    /// Enhances validation result with additional analysis.
    /// </summary>
    private async Task<KernelValidationResult> EnhanceValidationResultAsync(
        KernelValidationResult validationResult,
        object[] inputs)
    {
        try
        {
            // Perform additional analysis if enabled
            if (_options.EnablePerformanceAnalysis)
            {
                var performanceInsights = await _analyzer.AnalyzeValidationPerformanceAsync(validationResult, inputs);
                // Performance insights logged but not modifying validation result
                // since KernelValidationResult is a class, not a record
                LogPerformanceInsightsGenerated(_logger, validationResult.KernelName, null);
            }

            return validationResult;
        }
        catch (Exception ex)
        {
            LogEnhancementFailed(_logger, validationResult.KernelName, ex);
            return validationResult;
        }
    }

    /// <summary>
    /// Selects the optimal backend for tracing operations.
    /// </summary>
    private async Task<(IAccelerator?, string)> SelectOptimalBackendForTracingAsync()
    {
        await Task.CompletedTask; // Make async for consistency

        // Prefer GPU backends for comprehensive tracing, fallback to CPU
        var preferredBackends = new[] { "CUDA", "METAL", "OPENCL", "CPU" };

        foreach (var backend in preferredBackends)
        {
            if (_accelerators.TryGetValue(backend, out var accelerator))
            {
                return (accelerator, backend);
            }
        }

        // Fallback to primary accelerator
        if (_primaryAccelerator != null)
        {
            return (_primaryAccelerator, _primaryAccelerator.Type.ToString());
        }

        // No suitable backend found
        return (null, "None");
    }

    /// <summary>
    /// Converts Core PerformanceAnalysisResult to Abstractions PerformanceAnalysisResult.
    /// </summary>
    private static AbstractionsMemory.Debugging.PerformanceAnalysisResult ConvertToAbstractionsPerformanceAnalysisResult(PerformanceAnalysisResult coreResult)
    {
        return new AbstractionsMemory.Debugging.PerformanceAnalysisResult
        {
            KernelName = coreResult.KernelName,
            BackendType = "Unknown", // Not available in core result
            ExecutionTime = TimeSpan.FromMilliseconds(coreResult.ExecutionStatistics.AverageExecutionTimeMs),
            MemoryUsage = 0, // Could be extracted from MemoryAnalysis if needed
            ThroughputOpsPerSecond = 0.0, // ThroughputOpsPerSecond not available in current PerformanceReport
            Bottlenecks = [.. coreResult.BottleneckAnalysis.Bottlenecks.Select(b => b.Description)]
        };
    }

    /// <summary>
    /// Converts Core DeterminismAnalysisResult to Abstractions DeterminismAnalysisResult.
    /// </summary>
    private static AbstractionsMemory.Debugging.DeterminismAnalysisResult ConvertToAbstractionsDeterminismAnalysisResult(Analytics.DeterminismAnalysisResult coreResult)
    {
        return new AbstractionsMemory.Debugging.DeterminismAnalysisResult
        {
            KernelName = coreResult.KernelName,
            IsDeterministic = coreResult.IsDeterministic,
            ExecutionCount = coreResult.RunCount, // Map RunCount to ExecutionCount
            RunCount = coreResult.RunCount,
            MaxVariation = 0.0f, // Not available in core result
            VariabilityScore = coreResult.VariabilityScore,
            NonDeterministicComponents = [.. coreResult.NonDeterministicComponents],
            NonDeterminismSource = coreResult.NonDeterministicComponents.Count > 0 ? coreResult.NonDeterministicComponents[0] : null,
            Recommendations = [.. coreResult.Recommendations],
            AllResults = [], // Not available in core result
            StatisticalAnalysis = []
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

            try
            {
                _validator?.Dispose();
                _profiler?.Dispose();
                _analyzer?.Dispose();
                _debugLogger?.Dispose();

                _accelerators.Clear();

                LogOrchestratorDisposed(_logger, null);
            }
            catch (Exception ex)
            {
                LogDisposalError(_logger, ex);
            }
        }
    }
}

/// <summary>
/// Comprehensive performance analysis result.
/// </summary>
public record PerformanceAnalysisResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the performance report.
    /// </summary>
    /// <value>The performance report.</value>
    public PerformanceReport PerformanceReport { get; init; } = new() { KernelName = string.Empty, AnalysisTimeWindow = TimeSpan.FromMinutes(5) };
    /// <summary>
    /// Gets or sets the memory analysis.
    /// </summary>
    /// <value>The memory analysis.</value>
    public MemoryUsageAnalysis MemoryAnalysis { get; init; } = new() { MinimumMemoryUsage = 0 };
    /// <summary>
    /// Gets or sets the bottleneck analysis.
    /// </summary>
    /// <value>The bottleneck analysis.</value>
    public Core.BottleneckAnalysis BottleneckAnalysis { get; init; } = new() { KernelName = string.Empty };
    /// <summary>
    /// Gets or sets the execution statistics.
    /// </summary>
    /// <value>The execution statistics.</value>
    public AbstractionsExecutionStatistics ExecutionStatistics { get; init; } = new();
    /// <summary>
    /// Gets or sets the advanced analysis.
    /// </summary>
    /// <value>The advanced analysis.</value>
    public AdvancedPerformanceAnalysis AdvancedAnalysis { get; init; } = new();
    /// <summary>
    /// Gets or sets the generated at.
    /// </summary>
    /// <value>The generated at.</value>
    public DateTime GeneratedAt { get; init; }
}