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
        Capabilities = new Dictionary<string, object>();
        MemoryHierarchy = new Dictionary<string, long>();
        ComputeUnits = new List<ComputeUnitInfo>();
        SupportedFeatures = new HashSet<string>();
        PerformanceCounters = new Dictionary<string, double>();
        DetectedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the type of compute device.
    /// </summary>
    public BackendType DeviceType { get; }

    /// <summary>
    /// Gets the name of the device.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Gets or sets the device vendor.
    /// </summary>
    public string? Vendor { get; set; }

    /// <summary>
    /// Gets or sets the device architecture.
    /// </summary>
    public string? Architecture { get; set; }

    /// <summary>
    /// Gets or sets the compute capability (for CUDA) or equivalent.
    /// </summary>
    public string? ComputeCapability { get; set; }

    /// <summary>
    /// Gets or sets the total global memory in bytes.
    /// </summary>
    public long TotalGlobalMemory { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets the memory bus width in bits.
    /// </summary>
    public int MemoryBusWidth { get; set; }

    /// <summary>
    /// Gets or sets the base clock frequency in MHz.
    /// </summary>
    public int BaseClockMHz { get; set; }

    /// <summary>
    /// Gets or sets the boost clock frequency in MHz.
    /// </summary>
    public int? BoostClockMHz { get; set; }

    /// <summary>
    /// Gets or sets the number of compute units (CUs) or streaming multiprocessors (SMs).
    /// </summary>
    public int ComputeUnitCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum threads per compute unit.
    /// </summary>
    public int MaxThreadsPerComputeUnit { get; set; }

    /// <summary>
    /// Gets or sets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum work group dimensions.
    /// </summary>
    public int[]? MaxWorkGroupDimensions { get; set; }

    /// <summary>
    /// Gets or sets the warp/wavefront size.
    /// </summary>
    public int WarpSize { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size per compute unit in bytes.
    /// </summary>
    public long SharedMemoryPerComputeUnit { get; set; }

    /// <summary>
    /// Gets or sets the constant memory size in bytes.
    /// </summary>
    public long ConstantMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the L1 cache size in bytes.
    /// </summary>
    public long L1CacheSize { get; set; }

    /// <summary>
    /// Gets or sets the L2 cache size in bytes.
    /// </summary>
    public long L2CacheSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum registers per thread.
    /// </summary>
    public int MaxRegistersPerThread { get; set; }

    /// <summary>
    /// Gets or sets the theoretical peak FLOPS (single precision).
    /// </summary>
    public double PeakFlopsSingle { get; set; }

    /// <summary>
    /// Gets or sets the theoretical peak FLOPS (double precision).
    /// </summary>
    public double PeakFlopsDouble { get; set; }

    /// <summary>
    /// Gets or sets the thermal design power in watts.
    /// </summary>
    public double ThermalDesignPowerWatts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device supports unified memory.
    /// </summary>
    public bool SupportsUnifiedMemory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device supports peer-to-peer transfers.
    /// </summary>
    public bool SupportsPeerToPeer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device supports ECC memory.
    /// </summary>
    public bool SupportsECC { get; set; }

    /// <summary>
    /// Gets device-specific capabilities.
    /// </summary>
    public Dictionary<string, object> Capabilities { get; }

    /// <summary>
    /// Gets memory hierarchy information.
    /// </summary>
    public Dictionary<string, long> MemoryHierarchy { get; }

    /// <summary>
    /// Gets detailed compute unit information.
    /// </summary>
    public List<ComputeUnitInfo> ComputeUnits { get; }

    /// <summary>
    /// Gets supported features and extensions.
    /// </summary>
    public HashSet<string> SupportedFeatures { get; }

    /// <summary>
    /// Gets performance counter values.
    /// </summary>
    public Dictionary<string, double> PerformanceCounters { get; }

    /// <summary>
    /// Gets the timestamp when this hardware info was detected.
    /// </summary>
    public DateTimeOffset DetectedAt { get; }

    /// <summary>
    /// Gets or sets the driver version.
    /// </summary>
    public string? DriverVersion { get; set; }

    /// <summary>
    /// Gets or sets the runtime version (e.g., CUDA runtime version).
    /// </summary>
    public string? RuntimeVersion { get; set; }

    /// <summary>
    /// Adds a capability to the hardware info.
    /// </summary>
    /// <param name="name">The capability name.</param>
    /// <param name="value">The capability value.</param>
    public void AddCapability(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Capability name cannot be null or whitespace.", nameof(name));

        Capabilities[name] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets a capability by name.
    /// </summary>
    /// <typeparam name="T">The type of the capability value.</typeparam>
    /// <param name="name">The capability name.</param>
    /// <returns>The capability value if found; otherwise, default(T).</returns>
    public T? GetCapability<T>(string name)
    {
        if (Capabilities.TryGetValue(name, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    /// Adds memory hierarchy information.
    /// </summary>
    /// <param name="level">The memory level (e.g., "L1", "L2", "Global").</param>
    /// <param name="size">The memory size in bytes.</param>
    public void AddMemoryLevel(string level, long size)
    {
        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException("Memory level cannot be null or whitespace.", nameof(level));

        MemoryHierarchy[level] = size;
    }

    /// <summary>
    /// Adds a supported feature.
    /// </summary>
    /// <param name="feature">The feature name.</param>
    public void AddSupportedFeature(string feature)
    {
        if (!string.IsNullOrWhiteSpace(feature))
            SupportedFeatures.Add(feature);
    }

    /// <summary>
    /// Checks if a feature is supported.
    /// </summary>
    /// <param name="feature">The feature name.</param>
    /// <returns>True if the feature is supported; otherwise, false.</returns>
    public bool IsFeatureSupported(string feature)
    {
        return SupportedFeatures.Contains(feature);
    }

    /// <summary>
    /// Adds a performance counter value.
    /// </summary>
    /// <param name="counter">The counter name.</param>
    /// <param name="value">The counter value.</param>
    public void AddPerformanceCounter(string counter, double value)
    {
        if (string.IsNullOrWhiteSpace(counter))
            throw new ArgumentException("Counter name cannot be null or whitespace.", nameof(counter));

        PerformanceCounters[counter] = value;
    }

    /// <summary>
    /// Calculates the theoretical memory bandwidth utilization.
    /// </summary>
    /// <param name="actualBandwidthGBps">The actual measured bandwidth.</param>
    /// <returns>The bandwidth utilization as a percentage (0-100).</returns>
    public double CalculateBandwidthUtilization(double actualBandwidthGBps)
    {
        if (MemoryBandwidthGBps <= 0) return 0.0;
        return Math.Min(100.0, (actualBandwidthGBps / MemoryBandwidthGBps) * 100.0);
    }

    /// <summary>
    /// Calculates the theoretical compute utilization.
    /// </summary>
    /// <param name="actualFlops">The actual measured FLOPS.</param>
    /// <param name="doublePrecision">Whether to use double precision peak performance.</param>
    /// <returns>The compute utilization as a percentage (0-100).</returns>
    public double CalculateComputeUtilization(double actualFlops, bool doublePrecision = false)
    {
        var peakFlops = doublePrecision ? PeakFlopsDouble : PeakFlopsSingle;
        if (peakFlops <= 0) return 0.0;
        return Math.Min(100.0, (actualFlops / peakFlops) * 100.0);
    }

    /// <summary>
    /// Gets the compute intensity threshold for this hardware.
    /// </summary>
    /// <returns>The compute intensity (FLOPS per byte) where the device becomes compute-bound.</returns>
    public double GetComputeIntensityThreshold()
    {
        if (MemoryBandwidthGBps <= 0 || PeakFlopsSingle <= 0) return 1.0;
        return PeakFlopsSingle / (MemoryBandwidthGBps * 1e9);
    }

    /// <summary>
    /// Determines the optimal work group size for this hardware.
    /// </summary>
    /// <param name="kernelComplexity">The complexity level of the kernel.</param>
    /// <returns>The recommended work group size.</returns>
    public int GetOptimalWorkGroupSize(KernelComplexity kernelComplexity = KernelComplexity.Medium)
    {
        var baseSize = WarpSize * 4; // Start with 4 warps/wavefronts
        
        return kernelComplexity switch
        {
            KernelComplexity.Simple => Math.Min(MaxWorkGroupSize, baseSize * 2),
            KernelComplexity.Medium => Math.Min(MaxWorkGroupSize, baseSize),
            KernelComplexity.Complex => Math.Min(MaxWorkGroupSize, baseSize / 2),
            _ => Math.Min(MaxWorkGroupSize, baseSize)
        };
    }

    /// <summary>
    /// Gets a human-readable summary of the hardware information.
    /// </summary>
    /// <returns>A formatted summary string.</returns>
    public string GetSummary()
    {
        var memoryGB = TotalGlobalMemory / (1024.0 * 1024.0 * 1024.0);
        var peakTFlops = PeakFlopsSingle / 1e12;

        return $"{DeviceName} ({DeviceType}): " +
               $"{memoryGB:F1} GB Memory, " +
               $"{MemoryBandwidthGBps:F0} GB/s Bandwidth, " +
               $"{ComputeUnitCount} CUs, " +
               $"{peakTFlops:F1} TFLOPS Peak, " +
               $"{WarpSize} Warp Size";
    }

    /// <summary>
    /// Creates a copy of the hardware info with updated performance counters.
    /// </summary>
    /// <returns>A new instance with copied values.</returns>
    public HardwareInfo Clone()
    {
        var clone = new HardwareInfo(DeviceType, DeviceName)
        {
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
        };

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
    }

    /// <inheritdoc/>
    public bool Equals(HardwareInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return DeviceType == other.DeviceType &&
               DeviceName == other.DeviceName &&
               ComputeCapability == other.ComputeCapability;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is HardwareInfo other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(DeviceType, DeviceName, ComputeCapability);

    /// <inheritdoc/>
    public override string ToString() => GetSummary();
}

/// <summary>
/// Contains information about an individual compute unit.
/// </summary>
public sealed class ComputeUnitInfo
{
    /// <summary>
    /// Gets or sets the compute unit index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the number of cores in this compute unit.
    /// </summary>
    public int CoreCount { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size for this compute unit.
    /// </summary>
    public long SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the register file size for this compute unit.
    /// </summary>
    public int RegisterFileSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum occupancy for this compute unit.
    /// </summary>
    public double MaxOccupancy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this compute unit is currently available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>
/// Defines kernel complexity levels for optimization hints.
/// </summary>
public enum KernelComplexity
{
    /// <summary>
    /// Simple kernels with minimal computation per thread.
    /// </summary>
    Simple,

    /// <summary>
    /// Medium complexity kernels with moderate computation.
    /// </summary>
    Medium,

    /// <summary>
    /// Complex kernels with heavy computation per thread.
    /// </summary>
    Complex
}