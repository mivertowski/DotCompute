# Orleans Integration Guide

> **Version**: 0.4.1-rc2
> **Status**: Production Ready
> **Last Updated**: November 2025

## Overview

DotCompute integrates seamlessly with Microsoft Orleans to enable distributed GPU computing with actor-based concurrency. This guide covers GPU accelerator usage in Orleans grains, including device lifecycle management, error recovery, and reset strategies optimized for grain activation/deactivation patterns.

## Table of Contents

- [Why Orleans + DotCompute?](#why-orleans--dotcompute)
- [Quick Start](#quick-start)
- [Grain Lifecycle Integration](#grain-lifecycle-integration)
- [Device Management Strategies](#device-management-strategies)
- [Reset Integration](#reset-integration)
- [Error Recovery Patterns](#error-recovery-patterns)
- [Performance Optimization](#performance-optimization)
- [Advanced Patterns](#advanced-patterns)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Why Orleans + DotCompute?

Combining Orleans and DotCompute provides:

- **Actor-Based GPU Computing**: Encapsulate GPU operations in grains for clean concurrency
- **Automatic Distribution**: Orleans distributes GPU workloads across cluster nodes
- **Fault Tolerance**: Grain lifecycle management with automatic error recovery
- **Resource Isolation**: Each grain can manage its own GPU device or share a pool
- **Elastic Scaling**: Scale GPU compute capacity dynamically
- **State Management**: Persistent grain state with GPU computation results

## Quick Start

### 1. Add Dependencies

```xml
<ItemGroup>
  <PackageReference Include="DotCompute.Core" Version="0.4.0" />
  <PackageReference Include="DotCompute.Backends.CUDA" Version="0.4.0" />
  <PackageReference Include="Microsoft.Orleans.Core" Version="8.0.0" />
  <PackageReference Include="Microsoft.Orleans.Runtime" Version="8.0.0" />
</ItemGroup>
```

### 2. Define Grain Interface

```csharp
using DotCompute.Abstractions;

public interface IComputeGrain : IGrainWithStringKey
{
    Task<double[]> ProcessDataAsync(double[] input);
    Task ResetDeviceAsync();
    Task<DeviceInfo> GetDeviceInfoAsync();
}
```

### 3. Implement Grain

```csharp
using DotCompute.Abstractions;
using DotCompute.Abstractions.Recovery;
using Orleans;

public class ComputeGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;
    private readonly ILogger<ComputeGrain> _logger;

    public ComputeGrain(ILogger<ComputeGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize GPU accelerator on grain activation
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);

        _logger.LogInformation(
            "Grain {GrainId} activated with device {DeviceName}",
            this.GetGrainId(),
            _accelerator.DeviceName);

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<double[]> ProcessDataAsync(double[] input)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Accelerator not initialized");

        try
        {
            // GPU computation here
            var buffer = await _accelerator.AllocateAsync<double>(input.Length);
            await buffer.CopyFromAsync(input);

            // Execute kernel...
            var kernel = await _accelerator.CompileKernelAsync(kernelCode);
            await _accelerator.ExecuteKernelAsync(kernel, buffer);

            var result = new double[input.Length];
            await buffer.CopyToAsync(result);

            await buffer.DisposeAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPU computation failed");

            // Attempt recovery
            await HandleErrorAsync(ex);
            throw;
        }
    }

    public async Task ResetDeviceAsync()
    {
        if (_accelerator == null) return;

        var result = await _accelerator.ResetAsync(
            ResetOptions.Hard,
            CancellationToken.None);

        if (!result.Success)
        {
            _logger.LogError("Device reset failed: {Error}", result.ErrorMessage);
            throw new InvalidOperationException($"Reset failed: {result.ErrorMessage}");
        }

        _logger.LogInformation(
            "Device reset completed: Freed {MemoryMB} MB in {Duration}ms",
            result.MemoryFreedBytes / 1024 / 1024,
            result.Duration.TotalMilliseconds);
    }

    public async Task<DeviceInfo> GetDeviceInfoAsync()
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Accelerator not initialized");

        return new DeviceInfo
        {
            DeviceId = _accelerator.DeviceId,
            DeviceName = _accelerator.DeviceName,
            BackendType = _accelerator.BackendType.ToString(),
            TotalMemory = _accelerator.TotalMemory,
            AvailableMemory = _accelerator.AvailableMemory
        };
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        if (_accelerator != null)
        {
            // Orleans-optimized reset before deactivation
            await _accelerator.ResetAsync(
                ResetOptions.GrainDeactivation,
                cancellationToken);

            await _accelerator.DisposeAsync();
            _accelerator = null;
        }

        _logger.LogInformation(
            "Grain {GrainId} deactivated: {Reason}",
            this.GetGrainId(),
            reason);

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    private async Task HandleErrorAsync(Exception ex)
    {
        if (_accelerator == null) return;

        // Progressive reset based on error type
        var resetType = ex switch
        {
            OutOfMemoryException => ResetType.Hard,
            InvalidOperationException => ResetType.Context,
            _ => ResetType.Soft
        };

        var result = await _accelerator.ResetAsync(
            new ResetOptions { ResetType = resetType },
            CancellationToken.None);

        if (!result.Success)
        {
            _logger.LogError(
                "Error recovery failed: {Error}",
                result.ErrorMessage);
        }
    }
}
```

### 4. Configure Orleans Silo

```csharp
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            .ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(ComputeGrain).Assembly)
                     .WithReferences();
            });
    });

var host = builder.Build();
await host.RunAsync();
```

## Grain Lifecycle Integration

### OnActivateAsync Pattern

```csharp
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    try
    {
        // 1. Initialize accelerator
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);

        // 2. Warm up (optional but recommended)
        await WarmUpAcceleratorAsync(cancellationToken);

        // 3. Load any persistent state
        await ReadStateAsync();

        _logger.LogInformation(
            "Grain activated with GPU {Device}",
            _accelerator.DeviceName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Grain activation failed");
        throw;
    }

    await base.OnActivateAsync(cancellationToken);
}

private async Task WarmUpAcceleratorAsync(CancellationToken cancellationToken)
{
    // Execute a small dummy kernel to initialize GPU context
    var dummyBuffer = await _accelerator!.AllocateAsync<float>(1024);
    await dummyBuffer.DisposeAsync();

    // Soft reset to clean up
    await _accelerator.ResetAsync(ResetOptions.Soft, cancellationToken);
}
```

### OnDeactivateAsync Pattern

```csharp
public override async Task OnDeactivateAsync(
    DeactivationReason reason,
    CancellationToken cancellationToken)
{
    if (_accelerator != null)
    {
        try
        {
            // 1. Save any state
            await WriteStateAsync();

            // 2. Cancel pending operations
            _computeCts?.Cancel();

            // 3. Orleans-optimized reset
            var resetOptions = reason switch
            {
                DeactivationReason.ApplicationShutdown => ResetOptions.CompleteCleanup,
                DeactivationReason.ActivationIdle => ResetOptions.GrainDeactivation,
                _ => ResetOptions.Hard
            };

            var result = await _accelerator.ResetAsync(
                resetOptions,
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Reset during deactivation failed: {Error}",
                    result.ErrorMessage);
            }

            // 4. Dispose accelerator
            await _accelerator.DisposeAsync();
            _accelerator = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during grain deactivation");
        }
    }

    await base.OnDeactivateAsync(reason, cancellationToken);
}
```

## Device Management Strategies

### Strategy 1: One Device Per Grain (Isolated)

**Best for**: Independent compute tasks, fault isolation

```csharp
public class IsolatedComputeGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Each grain gets its own GPU device
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);

        await base.OnActivateAsync(cancellationToken);
    }

    // Rest of implementation...
}
```

**Pros**:
- Complete isolation between grains
- No contention for GPU resources
- Simple error handling

**Cons**:
- Higher memory usage
- Limited by number of GPUs
- Slower activation

### Strategy 2: Shared Device Pool (Efficient)

**Best for**: Many grains, limited GPUs, efficient resource usage

```csharp
public interface IAcceleratorPool
{
    Task<IAccelerator> AcquireAsync(CancellationToken cancellationToken);
    Task ReleaseAsync(IAccelerator accelerator);
}

public class AcceleratorPool : IAcceleratorPool
{
    private readonly ConcurrentBag<IAccelerator> _availableAccelerators;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxSize;

    public AcceleratorPool(int maxSize = 4)
    {
        _maxSize = maxSize;
        _availableAccelerators = new ConcurrentBag<IAccelerator>();
        _semaphore = new SemaphoreSlim(maxSize, maxSize);
    }

    public async Task<IAccelerator> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        if (_availableAccelerators.TryTake(out var accelerator))
        {
            // Soft reset before reuse
            await accelerator.ResetAsync(ResetOptions.Soft, cancellationToken);
            return accelerator;
        }

        // Create new accelerator
        return await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);
    }

    public async Task ReleaseAsync(IAccelerator accelerator)
    {
        try
        {
            // Context reset before returning to pool
            await accelerator.ResetAsync(
                ResetOptions.Context,
                CancellationToken.None);

            _availableAccelerators.Add(accelerator);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public class PooledComputeGrain : Grain, IComputeGrain
{
    private readonly IAcceleratorPool _pool;
    private IAccelerator? _accelerator;

    public PooledComputeGrain(IAcceleratorPool pool)
    {
        _pool = pool;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _accelerator = await _pool.AcquireAsync(cancellationToken);
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        if (_accelerator != null)
        {
            await _pool.ReleaseAsync(_accelerator);
            _accelerator = null;
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}
```

**Pros**:
- Efficient GPU utilization
- Supports many grains
- Fast activation (reuse existing devices)

**Cons**:
- Potential contention
- More complex error handling
- Need to manage pool lifecycle

### Strategy 3: Hybrid (Smart Allocation)

**Best for**: Mixed workloads, varying grain priorities

```csharp
public class SmartComputeGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;
    private readonly IAcceleratorPool? _pool;
    private readonly GrainPriority _priority;
    private bool _isPooled;

    public SmartComputeGrain(
        IAcceleratorPool? pool,
        GrainPriority priority = GrainPriority.Normal)
    {
        _pool = pool;
        _priority = priority;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // High-priority grains get dedicated devices
        if (_priority == GrainPriority.High)
        {
            _accelerator = await AcceleratorFactory.CreateAsync(
                AcceleratorType.CUDA,
                cancellationToken);
            _isPooled = false;
        }
        // Normal priority use pool
        else if (_pool != null)
        {
            _accelerator = await _pool.AcquireAsync(cancellationToken);
            _isPooled = true;
        }
        else
        {
            throw new InvalidOperationException("No pool available for normal priority grain");
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        if (_accelerator != null)
        {
            if (_isPooled && _pool != null)
            {
                await _pool.ReleaseAsync(_accelerator);
            }
            else
            {
                await _accelerator.ResetAsync(
                    ResetOptions.CompleteCleanup,
                    cancellationToken);
                await _accelerator.DisposeAsync();
            }

            _accelerator = null;
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}

public enum GrainPriority
{
    Normal,
    High
}
```

## Reset Integration

### Reset Options for Orleans

DotCompute provides Orleans-specific reset configurations:

```csharp
// Optimized for grain deactivation (default for grain lifecycle)
ResetOptions.GrainDeactivation
{
    ResetType = ResetType.Context,
    WaitForCompletion = true,
    ClearKernelCache = true,
    ClearMemoryPool = false,  // Pool will be reused
    Timeout = TimeSpan.FromSeconds(5)
}

// For error recovery in grains
ResetOptions.ErrorRecovery
{
    ResetType = ResetType.Hard,
    WaitForCompletion = true,
    ClearKernelCache = true,
    ClearMemoryPool = true,
    Timeout = TimeSpan.FromSeconds(10),
    Force = true  // Don't wait for stuck operations
}

// For application shutdown
ResetOptions.CompleteCleanup
{
    ResetType = ResetType.Full,
    WaitForCompletion = true,
    ClearKernelCache = true,
    ClearMemoryPool = true,
    Reinitialize = false,  // Will be disposed anyway
    Timeout = TimeSpan.FromSeconds(30)
}
```

### Periodic Reset Pattern

```csharp
public class LongRunningComputeGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;
    private int _operationCount;
    private const int ResetThreshold = 1000;

    public async Task<ComputeResult> ProcessAsync(ComputeInput input)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Not activated");

        try
        {
            var result = await ExecuteComputeAsync(input);

            _operationCount++;

            // Periodic soft reset to prevent resource buildup
            if (_operationCount % ResetThreshold == 0)
            {
                await _accelerator.ResetAsync(
                    ResetOptions.Soft,
                    CancellationToken.None);

                _logger.LogInformation(
                    "Performed periodic reset after {Count} operations",
                    _operationCount);
            }

            return result;
        }
        catch (Exception ex)
        {
            await HandleComputeErrorAsync(ex);
            throw;
        }
    }
}
```

### Memory Pressure Reset

```csharp
public class MemoryAwareGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;
    private readonly ILogger<MemoryAwareGrain> _logger;

    public async Task<ComputeResult> ProcessLargeDatasetAsync(LargeDataset dataset)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Not activated");

        // Check available memory before operation
        var availableMemory = _accelerator.AvailableMemory;
        var requiredMemory = dataset.EstimatedMemoryUsage;

        if (availableMemory < requiredMemory * 1.2) // 20% safety margin
        {
            _logger.LogWarning(
                "Low memory: {Available} MB available, {Required} MB required",
                availableMemory / 1024 / 1024,
                requiredMemory / 1024 / 1024);

            // Attempt to free memory
            var result = await _accelerator.ResetAsync(
                ResetOptions.Hard,
                CancellationToken.None);

            if (result.Success && result.MemoryFreedBytes > 0)
            {
                _logger.LogInformation(
                    "Freed {Freed} MB",
                    result.MemoryFreedBytes / 1024 / 1024);
            }

            // Recheck
            availableMemory = _accelerator.AvailableMemory;
            if (availableMemory < requiredMemory)
            {
                throw new OutOfMemoryException(
                    $"Insufficient GPU memory: {availableMemory / 1024 / 1024} MB available, " +
                    $"{requiredMemory / 1024 / 1024} MB required");
            }
        }

        return await ExecuteComputeAsync(dataset);
    }
}
```

## Error Recovery Patterns

### Retry with Progressive Reset

```csharp
public class ResilientComputeGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;

    public async Task<ComputeResult> ProcessWithRetryAsync(ComputeInput input)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Not activated");

        var maxAttempts = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await ExecuteComputeAsync(input);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Compute attempt {Attempt} failed, will retry",
                    attempt);

                // Progressive reset escalation
                var resetType = attempt switch
                {
                    1 => ResetType.Context,
                    2 => ResetType.Hard,
                    _ => ResetType.Full
                };

                var resetResult = await _accelerator.ResetAsync(
                    new ResetOptions { ResetType = resetType },
                    CancellationToken.None);

                if (!resetResult.Success)
                {
                    _logger.LogError(
                        "Reset failed: {Error}",
                        resetResult.ErrorMessage);
                    break;
                }

                // Exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)));
            }
        }

        throw new InvalidOperationException(
            $"Compute failed after {maxAttempts} attempts",
            lastException);
    }
}
```

### Circuit Breaker Pattern

```csharp
public class CircuitBreakerGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;
    private int _consecutiveFailures;
    private DateTime _lastFailureTime;
    private bool _circuitOpen;

    private const int FailureThreshold = 3;
    private static readonly TimeSpan CircuitResetTime = TimeSpan.FromMinutes(5);

    public async Task<ComputeResult> ProcessAsync(ComputeInput input)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Not activated");

        // Check circuit breaker
        if (_circuitOpen)
        {
            if (DateTime.UtcNow - _lastFailureTime > CircuitResetTime)
            {
                // Attempt to reset circuit
                await TryResetCircuitAsync();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Circuit breaker open. Retry after {CircuitResetTime}");
            }
        }

        try
        {
            var result = await ExecuteComputeAsync(input);

            // Success - reset failure count
            _consecutiveFailures = 0;
            return result;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _lastFailureTime = DateTime.UtcNow;

            _logger.LogError(
                ex,
                "Compute failed ({Failures}/{Threshold} failures)",
                _consecutiveFailures,
                FailureThreshold);

            // Open circuit if threshold exceeded
            if (_consecutiveFailures >= FailureThreshold)
            {
                _circuitOpen = true;
                _logger.LogCritical(
                    "Circuit breaker opened after {Failures} failures",
                    _consecutiveFailures);

                // Aggressive reset attempt
                await _accelerator.ResetAsync(
                    ResetOptions.ErrorRecovery,
                    CancellationToken.None);
            }

            throw;
        }
    }

    private async Task TryResetCircuitAsync()
    {
        _logger.LogInformation("Attempting to close circuit breaker");

        try
        {
            // Full device reset
            var result = await _accelerator!.ResetAsync(
                ResetOptions.Full,
                CancellationToken.None);

            if (result.Success)
            {
                _circuitOpen = false;
                _consecutiveFailures = 0;
                _logger.LogInformation("Circuit breaker closed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close circuit breaker");
        }
    }
}
```

## Performance Optimization

### Batch Processing with Smart Reset

```csharp
public class BatchProcessingGrain : Grain, IBatchComputeGrain
{
    private IAccelerator? _accelerator;

    public async Task<BatchResult[]> ProcessBatchAsync(
        ComputeInput[] inputs,
        CancellationToken cancellationToken)
    {
        if (_accelerator == null)
            throw new InvalidOperationException("Not activated");

        var results = new BatchResult[inputs.Length];
        var batchSize = 100;

        for (int i = 0; i < inputs.Length; i += batchSize)
        {
            var batch = inputs.Skip(i).Take(batchSize).ToArray();

            // Process batch
            for (int j = 0; j < batch.Length; j++)
            {
                results[i + j] = await ExecuteComputeAsync(batch[j]);
            }

            // Soft reset between batches
            if (i + batchSize < inputs.Length)
            {
                await _accelerator.ResetAsync(
                    ResetOptions.Soft,
                    cancellationToken);
            }
        }

        // Context reset at the end
        await _accelerator.ResetAsync(ResetOptions.Context, cancellationToken);

        return results;
    }
}
```

### Warm-Up and Precompilation

```csharp
public class OptimizedGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;
    private readonly Dictionary<string, IKernel> _precompiledKernels = new();

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);

        // Precompile common kernels
        await PrecompileKernelsAsync(cancellationToken);

        // Warm-up GPU
        await WarmUpAsync(cancellationToken);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task PrecompileKernelsAsync(CancellationToken cancellationToken)
    {
        var kernelSources = GetCommonKernelSources();

        foreach (var (name, source) in kernelSources)
        {
            var kernel = await _accelerator!.CompileKernelAsync(source);
            _precompiledKernels[name] = kernel;
        }

        _logger.LogInformation(
            "Precompiled {Count} kernels during activation",
            _precompiledKernels.Count);
    }

    private async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        // Execute small dummy workload to initialize GPU context
        var dummyBuffer = await _accelerator!.AllocateAsync<float>(1024);

        // Run a simple kernel
        if (_precompiledKernels.TryGetValue("warmup", out var warmupKernel))
        {
            await _accelerator.ExecuteKernelAsync(warmupKernel, dummyBuffer);
        }

        await dummyBuffer.DisposeAsync();

        // Clean up after warm-up
        await _accelerator.ResetAsync(ResetOptions.Soft, cancellationToken);

        _logger.LogInformation("GPU warm-up completed");
    }
}
```

## Advanced Patterns

### Multi-GPU Grain

```csharp
public class MultiGpuGrain : Grain, IMultiGpuComputeGrain
{
    private readonly List<IAccelerator> _accelerators = new();

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize multiple GPUs
        var deviceCount = await AcceleratorFactory.GetDeviceCountAsync(
            AcceleratorType.CUDA);

        for (int i = 0; i < deviceCount; i++)
        {
            var accelerator = await AcceleratorFactory.CreateAsync(
                AcceleratorType.CUDA,
                deviceId: i,
                cancellationToken);

            _accelerators.Add(accelerator);
        }

        _logger.LogInformation(
            "Activated with {Count} GPUs",
            _accelerators.Count);

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<ComputeResult> ProcessParallelAsync(
        ComputeInput[] inputs,
        CancellationToken cancellationToken)
    {
        // Distribute work across GPUs
        var tasks = inputs
            .Select((input, index) => ProcessOnGpuAsync(
                input,
                _accelerators[index % _accelerators.Count],
                cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        return CombineResults(results);
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        // Reset all GPUs in parallel
        var resetTasks = _accelerators.Select(async accelerator =>
        {
            await accelerator.ResetAsync(
                ResetOptions.GrainDeactivation,
                cancellationToken);
            await accelerator.DisposeAsync();
        });

        await Task.WhenAll(resetTasks);
        _accelerators.Clear();

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}
```

### Stateful Grain with Persistent GPU State

```csharp
public interface IStatefulComputeGrainState
{
    byte[] ModelWeights { get; set; }
    Dictionary<string, object> Metadata { get; set; }
}

public class StatefulComputeGrain : Grain, IStatefulComputeGrain
{
    private readonly IPersistentState<IStatefulComputeGrainState> _state;
    private IAccelerator? _accelerator;
    private IUnifiedBuffer<float>? _gpuState;

    public StatefulComputeGrain(
        [PersistentState("compute-state", "StateStore")]
        IPersistentState<IStatefulComputeGrainState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);

        // Restore GPU state from persistent storage
        await RestoreGpuStateAsync(cancellationToken);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task RestoreGpuStateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.ModelWeights?.Length > 0)
        {
            var weights = MemoryMarshal.Cast<byte, float>(_state.State.ModelWeights);
            _gpuState = await _accelerator!.AllocateAsync<float>(weights.Length);
            await _gpuState.CopyFromAsync(weights.ToArray());

            _logger.LogInformation(
                "Restored {Size} MB of GPU state",
                _state.State.ModelWeights.Length / 1024 / 1024);
        }
    }

    public async Task UpdateModelAsync(float[] newWeights)
    {
        if (_accelerator == null || _gpuState == null)
            throw new InvalidOperationException("Not activated");

        // Update GPU state
        await _gpuState.CopyFromAsync(newWeights);

        // Persist to Orleans state
        var bytes = MemoryMarshal.AsBytes(newWeights.AsSpan()).ToArray();
        _state.State.ModelWeights = bytes;
        await _state.WriteStateAsync();

        _logger.LogInformation("Model updated and persisted");
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        // Save GPU state before deactivation
        if (_gpuState != null)
        {
            var weights = new float[_gpuState.Length];
            await _gpuState.CopyToAsync(weights);

            var bytes = MemoryMarshal.AsBytes(weights.AsSpan()).ToArray();
            _state.State.ModelWeights = bytes;
            await _state.WriteStateAsync();

            await _gpuState.DisposeAsync();
        }

        if (_accelerator != null)
        {
            await _accelerator.ResetAsync(
                ResetOptions.GrainDeactivation,
                cancellationToken);
            await _accelerator.DisposeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}
```

## Best Practices

### 1. Always Reset on Deactivation

```csharp
// ✅ Good: Clean deactivation
public override async Task OnDeactivateAsync(
    DeactivationReason reason,
    CancellationToken cancellationToken)
{
    if (_accelerator != null)
    {
        await _accelerator.ResetAsync(
            ResetOptions.GrainDeactivation,
            cancellationToken);
        await _accelerator.DisposeAsync();
    }
    await base.OnDeactivateAsync(reason, cancellationToken);
}

// ❌ Bad: No reset
public override async Task OnDeactivateAsync(
    DeactivationReason reason,
    CancellationToken cancellationToken)
{
    await _accelerator?.DisposeAsync()!;  // Resource leak!
    await base.OnDeactivateAsync(reason, cancellationToken);
}
```

### 2. Use Appropriate Reset Levels

```csharp
// ✅ Good: Match reset to scenario
await _accelerator.ResetAsync(ResetOptions.Soft, ct);           // Between operations
await _accelerator.ResetAsync(ResetOptions.Context, ct);        // Periodic cleanup
await _accelerator.ResetAsync(ResetOptions.Hard, ct);           // Error recovery
await _accelerator.ResetAsync(ResetOptions.GrainDeactivation, ct); // Deactivation

// ❌ Bad: Over-resetting
await _accelerator.ResetAsync(ResetOptions.Full, ct);  // After every operation!
```

### 3. Handle Activation Errors

```csharp
// ✅ Good: Proper error handling
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    try
    {
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize GPU");
        throw;  // Let Orleans handle it
    }

    await base.OnActivateAsync(cancellationToken);
}

// ❌ Bad: Swallow errors
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    try
    {
        _accelerator = await AcceleratorFactory.CreateAsync(
            AcceleratorType.CUDA,
            cancellationToken);
    }
    catch
    {
        // Grain in invalid state!
    }
}
```

### 4. Log Reset Operations

```csharp
// ✅ Good: Comprehensive logging
var result = await _accelerator.ResetAsync(resetOptions, ct);
_logger.LogInformation(
    "Device reset: Type={Type}, Success={Success}, Duration={Duration}ms, Memory={MemoryMB}MB",
    result.ResetType,
    result.Success,
    result.Duration.TotalMilliseconds,
    result.MemoryFreedBytes / 1024 / 1024);

// ❌ Bad: No logging
await _accelerator.ResetAsync(resetOptions, ct);
```

## Troubleshooting

### Grain Activation Fails

**Symptoms**: Grains fail to activate with GPU errors.

**Solutions**:
```csharp
// Add retry logic to activation
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    var maxAttempts = 3;
    for (int i = 0; i < maxAttempts; i++)
    {
        try
        {
            _accelerator = await AcceleratorFactory.CreateAsync(
                AcceleratorType.CUDA,
                cancellationToken);
            break;
        }
        catch (Exception ex) when (i < maxAttempts - 1)
        {
            _logger.LogWarning(ex, "GPU initialization attempt {Attempt} failed", i + 1);
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
        }
    }

    await base.OnActivateAsync(cancellationToken);
}
```

### Memory Leaks in Grains

**Symptoms**: GPU memory usage grows over time.

**Solutions**:
```csharp
// Implement periodic cleanup
private Timer? _cleanupTimer;

public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    _accelerator = await AcceleratorFactory.CreateAsync(
        AcceleratorType.CUDA,
        cancellationToken);

    // Schedule periodic cleanup
    _cleanupTimer = RegisterTimer(
        PeriodicCleanupAsync,
        null,
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    await base.OnActivateAsync(cancellationToken);
}

private async Task PeriodicCleanupAsync(object state)
{
    if (_accelerator != null)
    {
        var result = await _accelerator.ResetAsync(
            ResetOptions.Context,
            CancellationToken.None);

        _logger.LogInformation(
            "Periodic cleanup freed {MemoryMB} MB",
            result.MemoryFreedBytes / 1024 / 1024);
    }
}
```

### Slow Deactivation

**Symptoms**: Grain deactivation takes too long.

**Solutions**:
```csharp
public override async Task OnDeactivateAsync(
    DeactivationReason reason,
    CancellationToken cancellationToken)
{
    if (_accelerator != null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));  // Timeout

        try
        {
            await _accelerator.ResetAsync(
                new ResetOptions
                {
                    ResetType = ResetType.Context,
                    Timeout = TimeSpan.FromSeconds(3),
                    Force = true  // Don't wait if stuck
                },
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Reset timed out during deactivation");
        }

        await _accelerator.DisposeAsync();
    }

    await base.OnDeactivateAsync(reason, cancellationToken);
}
```

## See Also

- [Device Reset API Reference](device-reset.md)
- [Memory Management Guide](memory-management.md)
- [Dependency Injection](dependency-injection.md)
- [Error Handling Best Practices](../advanced/error-handling.md)
- [Orleans Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/)
