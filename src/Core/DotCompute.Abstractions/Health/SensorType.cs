// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Health;

/// <summary>
/// Defines the types of sensors available for device health monitoring.
/// </summary>
/// <remarks>
/// Different compute devices support different sensor types. CPU devices typically
/// support temperature and utilization sensors, while GPU devices may additionally
/// support power, fan speed, clock frequencies, and memory statistics.
///
/// Availability varies by:
/// - Backend type (CUDA, OpenCL, Metal, CPU)
/// - Hardware vendor (NVIDIA, AMD, Intel, Apple)
/// - Driver version and capabilities
/// - Operating system
///
/// Applications should check sensor availability before querying values.
/// </remarks>
public enum SensorType
{
    /// <summary>
    /// Temperature sensor in degrees Celsius.
    /// Supported by most GPU and CPU devices.
    /// </summary>
    /// <remarks>
    /// Typical ranges:
    /// - GPU: 30-95°C (throttling typically begins at 80-85°C)
    /// - CPU: 30-100°C (varies by model and TDP)
    /// </remarks>
    Temperature = 0,

    /// <summary>
    /// Power consumption in watts.
    /// Primarily supported by discrete GPUs with power monitoring.
    /// </summary>
    /// <remarks>
    /// NVIDIA GPUs: Available via NVML
    /// AMD GPUs: Available via ROCm SMI
    /// Intel GPUs: Limited availability
    /// CPUs: Available on some platforms via RAPL (Running Average Power Limit)
    /// </remarks>
    PowerDraw = 1,

    /// <summary>
    /// Compute utilization percentage (0-100).
    /// Indicates how busy the device's compute cores are.
    /// </summary>
    /// <remarks>
    /// For GPUs: Percentage of time at least one kernel is executing
    /// For CPUs: Average utilization across all cores
    /// </remarks>
    ComputeUtilization = 2,

    /// <summary>
    /// Memory utilization percentage (0-100).
    /// Indicates memory bandwidth usage, not capacity.
    /// </summary>
    /// <remarks>
    /// Measures memory controller activity, not allocated memory.
    /// For capacity usage, see MemoryUsedBytes and MemoryTotalBytes.
    /// </remarks>
    MemoryUtilization = 3,

    /// <summary>
    /// Fan speed percentage (0-100).
    /// Only available on devices with active cooling fans.
    /// </summary>
    /// <remarks>
    /// Typically only available on:
    /// - Desktop discrete GPUs
    /// - Some high-end workstation CPUs
    /// Not available on mobile devices or passively cooled hardware.
    /// </remarks>
    FanSpeed = 4,

    /// <summary>
    /// Graphics (core) clock frequency in MHz.
    /// Indicates current operating frequency of compute cores.
    /// </summary>
    /// <remarks>
    /// May vary dynamically based on:
    /// - Workload
    /// - Temperature
    /// - Power limits
    /// - Boost clocks
    /// </remarks>
    GraphicsClock = 5,

    /// <summary>
    /// Memory clock frequency in MHz.
    /// Indicates current operating frequency of device memory.
    /// </summary>
    MemoryClock = 6,

    /// <summary>
    /// Absolute amount of memory currently used in bytes.
    /// Different from MemoryUtilization which measures bandwidth.
    /// </summary>
    MemoryUsedBytes = 7,

    /// <summary>
    /// Total device memory capacity in bytes.
    /// </summary>
    MemoryTotalBytes = 8,

    /// <summary>
    /// Available (free) device memory in bytes.
    /// Calculated as: TotalMemory - UsedMemory
    /// </summary>
    MemoryFreeBytes = 9,

    /// <summary>
    /// PCIe generation in use (e.g., 3 for PCIe 3.0, 4 for PCIe 4.0).
    /// Only applicable to PCIe-attached devices.
    /// </summary>
    /// <remarks>
    /// Not available for:
    /// - Integrated GPUs
    /// - Apple Silicon unified memory systems
    /// - CPU backends
    /// </remarks>
    PcieLinkGeneration = 10,

    /// <summary>
    /// PCIe link width (number of lanes, e.g., 8, 16).
    /// Only applicable to PCIe-attached devices.
    /// </summary>
    PcieLinkWidth = 11,

    /// <summary>
    /// PCIe transmit throughput in bytes per second.
    /// Measures data sent from host to device.
    /// </summary>
    PcieThroughputTx = 12,

    /// <summary>
    /// PCIe receive throughput in bytes per second.
    /// Measures data received from device to host.
    /// </summary>
    PcieThroughputRx = 13,

    /// <summary>
    /// Thermal throttling status (0 = none, >0 = active throttling).
    /// Indicates performance reduction due to thermal limits.
    /// </summary>
    /// <remarks>
    /// Values may indicate severity:
    /// - 0: No throttling
    /// - 1: Slight throttling
    /// - 2: Moderate throttling
    /// - 3+: Severe throttling
    ///
    /// Check vendor documentation for specific meanings.
    /// </remarks>
    ThrottlingStatus = 14,

    /// <summary>
    /// Power throttling status (0 = none, >0 = active throttling).
    /// Indicates performance reduction due to power limits.
    /// </summary>
    PowerThrottlingStatus = 15,

    /// <summary>
    /// Number of compute errors detected.
    /// Includes ECC errors, kernel crashes, and device resets.
    /// </summary>
    /// <remarks>
    /// High error counts may indicate:
    /// - Hardware issues
    /// - Driver instability
    /// - Overheating
    /// - Power supply problems
    /// </remarks>
    ErrorCount = 16,

    /// <summary>
    /// Custom vendor-specific sensor.
    /// Used for platform-specific metrics not covered by standard types.
    /// </summary>
    /// <remarks>
    /// Check SensorReading.Name property for vendor-specific identifier.
    /// Examples:
    /// - NVIDIA: NVLink throughput
    /// - AMD: Infinity Fabric metrics
    /// - Intel: EU (Execution Unit) utilization
    /// </remarks>
    Custom = 999
}
