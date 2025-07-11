using System;

namespace DotCompute.Abstractions;

/// <summary>
/// Contains information about an accelerator device.
/// This is a value type for AOT compatibility and zero allocations.
/// </summary>
public readonly struct AcceleratorInfo : IEquatable<AcceleratorInfo>
{
    /// <summary>
    /// Gets the name of the accelerator device.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the type of accelerator.
    /// </summary>
    public AcceleratorType Type { get; }
    
    /// <summary>
    /// Gets the total memory available on the device in bytes.
    /// </summary>
    public long TotalMemory { get; }
    
    /// <summary>
    /// Gets the compute capability version (major.minor format).
    /// </summary>
    public Version ComputeCapability { get; }
    
    /// <summary>
    /// Gets the maximum number of threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; }
    
    /// <summary>
    /// Gets the maximum grid dimensions.
    /// </summary>
    public Dim3 MaxGridDimensions { get; }
    
    /// <summary>
    /// Gets the maximum block dimensions.
    /// </summary>
    public Dim3 MaxBlockDimensions { get; }
    
    /// <summary>
    /// Gets the warp/wavefront size for this accelerator.
    /// </summary>
    public int WarpSize { get; }
    
    /// <summary>
    /// Gets the number of multiprocessors/compute units.
    /// </summary>
    public int MultiprocessorCount { get; }
    
    /// <summary>
    /// Gets the clock rate in MHz.
    /// </summary>
    public int ClockRateMHz { get; }
    
    /// <summary>
    /// Gets the supported features of this accelerator.
    /// </summary>
    public AcceleratorFeature SupportedFeatures { get; }
    
    public AcceleratorInfo(
        string name,
        AcceleratorType type,
        long totalMemory,
        Version computeCapability,
        int maxThreadsPerBlock,
        Dim3 maxGridDimensions,
        Dim3 maxBlockDimensions,
        int warpSize,
        int multiprocessorCount,
        int clockRateMHz,
        AcceleratorFeature supportedFeatures)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        TotalMemory = totalMemory;
        ComputeCapability = computeCapability ?? throw new ArgumentNullException(nameof(computeCapability));
        MaxThreadsPerBlock = maxThreadsPerBlock;
        MaxGridDimensions = maxGridDimensions;
        MaxBlockDimensions = maxBlockDimensions;
        WarpSize = warpSize;
        MultiprocessorCount = multiprocessorCount;
        ClockRateMHz = clockRateMHz;
        SupportedFeatures = supportedFeatures;
    }
    
    public bool Equals(AcceleratorInfo other)
    {
        return Name == other.Name &&
               Type == other.Type &&
               TotalMemory == other.TotalMemory &&
               ComputeCapability.Equals(other.ComputeCapability);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is AcceleratorInfo other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Type, TotalMemory, ComputeCapability);
    }
    
    public static bool operator ==(AcceleratorInfo left, AcceleratorInfo right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(AcceleratorInfo left, AcceleratorInfo right)
    {
        return !left.Equals(right);
    }
    
    public override string ToString()
    {
        return $"{Name} ({Type}) - {TotalMemory / (1024 * 1024 * 1024)}GB, Compute {ComputeCapability}";
    }
}

/// <summary>
/// Defines the type of accelerator device.
/// </summary>
public enum AcceleratorType
{
    /// <summary>
    /// CPU-based computation.
    /// </summary>
    CPU,
    
    /// <summary>
    /// NVIDIA CUDA GPU.
    /// </summary>
    CUDA,
    
    /// <summary>
    /// AMD ROCm GPU.
    /// </summary>
    ROCm,
    
    /// <summary>
    /// Intel oneAPI GPU.
    /// </summary>
    OneAPI,
    
    /// <summary>
    /// Apple Metal GPU.
    /// </summary>
    Metal,
    
    /// <summary>
    /// OpenCL-compatible device.
    /// </summary>
    OpenCL,
    
    /// <summary>
    /// DirectML-compatible device.
    /// </summary>
    DirectML,
    
    /// <summary>
    /// Custom or unknown accelerator type.
    /// </summary>
    Custom
}