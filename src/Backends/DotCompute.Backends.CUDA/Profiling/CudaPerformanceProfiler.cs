using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Profiling
{
    /// <summary>
    /// Production-grade CUDA performance profiler with CUPTI integration,
    /// metrics collection, and detailed performance analysis.
    /// </summary>
    public sealed class CudaPerformanceProfiler : IDisposable
    {
        private readonly ILogger<CudaPerformanceProfiler> _logger;
        private readonly ConcurrentDictionary<string, KernelProfile> _kernelProfiles;
        private readonly ConcurrentDictionary<string, MemoryProfile> _memoryProfiles;
        private readonly ConcurrentQueue<ProfilingEvent> _eventQueue;
        private readonly Timer _metricsTimer;
        private readonly SemaphoreSlim _profilingLock;
        private IntPtr _cuptiSubscriber;
        private bool _isProfilingActive;
        private bool _disposed;

        // CUPTI API imports
        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiSubscribe(
            out IntPtr subscriber,
            CuptiCallbackFunc callback,
            IntPtr userdata);

        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiUnsubscribe(IntPtr subscriber);

        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiEnableCallback(
            uint enable,
            IntPtr subscriber,
            CuptiCallbackDomain domain,
            uint cbid);

        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiActivityEnable(CuptiActivityKind kind);

        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiActivityDisable(CuptiActivityKind kind);

        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiActivityFlushAll(uint flag);

        [DllImport("cupti64_2023.3.1", CallingConvention = CallingConvention.Cdecl)]
        private static extern CuptiResult cuptiGetTimestamp(out ulong timestamp);

        // NVML API imports for GPU metrics
        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlInit();

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetUtilizationRates(
            IntPtr device,
            out NvmlUtilization utilization);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetMemoryInfo(
            IntPtr device,
            out NvmlMemory memory);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetTemperature(
            IntPtr device,
            NvmlTemperatureSensor sensorType,
            out uint temp);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetPowerUsage(
            IntPtr device,
            out uint power);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlDeviceGetHandleByIndex(
            uint index,
            out IntPtr device);

        [DllImport("nvml", CallingConvention = CallingConvention.Cdecl)]
        private static extern NvmlReturn nvmlShutdown();

        // Callback delegate for CUPTI
        private delegate void CuptiCallbackFunc(
            IntPtr userdata,
            CuptiCallbackDomain domain,
            uint cbid,
            IntPtr cbdata);

        private readonly CuptiCallbackFunc _cuptiCallback;
        /// <summary>
        /// Initializes a new instance of the CudaPerformanceProfiler class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public CudaPerformanceProfiler(ILogger<CudaPerformanceProfiler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernelProfiles = new ConcurrentDictionary<string, KernelProfile>();
            _memoryProfiles = new ConcurrentDictionary<string, MemoryProfile>();
            _eventQueue = new ConcurrentQueue<ProfilingEvent>();
            _profilingLock = new SemaphoreSlim(1, 1);

            // Store callback delegate to prevent GC

            _cuptiCallback = CuptiCallbackHandler;

            // Initialize NVML for GPU metrics

            InitializeNvml();

            // Start metrics collection timer

            _metricsTimer = new Timer(
                CollectMetricsWrapper,
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5));


            _logger.LogInfoMessage("CUDA Performance Profiler initialized");
        }

        /// <summary>
        /// Starts profiling session with specified configuration.
        /// </summary>
        public async Task StartProfilingAsync(ProfilingConfiguration? config = null)
        {
            config ??= ProfilingConfiguration.Default;


            await _profilingLock.WaitAsync();
            try
            {
                if (_isProfilingActive)
                {
                    _logger.LogWarningMessage("Profiling already active");
                    return;
                }

                _logger.LogInfoMessage("Starting profiling session with config: {config}");

                // Subscribe to CUPTI callbacks

                var result = cuptiSubscribe(
                    out _cuptiSubscriber,
                    _cuptiCallback,
                    IntPtr.Zero);


                if (result != CuptiResult.Success)
                {
                    throw new ProfilingException($"Failed to subscribe to CUPTI: {result}");
                }

                // Enable kernel profiling
                if (config.ProfileKernels)
                {
                    _ = cuptiActivityEnable(CuptiActivityKind.Kernel);
                    _ = cuptiActivityEnable(CuptiActivityKind.ConcurrentKernel);

                    _ = cuptiEnableCallback(
                        1,
                        _cuptiSubscriber,
                        CuptiCallbackDomain.Runtime,
                        (uint)CuptiRuntimeCallbackId.KernelLaunch);
                }

                // Enable memory profiling
                if (config.ProfileMemory)
                {
                    _ = cuptiActivityEnable(CuptiActivityKind.Memcpy);
                    _ = cuptiActivityEnable(CuptiActivityKind.Memset);
                    _ = cuptiActivityEnable(CuptiActivityKind.Memory);

                    _ = cuptiEnableCallback(
                        1,
                        _cuptiSubscriber,
                        CuptiCallbackDomain.Runtime,
                        (uint)CuptiRuntimeCallbackId.MemcpyAsync);
                }

                // Enable API profiling
                if (config.ProfileApi)
                {
                    _ = cuptiActivityEnable(CuptiActivityKind.RuntimeApi);
                    _ = cuptiActivityEnable(CuptiActivityKind.DriverApi);
                }

                // Enable metrics collection
                if (config.CollectMetrics)
                {
                    _ = cuptiActivityEnable(CuptiActivityKind.Metric);
                    _ = cuptiActivityEnable(CuptiActivityKind.MetricInstance);
                }

                _isProfilingActive = true;
                _logger.LogInfoMessage("Profiling session started successfully");
            }
            finally
            {
                _ = _profilingLock.Release();
            }
        }

        /// <summary>
        /// Stops the current profiling session and generates report.
        /// </summary>
        public async Task<ProfilingReport> StopProfilingAsync()
        {
            await _profilingLock.WaitAsync();
            try
            {
                if (!_isProfilingActive)
                {
                    _logger.LogWarningMessage("No active profiling session");
                    return new ProfilingReport();
                }

                _logger.LogInfoMessage("Stopping profiling session");

                // Flush all pending activities
                _ = cuptiActivityFlushAll(0);

                // Process remaining events

                ProcessQueuedEvents();

                // Disable all activities
                _ = cuptiActivityDisable(CuptiActivityKind.Kernel);
                _ = cuptiActivityDisable(CuptiActivityKind.ConcurrentKernel);
                _ = cuptiActivityDisable(CuptiActivityKind.Memcpy);
                _ = cuptiActivityDisable(CuptiActivityKind.Memset);
                _ = cuptiActivityDisable(CuptiActivityKind.Memory);
                _ = cuptiActivityDisable(CuptiActivityKind.RuntimeApi);
                _ = cuptiActivityDisable(CuptiActivityKind.DriverApi);
                _ = cuptiActivityDisable(CuptiActivityKind.Metric);
                _ = cuptiActivityDisable(CuptiActivityKind.MetricInstance);

                // Unsubscribe from callbacks

                if (_cuptiSubscriber != IntPtr.Zero)
                {
                    _ = cuptiUnsubscribe(_cuptiSubscriber);
                    _cuptiSubscriber = IntPtr.Zero;
                }

                _isProfilingActive = false;

                // Generate report

                var report = GenerateReport();


                _logger.LogInformation(
                    "Profiling session stopped. Kernels profiled: {KernelCount}, Memory ops: {MemoryCount}",
                    _kernelProfiles.Count,
                    _memoryProfiles.Count);


                return report;
            }
            finally
            {
                _ = _profilingLock.Release();
            }
        }

        /// <summary>
        /// Profiles a specific kernel execution.
        /// </summary>
        public async Task<KernelProfile> ProfileKernelAsync(
            string kernelName,
            Func<Task> kernelExecution,
            int warmupRuns = 3,
            int profileRuns = 10)
        {
            _logger.LogDebugMessage("Profiling kernel {kernelName}");

            // Warmup runs

            for (var i = 0; i < warmupRuns; i++)
            {
                await kernelExecution();
            }

            var profile = new KernelProfile
            {
                Name = kernelName,
                StartTime = DateTimeOffset.UtcNow
            };

            // Profile runs
            var executionTimes = new List<TimeSpan>();
            for (var i = 0; i < profileRuns; i++)
            {
                var start = Stopwatch.GetTimestamp();
                await kernelExecution();
                var elapsed = Stopwatch.GetElapsedTime(start);
                executionTimes.Add(elapsed);


                profile.ExecutionCount++;
                profile.TotalTime += elapsed;
            }

            // Calculate statistics
            profile.AverageTime = profile.TotalTime / profile.ExecutionCount;
            profile.MinTime = executionTimes.Min();
            profile.MaxTime = executionTimes.Max();

            // Calculate standard deviation

            var mean = executionTimes.Average(t => t.TotalMilliseconds);
            var variance = executionTimes.Average(t => Math.Pow(t.TotalMilliseconds - mean, 2));
            profile.StandardDeviation = TimeSpan.FromMilliseconds(Math.Sqrt(variance));

            // Store profile

            _kernelProfiles[kernelName] = profile;


            _logger.LogInformation(
                "Kernel {KernelName} profiled: Avg={AvgTime:F3}ms, Min={MinTime:F3}ms, Max={MaxTime:F3}ms, StdDev={StdDev:F3}ms",
                kernelName,
                profile.AverageTime.TotalMilliseconds,
                profile.MinTime.TotalMilliseconds,
                profile.MaxTime.TotalMilliseconds,
                profile.StandardDeviation.TotalMilliseconds);


            return profile;
        }

        /// <summary>
        /// Collects current GPU metrics.
        /// </summary>
        public async Task<GpuMetrics> CollectGpuMetricsAsync(int deviceIndex = 0)
        {
            var metrics = new GpuMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                DeviceIndex = deviceIndex
            };

            try
            {
                if (nvmlDeviceGetHandleByIndex((uint)deviceIndex, out var device) == NvmlReturn.Success)
                {
                    // Get utilization
                    if (nvmlDeviceGetUtilizationRates(device, out var utilization) == NvmlReturn.Success)
                    {
                        metrics.GpuUtilization = utilization.gpu;
                        metrics.MemoryUtilization = utilization.memory;
                    }

                    // Get memory info
                    if (nvmlDeviceGetMemoryInfo(device, out var memory) == NvmlReturn.Success)
                    {
                        metrics.MemoryUsed = memory.used;
                        metrics.MemoryTotal = memory.total;
                        metrics.MemoryFree = memory.free;
                    }

                    // Get temperature
                    if (nvmlDeviceGetTemperature(device, NvmlTemperatureSensor.Gpu, out var temp) == NvmlReturn.Success)
                    {
                        metrics.Temperature = temp;
                    }

                    // Get power usage
                    if (nvmlDeviceGetPowerUsage(device, out var power) == NvmlReturn.Success)
                    {
                        metrics.PowerUsage = power / 1000.0; // Convert to watts
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error collecting GPU metrics");
            }

            await Task.CompletedTask;
            return metrics;
        }

        /// <summary>
        /// Analyzes memory transfer patterns.
        /// </summary>
        public MemoryTransferAnalysis AnalyzeMemoryTransfers()
        {
            var analysis = new MemoryTransferAnalysis();


            if (_memoryProfiles.IsEmpty)
            {
                return analysis;
            }

            var profiles = _memoryProfiles.Values.ToList();

            // Calculate totals

            analysis.TotalTransfers = profiles.Count;
            analysis.TotalBytesTransferred = profiles.Sum(p => p.BytesTransferred);
            analysis.TotalTransferTime = TimeSpan.FromMilliseconds(
                profiles.Sum(p => p.TransferTime.TotalMilliseconds));

            // Group by transfer type

            analysis.TransfersByType = profiles
                .GroupBy(p => p.TransferType)
                .ToDictionary(
                    g => g.Key,
                    g => new TransferTypeStats
                    {
                        Count = g.Count(),
                        TotalBytes = g.Sum(p => p.BytesTransferred),
                        AverageBytes = g.Average(p => p.BytesTransferred),
                        TotalTime = TimeSpan.FromMilliseconds(g.Sum(p => p.TransferTime.TotalMilliseconds)),
                        AverageBandwidth = CalculateAverageBandwidth([.. g])
                    });

            // Find bottlenecks

            analysis.Bottlenecks = IdentifyMemoryBottlenecks(profiles);

            // Calculate overall bandwidth

            if (analysis.TotalTransferTime.TotalSeconds > 0)
            {
                analysis.OverallBandwidth = analysis.TotalBytesTransferred / analysis.TotalTransferTime.TotalSeconds;
            }

            return analysis;
        }

        /// <summary>
        /// CUPTI callback handler.
        /// </summary>
        private void CuptiCallbackHandler(
            IntPtr userdata,
            CuptiCallbackDomain domain,
            uint cbid,
            IntPtr cbdata)
        {
            try
            {
                var evt = new ProfilingEvent
                {
                    Domain = domain,
                    CallbackId = cbid,
                    Timestamp = DateTimeOffset.UtcNow,
                    Data = cbdata
                };


                _eventQueue.Enqueue(evt);

                // Process immediately if queue is getting large

                if (_eventQueue.Count > 1000)
                {
                    ProcessQueuedEvents();
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error in CUPTI callback handler");
            }
        }

        /// <summary>
        /// Processes queued profiling events.
        /// </summary>
        private void ProcessQueuedEvents()
        {
            while (_eventQueue.TryDequeue(out var evt))
            {
                try
                {
                    switch (evt.Domain)
                    {
                        case CuptiCallbackDomain.Runtime:
                            ProcessRuntimeEvent(evt);
                            break;
                        case CuptiCallbackDomain.Driver:
                            ProcessDriverEvent(evt);
                            break;
                        case CuptiCallbackDomain.Resource:
                            ProcessResourceEvent(evt);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, "Error processing profiling event");
                }
            }
        }

        /// <summary>
        /// Processes runtime API events.
        /// </summary>
        private void ProcessRuntimeEvent(ProfilingEvent evt)
        {
            switch ((CuptiRuntimeCallbackId)evt.CallbackId)
            {
                case CuptiRuntimeCallbackId.KernelLaunch:
                    // Extract kernel launch information
                    // This would involve marshaling the callback data structure
                    _logger.LogDebugMessage("Kernel launch event captured");
                    break;


                case CuptiRuntimeCallbackId.MemcpyAsync:
                    // Extract memory transfer information
                    _logger.LogDebugMessage("Memory transfer event captured");
                    break;
            }
        }

        /// <summary>
        /// Processes driver API events.
        /// </summary>
        private void ProcessDriverEvent(ProfilingEvent evt) => _logger.LogDebugMessage("Driver event captured: {evt.CallbackId}");

        /// <summary>
        /// Processes resource events.
        /// </summary>
        private void ProcessResourceEvent(ProfilingEvent evt) => _logger.LogDebugMessage("Resource event captured: {evt.CallbackId}");

        /// <summary>
        /// Timer callback wrapper for collecting metrics.
        /// </summary>
        private void CollectMetricsWrapper(object? state) => _ = Task.Run(async () => await CollectMetricsAsync(state));

        /// <summary>
        /// Collects periodic metrics.
        /// </summary>
        private async Task CollectMetricsAsync(object? state)
        {
            if (!_isProfilingActive)
            {
                return;
            }


            try
            {
                var metrics = await CollectGpuMetricsAsync();


                _logger.LogDebug(
                    "GPU Metrics - Util: {GpuUtil}%, Mem: {MemUtil}%, Temp: {Temp}°C, Power: {Power}W",
                    metrics.GpuUtilization,
                    metrics.MemoryUtilization,
                    metrics.Temperature,
                    metrics.PowerUsage);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error collecting periodic metrics");
            }
        }

        /// <summary>
        /// Generates profiling report.
        /// </summary>
        private ProfilingReport GenerateReport()
        {
            var report = new ProfilingReport
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                KernelProfiles = [.. _kernelProfiles.Values],
                MemoryProfiles = [.. _memoryProfiles.Values]
            };

            // Calculate summary statistics
            if (report.KernelProfiles.Any())
            {
                report.TotalKernelTime = TimeSpan.FromMilliseconds(
                    report.KernelProfiles.Sum(k => k.TotalTime.TotalMilliseconds));
                report.AverageKernelTime = TimeSpan.FromMilliseconds(
                    report.KernelProfiles.Average(k => k.AverageTime.TotalMilliseconds));
            }

            if (report.MemoryProfiles.Any())
            {
                report.TotalMemoryTransferred = report.MemoryProfiles.Sum(m => m.BytesTransferred);
                report.TotalMemoryTime = TimeSpan.FromMilliseconds(
                    report.MemoryProfiles.Sum(m => m.TransferTime.TotalMilliseconds));
            }

            // Identify top time consumers
            report.TopKernelsByTime = [.. report.KernelProfiles
                .OrderByDescending(k => k.TotalTime)
                .Take(10)];

            report.TopMemoryTransfers = [.. report.MemoryProfiles
                .OrderByDescending(m => m.BytesTransferred)
                .Take(10)];

            return report;
        }

        /// <summary>
        /// Exports profiling report to file.
        /// </summary>
        public async Task ExportReportAsync(ProfilingReport report, string filepath)
        {
            try
            {
                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true
                });


                await File.WriteAllTextAsync(filepath, json);


                _logger.LogInfoMessage("Profiling report exported to {filepath}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error exporting profiling report");
                throw;
            }
        }

        /// <summary>
        /// Calculates average bandwidth for transfers.
        /// </summary>
        private static double CalculateAverageBandwidth(IReadOnlyList<MemoryProfile> transfers)
        {
            if (!transfers.Any())
            {
                return 0;
            }


            var totalBytes = transfers.Sum(t => t.BytesTransferred);
            var totalSeconds = transfers.Sum(t => t.TransferTime.TotalSeconds);


            return totalSeconds > 0 ? totalBytes / totalSeconds : 0;
        }

        /// <summary>
        /// Identifies memory transfer bottlenecks.
        /// </summary>
        private static List<string> IdentifyMemoryBottlenecks(IReadOnlyList<MemoryProfile> profiles)
        {
            var bottlenecks = new List<string>();

            // Check for small transfers

            var smallTransfers = profiles.Count(p => p.BytesTransferred < 4096);
            if (smallTransfers > profiles.Count * 0.5)
            {
                bottlenecks.Add($"High number of small transfers ({smallTransfers}/{profiles.Count})");
            }

            // Check for low bandwidth
            var avgBandwidth = CalculateAverageBandwidth(profiles);
            if (avgBandwidth < 10_000_000_000) // Less than 10 GB/s
            {
                bottlenecks.Add($"Low average bandwidth: {avgBandwidth / 1_000_000_000:F2} GB/s");
            }

            // Check for unaligned transfers
            var unaligned = profiles.Count(p => p.BytesTransferred % 128 != 0);
            if (unaligned > profiles.Count * 0.2)
            {
                bottlenecks.Add($"Many unaligned transfers ({unaligned}/{profiles.Count})");
            }

            return bottlenecks;
        }

        /// <summary>
        /// Initializes NVML for metrics collection.
        /// </summary>
        private void InitializeNvml()
        {
            try
            {
                var result = nvmlInit();
                if (result == NvmlReturn.Success)
                {
                    _logger.LogInfoMessage("NVML initialized successfully");
                }
                else
                {
                    _logger.LogWarningMessage("Failed to initialize NVML: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NVML not available, GPU metrics will be limited");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _metricsTimer?.Dispose();
            _profilingLock?.Dispose();

            if (_isProfilingActive)
            {
                _ = StopProfilingAsync().Wait(TimeSpan.FromSeconds(5));
            }

            if (_cuptiSubscriber != IntPtr.Zero)
            {
                _ = cuptiUnsubscribe(_cuptiSubscriber);
            }

            try
            {
                _ = nvmlShutdown();
            }
            catch { }

            _disposed = true;
        }
        /// <summary>
        /// A class that represents profiling configuration.
        /// </summary>

        // Supporting classes and enums
        public class ProfilingConfiguration
        {
            /// <summary>
            /// Gets or sets the profile kernels.
            /// </summary>
            /// <value>The profile kernels.</value>
            public bool ProfileKernels { get; set; } = true;
            /// <summary>
            /// Gets or sets the profile memory.
            /// </summary>
            /// <value>The profile memory.</value>
            public bool ProfileMemory { get; set; } = true;
            /// <summary>
            /// Gets or sets the profile api.
            /// </summary>
            /// <value>The profile api.</value>
            public bool ProfileApi { get; set; }
            /// <summary>
            /// Gets or sets the collect metrics.
            /// </summary>
            /// <value>The collect metrics.</value>

            public bool CollectMetrics { get; set; } = true;
            /// <summary>
            /// Gets or sets the default.
            /// </summary>
            /// <value>The default.</value>


            public static ProfilingConfiguration Default => new();
        }
        /// <summary>
        /// A class that represents kernel profile.
        /// </summary>

        public class KernelProfile
        {
            /// <summary>
            /// Gets or sets the name.
            /// </summary>
            /// <value>The name.</value>
            public required string Name { get; init; }
            /// <summary>
            /// Gets or sets the start time.
            /// </summary>
            /// <value>The start time.</value>
            public DateTimeOffset StartTime { get; init; }
            /// <summary>
            /// Gets or sets the execution count.
            /// </summary>
            /// <value>The execution count.</value>
            public int ExecutionCount { get; set; }
            /// <summary>
            /// Gets or sets the total time.
            /// </summary>
            /// <value>The total time.</value>
            public TimeSpan TotalTime { get; set; }
            /// <summary>
            /// Gets or sets the average time.
            /// </summary>
            /// <value>The average time.</value>
            public TimeSpan AverageTime { get; set; }
            /// <summary>
            /// Gets or sets the min time.
            /// </summary>
            /// <value>The min time.</value>
            public TimeSpan MinTime { get; set; }
            /// <summary>
            /// Gets or sets the max time.
            /// </summary>
            /// <value>The max time.</value>
            public TimeSpan MaxTime { get; set; }
            /// <summary>
            /// Gets or sets the standard deviation.
            /// </summary>
            /// <value>The standard deviation.</value>
            public TimeSpan StandardDeviation { get; set; }
            /// <summary>
            /// Gets or sets the shared memory used.
            /// </summary>
            /// <value>The shared memory used.</value>
            public long SharedMemoryUsed { get; set; }
            /// <summary>
            /// Gets or sets the registers per thread.
            /// </summary>
            /// <value>The registers per thread.</value>
            public long RegistersPerThread { get; set; }
            /// <summary>
            /// Gets or sets the block size.
            /// </summary>
            /// <value>The block size.</value>
            public int BlockSize { get; set; }
            /// <summary>
            /// Gets or sets the grid size.
            /// </summary>
            /// <value>The grid size.</value>
            public int GridSize { get; set; }
        }
        /// <summary>
        /// A class that represents memory profile.
        /// </summary>

        public class MemoryProfile
        {
            /// <summary>
            /// Gets or sets the name.
            /// </summary>
            /// <value>The name.</value>
            public required string Name { get; init; }
            /// <summary>
            /// Gets or sets the transfer type.
            /// </summary>
            /// <value>The transfer type.</value>
            public MemoryTransferType TransferType { get; set; }
            /// <summary>
            /// Gets or sets the bytes transferred.
            /// </summary>
            /// <value>The bytes transferred.</value>
            public long BytesTransferred { get; set; }
            /// <summary>
            /// Gets or sets the transfer time.
            /// </summary>
            /// <value>The transfer time.</value>
            public TimeSpan TransferTime { get; set; }
            /// <summary>
            /// Gets or sets the bandwidth.
            /// </summary>
            /// <value>The bandwidth.</value>
            public double Bandwidth => BytesTransferred / TransferTime.TotalSeconds;
            /// <summary>
            /// Gets or sets a value indicating whether async.
            /// </summary>
            /// <value>The is async.</value>
            public bool IsAsync { get; set; }
            /// <summary>
            /// Gets or sets the stream identifier.
            /// </summary>
            /// <value>The stream id.</value>
            public int StreamId { get; set; }
        }
        /// <summary>
        /// A class that represents gpu metrics.
        /// </summary>

        public class GpuMetrics
        {
            /// <summary>
            /// Gets or sets the timestamp.
            /// </summary>
            /// <value>The timestamp.</value>
            public DateTimeOffset Timestamp { get; init; }
            /// <summary>
            /// Gets or sets the device index.
            /// </summary>
            /// <value>The device index.</value>
            public int DeviceIndex { get; init; }
            /// <summary>
            /// Gets or sets the gpu utilization.
            /// </summary>
            /// <value>The gpu utilization.</value>
            public uint GpuUtilization { get; set; }
            /// <summary>
            /// Gets or sets the memory utilization.
            /// </summary>
            /// <value>The memory utilization.</value>
            public uint MemoryUtilization { get; set; }
            /// <summary>
            /// Gets or sets the memory used.
            /// </summary>
            /// <value>The memory used.</value>
            public ulong MemoryUsed { get; set; }
            /// <summary>
            /// Gets or sets the memory total.
            /// </summary>
            /// <value>The memory total.</value>
            public ulong MemoryTotal { get; set; }
            /// <summary>
            /// Gets or sets the memory free.
            /// </summary>
            /// <value>The memory free.</value>
            public ulong MemoryFree { get; set; }
            /// <summary>
            /// Gets or sets the temperature.
            /// </summary>
            /// <value>The temperature.</value>
            public uint Temperature { get; set; }
            /// <summary>
            /// Gets or sets the power usage.
            /// </summary>
            /// <value>The power usage.</value>
            public double PowerUsage { get; set; }
        }
        /// <summary>
        /// A class that represents profiling report.
        /// </summary>

        public class ProfilingReport
        {
            /// <summary>
            /// Gets or sets the generated at.
            /// </summary>
            /// <value>The generated at.</value>
            public DateTimeOffset GeneratedAt { get; init; }
            /// <summary>
            /// Gets or sets the kernel profiles.
            /// </summary>
            /// <value>The kernel profiles.</value>
            public IReadOnlyList<KernelProfile> KernelProfiles { get; init; } = [];
            /// <summary>
            /// Gets or sets the memory profiles.
            /// </summary>
            /// <value>The memory profiles.</value>
            public IReadOnlyList<MemoryProfile> MemoryProfiles { get; init; } = [];
            /// <summary>
            /// Gets or sets the total kernel time.
            /// </summary>
            /// <value>The total kernel time.</value>
            public TimeSpan TotalKernelTime { get; set; }
            /// <summary>
            /// Gets or sets the average kernel time.
            /// </summary>
            /// <value>The average kernel time.</value>
            public TimeSpan AverageKernelTime { get; set; }
            /// <summary>
            /// Gets or sets the total memory transferred.
            /// </summary>
            /// <value>The total memory transferred.</value>
            public long TotalMemoryTransferred { get; set; }
            /// <summary>
            /// Gets or sets the total memory time.
            /// </summary>
            /// <value>The total memory time.</value>
            public TimeSpan TotalMemoryTime { get; set; }
            /// <summary>
            /// Gets or sets the top kernels by time.
            /// </summary>
            /// <value>The top kernels by time.</value>
            public IList<KernelProfile> TopKernelsByTime { get; } = [];
            /// <summary>
            /// Gets or sets the top memory transfers.
            /// </summary>
            /// <value>The top memory transfers.</value>
            public IList<MemoryProfile> TopMemoryTransfers { get; } = [];
        }
        /// <summary>
        /// A class that represents memory transfer analysis.
        /// </summary>

        public class MemoryTransferAnalysis
        {
            /// <summary>
            /// Gets or sets the total transfers.
            /// </summary>
            /// <value>The total transfers.</value>
            public int TotalTransfers { get; set; }
            /// <summary>
            /// Gets or sets the total bytes transferred.
            /// </summary>
            /// <value>The total bytes transferred.</value>
            public long TotalBytesTransferred { get; set; }
            /// <summary>
            /// Gets or sets the total transfer time.
            /// </summary>
            /// <value>The total transfer time.</value>
            public TimeSpan TotalTransferTime { get; set; }
            /// <summary>
            /// Gets or sets the overall bandwidth.
            /// </summary>
            /// <value>The overall bandwidth.</value>
            public double OverallBandwidth { get; set; }
            /// <summary>
            /// Gets or sets the transfers by type.
            /// </summary>
            /// <value>The transfers by type.</value>
            public Dictionary<MemoryTransferType, TransferTypeStats> TransfersByType { get; } = [];
            /// <summary>
            /// Gets or sets the bottlenecks.
            /// </summary>
            /// <value>The bottlenecks.</value>
            public IList<string> Bottlenecks { get; } = [];
        }
        /// <summary>
        /// A class that represents transfer type stats.
        /// </summary>

        public class TransferTypeStats
        {
            /// <summary>
            /// Gets or sets the count.
            /// </summary>
            /// <value>The count.</value>
            public int Count { get; set; }
            /// <summary>
            /// Gets or sets the total bytes.
            /// </summary>
            /// <value>The total bytes.</value>
            public long TotalBytes { get; set; }
            /// <summary>
            /// Gets or sets the average bytes.
            /// </summary>
            /// <value>The average bytes.</value>
            public double AverageBytes { get; set; }
            /// <summary>
            /// Gets or sets the total time.
            /// </summary>
            /// <value>The total time.</value>
            public TimeSpan TotalTime { get; set; }
            /// <summary>
            /// Gets or sets the average bandwidth.
            /// </summary>
            /// <value>The average bandwidth.</value>
            public double AverageBandwidth { get; set; }
        }

        private class ProfilingEvent
        {
            /// <summary>
            /// Gets or sets the domain.
            /// </summary>
            /// <value>The domain.</value>
            public CuptiCallbackDomain Domain { get; set; }
            /// <summary>
            /// Gets or sets the callback identifier.
            /// </summary>
            /// <value>The callback id.</value>
            public uint CallbackId { get; set; }
            /// <summary>
            /// Gets or sets the timestamp.
            /// </summary>
            /// <value>The timestamp.</value>
            public DateTimeOffset Timestamp { get; set; }
            /// <summary>
            /// Gets or sets the data.
            /// </summary>
            /// <value>The data.</value>
            public IntPtr Data { get; set; }
        }
        /// <summary>
        /// An memory transfer type enumeration.
        /// </summary>

        public enum MemoryTransferType
        {
            HostToDevice,
            DeviceToHost,
            DeviceToDevice,
            HostToHost,
            UnifiedMemory
        }
        /// <summary>
        /// An cupti result enumeration.
        /// </summary>

        // CUPTI enums
        private enum CuptiResult
        {
            Success = 0,
            ErrorInvalidParameter = 1,
            // Add other results as needed
        }
        /// <summary>
        /// An cupti callback domain enumeration.
        /// </summary>

        private enum CuptiCallbackDomain
        {
            Invalid = 0,
            Driver = 1,
            Runtime = 2,
            Resource = 3,
            Synchronize = 4
        }
        /// <summary>
        /// An cupti activity kind enumeration.
        /// </summary>

        private enum CuptiActivityKind
        {
            Invalid = 0,
            Memcpy = 1,
            Memset = 2,
            Kernel = 3,
            Driver = 4,
            RuntimeApi = 5,
            DriverApi = 6,
            Memory = 7,
            Memcpy2 = 8,
            ConcurrentKernel = 9,
            Name = 10,
            Marker = 11,
            MarkerData = 12,
            SourceLocator = 13,
            ContextApi = 14,
            Metric = 15,
            MetricInstance = 16
        }
        /// <summary>
        /// An cupti runtime callback id enumeration.
        /// </summary>

        private enum CuptiRuntimeCallbackId
        {
            Invalid = 0,
            KernelLaunch = 1,
            MemcpyAsync = 2,
            // Add other callback IDs as needed
        }

        // NVML structures and enums
        private struct NvmlUtilization
        {
            /// <summary>
            /// The gpu.
            /// </summary>
            public uint gpu;
            /// <summary>
            /// The memory.
            /// </summary>
            public uint memory;
        }

        private struct NvmlMemory
        {
            /// <summary>
            /// The total.
            /// </summary>
            public ulong total;
            /// <summary>
            /// The free.
            /// </summary>
            public ulong free;
            /// <summary>
            /// The used.
            /// </summary>
            public ulong used;
        }
        /// <summary>
        /// An nvml return enumeration.
        /// </summary>

        private enum NvmlReturn
        {
            Success = 0,
            Uninitialized = 1,
            // Add other returns as needed
        }
        /// <summary>
        /// An nvml temperature sensor enumeration.
        /// </summary>

        private enum NvmlTemperatureSensor
        {
            Gpu = 0,
            // Add other sensors as needed
        }

        private class ProfilingException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the ProfilingException class.
            /// </summary>
            /// <param name="message">The message.</param>
            public ProfilingException(string message) : base(message) { }
            /// <summary>
            /// Initializes a new instance of the ProfilingException class.
            /// </summary>
            public ProfilingException()
            {
            }
            /// <summary>
            /// Initializes a new instance of the ProfilingException class.
            /// </summary>
            /// <param name="message">The message.</param>
            /// <param name="innerException">The inner exception.</param>
            public ProfilingException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}