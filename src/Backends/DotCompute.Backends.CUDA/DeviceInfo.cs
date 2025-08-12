// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Detailed device information for CUDA devices.
/// </summary>
public class DeviceInfo
{
    /// <summary>Gets or sets the device name.</summary>
    public required string Name { get; init; }
    
    /// <summary>Gets or sets the device ID.</summary>
    public required int DeviceId { get; init; }
    
    /// <summary>Gets or sets the compute capability version.</summary>
    public required Version ComputeCapability { get; init; }
    
    /// <summary>Gets or sets the architecture generation (e.g., "Ada Lovelace").</summary>
    public required string ArchitectureGeneration { get; init; }
    
    /// <summary>Gets or sets whether this is an RTX 2000 Ada Generation GPU.</summary>
    public required bool IsRTX2000Ada { get; init; }
    
    /// <summary>Gets or sets the number of streaming multiprocessors.</summary>
    public required int StreamingMultiprocessors { get; init; }
    
    /// <summary>Gets or sets the estimated CUDA cores count.</summary>
    public required int EstimatedCudaCores { get; init; }
    
    /// <summary>Gets or sets the total memory in bytes.</summary>
    public required ulong TotalMemory { get; init; }
    
    /// <summary>Gets or sets the available memory in bytes.</summary>
    public required ulong AvailableMemory { get; init; }
    
    /// <summary>Gets or sets the memory bandwidth in GB/s.</summary>
    public required double MemoryBandwidthGBps { get; init; }
    
    /// <summary>Gets or sets the maximum threads per block.</summary>
    public required int MaxThreadsPerBlock { get; init; }
    
    /// <summary>Gets or sets the warp size.</summary>
    public required int WarpSize { get; init; }
    
    /// <summary>Gets or sets the shared memory per block in bytes.</summary>
    public required ulong SharedMemoryPerBlock { get; init; }
    
    /// <summary>Gets or sets the L2 cache size in bytes.</summary>
    public required int L2CacheSize { get; init; }
    
    /// <summary>Gets or sets the clock rate in kHz.</summary>
    public required int ClockRate { get; init; }
    
    /// <summary>Gets or sets the memory clock rate in kHz.</summary>
    public required int MemoryClockRate { get; init; }
    
    /// <summary>Gets or sets whether unified addressing is supported.</summary>
    public required bool SupportsUnifiedAddressing { get; init; }
    
    /// <summary>Gets or sets whether managed memory is supported.</summary>
    public required bool SupportsManagedMemory { get; init; }
    
    /// <summary>Gets or sets whether concurrent kernels are supported.</summary>
    public required bool SupportsConcurrentKernels { get; init; }
    
    /// <summary>Gets or sets whether ECC is enabled.</summary>
    public required bool IsECCEnabled { get; init; }
}