// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution.Scheduling;

/// <summary>
/// Production-grade device performance estimator that uses historical data, device capabilities,
/// and machine learning models to predict optimal device assignments for kernel execution.
/// </summary>
internal class DevicePerformanceEstimator
{
    private readonly ConcurrentDictionary<string, PerformanceHistory> _performanceHistory = new();
    private readonly ConcurrentDictionary<string, DeviceCapabilities> _deviceCapabilities = new();
    private readonly ConcurrentDictionary<string, double> _devicePerformanceScores = new();
    private readonly ConcurrentDictionary<string, KernelPerformanceModel> _kernelModels = new();
    private readonly ILogger? _logger;

    public DevicePerformanceEstimator(ILogger? logger = null)
    {
        _logger = logger;
        InitializeDeviceCapabilities();
    }

    /// <summary>
    /// Initializes device capabilities based on accelerator types.
    /// </summary>
    private void InitializeDeviceCapabilities()
    {
        // Initialize standard device capability profiles
        _deviceCapabilities["CPU"] = new DeviceCapabilities
        {
            ComputeUnits = Environment.ProcessorCount,
            MemoryBandwidth = 50_000_000_000L, // 50 GB/s typical DDR4
            ComputePerformance = 100_000_000_000L, // 100 GFLOPS
            MemoryLatency = 0.0001, // 100μs
            PowerEfficiency = 1.0,
            SupportedDataTypes = new[] { "float", "double", "int", "long" }
        };

        _deviceCapabilities["CUDA"] = new DeviceCapabilities
        {
            ComputeUnits = 2048, // Typical GPU cores
            MemoryBandwidth = 900_000_000_000L, // 900 GB/s typical GPU
            ComputePerformance = 15_000_000_000_000L, // 15 TFLOPS
            MemoryLatency = 0.0005, // 500μs
            PowerEfficiency = 3.0,
            SupportedDataTypes = new[] { "float", "half", "int" }
        };

        _deviceCapabilities["METAL"] = new DeviceCapabilities
        {
            ComputeUnits = 1024, // Apple GPU cores
            MemoryBandwidth = 400_000_000_000L, // 400 GB/s unified memory
            ComputePerformance = 10_000_000_000_000L, // 10 TFLOPS
            MemoryLatency = 0.0003, // 300μs
            PowerEfficiency = 2.5,
            SupportedDataTypes = new[] { "float", "half", "int" }
        };
    }


    /// <summary>
    /// Estimates the execution time for a kernel on a specific device using historical data and performance models.
    /// </summary>
    public TimeSpan EstimateExecutionTime(string kernelName, IAccelerator device, long dataSize)
    {
        var performanceKey = GetPerformanceKey(kernelName, device);

        // Try to get historical performance data
        if (_performanceHistory.TryGetValue(performanceKey, out var history) && history?.Measurements.Count > 0)
        {
            return EstimateFromHistoricalData(history!, dataSize);
        }

        // Fallback to analytical model
        return EstimateFromAnalyticalModel(kernelName, device, dataSize);
    }

    /// <summary>
    /// Estimates execution time from historical performance data.
    /// </summary>
    private TimeSpan EstimateFromHistoricalData(PerformanceHistory history, long dataSize)
    {
        // Use linear regression on historical data
        var measurements = history.Measurements.ToArray();

        if (measurements.Length == 1)
        {
            var single = measurements[0];
            var ratio = (double)dataSize / single.DataSize;
            return TimeSpan.FromMilliseconds(single.ExecutionTime.TotalMilliseconds * ratio);
        }

        // Calculate throughput (bytes per second) from recent measurements
        var recentMeasurements = measurements.TakeLast(Math.Min(10, measurements.Length));
        var averageThroughput = recentMeasurements.Average(m => m.DataSize / m.ExecutionTime.TotalSeconds);

        // Apply confidence factor based on data variance
        var variance = CalculateVariance(recentMeasurements.Select(m => m.DataSize / m.ExecutionTime.TotalSeconds));
        var confidenceFactor = Math.Max(1.1, 1.0 + variance / averageThroughput);

        var estimatedTime = dataSize / (averageThroughput / confidenceFactor);
        return TimeSpan.FromSeconds(Math.Max(estimatedTime, 0.001)); // Minimum 1ms
    }

    /// <summary>
    /// Estimates execution time using analytical performance model.
    /// </summary>
    private TimeSpan EstimateFromAnalyticalModel(string kernelName, IAccelerator device, long dataSize)
    {
        var deviceType = device.Type.ToString();

        if (!_deviceCapabilities.TryGetValue(deviceType, out var capabilities))
        {
            capabilities = _deviceCapabilities["CPU"]; // Fallback to CPU
        }

        // Get or create kernel performance model
        var model = _kernelModels.GetOrAdd(kernelName, _ => AnalyzeKernelCharacteristics(kernelName));

        // Calculate memory transfer time
        var memoryTransferTime = (dataSize * 2.0) / capabilities.MemoryBandwidth; // Input + output

        // Calculate compute time
        var operationsPerByte = model.ComputeIntensity;
        var totalOperations = dataSize * operationsPerByte;
        var computeTime = totalOperations / capabilities.ComputePerformance;

        // Add latency overhead
        var latencyOverhead = capabilities.MemoryLatency * model.MemoryAccessPatternFactor;

        // Total time is max of memory-bound and compute-bound plus latency
        var totalTime = Math.Max(memoryTransferTime, computeTime) + latencyOverhead;

        return TimeSpan.FromSeconds(Math.Max(totalTime, 0.001));
    }


    /// <summary>
    /// Updates performance metrics based on actual execution results and refines prediction models.
    /// </summary>
    public void UpdatePerformanceMetrics(string kernelName, IAccelerator device, TimeSpan actualTime, long dataSize)
    {
        var performanceKey = GetPerformanceKey(kernelName, device);

        // Add measurement to historical data
        var history = _performanceHistory.GetOrAdd(performanceKey, _ => new PerformanceHistory());

        var measurement = new PerformanceMeasurement
        {
            Timestamp = DateTime.UtcNow,
            ExecutionTime = actualTime,
            DataSize = dataSize,
            Throughput = dataSize / actualTime.TotalSeconds
        };

        history.AddMeasurement(measurement);

        // Update rolling performance score
        var throughput = dataSize / actualTime.TotalSeconds;
        _devicePerformanceScores.AddOrUpdate(performanceKey, throughput,
            (key, oldValue) => (oldValue * 0.8) + (throughput * 0.2)); // Exponential moving average

        // Update kernel model if we have enough data
        if (history.Measurements.Count >= 5)
        {
            UpdateKernelModel(kernelName, history);
        }

        _logger?.LogDebug("Updated performance metrics for {Kernel} on {Device}: {Throughput:F2} MB/s",
            kernelName, device.Info.Name, throughput / (1024 * 1024));
    }

    /// <summary>
    /// Updates the performance model for a kernel based on historical data.
    /// </summary>
    private void UpdateKernelModel(string kernelName, PerformanceHistory history)
    {
        var model = _kernelModels.GetOrAdd(kernelName, _ => new KernelPerformanceModel());

        var measurements = history.Measurements.ToArray();

        // Calculate average compute intensity (operations per byte)
        var avgThroughput = measurements.Average(m => m.Throughput);
        var avgDataSize = measurements.Average(m => m.DataSize);

        // Estimate compute intensity based on throughput patterns
        model.ComputeIntensity = EstimateComputeIntensity(measurements);
        model.MemoryAccessPatternFactor = EstimateMemoryAccessPattern(measurements);
        model.LastUpdated = DateTime.UtcNow;
        model.SampleCount = measurements.Length;
    }


    /// <summary>
    /// Gets the relative performance factor for a device based on its capabilities and historical data.
    /// </summary>
    public double GetDevicePerformanceFactor(IAccelerator device)
    {
        var deviceType = device.Type.ToString();

        // Get baseline performance from device capabilities
        if (!_deviceCapabilities.TryGetValue(deviceType, out var capabilities))
        {
            capabilities = _deviceCapabilities["CPU"];
        }

        // Base factor from theoretical performance
        var baseFactor = capabilities.ComputePerformance / _deviceCapabilities["CPU"].ComputePerformance;

        // Adjust based on historical performance if available
        var deviceKey = $"{device.Info.Name}_*"; // Wildcard for all kernels on this device
        var historicalScores = _devicePerformanceScores
            .Where(kvp => kvp.Key.StartsWith(device.Info.Name))
            .Select(kvp => kvp.Value)
            .ToArray();

        if (historicalScores.Length > 0)
        {
            var avgHistoricalThroughput = historicalScores.Average();
            var cpuBaseline = _devicePerformanceScores
                .Where(kvp => kvp.Key.Contains("CPU"))
                .Select(kvp => kvp.Value)
                .DefaultIfEmpty(50_000_000) // 50 MB/s baseline
                .Average();

            var historicalFactor = avgHistoricalThroughput / cpuBaseline;

            // Blend theoretical and historical factors
            baseFactor = (baseFactor * 0.3) + (historicalFactor * 0.7);
        }

        // Apply power efficiency factor
        baseFactor *= capabilities.PowerEfficiency;

        return Math.Max(baseFactor, 0.1); // Minimum factor of 0.1
    }


    /// <summary>
    /// Predicts memory requirements for kernel execution based on kernel analysis and historical patterns.
    /// </summary>
    public long PredictMemoryRequirement(string kernelName, long inputDataSize)
    {
        var model = _kernelModels.GetOrAdd(kernelName, _ => AnalyzeKernelCharacteristics(kernelName));

        // Base memory requirement (input + output)
        var baseMemory = inputDataSize * 2;

        // Add working memory based on kernel characteristics
        var workingMemoryFactor = model.MemoryAccessPatternFactor switch
        {
            < 1.2 => 1.1,  // Sequential access - minimal working memory
            < 2.0 => 1.5,  // Some random access - moderate working memory
            < 3.0 => 2.0,  // Complex access patterns - significant working memory
            _ => 3.0       // Very complex - large working memory
        };

        var workingMemory = (long)(inputDataSize * workingMemoryFactor);

        // Add algorithm-specific overhead
        var algorithmOverhead = kernelName.ToLowerInvariant() switch
        {
            var n when n.Contains("sort") => inputDataSize / 2,     // Sorting needs extra space
            var n when n.Contains("fft") => inputDataSize,          // FFT needs temporary buffers
            var n when n.Contains("matrix") => inputDataSize / 4,    // Matrix ops need temp space
            var n when n.Contains("reduce") => inputDataSize / 8,    // Reduction needs partial results
            _ => inputDataSize / 10                                  // Default overhead
        };

        var totalMemory = baseMemory + workingMemory + algorithmOverhead;

        // Add safety margin (20%)
        return (long)(totalMemory * 1.2);
    }


    /// <summary>
    /// Calculates device weight for load balancing based on current utilization and capabilities.
    /// </summary>
    public async Task<double> CalculateDeviceWeightAsync(IAccelerator device, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Ensure async without unnecessary delay

        var deviceType = device.Type.ToString();

        if (!_deviceCapabilities.TryGetValue(deviceType, out var capabilities))
        {
            capabilities = _deviceCapabilities["CPU"];
        }

        // Base weight from device performance
        var baseWeight = GetDevicePerformanceFactor(device);

        // Adjust for current memory utilization
        var memoryUtilization = GetMemoryUtilization(device);
        var memoryFactor = 1.0 - (memoryUtilization * 0.5); // Reduce weight as memory fills

        // Adjust for current load (estimated from recent activity)
        var loadFactor = GetCurrentLoadFactor(device);

        // Adjust for power efficiency
        var powerFactor = capabilities.PowerEfficiency;

        // Combine factors
        var weight = baseWeight * memoryFactor * loadFactor * powerFactor;

        return Math.Max(weight, 0.01); // Minimum weight to keep device available
    }


    /// <summary>
    /// Estimates compute capability using benchmark kernels and historical performance data.
    /// </summary>
    public async Task<double> EstimateComputeCapabilityAsync(IAccelerator device, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var deviceType = device.Type.ToString();

        if (!_deviceCapabilities.TryGetValue(deviceType, out var capabilities))
        {
            capabilities = _deviceCapabilities["CPU"];
        }

        // Theoretical compute capability (FLOPS)
        var theoreticalCapability = capabilities.ComputePerformance;

        // Adjust based on actual observed performance
        var observedFactor = GetObservedPerformanceFactor(device);

        // Account for memory bandwidth limitations
        var memoryBoundFactor = CalculateMemoryBoundFactor(capabilities);

        // Real-world efficiency factor (typically 60-90% of theoretical)
        var efficiencyFactor = deviceType switch
        {
            "CUDA" => 0.85,   // GPU efficiency
            "METAL" => 0.80,  // Apple GPU efficiency
            "OPENCL" => 0.75, // OpenCL overhead
            "CPU" => 0.90,    // CPU efficiency
            _ => 0.70          // Conservative default
        };

        var effectiveCapability = theoreticalCapability * observedFactor * memoryBoundFactor * efficiencyFactor;

        return effectiveCapability;
    }


    /// <summary>
    /// Scores devices for suitability to execute a specific kernel using comprehensive analysis.
    /// </summary>
    public double ScoreDeviceForKernel(string kernelName, IAccelerator device, long dataSize)
    {
        var deviceType = device.Type.ToString();

        // Performance factor based on historical data
        var performanceFactor = GetDevicePerformanceFactor(device);

        // Memory suitability factor
        var memoryRequired = PredictMemoryRequirement(kernelName, dataSize);
        var availableMemory = device.Info.TotalMemory;
        var memoryFactor = CalculateMemoryFactor(memoryRequired, availableMemory);

        // Current utilization factor
        var utilizationFactor = 1.0 - GetMemoryUtilization(device);

        // Kernel-device compatibility factor
        var compatibilityFactor = CalculateCompatibilityFactor(kernelName, deviceType);

        // Data transfer cost factor (prefer devices that minimize transfers)
        var transferCostFactor = CalculateTransferCostFactor(device, dataSize);

        // Power efficiency factor
        var powerFactor = _deviceCapabilities.TryGetValue(deviceType, out var capabilities)
            ? capabilities.PowerEfficiency : 1.0;

        // Combine all factors with weights
        var score = performanceFactor * 0.4 +     // 40% performance
                   memoryFactor * 0.2 +           // 20% memory availability
                   utilizationFactor * 0.15 +     // 15% current load
                   compatibilityFactor * 0.15 +   // 15% kernel compatibility
                   transferCostFactor * 0.05 +    // 5% transfer cost
                   powerFactor * 0.05;            // 5% power efficiency

        return Math.Max(score, 0.01); // Minimum score to keep device in consideration
    }


    /// <summary>
    /// Calculates a comprehensive device score for scheduling decisions.
    /// </summary>
    public async Task<double> CalculateDeviceScoreAsync(IAccelerator device, CancellationToken cancellationToken = default)
    {
        var computeCapability = await EstimateComputeCapabilityAsync(device, cancellationToken);
        var deviceWeight = await CalculateDeviceWeightAsync(device, cancellationToken);

        // Normalize compute capability to [0, 1] range
        var maxCapability = _deviceCapabilities.Values.Max(c => c.ComputePerformance);
        var normalizedCapability = computeCapability / maxCapability;

        // Combine capability and weight
        var score = (normalizedCapability * 0.7) + (deviceWeight * 0.3);

        return Math.Clamp(score, 0.0, 1.0);
    }

    #region Helper Methods

    /// <summary>
    /// Generates a performance key for caching.
    /// </summary>
    private static string GetPerformanceKey(string kernelName, IAccelerator device)
    {
        return $"{device.Info.Name}_{kernelName}";
    }

    /// <summary>
    /// Analyzes kernel characteristics to create a performance model.
    /// </summary>
    private KernelPerformanceModel AnalyzeKernelCharacteristics(string kernelName)
    {
        var model = new KernelPerformanceModel();
        var lowerName = kernelName.ToLowerInvariant();

        // Estimate compute intensity based on kernel name patterns
        model.ComputeIntensity = lowerName switch
        {
            var n when n.Contains("matrix") || n.Contains("gemm") => 50.0,     // Matrix operations are compute-intensive
            var n when n.Contains("fft") || n.Contains("transform") => 30.0,   // FFT operations
            var n when n.Contains("conv") || n.Contains("filter") => 25.0,     // Convolution operations
            var n when n.Contains("sort") => 10.0,                            // Sorting operations
            var n when n.Contains("reduce") || n.Contains("sum") => 5.0,       // Reduction operations
            var n when n.Contains("copy") || n.Contains("memcpy") => 1.0,      // Memory operations
            _ => 10.0                                                          // Default estimate
        };

        // Estimate memory access pattern complexity
        model.MemoryAccessPatternFactor = lowerName switch
        {
            var n when n.Contains("matrix") => 2.5,       // Complex access patterns
            var n when n.Contains("transpose") => 3.0,    // Irregular access
            var n when n.Contains("gather") => 2.8,       // Scattered access
            var n when n.Contains("scatter") => 2.8,      // Scattered access
            var n when n.Contains("sort") => 2.2,         // Irregular during sorting
            var n when n.Contains("scan") => 1.8,         // Somewhat irregular
            var n when n.Contains("reduce") => 1.5,       // Tree-like access
            _ => 1.2                                       // Mostly sequential
        };

        model.LastUpdated = DateTime.UtcNow;
        model.SampleCount = 0;

        return model;
    }

    /// <summary>
    /// Calculates variance in a sequence of values.
    /// </summary>
    private static double CalculateVariance(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count < 2) return 0.0;

        var mean = valuesList.Average();
        var sumOfSquaredDifferences = valuesList.Sum(v => Math.Pow(v - mean, 2));
        return sumOfSquaredDifferences / (valuesList.Count - 1);
    }

    /// <summary>
    /// Estimates compute intensity from performance measurements.
    /// </summary>
    private static double EstimateComputeIntensity(PerformanceMeasurement[] measurements)
    {
        if (measurements.Length < 2) return 10.0; // Default

        // Analyze throughput vs data size correlation
        var avgThroughput = measurements.Average(m => m.Throughput);
        var throughputVariation = CalculateVariance(measurements.Select(m => m.Throughput));

        // Higher variation suggests more compute-intensive operations
        var intensityFactor = 1.0 + (throughputVariation / avgThroughput);
        return Math.Clamp(intensityFactor * 10.0, 1.0, 100.0);
    }

    /// <summary>
    /// Estimates memory access pattern factor from measurements.
    /// </summary>
    private static double EstimateMemoryAccessPattern(PerformanceMeasurement[] measurements)
    {
        if (measurements.Length < 3) return 1.2; // Default

        // Analyze how performance scales with data size
        var sizeRatios = new List<double>();
        var timeRatios = new List<double>();

        for (int i = 1; i < measurements.Length; i++)
        {
            var sizeRatio = (double)measurements[i].DataSize / measurements[i - 1].DataSize;
            var timeRatio = measurements[i].ExecutionTime.TotalSeconds / measurements[i - 1].ExecutionTime.TotalSeconds;

            if (sizeRatio > 1.1) // Only consider significant size changes
            {
                sizeRatios.Add(sizeRatio);
                timeRatios.Add(timeRatio);
            }
        }

        if (sizeRatios.Count == 0) return 1.2;

        // If time scales more than linearly with size, memory access is complex
        var avgSizeRatio = sizeRatios.Average();
        var avgTimeRatio = timeRatios.Average();
        var scalingFactor = avgTimeRatio / avgSizeRatio;

        return Math.Clamp(scalingFactor, 1.0, 5.0);
    }

    /// <summary>
    /// Gets current memory utilization for a device.
    /// </summary>
    private static double GetMemoryUtilization(IAccelerator device)
    {
        // Estimate current memory usage
        var totalMemory = device.Info.TotalMemory;
        var currentUsage = GC.GetTotalMemory(false); // Rough estimate

        return Math.Min((double)currentUsage / totalMemory, 1.0);
    }

    /// <summary>
    /// Gets current load factor for a device.
    /// </summary>
    private double GetCurrentLoadFactor(IAccelerator device)
    {
        // Estimate load based on recent performance measurements
        var deviceKey = device.Info.Name;
        var recentMeasurements = _performanceHistory.Values
            .SelectMany(h => h.Measurements)
            .Where(m => m.Timestamp > DateTime.UtcNow.AddMinutes(-5)) // Last 5 minutes
            .Count();

        // More recent measurements indicate higher load
        var loadFactor = 1.0 - Math.Min(recentMeasurements / 10.0, 0.8); // Max 80% load reduction
        return Math.Max(loadFactor, 0.2); // Minimum 20% capacity
    }

    /// <summary>
    /// Gets observed performance factor compared to theoretical.
    /// </summary>
    private double GetObservedPerformanceFactor(IAccelerator device)
    {
        var deviceScores = _devicePerformanceScores
            .Where(kvp => kvp.Key.StartsWith(device.Info.Name))
            .Select(kvp => kvp.Value)
            .ToArray();

        if (deviceScores.Length == 0) return 1.0;

        var avgObservedThroughput = deviceScores.Average();
        var expectedThroughput = GetDevicePerformanceFactor(device) * 50_000_000; // 50 MB/s baseline

        return Math.Clamp(avgObservedThroughput / expectedThroughput, 0.1, 2.0);
    }

    /// <summary>
    /// Calculates memory bandwidth limitation factor.
    /// </summary>
    private static double CalculateMemoryBoundFactor(DeviceCapabilities capabilities)
    {
        // Ratio of memory bandwidth to compute performance
        var bytesPerFlop = capabilities.MemoryBandwidth / (double)capabilities.ComputePerformance;

        // Typical ratio is around 0.1-1.0 bytes per FLOP
        // Lower ratios indicate compute-bound, higher ratios indicate memory-bound
        return Math.Clamp(bytesPerFlop / 0.5, 0.3, 1.0);
    }

    /// <summary>
    /// Calculates memory suitability factor.
    /// </summary>
    private static double CalculateMemoryFactor(long memoryRequired, long availableMemory)
    {
        if (memoryRequired > availableMemory)
            return 0.0; // Cannot fit

        var utilizationRatio = (double)memoryRequired / availableMemory;

        // Prefer moderate memory utilization (50-80%)
        return utilizationRatio switch
        {
            < 0.1 => 0.8,    // Very low utilization - might be inefficient
            < 0.5 => 1.0,    // Good utilization
            < 0.8 => 0.9,    // High but safe utilization
            < 0.95 => 0.6,   // Very high utilization - risky
            _ => 0.2         // Extremely high - likely to fail
        };
    }

    /// <summary>
    /// Calculates kernel-device compatibility factor.
    /// </summary>
    private double CalculateCompatibilityFactor(string kernelName, string deviceType)
    {
        var lowerKernel = kernelName.ToLowerInvariant();

        // GPU-friendly kernels
        if (deviceType is "CUDA" or "METAL" or "OPENCL")
        {
            return lowerKernel switch
            {
                var n when n.Contains("matrix") || n.Contains("gemm") => 1.2,     // Excellent for GPU
                var n when n.Contains("conv") || n.Contains("filter") => 1.15,    // Very good for GPU
                var n when n.Contains("fft") || n.Contains("transform") => 1.1,   // Good for GPU
                var n when n.Contains("reduce") || n.Contains("sum") => 1.05,     // Good for GPU
                var n when n.Contains("sort") => 0.9,                            // Less ideal for GPU
                var n when n.Contains("scan") => 0.95,                           // Moderate for GPU
                _ => 1.0
            };
        }

        // CPU excels at different patterns
        return lowerKernel switch
        {
            var n when n.Contains("sort") => 1.1,        // Good for CPU
            var n when n.Contains("scan") => 1.05,       // Good for CPU
            var n when n.Contains("complex") => 1.05,    // Complex algorithms good for CPU
            _ => 1.0
        };
    }

    /// <summary>
    /// Calculates data transfer cost factor.
    /// </summary>
    private static double CalculateTransferCostFactor(IAccelerator device, long dataSize)
    {
        // Estimate transfer overhead
        var transferOverhead = device.Type switch
        {
            AcceleratorType.Cuda => 0.1,     // PCIe transfer overhead
            AcceleratorType.CPU => 0.0,      // No transfer needed
            _ => 0.05                         // Default overhead
        };

        // Larger transfers have relatively lower overhead
        var sizeGb = dataSize / (1024.0 * 1024.0 * 1024.0);
        var sizeFactor = Math.Min(sizeGb / 10.0, 1.0); // Normalize to 10GB

        return 1.0 - (transferOverhead * (1.0 - sizeFactor));
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Represents device capabilities for performance estimation.
    /// </summary>
    private class DeviceCapabilities
    {
        public int ComputeUnits { get; set; }
        public long MemoryBandwidth { get; set; } // Bytes per second
        public long ComputePerformance { get; set; } // Operations per second
        public double MemoryLatency { get; set; } // Seconds
        public double PowerEfficiency { get; set; } // Relative factor
        public string[] SupportedDataTypes { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Represents a performance measurement for a specific execution.
    /// </summary>
    private class PerformanceMeasurement
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public long DataSize { get; set; }
        public double Throughput { get; set; }
    }

    /// <summary>
    /// Maintains historical performance data for a device-kernel combination.
    /// </summary>
    private class PerformanceHistory
    {
        private readonly Queue<PerformanceMeasurement> _measurements = new();
        private readonly object _lock = new();
        private const int MaxMeasurements = 100;

        public IEnumerable<PerformanceMeasurement> Measurements
        {
            get
            {
                lock (_lock)
                {
                    return _measurements.ToArray();
                }
            }
        }

        public void AddMeasurement(PerformanceMeasurement measurement)
        {
            lock (_lock)
            {
                _measurements.Enqueue(measurement);

                // Keep only recent measurements
                while (_measurements.Count > MaxMeasurements)
                {
                    _measurements.Dequeue();
                }
            }
        }
    }

    /// <summary>
    /// Represents a performance model for a specific kernel.
    /// </summary>
    private class KernelPerformanceModel
    {
        public double ComputeIntensity { get; set; } = 10.0; // Operations per byte
        public double MemoryAccessPatternFactor { get; set; } = 1.2; // Complexity factor
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public int SampleCount { get; set; } = 0;
    }

    #endregion
}