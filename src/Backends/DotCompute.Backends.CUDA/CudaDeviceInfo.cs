// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA;


/// <summary>
/// Provides device information for compatibility with test/benchmark code.
/// </summary>
public class CudaDeviceInfo
{
private readonly CudaDevice _device;

internal CudaDeviceInfo(CudaDevice device)
{
    _device = device ?? throw new ArgumentNullException(nameof(device));
}

/// <summary>
/// Gets the device name.
/// </summary>
public string Name => _device.Name;

/// <summary>
/// Gets the compute capability as a version string.
/// </summary>
public string ComputeCapability => $"{_device.ComputeCapabilityMajor}.{_device.ComputeCapabilityMinor}";

/// <summary>
/// Gets the total memory in bytes.
/// </summary>
public ulong TotalMemory => _device.GlobalMemorySize;

/// <summary>
/// Gets the estimated number of CUDA cores based on compute capability and SM count.
/// </summary>
public int CudaCores
{
    get
    {
        var smCount = _device.StreamingMultiprocessorCount;
        return _device.ComputeCapabilityMajor switch
        {
            2 => smCount * 32,  // Fermi
            3 => smCount * 192, // Kepler
            5 => smCount * 128, // Maxwell
            6 => smCount * (_device.ComputeCapabilityMinor == 0 ? 64 : 128), // Pascal
            7 => smCount * 64,  // Volta/Turing
            8 => smCount * 64,  // Ampere
            9 => smCount * 128, // Ada Lovelace/Hopper
            _ => smCount * 64   // Default estimate
        };
    }
}

/// <summary>
/// Gets the memory bandwidth in GB/s.
/// </summary>
public double MemoryBandwidth => _device.MemoryBandwidthGBps;

/// <summary>
/// Gets the number of streaming multiprocessors.
/// </summary>
public int StreamingMultiprocessorCount => _device.StreamingMultiprocessorCount;

/// <summary>
/// Gets the maximum threads per block.
/// </summary>
public int MaxThreadsPerBlock => _device.MaxThreadsPerBlock;

/// <summary>
/// Gets the warp size.
/// </summary>
public int WarpSize => _device.WarpSize;

/// <summary>
/// Gets whether this is an RTX series GPU.
/// </summary>
public bool IsRTX => Name.Contains("RTX", StringComparison.OrdinalIgnoreCase);

/// <summary>
/// Gets the PCI bus information as a formatted string.
/// </summary>
public string PciInfo => $"{_device.PciDomainId:X4}:{_device.PciBusId:X2}:{_device.PciDeviceId:X2}";

/// <summary>
/// Gets the architecture generation.
/// </summary>
public string Architecture => _device.ArchitectureGeneration;
}
