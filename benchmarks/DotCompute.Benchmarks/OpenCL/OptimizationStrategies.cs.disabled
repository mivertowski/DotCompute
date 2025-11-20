// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL;
using DotCompute.Backends.OpenCL.Configuration;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Profiling;
using Microsoft.Extensions.Logging;

namespace DotCompute.Benchmarks.OpenCL;

/// <summary>
/// Implements and benchmarks various optimization strategies for OpenCL backend:
/// - Kernel compilation caching effectiveness
/// - Memory pooling performance impact
/// - Work-group size optimization
/// - Async execution overlap
/// - Vendor-specific optimizations
/// </summary>
public sealed class OptimizationStrategies
{
    private readonly ILogger<OptimizationStrategies> _logger;

    public OptimizationStrategies(ILogger<OptimizationStrategies> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Measures kernel compilation caching effectiveness
    /// </summary>
    public async Task<CachingEffectivenessReport> MeasureCompilationCachingAsync(
        OpenCLAccelerator accelerator,
        int iterationCount = 100)
    {
        _logger.LogInformation("Measuring kernel compilation caching effectiveness ({Iterations} iterations)", iterationCount);

        var kernelSource = @"
__kernel void benchmark_kernel(__global float* data, const int size)
{
    int idx = get_global_id(0);
    if (idx < size) {
        data[idx] = data[idx] * 2.0f + 1.0f;
    }
}";

        var firstCompileTime = TimeSpan.Zero;
        var cachedCompileTimes = new List<TimeSpan>();

        // First compilation (cold cache)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var kernel1 = await accelerator.CompileKernelAsync(new DotCompute.Abstractions.Kernels.KernelDefinition
        {
            Name = "benchmark_kernel",
            Source = kernelSource,
            EntryPoint = "benchmark_kernel"
        });
        firstCompileTime = sw.Elapsed;
        kernel1.Dispose();

        // Subsequent compilations (warm cache)
        for (int i = 0; i < iterationCount; i++)
        {
            sw.Restart();
            var kernel = await accelerator.CompileKernelAsync(new DotCompute.Abstractions.Kernels.KernelDefinition
            {
                Name = "benchmark_kernel",
                Source = kernelSource,
                EntryPoint = "benchmark_kernel"
            });
            cachedCompileTimes.Add(sw.Elapsed);
            kernel.Dispose();
        }

        var avgCachedTime = cachedCompileTimes.Average(t => t.TotalMilliseconds);
        var cacheSpeedup = firstCompileTime.TotalMilliseconds / avgCachedTime;

        var report = new CachingEffectivenessReport
        {
            FirstCompilationMs = firstCompileTime.TotalMilliseconds,
            AverageCachedCompilationMs = avgCachedTime,
            MinCachedCompilationMs = cachedCompileTimes.Min(t => t.TotalMilliseconds),
            MaxCachedCompilationMs = cachedCompileTimes.Max(t => t.TotalMilliseconds),
            CacheSpeedup = cacheSpeedup,
            IterationCount = iterationCount
        };

        _logger.LogInformation(
            "Caching effectiveness: First={FirstMs:F3}ms, Avg Cached={AvgMs:F3}ms, Speedup={Speedup:F2}x",
            report.FirstCompilationMs, report.AverageCachedCompilationMs, report.CacheSpeedup);

        return report;
    }

    /// <summary>
    /// Measures memory pooling performance impact
    /// </summary>
    public async Task<MemoryPoolingReport> MeasureMemoryPoolingAsync(
        ILoggerFactory loggerFactory,
        int allocationCount = 1000,
        int bufferSizeMB = 10)
    {
        _logger.LogInformation(
            "Measuring memory pooling impact ({Allocations} allocations, {SizeMB}MB each)",
            allocationCount, bufferSizeMB);

        // Test without pooling
        var withoutPoolingConfig = new OpenCLConfiguration
        {
            Memory = new MemoryConfiguration
            {
                EnableMemoryPooling = false
            }
        };

        var withoutPooling = new OpenCLAccelerator(loggerFactory, withoutPoolingConfig);
        await withoutPooling.InitializeAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < allocationCount; i++)
        {
            var buffer = await withoutPooling.AllocateAsync<float>((nuint)(bufferSizeMB * 1024 * 1024 / sizeof(float)));
            buffer.Dispose();
        }
        var timeWithoutPooling = sw.Elapsed;

        withoutPooling.Dispose();

        // Test with pooling
        var withPoolingConfig = new OpenCLConfiguration
        {
            Memory = new MemoryConfiguration
            {
                EnableMemoryPooling = true,
                InitialPoolSizeMB = bufferSizeMB * 10,
                MaxPoolSizeMB = bufferSizeMB * 100
            }
        };

        var withPooling = new OpenCLAccelerator(loggerFactory, withPoolingConfig);
        await withPooling.InitializeAsync();

        sw.Restart();
        for (int i = 0; i < allocationCount; i++)
        {
            var buffer = await withPooling.AllocateAsync<float>((nuint)(bufferSizeMB * 1024 * 1024 / sizeof(float)));
            buffer.Dispose();
        }
        var timeWithPooling = sw.Elapsed;

        withPooling.Dispose();

        var improvement = (timeWithoutPooling.TotalMilliseconds - timeWithPooling.TotalMilliseconds) /
                          timeWithoutPooling.TotalMilliseconds * 100.0;

        var report = new MemoryPoolingReport
        {
            TimeWithoutPoolingMs = timeWithoutPooling.TotalMilliseconds,
            TimeWithPoolingMs = timeWithPooling.TotalMilliseconds,
            ImprovementPercentage = improvement,
            AllocationCount = allocationCount,
            BufferSizeMB = bufferSizeMB
        };

        _logger.LogInformation(
            "Memory pooling impact: Without={WithoutMs:F1}ms, With={WithMs:F1}ms, Improvement={Improvement:F1}%",
            report.TimeWithoutPoolingMs, report.TimeWithPoolingMs, report.ImprovementPercentage);

        return report;
    }

    /// <summary>
    /// Analyzes optimal work-group sizes for different workload sizes
    /// </summary>
    public async Task<WorkGroupOptimizationReport> AnalyzeWorkGroupSizesAsync(
        OpenCLAccelerator accelerator,
        int[] workloadSizes)
    {
        _logger.LogInformation("Analyzing optimal work-group sizes for various workloads");

        var deviceInfo = accelerator.DeviceInfo;
        if (deviceInfo == null)
        {
            throw new InvalidOperationException("OpenCL device info not available");
        }

        var maxWorkGroupSize = (int)deviceInfo.MaxWorkGroupSize;
        var computeUnits = (int)deviceInfo.MaxComputeUnits;

        // Test different work-group sizes
        var workGroupSizes = new[] { 64, 128, 256, 512, Math.Min(1024, maxWorkGroupSize) };

        var results = new List<WorkGroupSizeResult>();

        foreach (var workloadSize in workloadSizes)
        {
            foreach (var workGroupSize in workGroupSizes)
            {
                // TODO: Execute kernel with different work-group sizes when execution engine is available
                // For now, provide theoretical analysis

                var globalSize = ((workloadSize + workGroupSize - 1) / workGroupSize) * workGroupSize;
                var numWorkGroups = globalSize / workGroupSize;
                var occupancy = Math.Min(1.0, (double)numWorkGroups / computeUnits);

                results.Add(new WorkGroupSizeResult
                {
                    WorkloadSize = workloadSize,
                    WorkGroupSize = workGroupSize,
                    NumWorkGroups = numWorkGroups,
                    GlobalSize = globalSize,
                    EstimatedOccupancy = occupancy,
                    ExecutionTimeMs = 0 // Placeholder for actual measurement
                });
            }
        }

        var report = new WorkGroupOptimizationReport
        {
            DeviceName = deviceInfo.Name,
            MaxWorkGroupSize = maxWorkGroupSize,
            ComputeUnits = computeUnits,
            Results = results
        };

        _logger.LogInformation(
            "Work-group analysis complete: Device={Device}, Max WG Size={MaxSize}, Compute Units={Units}",
            report.DeviceName, report.MaxWorkGroupSize, report.ComputeUnits);

        return report;
    }

    /// <summary>
    /// Measures vendor-specific optimization effectiveness
    /// </summary>
    public VendorOptimizationReport AnalyzeVendorOptimizations(OpenCLDeviceInfo deviceInfo)
    {
        _logger.LogInformation("Analyzing vendor-specific optimizations for {Vendor}", deviceInfo.Vendor);

        var report = new VendorOptimizationReport
        {
            Vendor = deviceInfo.Vendor,
            DeviceName = deviceInfo.Name,
            DriverVersion = deviceInfo.DriverVersion,
            OpenCLVersion = deviceInfo.OpenCLCVersion,
            Extensions = deviceInfo.Extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
        };

        // Analyze available extensions for optimization opportunities
        if (report.Extensions.Contains("cl_khr_fp16"))
        {
            report.AvailableOptimizations.Add("Half-precision floating point (FP16) operations");
        }

        if (report.Extensions.Contains("cl_khr_fp64"))
        {
            report.AvailableOptimizations.Add("Double-precision floating point (FP64) operations");
        }

        if (report.Extensions.Contains("cl_khr_local_int32_base_atomics"))
        {
            report.AvailableOptimizations.Add("Local memory atomic operations");
        }

        if (report.Extensions.Contains("cl_khr_subgroups"))
        {
            report.AvailableOptimizations.Add("Subgroup operations (similar to CUDA warps)");
        }

        // Vendor-specific optimizations
        if (deviceInfo.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            report.VendorSpecificFeatures.Add("Warp-level optimizations via subgroups");
            report.VendorSpecificFeatures.Add("Tensor core acceleration (if supported)");
            report.RecommendedWorkGroupMultiple = 32; // Warp size
        }
        else if (deviceInfo.Vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                 deviceInfo.Vendor.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase))
        {
            report.VendorSpecificFeatures.Add("Wavefront-level optimizations");
            report.VendorSpecificFeatures.Add("GCN/RDNA architecture-specific tuning");
            report.RecommendedWorkGroupMultiple = 64; // Wavefront size
        }
        else if (deviceInfo.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            report.VendorSpecificFeatures.Add("EU (Execution Unit) thread optimizations");
            report.VendorSpecificFeatures.Add("Shared local memory optimization");
            report.RecommendedWorkGroupMultiple = 16; // Typical EU thread group
        }

        _logger.LogInformation(
            "Vendor analysis: {OptCount} optimizations available, {FeatureCount} vendor-specific features",
            report.AvailableOptimizations.Count, report.VendorSpecificFeatures.Count);

        return report;
    }
}

// ==================== Report Data Structures ====================

public sealed record CachingEffectivenessReport
{
    public double FirstCompilationMs { get; init; }
    public double AverageCachedCompilationMs { get; init; }
    public double MinCachedCompilationMs { get; init; }
    public double MaxCachedCompilationMs { get; init; }
    public double CacheSpeedup { get; init; }
    public int IterationCount { get; init; }
}

public sealed record MemoryPoolingReport
{
    public double TimeWithoutPoolingMs { get; init; }
    public double TimeWithPoolingMs { get; init; }
    public double ImprovementPercentage { get; init; }
    public int AllocationCount { get; init; }
    public int BufferSizeMB { get; init; }
}

public sealed record WorkGroupOptimizationReport
{
    public string DeviceName { get; init; } = string.Empty;
    public int MaxWorkGroupSize { get; init; }
    public int ComputeUnits { get; init; }
    public List<WorkGroupSizeResult> Results { get; init; } = new();
}

public sealed record WorkGroupSizeResult
{
    public int WorkloadSize { get; init; }
    public int WorkGroupSize { get; init; }
    public int NumWorkGroups { get; init; }
    public int GlobalSize { get; init; }
    public double EstimatedOccupancy { get; init; }
    public double ExecutionTimeMs { get; init; }
}

public sealed record VendorOptimizationReport
{
    public string Vendor { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string DriverVersion { get; init; } = string.Empty;
    public string OpenCLVersion { get; init; } = string.Empty;
    public List<string> Extensions { get; init; } = new();
    public List<string> AvailableOptimizations { get; init; } = new();
    public List<string> VendorSpecificFeatures { get; init; } = new();
    public int RecommendedWorkGroupMultiple { get; init; }
}
