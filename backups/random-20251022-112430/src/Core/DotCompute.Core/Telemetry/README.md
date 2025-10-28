# DotCompute Telemetry System

## Overview

The DotCompute Telemetry System provides comprehensive production-grade observability for GPU compute operations. It includes structured logging, distributed tracing, performance profiling, and metrics collection with minimal performance impact (<1%).

## Features

### 🔍 **Structured Logging**
- Semantic properties with correlation IDs
- Automatic sensitive data redaction
- Async batched processing to avoid blocking
- Multiple sink support (console, file, network)
- JSON serialization for structured data

### 📊 **Metrics Collection**
- Kernel execution metrics (time, throughput, efficiency)
- Memory usage and allocation patterns  
- Device utilization and temperature monitoring
- Transfer bandwidth and latency tracking
- Error rates and recovery success metrics

### 🌐 **Distributed Tracing**
- OpenTelemetry integration
- Cross-device operation tracing
- Correlation ID propagation
- Performance bottleneck identification
- Distributed context management

### 📈 **Performance Profiling**
- Detailed kernel execution analysis
- Memory access pattern profiling
- Cache hit/miss rate monitoring
- Occupancy and warp efficiency tracking
- Power consumption analysis

### 📉 **Prometheus Integration**
- Real-time metrics export
- Custom dashboard support
- Alert threshold configuration
- Anomaly detection capabilities
- Multi-dimensional metrics

## Quick Start

### 1. Basic Setup

```csharp
// In Program.cs or Startup.cs
services.AddDotComputeTelemetry(options =>
{
    options.EnableTelemetry = true;
    options.EnableDistributedTracing = true;
    options.EnablePerformanceProfiling = true;
    
    // Configure Prometheus metrics
    options.Prometheus.Enabled = true;
    options.Prometheus.Port = 9090;
    
    // Configure structured logging
    options.StructuredLogging.EnableSynchronousLogging = false;
    options.StructuredLogging.FlushIntervalSeconds = 5;
});

// Add file logging
services.AddFileLogging("/logs/dotcompute.jsonl");
```

### 2. Recording Kernel Executions

```csharp
// Inject telemetry service
public class MyComputeService
{
    private readonly ITelemetryService _telemetry;
    
    public MyComputeService(ITelemetryService telemetry)
    {
        _telemetry = telemetry;
    }
    
    public async Task ExecuteKernelAsync()
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        
        // Start distributed trace
        var trace = _telemetry.StartDistributedTrace("matrix_multiply", correlationId);
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Execute your kernel
            await ExecuteMatrixMultiplyAsync();
            
            stopwatch.Stop();
            
            // Record successful execution
            _telemetry.RecordKernelExecution(
                kernelName: "matrix_multiply",
                deviceId: "gpu_0",
                executionTime: stopwatch.Elapsed,
                metrics: new KernelPerformanceMetrics
                {
                    ThroughputOpsPerSecond = 1_000_000,
                    OccupancyPercentage = 85.5,
                    CacheHitRate = 0.92,
                    MemoryBandwidthGBPerSecond = 450.2
                },
                correlationId: correlationId
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record failed execution
            _telemetry.RecordKernelExecution(
                kernelName: "matrix_multiply",
                deviceId: "gpu_0", 
                executionTime: stopwatch.Elapsed,
                metrics: new KernelPerformanceMetrics(),
                correlationId: correlationId,
                exception: ex
            );
            throw;
        }
        finally
        {
            // Finish distributed trace
            await _telemetry.FinishDistributedTraceAsync(correlationId);
        }
    }
}
```

### 3. Memory Operations

```csharp
public async Task RecordMemoryTransferAsync()
{
    var stopwatch = Stopwatch.StartNew();
    var correlationId = GetCurrentCorrelationId();
    
    try
    {
        // Perform memory transfer
        await TransferDataToGpuAsync(data);
        stopwatch.Stop();
        
        _telemetry.RecordMemoryOperation(
            operationType: "host_to_device",
            deviceId: "gpu_0",
            bytes: data.Length * sizeof(float),
            duration: stopwatch.Elapsed,
            metrics: new MemoryAccessMetrics
            {
                AccessPattern = "sequential",
                CoalescingEfficiency = 0.95,
                CacheHitRate = 0.88,
                MemorySegment = "global",
                TransferDirection = "upload"
            },
            correlationId: correlationId
        );
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _telemetry.RecordMemoryOperation(
            operationType: "host_to_device",
            deviceId: "gpu_0",
            bytes: data.Length * sizeof(float), 
            duration: stopwatch.Elapsed,
            metrics: new MemoryAccessMetrics(),
            correlationId: correlationId,
            exception: ex
        );
        throw;
    }
}
```

### 4. Performance Profiling

```csharp
public async Task<PerformanceProfile> ProfileOperationAsync()
{
    var correlationId = Guid.NewGuid().ToString("N")[..12];
    
    // Start performance profiling
    var profile = await _telemetry.CreatePerformanceProfileAsync(
        correlationId,
        new ProfileOptions
        {
            AutoStopAfter = TimeSpan.FromMinutes(5),
            EnableDetailedMemoryProfiling = true,
            EnableKernelProfiling = true
        });
    
    // Your compute operations here...
    // All operations with the same correlationId will be profiled
    
    return profile;
}
```

## Configuration Options

### Telemetry Provider Options

```csharp
services.Configure<TelemetryOptions>(options =>
{
    options.EnableSampling = true;
    options.SamplingIntervalSeconds = 30;
    options.MemoryAlertThreshold = 1024L * 1024 * 1024; // 1GB
    options.ErrorRateThreshold = 0.05; // 5%
    options.EnableDistributedTracing = true;
    options.EnablePerformanceProfiling = true;
});
```

### Structured Logging Options

```csharp
services.Configure<StructuredLoggingOptions>(options =>
{
    options.EnableSynchronousLogging = false;
    options.FlushIntervalSeconds = 5;
    options.MaxBufferSize = 10000;
    options.EnableSensitiveDataRedaction = true;
    options.MinimumLogLevel = LogLevel.Information;
});
```

### Log Buffer Options

```csharp
services.Configure<LogBufferOptions>(options =>
{
    options.MaxBufferSize = 10000;
    options.BatchSize = 100;
    options.BatchIntervalMs = 1000;
    options.DropOnFull = true;
    options.EnableCompression = true;
    options.CompressionThreshold = 50;
});
```

### Distributed Tracing Options

```csharp
services.Configure<DistributedTracingOptions>(options =>
{
    options.MaxTracesPerExport = 1000;
    options.TraceRetentionHours = 24;
    options.EnableSampling = true;
    options.SamplingRate = 0.1; // 10%
});
```

### Performance Profiler Options

```csharp
services.Configure<PerformanceProfilerOptions>(options =>
{
    options.MaxConcurrentProfiles = 10;
    options.EnableContinuousProfiling = true;
    options.SamplingIntervalMs = 100;
    options.AllowOrphanedRecords = false;
});
```

## Metrics Reference

### Core Execution Metrics

- `dotcompute_kernel_executions_total` - Total kernel executions
- `dotcompute_kernel_execution_duration_seconds` - Kernel execution time
- `dotcompute_memory_operations_total` - Total memory operations
- `dotcompute_memory_transfer_duration_seconds` - Memory transfer time
- `dotcompute_errors_total` - Total errors by type

### Performance Metrics

- `dotcompute_kernel_throughput_ops_per_second` - Operations throughput
- `dotcompute_kernel_occupancy_percentage` - GPU occupancy
- `dotcompute_cache_hit_rate` - Cache hit ratio
- `dotcompute_memory_bandwidth_gb_per_second` - Memory bandwidth
- `dotcompute_warp_efficiency` - Warp execution efficiency
- `dotcompute_branch_divergence` - Branch divergence ratio

### Resource Metrics

- `dotcompute_memory_usage_bytes` - Current memory usage
- `dotcompute_device_utilization_ratio` - Device utilization
- `dotcompute_device_temperature_celsius` - Device temperature
- `dotcompute_power_consumption_watts` - Power consumption

### Profiling Metrics

- `dotcompute_profiles_created_total` - Total profiles created
- `dotcompute_active_profiles` - Active profiles count
- `dotcompute_profile_duration_seconds` - Profile duration

## Dashboard Integration

### Prometheus Configuration

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'dotcompute'
    static_configs:
      - targets: ['localhost:9464']
    scrape_interval: 15s
    metrics_path: /metrics
```

### Grafana Dashboard

Import the provided Grafana dashboard JSON or create custom dashboards with these key panels:

1. **Kernel Execution Rate** - `rate(dotcompute_kernel_executions_total[5m])`
2. **Average Execution Time** - `rate(dotcompute_kernel_execution_duration_seconds_sum[5m]) / rate(dotcompute_kernel_execution_duration_seconds_count[5m])`
3. **Memory Utilization** - `dotcompute_memory_usage_bytes / dotcompute_memory_capacity_bytes`
4. **Device Temperature** - `dotcompute_device_temperature_celsius`
5. **Error Rate** - `rate(dotcompute_errors_total[5m])`

### Alert Rules

```yaml
# alerts.yml
groups:
  - name: dotcompute
    rules:
      - alert: HighErrorRate
        expr: rate(dotcompute_errors_total[5m]) > 0.1
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"
          
      - alert: HighMemoryUsage
        expr: dotcompute_memory_usage_bytes / dotcompute_memory_capacity_bytes > 0.9
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Memory usage above 90%"
          
      - alert: DeviceOverheating
        expr: dotcompute_device_temperature_celsius > 80
        for: 1m
        labels:
          severity: warning
        annotations:
          summary: "Device temperature above 80°C"
```

## Log Sinks

### Built-in Sinks

1. **ConsoleSink** - Outputs to console
2. **FileSink** - Writes to JSON Lines files

### Custom Sinks

```csharp
public class CustomLogSink : ILogSink, IHealthCheckable
{
    public bool IsHealthy { get; private set; } = true;
    
    public void Initialize() 
    {
        // Initialize your sink
    }
    
    public async Task WriteAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default)
    {
        // Write single entry
    }
    
    public async Task WriteBatchAsync(List<StructuredLogEntry> entries, CancellationToken cancellationToken = default)
    {
        // Write batch of entries
    }
    
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Flush buffered data
    }
    
    public void MarkUnhealthy(Exception? exception = null) => IsHealthy = false;
    public void MarkHealthy() => IsHealthy = true;
    
    public void Dispose() 
    {
        // Cleanup resources
    }
}

// Register custom sink
services.AddLogSink<CustomLogSink>();
```

## Export Formats

### Supported Formats

1. **Prometheus** - Native Prometheus text format
2. **OpenTelemetry** - OTLP protocol
3. **JSON** - Structured JSON export
4. **Custom** - Implement custom exporters

### Export Examples

```csharp
// Export to Prometheus format
await telemetryService.ExportTelemetryAsync(TelemetryExportFormat.Prometheus);

// Export to OpenTelemetry
await telemetryService.ExportTelemetryAsync(TelemetryExportFormat.OpenTelemetry);

// Export to file
var prometheusExporter = serviceProvider.GetService<PrometheusExporter>();
await prometheusExporter.ExportMetricsToFileAsync("/metrics/dotcompute.prom");
```

## Performance Considerations

### Minimal Impact Design
- **Async Processing** - All heavy operations are non-blocking
- **Batched I/O** - Logs and metrics are batched for efficiency
- **Sampling** - Configurable sampling rates for high-volume scenarios
- **Buffering** - Smart buffering with backpressure handling
- **Lock-Free** - Extensive use of concurrent collections

### Performance Targets
- **<1% CPU overhead** for telemetry operations
- **<50MB memory** baseline footprint
- **<10ms latency** for metric recording
- **>10,000 events/sec** processing capacity

### Optimization Tips
1. Use appropriate sampling rates for high-frequency operations
2. Configure buffer sizes based on your workload
3. Enable compression for large log volumes
4. Use dedicated log sinks for different log levels
5. Monitor telemetry system health metrics

## Troubleshooting

### Common Issues

**High Memory Usage**
```csharp
// Check buffer statistics
var stats = logBuffer.GetStatistics();
if (stats.BufferUtilization > 0.8)
{
    // Increase flush frequency or buffer size
    options.LogBuffer.BatchIntervalMs = 500; // Reduce from 1000ms
    options.LogBuffer.MaxBufferSize = 20000; // Increase buffer
}
```

**Dropped Log Entries**
```csharp
var stats = logBuffer.GetStatistics();
if (stats.DropRate > 0.05) // More than 5% dropped
{
    // Increase buffer size or add more sinks
    options.LogBuffer.MaxBufferSize *= 2;
    services.AddLogSink<AdditionalFileSink>();
}
```

**Missing Traces**
```csharp
// Verify correlation ID propagation
services.Configure<DistributedTracingOptions>(options =>
{
    options.EnableSampling = false; // Disable sampling temporarily
});
```

### Health Monitoring

```csharp
// Check telemetry system health
var health = telemetryService.GetSystemHealth();
if (!health.IsHealthy)
{
    logger.LogWarning("Telemetry system unhealthy: {Reason}", health.StatusMessage);
}

// Monitor buffer health
var bufferStats = logBuffer.GetStatistics();
if (bufferStats.DropRate > 0.1)
{
    logger.LogError("High log drop rate: {DropRate:P1}", bufferStats.DropRate);
}
```

## Integration Examples

### ASP.NET Core

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDotComputeTelemetry(options =>
        {
            options.Prometheus.Enabled = true;
            options.Prometheus.Port = 9090;
        });
        
        services.AddFileLogging("/app/logs/dotcompute.jsonl");
    }
    
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Prometheus metrics endpoint
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMetrics(); // Exposes /metrics endpoint
        });
    }
}
```

### Worker Service

```csharp
public class ComputeWorker : BackgroundService
{
    private readonly ITelemetryService _telemetry;
    
    public ComputeWorker(ITelemetryService telemetry)
    {
        _telemetry = telemetry;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            
            try
            {
                using var trace = _telemetry.StartDistributedTrace("batch_processing", correlationId);
                
                // Your compute work here
                await ProcessBatchAsync(stoppingToken);
                
                await _telemetry.FinishDistributedTraceAsync(correlationId, TraceStatus.Ok);
            }
            catch (Exception ex)
            {
                await _telemetry.FinishDistributedTraceAsync(correlationId, TraceStatus.Error);
                throw;
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

## Best Practices

### Correlation ID Management
- Generate correlation IDs at entry points
- Propagate through all operations
- Use short, readable IDs (8-12 characters)
- Store in async context or DI scope

### Metric Naming
- Use consistent naming conventions
- Include units in metric names
- Add appropriate labels for dimensionality
- Avoid high-cardinality labels

### Log Structure
- Use semantic properties consistently
- Avoid logging sensitive data
- Structure logs for machine parsing
- Include context for troubleshooting

### Performance Profiling
- Profile representative workloads
- Use appropriate sampling rates
- Focus on critical performance paths
- Analyze trends over time

This comprehensive telemetry system provides production-ready observability for DotCompute applications with minimal performance impact and extensive customization options.