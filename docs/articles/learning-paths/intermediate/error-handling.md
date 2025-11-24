# Error Handling

This module covers debugging GPU code and implementing robust error handling for production applications.

## GPU Error Types

### Synchronous Errors

Detected immediately during API calls:

| Error | Cause | Solution |
|-------|-------|----------|
| `InvalidArgument` | Bad parameter | Validate inputs |
| `OutOfMemory` | Allocation failed | Reduce size, free unused |
| `InvalidDevice` | Device not available | Check device selection |
| `CompilationFailed` | Kernel syntax error | Fix kernel code |

### Asynchronous Errors

Detected during or after kernel execution:

| Error | Cause | Solution |
|-------|-------|----------|
| `LaunchFailed` | Invalid configuration | Check grid/block sizes |
| `IllegalAddress` | Out-of-bounds access | Add bounds checks |
| `IllegalInstruction` | Unsupported operation | Check device capability |
| `AssertFailed` | Kernel assertion | Debug kernel logic |

## Basic Error Handling

### Try-Catch Pattern

```csharp
public async Task<bool> SafeExecuteAsync(float[] data)
{
    try
    {
        using var buffer = _service.CreateBuffer<float>(data.Length);
        await buffer.CopyFromAsync(data);
        await _service.ExecuteKernelAsync(kernel, config, buffer);
        await buffer.CopyToAsync(data);
        return true;
    }
    catch (OutOfMemoryException ex)
    {
        _logger.LogError(ex, "GPU out of memory for {Size} elements", data.Length);
        return false;
    }
    catch (KernelExecutionException ex)
    {
        _logger.LogError(ex, "Kernel execution failed: {Error}", ex.GpuError);
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during GPU computation");
        throw;
    }
}
```

### Error Checking After Operations

```csharp
public async Task ExecuteWithValidation()
{
    await _service.ExecuteKernelAsync(kernel, config, buffer);

    // Synchronize to catch async errors
    var error = await _service.SynchronizeAsync();

    if (error != GpuError.Success)
    {
        throw new KernelExecutionException($"Kernel failed: {error}");
    }
}
```

## Debugging Kernels

### Debug Mode Compilation

```csharp
services.AddDotCompute(options =>
{
    options.Compilation = new CompilationOptions
    {
        GenerateDebugInfo = true,
        EnableLineInfo = true,
        OptimizationLevel = OptimizationLevel.Debug
    };
});
```

### Kernel Assertions

```csharp
[Kernel]
public static void DebugKernel(Span<float> data, int expectedLength)
{
    int idx = Kernel.ThreadId.X;

    // Debug assertion (only in debug builds)
    Kernel.Assert(idx < expectedLength, "Thread index out of bounds");
    Kernel.Assert(data.Length == expectedLength, "Buffer size mismatch");

    if (idx < data.Length)
    {
        float value = data[idx];
        Kernel.Assert(!float.IsNaN(value), "NaN detected in input");

        data[idx] = ProcessValue(value);
    }
}
```

### Printf-Style Debugging

```csharp
[Kernel(EnablePrintf = true)]
public static void DebugWithPrintf(Span<float> data)
{
    int idx = Kernel.ThreadId.X;

    if (idx < 5)  // Limit output
    {
        Kernel.Printf("Thread %d: input = %f\n", idx, data[idx]);
    }

    if (idx < data.Length)
    {
        data[idx] = data[idx] * 2;

        if (idx < 5)
        {
            Kernel.Printf("Thread %d: output = %f\n", idx, data[idx]);
        }
    }
}
```

### CPU Validation Mode

Run kernel on CPU for debugging:

```csharp
#if DEBUG
// Force CPU backend for easier debugging
services.AddDotCompute(options =>
{
    options.RequiredBackend = BackendType.CPU;
});
#else
services.AddDotCompute();  // Use best available
#endif
```

## Production Error Handling

### Retry with Fallback

```csharp
public class ResilientComputeService
{
    private readonly IComputeService _gpuService;
    private readonly IComputeService _cpuService;

    public async Task<float[]> ComputeAsync(float[] input, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await ComputeOnGpuAsync(input);
            }
            catch (OutOfMemoryException) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning("GPU OOM, retrying with smaller batch");
                // Could try smaller batch size here
            }
            catch (KernelExecutionException ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Kernel failed, retrying");
                await Task.Delay(100 * (attempt + 1));  // Backoff
            }
        }

        // Fallback to CPU
        _logger.LogWarning("GPU failed after retries, falling back to CPU");
        return await ComputeOnCpuAsync(input);
    }
}
```

### Resource Cleanup

```csharp
public class ComputePipeline : IAsyncDisposable
{
    private readonly List<IBuffer<float>> _buffers = new();
    private bool _disposed;

    public async Task ProcessAsync(float[] data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ComputePipeline));

        try
        {
            var buffer = _service.CreateBuffer<float>(data.Length);
            _buffers.Add(buffer);

            await buffer.CopyFromAsync(data);
            await _service.ExecuteKernelAsync(kernel, config, buffer);
            await buffer.CopyToAsync(data);
        }
        catch
        {
            // Ensure cleanup on error
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Synchronize to ensure all operations complete
        await _service.SynchronizeAsync();

        // Dispose all buffers
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }
        _buffers.Clear();
    }
}
```

### Health Monitoring

```csharp
public class GpuHealthMonitor
{
    private readonly IComputeService _service;
    private readonly ILogger _logger;

    public async Task<GpuHealth> CheckHealthAsync()
    {
        try
        {
            var backend = _service.ActiveBackend;

            // Check memory
            var freeMemory = backend.GetFreeMemory();
            var totalMemory = backend.GetTotalMemory();
            var memoryUsage = (totalMemory - freeMemory) / (double)totalMemory;

            // Run quick test kernel
            var testPassed = await RunTestKernelAsync();

            // Check temperature (if available)
            var temperature = backend.GetTemperature();

            return new GpuHealth
            {
                IsHealthy = testPassed && memoryUsage < 0.95,
                MemoryUsagePercent = memoryUsage * 100,
                FreeMemoryMB = freeMemory / (1024 * 1024),
                TemperatureCelsius = temperature,
                Status = testPassed ? "OK" : "Test Failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPU health check failed");
            return new GpuHealth
            {
                IsHealthy = false,
                Status = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<bool> RunTestKernelAsync()
    {
        try
        {
            using var buffer = _service.CreateBuffer<float>(1024);
            await _service.ExecuteKernelAsync(
                TestKernels.Identity,
                new KernelConfig { BlockSize = 256, GridSize = 4 },
                buffer);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

## Common Issues and Solutions

### Issue 1: Illegal Memory Access

**Symptom**: Kernel crashes with `CUDA_ERROR_ILLEGAL_ADDRESS`

**Cause**: Out-of-bounds array access

**Solution**:
```csharp
[Kernel]
public static void SafeKernel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;

    // Always check bounds
    if (idx >= data.Length)
        return;

    data[idx] = data[idx] * 2;
}
```

### Issue 2: Timeout/Hang

**Symptom**: Kernel never completes

**Cause**: Infinite loop or deadlock

**Solution**:
```csharp
// Set execution timeout
var timeout = TimeSpan.FromSeconds(30);

using var cts = new CancellationTokenSource(timeout);

try
{
    await _service.ExecuteKernelAsync(kernel, config, buffer)
        .WaitAsync(cts.Token);
}
catch (OperationCanceledException)
{
    _logger.LogError("Kernel execution timed out");
    await _service.ResetDeviceAsync();
    throw new TimeoutException("Kernel execution exceeded time limit");
}
```

### Issue 3: Numerical Instability

**Symptom**: NaN or Inf results

**Solution**:
```csharp
[Kernel]
public static void StableKernel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx >= data.Length) return;

    float value = data[idx];

    // Check for invalid input
    if (float.IsNaN(value) || float.IsInfinity(value))
    {
        data[idx] = 0;  // or handle appropriately
        return;
    }

    // Avoid division by zero
    float denominator = CalculateDenominator(value);
    if (MathF.Abs(denominator) < 1e-10f)
    {
        denominator = 1e-10f;
    }

    data[idx] = value / denominator;
}
```

## Logging Best Practices

```csharp
public class GpuOperationLogger
{
    private readonly ILogger _logger;

    public async Task<T> LoggedOperationAsync<T>(
        string operationName,
        Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Starting GPU operation: {Operation}", operationName);

            var result = await operation();

            _logger.LogDebug(
                "GPU operation completed: {Operation}, Duration: {Duration}ms",
                operationName, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GPU operation failed: {Operation}, Duration: {Duration}ms",
                operationName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

## Exercises

### Exercise 1: Error Recovery

Implement a compute service that automatically retries failed operations with exponential backoff.

### Exercise 2: Debug Kernel

Add assertions and printf debugging to identify a bug in a provided kernel.

### Exercise 3: Health Dashboard

Create a monitoring dashboard that displays GPU health metrics.

## Key Takeaways

1. **Synchronize to catch async errors** - GPU errors may not surface immediately
2. **Add bounds checks to all kernels** - Most crashes come from out-of-bounds access
3. **Use CPU mode for debugging** - Easier to step through and inspect
4. **Implement fallbacks** - Production systems should handle GPU failures
5. **Monitor GPU health** - Catch issues before they cause failures

## Path Complete

Congratulations! You've completed the Intermediate Learning Path.

**What you learned:**
- Memory optimization and pooling
- Kernel performance tuning
- Multi-kernel pipeline design
- Production error handling

**Next steps:**
- [Advanced Path](../advanced/index.md) - Ring Kernels and multi-GPU
- [Performance Guide](../../guides/performance-tuning.md) - Deep dive into optimization
- [Ring Kernels](../../guides/ring-kernels/index.md) - Persistent GPU computation
