// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// P/Invoke wrapper for NVIDIA Management Library (NVML) for GPU monitoring.
    /// </summary>
    public sealed class NvmlWrapper(ILogger logger) : IDisposable
    {
#if WINDOWS
        private const string NVML_LIBRARY = "nvml.dll";
#else
        private const string NVML_LIBRARY = "libnvidia-ml.so.1";
#endif

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Initializes NVML library.
        /// </summary>
        public bool Initialize()
        {
            if (_initialized)
            {

                return true;
            }


            try
            {
                var result = nvmlInit_v2();
                if (result == NvmlReturn.Success)
                {
                    _initialized = true;
                    _logger.LogInfoMessage("NVML initialized successfully");

                    // Log NVML version

                    var versionBuffer = new StringBuilder(256);
                    if (nvmlSystemGetNVMLVersion(versionBuffer, (uint)versionBuffer.Capacity) == NvmlReturn.Success)
                    {
                        _logger.LogInfoMessage($"NVML Version: {versionBuffer}");
                    }


                    return true;
                }
                else
                {
                    _logger.LogWarningMessage("Failed to initialize NVML: {result}");
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                _logger.LogWarningMessage("NVML library not found. GPU metrics will not be available.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error initializing NVML");
                return false;
            }
        }

        /// <summary>
        /// Gets device metrics for the specified GPU.
        /// </summary>
        public GpuMetrics GetDeviceMetrics(int deviceIndex)
        {
            if (!_initialized)
            {
                if (!Initialize())
                {

                    return new GpuMetrics { IsAvailable = false };
                }

            }

            var metrics = new GpuMetrics { IsAvailable = true, DeviceIndex = deviceIndex };

            try
            {
                var device = IntPtr.Zero;
                var result = nvmlDeviceGetHandleByIndex((uint)deviceIndex, ref device);
                if (result != NvmlReturn.Success)
                {
                    _logger.LogWarningMessage("Failed to get device handle for index {Index}: {deviceIndex, result}");
                    metrics.IsAvailable = false;
                    return metrics;
                }

                // Get temperature
                uint temperature = 0;
                result = nvmlDeviceGetTemperature(device, NvmlTemperatureSensors.Gpu, ref temperature);
                if (result == NvmlReturn.Success)
                {
                    metrics.Temperature = temperature;
                }

                // Get power usage
                uint powerUsage = 0;
                result = nvmlDeviceGetPowerUsage(device, ref powerUsage);
                if (result == NvmlReturn.Success)
                {
                    metrics.PowerUsage = powerUsage / 1000.0; // Convert milliwatts to watts
                }

                // Get memory info
                var memInfo = new NvmlMemory();
                result = nvmlDeviceGetMemoryInfo(device, ref memInfo);
                if (result == NvmlReturn.Success)
                {
                    metrics.MemoryUsed = memInfo.Used;
                    metrics.MemoryTotal = memInfo.Total;
                    metrics.MemoryFree = memInfo.Free;
                    metrics.MemoryUtilization = (double)memInfo.Used / memInfo.Total * 100;
                }

                // Get utilization rates
                var utilization = new NvmlUtilization();
                result = nvmlDeviceGetUtilizationRates(device, ref utilization);
                if (result == NvmlReturn.Success)
                {
                    metrics.GpuUtilization = utilization.Gpu;
                    metrics.MemoryBandwidthUtilization = utilization.Memory;
                }

                // Get clock speeds
                uint clockSpeed = 0;
                result = nvmlDeviceGetClockInfo(device, NvmlClockType.Graphics, ref clockSpeed);
                if (result == NvmlReturn.Success)
                {
                    metrics.GraphicsClockMHz = clockSpeed;
                }

                result = nvmlDeviceGetClockInfo(device, NvmlClockType.Memory, ref clockSpeed);
                if (result == NvmlReturn.Success)
                {
                    metrics.MemoryClockMHz = clockSpeed;
                }

                // Get PCIe throughput
                uint txBytes = 0, rxBytes = 0;
                result = nvmlDeviceGetPcieThroughput(device, NvmlPcieUtilCounter.TxBytes, ref txBytes);
                if (result == NvmlReturn.Success)
                {
                    metrics.PcieTxBytes = txBytes;
                }

                result = nvmlDeviceGetPcieThroughput(device, NvmlPcieUtilCounter.RxBytes, ref rxBytes);
                if (result == NvmlReturn.Success)
                {
                    metrics.PcieRxBytes = rxBytes;
                }

                // Get fan speed
                uint fanSpeed = 0;
                result = nvmlDeviceGetFanSpeed(device, ref fanSpeed);
                if (result == NvmlReturn.Success)
                {
                    metrics.FanSpeedPercent = fanSpeed;
                }

                // Get throttling reasons
                ulong throttleReasons = 0;
                result = nvmlDeviceGetCurrentClocksThrottleReasons(device, ref throttleReasons);
                if (result == NvmlReturn.Success)
                {
                    metrics.IsThrottling = throttleReasons != 0;
                    metrics.ThrottleReasons = DecodeThrottleReasons(throttleReasons);
                }

                return metrics;
            }
            catch (Exception)
            {
                _logger.LogErrorMessage("Failed to get GPU metrics");
                metrics.IsAvailable = false;
                return metrics;
            }
        }

        private static string DecodeThrottleReasons(ulong reasons)
        {
            var reasonList = new StringBuilder();


            if ((reasons & 0x1) != 0)
            {
                _ = reasonList.Append("GpuIdle ");
            }


            if ((reasons & 0x2) != 0)
            {
                _ = reasonList.Append("ApplicationsClocksSetting ");
            }


            if ((reasons & 0x4) != 0)
            {
                _ = reasonList.Append("SwPowerCap ");
            }


            if ((reasons & 0x8) != 0)
            {
                _ = reasonList.Append("HwSlowdown ");
            }


            if ((reasons & 0x10) != 0)
            {
                _ = reasonList.Append("SyncBoost ");
            }

            if ((reasons & 0x20) != 0)
            {
                _ = reasonList.Append("SwThermalSlowdown ");
            }

            if ((reasons & 0x40) != 0)
            {
                _ = reasonList.Append("HwThermalSlowdown ");
            }

            if ((reasons & 0x80) != 0)
            {
                _ = reasonList.Append("HwPowerBrakeSlowdown ");
            }

            if ((reasons & 0x100) != 0)
            {
                _ = reasonList.Append("DisplayClockSetting ");
            }


            return reasonList.ToString().TrimEnd();
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


            if (_initialized)
            {
                try
                {
                    _ = nvmlShutdown();
                    _logger.LogInfoMessage("NVML shutdown completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during NVML shutdown");
                }
            }

            _disposed = true;
        }

        // ========================================
        // NVML P/Invoke Declarations
        // ========================================

        [DllImport(NVML_LIBRARY, EntryPoint = "nvmlInit_v2")]
        private static extern NvmlReturn nvmlInit_v2();

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlShutdown();

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlSystemGetNVMLVersion(StringBuilder version, uint length);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetHandleByIndex(uint index, ref IntPtr device);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetTemperature(IntPtr device, NvmlTemperatureSensors sensorType, ref uint temp);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetPowerUsage(IntPtr device, ref uint power);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetMemoryInfo(IntPtr device, ref NvmlMemory memory);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetUtilizationRates(IntPtr device, ref NvmlUtilization utilization);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetClockInfo(IntPtr device, NvmlClockType type, ref uint clock);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetPcieThroughput(IntPtr device, NvmlPcieUtilCounter counter, ref uint value);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetFanSpeed(IntPtr device, ref uint speed);

        [DllImport(NVML_LIBRARY)]
        private static extern NvmlReturn nvmlDeviceGetCurrentClocksThrottleReasons(IntPtr device, ref ulong clocksThrottleReasons);
    }
    /// <summary>
    /// An nvml return enumeration.
    /// </summary>

    // ========================================
    // NVML Data Structures and Enums
    // ========================================

    public enum NvmlReturn
    {
        Success = 0,
        Uninitialized = 1,
        InvalidArgument = 2,
        NotSupported = 3,
        NoPermission = 4,
        AlreadyInitialized = 5,
        NotFound = 6,
        InsufficientSize = 7,
        InsufficientPower = 8,
        DriverNotLoaded = 9,
        Timeout = 10,
        IrqIssue = 11,
        LibraryNotFound = 12,
        FunctionNotFound = 13,
        CorruptedInfoRom = 14,
        GpuIsLost = 15,
        ResetRequired = 16,
        OperatingSystem = 17,
        LibraryVersionMismatch = 18,
        InUse = 19,
        Unknown = 999
    }
    /// <summary>
    /// An nvml temperature sensors enumeration.
    /// </summary>

    public enum NvmlTemperatureSensors
    {
        Gpu = 0,
        Count = 1
    }
    /// <summary>
    /// An nvml clock type enumeration.
    /// </summary>

    public enum NvmlClockType
    {
        Graphics = 0,
        Sm = 1,
        Memory = 2,
        Video = 3,
        Count = 4
    }
    /// <summary>
    /// An nvml pcie util counter enumeration.
    /// </summary>

    public enum NvmlPcieUtilCounter
    {
        TxBytes = 0,
        RxBytes = 1
    }
    /// <summary>
    /// A nvml memory structure.
    /// </summary>

    [StructLayout(LayoutKind.Sequential)]
    public struct NvmlMemory
    {
        /// <summary>
        /// The total.
        /// </summary>
        public ulong Total;
        /// <summary>
        /// The free.
        /// </summary>
        public ulong Free;
        /// <summary>
        /// The used.
        /// </summary>
        public ulong Used;
    }
    /// <summary>
    /// A nvml utilization structure.
    /// </summary>

    [StructLayout(LayoutKind.Sequential)]
    public struct NvmlUtilization
    {
        /// <summary>
        /// The gpu.
        /// </summary>
        public uint Gpu;
        /// <summary>
        /// The memory.
        /// </summary>
        public uint Memory;
    }

    /// <summary>
    /// GPU metrics collected from NVML.
    /// </summary>
    public sealed class GpuMetrics
    {
        /// <summary>
        /// Gets or sets a value indicating whether available.
        /// </summary>
        /// <value>The is available.</value>
        public bool IsAvailable { get; set; }
        /// <summary>
        /// Gets or sets the device index.
        /// </summary>
        /// <value>The device index.</value>
        public int DeviceIndex { get; set; }
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
        /// Gets or sets the memory utilization.
        /// </summary>
        /// <value>The memory utilization.</value>
        public double MemoryUtilization { get; set; }
        /// <summary>
        /// Gets or sets the gpu utilization.
        /// </summary>
        /// <value>The gpu utilization.</value>
        public uint GpuUtilization { get; set; }
        /// <summary>
        /// Gets or sets the memory bandwidth utilization.
        /// </summary>
        /// <value>The memory bandwidth utilization.</value>
        public uint MemoryBandwidthUtilization { get; set; }
        /// <summary>
        /// Gets or sets the graphics clock m hz.
        /// </summary>
        /// <value>The graphics clock m hz.</value>
        public uint GraphicsClockMHz { get; set; }
        /// <summary>
        /// Gets or sets the memory clock m hz.
        /// </summary>
        /// <value>The memory clock m hz.</value>
        public uint MemoryClockMHz { get; set; }
        /// <summary>
        /// Gets or sets the pcie tx bytes.
        /// </summary>
        /// <value>The pcie tx bytes.</value>
        public uint PcieTxBytes { get; set; }
        /// <summary>
        /// Gets or sets the pcie rx bytes.
        /// </summary>
        /// <value>The pcie rx bytes.</value>
        public uint PcieRxBytes { get; set; }
        /// <summary>
        /// Gets or sets the fan speed percent.
        /// </summary>
        /// <value>The fan speed percent.</value>
        public uint FanSpeedPercent { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether throttling.
        /// </summary>
        /// <value>The is throttling.</value>
        public bool IsThrottling { get; set; }
        /// <summary>
        /// Gets or sets the throttle reasons.
        /// </summary>
        /// <value>The throttle reasons.</value>
        public string ThrottleReasons { get; set; } = string.Empty;
        /// <summary>
        /// Gets to string.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public override string ToString()
        {
            if (!IsAvailable)
            {

                return "GPU Metrics: Not Available";
            }


            return $"GPU {DeviceIndex}: {Temperature}°C, {PowerUsage:F1}W, " +
                   $"GPU: {GpuUtilization}%, Mem: {MemoryUtilization:F1}% ({MemoryUsed / 1048576}MB/{MemoryTotal / 1048576}MB), " +
                   $"Clocks: {GraphicsClockMHz}/{MemoryClockMHz}MHz" +
                   (IsThrottling ? $" [Throttling: {ThrottleReasons}]" : "");
        }
    }
}