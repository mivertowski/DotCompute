using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DotCompute.Backends.CUDA.Monitoring;
using DotCompute.Backends.CUDA.Profiling.Types;
using DotCompute.Backends.CUDA.Serialization;
using DotCompute.Core.Execution;
using Microsoft.Extensions.Logging;
using NvmlTemperatureSensor = DotCompute.Backends.CUDA.Monitoring.NvmlTemperatureSensors;
using PublicProfilingConfiguration = DotCompute.Backends.CUDA.Profiling.Configuration.ProfilingConfiguration;

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
        private readonly ConcurrentQueue<Types.CudaProfilingEvent> _eventQueue;
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
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiSubscribe(
            out IntPtr subscriber,
            CuptiCallbackFunc callback,
            IntPtr userdata);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiUnsubscribe(IntPtr subscriber);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiEnableCallback(
            uint enable,
            IntPtr subscriber,
            CuptiCallbackDomain domain,
            uint cbid);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiActivityEnable(CuptiActivityKind kind);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiActivityDisable(CuptiActivityKind kind);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiActivityFlushAll(uint flag);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cupti64_2023.3.1")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CuptiResult cuptiGetTimestamp(out ulong timestamp);

        // NVML API imports for GPU metrics
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern NvmlReturn nvmlInit();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern NvmlReturn nvmlDeviceGetUtilizationRates(
            IntPtr device,
            out NvmlUtilization utilization);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern NvmlReturn nvmlDeviceGetMemoryInfo(
            IntPtr device,
            out NvmlMemory memory);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern NvmlReturn nvmlDeviceGetTemperature(
            IntPtr device,
            NvmlTemperatureSensor sensorType,
            out uint temp);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern NvmlReturn nvmlDeviceGetPowerUsage(
            IntPtr device,
            out uint power);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern NvmlReturn nvmlDeviceGetHandleByIndex(
            uint index,
            out IntPtr device);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvml")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
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
            _eventQueue = new ConcurrentQueue<Types.CudaProfilingEvent>();
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
        internal async Task StartProfilingAsync(PublicProfilingConfiguration? config = null)
        {
            config ??= PublicProfilingConfiguration.Default;


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
                    _ = cuptiActivityEnable(CuptiActivityKind.Runtime);
                    _ = cuptiActivityEnable(CuptiActivityKind.Driver);
                }

                // Enable metrics collection
                if (config.CollectMetrics)
                {
                    _ = cuptiActivityEnable(CuptiActivityKind.Metric);
                    _ = cuptiActivityEnable(CuptiActivityKind.EventInstance);
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
                    return new ProfilingReport
                    {
                        ReportId = Guid.NewGuid().ToString(),
                        KernelProfiles = new Dictionary<string, KernelProfile>(),
                        MemoryProfiles = new Dictionary<string, MemoryProfile>(),
                        GpuMetricsHistory = new List<Types.GpuMetrics>(),
                        TransferAnalysis = new MemoryTransferAnalysis
                        {
                            StatsByType = new Dictionary<MemoryTransferType, TransferTypeStats>()
                        }
                    };
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
                _ = cuptiActivityDisable(CuptiActivityKind.Runtime);
                _ = cuptiActivityDisable(CuptiActivityKind.Driver);
                _ = cuptiActivityDisable(CuptiActivityKind.Metric);
                _ = cuptiActivityDisable(CuptiActivityKind.EventInstance);

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
                KernelName = kernelName,
                FirstInvocation = DateTimeOffset.UtcNow
            };

            // Profile runs
            var executionTimes = new List<TimeSpan>();
            for (var i = 0; i < profileRuns; i++)
            {
                var start = Stopwatch.GetTimestamp();
                await kernelExecution();
                var elapsed = Stopwatch.GetElapsedTime(start);
                executionTimes.Add(elapsed);


                profile.TotalInvocations++;
                profile.TotalExecutionTime += elapsed;
            }

            // Calculate statistics
            if (executionTimes.Count > 0)
            {
                profile.MinExecutionTime = executionTimes.Min();
                profile.MaxExecutionTime = executionTimes.Max();
            }
            profile.LastInvocation = DateTimeOffset.UtcNow;

            // Calculate standard deviation
            var mean = executionTimes.Average(t => t.TotalMilliseconds);
            var variance = executionTimes.Average(t => Math.Pow(t.TotalMilliseconds - mean, 2));
            var standardDeviation = TimeSpan.FromMilliseconds(Math.Sqrt(variance));

            // Store profile

            _kernelProfiles[kernelName] = profile;


            LogKernelProfiled(
                kernelName,
                profile.AverageExecutionTime.TotalMilliseconds,
                profile.MinExecutionTime.TotalMilliseconds,
                profile.MaxExecutionTime.TotalMilliseconds,
                standardDeviation.TotalMilliseconds);


            return profile;
        }

        /// <summary>
        /// Collects current GPU metrics.
        /// </summary>
        internal async Task<DotCompute.Backends.CUDA.Profiling.Types.GpuMetrics> CollectGpuMetricsAsync(int deviceIndex = 0)
        {
            var metrics = new DotCompute.Backends.CUDA.Profiling.Types.GpuMetrics
            {
                Timestamp = DateTimeOffset.UtcNow
            };

            try
            {
                if (nvmlDeviceGetHandleByIndex((uint)deviceIndex, out var device) == NvmlReturn.Success)
                {
                    // Get utilization
                    if (nvmlDeviceGetUtilizationRates(device, out var utilization) == NvmlReturn.Success)
                    {
                        metrics.GpuUtilizationPercent = (int)utilization.Gpu;
                        metrics.MemoryUtilizationPercent = (int)utilization.Memory;
                    }

                    // Get memory info
                    if (nvmlDeviceGetMemoryInfo(device, out var memory) == NvmlReturn.Success)
                    {
                        metrics.UsedMemoryBytes = (long)memory.Used;
                        metrics.TotalMemoryBytes = (long)memory.Total;
                    }

                    // Get temperature
                    if (nvmlDeviceGetTemperature(device, NvmlTemperatureSensor.Gpu, out var temp) == NvmlReturn.Success)
                    {
                        metrics.TemperatureCelsius = (int)temp;
                    }

                    // Get power usage
                    if (nvmlDeviceGetPowerUsage(device, out var power) == NvmlReturn.Success)
                    {
                        metrics.PowerUsageWatts = (int)(power / 1000); // Convert to watts
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
            var analysis = new MemoryTransferAnalysis
            {
                StatsByType = new Dictionary<MemoryTransferType, TransferTypeStats>()
            };

            if (_memoryProfiles.IsEmpty)
            {
                return analysis;
            }

            var profiles = _memoryProfiles.Values.ToList();

            // Calculate totals
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
                        TransferType = g.Key,
                        TransferCount = g.Count(),
                        TotalBytes = g.Sum(p => p.BytesTransferred),
                        TotalTime = TimeSpan.FromMilliseconds(g.Sum(p => p.TransferTime.TotalMilliseconds))
                    });

            // Create analysis with all init-only properties
            analysis = new MemoryTransferAnalysis
            {
                StatsByType = transfersByType
            };

            // Set mutable properties
            analysis.TotalTransfers = totalTransfers;
            analysis.TotalBytesTransferred = totalBytesTransferred;
            analysis.TotalTransferTime = totalTransferTime;

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
                var evt = new Types.CudaProfilingEvent
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
        private void ProcessRuntimeEvent(Types.CudaProfilingEvent evt)
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
        private void ProcessDriverEvent(Types.CudaProfilingEvent evt) => LogDriverEvent(evt.CallbackId);

        /// <summary>
        /// Processes resource events.
        /// </summary>
        private void ProcessResourceEvent(Types.CudaProfilingEvent evt) => LogResourceEvent(evt.CallbackId);

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
                    (uint)metrics.GpuUtilization,
                    (uint)metrics.MemoryUtilization,
                    (uint)metrics.Temperature,
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
            var kernelProfilesDict = new Dictionary<string, KernelProfile>(_kernelProfiles);
            var memoryProfilesDict = new Dictionary<string, MemoryProfile>(_memoryProfiles);

            var report = new ProfilingReport
            {
                ReportId = Guid.NewGuid().ToString(),
                GeneratedAt = DateTimeOffset.UtcNow,
                KernelProfiles = kernelProfilesDict,
                MemoryProfiles = memoryProfilesDict,
                GpuMetricsHistory = new List<Types.GpuMetrics>(),
                TransferAnalysis = AnalyzeMemoryTransfers()
            };

            // Calculate summary statistics
            if (report.KernelProfiles.Count > 0)
            {
                report.TotalKernelInvocations = report.KernelProfiles.Values.Sum(k => k.TotalInvocations);
            }

            if (report.MemoryProfiles.Count > 0)
            {
                report.TotalMemoryOperations = report.MemoryProfiles.Count;
                report.TotalBytesTransferred = report.MemoryProfiles.Values.Sum(m => m.BytesTransferred);
            }

            // Identify top time consumers
            var topKernelsByTime = report.KernelProfiles.Values
                .OrderByDescending(k => k.TotalExecutionTime)
                .Take(10)
                .ToList();

            var topMemoryTransfers = report.MemoryProfiles.Values
                .OrderByDescending(m => m.BytesTransferred)
                .Take(10)
                .ToList();

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
            catch (Exception ex)
            {
                Trace.TraceWarning($"NVML shutdown failed: {ex.Message}");
            }

            _disposed = true;
        }
    }
}
