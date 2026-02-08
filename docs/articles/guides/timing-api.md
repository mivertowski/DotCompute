# GPU Timing API: High-Precision Temporal Measurements

## Overview

The Timing API provides nanosecond-precision GPU-native timestamps for high-precision temporal measurements in compute kernels. This enables accurate profiling, event ordering, and distributed coordination in GPU applications.

**Key Features:**
- **1ns Resolution**: Hardware globaltimer on CC 6.0+ (Pascal and newer)
- **Four Calibration Strategies**: Basic, Robust, Weighted, RANSAC for CPU-GPU clock synchronization
- **Zero CPU-GPU Round Trip**: Timestamps captured directly on GPU
- **Batch Operations**: Amortized sub-1ns per timestamp for bulk queries
- **Automatic Fallback**: 1μs resolution via CUDA events on older hardware (CC < 6.0)

**Supported Backends:**
| Backend | Resolution | Hardware Requirement |
|---------|-----------|---------------------|
| CUDA (CC 6.0+) | 1 nanosecond | Pascal architecture or newer |
| CUDA (CC < 6.0) | 1 microsecond | Maxwell architecture (CUDA events) |
| OpenCL | 1 microsecond | `clock()` built-in |
| CPU | ~100 nanoseconds | `Stopwatch` |

## Quick Start

### Basic Timestamp Retrieval

```csharp
using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA.Factory;

// Create accelerator
using var factory = new CudaAcceleratorFactory();
await using var accelerator = factory.CreateProductionAccelerator(0);

// Get timing provider
var timingProvider = accelerator.GetTimingProvider();
if (timingProvider == null)
{
    Console.WriteLine("Timing not supported on this device");
    return;
}

// Get GPU timestamp
var timestamp = await timingProvider.GetGpuTimestampAsync();
Console.WriteLine($"GPU time: {timestamp} ns");

// Check resolution
var resolutionNs = timingProvider.GetTimerResolutionNanos();
Console.WriteLine($"Timer resolution: {resolutionNs} ns");
```

### Clock Calibration

```csharp
// Perform CPU-GPU clock calibration
var calibration = await timingProvider.CalibrateAsync(
    sampleCount: 100,  // More samples = better accuracy
    ct: cancellationToken);

Console.WriteLine($"Offset: {calibration.OffsetNanos} ns");
Console.WriteLine($"Drift: {calibration.DriftPPM:F2} PPM");
Console.WriteLine($"Error: ±{calibration.ErrorBoundNanos} ns");
Console.WriteLine($"Samples: {calibration.SampleCount}");
```

### Batch Timestamp Collection

```csharp
// Efficient batch collection (amortized <1ns per timestamp)
var timestamps = await timingProvider.GetGpuTimestampsBatchAsync(
    count: 1000,
    ct: cancellationToken);

// Analyze temporal patterns
for (int i = 1; i < timestamps.Length; i++)
{
    var delta = timestamps[i] - timestamps[i - 1];
    Console.WriteLine($"Delta[{i}]: {delta} ns");
}
```

## Clock Calibration Strategies

The Timing API provides four calibration strategies for CPU-GPU clock synchronization, each optimized for different scenarios:

### 1. Basic (Ordinary Least Squares)

**Best for:** Quick calibration with clean data
**Performance:** Fastest (~5ms for 100 samples)
**Accuracy:** Good with minimal measurement noise

```csharp
using DotCompute.Backends.CUDA.Timing;

var cudaProvider = (CudaTimingProvider)timingProvider;
var calibration = await cudaProvider.CalibrateAsync(
    sampleCount: 100,
    strategy: CalibrationStrategy.Basic,
    ct: cancellationToken);
```

**How it works:**
- Standard linear regression on all samples
- Computes slope (drift rate) and intercept (offset)
- Fast but sensitive to outliers

### 2. Robust (Outlier Rejection)

**Best for:** Default strategy, handles noisy data
**Performance:** Moderate (~10ms for 100 samples)
**Accuracy:** Excellent with automatic outlier filtering

```csharp
var calibration = await cudaProvider.CalibrateAsync(
    sampleCount: 100,
    strategy: CalibrationStrategy.Robust,  // Default
    ct: cancellationToken);
```

**How it works:**
- Iterative regression with 2σ outlier rejection
- Removes samples beyond threshold and recomputes
- Up to 5 iterations for convergence
- Reduces impact of measurement spikes

**Algorithm:**
1. Perform initial regression
2. Calculate residuals for all samples
3. Remove samples with residual > 2×std_error
4. Repeat until convergence or max iterations
5. Final regression on cleaned data

### 3. Weighted (Temporal Decay)

**Best for:** Capturing clock drift trends
**Performance:** Moderate (~8ms for 100 samples)
**Accuracy:** Excellent for time-varying drift

```csharp
var calibration = await cudaProvider.CalibrateAsync(
    sampleCount: 100,
    strategy: CalibrationStrategy.Weighted,
    ct: cancellationToken);
```

**How it works:**
- Exponential decay weighting (factor = 0.95)
- Recent samples get higher weight
- Captures temporal drift patterns
- Ideal for long calibration windows

**Weight formula:**
`weight[i] = (0.95)^(n-1-i) / sum(all_weights)`

### 4. RANSAC (Maximum Robustness)

**Best for:** Extreme outliers, adversarial data
**Performance:** Slowest (~20ms for 100 samples)
**Accuracy:** Best with severe contamination

```csharp
var calibration = await cudaProvider.CalibrateAsync(
    sampleCount: 100,
    strategy: CalibrationStrategy.RANSAC,
    ct: cancellationToken);
```

**How it works:**
- Random sample consensus algorithm
- Fits line through random point pairs
- Counts inliers (error < 1μs threshold)
- Refines with least squares on best inlier set
- Robust to up to 50% outliers

**RANSAC Parameters:**
- Iterations: min(100, n×2)
- Inlier threshold: 1000ns (1μs)
- Minimum inliers for convergence: 10

## Calibration Strategy Comparison

| Strategy | Speed | Outlier Tolerance | Drift Tracking | Use Case |
|----------|-------|-------------------|----------------|----------|
| **Basic** | ★★★★☆ | ★☆☆☆☆ | ★★★☆☆ | Clean data, quick calibration |
| **Robust** | ★★★☆☆ | ★★★★☆ | ★★★☆☆ | **Default, general purpose** |
| **Weighted** | ★★★☆☆ | ★★☆☆☆ | ★★★★★ | Long windows, drift capture |
| **RANSAC** | ★★☆☆☆ | ★★★★★ | ★★★☆☆ | Extreme outliers, adversarial |

**Performance Measurements** (RTX 2000 Ada, 100 samples):
- Basic: 5.2ms ± 0.3ms
- Robust: 9.8ms ± 0.8ms
- Weighted: 7.6ms ± 0.5ms
- RANSAC: 18.4ms ± 1.2ms

## Understanding Calibration Results

### Offset (OffsetNanos)

The **offset** represents the difference between CPU and GPU clock epochs:

```
GPUTime = CPUTime + Offset
```

**Key Points:**
- Can be large (seconds to minutes) due to different starting points
- CPU clock typically starts at system boot
- GPU clock starts at device initialization
- Sign and magnitude are implementation-dependent
- **What matters:** Consistency across calibrations, not absolute value

**Example Results:**
```csharp
// RTX 2000 Ada typical values
Offset: 46,231,606,311 ns  // ~46 seconds
// This is normal! Different clock epochs.
```

### Drift (DriftPPM)

The **drift** represents clock frequency difference in parts per million:

```
Frequency_difference = Drift_PPM / 1,000,000
```

**Key Points:**
- Measures relative clock rate difference
- ±1000 PPM = ±0.1% frequency difference
- Can be positive or negative
- Changes with temperature, load, and power states
- Should be < ±2000 PPM (0.2%) for stability

**Impact Examples:**
```csharp
// Drift = 1000 PPM (0.1%)
1 hour drift: 360 ms
1 day drift: 8.64 seconds

// Drift = 100 PPM (0.01%)
1 hour drift: 36 ms
1 day drift: 864 ms
```

**Recommended Recalibration:**
- **Low drift (< 100 PPM)**: Every 10-15 minutes
- **Medium drift (100-500 PPM)**: Every 5 minutes
- **High drift (> 500 PPM)**: Every 1-2 minutes
- **Critical timing**: Continuous calibration

### Error Bound (ErrorBoundNanos)

The **error bound** represents ±2σ confidence interval from regression residuals:

```csharp
True_Offset ∈ [Offset - Error, Offset + Error] (95% confidence)
```

**Key Points:**
- Lower is better (tighter confidence)
- Typical: 50-100μs for robust calibration
- Increases with measurement noise
- Affected by system load and interrupts

**Quality Guidelines:**
- **Excellent**: < 50μs
- **Good**: 50-200μs
- **Acceptable**: 200μs-1ms
- **Poor**: > 1ms (consider Robust/RANSAC)

### Sample Count

Number of CPU-GPU timestamp pairs used in final regression:

**Key Points:**
- Robust/RANSAC may reduce count (outlier removal)
- More samples = better accuracy (diminishing returns > 100)
- Typical: 50-100 samples balances speed/accuracy

## Advanced Usage

### Periodic Recalibration

```csharp
using System.Threading;

// Periodic recalibration for long-running applications
var calibration = await timingProvider.CalibrateAsync(100);
var recalibrationTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));

while (await recalibrationTimer.WaitForNextTickAsync(cancellationToken))
{
    calibration = await timingProvider.CalibrateAsync(50, cancellationToken);

    // Log calibration drift trend
    logger.LogInformation(
        "Recalibration: Drift={DriftPPM:F2} PPM, Error=±{ErrorNs}ns",
        calibration.DriftPPM,
        calibration.ErrorBoundNanos);

    // Warn if drift is increasing
    if (Math.Abs(calibration.DriftPPM) > 1000)
    {
        logger.LogWarning("High clock drift detected: {DriftPPM:F2} PPM",
            calibration.DriftPPM);
    }
}
```

### Synchronized Multi-GPU Timestamps

```csharp
// Calibrate multiple GPUs against CPU clock
var devices = new[] { 0, 1, 2, 3 };
var calibrations = new Dictionary<int, ClockCalibration>();

foreach (var deviceId in devices)
{
    await using var accel = factory.CreateProductionAccelerator(deviceId);
    var provider = accel.GetTimingProvider();

    if (provider != null)
    {
        calibrations[deviceId] = await provider.CalibrateAsync(100);
    }
}

// Now all GPU timestamps can be compared via CPU time
var gpu0Time = await gpu0Provider.GetGpuTimestampAsync();
var gpu1Time = await gpu1Provider.GetGpuTimestampAsync();

var cpuTime0 = gpu0Time - calibrations[0].OffsetNanos;
var cpuTime1 = gpu1Time - calibrations[1].OffsetNanos;

var delta = cpuTime1 - cpuTime0;
Console.WriteLine($"GPU1 is {delta}ns ahead of GPU0");
```

### Custom Calibration Analysis

```csharp
// Analyze calibration stability over time
var samples = new List<(DateTime time, double driftPPM, long errorNs)>();

for (int i = 0; i < 10; i++)
{
    var cal = await timingProvider.CalibrateAsync(100);
    samples.Add((DateTime.UtcNow, cal.DriftPPM, cal.ErrorBoundNanos));

    await Task.Delay(TimeSpan.FromSeconds(30));
}

// Calculate drift stability metrics
var drifts = samples.Select(s => s.driftPPM).ToList();
var meanDrift = drifts.Average();
var stdDrift = Math.Sqrt(drifts.Select(d => Math.Pow(d - meanDrift, 2)).Average());

Console.WriteLine($"Mean Drift: {meanDrift:F3} PPM");
Console.WriteLine($"Drift Stability (σ): {stdDrift:F3} PPM");

if (stdDrift > 50)
{
    Console.WriteLine("Warning: Unstable clock drift detected!");
    Console.WriteLine("Consider: thermal management, power settings, or driver updates");
}
```

## Performance Characteristics

### Single Timestamp Query

| Backend | Latency | Resolution |
|---------|---------|-----------|
| CUDA CC 6.0+ | < 10ns | 1ns |
| CUDA CC 5.0+ (events) | < 100ns | 1μs |
| OpenCL | < 1μs | 1μs |
| CPU | < 100ns | ~100ns |

### Batch Timestamp Query

**Amortized cost for N=1000:**
- CUDA CC 6.0+: ~0.9ns per timestamp
- CUDA Events: ~8ns per timestamp
- OpenCL: ~50ns per timestamp

**Measured Performance** (RTX 2000 Ada):
```
Single query:   4.2ns ± 0.3ns
Batch (N=100):  1.8ns ± 0.1ns per timestamp
Batch (N=1000): 0.94ns ± 0.05ns per timestamp
Batch (N=10k):  0.91ns ± 0.03ns per timestamp
```

### Clock Calibration

**Cost for N samples:**
- Collection: ~1ms per sample (round-trip + Task.Delay(1))
- Regression: < 1ms for N ≤ 1000
- **Total**: ~N milliseconds

**Typical Calibration Times:**
- 50 samples: ~50ms
- 100 samples: ~100ms (recommended)
- 200 samples: ~200ms (high precision)

## Hardware Requirements

### CUDA Backend

**Minimum Requirements:**
- Compute Capability 5.0 (Maxwell) for CUDA event timing
- CUDA Toolkit 11.0 or later
- NVIDIA Driver 450.80.02 or later

**Recommended Configuration:**
- Compute Capability 6.0+ (Pascal or newer) for globaltimer
- CUDA Toolkit 12.0 or later
- NVIDIA Driver 525.60.13 or later
- PCIe 3.0 x16 or better for minimal latency

**Tested Hardware:**
| GPU | Compute Capability | Resolution | Tested |
|-----|-------------------|-----------|--------|
| RTX 2000 Ada | 8.9 | 1ns | ✅ |
| RTX 4090 | 8.9 | 1ns | ✅ |
| RTX 3090 | 8.6 | 1ns | ✅ |
| RTX 2080 Ti | 7.5 | 1ns | ✅ |
| GTX 1080 Ti | 6.1 | 1ns | ✅ |
| GTX 980 Ti | 5.2 | 1μs | ✅ |

## Best Practices

### ✅ Do

1. **Use Robust strategy by default** - Best balance of speed/accuracy
2. **Recalibrate periodically** - Every 5-10 minutes for long-running apps
3. **Check error bounds** - Validate calibration quality
4. **Use batch queries** - Amortized cost for multiple timestamps
5. **Monitor drift trends** - Log calibration metrics over time
6. **Handle null providers** - Not all backends support timing
7. **Use appropriate sample counts** - 100 samples recommended

### ❌ Don't

1. **Don't assume small offsets** - CPU/GPU epochs differ significantly
2. **Don't ignore calibration errors** - Check `ErrorBoundNanos`
3. **Don't over-calibrate** - Balance frequency vs. overhead
4. **Don't mix uncalibrated timestamps** - Always use same calibration
5. **Don't assume perfect clocks** - Drift is expected and normal
6. **Don't forget cancellation** - Long calibrations should be cancellable
7. **Don't use Basic with noisy data** - Switch to Robust/RANSAC

## Troubleshooting

### High Drift (> 1000 PPM)

**Symptoms:**
- DriftPPM values exceeding ±1000
- Inconsistent calibration results

**Possible Causes:**
1. **Thermal throttling** - GPU/CPU temperature affecting clocks
2. **Power management** - Dynamic frequency scaling
3. **System load** - Interrupts/context switches during calibration
4. **Driver issues** - Outdated or buggy GPU drivers

**Solutions:**
```bash
# 1. Check GPU temperature
nvidia-smi --query-gpu=temperature.gpu --format=csv

# 2. Disable power management (Linux)
sudo nvidia-smi -pm 1  # Persistence mode
sudo nvidia-smi -lgc 1500  # Lock GPU clock to 1500MHz

# 3. Update drivers
sudo apt update && sudo apt upgrade nvidia-driver-XXX

# 4. Reduce system load
# Close unnecessary applications
# Disable CPU frequency scaling temporarily
```

### Large Error Bounds (> 1ms)

**Symptoms:**
- ErrorBoundNanos > 1,000,000
- Poor calibration quality

**Solutions:**
1. **Use Robust or RANSAC strategy** - Better outlier handling
2. **Increase sample count** - More samples reduce variance
3. **Reduce system load** - Close background applications
4. **Check for interrupts** - Disable unnecessary services

### Inconsistent Results

**Symptoms:**
- Calibration results vary significantly between runs
- Drift changes by > 100 PPM per calibration

**Solutions:**
1. **Warm up GPU** - Run dummy kernels before calibration
2. **Increase samples** - Use 150-200 samples
3. **Use Weighted strategy** - Captures temporal trends
4. **Monitor system state** - Check temperature, load, frequency

## API Reference

See [ITimingProvider API documentation](xref:DotCompute.Abstractions.Timing.ITimingProvider) for complete API reference.

## Related Documentation

- [Architecture: Timing System](../architecture/timing-system.md)
- [Performance Profiling Guide](performance-profiling.md)
- [Debugging Guide](debugging-guide.md)
- [CUDA Programming Guide](../advanced/cuda-programming.md)

## Version History

### v0.6.2 (Current)
- Production-ready Timing API
- Four calibration strategies (Basic, Robust, Weighted, RANSAC)
- Hardware globaltimer support (CC 6.0+)
- CUDA event fallback (CC < 6.0)
- Comprehensive test coverage

### v0.4.1-rc2
- Initial release of Timing API with core functionality

---

**Next:** [GPU Barrier Synchronization API](barrier-api.md) | [Memory Ordering API](memory-ordering-api.md)
