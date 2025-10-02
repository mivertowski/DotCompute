using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Models.Device;

namespace DotCompute.Abstractions.Models;

/// <summary>
/// Represents the capabilities and features of a compute device
/// </summary>
public sealed class DeviceCapabilities
{
    /// <summary>
    /// Gets or sets the device type
    /// </summary>
    public AcceleratorType DeviceType { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of compute units
    /// </summary>
    public int MaxComputeUnits { get; set; }

    /// <summary>
    /// Gets or sets the maximum work group size
    /// </summary>
    public int MaxWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum work item dimensions
    /// </summary>
    public int MaxWorkItemDimensions { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum work item sizes for each dimension
    /// </summary>
    public int[] MaxWorkItemSizes { get; set; } = new int[3];

    /// <summary>
    /// Gets or sets the global memory size in bytes
    /// </summary>
    public long GlobalMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the local memory size in bytes
    /// </summary>
    public long LocalMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the constant memory size in bytes
    /// </summary>
    public long ConstantMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory allocation size in bytes
    /// </summary>
    public long MaxMemoryAllocationSize { get; set; }

    /// <summary>
    /// Gets or sets whether the device supports unified memory
    /// </summary>
    public bool SupportsUnifiedMemory { get; set; }

    /// <summary>
    /// Gets or sets whether the device supports double precision
    /// </summary>
    public bool SupportsDoublePrecision { get; set; }

    /// <summary>
    /// Gets or sets whether the device supports half precision
    /// </summary>
    public bool SupportsHalfPrecision { get; set; }

    /// <summary>
    /// Gets or sets the supported data types
    /// </summary>
    public DataTypeSupport SupportedDataTypes { get; set; }


    /// <summary>
    /// Gets or sets the clock frequency in MHz
    /// </summary>
    public int ClockFrequency { get; set; }

    /// <summary>
    /// Gets or sets the memory clock frequency in MHz
    /// </summary>
    public int MemoryClockFrequency { get; set; }

    /// <summary>
    /// Gets or sets the memory bus width in bits
    /// </summary>
    public int MemoryBusWidth { get; set; }

    /// <summary>
    /// Gets or sets the compute capability major version (for CUDA devices)
    /// </summary>
    public int ComputeCapabilityMajor { get; set; }

    /// <summary>
    /// Gets or sets the compute capability minor version (for CUDA devices)
    /// </summary>
    public int ComputeCapabilityMinor { get; set; }

    /// <summary>
    /// Gets or sets the warp size (for CUDA/GPU devices)
    /// </summary>
    public int WarpSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of threads per block
    /// </summary>
    public int MaxThreadsPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the maximum block dimensions
    /// </summary>
    public Dim3 MaxBlockDimensions { get; set; }


    /// <summary>
    /// Gets or sets the maximum grid dimensions
    /// </summary>
    public Dim3 MaxGridDimensions { get; set; }


    /// <summary>
    /// Gets or sets the available device features
    /// </summary>
    public DeviceFeatures Features { get; set; }


    /// <summary>
    /// Gets or sets whether the device supports async operations
    /// </summary>
    public bool SupportsAsyncOperations { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the device supports peer-to-peer memory access
    /// </summary>
    public bool SupportsPeerToPeerAccess { get; set; }

    /// <summary>
    /// Gets or sets the device's cache configuration
    /// </summary>
    public CacheConfig CacheConfiguration { get; set; } = CacheConfig.PreferNone;

    /// <summary>
    /// Gets or sets additional vendor-specific properties
    /// </summary>
    public Dictionary<string, object> VendorProperties { get; set; } = [];

    /// <summary>
    /// Creates a new instance of DeviceCapabilities
    /// </summary>
    public DeviceCapabilities() { }

    /// <summary>
    /// Gets the compute capability as a decimal value
    /// </summary>
    public decimal ComputeCapability => ComputeCapabilityMajor + (ComputeCapabilityMinor / 10m);

    /// <summary>
    /// Checks if the device supports a specific feature
    /// </summary>
    /// <param name="feature">The feature to check</param>
    /// <returns>True if the feature is supported, false otherwise</returns>
    public bool SupportsFeature(DeviceFeature feature)
    {
        return Features.HasFlag((DeviceFeatures)feature);
    }

    /// <summary>
    /// Gets the maximum theoretical memory bandwidth in GB/s
    /// </summary>
    public double GetMaxMemoryBandwidth()
    {
        if (MemoryClockFrequency == 0 || MemoryBusWidth == 0)
        {
            return 0;
        }

        // Memory bandwidth = (Memory Clock * Bus Width * 2) / 8 / 1000

        return (MemoryClockFrequency * MemoryBusWidth * 2.0) / (8 * 1000);
    }
}