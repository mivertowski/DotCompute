# Health Monitoring API Guide

## Overview

DotCompute provides comprehensive health monitoring capabilities across all compute backends (CUDA, OpenCL, Metal, CPU). The health monitoring system enables real-time tracking of device status, performance metrics, and resource utilization, making it ideal for:

- **Production monitoring**: Track device health in deployed applications
- **Proactive maintenance**: Detect issues before they cause failures
- **Performance optimization**: Identify bottlenecks and resource constraints
- **Orleans integration**: Monitor GPU/CPU resources in actor-based systems
- **Error recovery**: Make informed decisions about when to trigger device resets

## Quick Start

### Basic Health Snapshot

```csharp
using DotCompute.Abstractions.Health;
using DotCompute.Backends.CUDA;

// Create an accelerator
using var accelerator = new CudaAccelerator(deviceId: 0, logger);

// Get health snapshot
var health = await accelerator.GetHealthSnapshotAsync();

Console.WriteLine($"Device: {health.DeviceName}");
Console.WriteLine($"Health Score: {health.HealthScore:P1}");
Console.WriteLine($"Status: {health.Status}");
Console.WriteLine($"Available: {health.IsAvailable}");
Console.WriteLine($"Throttling: {health.IsThrottling}");
```

### Reading Individual Sensors

```csharp
// Get all sensor readings
var readings = await accelerator.GetSensorReadingsAsync();

// Query specific sensors
var temp = health.GetSensorValue(SensorType.Temperature);
var memUsed = health.GetSensorValue(SensorType.MemoryUsedBytes);
var gpuUtil = health.GetSensorValue(SensorType.ComputeUtilization);

if (temp.HasValue)
{
    Console.WriteLine($"GPU Temperature: {temp.Value}°C");
}

// Check sensor availability
if (health.IsSensorAvailable(SensorType.PowerDraw))
{
    var power = health.GetSensorValue(SensorType.PowerDraw)!.Value;
    Console.WriteLine($"Power Draw: {power:F1} W");
}
```

## Core Concepts

### DeviceHealthSnapshot

The `DeviceHealthSnapshot` class represents a point-in-time view of device health:

```csharp
public sealed class DeviceHealthSnapshot
{
    // Device identification
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required string BackendType { get; init; }  // "CUDA", "OpenCL", "Metal", "CPU"

    // Health metrics
    public required DateTimeOffset Timestamp { get; init; }
    public required double HealthScore { get; init; }     // 0.0 to 1.0
    public required DeviceHealthStatus Status { get; init; }
    public required bool IsAvailable { get; init; }
    public required bool IsThrottling { get; init; }

    // Sensor data
    public required IReadOnlyList<SensorReading> SensorReadings { get; init; }

    // Error tracking
    public long ErrorCount { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset? LastErrorTimestamp { get; init; }
    public int ConsecutiveFailures { get; init; }

    // Optional metadata
    public string? StatusMessage { get; init; }
    public IReadOnlyDictionary<string, double>? CustomMetrics { get; init; }
}
```

### Health Status Levels

```csharp
public enum DeviceHealthStatus
{
    Unknown = 0,    // Status cannot be determined
    Healthy = 1,    // Operating normally (score > 0.9)
    Warning = 2,    // Performance degraded (score 0.7-0.9)
    Critical = 3,   // Severe issues (score 0.5-0.7)
    Offline = 4,    // Device unavailable (score = 0.0)
    Error = 5       // Error state (score < 0.5)
}
```

### Health Score Calculation

The health score (0.0 to 1.0) is a weighted composite of multiple factors:

**CUDA Backend**:
- Temperature: 30% weight
- Throttling status: 30% weight
- GPU utilization: 20% weight
- Memory pressure: 10% weight
- Power consumption: 10% weight

**Metal Backend**:
- Base health status: 95% (healthy), 70% (degraded), 40% (critical)
- Error rate penalty: up to -30%
- Performance bonus: up to +5%

**CPU Backend**:
- Base score: 1.0
- High CPU usage penalty: -20% (>80%), -10% (>60%)
- High memory usage penalty: -20% (>80%), -10% (>60%)
- Thread count penalty: -10% (>100 threads)

**OpenCL Backend**:
- Binary health: 1.0 (available) or 0.0 (unavailable)
- Compiler availability: -20% penalty if missing
- Memory capacity: -10% penalty if < 256MB

### Sensor Types

DotCompute supports 18 standard sensor types:

| Sensor Type | Unit | Description | Backends |
|-------------|------|-------------|----------|
| **Temperature** | Celsius | GPU/CPU core temperature | CUDA |
| **PowerDraw** | Watts | Current power consumption | CUDA |
| **ComputeUtilization** | % | GPU/CPU usage percentage | All |
| **MemoryUtilization** | % | Memory usage percentage | All |
| **FanSpeed** | % | Cooling fan speed | CUDA |
| **GraphicsClock** | MHz | GPU core clock frequency | CUDA, OpenCL |
| **MemoryClock** | MHz | Memory clock frequency | CUDA |
| **MemoryUsedBytes** | Bytes | Used memory | All |
| **MemoryTotalBytes** | Bytes | Total memory capacity | All |
| **MemoryFreeBytes** | Bytes | Free memory available | All |
| **PcieLinkGeneration** | - | PCIe generation (3, 4, 5) | CUDA |
| **PcieLinkWidth** | Lanes | PCIe lane count | CUDA |
| **PcieThroughputTx** | Bytes/sec | PCIe transmit throughput | CUDA |
| **PcieThroughputRx** | Bytes/sec | PCIe receive throughput | CUDA |
| **ThrottlingStatus** | - | Thermal throttling severity | CUDA |
| **PowerThrottlingStatus** | - | Power throttling severity | CUDA |
| **ErrorCount** | Count | Cumulative error count | CUDA |
| **Custom** | - | Backend-specific metrics | All |

## Backend-Specific Features

### CUDA Backend (NVML Integration)

The CUDA backend provides the most comprehensive hardware monitoring through NVIDIA Management Library (NVML):

```csharp
using var accelerator = new CudaAccelerator(deviceId: 0, logger);
var health = await accelerator.GetHealthSnapshotAsync();

// CUDA provides 14 sensor types
Console.WriteLine($"Temperature: {health.GetSensorValue(SensorType.Temperature)}°C");
Console.WriteLine($"Power: {health.GetSensorValue(SensorType.PowerDraw)}W");
Console.WriteLine($"GPU Util: {health.GetSensorValue(SensorType.ComputeUtilization)}%");
Console.WriteLine($"Memory Util: {health.GetSensorValue(SensorType.MemoryUtilization)}%");
Console.WriteLine($"Fan Speed: {health.GetSensorValue(SensorType.FanSpeed)}%");

// Check for throttling
if (health.IsThrottling)
{
    var throttleStatus = health.GetSensorValue(SensorType.ThrottlingStatus);
    Console.WriteLine($"⚠️ GPU is throttling (severity: {throttleStatus})");
}
```

**CUDA Health Score Algorithm**:
```csharp
// Temperature component (30% weight)
double tempScore = temperature switch
{
    < 70 => 1.0,
    < 75 => 0.9,
    < 80 => 0.7,
    < 85 => 0.5,
    < 90 => 0.3,
    _ => 0.1
};

// Throttling component (30% weight)
double throttleScore = isThrottling ? 0.5 : 1.0;

// Utilization component (20% weight)
double utilizationScore = gpuUtilization switch
{
    < 90 => 1.0,
    < 95 => 0.9,
    >= 95 => 0.8
};

// Memory pressure component (10% weight)
var memoryUsagePercent = (double)memoryUsed / memoryTotal * 100.0;
double memoryScore = memoryUsagePercent switch
{
    < 80 => 1.0,
    < 90 => 0.9,
    < 95 => 0.7,
    _ => 0.5
};

// Power component (10% weight)
var powerPercent = (powerUsage / maxPower) * 100.0;
double powerScore = powerPercent switch
{
    < 80 => 1.0,
    < 90 => 0.9,
    < 95 => 0.8,
    _ => 0.7
};

// Weighted composite
healthScore = (tempScore * 0.3) + (throttleScore * 0.3) +
              (utilizationScore * 0.2) + (memoryScore * 0.1) +
              (powerScore * 0.1);
```

### Metal Backend (Telemetry Integration)

Metal backend integrates with existing telemetry system:

```csharp
using var accelerator = new MetalAccelerator(logger, telemetryOptions);
var health = await accelerator.GetHealthSnapshotAsync();

// Metal provides 8 sensor types
Console.WriteLine($"System Memory Used: {health.GetSensorValue(SensorType.MemoryUsedBytes)} bytes");
Console.WriteLine($"Memory Util: {health.GetSensorValue(SensorType.MemoryUtilization)}%");

// Component health metrics (custom sensors)
foreach (var reading in health.SensorReadings)
{
    if (reading.SensorType == SensorType.Custom && reading.Name?.Contains("Component") == true)
    {
        Console.WriteLine($"{reading.Name}: {reading.Value:F1}/100");
    }
}

// Check circuit breakers
if (health.IsThrottling)
{
    Console.WriteLine("⚠️ Circuit breakers open - system throttling to prevent failures");
}
```

**Metal Limitations**:
- No detailed power consumption metrics
- No clock frequency reporting
- No PCIe metrics (unified memory architecture)
- Limited thermal sensors (OS-level only)
- System-wide memory metrics (not GPU-specific)

### OpenCL Backend (Limited Monitoring)

OpenCL provides basic device information only:

```csharp
using var accelerator = new OpenCLAccelerator(platform, device, logger);
var health = await accelerator.GetHealthSnapshotAsync();

// OpenCL provides 6 static device properties
Console.WriteLine($"Total Memory: {health.GetSensorValue(SensorType.MemoryTotalBytes)} bytes");
Console.WriteLine($"Max Clock: {health.GetSensorValue(SensorType.GraphicsClock)} MHz");

// Check for custom sensors (compute units, estimated GFlops)
foreach (var reading in health.SensorReadings)
{
    if (reading.SensorType == SensorType.Custom)
    {
        Console.WriteLine($"{reading.Name}: {reading.Value:F2}");
    }
}
```

**OpenCL Limitations**:
- No real-time utilization metrics
- No temperature sensors (requires vendor extensions)
- No power consumption metrics
- No throttling status detection
- Limited to static device properties

**Vendor Extensions** (not currently implemented):
- NVIDIA: `cl_nv_device_attribute_query` (temperature, throttling)
- AMD: `cl_amd_device_attribute_query` (temperature, fan speed)
- Intel: `cl_intel_device_attribute_query` (power, frequency)

### CPU Backend (Cross-Platform Process Metrics)

CPU backend uses .NET's cross-platform System.Diagnostics APIs:

```csharp
using var accelerator = new CpuAccelerator(options, threadPoolOptions, logger);
var health = await accelerator.GetHealthSnapshotAsync();

// CPU provides 11 comprehensive metrics
Console.WriteLine($"CPU Usage: {health.GetSensorValue(SensorType.ComputeUtilization)}%");
Console.WriteLine($"Working Set: {health.GetSensorValue(SensorType.MemoryUsedBytes)} bytes");
Console.WriteLine($"GC Gen0: {health.GetSensorReading(SensorType.Custom)?.Value}");

// Thread and GC metrics
var threadReading = health.SensorReadings.First(r => r.Name?.Contains("Thread") == true);
Console.WriteLine($"Thread Count: {threadReading.Value}");
```

**CPU Metrics**:
- Process-level CPU utilization (not system-wide)
- Working set, private, and virtual memory
- Thread count
- GC collection counts (Gen0, Gen1, Gen2)
- GC total memory
- Cross-platform memory detection (Windows, Linux, macOS)

**CPU Health Score Algorithm**:
```csharp
double healthScore = 1.0;

// Penalize high CPU usage
if (cpuUsage > 80.0)
    healthScore -= 0.2;  // 20% penalty
else if (cpuUsage > 60.0)
    healthScore -= 0.1;  // 10% penalty

// Penalize high memory usage
var memoryUtilization = (double)workingSet / totalMemory;
if (memoryUtilization > 0.8)
    healthScore -= 0.2;
else if (memoryUtilization > 0.6)
    healthScore -= 0.1;

// Penalize excessive threads
if (threadCount > 100)
    healthScore -= 0.1;

healthScore = Math.Clamp(healthScore, 0.0, 1.0);
```

## Common Usage Patterns

### Pattern 1: Continuous Monitoring

```csharp
using var cts = new CancellationTokenSource();
var accelerator = new CudaAccelerator(0, logger);

// Monitor health every 5 seconds
await foreach (var health in MonitorHealthAsync(accelerator, TimeSpan.FromSeconds(5), cts.Token))
{
    Console.WriteLine($"[{health.Timestamp:HH:mm:ss}] Score: {health.HealthScore:P1}, Status: {health.Status}");

    if (health.Status >= DeviceHealthStatus.Warning)
    {
        Console.WriteLine($"⚠️ {health.StatusMessage}");
    }

    if (health.IsThrottling)
    {
        Console.WriteLine("🔥 Device is throttling - consider reducing workload");
    }
}

async IAsyncEnumerable<DeviceHealthSnapshot> MonitorHealthAsync(
    IAccelerator accelerator,
    TimeSpan interval,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        yield return await accelerator.GetHealthSnapshotAsync(cancellationToken);
        await Task.Delay(interval, cancellationToken);
    }
}
```

### Pattern 2: Health-Based Decision Making

```csharp
public async Task<bool> ShouldExecuteKernelAsync(IAccelerator accelerator)
{
    var health = await accelerator.GetHealthSnapshotAsync();

    // Don't execute if device is unhealthy
    if (!health.IsAvailable || health.Status == DeviceHealthStatus.Offline)
    {
        _logger.LogWarning("Device offline - cannot execute kernel");
        return false;
    }

    // Don't execute if critically unhealthy
    if (health.Status == DeviceHealthStatus.Critical)
    {
        _logger.LogWarning("Device in critical state (score: {Score:P1}) - skipping execution",
            health.HealthScore);
        return false;
    }

    // Warn if throttling
    if (health.IsThrottling)
    {
        _logger.LogWarning("Device is throttling - performance may be degraded");
    }

    // Check temperature (CUDA only)
    var temp = health.GetSensorValue(SensorType.Temperature);
    if (temp.HasValue && temp.Value > 85.0)
    {
        _logger.LogWarning("High temperature ({Temp}°C) - consider cooling period", temp.Value);
        return false;
    }

    return true;
}
```

### Pattern 3: Proactive Error Recovery

```csharp
public async Task ExecuteWithHealthMonitoringAsync(
    IAccelerator accelerator,
    Func<Task> operation,
    CancellationToken cancellationToken = default)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            // Check health before execution
            var healthBefore = await accelerator.GetHealthSnapshotAsync(cancellationToken);

            if (healthBefore.Status >= DeviceHealthStatus.Warning)
            {
                _logger.LogInformation("Device health degraded - performing soft reset");
                await accelerator.ResetAsync(ResetOptions.Soft, cancellationToken);
            }

            // Execute operation
            await operation();

            // Check health after execution
            var healthAfter = await accelerator.GetHealthSnapshotAsync(cancellationToken);

            if (healthAfter.HealthScore < healthBefore.HealthScore - 0.2)
            {
                _logger.LogWarning("Health declined significantly during operation");
            }

            return; // Success
        }
        catch (OutOfMemoryException) when (await TryRecoverFromOomAsync(accelerator, cancellationToken))
        {
            _logger.LogInformation("Recovered from OOM - retrying");
            continue;
        }
    }
}

private async Task<bool> TryRecoverFromOomAsync(IAccelerator accelerator, CancellationToken ct)
{
    var health = await accelerator.GetHealthSnapshotAsync(ct);
    var memUsed = health.GetSensorValue(SensorType.MemoryUsedBytes);

    _logger.LogWarning("OOM error - memory used: {MemUsed:F2} GB",
        memUsed / (1024.0 * 1024 * 1024));

    // Perform hard reset to clear memory
    var result = await accelerator.ResetAsync(ResetOptions.Hard, ct);

    return result.Success;
}
```

### Pattern 4: Orleans Integration

```csharp
public class GpuComputeGrain : Grain, IGpuComputeGrain
{
    private IAccelerator? _accelerator;
    private Timer? _healthMonitorTimer;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        // Initialize accelerator
        _accelerator = new CudaAccelerator(deviceId: 0, _logger);

        // Start periodic health monitoring
        _healthMonitorTimer = RegisterTimer(
            CheckDeviceHealthAsync,
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10)
        );

        await base.OnActivateAsync(ct);
    }

    private async Task CheckDeviceHealthAsync(object state)
    {
        if (_accelerator == null) return;

        var health = await _accelerator.GetHealthSnapshotAsync();

        // Log health metrics
        _logger.LogInformation(
            "GPU Health - Score: {Score:P1}, Status: {Status}, Throttling: {Throttling}",
            health.HealthScore, health.Status, health.IsThrottling
        );

        // Trigger reset if health is degraded
        if (health.Status >= DeviceHealthStatus.Warning)
        {
            _logger.LogWarning("GPU health degraded - triggering reset");
            await _accelerator.ResetAsync(ResetOptions.GrainDeactivation);
        }

        // Check for high error count
        if (health.ErrorCount > 10)
        {
            _logger.LogError("High error count ({Count}) - performing hard reset",
                health.ErrorCount);
            await _accelerator.ResetAsync(ResetOptions.Hard);
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // Stop health monitoring
        _healthMonitorTimer?.Dispose();

        // Perform grain deactivation reset
        if (_accelerator != null)
        {
            await _accelerator.ResetAsync(ResetOptions.GrainDeactivation, ct);
            await _accelerator.DisposeAsync();
        }

        await base.OnDeactivateAsync(reason, ct);
    }
}
```

### Pattern 5: Performance Baselines

```csharp
public class HealthBaseline
{
    public double TypicalHealthScore { get; set; }
    public double TypicalTemperature { get; set; }
    public double TypicalUtilization { get; set; }
    public Dictionary<SensorType, double> TypicalValues { get; set; } = new();
}

public async Task<HealthBaseline> EstablishBaselineAsync(
    IAccelerator accelerator,
    int sampleCount = 10,
    TimeSpan interval = default)
{
    interval = interval == default ? TimeSpan.FromSeconds(5) : interval;

    var samples = new List<DeviceHealthSnapshot>();

    for (int i = 0; i < sampleCount; i++)
    {
        samples.Add(await accelerator.GetHealthSnapshotAsync());
        if (i < sampleCount - 1)
            await Task.Delay(interval);
    }

    return new HealthBaseline
    {
        TypicalHealthScore = samples.Average(s => s.HealthScore),
        TypicalTemperature = samples.Average(s => s.GetSensorValue(SensorType.Temperature) ?? 0),
        TypicalUtilization = samples.Average(s => s.GetSensorValue(SensorType.ComputeUtilization) ?? 0),
        TypicalValues = Enum.GetValues<SensorType>()
            .Where(st => st != SensorType.Custom)
            .ToDictionary(
                st => st,
                st => samples.Average(s => s.GetSensorValue(st) ?? 0)
            )
    };
}

public async Task<bool> IsDeviceAnomalousAsync(
    IAccelerator accelerator,
    HealthBaseline baseline,
    double threshold = 0.2)
{
    var current = await accelerator.GetHealthSnapshotAsync();

    // Check if health score deviated significantly
    var scoreDelta = Math.Abs(current.HealthScore - baseline.TypicalHealthScore);
    if (scoreDelta > threshold)
    {
        _logger.LogWarning(
            "Health score anomaly detected: {Current:P1} vs baseline {Baseline:P1}",
            current.HealthScore, baseline.TypicalHealthScore
        );
        return true;
    }

    // Check temperature anomaly (CUDA)
    var currentTemp = current.GetSensorValue(SensorType.Temperature);
    if (currentTemp.HasValue)
    {
        var tempDelta = Math.Abs(currentTemp.Value - baseline.TypicalTemperature);
        if (tempDelta > 15.0) // 15°C threshold
        {
            _logger.LogWarning(
                "Temperature anomaly: {Current}°C vs baseline {Baseline}°C",
                currentTemp.Value, baseline.TypicalTemperature
            );
            return true;
        }
    }

    return false;
}
```

## Best Practices

### ✅ DO

1. **Monitor health periodically** in long-running applications
   ```csharp
   // Monitor every 10-30 seconds
   using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
   while (await timer.WaitForNextTickAsync(ct))
   {
       var health = await accelerator.GetHealthSnapshotAsync(ct);
       // Check and act on health status
   }
   ```

2. **Check health before expensive operations**
   ```csharp
   var health = await accelerator.GetHealthSnapshotAsync();
   if (health.Status == DeviceHealthStatus.Healthy)
   {
       await ExecuteLongRunningKernelAsync();
   }
   ```

3. **Use health scores for adaptive behavior**
   ```csharp
   var health = await accelerator.GetHealthSnapshotAsync();
   var workloadScale = health.HealthScore; // Scale workload by health score
   var batchSize = (int)(maxBatchSize * workloadScale);
   ```

4. **Log health metrics for debugging**
   ```csharp
   _logger.LogInformation(
       "Device Health: {DeviceName} - Score: {Score:P1}, Status: {Status}, " +
       "Temp: {Temp}°C, Util: {Util}%",
       health.DeviceName, health.HealthScore, health.Status,
       health.GetSensorValue(SensorType.Temperature),
       health.GetSensorValue(SensorType.ComputeUtilization)
   );
   ```

5. **Integrate with reset system**
   ```csharp
   if (health.Status >= DeviceHealthStatus.Critical)
   {
       await accelerator.ResetAsync(ResetOptions.Hard);
   }
   ```

### ❌ DON'T

1. **Don't poll health too frequently** (causes overhead)
   ```csharp
   // ❌ BAD: Polling every 100ms
   while (true)
   {
       await Task.Delay(100);
       var health = await accelerator.GetHealthSnapshotAsync();
   }

   // ✅ GOOD: Poll every 10-30 seconds
   using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
   while (await timer.WaitForNextTickAsync())
   {
       var health = await accelerator.GetHealthSnapshotAsync();
   }
   ```

2. **Don't assume all sensors are available** on all backends
   ```csharp
   // ❌ BAD: Assuming temperature is available
   var temp = health.GetSensorValue(SensorType.Temperature)!.Value;

   // ✅ GOOD: Check availability first
   if (health.IsSensorAvailable(SensorType.Temperature))
   {
       var temp = health.GetSensorValue(SensorType.Temperature)!.Value;
   }
   ```

3. **Don't ignore health warnings** in production
   ```csharp
   // ❌ BAD: Ignoring warnings
   var health = await accelerator.GetHealthSnapshotAsync();
   await ExecuteKernelAsync(); // Execute regardless of health

   // ✅ GOOD: Act on warnings
   var health = await accelerator.GetHealthSnapshotAsync();
   if (health.Status >= DeviceHealthStatus.Warning)
   {
       _logger.LogWarning("Device health degraded: {Message}", health.StatusMessage);
       await accelerator.ResetAsync(ResetOptions.Soft);
   }
   ```

4. **Don't mix health monitoring with critical performance paths**
   ```csharp
   // ❌ BAD: Health check in hot loop
   for (int i = 0; i < 10000; i++)
   {
       var health = await accelerator.GetHealthSnapshotAsync();
       await ExecuteKernelAsync();
   }

   // ✅ GOOD: Monitor separately from execution
   var monitorTask = Task.Run(() => MonitorHealthAsync(accelerator, ct));
   for (int i = 0; i < 10000; i++)
   {
       await ExecuteKernelAsync();
   }
   ```

## Troubleshooting

### Issue: Health score always 0.0

**Symptoms**: `GetHealthSnapshotAsync()` returns `HealthScore = 0.0` and `Status = Unknown`

**Causes**:
1. Device not initialized
2. NVML not available (CUDA)
3. Telemetry not enabled (Metal)

**Solutions**:
```csharp
// Check if device is available
if (!health.IsAvailable)
{
    Console.WriteLine($"Device unavailable: {health.StatusMessage}");
}

// For CUDA: Check NVML initialization
// NVML requires nvidia-smi to be functional

// For Metal: Enable telemetry
var telemetryOptions = new MetalTelemetryOptions
{
    EnableHealthMonitoring = true,
    EnableMetrics = true
};
```

### Issue: Sensors not available

**Symptoms**: `IsSensorAvailable()` returns `false` for expected sensors

**Causes**:
1. Backend limitations (OpenCL has limited sensors)
2. Hardware doesn't support sensor
3. Driver version too old

**Solutions**:
```csharp
// Check backend type
if (health.BackendType == "OpenCL")
{
    Console.WriteLine("OpenCL has limited sensor support");
    Console.WriteLine("Consider using CUDA or vendor-specific extensions");
}

// Check sensor availability before using
foreach (var sensorType in new[] {
    SensorType.Temperature,
    SensorType.PowerDraw,
    SensorType.FanSpeed
})
{
    if (health.IsSensorAvailable(sensorType))
    {
        Console.WriteLine($"{sensorType}: {health.GetSensorValue(sensorType)}");
    }
    else
    {
        Console.WriteLine($"{sensorType}: Not available on this backend");
    }
}
```

### Issue: Health monitoring causes performance degradation

**Symptoms**: Application performance drops when health monitoring is enabled

**Causes**:
1. Polling too frequently
2. Synchronous health checks in hot paths

**Solutions**:
```csharp
// Use appropriate polling intervals
// - CUDA/Metal: 10-30 seconds (hardware query overhead)
// - CPU: 5-10 seconds (low overhead)
// - OpenCL: 30-60 seconds (minimal value from frequent polling)

// Run health monitoring on background thread
_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    while (await timer.WaitForNextTickAsync(_cancellationToken))
    {
        var health = await _accelerator.GetHealthSnapshotAsync(_cancellationToken);
        // Process health data
    }
}, _cancellationToken);

// Keep execution path separate
await ExecuteWorkloadAsync(); // No health checks here
```

## Integration with Reset System

Health monitoring and device reset work together for robust error recovery:

```csharp
public async Task<ResetOptions> DetermineResetStrategyAsync(IAccelerator accelerator)
{
    var health = await accelerator.GetHealthSnapshotAsync();

    // Choose reset strategy based on health
    return health.Status switch
    {
        DeviceHealthStatus.Healthy => ResetOptions.Soft,
        DeviceHealthStatus.Warning when health.IsThrottling => ResetOptions.Context,
        DeviceHealthStatus.Warning => ResetOptions.Soft,
        DeviceHealthStatus.Critical when health.ErrorCount > 5 => ResetOptions.Hard,
        DeviceHealthStatus.Critical => ResetOptions.Context,
        DeviceHealthStatus.Error => ResetOptions.ErrorRecovery,
        DeviceHealthStatus.Offline => ResetOptions.Full,
        _ => ResetOptions.Soft
    };
}

// Automatic health-based reset
public async Task ExecuteWithAutoRecoveryAsync(
    IAccelerator accelerator,
    Func<Task> operation,
    CancellationToken ct = default)
{
    const int maxRetries = 3;

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            // Check health and reset if needed
            var health = await accelerator.GetHealthSnapshotAsync(ct);
            if (health.Status >= DeviceHealthStatus.Warning)
            {
                var resetOptions = await DetermineResetStrategyAsync(accelerator);
                await accelerator.ResetAsync(resetOptions, ct);
            }

            // Execute operation
            await operation();
            return; // Success
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operation failed (attempt {Attempt}/{Max})",
                attempt + 1, maxRetries);

            if (attempt < maxRetries - 1)
            {
                // Escalate reset severity with each retry
                var resetType = attempt switch
                {
                    0 => ResetType.Soft,
                    1 => ResetType.Hard,
                    _ => ResetType.Full
                };

                await accelerator.ResetAsync(new ResetOptions { ResetType = resetType }, ct);
            }
        }
    }

    throw new InvalidOperationException("Operation failed after all retries");
}
```

## API Reference

### IAccelerator Methods

```csharp
public interface IAccelerator
{
    /// <summary>
    /// Gets a comprehensive health snapshot of the device.
    /// </summary>
    ValueTask<DeviceHealthSnapshot> GetHealthSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current sensor readings from the device.
    /// </summary>
    ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(
        CancellationToken cancellationToken = default);
}
```

### DeviceHealthSnapshot Methods

```csharp
/// <summary>
/// Gets the sensor reading for the specified sensor type.
/// </summary>
/// <returns>The sensor reading, or null if not available.</returns>
public SensorReading? GetSensorReading(SensorType sensorType);

/// <summary>
/// Checks if the specified sensor is available and has valid data.
/// </summary>
public bool IsSensorAvailable(SensorType sensorType);

/// <summary>
/// Gets the value of the specified sensor if available.
/// </summary>
/// <returns>The sensor value, or null if not available.</returns>
public double? GetSensorValue(SensorType sensorType);

/// <summary>
/// Creates an unavailable health snapshot with a reason message.
/// </summary>
public static DeviceHealthSnapshot CreateUnavailable(
    string deviceId,
    string deviceName,
    string backendType,
    string reason);
```

### SensorReading Methods

```csharp
/// <summary>
/// Creates a sensor reading with the specified value and metadata.
/// </summary>
public static SensorReading Create(
    SensorType sensorType,
    double value,
    double? minValue = null,
    double? maxValue = null,
    string? name = null);

/// <summary>
/// Creates an unavailable sensor reading.
/// </summary>
public static SensorReading Unavailable(
    SensorType sensorType,
    string? name = null);
```

## See Also

- [Device Reset API Guide](device-reset.md)
- [Orleans Integration Guide](orleans-integration.md)
- [Performance Optimization Guide](performance-optimization.md)
- [Error Handling Guide](error-handling.md)

---

**Version**: 0.5.3
**Last Updated**: January 2026
**Status**: Production Ready (Phase 5 Complete - 94/94 tests passing)
