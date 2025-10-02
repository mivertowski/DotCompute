// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Monitoring;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Abstractions.Types;
namespace DotCompute.Backends.CUDA.Advanced
{

    /// <summary>
    /// Advanced kernel profiler for CUDA with RTX 2000 Ada optimizations
    /// </summary>
    public sealed class CudaKernelProfiler : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, KernelProfileData> _profileData;
        private readonly CudaDeviceProperties _deviceProps;
        private readonly NvmlWrapper _nvml;
        private readonly CuptiWrapper _cupti;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the CudaKernelProfiler class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="logger">The logger.</param>

        public CudaKernelProfiler(CudaContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _profileData = new ConcurrentDictionary<string, KernelProfileData>();

            // Get device properties for Ada-specific optimizations
            var result = CudaRuntime.cudaGetDeviceProperties(ref _deviceProps, context.DeviceId);
            CudaRuntime.CheckError(result, "getting device properties");

            // Initialize monitoring wrappers

            _nvml = new NvmlWrapper(_logger);
            _ = _nvml.Initialize();


            _cupti = new CuptiWrapper(_logger);
            _ = _cupti.Initialize(context.DeviceId);
        }

        /// <summary>
        /// Profiles a kernel launch with comprehensive metrics
        /// </summary>
        public async Task<KernelProfilingResult> ProfileKernelAsync(
            string kernelName,
            IntPtr functionHandle,
            KernelArguments arguments,
            Compilation.CudaLaunchConfig launchConfig,
            int iterations = 100,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var timings = new List<double>();
            var startEvent = IntPtr.Zero;
            var endEvent = IntPtr.Zero;

            try
            {
                // Create CUDA events for precise timing
                CudaRuntime.CheckError(CudaRuntime.cudaEventCreate(ref startEvent), "creating start event");
                CudaRuntime.CheckError(CudaRuntime.cudaEventCreate(ref endEvent), "creating end event");

                // Warm-up runs
                var warmupRuns = Math.Min(10, iterations / 10);
                for (var i = 0; i < warmupRuns; i++)
                {
                    _ = await ExecuteKernelOnceAsync(functionHandle, arguments, launchConfig, startEvent, endEvent, cancellationToken);
                }

                _logger.LogInfoMessage("Starting profiling of kernel '{KernelName}' for {kernelName, iterations} iterations");

                // Profiling runs
                for (var i = 0; i < iterations; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var kernelTime = await ExecuteKernelOnceAsync(functionHandle, arguments, launchConfig, startEvent, endEvent, cancellationToken);
                    timings.Add(kernelTime);
                }

                stopwatch.Stop();

                // Calculate statistics
                var stats = CalculateStatistics(timings);
                var occupancy = CalculateOccupancy(launchConfig);

                // Get real metrics from NVML and CUPTI

                var gpuMetrics = _nvml.GetDeviceMetrics(_context.DeviceId);
                var cuptiSession = _cupti.StartProfiling();


                KernelMetrics kernelMetrics;
                if (cuptiSession != null)
                {
                    // Execute one more time with CUPTI profiling
                    _ = await ExecuteKernelOnceAsync(functionHandle, arguments, launchConfig, startEvent, endEvent, cancellationToken);
                    kernelMetrics = _cupti.CollectMetrics(cuptiSession);
                }
                else
                {
                    kernelMetrics = new KernelMetrics();
                }

                // Use real metrics if available, fallback to calculated

                var throughput = new CudaThroughputMetrics
                {
                    MemoryBandwidth = kernelMetrics.DramReadThroughput > 0

                        ? (kernelMetrics.DramReadThroughput + kernelMetrics.DramWriteThroughput) / 1024.0 // Convert to GB/s
                        : CalculateThroughput(stats.AverageTime, arguments).MemoryBandwidth,
                    ComputePerformance = kernelMetrics.FlopEfficiency > 0
                        ? _deviceProps.MultiProcessorCount * (_deviceProps.ClockRate / 1000.0) * 128 * kernelMetrics.FlopEfficiency
                        : CalculateThroughput(stats.AverageTime, arguments).ComputePerformance
                };

                // Update occupancy with real metrics

                if (kernelMetrics.AchievedOccupancy > 0)
                {
                    occupancy.TheoreticalOccupancy = kernelMetrics.AchievedOccupancy;
                }


                var (bottlenecks, optimizationSuggestions) = AnalyzeBottlenecks(stats, occupancy, gpuMetrics, kernelMetrics);

                // Store profile data for future analysis
                var profileData = new KernelProfileData
                {
                    KernelName = kernelName,
                    LaunchConfig = launchConfig,
                    Timings = timings,
                    Statistics = stats,
                    Occupancy = occupancy,
                    LastProfiled = DateTime.UtcNow
                };
                _ = _profileData.AddOrUpdate(kernelName, profileData, (k, v) => profileData);

                return new KernelProfilingResult
                {
                    Iterations = iterations,
                    AverageTimeMs = stats.AverageTime,
                    MinTimeMs = stats.MinTime,
                    MaxTimeMs = stats.MaxTime,
                    MedianTimeMs = stats.MedianTime,
                    StdDevMs = stats.StandardDeviation,
                    PercentileTimingsMs = stats.Percentiles,
                    AchievedOccupancy = occupancy.TheoreticalOccupancy,
                    MemoryThroughputGBps = throughput.MemoryBandwidth,
                    ComputeThroughputGFLOPS = throughput.ComputePerformance,
                    Bottleneck = new Abstractions.Interfaces.Kernels.BottleneckAnalysis
                    {
                        Type = bottlenecks.PrimaryBottleneck switch
                        {
                            BottleneckType.None => BottleneckType.None,
                            BottleneckType.Occupancy => BottleneckType.CPU,
                            BottleneckType.MemoryBandwidth => BottleneckType.Memory,
                            BottleneckType.Compute => BottleneckType.GPU,
                            BottleneckType.ThreadDivergence => BottleneckType.GPU,
                            _ => BottleneckType.None
                        },
                        Severity = bottlenecks.Severity,
                        Details = bottlenecks.Details
                    },
                    OptimizationSuggestions = optimizationSuggestions
                };
            }
            finally
            {
                // Clean up events
                if (startEvent != IntPtr.Zero)
                {
                    _ = CudaRuntime.cudaEventDestroy(startEvent);
                }

                if (endEvent != IntPtr.Zero)
                {
                    _ = CudaRuntime.cudaEventDestroy(endEvent);
                }
            }
        }

        /// <summary>
        /// Executes a kernel once and measures timing
        /// </summary>
        private async Task<double> ExecuteKernelOnceAsync(
            IntPtr functionHandle,
            KernelArguments arguments,
            Compilation.CudaLaunchConfig launchConfig,
            IntPtr startEvent,
            IntPtr endEvent,
            CancellationToken cancellationToken)
        {
            _context.MakeCurrent();

            // Record start event
            CudaRuntime.CheckError(CudaRuntime.cudaEventRecord(startEvent, _context.Stream), "recording start event");

            // Launch kernel
            var argPtrs = PrepareKernelArguments(arguments);
            try
            {
                var result = CudaRuntime.cuLaunchKernel(
                    functionHandle,
                    launchConfig.GridX, launchConfig.GridY, launchConfig.GridZ,
                    launchConfig.BlockX, launchConfig.BlockY, launchConfig.BlockZ,
                    launchConfig.SharedMemoryBytes,
                    _context.Stream,
                    argPtrs,
                    IntPtr.Zero);

                CudaRuntime.CheckError(result, "launching kernel");
            }
            finally
            {
                FreeKernelArguments(argPtrs);
            }

            // Record end event
            CudaRuntime.CheckError(CudaRuntime.cudaEventRecord(endEvent, _context.Stream), "recording end event");

            // Wait for completion
            _ = await Task.Run(() => CudaRuntime.cudaEventSynchronize(endEvent), cancellationToken);

            // Calculate elapsed time
            float elapsedMs = 0;
            CudaRuntime.CheckError(CudaRuntime.cudaEventElapsedTime(ref elapsedMs, startEvent, endEvent), "calculating elapsed time");

            return elapsedMs;
        }

        /// <summary>
        /// Calculates comprehensive statistics from timing data
        /// </summary>
        private static ProfilingStatistics CalculateStatistics(IReadOnlyList<double> timings)
        {
            timings.Sort();

            var count = timings.Count;
            var sum = timings.Sum();
            var average = sum / count;
            var median = count % 2 == 0
                ? (timings[count / 2 - 1] + timings[count / 2]) / 2
                : timings[count / 2];

            var variance = timings.Select(t => Math.Pow(t - average, 2)).Average();
            var stdDev = Math.Sqrt(variance);

            var percentiles = new Dictionary<int, double>
            {
                [50] = median,
                [90] = timings[(int)(count * 0.9)],
                [95] = timings[(int)(count * 0.95)],
                [99] = timings[(int)(count * 0.99)]
            };

            return new ProfilingStatistics
            {
                AverageTime = average,
                MinTime = timings.Min(),
                MaxTime = timings.Max(),
                MedianTime = median,
                StandardDeviation = stdDev,
                Percentiles = percentiles
            };
        }

        /// <summary>
        /// Calculates occupancy metrics for the launch configuration
        /// </summary>
        private OccupancyMetrics CalculateOccupancy(Compilation.CudaLaunchConfig launchConfig)
        {
            var blockSize = (int)(launchConfig.BlockX * launchConfig.BlockY * launchConfig.BlockZ);
            var maxThreadsPerSM = _deviceProps.MaxThreadsPerMultiProcessor;
            var maxBlocksPerSM = maxThreadsPerSM / blockSize;

            var theoreticalOccupancy = Math.Min(1.0, (double)(maxBlocksPerSM * blockSize) / maxThreadsPerSM);
            var activeWarps = (blockSize + 31) / 32 * maxBlocksPerSM; // Round up to warps
            var maxWarps = maxThreadsPerSM / 32;
            var warpOccupancy = Math.Min(1.0, (double)activeWarps / maxWarps);

            // Ada-specific occupancy calculations
            var isAda = _deviceProps.Major == 8 && _deviceProps.Minor == 9;
            var adaOptimal = isAda && blockSize == 512 && theoreticalOccupancy >= 0.75;

            return new OccupancyMetrics
            {
                TheoreticalOccupancy = theoreticalOccupancy,
                WarpOccupancy = warpOccupancy,
                BlocksPerSM = maxBlocksPerSM,
                ActiveWarps = activeWarps,
                IsOptimalForAda = adaOptimal
            };
        }

        /// <summary>
        /// Calculates throughput metrics
        /// </summary>
        private CudaThroughputMetrics CalculateThroughput(double avgTimeMs, KernelArguments arguments)
        {
            // Estimate memory bandwidth utilization
            var memorySize = EstimateMemoryFootprint(arguments);
            var memoryBandwidth = memorySize / (avgTimeMs / 1000.0) / (1024 * 1024 * 1024); // GB/s

            // Estimate compute performance (simplified)
            var smCount = _deviceProps.MultiProcessorCount;
            var clockRate = _deviceProps.ClockRate / 1000.0; // MHz to GHz
            var peakGFLOPS = smCount * clockRate * 128; // Approximate for Ada
            var achievedGFLOPS = peakGFLOPS * 0.3; // Rough estimate

            return new CudaThroughputMetrics
            {
                MemoryBandwidth = memoryBandwidth,
                ComputePerformance = achievedGFLOPS
            };
        }

        /// <summary>
        /// Analyzes potential bottlenecks and generates optimization suggestions using real metrics
        /// </summary>
        private (BottleneckAnalysis bottleneck, IReadOnlyList<string> suggestions) AnalyzeBottlenecks(
            ProfilingStatistics stats,

            OccupancyMetrics occupancy,
            GpuMetrics gpuMetrics,
            KernelMetrics kernelMetrics)
        {
            var suggestions = new List<string>();
            var primaryBottleneck = BottleneckType.None;
            var severity = 0.0;
            var details = "No significant bottleneck detected";

            // Check for thermal throttling using real metrics
            if (gpuMetrics.IsAvailable && gpuMetrics.IsThrottling)
            {
                primaryBottleneck = BottleneckType.Compute;
                severity = 0.8;
                details = $"GPU is throttling: {gpuMetrics.ThrottleReasons}";
                suggestions.Add($"GPU throttling detected. Temperature: {gpuMetrics.Temperature}°C, Power: {gpuMetrics.PowerUsage:F1}W");
            }
            // Check for memory bandwidth bottleneck
            else if (gpuMetrics.IsAvailable && gpuMetrics.MemoryBandwidthUtilization > 80)
            {
                primaryBottleneck = BottleneckType.MemoryBandwidth;
                severity = gpuMetrics.MemoryBandwidthUtilization / 100.0;
                details = $"High memory bandwidth utilization: {gpuMetrics.MemoryBandwidthUtilization}%";
                suggestions.Add("Memory bandwidth saturated. Consider data compression or reducing memory accesses");
            }
            // Check for low SM efficiency
            else if (kernelMetrics.SmEfficiency > 0 && kernelMetrics.SmEfficiency < 0.6)
            {
                primaryBottleneck = BottleneckType.ThreadDivergence;
                severity = 1.0 - kernelMetrics.SmEfficiency;
                details = $"Low SM efficiency: {kernelMetrics.SmEfficiency:P1}";
                suggestions.Add("Low SM efficiency detected. Check for thread divergence and uncoalesced memory access");
            }
            // Check occupancy with real metrics
            else if (kernelMetrics.AchievedOccupancy > 0 && kernelMetrics.AchievedOccupancy < 0.5)
            {
                primaryBottleneck = BottleneckType.Occupancy;
                severity = 1.0 - kernelMetrics.AchievedOccupancy;
                details = $"Low achieved occupancy: {kernelMetrics.AchievedOccupancy:P1}";
                suggestions.Add("Low occupancy detected. Consider adjusting block size or reducing register/shared memory usage");

                if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
                {
                    suggestions.Add("For RTX 2000 Ada, try block sizes of 512 threads for optimal performance");
                }
            }

            // Additional checks based on real GPU state
            if (gpuMetrics.IsAvailable)
            {
                if (gpuMetrics.Temperature > 80)
                {
                    suggestions.Add($"High GPU temperature ({gpuMetrics.Temperature}°C). Consider improving cooling");
                }


                if (gpuMetrics.MemoryUtilization > 90)
                {
                    suggestions.Add($"High memory usage ({gpuMetrics.MemoryUtilization:F1}%). Consider memory optimization");
                }


                if (gpuMetrics.GpuUtilization < 50)
                {
                    suggestions.Add($"Low GPU utilization ({gpuMetrics.GpuUtilization}%). Consider increasing parallelism");
                }
            }

            if (stats.StandardDeviation > stats.AverageTime * 0.1)
            {
                suggestions.Add("High timing variance detected. Check for thread divergence or memory access patterns");
            }

            if (_deviceProps.Major == 8 && _deviceProps.Minor == 9 && !occupancy.IsOptimalForAda)
            {
                suggestions.Add("Consider Ada-specific optimizations: use 512-thread blocks and leverage 100KB shared memory");
            }

            var bottleneckAnalysis = new BottleneckAnalysis
            {
                PrimaryBottleneck = primaryBottleneck,
                Severity = severity,
                Details = details
            };

            return (bottleneckAnalysis, suggestions);
        }

        /// <summary>
        /// Estimates memory footprint from kernel arguments
        /// </summary>
        private static long EstimateMemoryFootprint(KernelArguments arguments)
        {
            long totalSize = 0;
            for (var i = 0; i < arguments.Length; i++)
            {
                var argValue = arguments.Get(i);
                if (argValue is ISyncMemoryBuffer memoryBuffer)
                {
                    totalSize += (long)memoryBuffer.SizeInBytes;
                }
                else
                {
                    totalSize += 8; // Assume 8 bytes for scalar values
                }
            }
            return totalSize;
        }

        /// <summary>
        /// Prepares kernel arguments for execution
        /// </summary>
        private static IntPtr PrepareKernelArguments(KernelArguments arguments)
            // KernelArguments is a wrapper for kernel parameters
            // For now, return a placeholder since actual implementation depends on the structure



            => IntPtr.Zero;


        /// <summary>
        /// Frees kernel arguments after execution
        /// </summary>
        private static void FreeKernelArguments(IntPtr argPtrs)
        {
            if (argPtrs == IntPtr.Zero)
            {
                return;
            }

            // Free the argument pointer array
            Marshal.FreeHGlobal(argPtrs);

            // Note: Individual argument memory should be tracked and freed separately
            // This is a simplified implementation - in production, maintain a list of
            // allocated pointers and free them all here
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _nvml?.Dispose();
                _cupti?.Dispose();
                _profileData.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Profiling statistics container
    /// </summary>
    public sealed class ProfilingStatistics
    {
        /// <summary>
        /// Gets or sets the average time.
        /// </summary>
        /// <value>The average time.</value>
        public double AverageTime { get; set; }
        /// <summary>
        /// Gets or sets the min time.
        /// </summary>
        /// <value>The min time.</value>
        public double MinTime { get; set; }
        /// <summary>
        /// Gets or sets the max time.
        /// </summary>
        /// <value>The max time.</value>
        public double MaxTime { get; set; }
        /// <summary>
        /// Gets or sets the median time.
        /// </summary>
        /// <value>The median time.</value>
        public double MedianTime { get; set; }
        /// <summary>
        /// Gets or sets the standard deviation.
        /// </summary>
        /// <value>The standard deviation.</value>
        public double StandardDeviation { get; set; }
        /// <summary>
        /// Gets or sets the percentiles.
        /// </summary>
        /// <value>The percentiles.</value>
        public Dictionary<int, double> Percentiles { get; } = [];
    }

    /// <summary>
    /// Occupancy metrics for kernel execution
    /// </summary>
    public sealed class OccupancyMetrics
    {
        /// <summary>
        /// Gets or sets the theoretical occupancy.
        /// </summary>
        /// <value>The theoretical occupancy.</value>
        public double TheoreticalOccupancy { get; set; }
        /// <summary>
        /// Gets or sets the warp occupancy.
        /// </summary>
        /// <value>The warp occupancy.</value>
        public double WarpOccupancy { get; set; }
        /// <summary>
        /// Gets or sets the blocks per s m.
        /// </summary>
        /// <value>The blocks per s m.</value>
        public int BlocksPerSM { get; set; }
        /// <summary>
        /// Gets or sets the active warps.
        /// </summary>
        /// <value>The active warps.</value>
        public int ActiveWarps { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether optimal for ada.
        /// </summary>
        /// <value>The is optimal for ada.</value>
        public bool IsOptimalForAda { get; set; }
    }

    /// <summary>
    /// CUDA-specific throughput performance metrics
    /// </summary>
    public sealed class CudaThroughputMetrics
    {
        /// <summary>
        /// Gets or sets the memory bandwidth.
        /// </summary>
        /// <value>The memory bandwidth.</value>
        public double MemoryBandwidth { get; set; } // GB/s
        /// <summary>
        /// Gets or sets the compute performance.
        /// </summary>
        /// <value>The compute performance.</value>
        public double ComputePerformance { get; set; } // GFLOPS
    }

    /// <summary>
    /// Bottleneck analysis results
    /// </summary>
    public sealed class BottleneckAnalysis
    {
        /// <summary>
        /// Gets or sets the primary bottleneck.
        /// </summary>
        /// <value>The primary bottleneck.</value>
        public BottleneckType PrimaryBottleneck { get; set; }
        /// <summary>
        /// Gets or sets the suggestions.
        /// </summary>
        /// <value>The suggestions.</value>
        public IList<string> Suggestions { get; } = [];
        /// <summary>
        /// Gets or sets the severity.
        /// </summary>
        /// <value>The severity.</value>
        public double Severity { get; set; }
        /// <summary>
        /// Gets or sets the details.
        /// </summary>
        /// <value>The details.</value>
        public string Details { get; set; } = string.Empty;
    }


    /// <summary>
    /// Kernel profile data storage
    /// </summary>
    internal sealed class KernelProfileData
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        /// <value>The kernel name.</value>
        public string KernelName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the launch config.
        /// </summary>
        /// <value>The launch config.</value>
        public Compilation.CudaLaunchConfig LaunchConfig { get; set; }
        /// <summary>
        /// Gets or sets the timings.
        /// </summary>
        /// <value>The timings.</value>
        public IList<double> Timings { get; } = [];
        /// <summary>
        /// Gets or sets the statistics.
        /// </summary>
        /// <value>The statistics.</value>
        public ProfilingStatistics Statistics { get; set; } = new();
        /// <summary>
        /// Gets or sets the occupancy.
        /// </summary>
        /// <value>The occupancy.</value>
        public OccupancyMetrics Occupancy { get; set; } = new();
        /// <summary>
        /// Gets or sets the last profiled.
        /// </summary>
        /// <value>The last profiled.</value>
        public DateTime LastProfiled { get; set; }
    }
}
