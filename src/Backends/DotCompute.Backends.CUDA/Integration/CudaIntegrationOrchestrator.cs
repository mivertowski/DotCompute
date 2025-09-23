// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Execution;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.P2P;
using DotCompute.Backends.CUDA.Advanced;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using System.Linq;
using AbstractionsCompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using AbstractionsKernelArgument = DotCompute.Abstractions.Kernels.KernelArgument;
using InterfacesKernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Main orchestrator for CUDA backend integration with production optimizations
/// </summary>
public sealed class CudaIntegrationOrchestrator : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly CudaContext _context;
    private readonly CudaDeviceManager _deviceManager;
    private readonly CudaContextManager _contextManager;
    private readonly CudaMemoryIntegration _memoryIntegration;
    private readonly CudaKernelIntegration _kernelIntegration;
    private readonly CudaStreamManager _streamManager;
    private readonly Timer _healthCheckTimer;
    private bool _disposed;

    public CudaIntegrationOrchestrator(
        IServiceProvider serviceProvider,
        CudaContext context,
        ILogger<CudaIntegrationOrchestrator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        
        _deviceManager = new CudaDeviceManager(loggerFactory.CreateLogger<CudaDeviceManager>());
        _contextManager = new CudaContextManager(context, loggerFactory.CreateLogger<CudaContextManager>());
        _memoryIntegration = new CudaMemoryIntegration(context, loggerFactory.CreateLogger<CudaMemoryIntegration>());
        _kernelIntegration = new CudaKernelIntegration(context, loggerFactory.CreateLogger<CudaKernelIntegration>());
        _streamManager = new CudaStreamManager(context, loggerFactory.CreateLogger<CudaStreamManager>());

        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

        _logger.LogInfoMessage($"CUDA Integration Orchestrator initialized for device {context.DeviceId}");
    }

    /// <summary>
    /// Device manager for hardware management
    /// </summary>
    public CudaDeviceManager DeviceManager => _deviceManager;

    /// <summary>
    /// Context manager for CUDA context lifecycle
    /// </summary>
    public CudaContextManager ContextManager => _contextManager;

    /// <summary>
    /// Memory integration for unified buffer management
    /// </summary>
    public CudaMemoryIntegration MemoryIntegration => _memoryIntegration;

    /// <summary>
    /// Kernel integration for optimized execution
    /// </summary>
    public CudaKernelIntegration KernelIntegration => _kernelIntegration;

    /// <summary>
    /// Stream manager for concurrent execution
    /// </summary>
    public CudaStreamManager StreamManager => _streamManager;

    /// <summary>
    /// Executes a kernel with full optimization pipeline
    /// </summary>
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

            // Execute with the kernel integration
            var result = await _kernelIntegration.ExecuteKernelAsync(
                kernel, arguments, execConfig, cancellationToken)
                .ConfigureAwait(false);

            var endTime = DateTimeOffset.UtcNow;

            return new CudaExecutionResult
            {
                Success = result.Success,
                ExecutionTime = endTime - startTime,
                KernelExecutionResult = result,
                OptimizationsApplied = options.EnableAdvancedOptimizations,
                PerformanceMetrics = null // Will be populated by performance monitor
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during optimized kernel execution");
            return new CudaExecutionResult
            {
                Success = false,
                ExecutionTime = DateTimeOffset.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets comprehensive system health status
    /// </summary>
    public CudaSystemHealth GetSystemHealth()
    {
        ThrowIfDisposed();

        try
        {
            var deviceHealth = _deviceManager.GetDeviceHealth();
            var contextHealth = _contextManager.GetContextHealth();
            var memoryHealth = _memoryIntegration.GetMemoryHealth();
            var kernelHealth = _kernelIntegration.GetKernelHealth();
            var streamHealth = _streamManager.GetStreamHealth();

            return new CudaSystemHealth
            {
                OverallHealth = CalculateOverallHealth(deviceHealth, contextHealth, memoryHealth, kernelHealth, streamHealth),
                DeviceHealth = deviceHealth,
                ContextHealth = contextHealth,
                MemoryHealth = memoryHealth,
                KernelHealth = kernelHealth,
                StreamHealth = streamHealth,
                LastChecked = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error getting system health");
            return new CudaSystemHealth
            {
                OverallHealth = 0.0,
                LastChecked = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Optimizes the entire backend for the current workload
    /// </summary>
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
            // Device optimization
            await _deviceManager.OptimizeDeviceAsync(profile, cancellationToken);
            summary.OptimizationsApplied.Add("Device optimization completed");

            // Context optimization
            await _contextManager.OptimizeContextAsync(profile, cancellationToken);
            summary.OptimizationsApplied.Add("Context optimization completed");

            // Memory optimization
            await _memoryIntegration.OptimizeMemoryAsync(profile, cancellationToken);
            summary.OptimizationsApplied.Add("Memory optimization completed");

            // Kernel optimization
            await _kernelIntegration.OptimizeKernelsAsync(profile, cancellationToken);
            summary.OptimizationsApplied.Add("Kernel optimization completed");

            // Stream optimization
            _streamManager.OptimizeStreamUsage();
            summary.OptimizationsApplied.Add("Stream optimization completed");

            summary.EndTime = DateTimeOffset.UtcNow;
            summary.Success = true;

            _logger.LogInfoMessage($"Backend optimization completed: {summary.OptimizationsApplied.Count} optimizations applied");

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during backend optimization");
            summary.Success = false;
            summary.ErrorMessage = ex.Message;
            summary.EndTime = DateTimeOffset.UtcNow;
            return summary;
        }
    }

    private static Task ApplyAdvancedOptimizationsAsync(
        CudaCompiledKernel kernel,
        AbstractionsKernelArgument[] arguments,
        CudaExecutionOptions options,
        CancellationToken cancellationToken)
    {
        // Advanced features optimization logic would go here
        return Task.CompletedTask;
    }

    private async Task<KernelExecutionConfig> GetOptimalExecutionConfigAsync(
        CudaCompiledKernel kernel,
        AbstractionsKernelArgument[] arguments,
        CudaExecutionOptions options,
        CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        // Determine problem size
        var problemSize = EstimateProblemSize(arguments);

        // Get optimal configuration from kernel integration
        return _kernelIntegration.GetOptimalExecutionConfig(kernel, problemSize);
    }

    private static int[] EstimateProblemSize(AbstractionsKernelArgument[] arguments)
    {
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

    private static double CalculateOverallHealth(
        double deviceHealth,
        double contextHealth,
        double memoryHealth,
        double kernelHealth,
        double streamHealth)
    {
        return (deviceHealth + contextHealth + memoryHealth + kernelHealth + streamHealth) / 5.0;
    }

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
                _logger.LogWarningMessage($"CUDA backend health degraded: {health.OverallHealth}");
            }
            else
            {
                _logger.LogDebugMessage($"CUDA backend health: {health.OverallHealth}");
            }

            // Trigger maintenance if needed
            if (health.OverallHealth < 0.5)
            {
                PerformMaintenance();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during health check");
        }
    }

    private void PerformMaintenance()
    {
        try
        {
            _deviceManager.PerformMaintenance();
            _contextManager.PerformMaintenance();
            _memoryIntegration.PerformMaintenance();
            _kernelIntegration.PerformMaintenance();
            _streamManager.OptimizeStreamUsage();

            _logger.LogInfoMessage("CUDA backend maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during backend maintenance");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaIntegrationOrchestrator));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _streamManager?.Dispose();
            _kernelIntegration?.Dispose();
            _memoryIntegration?.Dispose();
            _contextManager?.Dispose();
            _deviceManager?.Dispose();

            _disposed = true;

            _logger.LogInfoMessage("CUDA Integration Orchestrator disposed");
        }
    }
}
