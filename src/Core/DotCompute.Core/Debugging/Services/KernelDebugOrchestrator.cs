// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Debugging.Analytics;
using DotCompute.Core.Debugging.Core;
using DotCompute.Core.Debugging.Infrastructure;
using KernelDebugProfiler = DotCompute.Core.Debugging.KernelDebugProfiler;
using Microsoft.Extensions.Logging;

// Using aliases to resolve type conflicts
using CoreKernelValidator = DotCompute.Core.Debugging.Core.KernelValidator;
using CoreKernelProfiler = DotCompute.Core.Debugging.Core.KernelProfiler;

namespace DotCompute.Core.Debugging.Services;

/// <summary>
/// High-level orchestrator for kernel debugging operations.
/// Coordinates validation, profiling, analysis, and logging for comprehensive kernel debugging.
/// </summary>
public sealed class KernelDebugOrchestrator : IKernelDebugService, IDisposable
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

    public KernelDebugOrchestrator(
        ILogger<KernelDebugOrchestrator> logger,
        IAccelerator? primaryAccelerator = null,
        DebugServiceOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        _logger.LogInformation("Kernel debug orchestrator initialized with {AcceleratorCount} accelerators", _accelerators.Count);
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

        _logger.LogInformation("Starting comprehensive kernel validation for {KernelName}", kernelName);

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

            _logger.LogInformation("Completed kernel validation for {KernelName}: {IsValid}", kernelName, enhancedResult.IsValid);
            return enhancedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during kernel validation orchestration for {KernelName}", kernelName);
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

        _logger.LogDebug("Executing kernel {KernelName} on {BackendType}", kernelName, backendType);

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
            _logger.LogError(ex, "Error during kernel execution orchestration for {KernelName} on {BackendType}", kernelName, backendType);
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
        ComparisonStrategy comparisonStrategy = ComparisonStrategy.Tolerance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        _logger.LogDebug("Comparing {ResultCount} kernel execution results", resultsList.Count);

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
            _logger.LogError(ex, "Error during result comparison orchestration");
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

        _logger.LogInformation("Starting kernel execution trace for {KernelName} with {TracePointCount} trace points", kernelName, tracePoints.Length);

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
            var enhancedTrace = await _analyzer.EnhanceExecutionTraceAsync(trace) ?? trace;

            // Log tracing completion
            await _debugLogger.LogTracingCompletionAsync(kernelName, enhancedTrace);

            _logger.LogInformation("Completed kernel execution trace for {KernelName}: {Success}", kernelName, enhancedTrace.Success);
            return enhancedTrace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during kernel execution trace orchestration for {KernelName}", kernelName);
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
    public async Task<DotCompute.Core.Debugging.Core.PerformanceAnalysisResult> AnalyzePerformanceAsync(
        string kernelName,
        TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        _logger.LogInformation("Starting performance analysis for kernel {KernelName}", kernelName);

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
            var result = new DotCompute.Core.Debugging.Core.PerformanceAnalysisResult
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

            _logger.LogInformation("Completed performance analysis for kernel {KernelName}", kernelName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance analysis orchestration for {KernelName}", kernelName);
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


        _logger.LogInformation("Starting determinism validation for kernel {KernelName} with {Iterations} iterations", kernelName, iterations);

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
                AllResults = new List<object>(), // Not available in core result
                MaxVariation = 0.0f, // Not available in core result
                NonDeterminismSource = determinismResult.NonDeterministicComponents.FirstOrDefault(),
                Recommendations = determinismResult.Recommendations
            };

            _logger.LogInformation("Completed determinism validation for kernel {KernelName}: {IsDeterministic}", kernelName, determinismResult.IsDeterministic);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during determinism validation orchestration for {KernelName}", kernelName);
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

        _accelerators.TryAdd(backendType, accelerator);
        _validator.RegisterAccelerator(backendType, accelerator);

        _logger.LogDebug("Registered accelerator for backend: {BackendType}", backendType);
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
            _validator.UnregisterAccelerator(backendType);
            _logger.LogDebug("Unregistered accelerator for backend: {BackendType}", backendType);
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
        return _accelerators.Keys.ToArray();
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

        _logger.LogDebug("Updated debug service options");
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

        _logger.LogInformation("Starting memory pattern analysis for kernel {KernelName}", kernelName);

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
                AccessPatterns = new List<DotCompute.Abstractions.Debugging.MemoryAccessPattern>(), // Not available in current MemoryPatternAnalysis
                Optimizations = new List<DotCompute.Abstractions.Debugging.PerformanceOptimization>(), // Not available in current MemoryPatternAnalysis
                Warnings = analysis.LeakProbability > 0.5 ? new List<string> { "Potential memory leak detected" } : new List<string>()
            };

            _logger.LogInformation("Completed memory pattern analysis for kernel {KernelName}", kernelName);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory pattern analysis for {KernelName}", kernelName);
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

        var backendInfos = new List<BackendInfo>();

        foreach (var (backendType, accelerator) in _accelerators)
        {
            var backendInfo = new BackendInfo
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

        _logger.LogDebug("Retrieved information for {BackendCount} backends", backendInfos.Count);
        return backendInfos;
    }

    /// <summary>
    /// Configures debugging options such as verbosity level, output formats, etc.
    /// </summary>
    /// <param name="options">Debugging configuration options.</param>
    public void Configure(DebugServiceOptions options)
    {
        UpdateOptions(options); // Delegate to existing implementation
    }

    /// <summary>
    /// Gets capabilities for a specific backend accelerator.
    /// </summary>
    private static string[] GetBackendCapabilities(IAccelerator accelerator)
    {
        var capabilities = new List<string> { "Kernel Execution" };

        // Add more capabilities based on accelerator type
        if (accelerator.GetType().Name.Contains("Cuda"))
        {
            capabilities.AddRange(["GPU Compute", "CUDA Kernels", "Unified Memory"]);
        }
        else if (accelerator.GetType().Name.Contains("Cpu"))
        {
            capabilities.AddRange(["Multi-threading", "SIMD", "Vector Operations"]);
        }

        return capabilities.ToArray();
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
    {
        // Default implementation - could be enhanced with accelerator-specific queries
        return Environment.WorkingSet; // Fallback to current working set
    }

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
                _logger.LogDebug("Performance insights generated for kernel {KernelName}", validationResult.KernelName);
            }

            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enhance validation result for {KernelName}", validationResult.KernelName);
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
    private static DotCompute.Abstractions.Debugging.PerformanceAnalysisResult ConvertToAbstractionsPerformanceAnalysisResult(DotCompute.Core.Debugging.Core.PerformanceAnalysisResult coreResult)
    {
        return new DotCompute.Abstractions.Debugging.PerformanceAnalysisResult
        {
            KernelName = coreResult.KernelName,
            BackendType = "Unknown", // Not available in core result
            ExecutionTime = coreResult.ExecutionStatistics?.AverageExecutionTime ?? TimeSpan.Zero,
            MemoryUsage = 0, // Could be extracted from MemoryAnalysis if needed
            ThroughputOpsPerSecond = 0.0, // ThroughputOpsPerSecond not available in current PerformanceReport
            Bottlenecks = coreResult.BottleneckAnalysis?.Bottlenecks?.Select(b => b.Description).ToList() ?? new List<string>()
        };
    }

    /// <summary>
    /// Converts Core DeterminismAnalysisResult to Abstractions DeterminismAnalysisResult.
    /// </summary>
    private static DotCompute.Abstractions.Debugging.DeterminismAnalysisResult ConvertToAbstractionsDeterminismAnalysisResult(DotCompute.Core.Debugging.Analytics.DeterminismAnalysisResult coreResult)
    {
        return new DotCompute.Abstractions.Debugging.DeterminismAnalysisResult
        {
            KernelName = coreResult.KernelName,
            IsDeterministic = coreResult.IsDeterministic,
            ExecutionCount = coreResult.RunCount, // Map RunCount to ExecutionCount
            RunCount = coreResult.RunCount,
            MaxVariation = 0.0f, // Not available in core result
            VariabilityScore = coreResult.VariabilityScore,
            NonDeterministicComponents = coreResult.NonDeterministicComponents,
            NonDeterminismSource = coreResult.NonDeterministicComponents.FirstOrDefault(),
            Recommendations = coreResult.Recommendations,
            AllResults = new List<object>(), // Not available in core result
            StatisticalAnalysis = new Dictionary<string, object>()
        };
    }

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

                _logger.LogDebug("Kernel debug orchestrator disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during kernel debug orchestrator disposal");
            }
        }
    }
}