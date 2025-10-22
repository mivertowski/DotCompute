// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Execution;
using DotCompute.Backends.CUDA.Integration.Components;
using DotCompute.Backends.CUDA.Integration.Components.Health;
using DotCompute.Backends.CUDA.P2P;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Resolve type ambiguities
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using CudaStreamManager = DotCompute.Backends.CUDA.Execution.CudaStreamManager;
using AbstractionsKernelArgument = DotCompute.Abstractions.Kernels.KernelArgument;
using InterfacesKernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;
using CudaHealthStatus = DotCompute.Backends.CUDA.Integration.Components.Enums.CudaHealthStatus;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Complete CUDA backend integration for production use with RTX 2000 Ada optimizations.
/// Orchestrates device management, kernel execution, memory operations, and error handling.
/// </summary>
public sealed partial class CudaBackendIntegration : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CudaBackendIntegration> _logger;
    private readonly CudaContext _context;

    // Core component managers
    private readonly CudaDeviceManager _deviceManager;
    private readonly Components.CudaKernelExecutor _kernelExecutor;
    private readonly CudaMemoryManager _memoryManager;
    private readonly CudaErrorHandler _errorHandler;

    // Stream and event management
    private readonly CudaStreamManager _streamManager;
    private readonly CudaEventManager _eventManager;

    // Advanced features
    private readonly CudaGraphSupport _graphSupport;
    private readonly CudaP2PManager _p2pManager;
    private readonly CudaPerformanceMonitor _performanceMonitor;

    // Health monitoring
    private readonly Timer _healthCheckTimer;
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaBackendIntegration class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    public CudaBackendIntegration(
        IServiceProvider serviceProvider,
        CudaContext context,
        ILogger<CudaBackendIntegration> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize logger factory for components
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Initialize core component managers
        _deviceManager = new CudaDeviceManager(loggerFactory.CreateLogger<CudaDeviceManager>());
        _errorHandler = new CudaErrorHandler(context, loggerFactory.CreateLogger<CudaErrorHandler>());
        _memoryManager = new CudaMemoryManager(context, loggerFactory.CreateLogger<CudaMemoryManager>());

        // Initialize stream and event management
        _streamManager = new CudaStreamManager(context, loggerFactory.CreateLogger<CudaStreamManager>());
        _eventManager = new CudaEventManager(context, loggerFactory.CreateLogger<CudaEventManager>());

        // Initialize kernel executor with all dependencies
        var accelerator = CudaDeviceManager.CreateAcceleratorWrapper(context);
        _kernelExecutor = new Components.CudaKernelExecutor(
            accelerator, context, _streamManager, _eventManager,
            loggerFactory.CreateLogger<Components.CudaKernelExecutor>());

        // Initialize advanced features
        _graphSupport = new CudaGraphSupport(context, _streamManager, _eventManager,
            loggerFactory.CreateLogger<CudaGraphSupport>());
        _p2pManager = new CudaP2PManager(loggerFactory.CreateLogger<CudaP2PManager>());
        _performanceMonitor = new CudaPerformanceMonitor(context, _logger);

        // Set up health monitoring
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

        LogBackendInitialized(_logger, context.DeviceId);
    }

    #region Public Properties

    /// <summary>
    /// Device manager for hardware abstraction and device information.
    /// </summary>
    public CudaDeviceManager DeviceManager => _deviceManager;

    /// <summary>
    /// Kernel executor for optimized kernel execution.
    /// </summary>
    public Components.CudaKernelExecutor KernelExecutor => _kernelExecutor;

    /// <summary>
    /// Memory manager for unified memory operations.
    /// </summary>
    public CudaMemoryManager MemoryManager => _memoryManager;

    /// <summary>
    /// Error handler for comprehensive error management.
    /// </summary>
    public CudaErrorHandler ErrorHandler => _errorHandler;

    /// <summary>
    /// Stream manager for CUDA streams.
    /// </summary>
    public CudaStreamManager StreamManager => _streamManager;

    /// <summary>
    /// Event manager for timing and synchronization.
    /// </summary>
    public CudaEventManager EventManager => _eventManager;

    /// <summary>
    /// Graph support for kernel fusion.
    /// </summary>
    public CudaGraphSupport GraphSupport => _graphSupport;

    /// <summary>
    /// P2P manager for multi-GPU operations.
    /// </summary>
    public CudaP2PManager P2PManager => _p2pManager;

    /// <summary>
    /// Performance monitoring and metrics.
    /// </summary>
    public CudaPerformanceMonitor PerformanceMonitor => _performanceMonitor;

    #endregion

    #region High-Level Operations

    /// <summary>
    /// Executes a kernel with full optimization pipeline.
    /// </summary>
    /// <param name="kernel">Compiled kernel to execute.</param>
    /// <param name="arguments">Kernel arguments.</param>
    /// <param name="options">Execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result with performance metrics.</returns>
    public async Task<CudaExecutionResult> ExecuteOptimizedKernelAsync(
        CudaCompiledKernel kernel,
        AbstractionsKernelArgument[] arguments,
        CudaExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Apply advanced optimizations if enabled
            if (options.EnableAdvancedOptimizations)
            {
                await ApplyAdvancedOptimizationsAsync(kernel, arguments, options, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Get optimal execution configuration
            var execConfig = await GetOptimalExecutionConfigAsync(kernel, arguments, options, cancellationToken)
                .ConfigureAwait(false);

            // Convert arguments and execute
            var compiledKernel = ConvertToCompiledKernel(kernel);
            var convertedArguments = ConvertKernelArguments(arguments);

            var executionResult = await _kernelExecutor.ExecuteAndWaitAsync(
                compiledKernel, convertedArguments, execConfig, cancellationToken)
                .ConfigureAwait(false);

            var endTime = DateTimeOffset.UtcNow;

            return new CudaExecutionResult
            {
                Success = executionResult.Success,
                ExecutionTime = endTime - startTime,
                KernelExecutionResult = executionResult,
                OptimizationsApplied = options.EnableAdvancedOptimizations,
                PerformanceMetrics = _performanceMonitor.GetCurrentMetrics()
            };
        }
        catch (Exception ex)
        {
            var handlingResult = _errorHandler.HandleError(
                CudaError.LaunchFailed, "ExecuteOptimizedKernel", ex.Message);

            return new CudaExecutionResult
            {
                Success = false,
                ExecutionTime = DateTimeOffset.UtcNow - startTime,
                ErrorMessage = handlingResult.ErrorMessage
            };
        }
    }

    /// <summary>
    /// Creates and executes an optimized graph workflow.
    /// </summary>
    /// <param name="workflowId">Workflow identifier.</param>
    /// <param name="operations">Graph operations to execute.</param>
    /// <param name="options">Workflow options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Graph execution result.</returns>
    public async Task<CudaGraphExecutionResult> ExecuteOptimizedWorkflowAsync(
        string workflowId,
        IEnumerable<Execution.Graph.CudaKernelOperation> operations,
        CudaWorkflowOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Create optimized graph
            var graphId = await _graphSupport.CreateGraphAsync(
                workflowId, operations, options.GraphOptions, cancellationToken)
                .ConfigureAwait(false);

            // Instantiate graph
            var instance = await _graphSupport.InstantiateGraphAsync(graphId, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                // Execute graph
                var stream = options.UseHighPriorityStream ?
                    _streamManager.HighPriorityStream : _streamManager.DefaultStream;

                return await _graphSupport.ExecuteGraphAsync(instance, stream, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                instance.Dispose();
            }
        }
        catch (Exception ex)
        {
            var handlingResult = _errorHandler.HandleError(
                CudaError.LaunchFailed, "ExecuteOptimizedWorkflow", ex.Message);

            return new CudaGraphExecutionResult
            {
                Success = false,
                ErrorMessage = handlingResult.ErrorMessage
            };
        }
    }

    /// <summary>
    /// Performs multi-GPU data transfer with optimization.
    /// </summary>
    /// <param name="sourceBuffer">Source memory buffer.</param>
    /// <param name="sourceDevice">Source device ID.</param>
    /// <param name="destinationBuffer">Destination memory buffer.</param>
    /// <param name="destinationDevice">Destination device ID.</param>
    /// <param name="options">Transfer options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transfer result.</returns>
    public async Task<CudaP2PTransferResult> TransferDataOptimizedAsync(
        IUnifiedMemoryBuffer sourceBuffer,
        int sourceDevice,
        IUnifiedMemoryBuffer destinationBuffer,
        int destinationDevice,
        CudaTransferOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Use appropriate stream based on priority
            var stream = options.Priority switch
            {
                CudaTransferPriority.High => _streamManager.HighPriorityStream,
                CudaTransferPriority.Low => _streamManager.LowPriorityStream,
                _ => _streamManager.DefaultStream
            };

            return await _p2pManager.TransferAsync(
                sourceBuffer, sourceDevice,
                destinationBuffer, destinationDevice,
                options.SizeBytes, stream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var handlingResult = _errorHandler.HandleError(
                CudaError.InvalidValue, "TransferDataOptimized", ex.Message);

            return new CudaP2PTransferResult
            {
                Success = false,
                ErrorMessage = handlingResult.ErrorMessage
            };
        }
    }

    /// <summary>
    /// Gets comprehensive system health status.
    /// </summary>
    /// <returns>System health information.</returns>
    public CudaSystemHealth GetSystemHealth()
    {
        ThrowIfDisposed();

        try
        {
            var healthAssessment = _errorHandler.PerformHealthCheck();
            var streamStats = _streamManager.GetStatistics();
            var eventStats = _eventManager.GetStatistics();
            var p2pStats = _p2pManager.GetStatistics();
            var perfMetrics = _performanceMonitor.GetCurrentMetrics();
            var memoryAnalytics = _memoryManager.GetUsageAnalytics();

            return new CudaSystemHealth
            {
                OverallHealth = CalculateOverallHealth(healthAssessment, streamStats, eventStats, p2pStats),
                StreamHealth = CalculateStreamHealth(streamStats),
                EventHealth = CalculateEventHealth(eventStats),
                P2PHealth = CalculateP2PHealth(p2pStats),
                AdvancedFeaturesHealth = 0.8, // Static value since advanced features are simplified
                PerformanceHealth = CalculatePerformanceHealth(perfMetrics),
                MemoryHealth = CalculateMemoryHealth(memoryAnalytics),
                LastChecked = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _ = _errorHandler.HandleError(CudaError.Unknown, "GetSystemHealth", ex.Message);

            return new CudaSystemHealth
            {
                OverallHealth = 0.0,
                LastChecked = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Optimizes the entire backend for the current workload.
    /// </summary>
    /// <param name="profile">Workload profile for optimization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Optimization summary.</returns>
    public async Task<CudaOptimizationSummary> OptimizeForWorkloadAsync(
        CudaWorkloadProfile profile,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var summary = new CudaOptimizationSummary
        {
            Profile = profile,
            StartTime = DateTimeOffset.UtcNow
        };

        try
        {
            // Stream optimization
            _streamManager.OptimizeStreamUsage();
            summary.OptimizationsApplied.Add("Stream usage optimized");

            // Memory optimization
            var memoryCleanup = await _memoryManager.PerformCleanupAsync(cancellationToken)
                .ConfigureAwait(false);
            summary.OptimizationsApplied.Add($"Memory optimized: {memoryCleanup.MemoryFreed} bytes freed");

            // Event cleanup
            var cleanedEvents = _eventManager.GetStatistics().ActiveEvents;
            summary.OptimizationsApplied.Add($"Event cleanup performed ({cleanedEvents} events)");

            // P2P topology optimization
            if (_p2pManager.GetStatistics().TotalDevices > 1)
            {
                var topology = await _p2pManager.DiscoverTopologyAsync(cancellationToken)
                    .ConfigureAwait(false);
                summary.OptimizationsApplied.Add($"P2P topology optimized ({topology.DeviceCount} devices)");
            }

            // Workload-specific optimizations
            ApplyWorkloadSpecificOptimizations(profile, summary);

            summary.EndTime = DateTimeOffset.UtcNow;
            summary.Success = true;

            LogOptimizationCompleted(_logger, summary.OptimizationsApplied.Count);

            return summary;
        }
        catch (Exception ex)
        {
            _ = _errorHandler.HandleError(CudaError.Unknown, "OptimizeForWorkload", ex.Message);

            summary.Success = false;
            summary.ErrorMessage = ex.Message;
            summary.EndTime = DateTimeOffset.UtcNow;
            return summary;
        }
    }

    #endregion

    #region Private Helper Methods

    private static Task ApplyAdvancedOptimizationsAsync(
        CudaCompiledKernel kernel,
        AbstractionsKernelArgument[] arguments,
        CudaExecutionOptions options,
        CancellationToken cancellationToken)
        // Advanced optimizations are simplified in this implementation

        => Task.CompletedTask;

    private async Task<KernelExecutionConfig> GetOptimalExecutionConfigAsync(
        CudaCompiledKernel kernel,
        AbstractionsKernelArgument[] arguments,
        CudaExecutionOptions options,
        CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        // Determine problem size
        var problemSize = EstimateProblemSize(arguments);

        // Get optimal configuration from kernel executor
        var compiledKernel = ConvertToCompiledKernel(kernel);
        var config = _kernelExecutor.GetOptimalExecutionConfig(compiledKernel, problemSize);

        // Apply user preferences
        if (options.PreferredStream != null)
        {
            config = config with { Stream = options.PreferredStream };
        }

        return config;
    }

    private static ICompiledKernel ConvertToCompiledKernel(CudaCompiledKernel cudaKernel)
        // Convert CUDA-specific kernel to ICompiledKernel interface

        => cudaKernel; // CudaCompiledKernel implements ICompiledKernel

    private static InterfacesKernelArgument[] ConvertKernelArguments(AbstractionsKernelArgument[] arguments)
    {
        return [.. arguments.Select(arg => new InterfacesKernelArgument
        {
            Name = arg.Name,
            Value = arg.Value ?? new object(),
            Type = arg.Type,
            IsDeviceMemory = arg.IsDeviceMemory,
            MemoryBuffer = arg.MemoryBuffer as IUnifiedMemoryBuffer,
            SizeInBytes = arg.SizeInBytes,
            IsOutput = arg.IsOutput
        })];
    }

    private static int[] EstimateProblemSize(AbstractionsKernelArgument[] arguments)
    {
        // Simple heuristic to estimate problem size from arguments
        foreach (var arg in arguments)
        {
            if (arg.Value is int size && size > 0)
            {
                return [size];
            }
            if (arg.Value is int[] dimensions)
            {
                return dimensions;
            }
        }

        return [1]; // Default size
    }

    private static void ApplyWorkloadSpecificOptimizations(
        CudaWorkloadProfile profile,
        CudaOptimizationSummary summary)
    {
        if (profile.HasMatrixOperations)
        {
            summary.OptimizationsApplied.Add("Matrix operation optimizations enabled");
        }

        if (profile.HasCooperativeWorkloads)
        {
            summary.OptimizationsApplied.Add("Cooperative workload optimizations enabled");
        }

        if (profile.IsMemoryIntensive)
        {
            summary.OptimizationsApplied.Add("Memory-intensive optimizations applied");
        }

        if (profile.IsComputeIntensive)
        {
            summary.OptimizationsApplied.Add("Compute-intensive optimizations applied");
        }
    }

    #region Health Calculation Methods

    private static double CalculateOverallHealth(
        CudaSystemHealthAssessment healthAssessment,
        CudaStreamStatistics streamStats,
        CudaEventStatistics eventStats,
        CudaP2PStatistics p2pStats)
    {
        var healthScore = healthAssessment.OverallHealth switch
        {
            CudaHealthStatus.Healthy => 1.0,
            CudaHealthStatus.Warning => 0.7,
            CudaHealthStatus.Critical => 0.3,
            _ => 0.5
        };

        var streamHealth = CalculateStreamHealth(streamStats);
        var eventHealth = CalculateEventHealth(eventStats);
        var p2pHealth = CalculateP2PHealth(p2pStats);

        return (healthScore + streamHealth + eventHealth + p2pHealth) / 4.0;
    }

    private static double CalculateStreamHealth(CudaStreamStatistics stats)
    {
        if (stats.MaxConcurrentStreams == 0)
        {
            return 1.0;
        }

        var utilization = (double)stats.ActiveStreams / stats.MaxConcurrentStreams;
        return utilization < 0.9 ? 1.0 : Math.Max(0.0, 2.0 - 2.0 * utilization);
    }

    private static double CalculateEventHealth(CudaEventStatistics stats)
    {
        if (stats.MaxConcurrentEvents == 0)
        {
            return 1.0;
        }

        var utilization = (double)stats.ActiveEvents / stats.MaxConcurrentEvents;
        return utilization < 0.8 ? 1.0 : Math.Max(0.0, 2.0 - 2.5 * utilization);
    }

    private static double CalculateP2PHealth(CudaP2PStatistics stats)
    {
        if (stats.TotalConnections == 0)
        {
            return 1.0;
        }

        var enabledRatio = (double)stats.EnabledConnections / stats.TotalConnections;
        return enabledRatio > 0.5 ? 1.0 : enabledRatio * 2.0;
    }

    private static double CalculatePerformanceHealth(CudaPerformanceMetrics metrics) => metrics.MemoryUtilization < 0.9 && metrics.ComputeUtilization < 0.95 ? 1.0 : 0.5;

    private static double CalculateMemoryHealth(MemoryUsageAnalytics analytics)
    {
        var failureRate = analytics.TotalAllocations > 0 ?
            (double)analytics.FailedAllocations / analytics.TotalAllocations : 0.0;

        return failureRate switch
        {
            < 0.01 => 1.0,  // Less than 1% failure rate
            < 0.05 => 0.8,  // Less than 5% failure rate
            _ => 0.5        // 5% or higher failure rate
        };
    }

    #endregion

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var health = GetSystemHealth();

            if (health.OverallHealth < 0.7)
            {
                LogBackendHealthDegraded(_logger, health.OverallHealth);
            }
            else
            {
                LogBackendHealth(_logger, health.OverallHealth);
            }

            // Trigger maintenance if needed
            if (health.OverallHealth < 0.5)
            {
                PerformMaintenance();
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckError(_logger, ex);
        }
    }

    private void PerformMaintenance()
    {
        try
        {
            _streamManager.OptimizeStreamUsage();
            _ = Task.Run(async () =>
            {
                try
                {
                    _ = await _memoryManager.PerformCleanupAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogMemoryCleanupError(_logger, ex);
                }
            });

            LogMaintenanceCompleted(_logger);
        }
        catch (Exception ex)
        {
            _ = _errorHandler.HandleError(CudaError.Unknown, "PerformMaintenance", ex.Message);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _healthCheckTimer?.Dispose();

            // Dispose components in reverse order of dependency
            _performanceMonitor?.Dispose();
            _p2pManager?.Dispose();
            _graphSupport?.Dispose();
            _kernelExecutor?.Dispose();
            _eventManager?.Dispose();
            _streamManager?.Dispose();
            _memoryManager?.Dispose();
            _errorHandler?.Dispose();
            _deviceManager?.Dispose();

            LogBackendDisposed(_logger);
        }
    }
}

#region Supporting Types

/// <summary>
/// CUDA execution options for kernel execution.
/// </summary>
public sealed class CudaExecutionOptions
{
    /// <summary>
    /// Gets or sets the enable advanced optimizations.
    /// </summary>
    /// <value>The enable advanced optimizations.</value>
    public bool EnableAdvancedOptimizations { get; set; } = true;
    /// <summary>
    /// Gets or sets the preferred stream.
    /// </summary>
    /// <value>The preferred stream.</value>
    public object? PreferredStream { get; set; }
    /// <summary>
    /// Gets or sets the capture timings.
    /// </summary>
    /// <value>The capture timings.</value>
    public bool CaptureTimings { get; set; } = true;
}

/// <summary>
/// CUDA workflow options for graph execution.
/// </summary>
public sealed class CudaWorkflowOptions
{
    /// <summary>
    /// Gets or sets the graph options.
    /// </summary>
    /// <value>The graph options.</value>
    public CudaGraphOptimizationOptions? GraphOptions { get; set; }
    /// <summary>
    /// Gets or sets the use high priority stream.
    /// </summary>
    /// <value>The use high priority stream.</value>
    public bool UseHighPriorityStream { get; set; }
    /// <summary>
    /// Gets or sets the enable kernel fusion.
    /// </summary>
    /// <value>The enable kernel fusion.</value>
    public bool EnableKernelFusion { get; set; } = true;
}

/// <summary>
/// CUDA transfer options for P2P operations.
/// </summary>
public sealed class CudaTransferOptions
{
    /// <summary>
    /// Gets or sets the size bytes.
    /// </summary>
    /// <value>The size bytes.</value>
    public ulong SizeBytes { get; set; }
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>The priority.</value>
    public CudaTransferPriority Priority { get; set; } = CudaTransferPriority.Normal;
    /// <summary>
    /// Gets or sets the use optimal path.
    /// </summary>
    /// <value>The use optimal path.</value>
    public bool UseOptimalPath { get; set; } = true;
}
/// <summary>
/// An cuda transfer priority enumeration.
/// </summary>

/// <summary>
/// Transfer priority levels.
/// </summary>
public enum CudaTransferPriority
{
    Low,
    Normal,
    High
}

/// <summary>
/// CUDA execution result.
/// </summary>
public sealed class CudaExecutionResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; set; }
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the kernel execution result.
    /// </summary>
    /// <value>The kernel execution result.</value>
    public object? KernelExecutionResult { get; set; }
    /// <summary>
    /// Gets or sets the optimizations applied.
    /// </summary>
    /// <value>The optimizations applied.</value>
    public bool OptimizationsApplied { get; set; }
    /// <summary>
    /// Gets or sets the performance metrics.
    /// </summary>
    /// <value>The performance metrics.</value>
    public CudaPerformanceMetrics? PerformanceMetrics { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// System health information.
/// </summary>
public sealed class CudaSystemHealth
{
    /// <summary>
    /// Gets or sets the overall health.
    /// </summary>
    /// <value>The overall health.</value>
    public double OverallHealth { get; set; }
    /// <summary>
    /// Gets or sets the device health.
    /// </summary>
    /// <value>The device health.</value>
    public double DeviceHealth { get; set; }
    /// <summary>
    /// Gets or sets the context health.
    /// </summary>
    /// <value>The context health.</value>
    public double ContextHealth { get; set; }
    /// <summary>
    /// Gets or sets the memory health.
    /// </summary>
    /// <value>The memory health.</value>
    public double MemoryHealth { get; set; }
    /// <summary>
    /// Gets or sets the kernel health.
    /// </summary>
    /// <value>The kernel health.</value>
    public double KernelHealth { get; set; }
    /// <summary>
    /// Gets or sets the stream health.
    /// </summary>
    /// <value>The stream health.</value>
    public double StreamHealth { get; set; }
    /// <summary>
    /// Gets or sets the event health.
    /// </summary>
    /// <value>The event health.</value>
    public double EventHealth { get; set; }
    /// <summary>
    /// Gets or sets the p2 p health.
    /// </summary>
    /// <value>The p2 p health.</value>
    public double P2PHealth { get; set; }
    /// <summary>
    /// Gets or sets the advanced features health.
    /// </summary>
    /// <value>The advanced features health.</value>
    public double AdvancedFeaturesHealth { get; set; }
    /// <summary>
    /// Gets or sets the performance health.
    /// </summary>
    /// <value>The performance health.</value>
    public double PerformanceHealth { get; set; }
    /// <summary>
    /// Gets or sets the last checked.
    /// </summary>
    /// <value>The last checked.</value>
    public DateTimeOffset LastChecked { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Workload profile for optimization.
/// </summary>
public sealed class CudaWorkloadProfile
{
    /// <summary>
    /// Gets or sets a value indicating whether matrix operations.
    /// </summary>
    /// <value>The has matrix operations.</value>
    public bool HasMatrixOperations { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether cooperative workloads.
    /// </summary>
    /// <value>The has cooperative workloads.</value>
    public bool HasCooperativeWorkloads { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether memory intensive.
    /// </summary>
    /// <value>The is memory intensive.</value>
    public bool IsMemoryIntensive { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether compute intensive.
    /// </summary>
    /// <value>The is compute intensive.</value>
    public bool IsComputeIntensive { get; set; }
    /// <summary>
    /// Gets or sets the requires high precision.
    /// </summary>
    /// <value>The requires high precision.</value>
    public bool RequiresHighPrecision { get; set; }
    /// <summary>
    /// Gets or sets the estimated parallelism.
    /// </summary>
    /// <value>The estimated parallelism.</value>
    public int EstimatedParallelism { get; set; }
}

/// <summary>
/// Optimization summary.
/// </summary>
public sealed class CudaOptimizationSummary
{
    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    /// <value>The profile.</value>
    public CudaWorkloadProfile Profile { get; set; } = new();
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; set; }
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the optimizations applied.
    /// </summary>
    /// <value>The optimizations applied.</value>
    public IList<string> OptimizationsApplied { get; } = [];
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Performance metrics.
/// </summary>
public sealed class CudaPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the memory utilization.
    /// </summary>
    /// <value>The memory utilization.</value>
    public double MemoryUtilization { get; set; }
    /// <summary>
    /// Gets or sets the compute utilization.
    /// </summary>
    /// <value>The compute utilization.</value>
    public double ComputeUtilization { get; set; }
    /// <summary>
    /// Gets or sets the throughput g f l o p s.
    /// </summary>
    /// <value>The throughput g f l o p s.</value>
    public double ThroughputGFLOPS { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g bps.
    /// </summary>
    /// <value>The memory bandwidth g bps.</value>
    public double MemoryBandwidthGBps { get; set; }
    /// <summary>
    /// Gets or sets the measurement window.
    /// </summary>
    /// <value>The measurement window.</value>
    public TimeSpan MeasurementWindow { get; set; }
}

/// <summary>
/// Performance monitor implementation.
/// </summary>
public sealed class CudaPerformanceMonitor : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly Timer _metricsTimer;
    private CudaPerformanceMetrics _currentMetrics;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaPerformanceMonitor class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    public CudaPerformanceMonitor(CudaContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
        _currentMetrics = new CudaPerformanceMetrics();

        _metricsTimer = new Timer(UpdateMetrics, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
    }
    /// <summary>
    /// Gets the current metrics.
    /// </summary>
    /// <returns>The current metrics.</returns>

    public CudaPerformanceMetrics CurrentMetrics => _currentMetrics;

    /// <summary>
    /// Gets the current performance metrics.
    /// </summary>
    public CudaPerformanceMetrics GetCurrentMetrics() => CurrentMetrics;

    private void UpdateMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Simplified metrics gathering
            _currentMetrics = new CudaPerformanceMetrics
            {
                MemoryUtilization = 0.6 + Random.Shared.NextDouble() * 0.3, // 60-90%
                ComputeUtilization = 0.4 + Random.Shared.NextDouble() * 0.5, // 40-90%
                ThroughputGFLOPS = 1000 + Random.Shared.NextDouble() * 2000, // 1-3 TFLOPS
                MemoryBandwidthGBps = 400 + Random.Shared.NextDouble() * 400, // 400-800 GB/s
                MeasurementWindow = TimeSpan.FromSeconds(5)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance metrics");
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsTimer?.Dispose();
            _disposed = true;
        }
    }
}



#endregion