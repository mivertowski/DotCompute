# DotCompute Production Deployment Guide

## Table of Contents
- [Overview](#overview)
- [System Requirements](#system-requirements)
- [Installation](#installation)
- [Configuration](#configuration)
- [Performance Tuning](#performance-tuning)
- [Monitoring](#monitoring)
- [Troubleshooting](#troubleshooting)
- [Security Considerations](#security-considerations)
- [Best Practices](#best-practices)

## Overview

This guide provides comprehensive instructions for deploying DotCompute in production environments, covering both CPU-only and GPU-accelerated deployments.

## System Requirements

### Hardware Requirements

#### CPU-Only Deployment
- **Processor**: x64 with AVX2 support (AVX512 recommended)
- **Memory**: 8GB minimum (16GB+ recommended)
- **Storage**: 500MB for runtime + data storage

#### GPU-Accelerated Deployment
- **GPU**: NVIDIA GPU with Compute Capability 5.0+
- **VRAM**: 4GB minimum (8GB+ recommended)
- **CUDA**: Version 12.0 or higher
- **Driver**: NVIDIA Driver 525.60.13 or newer

### Software Requirements
- **.NET Runtime**: 9.0 or later
- **Operating System**:
  - Windows Server 2019/2022
  - Ubuntu 20.04/22.04 LTS
  - RHEL 8/9
- **Container Support** (Optional):
  - Docker 20.10+
  - Kubernetes 1.24+

## Installation

### NuGet Package Installation

```bash
# Core packages
dotnet add package DotCompute.Core --version 0.2.0-alpha
dotnet add package DotCompute.Backends.CPU --version 0.2.0-alpha

# GPU support (optional)
dotnet add package DotCompute.Backends.CUDA --version 0.2.0-alpha

# Memory management
dotnet add package DotCompute.Memory --version 0.2.0-alpha
```

### Docker Deployment

```dockerfile
# Dockerfile for CPU-only deployment
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["YourApp.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish \
    /p:PublishAot=true \
    /p:StripSymbols=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./YourApp"]
```

```dockerfile
# Dockerfile for GPU deployment
FROM nvidia/cuda:12.0-runtime-ubuntu22.04 AS base
RUN apt-get update && apt-get install -y \
    wget \
    && wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-runtime-9.0

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# ... (same as CPU build)

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Kubernetes Deployment

```yaml
# dotcompute-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotcompute-app
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotcompute
  template:
    metadata:
      labels:
        app: dotcompute
    spec:
      containers:
      - name: app
        image: your-registry/dotcompute-app:latest
        resources:
          requests:
            memory: "4Gi"
            cpu: "2"
            nvidia.com/gpu: 1  # For GPU nodes
          limits:
            memory: "8Gi"
            cpu: "4"
            nvidia.com/gpu: 1
        env:
        - name: DOTCOMPUTE_BACKEND
          value: "Auto"
        - name: DOTCOMPUTE_MEMORY_POOL_SIZE
          value: "2147483648"  # 2GB
        - name: DOTCOMPUTE_ENABLE_PROFILING
          value: "true"
```

## Configuration

### Application Configuration

```json
// appsettings.Production.json
{
  "DotCompute": {
    "Backend": {
      "PreferredBackend": "Auto",
      "FallbackEnabled": true,
      "BackendPriority": ["CUDA", "CPU"]
    },
    "Memory": {
      "PoolSize": 2147483648,
      "EnablePooling": true,
      "PinnedMemoryRatio": 0.25,
      "UnifiedMemoryEnabled": true
    },
    "Compilation": {
      "CacheEnabled": true,
      "CacheDirectory": "/var/cache/dotcompute",
      "OptimizationLevel": "Aggressive",
      "GenerateDebugInfo": false
    },
    "Performance": {
      "EnableProfiling": false,
      "MetricsExportInterval": 60,
      "AdaptiveOptimization": true,
      "ThreadPoolSize": 0  // 0 = auto-detect
    },
    "Logging": {
      "MinimumLevel": "Information",
      "EnablePerformanceLogging": true
    }
  }
}
```

### Environment Variables

```bash
# Backend selection
export DOTCOMPUTE_BACKEND=Auto

# Memory configuration
export DOTCOMPUTE_MEMORY_POOL_SIZE=2147483648
export DOTCOMPUTE_PINNED_MEMORY_RATIO=0.25

# Performance tuning
export DOTCOMPUTE_THREAD_POOL_SIZE=16
export DOTCOMPUTE_ENABLE_PROFILING=false

# CUDA specific
export CUDA_VISIBLE_DEVICES=0,1  # GPU selection
export CUDA_CACHE_PATH=/var/cache/cuda
export CUDA_LAUNCH_BLOCKING=0
```

## Performance Tuning

### CPU Optimization

```csharp
// Startup.cs
services.AddDotComputeRuntime(options =>
{
    options.CpuOptions = new CpuBackendOptions
    {
        EnableAvx512 = true,
        ThreadCount = Environment.ProcessorCount,
        AffinityMask = null,  // Let OS manage
        EnableNuma = true
    };
});
```

### GPU Optimization

```csharp
services.AddDotComputeRuntime(options =>
{
    options.CudaOptions = new CudaBackendOptions
    {
        DeviceId = 0,
        EnablePeerAccess = true,
        StreamCount = 4,
        EnableMemoryPooling = true,
        ComputeMode = ComputeMode.Exclusive,
        KernelCacheSize = 1000
    };
});
```

### Memory Optimization

```csharp
services.AddDotComputeMemory(options =>
{
    options.PoolConfiguration = new MemoryPoolConfig
    {
        InitialSize = 1L << 30,  // 1GB
        MaxSize = 4L << 30,      // 4GB
        GrowthFactor = 2.0,
        TrimInterval = TimeSpan.FromMinutes(5),
        EnableStatistics = true
    };
});
```

## Monitoring

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<DotComputeHealthCheck>("dotcompute")
    .AddCheck<GpuHealthCheck>("gpu", tags: new[] { "gpu" });

// Custom health check
public class DotComputeHealthCheck : IHealthCheck
{
    private readonly IComputeOrchestrator _orchestrator;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var backends = await _orchestrator.GetAvailableBackendsAsync();
            if (!backends.Any())
                return HealthCheckResult.Unhealthy("No compute backends available");
                
            return HealthCheckResult.Healthy($"{backends.Count()} backends available");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Compute system check failed", ex);
        }
    }
}
```

### Metrics Collection

```csharp
// Prometheus metrics
services.AddSingleton<IMetricsCollector, PrometheusMetricsCollector>();

public class PrometheusMetricsCollector : IMetricsCollector
{
    private readonly Counter _kernelExecutions = Metrics.CreateCounter(
        "dotcompute_kernel_executions_total",
        "Total kernel executions");
        
    private readonly Histogram _executionDuration = Metrics.CreateHistogram(
        "dotcompute_execution_duration_seconds",
        "Kernel execution duration");
        
    private readonly Gauge _memoryUsage = Metrics.CreateGauge(
        "dotcompute_memory_usage_bytes",
        "Current memory usage");
}
```

### Logging

```csharp
// Structured logging with Serilog
builder.Host.UseSerilog((context, config) =>
{
    config
        .MinimumLevel.Information()
        .MinimumLevel.Override("DotCompute", LogLevel.Debug)
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.File("logs/dotcompute-.log", 
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30)
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200"))
        {
            IndexFormat = "dotcompute-{0:yyyy.MM.dd}"
        });
});
```

## Troubleshooting

### Common Issues

#### CUDA Not Available
```bash
# Check CUDA installation
nvidia-smi
nvcc --version

# Verify CUDA libraries
ldconfig -p | grep cuda

# Check compute capability
/usr/local/cuda/extras/demo_suite/deviceQuery
```

#### Memory Issues
```csharp
// Enable detailed memory diagnostics
services.AddDotComputeRuntime(options =>
{
    options.DiagnosticOptions = new DiagnosticOptions
    {
        EnableMemoryTracking = true,
        EnableLeakDetection = true,
        DumpOnOutOfMemory = true,
        DumpPath = "/var/dump/dotcompute"
    };
});
```

#### Performance Issues
```csharp
// Enable performance profiling
services.AddDotComputeProfiling(options =>
{
    options.EnableKernelProfiling = true;
    options.EnableMemoryProfiling = true;
    options.ProfileOutputPath = "/var/log/dotcompute/profiles";
    options.SamplingInterval = TimeSpan.FromSeconds(1);
});
```

### Diagnostic Tools

```bash
# Generate diagnostic report
dotnet-dump collect -p <PID>
dotnet-dump analyze <dump-file>

# CPU profiling
dotnet-trace collect -p <PID> --providers DotCompute

# Memory analysis
dotnet-gcdump collect -p <PID>
```

## Security Considerations

### Kernel Validation

```csharp
services.AddDotComputeSecurity(options =>
{
    options.ValidateKernels = true;
    options.AllowUnsafeCode = false;
    options.MaxKernelSize = 1048576;  // 1MB
    options.EnableSandboxing = true;
});
```

### Resource Limits

```csharp
services.AddDotComputeResourceLimits(options =>
{
    options.MaxMemoryPerOperation = 1L << 30;  // 1GB
    options.MaxExecutionTime = TimeSpan.FromMinutes(5);
    options.MaxConcurrentKernels = 10;
    options.EnableResourceQuotas = true;
});
```

### Network Security

```yaml
# Network policy for Kubernetes
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: dotcompute-network-policy
spec:
  podSelector:
    matchLabels:
      app: dotcompute
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          role: frontend
    ports:
    - protocol: TCP
      port: 8080
```

## Best Practices

### 1. Resource Management
- Always dispose compute resources properly
- Use memory pooling for frequent allocations
- Implement circuit breakers for GPU operations
- Monitor memory fragmentation

### 2. Error Handling
```csharp
public async Task<ComputeResult> ExecuteWithResilience(KernelDefinition kernel, object data)
{
    var policy = Policy
        .Handle<CudaException>()
        .OrResult<ComputeResult>(r => !r.Success)
        .RetryAsync(3, onRetry: (outcome, retry) =>
        {
            _logger.LogWarning($"Kernel execution failed, retry {retry}");
        });
        
    return await policy.ExecuteAsync(async () =>
    {
        return await _orchestrator.ExecuteAsync(kernel, data);
    });
}
```

### 3. Performance Optimization
- Pre-compile frequently used kernels
- Use kernel fusion for complex operations
- Implement adaptive batch sizing
- Cache compiled kernels aggressively

### 4. Monitoring and Alerting
```yaml
# Prometheus alert rules
groups:
- name: dotcompute
  rules:
  - alert: HighMemoryUsage
    expr: dotcompute_memory_usage_bytes > 3221225472  # 3GB
    for: 5m
    annotations:
      summary: "High memory usage detected"
      
  - alert: KernelExecutionErrors
    expr: rate(dotcompute_kernel_errors_total[5m]) > 0.1
    for: 5m
    annotations:
      summary: "High kernel execution error rate"
```

### 5. Deployment Checklist
- [ ] Verify system requirements
- [ ] Configure resource limits
- [ ] Set up monitoring and alerting
- [ ] Enable health checks
- [ ] Configure logging
- [ ] Test failover scenarios
- [ ] Validate performance benchmarks
- [ ] Document configuration
- [ ] Set up backup and recovery
- [ ] Review security settings

## Support

For production support and issues:
- GitHub Issues: https://github.com/dotcompute/dotcompute/issues
- Documentation: https://docs.dotcompute.io
- Community Forum: https://forum.dotcompute.io