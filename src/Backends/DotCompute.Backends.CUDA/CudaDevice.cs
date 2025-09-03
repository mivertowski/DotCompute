// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Represents a CUDA-capable GPU device with enhanced device detection capabilities.
    /// Provides detailed hardware information and capabilities for RTX 2000 Ada Generation and other CUDA devices.
    /// </summary>
    public sealed class CudaDevice : IDisposable
    {
        private readonly ILogger _logger;
        private readonly int _deviceId;
        private readonly CudaDeviceProperties _deviceProperties;
        private readonly Dictionary<string, object> _capabilities;
        private bool _disposed;

        /// <summary>
        /// Gets the device ID.
        /// </summary>
        public int DeviceId => _deviceId;

        /// <summary>
        /// Gets the device name.
        /// </summary>
        public string Name => _deviceProperties.DeviceName ?? $"CUDA Device {_deviceId}";

        /// <summary>
        /// Gets the compute capability major version.
        /// </summary>
        public int ComputeCapabilityMajor => _deviceProperties.Major;

        /// <summary>
        /// Gets the compute capability minor version.
        /// </summary>
        public int ComputeCapabilityMinor => _deviceProperties.Minor;

        /// <summary>
        /// Gets the compute capability as a version object.
        /// </summary>
        public Version ComputeCapability => new(_deviceProperties.Major, _deviceProperties.Minor);

        /// <summary>
        /// Gets the total global memory in bytes.
        /// </summary>
        public ulong GlobalMemorySize => _deviceProperties.TotalGlobalMem;

        /// <summary>
        /// Gets the number of streaming multiprocessors (SMs).
        /// </summary>
        public int StreamingMultiprocessorCount => _deviceProperties.MultiProcessorCount;

        /// <summary>
        /// Gets the maximum threads per block.
        /// </summary>
        public int MaxThreadsPerBlock => _deviceProperties.MaxThreadsPerBlock;

        /// <summary>
        /// Gets the maximum threads per multiprocessor.
        /// </summary>
        public int MaxThreadsPerMultiprocessor => _deviceProperties.MaxThreadsPerMultiProcessor;

        /// <summary>
        /// Gets the warp size.
        /// </summary>
        public int WarpSize => _deviceProperties.WarpSize;

        /// <summary>
        /// Gets the shared memory per block in bytes.
        /// </summary>
        public ulong SharedMemoryPerBlock => _deviceProperties.SharedMemPerBlock;

        /// <summary>
        /// Gets the constant memory size in bytes.
        /// </summary>
        public ulong ConstantMemorySize => _deviceProperties.TotalConstMem;

        /// <summary>
        /// Gets the L2 cache size in bytes.
        /// </summary>
        public int L2CacheSize => _deviceProperties.L2CacheSize;

        /// <summary>
        /// Gets the memory clock rate in kHz.
        /// </summary>
        public int MemoryClockRate => _deviceProperties.MemoryClockRate;

        /// <summary>
        /// Gets the memory bus width in bits.
        /// </summary>
        public int MemoryBusWidth => _deviceProperties.MemoryBusWidth;

        /// <summary>
        /// Gets the core clock rate in kHz.
        /// </summary>
        public int ClockRate => _deviceProperties.ClockRate;

        /// <summary>
        /// Gets whether the device supports unified addressing.
        /// </summary>
        public bool SupportsUnifiedAddressing => _deviceProperties.UnifiedAddressing > 0;

        /// <summary>
        /// Gets whether the device supports managed memory.
        /// </summary>
        public bool SupportsManagedMemory => _deviceProperties.ManagedMemory > 0;

        /// <summary>
        /// Gets whether the device supports concurrent kernels.
        /// </summary>
        public bool SupportsConcurrentKernels => _deviceProperties.ConcurrentKernels > 0;

        /// <summary>
        /// Gets whether the device supports concurrent managed access.
        /// </summary>
        public bool ConcurrentManagedAccess => _deviceProperties.ConcurrentManagedAccess > 0;

        /// <summary>
        /// Gets whether the device supports pageable memory access.
        /// </summary>
        public bool PageableMemoryAccess => _deviceProperties.PageableMemoryAccess > 0;

        /// <summary>
        /// Gets whether ECC (Error-Correcting Code) is enabled.
        /// </summary>
        public bool IsECCEnabled => _deviceProperties.ECCEnabled > 0;

        /// <summary>
        /// Gets the PCI bus ID.
        /// </summary>
        public int PciBusId => _deviceProperties.PciBusId;

        /// <summary>
        /// Gets the PCI device ID.
        /// </summary>
        public int PciDeviceId => _deviceProperties.PciDeviceId;

        /// <summary>
        /// Gets the PCI domain ID.
        /// </summary>
        public int PciDomainId => _deviceProperties.PciDomainId;

        /// <summary>
        /// Gets whether this is an RTX 2000 Ada Generation GPU.
        /// </summary>
        public bool IsRTX2000Ada => DetectRTX2000Ada();

        /// <summary>
        /// Gets the device architecture generation (e.g., "Ada Lovelace", "Ampere", etc.).
        /// </summary>
        public string ArchitectureGeneration => GetArchitectureGeneration();

        /// <summary>
        /// Gets the estimated memory bandwidth in GB/s.
        /// </summary>
        public double MemoryBandwidthGBps
            => 2.0 * _deviceProperties.MemoryClockRate * (_deviceProperties.MemoryBusWidth / 8) / 1.0e6;

        /// <summary>
        /// Gets device-specific capabilities.
        /// </summary>
        public IReadOnlyDictionary<string, object> Capabilities => _capabilities;

        /// <summary>
        /// Gets the device properties structure.
        /// </summary>
        public CudaDeviceProperties Properties => _deviceProperties;

        /// <summary>
        /// Gets device information for compatibility with test/benchmark code.
        /// </summary>
        public CudaDeviceInfo Info => new(this);

        /// <summary>
        /// Gets the memory manager for this device.
        /// </summary>
        /// <remarks>
        /// Memory manager must be set externally to avoid circular dependencies.
        /// </remarks>
        public Memory.CudaMemoryManager? Memory { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaDevice"/> class.
        /// </summary>
        /// <param name="deviceId">The CUDA device ID.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when deviceId is negative.</exception>
        /// <exception cref="InvalidOperationException">Thrown when device properties cannot be retrieved.</exception>
        public CudaDevice(int deviceId, ILogger? logger = null)
        {
            if (deviceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deviceId), "Device ID must be non-negative");
            }

            _logger = logger ?? NullLogger.Instance;
            _deviceId = deviceId;

            // Query device properties using P/Invoke
            var result = CudaRuntime.cudaGetDeviceProperties(ref _deviceProperties, _deviceId);
            if (result != CudaError.Success)
            {
                var error = CudaRuntime.GetErrorString(result);
                _logger.LogError("Failed to get CUDA device properties for device {DeviceId}: {Error}",
                    deviceId, error);
                throw new InvalidOperationException($"Failed to get CUDA device properties: {error}");
            }

            // Build capabilities dictionary
            _capabilities = BuildCapabilities();

            // Note: Memory manager must be set externally to avoid circular dependencies
            
            // Log managed memory support for debugging
            _logger.LogInformation("Device {DeviceId} ManagedMemory field value: {ManagedMemory}, SupportsManagedMemory: {Supports}",
                deviceId, _deviceProperties.ManagedMemory, SupportsManagedMemory);

            _logger.LogInformation("Initialized CUDA device {DeviceId}: {DeviceName} (CC {Major}.{Minor})",
                _deviceId, Name, ComputeCapabilityMajor, ComputeCapabilityMinor);
        }

        /// <summary>
        /// Creates a CudaDevice instance by detecting and validating the specified device.
        /// </summary>
        /// <param name="deviceId">The device ID to detect.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <returns>A new CudaDevice instance if successful, null otherwise.</returns>
        public static CudaDevice? Detect(int deviceId, ILogger<CudaDevice>? logger = null)
        {
            try
            {
                // First check if device exists
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
                if (result != CudaError.Success || deviceId >= deviceCount)
                {
                    logger?.LogWarning("CUDA device {DeviceId} not found (total devices: {DeviceCount})",
                        deviceId, deviceCount);
                    return null;
                }

                return new CudaDevice(deviceId, logger);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to detect CUDA device {DeviceId}", deviceId);
                return null;
            }
        }

        /// <summary>
        /// Detects all available CUDA devices on the system.
        /// </summary>
        /// <param name="logger">Optional logger instance.</param>
        /// <returns>An enumerable of detected CudaDevice instances.</returns>
        public static IEnumerable<CudaDevice> DetectAll(ILogger? logger = null)
        {
            var devices = new List<CudaDevice>();

            try
            {
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
                if (result != CudaError.Success)
                {
                    logger?.LogWarning("Failed to get CUDA device count: {Error}",
                        CudaRuntime.GetErrorString(result));
                    return devices;
                }

                logger?.LogInformation("Detecting {DeviceCount} CUDA devices", deviceCount);

                for (var i = 0; i < deviceCount; i++)
                {
                    try
                    {
                        var device = new CudaDevice(i, logger);
                        devices.Add(device);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Failed to initialize CUDA device {DeviceId}", i);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to detect CUDA devices");
            }

            return devices;
        }

        /// <summary>
        /// Gets the current memory usage information for this device.
        /// </summary>
        /// <returns>A tuple containing (free memory, total memory) in bytes.</returns>
        /// <exception cref="InvalidOperationException">Thrown when memory info cannot be retrieved.</exception>
        public (ulong freeMemory, ulong totalMemory) GetMemoryInfo()
        {
            ThrowIfDisposed();

            // Set device context first
            var setResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to set device {_deviceId}: {CudaRuntime.GetErrorString(setResult)}");
            }

            var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to get memory info: {CudaRuntime.GetErrorString(result)}");
            }

            return (free, total);
        }

        /// <summary>
        /// Checks if this device supports the specified compute capability.
        /// </summary>
        /// <param name="major">Major compute capability version.</param>
        /// <param name="minor">Minor compute capability version.</param>
        /// <returns>True if the device supports the specified compute capability or higher.</returns>
        public bool SupportsComputeCapability(int major, int minor)
        {
            return ComputeCapabilityMajor > major ||
                   (ComputeCapabilityMajor == major && ComputeCapabilityMinor >= minor);
        }

        /// <summary>
        /// Gets the estimated CUDA cores count for this device.
        /// Note: This is an approximation based on architecture and SM count.
        /// </summary>
        /// <returns>Estimated number of CUDA cores.</returns>
        public int GetEstimatedCudaCores()
        {
            // CUDA cores per SM varies by architecture
            var coresPerSM = ComputeCapabilityMajor switch
            {
                3 => 192, // Kepler
                5 => 128, // Maxwell
                6 => ComputeCapabilityMinor == 0 ? 64 : 128, // Pascal
                7 => ComputeCapabilityMinor == 0 ? 64 : 128, // Volta/Turing
                8 => ComputeCapabilityMinor == 6 ? 128 : 64, // Ampere
                9 => 128, // Ada Lovelace/Hopper
                _ => 128  // Default assumption
            };

            return StreamingMultiprocessorCount * coresPerSM;
        }

        /// <summary>
        /// Creates an AcceleratorInfo instance from this device.
        /// </summary>
        /// <returns>An AcceleratorInfo instance representing this device.</returns>
        public AcceleratorInfo ToAcceleratorInfo()
        {
            ThrowIfDisposed();

            var (freeMemory, totalMemory) = GetMemoryInfo();

            return new AcceleratorInfo(
                type: AcceleratorType.CUDA,
                name: Name,
                driverVersion: $"{ComputeCapabilityMajor}.{ComputeCapabilityMinor}",
                memorySize: (long)totalMemory,
                computeUnits: StreamingMultiprocessorCount,
                maxClockFrequency: ClockRate / 1000, // Convert kHz to MHz
                computeCapability: ComputeCapability,
                maxSharedMemoryPerBlock: (long)SharedMemoryPerBlock,
                isUnifiedMemory: SupportsManagedMemory
            )
            {
                Capabilities = new Dictionary<string, object>(_capabilities)
                {
                    ["AvailableMemory"] = freeMemory,
                    ["DeviceId"] = _deviceId,
                    ["IsRTX2000Ada"] = IsRTX2000Ada,
                    ["ArchitectureGeneration"] = ArchitectureGeneration,
                    ["EstimatedCudaCores"] = GetEstimatedCudaCores(),
                    ["MemoryBandwidthGBps"] = MemoryBandwidthGBps
                }
            };
        }

        private bool DetectRTX2000Ada()
        {
            // RTX 2000 Ada Generation GPUs have specific characteristics:
            // - Compute Capability 8.9 (Ada Lovelace architecture)
            // - Specific device name patterns
            var isAdaLovelace = ComputeCapabilityMajor == 8 && ComputeCapabilityMinor == 9;
            var nameContainsRTX2000 = Name.Contains("RTX 2000", StringComparison.OrdinalIgnoreCase) ||
                                      Name.Contains("RTX A2000", StringComparison.OrdinalIgnoreCase) ||
                                      Name.Contains("RTX 2000 Ada", StringComparison.OrdinalIgnoreCase);

            return isAdaLovelace && nameContainsRTX2000;
        }

        private string GetArchitectureGeneration()
        {
            return ComputeCapabilityMajor switch
            {
                3 => "Kepler",
                5 => "Maxwell",
                6 => "Pascal",
                7 => ComputeCapabilityMinor == 0 ? "Volta" : "Turing",
                8 => ComputeCapabilityMinor == 0 ? "Ampere" :
                     ComputeCapabilityMinor == 6 ? "Ampere" :
                     ComputeCapabilityMinor == 9 ? "Ada Lovelace" : "Ampere",
                9 => ComputeCapabilityMinor == 0 ? "Hopper" : "Ada Lovelace",
                _ => "Unknown"
            };
        }

        private Dictionary<string, object> BuildCapabilities()
        {
            var capabilities = new Dictionary<string, object>
            {
                // Core specifications
                ["ComputeCapabilityMajor"] = ComputeCapabilityMajor,
                ["ComputeCapabilityMinor"] = ComputeCapabilityMinor,
                ["StreamingMultiprocessors"] = StreamingMultiprocessorCount,
                ["WarpSize"] = WarpSize,
                ["MaxThreadsPerBlock"] = MaxThreadsPerBlock,
                ["MaxThreadsPerMultiprocessor"] = MaxThreadsPerMultiprocessor,

                // Memory specifications
                ["GlobalMemorySize"] = GlobalMemorySize,
                ["SharedMemoryPerBlock"] = SharedMemoryPerBlock,
                ["ConstantMemorySize"] = ConstantMemorySize,
                ["L2CacheSize"] = L2CacheSize,

                // Clock specifications
                ["ClockRate"] = ClockRate,
                ["MemoryClockRate"] = MemoryClockRate,
                ["MemoryBusWidth"] = MemoryBusWidth,
                ["MemoryBandwidthGBps"] = MemoryBandwidthGBps,

                // Feature support
                ["SupportsUnifiedAddressing"] = SupportsUnifiedAddressing,
                ["SupportsManagedMemory"] = SupportsManagedMemory,
                ["SupportsConcurrentKernels"] = SupportsConcurrentKernels,
                ["IsECCEnabled"] = IsECCEnabled,
                ["AsyncEngineCount"] = _deviceProperties.AsyncEngineCount,

                // PCI information
                ["PciBusId"] = PciBusId,
                ["PciDeviceId"] = PciDeviceId,
                ["PciDomainId"] = PciDomainId,

                // Architecture information
                ["ArchitectureGeneration"] = ArchitectureGeneration,
                ["IsRTX2000Ada"] = IsRTX2000Ada,
                ["EstimatedCudaCores"] = GetEstimatedCudaCores(),

                // Grid and block limits
                ["MaxGridSizeX"] = _deviceProperties.MaxGridSizeX,
                ["MaxGridSizeY"] = _deviceProperties.MaxGridSizeY,
                ["MaxGridSizeZ"] = _deviceProperties.MaxGridSizeZ,
                ["MaxBlockDimX"] = _deviceProperties.MaxThreadsDimX,
                ["MaxBlockDimY"] = _deviceProperties.MaxThreadsDimY,
                ["MaxBlockDimZ"] = _deviceProperties.MaxThreadsDimZ,

                // Advanced features (based on compute capability)
                ["SupportsTensorCores"] = ComputeCapabilityMajor >= 7,
                ["SupportsBFloat16"] = ComputeCapabilityMajor >= 8,
                ["SupportsCooperativeGroups"] = ComputeCapabilityMajor >= 6,
                ["SupportsDynamicParallelism"] = ComputeCapabilityMajor >= 3 && ComputeCapabilityMinor >= 5,
                ["SupportsUnifiedMemory"] = SupportsManagedMemory
            };

            return capabilities;
        }

        /// <summary>
        /// Creates a CUDA context for this device.
        /// </summary>
        public CudaContext CreateContext() => new(_deviceId);

        /// <summary>
        /// Synchronizes all operations on this device asynchronously.
        /// </summary>
        public static async Task SynchronizeAsync(CancellationToken cancellationToken = default) => await Task.Run(() => CudaRuntime.cudaDeviceSynchronize(), cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Copies data from host to device asynchronously.
        /// </summary>
        public static async Task CopyToDeviceAsync(IntPtr hostPtr, IUnifiedMemoryBuffer deviceBuffer, ulong sizeInBytes, CancellationToken cancellationToken = default)
        {
            if (deviceBuffer is CudaMemoryBuffer cudaBuffer)
            {
                await Task.Run(() =>
                {
                    var result = CudaRuntime.cudaMemcpy(cudaBuffer.DevicePointer, hostPtr, (nuint)sizeInBytes, CudaMemcpyKind.HostToDevice);
                    if (result != CudaError.Success)
                    {
                        throw new InvalidOperationException($"CUDA memory copy failed: {CudaRuntime.GetErrorString(result)}");
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException("Device buffer must be a CUDA memory buffer", nameof(deviceBuffer));
            }
        }

        /// <summary>
        /// Allocates memory on this device asynchronously.
        /// </summary>
        public async Task<IUnifiedMemoryBuffer> AllocateAsync(ulong sizeInBytes)
        {
            if (Memory == null)
                throw new InvalidOperationException("Memory manager has not been initialized for this device");
            return await Memory.AllocateAsync((long)sizeInBytes).ConfigureAwait(false);
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        /// <summary>
        /// Releases all resources used by the CudaDevice.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _logger.LogDebug("Disposed CUDA device {DeviceId}", _deviceId);
            }
        }
    }
}
