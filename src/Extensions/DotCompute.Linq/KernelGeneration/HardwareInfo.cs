// <copyright file="HardwareInfo.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Types;
namespace DotCompute.Linq.KernelGeneration;
/// <summary>
/// Contains comprehensive hardware capability information for kernel optimization and compilation.
/// Provides detailed specs for CPU, GPU, and other compute devices.
/// </summary>
public sealed class HardwareInfo : IEquatable<HardwareInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HardwareInfo"/> class.
    /// </summary>
    /// <param name="deviceType">The type of compute device.</param>
    /// <param name="deviceName">The name of the device.</param>
    public HardwareInfo(BackendType deviceType, string deviceName)
    {
        DeviceType = deviceType;
        DeviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
        Capabilities = [];
        MemoryHierarchy = [];
        ComputeUnits = [];
        SupportedFeatures = [];
        PerformanceCounters = [];
        DetectedAt = DateTimeOffset.UtcNow;
    }
    /// Gets the type of compute device.
    public BackendType DeviceType { get; }
    /// Gets the name of the device.
    public string DeviceName { get; }
    /// Gets or sets the device vendor.
    public string? Vendor { get; set; }
    /// Gets or sets the device architecture.
    public string? Architecture { get; set; }
    /// Gets or sets the compute capability (for CUDA) or equivalent.
    public string? ComputeCapability { get; set; }
    /// Gets or sets the total global memory in bytes.
    public long TotalGlobalMemory { get; set; }
    /// Gets or sets the memory bandwidth in GB/s.
    public double MemoryBandwidthGBps { get; set; }
    /// Gets or sets the memory bus width in bits.
    public int MemoryBusWidth { get; set; }
    /// Gets or sets the base clock frequency in MHz.
    public int BaseClockMHz { get; set; }
    /// Gets or sets the boost clock frequency in MHz.
    public int? BoostClockMHz { get; set; }
    /// Gets or sets the number of compute units (CUs) or streaming multiprocessors (SMs).
    public int ComputeUnitCount { get; set; }
    /// Gets or sets the maximum threads per compute unit.
    public int MaxThreadsPerComputeUnit { get; set; }
    /// Gets or sets the maximum work group size.
    public int MaxWorkGroupSize { get; set; }
    /// Gets or sets the maximum work group dimensions.
    public int[]? MaxWorkGroupDimensions { get; set; }
    /// Gets or sets the warp/wavefront size.
    public int WarpSize { get; set; }
    /// Gets or sets the shared memory size per compute unit in bytes.
    public long SharedMemoryPerComputeUnit { get; set; }
    /// Gets or sets the constant memory size in bytes.
    public long ConstantMemorySize { get; set; }
    /// Gets or sets the L1 cache size in bytes.
    public long L1CacheSize { get; set; }
    /// Gets or sets the L2 cache size in bytes.
    public long L2CacheSize { get; set; }
    /// Gets or sets the maximum registers per thread.
    public int MaxRegistersPerThread { get; set; }
    /// Gets or sets the theoretical peak FLOPS (single precision).
    public double PeakFlopsSingle { get; set; }
    /// Gets or sets the theoretical peak FLOPS (double precision).
    public double PeakFlopsDouble { get; set; }
    /// Gets or sets the thermal design power in watts.
    public double ThermalDesignPowerWatts { get; set; }
    /// Gets or sets a value indicating whether the device supports unified memory.
    public bool SupportsUnifiedMemory { get; set; }
    /// Gets or sets a value indicating whether the device supports peer-to-peer transfers.
    public bool SupportsPeerToPeer { get; set; }
    /// Gets or sets a value indicating whether the device supports ECC memory.
    public bool SupportsECC { get; set; }
    /// Gets device-specific capabilities.
    public Dictionary<string, object> Capabilities { get; }
    /// Gets memory hierarchy information.
    public Dictionary<string, long> MemoryHierarchy { get; }
    /// Gets detailed compute unit information.
    public List<ComputeUnitInfo> ComputeUnits { get; }
    /// Gets supported features and extensions.
    public HashSet<string> SupportedFeatures { get; }
    /// Gets performance counter values.
    public Dictionary<string, double> PerformanceCounters { get; }
    /// Gets the timestamp when this hardware info was detected.
    public DateTimeOffset DetectedAt { get; }
    /// Gets or sets the driver version.
    public string? DriverVersion { get; set; }
    /// Gets or sets the runtime version (e.g., CUDA runtime version).
    public string? RuntimeVersion { get; set; }
    /// Adds a capability to the hardware info.
    /// <param name="name">The capability name.</param>
    /// <param name="value">The capability value.</param>
    public void AddCapability(string name, object value)
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Capability name cannot be null or whitespace.", nameof(name));
        }
        Capabilities[name] = value ?? throw new ArgumentNullException(nameof(value));
    /// Gets a capability by name.
    /// <typeparam name="T">The type of the capability value.</typeparam>
    /// <returns>The capability value if found; otherwise, default(T).</returns>
    public T? GetCapability<T>(string name)
        if (Capabilities.TryGetValue(name, out var value) && value is T typedValue)
            return typedValue;
        return default;
    /// Adds memory hierarchy information.
    /// <param name="level">The memory level (e.g., "L1", "L2", "Global").</param>
    /// <param name="size">The memory size in bytes.</param>
    public void AddMemoryLevel(string level, long size)
        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException("Memory level cannot be null or whitespace.", nameof(level));
        MemoryHierarchy[level] = size;
    /// Adds a supported feature.
    /// <param name="feature">The feature name.</param>
    public void AddSupportedFeature(string feature)
        if (!string.IsNullOrWhiteSpace(feature))
            SupportedFeatures.Add(feature);
    /// Checks if a feature is supported.
    /// <returns>True if the feature is supported; otherwise, false.</returns>
    public bool IsFeatureSupported(string feature)
        return SupportedFeatures.Contains(feature);
    /// Adds a performance counter value.
    /// <param name="counter">The counter name.</param>
    /// <param name="value">The counter value.</param>
    public void AddPerformanceCounter(string counter, double value)
        if (string.IsNullOrWhiteSpace(counter))
            throw new ArgumentException("Counter name cannot be null or whitespace.", nameof(counter));
        PerformanceCounters[counter] = value;
    /// Calculates the theoretical memory bandwidth utilization.
    /// <param name="actualBandwidthGBps">The actual measured bandwidth.</param>
    /// <returns>The bandwidth utilization as a percentage (0-100).</returns>
    public double CalculateBandwidthUtilization(double actualBandwidthGBps)
        if (MemoryBandwidthGBps <= 0)
            return 0.0;
        return Math.Min(100.0, (actualBandwidthGBps / MemoryBandwidthGBps) * 100.0);
    /// Calculates the theoretical compute utilization.
    /// <param name="actualFlops">The actual measured FLOPS.</param>
    /// <param name="doublePrecision">Whether to use double precision peak performance.</param>
    /// <returns>The compute utilization as a percentage (0-100).</returns>
    public double CalculateComputeUtilization(double actualFlops, bool doublePrecision = false)
        var peakFlops = doublePrecision ? PeakFlopsDouble : PeakFlopsSingle;
        if (peakFlops <= 0)
        return Math.Min(100.0, (actualFlops / peakFlops) * 100.0);
    /// Gets the compute intensity threshold for this hardware.
    /// <returns>The compute intensity (FLOPS per byte) where the device becomes compute-bound.</returns>
    public double GetComputeIntensityThreshold()
        if (MemoryBandwidthGBps <= 0 || PeakFlopsSingle <= 0)
            return 1.0;
        return PeakFlopsSingle / (MemoryBandwidthGBps * 1e9);
    /// Determines the optimal work group size for this hardware.
    /// <param name="kernelComplexity">The complexity level of the kernel.</param>
    /// <returns>The recommended work group size.</returns>
    public int GetOptimalWorkGroupSize(KernelComplexity kernelComplexity = KernelComplexity.Medium)
        var baseSize = WarpSize * 4; // Start with 4 warps/wavefronts
        return kernelComplexity switch
            KernelComplexity.Simple => Math.Min(MaxWorkGroupSize, baseSize * 2),
            KernelComplexity.Medium => Math.Min(MaxWorkGroupSize, baseSize),
            KernelComplexity.Complex => Math.Min(MaxWorkGroupSize, baseSize / 2),
            _ => Math.Min(MaxWorkGroupSize, baseSize)
        };
    /// Gets a human-readable summary of the hardware information.
    /// <returns>A formatted summary string.</returns>
    public string GetSummary()
        var memoryGB = TotalGlobalMemory / (1024.0 * 1024.0 * 1024.0);
        var peakTFlops = PeakFlopsSingle / 1e12;
        return $"{DeviceName} ({DeviceType}): " +
               $"{memoryGB:F1} GB Memory, " +
               $"{MemoryBandwidthGBps:F0} GB/s Bandwidth, " +
               $"{ComputeUnitCount} CUs, " +
               $"{peakTFlops:F1} TFLOPS Peak, " +
               $"{WarpSize} Warp Size";
    /// Creates a copy of the hardware info with updated performance counters.
    /// <returns>A new instance with copied values.</returns>
    public HardwareInfo Clone()
        var clone = new HardwareInfo(DeviceType, DeviceName)
            Vendor = Vendor,
            Architecture = Architecture,
            ComputeCapability = ComputeCapability,
            TotalGlobalMemory = TotalGlobalMemory,
            MemoryBandwidthGBps = MemoryBandwidthGBps,
            MemoryBusWidth = MemoryBusWidth,
            BaseClockMHz = BaseClockMHz,
            BoostClockMHz = BoostClockMHz,
            ComputeUnitCount = ComputeUnitCount,
            MaxThreadsPerComputeUnit = MaxThreadsPerComputeUnit,
            MaxWorkGroupSize = MaxWorkGroupSize,
            MaxWorkGroupDimensions = MaxWorkGroupDimensions?.ToArray(),
            WarpSize = WarpSize,
            SharedMemoryPerComputeUnit = SharedMemoryPerComputeUnit,
            ConstantMemorySize = ConstantMemorySize,
            L1CacheSize = L1CacheSize,
            L2CacheSize = L2CacheSize,
            MaxRegistersPerThread = MaxRegistersPerThread,
            PeakFlopsSingle = PeakFlopsSingle,
            PeakFlopsDouble = PeakFlopsDouble,
            ThermalDesignPowerWatts = ThermalDesignPowerWatts,
            SupportsUnifiedMemory = SupportsUnifiedMemory,
            SupportsPeerToPeer = SupportsPeerToPeer,
            SupportsECC = SupportsECC,
            DriverVersion = DriverVersion,
            RuntimeVersion = RuntimeVersion
        foreach (var capability in Capabilities)
            clone.Capabilities[capability.Key] = capability.Value;
        foreach (var memory in MemoryHierarchy)
            clone.MemoryHierarchy[memory.Key] = memory.Value;
        foreach (var unit in ComputeUnits)
            clone.ComputeUnits.Add(unit);
        foreach (var feature in SupportedFeatures)
            clone.SupportedFeatures.Add(feature);
        foreach (var counter in PerformanceCounters)
            clone.PerformanceCounters[counter.Key] = counter.Value;
        return clone;
    /// <inheritdoc/>
    public bool Equals(HardwareInfo? other)
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return DeviceType == other.DeviceType &&
               DeviceName == other.DeviceName &&
               ComputeCapability == other.ComputeCapability;
    public override bool Equals(object? obj) => obj is HardwareInfo other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(DeviceType, DeviceName, ComputeCapability);
    public override string ToString() => GetSummary();
}
/// Contains information about an individual compute unit.
public sealed class ComputeUnitInfo
    /// Gets or sets the compute unit index.
    public int Index { get; set; }
    /// Gets or sets the number of cores in this compute unit.
    public int CoreCount { get; set; }
    /// Gets or sets the shared memory size for this compute unit.
    public long SharedMemorySize { get; set; }
    /// Gets or sets the register file size for this compute unit.
    public int RegisterFileSize { get; set; }
    /// Gets or sets the maximum occupancy for this compute unit.
    public double MaxOccupancy { get; set; }
    /// Gets or sets a value indicating whether this compute unit is currently available.
    public bool IsAvailable { get; set; } = true;
/// Defines kernel complexity levels for optimization hints.
public enum KernelComplexity
    /// Simple kernels with minimal computation per thread.
    Simple,
    /// Medium complexity kernels with moderate computation.
    Medium,
    /// Complex kernels with heavy computation per thread.
    Complex
