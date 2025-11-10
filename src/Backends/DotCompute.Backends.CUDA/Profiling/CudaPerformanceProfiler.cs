using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DotCompute.Backends.CUDA.Profiling.Types;
using DotCompute.Backends.CUDA.Serialization;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Profiling
{
    /// <summary>
    /// Production-grade CUDA performance profiler with CUPTI integration,
    /// metrics collection, and detailed performance analysis.
    /// </summary>
    public sealed partial class CudaPerformanceProfiler : IDisposable
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

#pragma warning disable CA1823 // Field is used for JSON serialization configuration - future-proofing
        private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };
#pragma warning restore CA1823

        // CUPTI API imports
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiSubscribe(
            out IntPtr subscriber,
            CuptiCallbackFunc callback,
            IntPtr userdata);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiUnsubscribe(IntPtr subscriber);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiEnableCallback(
            uint enable,
            IntPtr subscriber,
            CuptiCallbackDomain domain,
            uint cbid);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiActivityEnable(CuptiActivityKind kind);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiActivityDisable(CuptiActivityKind kind);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiActivityFlushAll(uint flag);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CuptiResult cuptiGetTimestamp(out ulong timestamp);

        // NVML API imports for GPU metrics
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlInit();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlDeviceGetUtilizationRates(
            IntPtr device,
            out NvmlUtilization utilization);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlDeviceGetMemoryInfo(
            IntPtr device,
            out NvmlMemory memory);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlDeviceGetTemperature(
            IntPtr device,
            NvmlTemperatureSensor sensorType,
            out uint temp);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlDeviceGetPowerUsage(
            IntPtr device,
            out uint power);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlDeviceGetHandleByIndex(
            uint index,
            out IntPtr device);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial NvmlReturn nvmlShutdown();

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


            LogProfilerInitialized();
        }

        /// <summary>
        /// Starts profiling session with specified configuration.
        /// </summary>
        internal async Task StartProfilingAsync(ProfilingConfiguration? config = null)
        {
            config ??= ProfilingConfiguration.Default;


            await _profilingLock.WaitAsync();
            try
            {
                if (_isProfilingActive)
                {
                    LogProfilingAlreadyActive();
                    return;
                }

                LogProfilingSessionStart(config);

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
                LogProfilingSessionStarted();
            }
            finally
            {
                _ = _profilingLock.Release();
            }
        }

        /// <summary>
        /// Stops the current profiling session and generates report.
        /// </summary>
        internal async Task<ProfilingReport> StopProfilingAsync()
        {
            await _profilingLock.WaitAsync();
            try
            {
                if (!_isProfilingActive)
                {
                    LogNoActiveSession();
                    return new ProfilingReport();
                }

                LogStoppingProfilingSession();

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


                LogProfilingSessionStopped(_kernelProfiles.Count, _memoryProfiles.Count);


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
        internal async Task<KernelProfile> ProfileKernelAsync(
            string kernelName,
            Func<Task> kernelExecution,
            int warmupRuns = 3,
            int profileRuns = 10)
        {
            LogProfilingKernel(kernelName);

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


            LogKernelProfiled(
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
        internal async Task<GpuMetrics> CollectGpuMetricsAsync(int deviceIndex = 0)
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
                LogGpuMetricsError(ex);
            }

            await Task.CompletedTask;
            return metrics;
        }

        /// <summary>
        /// Analyzes memory transfer patterns.
        /// </summary>
        internal MemoryTransferAnalysis AnalyzeMemoryTransfers()
        {
            var analysis = new MemoryTransferAnalysis();


            if (_memoryProfiles.IsEmpty)
            {
                return analysis;
            }

            var profiles = _memoryProfiles.Values.ToList();

            // Calculate totals

            // Calculate values
            var totalTransfers = profiles.Count;
            var totalBytesTransferred = profiles.Sum(p => p.BytesTransferred);
            var totalTransferTime = TimeSpan.FromMilliseconds(
                profiles.Sum(p => p.TransferTime.TotalMilliseconds));

            // Group by transfer type
            var transfersByType = profiles
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
            var bottlenecks = IdentifyMemoryBottlenecks(profiles);

            // Calculate overall bandwidth
            var overallBandwidth = totalTransferTime.TotalSeconds > 0
                ? totalBytesTransferred / totalTransferTime.TotalSeconds
                : 0.0;

            // Create analysis with all init-only properties
            analysis = new MemoryTransferAnalysis
            {
                TotalTransfers = totalTransfers,
                TotalBytesTransferred = totalBytesTransferred,
                TotalTransferTime = totalTransferTime,
                OverallBandwidth = overallBandwidth,
                TransfersByType = transfersByType,
                Bottlenecks = bottlenecks
            };

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
                LogCuptiCallbackError(ex);
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
                    LogProfilingEventError(ex);
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
                    LogKernelLaunchEvent();
                    break;


                case CuptiRuntimeCallbackId.MemcpyAsync:
                    // Extract memory transfer information
                    LogMemoryTransferEvent();
                    break;
            }
        }

        /// <summary>
        /// Processes driver API events.
        /// </summary>
        private void ProcessDriverEvent(ProfilingEvent evt) => LogDriverEvent(evt.CallbackId);

        /// <summary>
        /// Processes resource events.
        /// </summary>
        private void ProcessResourceEvent(ProfilingEvent evt) => LogResourceEvent(evt.CallbackId);

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


                LogGpuMetrics(
                    metrics.GpuUtilization,
                    metrics.MemoryUtilization,
                    metrics.Temperature,
                    metrics.PowerUsage);
            }
            catch (Exception ex)
            {
                LogPeriodicMetricsError(ex);
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
            if (report.KernelProfiles.Count > 0)
            {
                report.TotalKernelTime = TimeSpan.FromMilliseconds(
                    report.KernelProfiles.Sum(k => k.TotalTime.TotalMilliseconds));
                report.AverageKernelTime = TimeSpan.FromMilliseconds(
                    report.KernelProfiles.Average(k => k.AverageTime.TotalMilliseconds));
            }

            if (report.MemoryProfiles.Count > 0)
            {
                report.TotalMemoryTransferred = report.MemoryProfiles.Sum(m => m.BytesTransferred);
                report.TotalMemoryTime = TimeSpan.FromMilliseconds(
                    report.MemoryProfiles.Sum(m => m.TransferTime.TotalMilliseconds));
            }

            // Identify top time consumers
            var topKernelsByTime = report.KernelProfiles
                .OrderByDescending(k => k.TotalTime)
                .Take(10)
                .ToList();

            var topMemoryTransfers = report.MemoryProfiles
                .OrderByDescending(m => m.BytesTransferred)
                .Take(10)
                .ToList();

            // Recreate report with init-only properties
            report = new ProfilingReport
            {
                GeneratedAt = report.GeneratedAt,
                KernelProfiles = report.KernelProfiles,
                MemoryProfiles = report.MemoryProfiles,
                TotalKernelTime = report.TotalKernelTime,
                AverageKernelTime = report.AverageKernelTime,
                TotalMemoryTransferred = report.TotalMemoryTransferred,
                TotalMemoryTime = report.TotalMemoryTime,
                TopKernelsByTime = topKernelsByTime,
                TopMemoryTransfers = topMemoryTransfers
            };

            return report;
        }

        /// <summary>
        /// Exports profiling report to file.
        /// </summary>
        internal async Task ExportReportAsync(ProfilingReport report, string filepath)
        {
            try
            {
                var json = JsonSerializer.Serialize(report, typeof(object), CudaJsonContextIndented.Default);


                await File.WriteAllTextAsync(filepath, json);


                LogReportExported(filepath);
            }
            catch (Exception ex)
            {
                LogReportExportError(ex);
                throw;
            }
        }

        /// <summary>
        /// Calculates average bandwidth for transfers.
        /// </summary>
        private static double CalculateAverageBandwidth(IReadOnlyList<MemoryProfile> transfers)
        {
            if (transfers.Count == 0)
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
                    LogNvmlInitialized();
                }
                else
                {
                    LogNvmlInitFailed(result);
                }
            }
            catch (Exception ex)
            {
                LogNvmlNotAvailable(ex);
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
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks - required in synchronous Dispose path
                _ = StopProfilingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
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
    }
}
