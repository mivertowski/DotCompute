# Metal Backend Troubleshooting Guide

This guide helps you diagnose and resolve common issues with the DotCompute Metal backend.

---

## Table of Contents

1. [Installation and Setup Issues](#installation-and-setup-issues)
2. [Compilation Errors](#compilation-errors)
3. [Runtime Errors](#runtime-errors)
4. [Performance Problems](#performance-problems)
5. [Memory Issues](#memory-issues)
6. [Debugging Strategies](#debugging-strategies)
7. [Platform-Specific Issues](#platform-specific-issues)
8. [Getting Help](#getting-help)

---

## Installation and Setup Issues

### Error: Metal device not available

**Symptoms:**
```
InvalidOperationException: Failed to create Metal device. Metal may not be available on this system.
```

**Diagnosis:**
```bash
# Check Metal support
system_profiler SPDisplaysDataType | grep Metal

# Check macOS version
sw_vers

# Check for Metal framework
ls -l /System/Library/Frameworks/Metal.framework
```

**Solutions:**

1. **Verify macOS version:**
   ```bash
   # Metal requires macOS 10.13+ (Monterey 12.0+ recommended)
   sw_vers
   ```
   - If macOS < 12.0, upgrade to Monterey or later
   - For optimal performance, use macOS 13.0+ (Ventura)

2. **Check GPU compatibility:**
   ```bash
   # List GPUs
   system_profiler SPDisplaysDataType | grep "Chipset Model"
   ```
   - Apple Silicon (M1/M2/M3): Fully supported
   - Intel Mac 2016+: Supported
   - Earlier Intel Macs: May have limited support

3. **Verify native library:**
   ```bash
   # Check if libDotComputeMetal.dylib exists
   ls -l src/Backends/DotCompute.Backends.Metal/libDotComputeMetal.dylib

   # Check library dependencies
   otool -L libDotComputeMetal.dylib
   ```

   If missing, rebuild:
   ```bash
   cd src/Backends/DotCompute.Backends.Metal/native
   mkdir -p build && cd build
   cmake ..
   make
   ```

---

## Compilation Errors

### Error: MSL compilation failed

**Symptoms:**
```
MetalCompilationException: MSL compilation failed
  Compilation log: error: use of undeclared identifier 'foo'
```

**Common Causes:**

1. **Unsupported C# feature in kernel:**
   ```csharp
   // ❌ WRONG: LINQ not supported in kernels
   [Kernel]
   public static void BadKernel(Span<float> data)
   {
       var sum = data.ToArray().Sum();  // Error!
   }

   // ✅ CORRECT: Use explicit loops
   [Kernel]
   public static void GoodKernel(Span<float> data)
   {
       float sum = 0.0f;
       for (int i = 0; i < data.Length; i++)
           sum += data[i];
   }
   ```

2. **Type mismatch in MSL:**
   ```csharp
   // ❌ WRONG: Unsupported type
   [Kernel]
   public static void BadKernel(Span<decimal> data)  // decimal not supported
   {
       // ...
   }

   // ✅ CORRECT: Use supported types
   [Kernel]
   public static void GoodKernel(Span<float> data)  // float supported
   {
       // ...
   }
   ```

**Solutions:**

1. **Enable debug logging:**
   ```csharp
   services.AddLogging(builder =>
   {
       builder.SetMinimumLevel(LogLevel.Debug);
       builder.AddFilter("DotCompute.Backends.Metal", LogLevel.Trace);
   });
   ```

2. **Review generated MSL:**
   ```csharp
   var translator = new CSharpToMSLTranslator(logger);
   var mslSource = translator.Translate(csharpSource, kernelName, parameters);
   Console.WriteLine("Generated MSL:");
   Console.WriteLine(mslSource);
   ```

3. **Test with Metal CLI:**
   ```bash
   # Save MSL to file
   echo 'kernel void test() {}' > test.metal

   # Compile with metal compiler
   xcrun -sdk macosx metal test.metal -o test.air
   xcrun -sdk macosx metallib test.air -o test.metallib
   ```

### Error: Function not found

**Symptoms:**
```
MetalException: Function 'my_kernel' not found in library
```

**Diagnosis:**
- Entry point name mismatch
- Kernel not marked as `kernel` function in MSL
- Compilation optimization removed function

**Solutions:**

1. **Verify entry point:**
   ```csharp
   var definition = new KernelDefinition
   {
       Name = "vector_add",
       EntryPoint = "vector_add",  // Must match MSL function name
       Source = "kernel void vector_add(...) { }"
   };
   ```

2. **Check MSL function signature:**
   ```metal
   // ✅ CORRECT: kernel qualifier required
   kernel void my_kernel(...) { }

   // ❌ WRONG: Missing kernel qualifier
   void my_kernel(...) { }
   ```

---

## Runtime Errors

### Error: Invalid buffer access

**Symptoms:**
```
MetalException: Buffer access out of bounds
Metal API validation error: Bound buffer is too small
```

**Common Causes:**

1. **Missing bounds checking:**
   ```csharp
   // ❌ WRONG: No bounds check
   [Kernel]
   public static void Unsafe(Span<float> data)
   {
       int idx = Kernel.ThreadId.X;
       data[idx] *= 2.0f;  // May access beyond data.Length!
   }

   // ✅ CORRECT: Always check bounds
   [Kernel]
   public static void Safe(Span<float> data)
   {
       int idx = Kernel.ThreadId.X;
       if (idx < data.Length)
           data[idx] *= 2.0f;
   }
   ```

2. **Incorrect grid size:**
   ```csharp
   // ❌ WRONG: Grid size doesn't match data size
   var gridSize = new MTLSize(1000, 1, 1);
   await kernel.ExecuteAsync(gridSize, threadgroupSize, smallBuffer);  // Buffer only 500 elements!

   // ✅ CORRECT: Match grid size to data
   var gridSize = new MTLSize(buffer.Length, 1, 1);
   await kernel.ExecuteAsync(gridSize, threadgroupSize, buffer);
   ```

**Solutions:**

1. **Enable Metal API validation:**
   ```bash
   export METAL_DEVICE_WRAPPER_TYPE=1
   export METAL_ERROR_MODE=2
   dotnet run
   ```

2. **Add bounds checking everywhere:**
   ```csharp
   [Kernel]
   public static void Robust(Span<float> data, int actualLength)
   {
       int idx = Kernel.ThreadId.X;
       if (idx < actualLength && idx < data.Length)
       {
           data[idx] *= 2.0f;
       }
   }
   ```

### Error: Command buffer execution failed

**Symptoms:**
```
MetalException: Command buffer failed with error: MTLCommandBufferErrorOutOfMemory
```

**Diagnosis:**
```csharp
// Check memory pressure
var metalMemory = (MetalMemoryManager)accelerator.Memory;
var pressure = metalMemory.PressureMonitor.GetCurrentPressureLevel();
Console.WriteLine($"Memory pressure: {pressure}");

// Check available memory
var available = metalMemory.PressureMonitor.GetAvailableMemory();
Console.WriteLine($"Available: {available / (1024*1024)} MB");
```

**Solutions:**

1. **Reduce allocation size:**
   ```csharp
   // Process in chunks instead of all at once
   const int chunkSize = 1_000_000;
   for (int i = 0; i < totalSize; i += chunkSize)
   {
       int currentSize = Math.Min(chunkSize, totalSize - i);
       using var chunk = await allocator.AllocateAsync<float>(currentSize);
       await ProcessChunkAsync(chunk);
   }
   ```

2. **Enable memory pooling:**
   ```csharp
   services.AddDotComputeMetalBackend(options =>
   {
       options.MemoryPoolSizeClasses = 21;
       options.EnableUnifiedMemory = true;
   });
   ```

3. **Monitor and react to memory pressure:**
   ```csharp
   metalMemory.PressureMonitor.PressureChanged += (sender, args) =>
   {
       if (args.Level >= MemoryPressureLevel.High)
       {
           // Trigger cleanup
           await CleanupUnusedBuffersAsync();
       }
   };
   ```

---

## Performance Problems

### Problem: Slower than expected performance

**Symptoms:**
- GPU execution slower than CPU
- Performance degrades over time
- High latency between kernel launches

**Diagnosis:**

1. **Profile kernel execution:**
   ```csharp
   var profiler = ((MetalAccelerator)accelerator).Profiler;
   profiler.StartProfiling("perf_analysis");

   await kernel.ExecuteAsync(...);

   var report = profiler.EndProfiling();
   Console.WriteLine($"GPU time: {report.TotalGpuTimeMs}ms");
   Console.WriteLine($"CPU time: {report.TotalCpuTimeMs}ms");
   ```

2. **Check cache hit rate:**
   ```csharp
   var telemetry = provider.GetService<MetalTelemetryManager>();
   var metrics = telemetry?.GetCurrentMetrics();
   Console.WriteLine($"Cache hit rate: {metrics.CacheHitRate:P}");
   ```

3. **Use Xcode Instruments:**
   ```bash
   # Launch with Metal System Trace
   open -a Instruments
   # Select "Metal System Trace" template
   # Profile your application
   ```

**Solutions:**

1. **Enable unified memory (Apple Silicon):**
   ```csharp
   services.AddDotComputeMetalBackend(options =>
   {
       options.EnableUnifiedMemory = true;  // 2-3x speedup
   });
   ```

2. **Optimize threadgroup sizes:**
   ```csharp
   var options = new CompilationOptions
   {
       EnableAutoTuning = true,  // Automatically selects optimal size
       OptimizationLevel = OptimizationLevel.Maximum
   };
   ```

3. **Use compute graphs for batching:**
   ```csharp
   var graph = new MetalComputeGraph("pipeline", logger);
   var node1 = graph.AddKernelNode(kernel1, ...);
   var node2 = graph.AddKernelNode(kernel2, ..., dependencies: new[] { node1 });
   graph.Build();

   var executor = new MetalGraphExecutor(logger);
   await executor.ExecuteAsync(graph, commandQueue);  // Parallel execution
   ```

4. **Check for memory transfers:**
   ```csharp
   // ❌ BAD: Unnecessary transfers
   var span = buffer.AsSpan();  // CPU access
   await kernel.ExecuteAsync(buffer);  // GPU access - transfer!

   // ✅ GOOD: Batch CPU operations
   var span = buffer.AsSpan();
   for (int i = 0; i < 100; i++)
       span[i] = i;  // All CPU operations
   buffer.EnsureOnDevice();  // Single transfer
   await kernel.ExecuteAsync(buffer);
   ```

### Problem: High compilation times

**Symptoms:**
- First kernel execution takes 50-100ms
- Compilation happens every run
- Low cache hit rate

**Diagnosis:**
```csharp
var telemetry = provider.GetService<MetalTelemetryManager>();
var metrics = telemetry?.GetCurrentMetrics();
Console.WriteLine($"Total compilations: {metrics.TotalCompilations}");
Console.WriteLine($"Cache hit rate: {metrics.CacheHitRate:P}");
```

**Solutions:**

1. **Enable persistent caching:**
   ```csharp
   services.AddDotComputeMetalBackend(options =>
   {
       options.CacheDirectory = "./metal_cache";
       options.MaxCachedKernels = 1000;
   });
   ```

2. **Precompile kernels at startup:**
   ```csharp
   // Warmup phase
   await accelerator.CompileKernelAsync(kernel1Definition);
   await accelerator.CompileKernelAsync(kernel2Definition);

   // Now all kernels are cached
   await kernel1.ExecuteAsync(...);  // <1ms compilation
   ```

---

## Memory Issues

### Problem: Out of memory errors

**Symptoms:**
```
MetalMemoryException: Failed to allocate buffer
  Requested: 2147483648 bytes
  Available: 536870912 bytes
```

**Diagnosis:**
```csharp
var metalMemory = (MetalMemoryManager)accelerator.Memory;
var stats = metalMemory.GetStatistics();
Console.WriteLine($"Total allocated: {stats.TotalBytesAllocated / (1024*1024)} MB");
Console.WriteLine($"Peak usage: {stats.PeakBytesAllocated / (1024*1024)} MB");

var pressure = metalMemory.PressureMonitor.GetCurrentPressureLevel();
Console.WriteLine($"Memory pressure: {pressure}");
```

**Solutions:**

1. **Use memory pooling:**
   ```csharp
   services.AddDotComputeMetalBackend(options =>
   {
       options.MemoryPoolSizeClasses = 21;
   });
   ```

2. **Dispose buffers promptly:**
   ```csharp
   // ❌ BAD: Accumulates memory
   var buffers = new List<IUnifiedMemoryBuffer<float>>();
   for (int i = 0; i < 1000; i++)
   {
       buffers.Add(await allocator.AllocateAsync<float>(1_000_000));
   }

   // ✅ GOOD: Dispose when done
   for (int i = 0; i < 1000; i++)
   {
       using var buffer = await allocator.AllocateAsync<float>(1_000_000);
       await ProcessAsync(buffer);
   }  // Automatic disposal
   ```

3. **Process in chunks:**
   ```csharp
   const int chunkSize = 10_000_000;
   for (int offset = 0; offset < largeData.Length; offset += chunkSize)
   {
       int size = Math.Min(chunkSize, largeData.Length - offset);
       using var chunk = await allocator.AllocateAsync<float>(size);
       largeData.AsSpan(offset, size).CopyTo(chunk.AsSpan());
       await kernel.ExecuteAsync(chunk);
   }
   ```

### Problem: Memory leaks

**Symptoms:**
- Memory usage grows over time
- Application eventually crashes with OOM
- High allocation count with low free count

**Diagnosis:**
```csharp
// Enable memory tracking
var metalMemory = (MetalMemoryManager)accelerator.Memory;
var pool = metalMemory.Pool;

// Check pool statistics
var poolStats = pool.GetStatistics();
Console.WriteLine($"Active buffers: {poolStats.CurrentActiveBuffers}");
Console.WriteLine($"Pooled buffers: {poolStats.CurrentPooledBuffers}");

// Monitor over time
Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        var stats = metalMemory.GetStatistics();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Active: {stats.CurrentActiveBuffers}, " +
                         $"Total: {stats.TotalBytesAllocated / (1024*1024)}MB");
    }
});
```

**Solutions:**

1. **Always use `using` statements:**
   ```csharp
   // ✅ CORRECT: Automatic disposal
   using var buffer = await allocator.AllocateAsync<float>(1000);

   // ❌ WRONG: Manual disposal (easy to forget)
   var buffer = await allocator.AllocateAsync<float>(1000);
   // ... forgot to dispose!
   ```

2. **Dispose accelerator at shutdown:**
   ```csharp
   await using var accelerator = provider.GetRequiredService<IAccelerator>();
   // ... use accelerator
   // Automatic disposal
   ```

3. **Clear kernel cache if needed:**
   ```csharp
   var compiler = (MetalKernelCompiler)accelerator.GetCompiler();
   await compiler.ClearCacheAsync();
   ```

---

## Debugging Strategies

### Enable Comprehensive Logging

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole();
    builder.AddFilter("DotCompute.Backends.Metal", LogLevel.Trace);
    builder.AddFilter("DotCompute.Backends.Metal.Kernels", LogLevel.Debug);
});
```

### Use Debug Markers

```csharp
services.AddDotComputeMetalBackend(options =>
{
    options.EnableDebugMarkers = true;  // Visible in Xcode Instruments
});
```

### Cross-Backend Validation

```csharp
// Automatically validates GPU results against CPU
services.AddProductionDebugging();

var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
var result = await orchestrator.ExecuteAsync<float[]>(
    nameof(MyKernel),
    inputBuffer,
    outputBuffer);
// Throws if results differ between backends
```

### Manual Validation

```csharp
// Execute on GPU
await metalKernel.ExecuteAsync(inputBuffer, gpuResult);

// Execute on CPU for comparison
await cpuKernel.ExecuteAsync(inputBuffer, cpuResult);

// Compare results
var gpuSpan = gpuResult.AsSpan();
var cpuSpan = cpuResult.AsSpan();
for (int i = 0; i < gpuSpan.Length; i++)
{
    if (Math.Abs(gpuSpan[i] - cpuSpan[i]) > 0.0001f)
    {
        Console.WriteLine($"Mismatch at {i}: GPU={gpuSpan[i]}, CPU={cpuSpan[i]}");
    }
}
```

### Use Xcode Instruments

1. **Metal System Trace:**
   ```bash
   # Profile GPU usage, command buffers, memory
   open -a Instruments
   # Select "Metal System Trace"
   ```

2. **Allocations:**
   ```bash
   # Track memory allocations and leaks
   instruments -t Allocations -D trace.trace ./MyApp
   ```

3. **Time Profiler:**
   ```bash
   # Profile CPU usage
   instruments -t "Time Profiler" -D trace.trace ./MyApp
   ```

---

## Platform-Specific Issues

### Apple Silicon (M1/M2/M3)

**Issue: Performance worse than expected**

**Solution:**
- Ensure unified memory is enabled
- Verify running native ARM64 binary, not x64 under Rosetta:
  ```bash
  file MyApp
  # Should show: Mach-O 64-bit executable arm64
  ```

**Issue: Kernel crashes on M3**

**Solution:**
- M3 has 64KB threadgroup memory, may need different sizes
- Check GPU family detection:
  ```csharp
  Console.WriteLine($"GPU Family: {accelerator.Info.GpuFamily}");
  // Should be "Apple9" for M3
  ```

### Intel Mac

**Issue: Slower than Apple Silicon**

**Expected Behavior:**
- Intel Macs use discrete GPUs with explicit memory transfers
- 2-3x slower than Apple Silicon due to PCIe transfers
- Unified memory not available

**Optimization:**
```csharp
services.AddDotComputeMetalBackend(options =>
{
    options.EnableUnifiedMemory = false;  // Discrete GPU
    // Explicit transfers will be used
});
```

---

## Getting Help

### Before Asking for Help

1. **Check logs:**
   ```bash
   dotnet run > output.log 2>&1
   ```

2. **Collect system info:**
   ```bash
   system_profiler SPHardwareDataType SPDisplaysDataType > sysinfo.txt
   sw_vers >> sysinfo.txt
   xcrun metal --version >> sysinfo.txt
   ```

3. **Create minimal repro:**
   ```csharp
   // Simplify to smallest code that reproduces issue
   var accelerator = CreateAccelerator();
   var buffer = await accelerator.Memory.AllocateAsync<float>(1000);
   // Issue occurs here
   ```

### Where to Get Help

1. **GitHub Issues**: [DotCompute/DotCompute/issues](https://github.com/DotCompute/DotCompute/issues)
   - Tag with `backend:metal`
   - Include system info, logs, and repro

2. **GitHub Discussions**: [Discussions](https://github.com/DotCompute/DotCompute/discussions)
   - For questions and general help

3. **Stack Overflow**: Tag with `dotcompute` and `metal`

### Information to Include

```
**System Information:**
- macOS version: 14.2.1 (Sonoma)
- Hardware: MacBook Pro M2 Max
- .NET version: 9.0.0
- DotCompute version: 0.2.0

**Issue Description:**
[Clear description of problem]

**Code Sample:**
```csharp
[Minimal reproduction code]
```

**Logs:**
[Relevant log output with LogLevel.Trace]

**Expected Behavior:**
[What you expected to happen]

**Actual Behavior:**
[What actually happened]
```

---

## Common Error Reference

| Error Code | Description | Solution |
|------------|-------------|----------|
| `DC-METAL-001` | Device not found | Check Metal support, upgrade macOS |
| `DC-METAL-002` | Compilation failed | Review MSL syntax, check logs |
| `DC-METAL-003` | Buffer out of bounds | Add bounds checking in kernel |
| `DC-METAL-004` | Out of memory | Reduce allocations, use pooling |
| `DC-METAL-005` | Command buffer failed | Check memory pressure, reduce workload |
| `DC-METAL-006` | Function not found | Verify entry point name matches MSL |
| `DC-METAL-007` | Invalid threadgroup size | Use auto-tuning or adjust manually |
| `DC-METAL-008` | Native library not found | Rebuild libDotComputeMetal.dylib |

---

## See Also

- [Metal Backend README](../../src/Backends/DotCompute.Backends.Metal/README.md)
- [Getting Started Guide](GETTING_STARTED_METAL.md)
- [API Reference](METAL_API_REFERENCE.md)
- [Architecture Documentation](../ARCHITECTURE.md)
- [Apple Metal Documentation](https://developer.apple.com/documentation/metal)
- [Metal Debugging Guide](https://developer.apple.com/documentation/metal/debugging_tools)

---

**Last Updated:** December 2025
**Version:** v0.2.0
