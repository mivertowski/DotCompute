# Device Reset and Error Recovery

> **Version**: 0.4.0
> **Status**: Production Ready
> **Last Updated**: November 2025

## Overview

DotCompute provides a comprehensive device reset and error recovery system that allows applications to gracefully handle GPU errors, memory exhaustion, and other hardware-related failures. The reset API supports four different reset levels, from lightweight synchronization to complete device reinitialization.

## Table of Contents

- [Reset Types](#reset-types)
- [Quick Start](#quick-start)
- [Reset Options](#reset-options)
- [Reset Results](#reset-results)
- [Backend-Specific Behavior](#backend-specific-behavior)
- [Error Recovery Patterns](#error-recovery-patterns)
- [Best Practices](#best-practices)
- [Advanced Scenarios](#advanced-scenarios)
- [Troubleshooting](#troubleshooting)

## Reset Types

DotCompute defines four reset levels, each providing different cleanup guarantees:

### Soft Reset (`ResetType.Soft`)

**Purpose**: Lightweight synchronization without resource cleanup.

**What it does**:
- Waits for pending GPU operations to complete
- Synchronizes command queues
- No memory or cache cleanup

**When to use**:
- Between batch processing operations
- Before reading GPU results
- Periodic synchronization points
- Minimal disruption scenarios

**Performance**: < 10ms typical

```csharp
// Soft reset - just synchronize
await accelerator.ResetAsync(ResetOptions.Soft, cancellationToken);
```

### Context Reset (`ResetType.Context`)

**Purpose**: Clear compilation caches and reset context state.

**What it does**:
- Everything from Soft Reset
- Clears kernel compilation cache
- Resets command buffer pools (Metal)
- Clears OpenCL program cache

**When to use**:
- After dynamic kernel compilation
- When switching workload types
- Cache invalidation scenarios
- GPU context refresh

**Performance**: 100-200ms typical

```csharp
// Context reset - clear caches
await accelerator.ResetAsync(ResetOptions.Context, cancellationToken);
```

### Hard Reset (`ResetType.Hard`)

**Purpose**: Full resource cleanup without device reinitialization.

**What it does**:
- Everything from Context Reset
- Clears memory pools
- Frees all cached allocations
- Resets performance counters
- Clears pending operations

**When to use**:
- After memory exhaustion
- Memory leak recovery
- Before major workload changes
- Resource cleanup scenarios

**Performance**: 200-500ms typical

```csharp
// Hard reset - full cleanup
await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);
```

### Full Reset (`ResetType.Full`)

**Purpose**: Complete device reinitialization.

**What it does**:
- Everything from Hard Reset
- Destroys and recreates device context
- Reinitializes GPU driver state
- Resets all hardware counters
- Full device recovery

**When to use**:
- GPU driver errors
- Hardware malfunction recovery
- Critical error scenarios
- Complete state reset required

**Performance**: 500-1000ms typical

```csharp
// Full reset - reinitialize device
await accelerator.ResetAsync(ResetOptions.Full, cancellationToken);
```

## Quick Start

### Basic Reset

```csharp
using DotCompute.Abstractions;
using DotCompute.Abstractions.Recovery;

// Get accelerator
var accelerator = await AcceleratorFactory.CreateAsync(
    AcceleratorType.CUDA,
    cancellationToken);

try
{
    // Your GPU work here
    await accelerator.ExecuteKernelAsync(kernel, cancellationToken);
}
catch (OutOfMemoryException)
{
    // Recover from memory exhaustion
    var result = await accelerator.ResetAsync(
        ResetOptions.Hard,
        cancellationToken);

    if (result.Success)
    {
        Console.WriteLine($"Freed {result.MemoryFreedBytes / 1024 / 1024} MB");
        // Retry operation
    }
}
```

### Orleans Grain Reset

```csharp
public class ComputeGrain : Grain, IComputeGrain
{
    private IAccelerator? _accelerator;

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_accelerator != null)
        {
            // Use Orleans-optimized reset options
            await _accelerator.ResetAsync(
                ResetOptions.GrainDeactivation,
                cancellationToken);

            await _accelerator.DisposeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}
```

## Reset Options

The `ResetOptions` class provides fine-grained control over reset behavior:

### Properties

```csharp
public sealed class ResetOptions
{
    /// <summary>
    /// Type of reset to perform (required)
    /// </summary>
    public ResetType ResetType { get; set; }

    /// <summary>
    /// Wait for pending operations before reset (default: true)
    /// </summary>
    public bool WaitForCompletion { get; set; } = true;

    /// <summary>
    /// Maximum time to wait for reset (default: 30s)
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Clear memory pool during reset (default: false)
    /// </summary>
    public bool ClearMemoryPool { get; set; }

    /// <summary>
    /// Clear kernel compilation cache (default: false)
    /// </summary>
    public bool ClearKernelCache { get; set; }

    /// <summary>
    /// Reinitialize device context (default: false)
    /// </summary>
    public bool Reinitialize { get; set; }

    /// <summary>
    /// Force reset even if operations are pending (default: false)
    /// WARNING: Can cause data loss
    /// </summary>
    public bool Force { get; set; }
}
```

### Predefined Options

DotCompute provides several predefined option sets:

```csharp
// Minimal disruption
var options = ResetOptions.Soft;

// Cache invalidation
var options = ResetOptions.Context;

// Resource cleanup
var options = ResetOptions.Hard;

// Complete reinitialization
var options = ResetOptions.Full;

// Error recovery (aggressive)
var options = ResetOptions.ErrorRecovery;

// Orleans grain deactivation (optimized)
var options = ResetOptions.GrainDeactivation;

// Complete cleanup (for shutdown)
var options = ResetOptions.CompleteCleanup;
```

### Custom Configuration

```csharp
var options = new ResetOptions
{
    ResetType = ResetType.Hard,
    WaitForCompletion = true,
    Timeout = TimeSpan.FromSeconds(10),
    ClearMemoryPool = true,
    ClearKernelCache = true,
    Reinitialize = false,
    Force = false
};

var result = await accelerator.ResetAsync(options, cancellationToken);
```

## Reset Results

The `ResetResult` class provides detailed information about the reset operation:

### Success Result

```csharp
var result = await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);

if (result.Success)
{
    Console.WriteLine($"Device: {result.DeviceName} ({result.DeviceId})");
    Console.WriteLine($"Backend: {result.BackendType}");
    Console.WriteLine($"Reset Type: {result.ResetType}");
    Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
    Console.WriteLine($"Memory Freed: {result.MemoryFreedBytes / 1024 / 1024} MB");
    Console.WriteLine($"Kernels Cleared: {result.KernelsCacheCleared}");
    Console.WriteLine($"Pending Ops: {result.PendingOperationsCleared}");
    Console.WriteLine($"Reinitialized: {result.WasReinitialized}");

    // Backend-specific diagnostics
    foreach (var (key, value) in result.DiagnosticInfo)
    {
        Console.WriteLine($"{key}: {value}");
    }
}
```

### Failure Result

```csharp
var result = await accelerator.ResetAsync(ResetOptions.Full, cancellationToken);

if (!result.Success)
{
    Console.WriteLine($"Reset failed: {result.ErrorMessage}");
    Console.WriteLine($"Error code: {result.ErrorCode}");

    // Detailed diagnostic information
    foreach (var (key, value) in result.DiagnosticInfo)
    {
        Console.WriteLine($"{key}: {value}");
    }
}
```

### Properties Reference

```csharp
public sealed class ResetResult
{
    public bool Success { get; }
    public string DeviceId { get; }
    public string DeviceName { get; }
    public string BackendType { get; }
    public ResetType ResetType { get; }
    public DateTimeOffset Timestamp { get; }
    public TimeSpan Duration { get; }
    public bool WasReinitialized { get; }

    // Resource cleanup metrics
    public long PendingOperationsCleared { get; }
    public long MemoryFreedBytes { get; }
    public int KernelsCacheCleared { get; }
    public bool MemoryPoolCleared { get; }
    public bool KernelCacheCleared { get; }

    // Error information (when Success = false)
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    // Backend-specific diagnostics
    public IReadOnlyDictionary<string, string> DiagnosticInfo { get; }
}
```

## Backend-Specific Behavior

### CUDA Backend

**Soft Reset**:
- `cudaDeviceSynchronize()`
- Minimal overhead

**Context Reset**:
- Clears NVRTC compilation cache
- Resets CUDA module cache
- ~150ms typical

**Hard Reset**:
- Clears memory pool
- Frees cached allocations
- Resets cuBLAS/cuDNN handles
- ~300ms typical

**Full Reset**:
- `cudaDeviceReset()`
- Complete reinitialization
- ~800ms typical

**Diagnostics**:
```csharp
result.DiagnosticInfo["ComputeCapability"] // "8.9"
result.DiagnosticInfo["DriverVersion"]     // "545.29.06"
result.DiagnosticInfo["CudaVersion"]       // "12.3"
result.DiagnosticInfo["MemoryFreed"]       // "512 MB"
```

### OpenCL Backend

**Soft Reset**:
- `clFinish()` on all queues
- Fast synchronization

**Context Reset**:
- Clears program cache
- Releases command pools
- ~120ms typical

**Hard Reset**:
- Clears memory pool
- Releases all buffers
- ~250ms typical

**Full Reset**:
- Recreates OpenCL context
- Reinitializes device
- ~600ms typical

**Diagnostics**:
```csharp
result.DiagnosticInfo["OpenCLVersion"]  // "3.0"
result.DiagnosticInfo["Device"]         // "Intel UHD Graphics"
result.DiagnosticInfo["Vendor"]         // "Intel"
result.DiagnosticInfo["Platform"]       // "Intel OpenCL"
```

### Metal Backend

**Soft Reset**:
- Waits for command buffer completion
- Fast on Apple Silicon

**Context Reset**:
- Clears command buffer pool
- Resets pipeline state cache
- ~100ms typical

**Hard Reset**:
- Clears unified memory allocations
- Resets resource heaps
- ~200ms typical

**Full Reset**:
- Recreates MTLDevice
- Full resource reinitialization
- ~500ms typical

**Diagnostics**:
```csharp
result.DiagnosticInfo["DeviceName"]         // "Apple M1 Pro"
result.DiagnosticInfo["Architecture"]       // "Apple Silicon"
result.DiagnosticInfo["UnifiedMemory"]      // "True"
result.DiagnosticInfo["MaxThreadgroupSize"] // "1024"
```

### CPU Backend

**All Reset Types**:
- Synchronous operations (no pending work)
- Memory pool cleanup only
- < 50ms typical

**Diagnostics**:
```csharp
result.DiagnosticInfo["ProcessorCount"]    // "16"
result.DiagnosticInfo["OSVersion"]         // "Linux 6.6.87.2"
result.DiagnosticInfo["PerformanceMode"]   // "HighPerformance"
result.DiagnosticInfo["Vectorization"]     // "True"
```

## Error Recovery Patterns

### Automatic Retry with Exponential Backoff

```csharp
public async Task<T> ExecuteWithRetryAsync<T>(
    Func<IAccelerator, CancellationToken, Task<T>> operation,
    IAccelerator accelerator,
    CancellationToken cancellationToken)
{
    var maxAttempts = 3;
    var resetType = ResetType.Soft;

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            return await operation(accelerator, cancellationToken);
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            // Escalate reset severity with each retry
            resetType = attempt switch
            {
                1 => ResetType.Context,
                2 => ResetType.Hard,
                _ => ResetType.Full
            };

            var result = await accelerator.ResetAsync(
                new ResetOptions { ResetType = resetType },
                cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Reset failed: {result.ErrorMessage}", ex);
            }

            // Exponential backoff
            await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)));
        }
    }

    throw new InvalidOperationException($"Operation failed after {maxAttempts} attempts");
}
```

### Memory Exhaustion Recovery

```csharp
public async Task HandleOutOfMemoryAsync(
    IAccelerator accelerator,
    CancellationToken cancellationToken)
{
    // Try progressive cleanup
    var resetTypes = new[]
    {
        ResetType.Context,  // First try cache cleanup
        ResetType.Hard,     // Then full memory cleanup
        ResetType.Full      // Last resort: reinitialize
    };

    foreach (var resetType in resetTypes)
    {
        var result = await accelerator.ResetAsync(
            new ResetOptions { ResetType = resetType },
            cancellationToken);

        if (result.Success && result.MemoryFreedBytes > 0)
        {
            Console.WriteLine($"Freed {result.MemoryFreedBytes / 1024 / 1024} MB " +
                            $"using {resetType} reset");
            return;
        }
    }

    throw new OutOfMemoryException("Unable to free GPU memory");
}
```

### Graceful Degradation

```csharp
public async Task<ComputeResult> ExecuteWithFallbackAsync(
    IKernel kernel,
    IAccelerator gpuAccelerator,
    IAccelerator cpuAccelerator,
    CancellationToken cancellationToken)
{
    try
    {
        // Try GPU first
        return await gpuAccelerator.ExecuteKernelAsync(kernel, cancellationToken);
    }
    catch (Exception ex)
    {
        // Log GPU failure
        Console.WriteLine($"GPU execution failed: {ex.Message}");

        // Attempt GPU recovery
        var result = await gpuAccelerator.ResetAsync(
            ResetOptions.Hard,
            cancellationToken);

        if (result.Success)
        {
            // Retry GPU
            try
            {
                return await gpuAccelerator.ExecuteKernelAsync(kernel, cancellationToken);
            }
            catch
            {
                // Fall through to CPU
            }
        }

        // Fallback to CPU
        Console.WriteLine("Falling back to CPU execution");
        return await cpuAccelerator.ExecuteKernelAsync(kernel, cancellationToken);
    }
}
```

## Best Practices

### 1. Choose Appropriate Reset Level

```csharp
// ✅ Good: Match reset level to scenario
await accelerator.ResetAsync(ResetOptions.Soft, cancellationToken);        // Between batches
await accelerator.ResetAsync(ResetOptions.Context, cancellationToken);     // After compilation
await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);        // Memory recovery
await accelerator.ResetAsync(ResetOptions.Full, cancellationToken);        // Driver errors

// ❌ Bad: Over-resetting
await accelerator.ResetAsync(ResetOptions.Full, cancellationToken);  // For every batch!
```

### 2. Always Check Reset Results

```csharp
// ✅ Good: Check success and handle failures
var result = await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);
if (!result.Success)
{
    throw new InvalidOperationException($"Reset failed: {result.ErrorMessage}");
}

// ❌ Bad: Ignore failures
await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);
// Continue without checking...
```

### 3. Use Cancellation Tokens

```csharp
// ✅ Good: Support cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await accelerator.ResetAsync(ResetOptions.Hard, cts.Token);

// ❌ Bad: No timeout
var result = await accelerator.ResetAsync(ResetOptions.Hard, CancellationToken.None);
```

### 4. Log Reset Operations

```csharp
// ✅ Good: Comprehensive logging
var result = await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);
_logger.LogInformation(
    "Device reset completed: Type={ResetType}, Duration={Duration}ms, MemoryFreed={MemoryMB}MB",
    result.ResetType,
    result.Duration.TotalMilliseconds,
    result.MemoryFreedBytes / 1024 / 1024);

// ❌ Bad: No logging
await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);
```

### 5. Reset Before Disposal

```csharp
// ✅ Good: Clean shutdown
await accelerator.ResetAsync(ResetOptions.CompleteCleanup, cancellationToken);
await accelerator.DisposeAsync();

// ❌ Bad: Direct disposal
await accelerator.DisposeAsync();  // May leak resources
```

## Advanced Scenarios

### Custom Timeout Handling

```csharp
var options = new ResetOptions
{
    ResetType = ResetType.Hard,
    Timeout = TimeSpan.FromSeconds(5)  // Custom timeout
};

try
{
    var result = await accelerator.ResetAsync(options, cancellationToken);
}
catch (TimeoutException)
{
    // Force reset if timeout
    options.Force = true;
    options.Timeout = TimeSpan.FromSeconds(30);
    var result = await accelerator.ResetAsync(options, cancellationToken);
}
```

### Multi-GPU Reset Coordination

```csharp
public async Task ResetAllAcceleratorsAsync(
    IEnumerable<IAccelerator> accelerators,
    CancellationToken cancellationToken)
{
    var tasks = accelerators.Select(async accelerator =>
    {
        var result = await accelerator.ResetAsync(
            ResetOptions.Hard,
            cancellationToken);

        return (Accelerator: accelerator, Result: result);
    });

    var results = await Task.WhenAll(tasks);

    foreach (var (accelerator, result) in results)
    {
        if (!result.Success)
        {
            Console.WriteLine($"Reset failed for {accelerator.DeviceName}: " +
                            $"{result.ErrorMessage}");
        }
    }
}
```

### Reset with State Preservation

```csharp
public async Task ResetWithStatePreservationAsync(
    IAccelerator accelerator,
    CancellationToken cancellationToken)
{
    // Save critical state
    var criticalBuffers = await SaveCriticalStateAsync(accelerator);

    try
    {
        // Perform reset
        var result = await accelerator.ResetAsync(
            ResetOptions.Hard,
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Reset failed: {result.ErrorMessage}");
        }

        // Restore state
        await RestoreCriticalStateAsync(accelerator, criticalBuffers);
    }
    finally
    {
        // Cleanup saved state
        foreach (var buffer in criticalBuffers)
        {
            await buffer.DisposeAsync();
        }
    }
}
```

## Troubleshooting

### Reset Hangs or Times Out

**Symptoms**: Reset operation doesn't complete within timeout period.

**Causes**:
- Pending GPU operations stuck
- Driver-level deadlock
- Hardware malfunction

**Solutions**:
```csharp
// 1. Try force reset
var options = new ResetOptions
{
    ResetType = ResetType.Hard,
    Force = true,  // Skip waiting for pending operations
    Timeout = TimeSpan.FromSeconds(60)
};
var result = await accelerator.ResetAsync(options, cancellationToken);

// 2. If that fails, escalate to Full reset
if (!result.Success)
{
    options.ResetType = ResetType.Full;
    result = await accelerator.ResetAsync(options, cancellationToken);
}

// 3. Last resort: recreate accelerator
if (!result.Success)
{
    await accelerator.DisposeAsync();
    accelerator = await AcceleratorFactory.CreateAsync(
        AcceleratorType.CUDA,
        cancellationToken);
}
```

### Reset Doesn't Free Memory

**Symptoms**: `MemoryFreedBytes` is 0 or less than expected.

**Causes**:
- Memory still referenced
- Memory pool not cleared
- Driver caching

**Solutions**:
```csharp
// Ensure all buffers are disposed
await DisposeAllBuffersAsync();

// Use Hard or Full reset
var result = await accelerator.ResetAsync(ResetOptions.Hard, cancellationToken);

// Check for memory leaks
if (result.MemoryFreedBytes < expectedBytes)
{
    // Force GC collection
    GC.Collect();
    GC.WaitForPendingFinalizers();

    // Retry reset
    result = await accelerator.ResetAsync(ResetOptions.Full, cancellationToken);
}
```

### Reset Fails with Driver Error

**Symptoms**: Reset returns `Success = false` with driver error code.

**Causes**:
- GPU driver crash
- Hardware malfunction
- Unsupported operation

**Solutions**:
```csharp
var result = await accelerator.ResetAsync(ResetOptions.Full, cancellationToken);

if (!result.Success && result.ErrorCode != null)
{
    // Check diagnostic information
    Console.WriteLine($"Driver error: {result.ErrorCode}");
    foreach (var (key, value) in result.DiagnosticInfo)
    {
        Console.WriteLine($"{key}: {value}");
    }

    // May require application restart or driver reinstall
    throw new InvalidOperationException(
        $"Unrecoverable GPU error: {result.ErrorMessage}");
}
```

## See Also

- [Orleans Integration Guide](orleans-integration.md)
- [Memory Management Guide](memory-management.md)
- [Multi-GPU Programming](multi-gpu.md)
- [Error Handling Best Practices](../advanced/error-handling.md)
- [Performance Tuning](performance-tuning.md)
