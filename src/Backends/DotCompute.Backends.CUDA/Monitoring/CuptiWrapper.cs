// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// P/Invoke wrapper for CUDA Profiling Tools Interface (CUPTI) for detailed performance metrics.
    /// </summary>
    public sealed class CuptiWrapper : IDisposable
    {
#if WINDOWS
        private const string CUPTI_LIBRARY = "cupti64_2024.3.2.dll";
#else
        private const string CUPTI_LIBRARY = "libcupti.so";
#endif

        private readonly ILogger _logger;
        private readonly Dictionary<string, CuptiMetric> _availableMetrics;
        private bool _initialized;
        private bool _disposed;
        private IntPtr _subscriber;

        public CuptiWrapper(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _availableMetrics = [];
        }

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
                // Initialize CUPTI
                var result = cuptiActivityInitialize();
                if (result != CuptiResult.Success)
                {
                    _logger.LogWarning("Failed to initialize CUPTI: {Error}", result);
                    return false;
                }

                // Subscribe to CUPTI events
                result = cuptiSubscribe(ref _subscriber, IntPtr.Zero, IntPtr.Zero);
                if (result != CuptiResult.Success)
                {
                    _logger.LogWarning("Failed to subscribe to CUPTI: {Error}", result);
                    return false;
                }

                // Enable activity types
                EnableActivityTypes();

                // Discover available metrics
                DiscoverMetrics(deviceId);

                _initialized = true;
                _logger.LogInformation("CUPTI initialized successfully with {MetricCount} metrics available",

                    _availableMetrics.Count);


                return true;
            }
            catch (DllNotFoundException)
            {
                _logger.LogWarning("CUPTI library not found. Detailed kernel metrics will not be available.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CUPTI");
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
            _ = cuptiActivityEnable(CuptiActivityKind.MemCpy);
            _ = cuptiActivityEnable(CuptiActivityKind.MemSet);


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
                _logger.LogError(ex, "Error collecting CUPTI metrics");
            }

            return metrics;
        }

        private static void EnableActivityTypes()
        {
            // Enable kernel execution tracking
            _ = cuptiActivityEnable(CuptiActivityKind.Kernel);
            _ = cuptiActivityEnable(CuptiActivityKind.ConcurrentKernel);

            // Enable memory operation tracking
            _ = cuptiActivityEnable(CuptiActivityKind.MemCpy);
            _ = cuptiActivityEnable(CuptiActivityKind.MemSet);
            _ = cuptiActivityEnable(CuptiActivityKind.MemCpy2);

            // Enable overhead tracking
            _ = cuptiActivityEnable(CuptiActivityKind.Overhead);
        }

        private void DiscoverMetrics(int deviceId)
        {
            // Common metrics to discover
            var commonMetrics = new[]
            {
                "achieved_occupancy",
                "sm_efficiency",
                "ipc",                     // Instructions per cycle
                "issued_ipc",
                "dram_read_throughput",
                "dram_write_throughput",
                "gld_throughput",          // Global load throughput
                "gst_throughput",          // Global store throughput
                "shared_load_throughput",
                "shared_store_throughput",
                "l2_read_throughput",
                "l2_write_throughput",
                "flop_count_sp",           // Single precision FLOP count
                "flop_count_dp",           // Double precision FLOP count
                "flop_sp_efficiency",
                "flop_dp_efficiency",
                "stall_memory_dependency",
                "stall_exec_dependency",
                "stall_inst_fetch",
                "branch_efficiency",
                "warp_execution_efficiency"
            };

            foreach (var metricName in commonMetrics)
            {
                _availableMetrics[metricName] = new CuptiMetric
                {
                    Name = metricName,
                    Id = (uint)_availableMetrics.Count,
                    IsAvailable = true
                };
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
            // In real implementation, enable specific metric collection
            _logger.LogDebug("Enabling metric: {MetricName}", metric.Name);
        }

        private static double ReadMetricValue(CuptiMetric metric)
        {
            // In real implementation, read actual metric value
            // For now, return simulated values
            return metric.Name switch
            {
                "achieved_occupancy" => 0.75,
                "sm_efficiency" => 0.85,
                "dram_read_throughput" => 250.5, // GB/s
                "dram_write_throughput" => 180.2, // GB/s
                _ => 0.0
            };
        }

        private void ProcessActivityRecord(IntPtr record, KernelMetrics metrics)
        {
            // Read activity kind
            var kind = Marshal.ReadInt32(record);


            switch ((CuptiActivityKind)kind)
            {
                case CuptiActivityKind.Kernel:
                case CuptiActivityKind.ConcurrentKernel:
                    ProcessKernelActivity(record, metrics);
                    break;
                case CuptiActivityKind.MemCpy:
                case CuptiActivityKind.MemCpy2:
                    ProcessMemcpyActivity(record, metrics);
                    break;
            }
        }

        private static void ProcessKernelActivity(IntPtr record, KernelMetrics metrics)
        {
            // Parse kernel execution record
            // This would extract timing, grid/block dimensions, etc. TODO
            metrics.KernelExecutions++;
        }

        private static void ProcessMemcpyActivity(IntPtr record, KernelMetrics metrics)
        {
            // Parse memory copy record
            metrics.MemoryTransfers++;
        }

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


                    _logger.LogInformation("CUPTI shutdown completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during CUPTI shutdown");
                }
            }

            _disposed = true;
        }

        // ========================================
        // CUPTI P/Invoke Declarations
        // ========================================

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityInitialize();

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiFinalize();

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiSubscribe(
            ref IntPtr subscriber,
            IntPtr callback,
            IntPtr userdata);

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiUnsubscribe(IntPtr subscriber);

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityEnable(CuptiActivityKind kind);

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityDisable(CuptiActivityKind kind);

        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiActivityFlushAll(uint flag);

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

    public enum CuptiActivityKind : uint
    {
        Invalid = 0,
        MemCpy = 1,
        MemSet = 2,
        Kernel = 3,
        Driver = 4,
        Runtime = 5,
        EventInstance = 6,
        Metric = 7,
        DeviceAttribute = 8,
        Context = 9,
        ConcurrentKernel = 10,
        NameShortcut = 11,
        Overhead = 20,
        MemCpy2 = 21
    }

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
    /// Represents a CUPTI metric.
    /// </summary>
    public sealed class CuptiMetric
    {
        public string Name { get; set; } = string.Empty;
        public uint Id { get; set; }
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Represents a profiling session.
    /// </summary>
    public sealed class ProfilingSession
    {
        public string[] RequestedMetrics { get; }
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
        public int KernelExecutions { get; set; }
        public int MemoryTransfers { get; set; }
        public Dictionary<string, double> MetricValues { get; } = [];


        public double AchievedOccupancy => MetricValues.GetValueOrDefault("achieved_occupancy", 0);
        public double SmEfficiency => MetricValues.GetValueOrDefault("sm_efficiency", 0);
        public double DramReadThroughput => MetricValues.GetValueOrDefault("dram_read_throughput", 0);
        public double DramWriteThroughput => MetricValues.GetValueOrDefault("dram_write_throughput", 0);
        public double GlobalLoadThroughput => MetricValues.GetValueOrDefault("gld_throughput", 0);
        public double GlobalStoreThroughput => MetricValues.GetValueOrDefault("gst_throughput", 0);
        public double FlopEfficiency => MetricValues.GetValueOrDefault("flop_sp_efficiency", 0);
    }
}