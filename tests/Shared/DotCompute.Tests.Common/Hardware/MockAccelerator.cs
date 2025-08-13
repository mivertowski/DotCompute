// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Tests.Shared.TestInfrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Mock accelerator implementation that wraps a MockHardwareDevice.
/// Provides full IAccelerator implementation for testing without hardware dependencies.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MockAccelerator : IAccelerator
{
    private readonly ILogger _logger;
    private readonly MockHardwareDevice _hardwareDevice;
    private readonly IMemoryManager _memoryManager;
    private readonly AcceleratorContext _context;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MockAccelerator class.
    /// </summary>
    /// <param name="hardwareDevice">The underlying hardware device to wrap.</param>
    /// <param name="logger">Optional logger instance.</param>
    public MockAccelerator(MockHardwareDevice hardwareDevice, ILogger? logger = null)
    {
        _hardwareDevice = hardwareDevice ?? throw new ArgumentNullException(nameof(hardwareDevice));
        _logger = logger ?? NullLogger.Instance;
        _memoryManager = new TestMemoryManager();
        _context = new AcceleratorContext(IntPtr.Zero, 0);

        _logger.LogDebug("Created MockAccelerator for device {DeviceId} ({DeviceName})", 
            hardwareDevice.Id, hardwareDevice.Name);
    }

    /// <summary>
    /// Convenience constructor that creates a MockAccelerator with a specific device type.
    /// </summary>
    /// <param name="deviceType">The type of device to create.</param>
    /// <param name="logger">Optional logger instance.</param>
    public MockAccelerator(AcceleratorType deviceType = AcceleratorType.CUDA, ILogger? logger = null)
        : this(CreateDefaultDevice(deviceType, logger), logger)
    {
    }

    /// <inheritdoc/>
    public AcceleratorInfo Info => _hardwareDevice.ToAcceleratorInfo();

    /// <inheritdoc/>
    public AcceleratorType Type => _hardwareDevice.Type;

    /// <inheritdoc/>
    public IMemoryManager Memory => _memoryManager;

    /// <inheritdoc/>
    public AcceleratorContext Context => _context;

    /// <summary>
    /// Gets the underlying hardware device.
    /// </summary>
    public MockHardwareDevice HardwareDevice => _hardwareDevice;

    /// <summary>
    /// Gets whether the accelerator is currently available.
    /// </summary>
    public bool IsAvailable => _hardwareDevice.IsAvailable && !_disposed;

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfUnavailable();

        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        _logger.LogDebug("Compiling kernel '{KernelName}' on device {DeviceId}", 
            definition.Name, _hardwareDevice.Id);

        // Simulate compilation time based on device type and options
        var compilationTime = CalculateCompilationTime(definition, options);
        if (compilationTime > TimeSpan.Zero)
        {
            await Task.Delay(compilationTime, cancellationToken);
        }

        // Create a mock compiled kernel
        var kernel = new MockCompiledKernel(definition, options, _hardwareDevice, _logger);
        
        _logger.LogDebug("Successfully compiled kernel '{KernelName}' in {CompilationTime}ms", 
            definition.Name, compilationTime.TotalMilliseconds);

        return kernel;
    }

    /// <inheritdoc/>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Simulate synchronization delay
        var syncTime = _hardwareDevice.Type switch
        {
            AcceleratorType.CPU => TimeSpan.FromMicroseconds(10),
            AcceleratorType.CUDA => TimeSpan.FromMicroseconds(100),
            AcceleratorType.Metal => TimeSpan.FromMicroseconds(50),
            _ => TimeSpan.FromMicroseconds(25)
        };

        if (syncTime > TimeSpan.Zero)
        {
            await Task.Delay(syncTime, cancellationToken);
        }

        _logger.LogTrace("Synchronized accelerator {DeviceId}", _hardwareDevice.Id);
    }

    /// <summary>
    /// Simulates a device failure for testing error handling.
    /// </summary>
    /// <param name="errorMessage">The error message to simulate.</param>
    public void SimulateFailure(string errorMessage = "Simulated accelerator failure")
    {
        _hardwareDevice.SimulateFailure(errorMessage);
        _logger.LogWarning("Simulated failure on accelerator {DeviceId}: {Error}", 
            _hardwareDevice.Id, errorMessage);
    }

    /// <summary>
    /// Resets any simulated failures.
    /// </summary>
    public void ResetFailure()
    {
        _hardwareDevice.ResetFailure();
        _logger.LogInformation("Reset failure simulation on accelerator {DeviceId}", _hardwareDevice.Id);
    }

    /// <summary>
    /// Simulates memory usage on the device.
    /// </summary>
    /// <param name="memoryUsed">The amount of memory to mark as used.</param>
    public void SimulateMemoryUsage(long memoryUsed)
    {
        _hardwareDevice.SimulateMemoryUsage(memoryUsed);
    }

    /// <summary>
    /// Gets performance metrics for this accelerator.
    /// </summary>
    /// <returns>A dictionary containing performance metrics.</returns>
    public Dictionary<string, object> GetPerformanceMetrics()
    {
        ThrowIfDisposed();

        var metrics = new Dictionary<string, object>
        {
            ["DeviceId"] = _hardwareDevice.Id,
            ["DeviceName"] = _hardwareDevice.Name,
            ["DeviceType"] = _hardwareDevice.Type.ToString(),
            ["IsAvailable"] = IsAvailable,
            ["TotalMemory"] = _hardwareDevice.TotalMemory,
            ["AvailableMemory"] = _hardwareDevice.AvailableMemory,
            ["MemoryUtilization"] = _hardwareDevice.TotalMemory > 0 
                ? (_hardwareDevice.TotalMemory - _hardwareDevice.AvailableMemory) / (double)_hardwareDevice.TotalMemory 
                : 0,
            ["ComputeUnits"] = _hardwareDevice.ComputeUnits,
            ["MaxClockFrequency"] = _hardwareDevice.MaxClockFrequency,
            ["AllocatedBuffers"] = (_memoryManager as TestMemoryManager)?.AllocatedBufferCount ?? 0,
            ["AllocatedMemory"] = (_memoryManager as TestMemoryManager)?.TotalAllocatedMemory ?? 0,
            ["LastHealthCheck"] = _hardwareDevice.HealthCheck(),
            ["MetricsTimestamp"] = DateTime.UtcNow
        };

        // Add device-specific metrics
        foreach (var capability in _hardwareDevice.Capabilities)
        {
            metrics[$"Capability_{capability.Key}"] = capability.Value;
        }

        return metrics;
    }

    /// <summary>
    /// Performs a comprehensive health check on the accelerator.
    /// </summary>
    /// <returns>True if the accelerator is healthy, false otherwise.</returns>
    public bool PerformHealthCheck()
    {
        if (_disposed)
        {
            _logger.LogWarning("Health check failed: Accelerator is disposed");
            return false;
        }

        if (!_hardwareDevice.HealthCheck())
        {
            _logger.LogWarning("Health check failed: Underlying hardware device failed health check");
            return false;
        }

        try
        {
            // Test memory manager
            if (_memoryManager is TestMemoryManager testMemManager)
            {
                var testSize = 1024;
                using var testBuffer = testMemManager.AllocateAsync(testSize).AsTask().Result;
                if (testBuffer.SizeInBytes != testSize)
                {
                    _logger.LogWarning("Health check failed: Memory allocation test failed");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed: Memory manager test failed");
            return false;
        }

        _logger.LogDebug("Health check passed for accelerator {DeviceId}", _hardwareDevice.Id);
        return true;
    }

    private static MockHardwareDevice CreateDefaultDevice(AcceleratorType deviceType, ILogger? logger)
    {
        return deviceType switch
        {
            AcceleratorType.CUDA => MockCudaDevice.CreateRTX4070(logger),
            AcceleratorType.CPU => MockCpuDevice.CreateIntelCore(logger),
            AcceleratorType.Metal => MockMetalDevice.CreateM2(logger),
            AcceleratorType.GPU => MockCudaDevice.CreateRTX4070(logger),
            _ => throw new NotSupportedException($"Device type {deviceType} is not supported")
        };
    }

    private TimeSpan CalculateCompilationTime(KernelDefinition definition, CompilationOptions options)
    {
        // Simulate compilation time based on various factors
        var baseTime = _hardwareDevice.Type switch
        {
            AcceleratorType.CPU => 50, // CPU compilation is typically faster
            AcceleratorType.CUDA => 200, // NVRTC compilation
            AcceleratorType.Metal => 100, // Metal shader compilation
            _ => 150
        };

        // Adjust for optimization level
        var optimizationMultiplier = options.OptimizationLevel switch
        {
            OptimizationLevel.None => 0.5,
            OptimizationLevel.Debug => 0.7,
            OptimizationLevel.Default => 1.0,
            OptimizationLevel.Release => 1.3,
            OptimizationLevel.Maximum => 1.8,
            OptimizationLevel.Aggressive => 2.2,
            _ => 1.0
        };

        // Adjust for code size (rough estimation)
        var codeSize = definition.Code?.Length ?? 1000;
        var sizeMultiplier = Math.Max(0.5, Math.Min(3.0, codeSize / 1000.0));

        var totalMs = (int)(baseTime * optimizationMultiplier * sizeMultiplier);
        return TimeSpan.FromMilliseconds(totalMs);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ThrowIfUnavailable()
    {
        if (!IsAvailable)
        {
            var errorMessage = _hardwareDevice.FailureMessage ?? "Device is not available";
            throw new InvalidOperationException($"Accelerator is not available: {errorMessage}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Dispose memory manager
            if (_memoryManager is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_memoryManager is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger.LogDebug("MockAccelerator disposed for device {DeviceId}", _hardwareDevice.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during MockAccelerator disposal");
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Mock compiled kernel implementation for the hardware abstraction layer.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MockCompiledKernel : ICompiledKernel
{
    private readonly KernelDefinition _definition;
    private readonly CompilationOptions _options;
    private readonly MockHardwareDevice _device;
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MockCompiledKernel class.
    /// </summary>
    /// <param name="definition">The kernel definition.</param>
    /// <param name="options">The compilation options.</param>
    /// <param name="device">The target device.</param>
    /// <param name="logger">Optional logger instance.</param>
    public MockCompiledKernel(
        KernelDefinition definition,
        CompilationOptions options,
        MockHardwareDevice device,
        ILogger? logger = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public string Name => _definition.Name;

    /// <summary>
    /// Gets the compilation options used for this kernel.
    /// </summary>
    public CompilationOptions CompilationOptions => _options;

    /// <summary>
    /// Gets the target device for this kernel.
    /// </summary>
    public MockHardwareDevice TargetDevice => _device;

    /// <inheritdoc/>
    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_device.IsAvailable)
        {
            throw new InvalidOperationException($"Cannot execute kernel: Device {_device.Id} is not available");
        }

        _logger.LogDebug("Executing kernel '{KernelName}' on device {DeviceId} with {ArgumentCount} arguments",
            Name, _device.Id, arguments.Length);

        // Simulate kernel execution time
        var executionTime = CalculateExecutionTime(arguments);
        if (executionTime > TimeSpan.Zero)
        {
            await Task.Delay(executionTime, cancellationToken);
        }

        // Simulate potential execution errors based on device state
        SimulateExecutionConditions();

        _logger.LogTrace("Kernel '{KernelName}' executed successfully in {ExecutionTime}ms",
            Name, executionTime.TotalMilliseconds);
    }

    private TimeSpan CalculateExecutionTime(KernelArguments arguments)
    {
        // Base execution time varies by device type
        var baseTimeMs = _device.Type switch
        {
            AcceleratorType.CPU => 10,
            AcceleratorType.CUDA => 2,
            AcceleratorType.Metal => 3,
            _ => 5
        };

        // Scale by number of arguments (rough approximation)
        var argumentMultiplier = Math.Max(1, arguments.Length * 0.1);

        // Scale by device performance (inverse of compute units for simplicity)
        var performanceMultiplier = Math.Max(0.1, 100.0 / _device.ComputeUnits);

        var totalMs = (int)(baseTimeMs * argumentMultiplier * performanceMultiplier);
        return TimeSpan.FromMilliseconds(totalMs);
    }

    private void SimulateExecutionConditions()
    {
        // Simulate memory pressure
        if (_device.AvailableMemory < _device.TotalMemory * 0.1) // Less than 10% memory available
        {
            throw new OutOfMemoryException($"Device {_device.Id} is low on memory");
        }

        // Simulate random execution failures (very low probability)
        if (Random.Shared.NextDouble() < 0.001) // 0.1% chance
        {
            throw new InvalidOperationException("Simulated random kernel execution failure");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _logger.LogTrace("MockCompiledKernel '{KernelName}' disposed", Name);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}