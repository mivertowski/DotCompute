// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Unified Metal execution manager that orchestrates all execution components,
/// integrating with the existing Metal backend architecture for production-grade usage.
/// </summary>
public sealed class MetalExecutionManager : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalExecutionManager> _logger;
    private readonly MetalExecutionOptions _options;
    private MetalCommandStream _commandStream = null!;
    private MetalEventManager _eventManager = null!;
    private MetalErrorHandler _errorHandler = null!;
    private MetalExecutionContext _executionContext = null!;
    private MetalCommandEncoderFactory _encoderFactory = null!;
    private readonly MetalExecutionTelemetry _telemetry = null!;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes the Metal execution manager with all required components
    /// </summary>
    public MetalExecutionManager(
        IntPtr device,
        ILogger<MetalExecutionManager> logger,
        IOptions<MetalExecutionManagerOptions>? options = null)
    {
        _device = device != IntPtr.Zero ? device : throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var managerOptions = options?.Value ?? new MetalExecutionManagerOptions();
        _options = managerOptions.ExecutionOptions;

        try
        {
            // Initialize core execution components
            InitializeComponents(managerOptions);

            // Initialize telemetry if enabled

            if (managerOptions.EnableTelemetry)
            {
                _telemetry = new MetalExecutionTelemetry(_logger, managerOptions.TelemetryReportingInterval);
            }
            else
            {
                _telemetry = new MetalExecutionTelemetry(_logger); // No reporting interval
            }

            // Record initialization telemetry
            _telemetry.RecordEvent("Initialization", "ExecutionManagerCreated", new Dictionary<string, object>
            {
                ["Device"] = _device.ToString(CultureInfo.InvariantCulture),
                ["IsAppleSilicon"] = _executionContext?.IsAppleSilicon ?? false,
                ["EnableTelemetry"] = managerOptions.EnableTelemetry
            });

            _logger.LogInformation(
                "Metal Execution Manager initialized: device={Device}, architecture={Architecture}, " +
                "telemetry={TelemetryEnabled}, components_initialized=5",
                _device, (_executionContext?.IsAppleSilicon ?? false) ? "Apple Silicon" : "Intel Mac", managerOptions.EnableTelemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Metal Execution Manager");
            Dispose(); // Clean up any partially initialized components
            throw;
        }
    }

    #region Public Properties

    /// <summary>
    /// Gets the Metal device handle
    /// </summary>
    public IntPtr Device => _device;

    /// <summary>
    /// Gets the command stream manager
    /// </summary>
    public MetalCommandStream CommandStream => _commandStream;

    /// <summary>
    /// Gets the event manager
    /// </summary>
    public MetalEventManager EventManager => _eventManager;

    /// <summary>
    /// Gets the error handler
    /// </summary>
    public MetalErrorHandler ErrorHandler => _errorHandler;

    /// <summary>
    /// Gets the execution context
    /// </summary>
    public MetalExecutionContext ExecutionContext => _executionContext;

    /// <summary>
    /// Gets the command encoder factory
    /// </summary>
    public MetalCommandEncoderFactory EncoderFactory => _encoderFactory;

    /// <summary>
    /// Gets whether the execution manager is running on Apple Silicon
    /// </summary>
    public bool IsAppleSilicon => _executionContext.IsAppleSilicon;

    /// <summary>
    /// Gets whether the GPU is currently available
    /// </summary>
    public bool IsGpuAvailable => _errorHandler.IsGpuAvailable;

    #endregion

    #region High-Level Execution Methods

    /// <summary>
    /// Executes a compute operation with comprehensive management and error handling
    /// </summary>
    public async Task<T> ExecuteComputeOperationAsync<T>(
        MetalComputeOperationDescriptor descriptor,
        Func<MetalOperationExecutionInfo, MetalCommandEncoder, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var executionOptions = new MetalExecutionOptions
        {
            Priority = descriptor.Priority,
            Dependencies = descriptor.Dependencies?.ToArray(),
            Timeout = descriptor.EstimatedDuration,
            EnableProfiling = _options.ProfilingLevel != MetalProfilingLevel.None
        };

        // Record operation start telemetry
        _telemetry.RecordEvent("Execution", "ComputeOperationStarted", new Dictionary<string, object>
        {
            ["OperationId"] = descriptor.OperationId,
            ["Name"] = descriptor.Name,
            ["Priority"] = descriptor.Priority.ToString()
        });

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var result = await _executionContext.ExecuteOperationAsync(
                descriptor.OperationId,
                async executionInfo =>
                {
                    // Create command buffer and encoder
                    var commandBuffer = MetalNative.CreateCommandBuffer(executionInfo.StreamHandle.CommandQueue);
                    if (commandBuffer == IntPtr.Zero)
                    {
                        throw new MetalOperationException(descriptor.OperationId, "Failed to create command buffer");
                    }

                    try
                    {
                        using var encoder = _encoderFactory.CreateEncoder(commandBuffer);

                        // Execute the user operation

                        var operationResult = await operation(executionInfo, encoder).ConfigureAwait(false);

                        // Ensure encoding is finished
                        encoder.EndEncoding();

                        // Commit and wait for completion
                        MetalNative.CommitCommandBuffer(commandBuffer);
                        MetalNative.WaitUntilCompleted(commandBuffer);

                        return operationResult;
                    }
                    finally
                    {
                        MetalNative.ReleaseCommandBuffer(commandBuffer);
                    }
                },
                executionOptions,
                cancellationToken).ConfigureAwait(false);

            var duration = DateTimeOffset.UtcNow - startTime;

            // Record successful execution telemetry
            _telemetry.RecordTiming(descriptor.Name, duration, success: true);
            _telemetry.RecordEvent("Execution", "ComputeOperationCompleted", new Dictionary<string, object>
            {
                ["OperationId"] = descriptor.OperationId,
                ["Duration"] = duration.TotalMilliseconds,
                ["Success"] = true
            });

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;

            // Record failed execution telemetry
            _telemetry.RecordTiming(descriptor.Name, duration, success: false);
            _telemetry.RecordEvent("Execution", "ComputeOperationFailed", new Dictionary<string, object>
            {
                ["OperationId"] = descriptor.OperationId,
                ["Duration"] = duration.TotalMilliseconds,
                ["Success"] = false,
                ["Error"] = ex.Message
            });

            if (ex is MetalException metalEx)
            {
                _telemetry.RecordError("ComputeOperation", metalEx.ErrorCode, ex.Message);
            }

            throw;
        }
    }

    /// <summary>
    /// Executes a memory operation with comprehensive management
    /// </summary>
    public async Task ExecuteMemoryOperationAsync(
        MetalMemoryOperationDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _telemetry.RecordEvent("Memory", "MemoryOperationStarted", new Dictionary<string, object>
        {
            ["OperationId"] = descriptor.OperationId,
            ["Type"] = descriptor.Operation.ToString(),
            ["Bytes"] = descriptor.BytesToCopy
        });

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _ = await _executionContext.ExecuteOperationAsync(
                descriptor.OperationId,
                async executionInfo =>
                {
                    switch (descriptor.Operation)
                    {
                        case MetalMemoryOperationDescriptor.OperationType.DeviceToDevice:
                            MetalNative.CopyBuffer(
                                descriptor.Source,

                                descriptor.SourceOffset,
                                descriptor.Destination,

                                descriptor.DestinationOffset,
                                descriptor.BytesToCopy);
                            break;

                        case MetalMemoryOperationDescriptor.OperationType.Clear:
                            // Clear operation would be implemented here
                            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                            break;

                        default:
                            throw new NotSupportedException($"Memory operation {descriptor.Operation} not supported");
                    }

                    return true;
                },
                new MetalExecutionOptions
                {
                    Priority = descriptor.Priority,
                    Dependencies = descriptor.Dependencies?.ToArray()
                },
                cancellationToken).ConfigureAwait(false);

            var duration = DateTimeOffset.UtcNow - startTime;
            _telemetry.RecordTiming($"Memory_{descriptor.Operation}", duration, success: true);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _telemetry.RecordTiming($"Memory_{descriptor.Operation}", duration, success: false);

            if (ex is MetalException metalEx)
            {
                _telemetry.RecordError("MemoryOperation", metalEx.ErrorCode, ex.Message);
            }

            throw;
        }
    }

    #endregion

    #region Resource Management

    /// <summary>
    /// Creates and tracks a Metal buffer
    /// </summary>
    public IntPtr CreateTrackedBuffer(nuint length, MetalStorageMode storageMode, string? resourceId = null)
    {
        ThrowIfDisposed();

        var buffer = MetalNative.CreateBuffer(_device, length, storageMode);
        if (buffer == IntPtr.Zero)
        {
            throw new MetalOperationException("CreateBuffer", "Failed to create Metal buffer");
        }

        var id = resourceId ?? Guid.NewGuid().ToString("N")[..8];
        _executionContext.TrackResource(id, buffer, MetalResourceType.Buffer, (long)length);

        _telemetry.RecordResourceAllocation(MetalResourceType.Buffer, (long)length, success: true);

        return buffer;
    }

    /// <summary>
    /// Releases and untracks a Metal resource
    /// </summary>
    public void ReleaseTrackedResource(string resourceId, bool releaseNativeResource = true)
    {
        ThrowIfDisposed();
        _executionContext.UntrackResource(resourceId, releaseNativeResource);
    }

    #endregion

    #region Monitoring and Diagnostics

    /// <summary>
    /// Gets comprehensive statistics from all execution components
    /// </summary>
    public MetalExecutionManagerStats GetStatistics()
    {
        ThrowIfDisposed();

        var streamStats = _commandStream.GetStatistics();
        var eventStats = _eventManager.GetStatistics();
        var contextStats = _executionContext.GetStatistics();
        var errorStats = _errorHandler.GetErrorStatistics();

        var stats = new MetalExecutionManagerStats
        {
            // Component statistics
            StreamStatistics = streamStats,
            EventStatistics = eventStats,
            ExecutionStatistics = contextStats,

            // Overall health
            IsGpuAvailable = _errorHandler.IsGpuAvailable,
            IsExecutionPaused = _executionContext.IsExecutionPaused,
            IsAppleSilicon = _executionContext.IsAppleSilicon
        };

        // Add items to collection properties
        var errorStatistics = errorStats.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
        foreach (var kvp in errorStatistics)
        {
            stats.ErrorStatistics[kvp.Key] = kvp.Value;
        }

        var telemetryMetrics = _telemetry.GetMetrics();
        foreach (var kvp in telemetryMetrics)
        {
            stats.TelemetryMetrics[kvp.Key] = kvp.Value;
        }

        var recentEvents = _telemetry.GetRecentEvents(20);
        foreach (var evt in recentEvents)
        {
            stats.RecentTelemetryEvents.Add(evt);
        }

        return stats;
    }

    /// <summary>
    /// Performs a comprehensive health check
    /// </summary>
    public async Task<MetalExecutionManagerHealthCheck> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var healthCheck = new MetalExecutionManagerHealthCheck
        {
            CheckTime = DateTimeOffset.UtcNow,
            IsHealthy = true
        };

        try
        {
            // Check execution context health
            var contextHealth = await _executionContext.PerformHealthCheckAsync(cancellationToken).ConfigureAwait(false);
            if (!contextHealth.IsHealthy)
            {
                healthCheck.IsHealthy = false;
                foreach (var issue in contextHealth.Issues)
                {
                    healthCheck.Issues.Add(issue);
                }
            }

            // Get component statistics
            var streamStats = _commandStream.GetStatistics();
            var eventStats = _eventManager.GetStatistics();

            // Check component health
            healthCheck.ComponentHealth["GPU"] = _errorHandler.IsGpuAvailable;
            healthCheck.ComponentHealth["ExecutionContext"] = contextHealth.IsHealthy;
            healthCheck.ComponentHealth["CommandStream"] = streamStats.ActiveStreams >= 0;
            healthCheck.ComponentHealth["EventManager"] = eventStats.ActiveEvents >= 0;

            // Check for performance issues
            var stats = GetStatistics();
            if (stats.ExecutionStatistics.SuccessRate < 0.95) // Less than 95% success rate
            {
                healthCheck.IsHealthy = false;
                healthCheck.Issues.Add($"Low operation success rate: {stats.ExecutionStatistics.SuccessRate:P2}");
            }

            _telemetry.RecordEvent("Health", "HealthCheckCompleted", new Dictionary<string, object>
            {
                ["IsHealthy"] = healthCheck.IsHealthy,
                ["IssueCount"] = healthCheck.Issues.Count
            });
        }
        catch (Exception ex)
        {
            healthCheck.IsHealthy = false;
            healthCheck.Issues.Add($"Health check failed: {ex.Message}");
        }

        return healthCheck;
    }

    /// <summary>
    /// Generates a comprehensive diagnostic report
    /// </summary>
    public MetalDiagnosticInfo GenerateDiagnosticReport()
    {
        ThrowIfDisposed();

        var stats = GetStatistics();
        var telemetryReport = _telemetry.GenerateReport();

        var diagnosticInfo = new MetalDiagnosticInfo
        {
            Health = stats.IsGpuAvailable ? MetalExecutionHealth.Healthy : MetalExecutionHealth.Critical,
            Architecture = stats.IsAppleSilicon ? MetalGpuArchitecture.AppleM1 : MetalGpuArchitecture.IntelIntegrated,
            PlatformOptimization = stats.IsAppleSilicon ? MetalPlatformOptimization.MacOS : MetalPlatformOptimization.Generic,
            Configuration = _options.ExecutionConfiguration
        };

        // Add items to collection properties
        var resourceUsage = stats.ExecutionStatistics.ResourceBreakdown
            .ToDictionary(kvp => kvp.Key, kvp => (long)kvp.Value);
        foreach (var kvp in resourceUsage)
        {
            diagnosticInfo.ResourceUsage[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in stats.TelemetryMetrics)
        {
            diagnosticInfo.PerformanceMetrics[kvp.Key] = kvp.Value;
        }

        // Messages would be populated with recent diagnostic messages - currently empty

        diagnosticInfo.SystemInfo["Device"] = _device.ToString(CultureInfo.InvariantCulture);
        diagnosticInfo.SystemInfo["IsAppleSilicon"] = stats.IsAppleSilicon.ToString(CultureInfo.InvariantCulture);
        diagnosticInfo.SystemInfo["GpuAvailable"] = stats.IsGpuAvailable.ToString(CultureInfo.InvariantCulture);
        diagnosticInfo.SystemInfo["ExecutionPaused"] = stats.IsExecutionPaused.ToString(CultureInfo.InvariantCulture);

        return diagnosticInfo;
    }

    #endregion

    #region Private Methods

    private void InitializeComponents(MetalExecutionManagerOptions options)
    {
        // Create loggers for each component
        var streamLogger = LoggerFactory.Create(builder =>

            builder.SetMinimumLevel(_logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalCommandStream>();


        var eventLogger = LoggerFactory.Create(builder =>

            builder.SetMinimumLevel(_logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalEventManager>();


        var errorLogger = LoggerFactory.Create(builder =>

            builder.SetMinimumLevel(_logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalErrorHandler>();


        var contextLogger = LoggerFactory.Create(builder =>

            builder.SetMinimumLevel(_logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalExecutionContext>();


        var encoderLogger = LoggerFactory.Create(builder =>

            builder.SetMinimumLevel(_logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalCommandEncoder>();

        // Initialize components in order
        _commandStream = new MetalCommandStream(_device, streamLogger);
        _eventManager = new MetalEventManager(_device, eventLogger);
        _errorHandler = new MetalErrorHandler(errorLogger, options.ErrorRecoveryOptions);


        var contextOptions = new MetalExecutionContextOptions
        {
            ErrorRecoveryOptions = options.ErrorRecoveryOptions,
            EnablePerformanceTracking = options.EnablePerformanceTracking
        };
        _executionContext = new MetalExecutionContext(_device, contextLogger, contextOptions);


        _encoderFactory = new MetalCommandEncoderFactory(encoderLogger);

        _logger.LogDebug("Initialized Metal execution components: CommandStream, EventManager, ErrorHandler, ExecutionContext, EncoderFactory");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _logger.LogInformation("Disposing Metal Execution Manager...");

            try
            {
                // Record disposal telemetry
                _telemetry?.RecordEvent("Lifecycle", "ExecutionManagerDisposed", new Dictionary<string, object>
                {
                    ["Device"] = _device.ToString(CultureInfo.InvariantCulture),
                    ["IsAppleSilicon"] = _executionContext?.IsAppleSilicon ?? false
                });

                // Dispose components in reverse order of creation
                _encoderFactory?.NotifyEncoderDisposed(IntPtr.Zero); // Cleanup any remaining state
                _executionContext?.Dispose();
                _errorHandler?.Dispose();
                _eventManager?.Dispose();
                _commandStream?.Dispose();
                _telemetry?.Dispose();

                _logger.LogInformation("Metal Execution Manager disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Metal Execution Manager disposal");
            }
        }
    }
}

#region Configuration Types

/// <summary>
/// Options for configuring the Metal execution manager
/// </summary>
public sealed class MetalExecutionManagerOptions
{
    public MetalExecutionOptions ExecutionOptions { get; set; } = new();
    public MetalErrorRecoveryOptions? ErrorRecoveryOptions { get; set; }
    public bool EnableTelemetry { get; set; } = true;
    public bool EnablePerformanceTracking { get; set; } = true;
    public TimeSpan? TelemetryReportingInterval { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Comprehensive statistics from all execution components
/// </summary>
public sealed class MetalExecutionManagerStats
{
    public MetalStreamStatistics StreamStatistics { get; set; } = new();
    public MetalEventStatistics EventStatistics { get; set; } = new();
    public MetalExecutionStatistics ExecutionStatistics { get; set; } = new();
    public Dictionary<string, MetalErrorHandler.ErrorStatistics> ErrorStatistics { get; } = [];
    public bool IsGpuAvailable { get; set; }
    public bool IsExecutionPaused { get; set; }
    public bool IsAppleSilicon { get; set; }
    public Dictionary<string, object> TelemetryMetrics { get; } = [];
    public IList<MetalTelemetryEvent> RecentTelemetryEvents { get; } = [];
}

/// <summary>
/// Health check result for the execution manager
/// </summary>
public sealed class MetalExecutionManagerHealthCheck
{
    public DateTimeOffset CheckTime { get; set; }
    public bool IsHealthy { get; set; }
    public IList<string> Issues { get; } = [];
    public Dictionary<string, bool> ComponentHealth { get; } = [];
}



#endregion