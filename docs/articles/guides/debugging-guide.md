# Debugging Guide

This guide provides practical techniques for debugging compute kernels, validating correctness, and troubleshooting common issues.

## Enabling Debug Mode

### Development Profile

Enable comprehensive debugging during development:

```csharp
#if DEBUG
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Development;
    options.ValidateAllExecutions = true;
    options.EnableCrossBackendValidation = true;
    options.ThrowOnValidationFailure = true;
    options.ToleranceThreshold = 1e-5;
});
#endif
```

**Behavior**:
- Validates **every** kernel execution
- Compares GPU results against CPU reference
- Throws exceptions on validation failures
- Detailed error messages
- **Overhead**: 2-5x slower (acceptable in development)

### Testing Profile

Enable selective debugging for CI/CD:

```csharp
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Testing;
    options.SamplingRate = 0.1;  // Validate 10% of executions
    options.ThrowOnValidationFailure = true;
});
```

**Behavior**:
- Validates 10% of executions randomly
- Catches intermittent issues
- **Overhead**: 20-50% slower
- Good for integration tests

### Production Profile

Enable minimal debugging in production:

```csharp
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Production;
    options.ValidateAllExecutions = false;
    options.ThrowOnValidationFailure = false;  // Log only
});
```

**Behavior**:
- Validates only suspicious results (NaN, Inf)
- Logs warnings instead of throwing
- **Overhead**: < 5%
- Safe for production use

## Cross-Backend Validation

### Validate GPU Against CPU

The most powerful debugging technique:

```csharp
var debugService = services.GetRequiredService<IKernelDebugService>();

var validation = await debugService.ValidateCrossBackendAsync(
    kernelName: "MyKernel",
    parameters: new { input, output },
    primaryBackend: AcceleratorType.CUDA,    // GPU implementation
    referenceBackend: AcceleratorType.CPU     // Trusted reference
);

if (!validation.IsValid)
{
    Console.WriteLine($"❌ Validation FAILED");
    Console.WriteLine($"Found {validation.Differences.Count} differences");
    Console.WriteLine($"Severity: {validation.Severity}");
    Console.WriteLine($"Recommendation: {validation.Recommendation}");

    // Print first 10 differences
    foreach (var diff in validation.Differences.Take(10))
    {
        Console.WriteLine(
            $"  Index {diff.Index}: " +
            $"GPU={diff.PrimaryValue:F6}, " +
            $"CPU={diff.ReferenceValue:F6}, " +
            $"Error={diff.RelativeError:E2}"
        );
    }
}
else
{
    Console.WriteLine($"✅ Validation PASSED");
    Console.WriteLine($"GPU speedup: {validation.Speedup:F2}x");
}
```

### Understanding Validation Results

**Valid Result** (all differences within tolerance):
```
✅ Validation PASSED
GPU speedup: 47.32x
No differences found (tolerance: 1e-5)
```

**Invalid Result** (differences exceed tolerance):
```
❌ Validation FAILED
Found 127 differences
Severity: Medium
Recommendation: Check for race conditions in parallel sections

First 10 differences:
  Index 42: GPU=3.141593, CPU=3.141592, Error=3.18e-07
  Index 108: GPU=2.718282, CPU=2.718281, Error=3.68e-07
  ...
```

### Tolerance Thresholds

```csharp
// Strict (default for testing)
options.ToleranceThreshold = 1e-5;  // 0.001% relative error

// Lenient (for accumulating operations)
options.ToleranceThreshold = 1e-3;  // 0.1% relative error

// Very lenient (for known precision issues)
options.ToleranceThreshold = 1e-2;  // 1% relative error
```

**Rule of Thumb**:
- Simple operations (add, multiply): 1e-5
- Accumulating operations (sum, dot product): 1e-3
- Transcendental functions (sin, exp, log): 1e-4

## Determinism Testing

### Check for Non-Deterministic Results

```csharp
var determinism = await debugService.TestDeterminismAsync(
    kernelName: "MyKernel",
    parameters: new { input, output },
    backend: AcceleratorType.CUDA,
    runs: 100  // Run 100 times with same input
);

if (!determinism.IsDeterministic)
{
    Console.WriteLine($"⚠️ Kernel is NON-DETERMINISTIC!");
    Console.WriteLine($"Found {determinism.Violations.Count} violations");
    Console.WriteLine($"Likely cause: {determinism.Cause}");

    // Show some violations
    foreach (var violation in determinism.Violations.Take(5))
    {
        Console.WriteLine(
            $"  Run {violation.RunIndex}, " +
            $"Index {violation.ElementIndex}: " +
            $"Expected {violation.ExpectedValue}, " +
            $"Got {violation.ActualValue}"
        );
    }
}
else
{
    Console.WriteLine("✅ Kernel is deterministic");
}
```

### Common Non-Determinism Causes

**1. Race Conditions**:
```csharp
// ❌ Race condition: Multiple threads writing same location
[Kernel]
public static void HasRaceCondition(Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    output[0] += idx;  // Race! All threads write to output[0]
}

// ✅ Fixed: Each thread writes unique location
[Kernel]
public static void NoRaceCondition(Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] += idx;  // Each thread has unique index
    }
}
```

**2. Unordered Reduction**:
```csharp
// ❌ Non-deterministic: Floating-point addition is not associative
[Kernel]
public static void UnorderedSum(ReadOnlySpan<float> input, Span<float> partialSums)
{
    int idx = Kernel.ThreadId.X;
    float sum = 0;

    // Different thread scheduling = different accumulation order = different result
    for (int i = idx; i < input.Length; i += Kernel.GridDim.X)
    {
        sum += input[i];
    }

    partialSums[Kernel.BlockId.X] = sum;
}
```

**Solution**: Use Kahan summation or accept small non-determinism

## Common Issues and Solutions

### Issue 1: Wrong Results on GPU

**Symptoms**:
- GPU produces different results than expected
- Cross-backend validation fails
- Results are NaN or Inf

**Debug Steps**:

**Step 1**: Validate against CPU
```csharp
var validation = await debugService.ValidateCrossBackendAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    AcceleratorType.CPU
);
```

**Step 2**: Check for common issues
```csharp
// Check for NaN/Inf
if (result.Any(float.IsNaN))
{
    Console.WriteLine("❌ Result contains NaN");
    // Causes: Division by zero, sqrt of negative, log of negative
}

if (result.Any(float.IsInfinity))
{
    Console.WriteLine("❌ Result contains Infinity");
    // Causes: Overflow, division by zero
}
```

**Step 3**: Validate numerical stability
```csharp
var stability = await debugService.ValidateNumericalStabilityAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA
);

if (!stability.IsStable)
{
    Console.WriteLine($"⚠️ Numerical instability detected");
    Console.WriteLine($"NaN count: {stability.NaNCount}");
    Console.WriteLine($"Inf count: {stability.InfCount}");
    Console.WriteLine($"Overflow count: {stability.OverflowCount}");
}
```

**Common Causes**:
1. Missing bounds check
2. Race condition
3. Uninitialized memory
4. Integer overflow
5. Division by zero

### Issue 2: Slow Performance

**Symptoms**:
- Kernel is slower than expected
- GPU slower than CPU
- Performance varies widely

**Debug Steps**:

**Step 1**: Profile the kernel
```csharp
var profile = await debugService.ProfileKernelAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    iterations: 1000
);

Console.WriteLine($"Average: {profile.AverageTime.TotalMicroseconds:F2}μs");
Console.WriteLine($"Std dev: {profile.StandardDeviation.TotalMicroseconds:F2}μs");
Console.WriteLine($"Min/Max: {profile.MinTime.TotalMicroseconds:F2}μs / {profile.MaxTime.TotalMicroseconds:F2}μs");

// High std dev indicates variable performance
if (profile.StandardDeviation.TotalMilliseconds > profile.AverageTime.TotalMilliseconds * 0.1)
{
    Console.WriteLine("⚠️ High variability in execution time");
}
```

**Step 2**: Analyze memory patterns
```csharp
var memoryReport = await debugService.AnalyzeMemoryPatternsAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA
);

Console.WriteLine($"Sequential access: {memoryReport.SequentialAccessRate:P1}");
Console.WriteLine($"Cache hit rate: {memoryReport.CacheHitRate:P1}");
Console.WriteLine($"Bandwidth utilization: {memoryReport.BandwidthUtilization:P1}");

foreach (var suggestion in memoryReport.Suggestions)
{
    Console.WriteLine($"💡 {suggestion}");
}
```

**Step 3**: Compare backends
```csharp
var cpuTime = await BenchmarkBackend(AcceleratorType.CPU);
var gpuTime = await BenchmarkBackend(AcceleratorType.CUDA);

Console.WriteLine($"CPU: {cpuTime:F2}ms");
Console.WriteLine($"GPU: {gpuTime:F2}ms");

if (gpuTime > cpuTime)
{
    Console.WriteLine("⚠️ GPU is slower than CPU!");
    Console.WriteLine("Possible causes:");
    Console.WriteLine("  - Data too small (< 10,000 elements)");
    Console.WriteLine("  - Memory-bound operation");
    Console.WriteLine("  - Transfer overhead dominates");
}
```

**Common Causes**:
1. Poor memory access pattern
2. Too many branches
3. Low parallelism
4. Small data size
5. Transfer overhead

### Issue 3: Intermittent Failures

**Symptoms**:
- Kernel passes sometimes, fails other times
- Non-deterministic results
- Hard to reproduce

**Debug Steps**:

**Step 1**: Test determinism
```csharp
var determinism = await debugService.TestDeterminismAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    runs: 100
);

if (!determinism.IsDeterministic)
{
    Console.WriteLine($"❌ Non-deterministic (cause: {determinism.Cause})");
}
```

**Step 2**: Stress test
```csharp
var stressTest = await debugService.StressTestKernelAsync(
    "MyKernel",
    inputGenerator: new RandomInputGenerator(),
    backend: AcceleratorType.CUDA,
    iterations: 10_000
);

Console.WriteLine($"Success rate: {stressTest.SuccessRate:P1}");
Console.WriteLine($"Failures: {stressTest.FailureCount}");

if (stressTest.FailureCount > 0)
{
    Console.WriteLine("Sample failures:");
    foreach (var failure in stressTest.Failures.Take(5))
    {
        Console.WriteLine($"  Input: {failure.Input}");
        Console.WriteLine($"  Error: {failure.Error}");
    }
}
```

**Step 3**: Detect race conditions
```csharp
var raceReport = await debugService.DetectRaceConditionsAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    concurrentExecutions: 100
);

if (raceReport.HasRaceConditions)
{
    Console.WriteLine($"❌ Race conditions detected");
    Console.WriteLine($"Conflicts: {raceReport.ConflictCount}");

    foreach (var conflict in raceReport.Conflicts.Take(5))
    {
        Console.WriteLine($"  Location: {conflict.MemoryLocation}");
        Console.WriteLine($"  Threads: {string.Join(", ", conflict.ConflictingThreads)}");
    }
}
```

**Common Causes**:
1. Race conditions
2. Unordered reduction
3. Thread-unsafe operations
4. Shared memory conflicts

### Issue 4: Out of Memory

**Symptoms**:
- `OutOfMemoryException` thrown
- Kernel fails to allocate buffers
- System becomes unresponsive

**Debug Steps**:

**Step 1**: Check memory usage
```csharp
var memoryStats = memoryManager.GetStatistics();

Console.WriteLine($"Total allocated: {memoryStats.TotalAllocated / 1024 / 1024:F2} MB");
Console.WriteLine($"Total pooled: {memoryStats.TotalPooled / 1024 / 1024:F2} MB");
Console.WriteLine($"Active buffers: {memoryStats.ActiveBuffers}");
Console.WriteLine($"Peak usage: {memoryStats.PeakUsage / 1024 / 1024:F2} MB");
Console.WriteLine($"Pool hit rate: {memoryStats.HitRate:P1}");
```

**Step 2**: Check GPU memory
```csharp
var accelerator = await acceleratorManager.GetOrCreateAcceleratorAsync(AcceleratorType.CUDA);
var deviceStats = accelerator.GetMemoryStatistics();

Console.WriteLine($"Total GPU memory: {deviceStats.TotalMemory / 1024 / 1024:F2} MB");
Console.WriteLine($"Used GPU memory: {deviceStats.UsedMemory / 1024 / 1024:F2} MB");
Console.WriteLine($"Free GPU memory: {deviceStats.FreeMemory / 1024 / 1024:F2} MB");

if (deviceStats.FreeMemory < 100 * 1024 * 1024)  // < 100 MB
{
    Console.WriteLine("⚠️ Low GPU memory!");
}
```

**Step 3**: Find memory leaks
```csharp
// Track allocations
var initialActiveBuffers = memoryStats.ActiveBuffers;

// Run kernel
await orchestrator.ExecuteKernelAsync("MyKernel", parameters);

// Force GC
GC.Collect();
GC.WaitForPendingFinalizers();

var finalActiveBuffers = memoryManager.GetStatistics().ActiveBuffers;

if (finalActiveBuffers > initialActiveBuffers)
{
    Console.WriteLine($"⚠️ Memory leak detected!");
    Console.WriteLine($"Leaked buffers: {finalActiveBuffers - initialActiveBuffers}");
}
```

**Solutions**:
1. Use `using` statements for buffers
2. Return buffers to pool
3. Reduce batch size
4. Use streaming for large data

## Debugging Tools

### Print Debugging (CPU Only)

```csharp
[Kernel]
public static void DebugPrint(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;

    // Only works on CPU backend
    if (idx < 10)  // Print first 10 threads
    {
        Console.WriteLine($"Thread {idx}: input={input[idx]}");
    }

    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2;
    }
}

// Force CPU execution for debugging
await orchestrator.ExecuteKernelAsync(
    "DebugPrint",
    parameters,
    forceBackend: AcceleratorType.CPU
);
```

**Note**: `Console.WriteLine` only works on CPU backend

### Golden Reference Testing

```csharp
// Create known-good output
var goldenOutput = ComputeExpectedOutput(input);

// Test kernel against golden reference
var validation = await debugService.ValidateAgainstGoldenAsync(
    "MyKernel",
    parameters: new { input },
    expectedOutput: goldenOutput,
    backend: AcceleratorType.CUDA
);

if (!validation.IsValid)
{
    Console.WriteLine($"❌ Failed to match golden reference");
    Console.WriteLine($"Differences: {validation.Differences.Count}");
}
```

### Regression Testing

```csharp
[Fact]
public async Task MyKernel_ProducesSameResultsAsPreviousVersion()
{
    // Load results from previous version
    var previousResults = LoadPreviousResults("v0.1.0");

    // Execute current version
    var currentResults = await orchestrator.ExecuteKernelAsync(
        "MyKernel",
        parameters
    );

    // Compare
    Assert.Equal(previousResults, currentResults);
}
```

## IDE Integration

### Visual Studio

**Diagnostic Warnings**:
- DC001-DC012 diagnostics show as error squiggles
- Hover for quick explanation
- Click lightbulb for automated fixes

**Debugging**:
- Set breakpoints in kernel code (CPU only)
- Step through execution
- Watch variables
- Call stack shows kernel invocation

### VS Code

**C# Dev Kit Extension**:
```bash
code --install-extension ms-dotnettools.csdevkit
```

**Features**:
- Same diagnostics as Visual Studio
- Quick fixes via lightbulb
- IntelliSense for generated code

## Logging and Diagnostics

### Enable Detailed Logging

```csharp
services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);

    // Filter to DotCompute only
    logging.AddFilter("DotCompute", LogLevel.Trace);
});
```

### Log Output Example

```
[Trace] DotCompute.Core.KernelExecutionService: Discovering kernel 'VectorAdd'
[Debug] DotCompute.Core.KernelExecutionService: Backend selection: DataSize=4000000, Intensity=Low
[Debug] DotCompute.Core.KernelExecutionService: Selected backend: CPU (rule: small data)
[Trace] DotCompute.Backends.CPU.CpuAccelerator: Compiling kernel 'VectorAdd' (SIMD=AVX2)
[Debug] DotCompute.Memory.UnifiedMemoryManager: Allocated 4.00 MB from pool (hit rate: 92.3%)
[Info] DotCompute.Core.KernelExecutionService: Executed 'VectorAdd' in 2.34ms
```

### Custom Diagnostics

```csharp
public class CustomDiagnostics
{
    private readonly ILogger<CustomDiagnostics> _logger;

    public async Task DiagnoseKernel(string kernelName, object parameters)
    {
        _logger.LogInformation("=== Diagnostics for {Kernel} ===", kernelName);

        // 1. Check kernel exists
        var registry = GetService<IKernelRegistry>();
        var metadata = registry.GetKernel(kernelName);
        if (metadata == null)
        {
            _logger.LogError("❌ Kernel not found: {Kernel}", kernelName);
            return;
        }

        _logger.LogInformation("✅ Kernel found: {Namespace}.{Type}.{Method}",
            metadata.Namespace, metadata.DeclaringType, metadata.Name);

        // 2. Check backend availability
        var manager = GetService<IAcceleratorManager>();
        var availableBackends = manager.GetAvailableBackends();
        _logger.LogInformation("Available backends: {Backends}",
            string.Join(", ", availableBackends));

        // 3. Profile execution
        var profile = await ProfileKernel(kernelName, parameters);
        _logger.LogInformation("Average time: {Time:F2}μs", profile.AverageTime.TotalMicroseconds);

        // 4. Validate correctness
        var validation = await ValidateKernel(kernelName, parameters);
        if (validation.IsValid)
        {
            _logger.LogInformation("✅ Validation passed");
        }
        else
        {
            _logger.LogWarning("⚠️ Validation failed: {Count} differences",
                validation.Differences.Count);
        }

        _logger.LogInformation("=== Diagnostics complete ===");
    }
}
```

## Best Practices

### ✅ Do

1. **Enable debug validation in development** - Catches issues early
2. **Use cross-backend validation** - Most reliable correctness check
3. **Test determinism for critical kernels** - Avoid subtle bugs
4. **Profile before and after optimization** - Verify improvements
5. **Use golden reference tests** - Prevent regressions
6. **Log diagnostic information** - Helps troubleshoot production issues

### ❌ Don't

1. **Don't disable validation in tests** - May miss correctness issues
2. **Don't ignore analyzer warnings** - DC001-DC012 catch real problems
3. **Don't assume GPU is correct** - Validate against CPU
4. **Don't skip stress testing** - Catches intermittent issues
5. **Don't forget to dispose buffers** - Causes memory leaks

## Troubleshooting Checklist

When a kernel misbehaves:

- [ ] Enable debug validation
- [ ] Run cross-backend validation
- [ ] Check for NaN/Inf in results
- [ ] Test determinism (run 100 times)
- [ ] Profile performance (check for anomalies)
- [ ] Analyze memory access patterns
- [ ] Check for race conditions
- [ ] Verify bounds checking
- [ ] Test with small, known inputs
- [ ] Review analyzer warnings (DC001-DC012)
- [ ] Check memory usage (no leaks)
- [ ] Compare CPU vs GPU results

## Further Reading

- [Kernel Development Guide](kernel-development.md) - Writing correct kernels
- [Performance Tuning Guide](performance-tuning.md) - Optimization techniques
- [Architecture: Debugging System](../architecture/debugging-system.md) - Technical details
- [Diagnostic Rules Reference](../reference/diagnostic-rules.md) - DC001-DC012 reference
- [Troubleshooting Guide](troubleshooting.md) - Common issues and solutions

---

**Debug Early • Validate Often • Trust But Verify**
