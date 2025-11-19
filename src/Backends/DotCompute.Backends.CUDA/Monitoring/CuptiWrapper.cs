// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// P/Invoke wrapper for CUDA Profiling Tools Interface (CUPTI) for detailed performance metrics.
    /// </summary>
    public sealed partial class CuptiWrapper(ILogger logger) : IDisposable
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6865,
            Level = LogLevel.Warning,
            Message = "Error during CUPTI shutdown")]
        private static partial void LogCuptiShutdownError(ILogger logger, Exception ex);

        #endregion

#if WINDOWS
        private const string CUPTI_LIBRARY = "cupti64_2024.3.2.dll";
#else
        private const string CUPTI_LIBRARY = "libcupti.so";
#endif

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly Dictionary<string, CuptiMetric> _availableMetrics = [];
        private readonly Dictionary<string, uint> _eventIds = [];
        private readonly Dictionary<string, uint> _metricIds = [];
        private bool _initialized;
        private bool _disposed;
        private IntPtr _subscriber;
        private IntPtr _eventGroup;
        private int _deviceId;

        /// <summary>
        /// Initializes CUPTI library and discovers available metrics.
        /// </summary>
        public bool Initialize(int deviceId = 0)
        {
            if (_initialized)
            {

                return true;
            }


            try
            {
                _deviceId = deviceId;

                // Subscribe to CUPTI events for Event/Metric API
                // Note: CUPTI 13+ does not require cuptiActivityInitialize() - Activity API is ready to use
                var result = cuptiSubscribe(ref _subscriber, IntPtr.Zero, IntPtr.Zero);
                if (result != CuptiResult.Success)
                {
                    _logger.LogWarningMessage("Failed to subscribe to CUPTI: {result}");
                    return false;
                }

                // Enable activity types
                EnableActivityTypes();

                // Discover available events and metrics
                DiscoverEventsAndMetrics(deviceId);

                // Create event group for profiling
                CreateEventGroup(deviceId);

                _initialized = true;
                _logger.LogInfoMessage($"CUPTI initialized successfully with {_availableMetrics.Count} metrics and {_eventIds.Count} events available");


                return true;
            }
            catch (DllNotFoundException)
            {
                _logger.LogWarningMessage("CUPTI library not found. Detailed kernel metrics will not be available.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error initializing CUPTI");
                return false;
            }
        }

        /// <summary>
        /// Starts profiling for kernel execution.
        /// </summary>
        public ProfilingSession? StartProfiling(string[]? metrics = null)
        {
            if (!_initialized)
            {
                if (!Initialize())
                {

                    return null;
                }
            }

            var session = new ProfilingSession(metrics ?? GetDefaultMetrics());

            // Enable requested metrics

            foreach (var metricName in session.RequestedMetrics)
            {
                if (_availableMetrics.TryGetValue(metricName, out var metric))
                {
                    EnableMetric(metric);
                }
            }

            // Start activity recording
            _ = cuptiActivityEnable(CuptiActivityKind.Kernel);
            _ = cuptiActivityEnable(CuptiActivityKind.Memcpy);
            _ = cuptiActivityEnable(CuptiActivityKind.Memset);


            return session;
        }

        /// <summary>
        /// Collects metrics from a completed profiling session.
        /// </summary>
        public KernelMetrics CollectMetrics(ProfilingSession session)
        {
            if (!_initialized || session == null)
            {

                return new KernelMetrics();
            }


            var metrics = new KernelMetrics();

            try
            {
                // Force flush of activity buffers
                _ = cuptiActivityFlushAll(0);

                // Read activity records
                var buffer = IntPtr.Zero;
                nuint validSize = 0;


                var result = cuptiActivityGetNextRecord(buffer, validSize, out var record);
                while (result == CuptiResult.Success)
                {
                    ProcessActivityRecord(record, metrics);
                    result = cuptiActivityGetNextRecord(buffer, validSize, out record);
                }

                // Collect metric values
                foreach (var metricName in session.RequestedMetrics)
                {
                    if (_availableMetrics.TryGetValue(metricName, out var metric))
                    {
                        var value = ReadMetricValue(metric);
                        metrics.MetricValues[metricName] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error collecting CUPTI metrics");
            }

            return metrics;
        }

        private static void EnableActivityTypes()
        {
            // Enable kernel execution tracking
            _ = cuptiActivityEnable(CuptiActivityKind.Kernel);
            _ = cuptiActivityEnable(CuptiActivityKind.ConcurrentKernel);

            // Enable memory operation tracking
            _ = cuptiActivityEnable(CuptiActivityKind.Memcpy);
            _ = cuptiActivityEnable(CuptiActivityKind.Memset);
            _ = cuptiActivityEnable(CuptiActivityKind.MemCpy2);

            // Enable overhead tracking
            _ = cuptiActivityEnable(CuptiActivityKind.Overhead);
        }

        private void DiscoverEventsAndMetrics(int deviceId)
        {
            try
            {
                IntPtr device = IntPtr.Zero;
                var result = cuptiDeviceGetAttribute((uint)deviceId, CuptiDeviceAttribute.MaxEventId, out var sizeBytes, ref device);

                if (result == CuptiResult.Success)
                {
                    // Enumerate events
                    nuint arraySizeBytes = 0;
                    result = cuptiDeviceEnumEventDomains(device, ref arraySizeBytes, IntPtr.Zero);

                    if (result == CuptiResult.Success && arraySizeBytes > 0)
                    {
                        var domains = Marshal.AllocHGlobal((int)arraySizeBytes);
                        try
                        {
                            result = cuptiDeviceEnumEventDomains(device, ref arraySizeBytes, domains);
                            if (result == CuptiResult.Success)
                            {
                                var domainCount = (int)(arraySizeBytes / (nuint)IntPtr.Size);
                                for (var i = 0; i < domainCount; i++)
                                {
                                    var domainPtr = Marshal.ReadIntPtr(domains, i * IntPtr.Size);
                                    EnumerateEventsInDomain(domainPtr);
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(domains);
                        }
                    }
                }

                // Enumerate metrics (derived from events)
                nuint metricArraySize = 0;
                result = cuptiDeviceEnumMetrics(device, ref metricArraySize, IntPtr.Zero);

                if (result == CuptiResult.Success && metricArraySize > 0)
                {
                    var metrics = Marshal.AllocHGlobal((int)metricArraySize);
                    try
                    {
                        result = cuptiDeviceEnumMetrics(device, ref metricArraySize, metrics);
                        if (result == CuptiResult.Success)
                        {
                            var metricCount = (int)(metricArraySize / sizeof(uint));
                            for (var i = 0; i < metricCount; i++)
                            {
                                var metricId = (uint)Marshal.ReadInt32(metrics, i * sizeof(int));
                                var metricName = GetMetricName(metricId);
                                if (!string.IsNullOrEmpty(metricName))
                                {
                                    _metricIds[metricName] = metricId;
                                    _availableMetrics[metricName] = new CuptiMetric
                                    {
                                        Name = metricName,
                                        Id = metricId,
                                        IsAvailable = true
                                    };
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(metrics);
                    }
                }

                _logger.LogInfoMessage($"Discovered {_eventIds.Count} events and {_metricIds.Count} metrics");
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Error discovering CUPTI events/metrics: {ex.Message}");
                // Fall back to predefined list if enumeration fails
                AddPredefinedMetrics();
            }
        }

        private void EnumerateEventsInDomain(IntPtr domainPtr)
        {
            nuint arraySizeBytes = 0;
            var result = cuptiEventDomainEnumEvents(domainPtr, ref arraySizeBytes, IntPtr.Zero);

            if (result == CuptiResult.Success && arraySizeBytes > 0)
            {
                var events = Marshal.AllocHGlobal((int)arraySizeBytes);
                try
                {
                    result = cuptiEventDomainEnumEvents(domainPtr, ref arraySizeBytes, events);
                    if (result == CuptiResult.Success)
                    {
                        var eventCount = (int)(arraySizeBytes / sizeof(uint));
                        for (var i = 0; i < eventCount; i++)
                        {
                            var eventId = (uint)Marshal.ReadInt32(events, i * sizeof(int));
                            var eventName = GetEventName(eventId);
                            if (!string.IsNullOrEmpty(eventName))
                            {
                                _eventIds[eventName] = eventId;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(events);
                }
            }
        }

        private static string GetMetricName(uint metricId)
        {
            var nameBuffer = new byte[256];
            nuint size = (nuint)nameBuffer.Length;

            var result = cuptiMetricGetAttribute(metricId, CuptiMetricAttribute.Name, ref size, nameBuffer);
            if (result == CuptiResult.Success)
            {
                return System.Text.Encoding.UTF8.GetString(nameBuffer, 0, (int)size - 1).TrimEnd('\0');
            }
            return string.Empty;
        }

        private static string GetEventName(uint eventId)
        {
            var nameBuffer = new byte[256];
            nuint size = (nuint)nameBuffer.Length;

            var result = cuptiEventGetAttribute(eventId, CuptiEventAttribute.Name, ref size, nameBuffer);
            if (result == CuptiResult.Success)
            {
                return System.Text.Encoding.UTF8.GetString(nameBuffer, 0, (int)size - 1).TrimEnd('\0');
            }
            return string.Empty;
        }

        private void AddPredefinedMetrics()
        {
            // Fallback to predefined metrics if dynamic discovery fails
            var predefinedMetrics = new[]
            {
                "achieved_occupancy", "sm_efficiency", "ipc", "issued_ipc",
                "dram_read_throughput", "dram_write_throughput",
                "gld_throughput", "gst_throughput",
                "shared_load_throughput", "shared_store_throughput",
                "l2_read_throughput", "l2_write_throughput",
                "flop_count_sp", "flop_count_dp",
                "flop_sp_efficiency", "flop_dp_efficiency",
                "stall_memory_dependency", "stall_exec_dependency", "stall_inst_fetch",
                "branch_efficiency", "warp_execution_efficiency"
            };

            for (uint i = 0; i < predefinedMetrics.Length; i++)
            {
                var metricName = predefinedMetrics[i];
                _availableMetrics[metricName] = new CuptiMetric
                {
                    Name = metricName,
                    Id = i,
                    IsAvailable = false // Mark as unavailable since we couldn't enumerate
                };
            }
        }

        private void CreateEventGroup(int deviceId)
        {
            IntPtr context = IntPtr.Zero;
            var result = cuptiEventGroupCreate(context, ref _eventGroup, 0);

            if (result != CuptiResult.Success)
            {
                _logger.LogWarningMessage($"Failed to create CUPTI event group: {result}");
                _eventGroup = IntPtr.Zero;
            }
        }

        private static string[] GetDefaultMetrics()
        {
            return
            [
                "achieved_occupancy",
                "sm_efficiency",
                "dram_read_throughput",
                "dram_write_throughput",
                "gld_throughput",
                "gst_throughput",
                "flop_sp_efficiency"
            ];
        }

        private void EnableMetric(CuptiMetric metric)
        {
            if (_eventGroup == IntPtr.Zero || !metric.IsAvailable)
            {
                return;
            }

            // Add metric's events to the event group
            if (_metricIds.TryGetValue(metric.Name, out var metricId))
            {
                // Get the number of events required for this metric
                nuint numEvents = 0;
                var result = cuptiMetricGetNumEvents(metricId, ref numEvents);

                if (result == CuptiResult.Success && numEvents > 0)
                {
                    // Get event IDs for this metric
                    var eventIds = new uint[numEvents];
                    result = cuptiMetricEnumEvents(metricId, ref numEvents, eventIds);

                    if (result == CuptiResult.Success)
                    {
                        // Add each event to the event group
                        foreach (var eventId in eventIds)
                        {
                            _ = cuptiEventGroupAddEvent(_eventGroup, eventId);
                        }
                    }
                }

                _logger.LogDebugMessage($"Enabled metric: {metric.Name} with {numEvents} events");
            }
        }

        private double ReadMetricValue(CuptiMetric metric)
        {
            if (!metric.IsAvailable || _eventGroup == IntPtr.Zero)
            {
                return 0.0;
            }

            if (!_metricIds.TryGetValue(metric.Name, out var metricId))
            {
                return 0.0;
            }

            try
            {
                // Get number of events for this metric
                nuint numEvents = 0;
                var result = cuptiMetricGetNumEvents(metricId, ref numEvents);

                if (result != CuptiResult.Success || numEvents == 0)
                {
                    return 0.0;
                }

                // Get event IDs
                var eventIds = new uint[numEvents];
                result = cuptiMetricEnumEvents(metricId, ref numEvents, eventIds);

                if (result != CuptiResult.Success)
                {
                    return 0.0;
                }

                // Read event values
                var eventValues = new ulong[numEvents];
                for (var i = 0; i < (int)numEvents; i++)
                {
                    ulong value = 0;
                    result = cuptiEventGroupReadEvent(_eventGroup, CuptiReadEventFlags.None, eventIds[i], ref value, out var sizeRead);

                    if (result == CuptiResult.Success)
                    {
                        eventValues[i] = value;
                    }
                }

                // Compute metric value from event values
                var metricValueKind = CuptiMetricValueKind.Double;
                var metricValue = 0.0;

                result = cuptiMetricGetValue(_deviceId, metricId, (nuint)(numEvents * sizeof(ulong)),
                    eventValues, 0, ref metricValueKind, ref metricValue);

                if (result == CuptiResult.Success)
                {
                    return metricValue;
                }

                _logger.LogWarningMessage($"Failed to compute metric value for {metric.Name}: {result}");
                return 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogWarningMessage($"Error reading metric {metric.Name}: {ex.Message}");
                return 0.0;
            }
        }

        private static void ProcessActivityRecord(IntPtr record, KernelMetrics metrics)
        {
            // Read activity kind
            var kind = Marshal.ReadInt32(record);


            switch ((CuptiActivityKind)kind)
            {
                case CuptiActivityKind.Kernel:
                case CuptiActivityKind.ConcurrentKernel:
                    ProcessKernelActivity(record, metrics);
                    break;
                case CuptiActivityKind.Memcpy:
                case CuptiActivityKind.MemCpy2:
                    ProcessMemcpyActivity(record, metrics);
                    break;
            }
        }

        private static void ProcessKernelActivity(IntPtr record, KernelMetrics metrics)
            // Parse kernel execution record
            // This would extract timing, grid/block dimensions, etc. TODO

            => metrics.KernelExecutions++;

        private static void ProcessMemcpyActivity(IntPtr record, KernelMetrics metrics)
            // Parse memory copy record

            => metrics.MemoryTransfers++;
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            if (_initialized)
            {
                try
                {
                    if (_subscriber != IntPtr.Zero)
                    {
                        _ = cuptiUnsubscribe(_subscriber);
                    }

                    _ = cuptiActivityFlushAll(1);
                    _ = cuptiFinalize();


                    _logger.LogInfoMessage("CUPTI shutdown completed");
                }
                catch (Exception ex)
                {
                    LogCuptiShutdownError(_logger, ex);
                }
            }

            _disposed = true;
        }

        // ========================================
        // CUPTI P/Invoke Declarations
        // ========================================

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityInitialize();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiFinalize();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiSubscribe(
            ref IntPtr subscriber,
            IntPtr callback,
            IntPtr userdata);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiUnsubscribe(IntPtr subscriber);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityEnable(CuptiActivityKind kind);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityDisable(CuptiActivityKind kind);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityFlushAll(uint flag);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityGetNextRecord(
            IntPtr buffer,
            nuint validBufferSizeBytes,
            out IntPtr record);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CuptiCallbackFunc(
            IntPtr userdata,
            CuptiCallbackDomain domain,
            uint cbid,
            IntPtr cbdata);
    }
    /// <summary>
    /// An cupti result enumeration.
    /// </summary>

    // ========================================
    // CUPTI Data Structures and Enums
    // ========================================

    public enum CuptiResult
    {
        Success = 0,
        InvalidParameter = 1,
        InvalidDevice = 2,
        InvalidContext = 3,
        InvalidEventDomain = 4,
        InvalidEvent = 5,
        OutOfMemory = 6,
        HardwareBufferBusy = 7,
        NotReady = 8,
        NotCompatible = 9,
        NotInitialized = 10,
        InvalidMetricId = 11,
        InvalidOperation = 12,
        Unknown = 999
    }
    /// <summary>
    /// An cupti activity kind enumeration.
    /// </summary>

    public enum CuptiActivityKind : uint
    {
        Invalid = 0,
        Memcpy = 1,
        Memset = 2,
        Kernel = 3,
        Driver = 4,
        Runtime = 5,
        EventInstance = 6,
        Metric = 7,
        DeviceAttribute = 8,
        Context = 9,
        ConcurrentKernel = 10,
        NameShortcut = 11,
        Memory = 12,
        Overhead = 20,
        MemCpy2 = 21
    }
    /// <summary>
    /// An cupti callback domain enumeration.
    /// </summary>

    public enum CuptiCallbackDomain
    {
        Invalid = 0,
        Driver = 1,
        Runtime = 2,
        Resource = 3,
        Synchronize = 4,
        Nvtx = 5
    }

    /// <summary>
    /// CUPTI runtime callback IDs for runtime API callbacks.
    /// </summary>
    public enum CuptiRuntimeCallbackId : uint
    {
        Invalid = 0,
        KernelLaunch = 1,
        MemcpyAsync = 2,
        Memcpy = 3,
        Memset = 4,
        MemsetAsync = 5
    }

    /// <summary>
    /// Represents a CUPTI metric.
    /// </summary>
    public sealed class CuptiMetric
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public uint Id { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether available.
        /// </summary>
        /// <value>The is available.</value>
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Represents a profiling session.
    /// </summary>
    public sealed class ProfilingSession
    {
        /// <summary>
        /// Gets or sets the requested metrics.
        /// </summary>
        /// <value>The requested metrics.</value>
        public IReadOnlyList<string> RequestedMetrics { get; }
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; }


        internal ProfilingSession(string[] metrics)
        {
            RequestedMetrics = metrics;
            StartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Kernel execution metrics collected via CUPTI.
    /// </summary>
    public sealed class KernelMetrics
    {
        /// <summary>
        /// Gets or sets the kernel executions.
        /// </summary>
        /// <value>The kernel executions.</value>
        public int KernelExecutions { get; set; }
        /// <summary>
        /// Gets or sets the memory transfers.
        /// </summary>
        /// <value>The memory transfers.</value>
        public int MemoryTransfers { get; set; }
        /// <summary>
        /// Gets or sets the metric values.
        /// </summary>
        /// <value>The metric values.</value>
        public Dictionary<string, double> MetricValues { get; } = [];
        /// <summary>
        /// Gets or sets the achieved occupancy.
        /// </summary>
        /// <value>The achieved occupancy.</value>


        public double AchievedOccupancy => MetricValues.GetValueOrDefault("achieved_occupancy", 0);
        /// <summary>
        /// Gets or sets the sm efficiency.
        /// </summary>
        /// <value>The sm efficiency.</value>
        public double SmEfficiency => MetricValues.GetValueOrDefault("sm_efficiency", 0);
        /// <summary>
        /// Gets or sets the dram read throughput.
        /// </summary>
        /// <value>The dram read throughput.</value>
        public double DramReadThroughput => MetricValues.GetValueOrDefault("dram_read_throughput", 0);
        /// <summary>
        /// Gets or sets the dram write throughput.
        /// </summary>
        /// <value>The dram write throughput.</value>
        public double DramWriteThroughput => MetricValues.GetValueOrDefault("dram_write_throughput", 0);
        /// <summary>
        /// Gets or sets the global load throughput.
        /// </summary>
        /// <value>The global load throughput.</value>
        public double GlobalLoadThroughput => MetricValues.GetValueOrDefault("gld_throughput", 0);
        /// <summary>
        /// Gets or sets the global store throughput.
        /// </summary>
        /// <value>The global store throughput.</value>
        public double GlobalStoreThroughput => MetricValues.GetValueOrDefault("gst_throughput", 0);
        /// <summary>
        /// Gets or sets the flop efficiency.
        /// </summary>
        /// <value>The flop efficiency.</value>
        public double FlopEfficiency => MetricValues.GetValueOrDefault("flop_sp_efficiency", 0);
    }
}
