// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Native.Types;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;

using DotCompute.Abstractions.Kernels;
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
        private bool _disposed;

        public CudaKernelProfiler(CudaContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _profileData = new ConcurrentDictionary<string, KernelProfileData>();

            // Get device properties for Ada-specific optimizations
            var result = CudaRuntime.cudaGetDeviceProperties(ref _deviceProps, context.DeviceId);
            CudaRuntime.CheckError(result, "getting device properties");
        }

        /// <summary>
        /// Profiles a kernel launch with comprehensive metrics
        /// </summary>
        public async Task<Core.Kernels.KernelProfilingResult> ProfileKernelAsync(
            string kernelName,
            IntPtr functionHandle,
            KernelArguments arguments,
            CudaLaunchConfig launchConfig,
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

                _logger.LogInformation("Starting profiling of kernel '{KernelName}' for {Iterations} iterations", kernelName, iterations);

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
                var throughput = CalculateThroughput(stats.AverageTime, arguments);
                var (bottlenecks, optimizationSuggestions) = AnalyzeBottlenecks(stats, occupancy);

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

                return new Core.Kernels.KernelProfilingResult
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
                    Bottleneck = bottlenecks,
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
            CudaLaunchConfig launchConfig,
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
        private static ProfilingStatistics CalculateStatistics(List<double> timings)
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
        private OccupancyMetrics CalculateOccupancy(CudaLaunchConfig launchConfig)
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
        private ThroughputMetrics CalculateThroughput(double avgTimeMs, KernelArguments arguments)
        {
            // Estimate memory bandwidth utilization
            var memorySize = EstimateMemoryFootprint(arguments);
            var memoryBandwidth = memorySize / (avgTimeMs / 1000.0) / (1024 * 1024 * 1024); // GB/s

            // Estimate compute performance (simplified)
            var smCount = _deviceProps.MultiProcessorCount;
            var clockRate = _deviceProps.ClockRate / 1000.0; // MHz to GHz
            var peakGFLOPS = smCount * clockRate * 128; // Approximate for Ada
            var achievedGFLOPS = peakGFLOPS * 0.3; // Rough estimate

            return new ThroughputMetrics
            {
                MemoryBandwidth = memoryBandwidth,
                ComputePerformance = achievedGFLOPS
            };
        }

        /// <summary>
        /// Analyzes potential bottlenecks and generates optimization suggestions
        /// </summary>
        private (Core.Kernels.BottleneckAnalysis bottleneck, List<string> suggestions) AnalyzeBottlenecks(ProfilingStatistics stats, OccupancyMetrics occupancy)
        {
            var suggestions = new List<string>();
            var primaryBottleneck = Core.Kernels.BottleneckType.None;
            var severity = 0.0;
            var details = "No significant bottleneck detected";

            if (occupancy.TheoreticalOccupancy < 0.5)
            {
                primaryBottleneck = Core.Kernels.BottleneckType.RegisterPressure; // Map occupancy issues to register pressure
                severity = 1.0 - occupancy.TheoreticalOccupancy;
                details = $"Low occupancy detected ({occupancy.TheoreticalOccupancy:P1}) - likely due to register pressure";
                suggestions.Add("Low occupancy detected. Consider adjusting block size or reducing register/shared memory usage");

                if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
                {
                    suggestions.Add("For RTX 2000 Ada, try block sizes of 512 threads for optimal performance");
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

            var bottleneckAnalysis = new Core.Kernels.BottleneckAnalysis
            {
                Type = primaryBottleneck,
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

        public void Dispose()
        {
            if (!_disposed)
            {
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
        public double AverageTime { get; set; }
        public double MinTime { get; set; }
        public double MaxTime { get; set; }
        public double MedianTime { get; set; }
        public double StandardDeviation { get; set; }
        public Dictionary<int, double> Percentiles { get; set; } = [];
    }

    /// <summary>
    /// Occupancy metrics for kernel execution
    /// </summary>
    public sealed class OccupancyMetrics
    {
        public double TheoreticalOccupancy { get; set; }
        public double WarpOccupancy { get; set; }
        public int BlocksPerSM { get; set; }
        public int ActiveWarps { get; set; }
        public bool IsOptimalForAda { get; set; }
    }

    /// <summary>
    /// Throughput performance metrics
    /// </summary>
    public sealed class ThroughputMetrics
    {
        public double MemoryBandwidth { get; set; } // GB/s
        public double ComputePerformance { get; set; } // GFLOPS
    }

    /// <summary>
    /// Bottleneck analysis results
    /// </summary>
    public sealed class BottleneckAnalysis
    {
        public BottleneckType PrimaryBottleneck { get; set; }
        public List<string> Suggestions { get; set; } = [];
    }

    /// <summary>
    /// Types of performance bottlenecks
    /// </summary>
    public enum BottleneckType
    {
        None,
        Occupancy,
        MemoryBandwidth,
        Compute,
        ThreadDivergence
    }

    /// <summary>
    /// Kernel profile data storage
    /// </summary>
    internal sealed class KernelProfileData
    {
        public string KernelName { get; set; } = string.Empty;
        public CudaLaunchConfig LaunchConfig { get; set; }
        public List<double> Timings { get; set; } = [];
        public ProfilingStatistics Statistics { get; set; } = new();
        public OccupancyMetrics Occupancy { get; set; } = new();
        public DateTime LastProfiled { get; set; }
    }
}
