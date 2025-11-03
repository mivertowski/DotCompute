# Troubleshooting Guide

Common issues, diagnostic approaches, and solutions for DotCompute applications.

## Quick Diagnostic Steps

When encountering an issue:

1. **Check Exception Message**: Often contains specific error code and context
2. **Review Logs**: Enable logging for detailed information
3. **Verify Installation**: Ensure required backends are available
4. **Test with CPU Backend**: Isolates GPU-specific issues
5. **Run Diagnostics**: Use built-in diagnostic tools

```csharp
// Enable detailed logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// Run diagnostics
var diagnostics = await orchestrator.RunDiagnosticsAsync();
Console.WriteLine(diagnostics.Summary);
```

## Installation and Setup Issues

### Issue: "DotCompute runtime not registered"

**Symptom**:
```
System.InvalidOperationException: Unable to resolve service for type 'DotCompute.Abstractions.IComputeOrchestrator'
```

**Cause**: `AddDotComputeRuntime()` not called

**Solution**:
```csharp
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDotComputeRuntime();  // Add this
    });
```

### Issue: "No suitable backend found"

**Symptom**:
```
DotCompute.Exceptions.NoBackendAvailableException: No suitable backend found for execution
```

**Cause**: No backends available or all backends disabled

**Diagnosis**:
```csharp
var devices = await orchestrator.GetAvailableDevicesAsync();
if (devices.Count == 0)
{
    Console.WriteLine("No devices available");
}
else
{
    foreach (var device in devices)
    {
        Console.WriteLine($"Device: {device.Name}, Type: {device.Type}");
    }
}
```

**Solutions**:

1. **Enable CPU Fallback**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.EnableCpuFallback = true;  // Always have fallback
});
```

2. **Check Backend Installation**:
```bash
# CUDA
nvidia-smi
nvcc --version

# Metal (macOS)
system_profiler SPDisplaysDataType

# OpenCL
clinfo
```

3. **Verify Package References**:
```xml
<PackageReference Include="DotCompute.Core" Version="0.2.0-alpha" />
<PackageReference Include="DotCompute.Backends.CPU" Version="0.2.0-alpha" />
<PackageReference Include="DotCompute.Backends.CUDA" Version="0.2.0-alpha" />
```

### Issue: "CUDA runtime not found"

**Symptom**:
```
DotCompute.Backends.CUDA.CudaException: CUDA runtime library not found
```

**Cause**: CUDA Toolkit not installed or not in PATH

**Solutions**:

1. **Install CUDA Toolkit**:
```bash
# Ubuntu/Debian
sudo apt install nvidia-cuda-toolkit

# Windows
# Download from https://developer.nvidia.com/cuda-downloads

# Verify
nvidia-smi
```

2. **Set CUDA_HOME** (if needed):
```bash
export CUDA_HOME=/usr/local/cuda
export PATH=$CUDA_HOME/bin:$PATH
export LD_LIBRARY_PATH=$CUDA_HOME/lib64:$LD_LIBRARY_PATH
```

3. **Check Compute Capability**:
```csharp
var cudaBackend = orchestrator.GetBackend(BackendType.CUDA);
var capability = await cudaBackend.GetComputeCapabilityAsync();
Console.WriteLine($"Compute Capability: {capability}");

// Minimum: 5.0
if (capability < 5.0)
{
    Console.WriteLine("Warning: Old GPU, performance may be limited");
}
```

## Compilation Issues

### Issue: "Kernel compilation failed"

**Symptom**:
```
DotCompute.Exceptions.KernelCompilationException: Failed to compile kernel 'MyKernel'
  NVRTC: identifier "xyz" is undefined
```

**Cause**: Syntax error in kernel code

**Diagnosis**:
```csharp
try
{
    await orchestrator.ExecuteKernelAsync("MyKernel", params);
}
catch (KernelCompilationException ex)
{
    Console.WriteLine($"Compilation error: {ex.Message}");
    Console.WriteLine($"Compiler output:\n{ex.CompilerOutput}");
}
```

**Common Causes**:

1. **Undefined Variables**:
```csharp
// ❌ Wrong
[Kernel]
public static void MyKernel(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < length)  // 'length' not defined
    {
        output[idx] = input[idx];
    }
}

// ✅ Correct
[Kernel]
public static void MyKernel(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)  // Use output.Length
    {
        output[idx] = input[idx];
    }
}
```

2. **Unsupported Types**:
```csharp
// ❌ Wrong: strings not supported
[Kernel]
public static void MyKernel(string text)  // Error!

// ✅ Correct: use supported types
[Kernel]
public static void MyKernel(ReadOnlySpan<byte> text)
```

3. **Missing Bounds Check**:
```csharp
// ❌ Diagnostic DC006: Missing bounds check
[Kernel]
public static void MyKernel(Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    output[idx] = 0;  // May be out of bounds!
}

// ✅ Correct
[Kernel]
public static void MyKernel(Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = 0;
    }
}
```

### Issue: Analyzer Warnings

**Symptom**: IDE shows diagnostic warnings DC001-DC012

**Solutions**: Use automated code fixes (Ctrl+. in Visual Studio)

| Diagnostic | Issue | Auto-Fix |
|------------|-------|----------|
| DC001 | Method should be kernel | Add `[Kernel]` attribute |
| DC002 | Wrong return type | Change to `void` |
| DC003 | Instance method | Change to `static` |
| DC004 | Unsupported parameter type | Change to `Span<T>` or scalar |
| DC005 | Write to ReadOnlySpan | Change to `Span<T>` |
| DC006 | Missing bounds check | Add `if (idx < length)` |
| DC007 | Non-linear array access | Simplify indexing |
| DC008 | Async in kernel | Remove `async`/`await` |
| DC009 | Thread ID not used | Add thread indexing |
| DC010 | Incorrect threading pattern | Fix thread access |
| DC011 | Performance issue | Apply suggested optimization |
| DC012 | Missing documentation | Generate XML doc |

## Runtime Errors

### Issue: "Index out of range"

**Symptom**:
```
System.IndexOutOfRangeException: Index was outside the bounds of the array
  at MyKernel execution
```

**Cause**: Thread index exceeds buffer size

**Diagnosis**:
```csharp
// Enable debug validation
services.AddDotComputeRuntime()
    .AddDevelopmentDebugging();

// Run kernel
await orchestrator.ExecuteKernelAsync("MyKernel", params);
// Debug service will report: "Thread 1024 accessed index 1000 in buffer of size 1000"
```

**Solution**: Always bounds-check
```csharp
[Kernel]
public static void MyKernel(Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)  // Critical!
    {
        output[idx] = 0;
    }
}
```

### Issue: "Incorrect results"

**Symptom**: Kernel produces wrong output

**Diagnosis**: Use cross-backend validation
```csharp
services.AddDotComputeRuntime()
    .AddDevelopmentDebugging();

var result = await orchestrator.ExecuteKernelAsync("MyKernel", params);

// Automatically validates against CPU backend
// Reports differences if found
```

**Common Causes**:

1. **Race Conditions**:
```csharp
// ❌ Wrong: Data race
[Kernel]
public static void SumReduction(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[0] += data[idx];  // Race! Multiple threads write to data[0]
    }
}

// ✅ Correct: Use atomic operations or proper reduction
[Kernel]
public static void SumReduction(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < input.Length)
    {
        // Each thread writes to unique location
        output[idx] = input[idx];
    }
    // Separate reduction step needed
}
```

2. **Floating-Point Precision**:
```csharp
// Different backends may have different precision
var cpuResult = await ExecuteOnCpu();    // 3.14159265
var gpuResult = await ExecuteOnGpu();    // 3.14159274 (slightly different)

// Solution: Use tolerance in comparisons
Assert.Equal(cpuResult, gpuResult, precision: 5);  // Compare to 5 decimal places
```

3. **Non-Deterministic Execution**:
```csharp
// Test determinism
services.AddDevelopmentDebugging();

var results = new List<float[]>();
for (int i = 0; i < 10; i++)
{
    var result = new float[1000];
    await orchestrator.ExecuteKernelAsync("MyKernel", new { output = result });
    results.Add(result);
}

// Check if all runs produce same result
var determinismResult = await debugService.TestDeterminismAsync("MyKernel", params);
if (!determinismResult.IsDeterministic)
{
    Console.WriteLine("Warning: Kernel has non-deterministic behavior");
}
```

### Issue: "Intermittent failures"

**Symptom**: Kernel sometimes succeeds, sometimes fails

**Causes and Solutions**:

1. **Synchronization Issues**:
```csharp
// ✅ Always synchronize between dependent kernels
await orchestrator.ExecuteKernelAsync("Kernel1", params1);
await orchestrator.SynchronizeDeviceAsync();  // Critical!
await orchestrator.ExecuteKernelAsync("Kernel2", params2);
```

2. **Memory Not Initialized**:
```csharp
// ❌ Wrong: Uninitialized memory
var buffer = await memoryManager.AllocateAsync<float>(1000);
await orchestrator.ExecuteKernelAsync("Kernel", new { data = buffer });
// May contain garbage values

// ✅ Correct: Initialize memory
var buffer = await memoryManager.AllocateAsync<float>(1000);
await buffer.FillAsync(0.0f);  // Clear to zero
await orchestrator.ExecuteKernelAsync("Kernel", new { data = buffer });
```

3. **Resource Contention**:
```csharp
// ✅ Limit concurrent executions
var semaphore = new SemaphoreSlim(maxConcurrency: 4);

await semaphore.WaitAsync();
try
{
    await orchestrator.ExecuteKernelAsync("Kernel", params);
}
finally
{
    semaphore.Release();
}
```

## Memory Issues

### Issue: "Out of memory"

**Symptom**:
```
DotCompute.Exceptions.OutOfMemoryException: Failed to allocate 4096 MB on device 0
```

**Diagnosis**:
```csharp
var available = await memoryManager.GetAvailableMemoryAsync(deviceId: 0);
var total = await memoryManager.GetTotalMemoryAsync(deviceId: 0);
Console.WriteLine($"Available: {available / (1024 * 1024)} MB");
Console.WriteLine($"Total: {total / (1024 * 1024)} MB");
Console.WriteLine($"Used: {(total - available) / (1024 * 1024)} MB");
```

**Solutions**:

1. **Reduce Batch Size**:
```csharp
// Process in chunks
int maxElements = (int)(available / sizeof(float) * 0.8);  // Use 80% of available
int chunkSize = Math.Min(requestedSize, maxElements);

for (int i = 0; i < data.Length; i += chunkSize)
{
    var chunk = data[i..Math.Min(i + chunkSize, data.Length)];
    await ProcessChunkAsync(chunk);
}
```

2. **Enable Memory Pooling**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.MemoryPooling.Enabled = true;
    options.MemoryPooling.TrimInterval = TimeSpan.FromMinutes(1);  // Aggressive trimming
});
```

3. **Dispose Buffers Promptly**:
```csharp
// ✅ Use await using for automatic disposal
await using var buffer = await memoryManager.AllocateAsync<float>(size);
// Disposed immediately after scope exit
```

4. **Use Streaming**:
```csharp
// For datasets larger than GPU memory
var options = new ExecutionOptions
{
    UseStreaming = true,
    ChunkSize = 100_000_000  // 100M elements per chunk
};

await orchestrator.ExecuteKernelAsync("ProcessHugeDataset", params, options);
// Automatically streams data in chunks
```

### Issue: "Memory leak"

**Symptom**: Memory usage grows over time

**Diagnosis**:
```csharp
// Track active allocations
var initialStats = await memoryManager.GetPoolStatisticsAsync();
Console.WriteLine($"Active allocations: {initialStats.ActiveAllocations}");

// Run operations...

var finalStats = await memoryManager.GetPoolStatisticsAsync();
Console.WriteLine($"Active allocations: {finalStats.ActiveAllocations}");

if (finalStats.ActiveAllocations > initialStats.ActiveAllocations + 10)
{
    Console.WriteLine("Warning: Possible memory leak");
}
```

**Common Causes**:

1. **Forgetting to Dispose**:
```csharp
// ❌ Leak
public async Task ProcessData()
{
    var buffer = await memoryManager.AllocateAsync<float>(1000);
    // Forgot to dispose
}

// ✅ No leak
public async Task ProcessData()
{
    await using var buffer = await memoryManager.AllocateAsync<float>(1000);
    // Disposed automatically
}
```

2. **Exception Before Disposal**:
```csharp
// ❌ Leak on exception
var buffer = await memoryManager.AllocateAsync<float>(1000);
// Exception thrown here
await buffer.DisposeAsync();  // Never reached

// ✅ Disposed even on exception
await using var buffer = await memoryManager.AllocateAsync<float>(1000);
// Always disposed
```

## Performance Issues

### Issue: "Slow execution"

**Symptom**: Kernel takes longer than expected

**Diagnosis**:
```csharp
// Enable profiling
await orchestrator.EnableProfilingAsync();

var stopwatch = Stopwatch.StartNew();
await orchestrator.ExecuteKernelAsync("MyKernel", params);
stopwatch.Stop();

var profile = await orchestrator.GetProfileAsync("MyKernel");
Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Compute time: {profile.ComputeTime}ms");
Console.WriteLine($"Transfer time: {profile.TransferTime}ms");
Console.WriteLine($"Overhead: {profile.OverheadTime}ms");
```

**Common Causes**:

1. **Transfer Overhead Dominates**:
```csharp
// If transfer_time > compute_time, problem is data movement

// ✅ Solution: Keep data on GPU
await orchestrator.ExecuteKernelAsync("Kernel1", params1);
// Don't transfer intermediate result to CPU
await orchestrator.ExecuteKernelAsync("Kernel2", params2);  // Reuse GPU data
```

2. **Small Workload**:
```csharp
// GPU not efficient for small data
if (dataSize < 10_000)
{
    // Use CPU instead
    options.PreferredBackend = BackendType.CPU;
}
```

3. **CPU Backend Instead of GPU**:
```csharp
// Check which backend was used
var executionInfo = await orchestrator.GetLastExecutionInfoAsync();
Console.WriteLine($"Backend used: {executionInfo.BackendUsed}");

if (executionInfo.BackendUsed == BackendType.CPU && gpuAvailable)
{
    // Force GPU
    options.PreferredBackend = BackendType.CUDA;
    options.EnableCpuFallback = false;
}
```

4. **Unoptimized Memory Access**:
```csharp
// ❌ Non-coalesced access (slow on GPU)
[Kernel]
public static void Transpose(ReadOnlySpan<float> input, Span<float> output, int width, int height)
{
    int idx = Kernel.ThreadId.X;
    int row = idx / width;
    int col = idx % width;
    output[col * height + row] = input[row * width + col];  // Strided access
}

// ✅ Tiled transpose (faster)
// See performance-tuning.md for optimized version
```

5. **Not Using SIMD on CPU**:
```csharp
// Enable SIMD intrinsics
services.AddDotComputeRuntime(options =>
{
    options.CPU.EnableSIMD = true;  // AVX2/AVX512
    options.CPU.VectorWidth = 8;    // 8 floats per vector (AVX2)
});
```

### Issue: "Poor multi-GPU scaling"

**Symptom**: 2 GPUs only 1.3x faster than 1 GPU

**Diagnosis**:
```csharp
var profile = await orchestrator.GetMultiGpuProfileAsync();
Console.WriteLine($"GPU 0 time: {profile.DeviceTimes[0]}ms");
Console.WriteLine($"GPU 1 time: {profile.DeviceTimes[1]}ms");
Console.WriteLine($"Transfer time: {profile.TransferTime}ms");
Console.WriteLine($"Sync time: {profile.SyncTime}ms");

// If transfer_time + sync_time > 50% of total: overhead problem
```

**Solutions**:

1. **Enable P2P**:
```csharp
if (await orchestrator.CanEnablePeerAccessAsync(0, 1))
{
    await memoryManager.EnablePeerAccessAsync(0, 1);
    // Direct GPU-GPU transfers (2x faster)
}
```

2. **Minimize Synchronization**:
```csharp
// ❌ Sync after every kernel
for (int i = 0; i < 100; i++)
{
    await orchestrator.ExecuteKernelAsync("Kernel", params);
    await orchestrator.SynchronizeAllDevicesAsync();  // Overhead!
}

// ✅ Batch and sync once
var tasks = new Task[100];
for (int i = 0; i < 100; i++)
{
    tasks[i] = orchestrator.ExecuteKernelAsync("Kernel", params);
}
await Task.WhenAll(tasks);
await orchestrator.SynchronizeAllDevicesAsync();
```

3. **Use Dynamic Load Balancing**:
```csharp
var options = new ExecutionOptions
{
    LoadBalancingStrategy = LoadBalancingStrategy.Dynamic
};
// Automatically distributes work based on GPU performance
```

## Platform-Specific Issues

### Windows

**Issue**: "CUDA driver version mismatch"

**Solution**:
```bash
# Update NVIDIA driver
# Download from https://www.nvidia.com/Download/index.aspx

# Or use GeForce Experience
```

**Issue**: "Visual Studio can't find DotCompute"

**Solution**:
1. Rebuild solution: Ctrl+Shift+B
2. Clean solution: Build → Clean Solution
3. Delete `bin` and `obj` folders
4. Restore packages: `dotnet restore`

### Linux

**Issue**: "libcuda.so.1: cannot open shared object file"

**Solution**:
```bash
# Install NVIDIA drivers
sudo apt install nvidia-driver-535  # Or latest version

# Add to library path
export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH

# Or system-wide
sudo ldconfig
```

**Issue**: "Permission denied" when accessing GPU

**Solution**:
```bash
# Add user to video group
sudo usermod -a -G video $USER

# Logout and login again
```

### macOS

**Issue**: "Metal backend not available"

**Cause**: Running on Intel Mac or macOS < 11.0

**Solution**:
- Apple Silicon required for Metal backend
- Use CPU backend on Intel Macs

**Issue**: "Kernel compilation slow on first run"

**Cause**: Metal shader compilation and caching

**Solution**:
- First run is slower (compiles and caches)
- Subsequent runs are fast (uses cached shaders)
- This is expected behavior

## Debugging Tools

### Built-in Diagnostics

```csharp
// Run full system diagnostics
var diagnostics = await orchestrator.RunDiagnosticsAsync();

Console.WriteLine($"Status: {diagnostics.Status}");
Console.WriteLine($"Available backends: {string.Join(", ", diagnostics.AvailableBackends)}");
Console.WriteLine($"Memory available: {diagnostics.TotalAvailableMemory / (1024 * 1024)} MB");
Console.WriteLine($"Issues found: {diagnostics.Issues.Count}");

foreach (var issue in diagnostics.Issues)
{
    Console.WriteLine($"  {issue.Severity}: {issue.Message}");
    Console.WriteLine($"  Recommendation: {issue.Recommendation}");
}
```

### Logging

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Trace);  // Verbose logging
});

// Log categories
builder.AddFilter("DotCompute.Core", LogLevel.Debug);
builder.AddFilter("DotCompute.Backends", LogLevel.Trace);
builder.AddFilter("DotCompute.Memory", LogLevel.Information);
```

### Cross-Backend Validation

```csharp
services.AddDotComputeRuntime()
    .AddDevelopmentDebugging();

// Automatically validates GPU results against CPU
var result = await orchestrator.ExecuteKernelAsync("MyKernel", params);

// Check validation result
var validation = await debugService.GetLastValidationResultAsync();
if (!validation.IsValid)
{
    Console.WriteLine($"Validation failed! Max difference: {validation.MaxDifference}");
    Console.WriteLine($"First mismatch at index: {validation.FirstMismatchIndex}");
}
```

### Performance Profiling

```csharp
await orchestrator.EnableProfilingAsync();

// Execute kernels...

var profile = await orchestrator.GetProfileAsync();
Console.WriteLine($"Total executions: {profile.TotalExecutions}");
Console.WriteLine($"Average compute time: {profile.AverageComputeTime}ms");
Console.WriteLine($"Average transfer time: {profile.AverageTransferTime}ms");
Console.WriteLine($"Average overhead: {profile.AverageOverheadTime}ms");

// Detailed breakdown
foreach (var kernel in profile.KernelProfiles)
{
    Console.WriteLine($"Kernel: {kernel.Name}");
    Console.WriteLine($"  Executions: {kernel.ExecutionCount}");
    Console.WriteLine($"  Average time: {kernel.AverageTime}ms");
    Console.WriteLine($"  Backend: {kernel.Backend}");
}
```

## Getting Help

### Collect Diagnostic Information

```csharp
// Generate diagnostic report
var report = await orchestrator.GenerateDiagnosticReportAsync();

// Save to file
await File.WriteAllTextAsync("dotcompute-diagnostic.txt", report);

// Include in bug report:
// - DotCompute version
// - .NET version
// - OS and version
// - GPU model and driver version
// - Diagnostic report
// - Minimal reproduction code
```

### Enable Debug Compilation

```csharp
services.AddDotComputeRuntime(options =>
{
    options.Debug.GenerateDebugInfo = true;  // Include debug symbols
    options.Debug.KeepIntermediateFiles = true;  // Keep generated code
    options.Debug.OutputDirectory = "./debug-output";
});

// Generated CUDA kernels saved to ./debug-output/*.cu
// Can inspect and debug manually
```

### Community Support

- **GitHub Issues**: https://github.com/yourusername/DotCompute/issues
- **Discussions**: https://github.com/yourusername/DotCompute/discussions
- **Documentation**: https://dotcompute.dev/docs

When reporting issues, include:
1. DotCompute version
2. .NET version
3. Operating system
4. GPU model and driver version
5. Diagnostic report
6. Minimal code that reproduces the issue
7. Expected vs actual behavior

## Further Reading

- [Debugging Guide](debugging-guide.md) - Detailed debugging techniques
- [Performance Tuning](performance-tuning.md) - Optimization strategies
- [Memory Management](memory-management.md) - Memory best practices
- [Backend Selection](backend-selection.md) - Choosing optimal backends

---

**Troubleshooting • Diagnostics • Problem Solving • Production Ready**
