// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Represents a CUDA-capable GPU device with enhanced device detection capabilities.
    /// Provides detailed hardware information and capabilities for RTX 2000 Ada Generation and other CUDA devices.
    /// Requires CUDA 13.0+ and compute capability 7.5 or higher (Turing architecture minimum).
    /// </summary>
    public sealed class CudaDevice : IDisposable
    {
        // Dynamic minimum requirements based on CUDA version detection
        private static readonly Lazy<(int major, int minor, string arch)> _minimumRequirements = new(() =>

        {
            // Use CudaCapabilityManager to get dynamic requirements
            var targetCapability = DotCompute.Backends.CUDA.Configuration.CudaCapabilityManager.GetTargetComputeCapability();
            var arch = targetCapability.major switch
            {
                >= 9 => "Hopper",
                8 => targetCapability.minor >= 9 ? "Ada Lovelace" : "Ampere",
                7 => targetCapability.minor >= 5 ? "Turing" : "Volta",
                6 => "Pascal",
                5 => "Maxwell",
                _ => "Legacy"
            };
            return (targetCapability.major, targetCapability.minor, arch);
        });

        private static int MinimumComputeCapabilityMajor => _minimumRequirements.Value.major;
        private static int MinimumComputeCapabilityMinor => _minimumRequirements.Value.minor;
        private static string MinimumArchitecture => _minimumRequirements.Value.arch;


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
        /// Uses the corrected detection logic that handles known device issues.
        /// </summary>
        public bool SupportsManagedMemory => _deviceProperties.ManagedMemorySupported;

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
        /// <exception cref="InvalidOperationException">Thrown when device properties cannot be retrieved or device doesn't meet CUDA 13.0 requirements.</exception>
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

            // Verify CUDA 13.0 minimum requirements
            if (!IsCuda13Compatible())
            {
                var architecture = GetArchitectureGeneration();
                var errorMsg = $"Device {Name} (CC {ComputeCapabilityMajor}.{ComputeCapabilityMinor}, {architecture}) " +
                              $"is not compatible with CUDA 13.0. Minimum requirement: CC {MinimumComputeCapabilityMajor}.{MinimumComputeCapabilityMinor} ({MinimumArchitecture}). " +
                              "Supported architectures: Turing (sm_75), Ampere (sm_80/86), Ada Lovelace (sm_89), Hopper (sm_90).";


                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            // Build capabilities dictionary
            _capabilities = BuildCapabilities();

            // Note: Memory manager must be set externally to avoid circular dependencies

            // Log managed memory support for debugging

            _logger.LogInfoMessage($"Device {deviceId} ManagedMemory field value: {_deviceProperties.ManagedMemory}, SupportsManagedMemory: {SupportsManagedMemory}");

            _logger.LogInfoMessage($"Initialized CUDA 13.0-compatible device {_deviceId}: {Name} (CC {ComputeCapabilityMajor}.{ComputeCapabilityMinor}, {GetArchitectureGeneration()})");
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
        /// Checks if this device is compatible with CUDA 13.0 requirements.
        /// </summary>
        /// <returns>True if the device meets CUDA 13.0 minimum requirements.</returns>
        public bool IsCuda13Compatible()
        {
            // CUDA 13.0 drops support for Maxwell (sm_5x), Pascal (sm_6x), and Volta (sm_70, sm_72)
            // Minimum supported: Turing (sm_75) and newer
            return SupportsComputeCapability(MinimumComputeCapabilityMajor, MinimumComputeCapabilityMinor);
        }

        /// <summary>
        /// Gets the estimated CUDA cores count for this device.
        /// Note: This is an approximation based on architecture and SM count.
        /// </summary>
        /// <returns>Estimated number of CUDA cores.</returns>
        public int GetEstimatedCudaCores()
        {
            // CUDA cores per SM varies by architecture
            // Only CUDA 13.0+ supported architectures included
            var coresPerSM = ComputeCapabilityMajor switch
            {
                7 when ComputeCapabilityMinor >= 5 => 64,  // Turing (sm_75)
                8 when ComputeCapabilityMinor == 0 => 64,  // Ampere GA100 (sm_80)
                8 when ComputeCapabilityMinor == 6 => 128, // Ampere GA10x (sm_86)
                8 when ComputeCapabilityMinor == 7 => 128, // Ampere GA10x (sm_87)
                8 when ComputeCapabilityMinor == 9 => 128, // Ada Lovelace (sm_89)
                9 when ComputeCapabilityMinor == 0 => 128, // Hopper (sm_90)
                _ => 128  // Default for future architectures
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
                // Only CUDA 13.0+ supported architectures
                7 when ComputeCapabilityMinor == 5 => "Turing",
                8 when ComputeCapabilityMinor == 0 => "Ampere (GA100)",
                8 when ComputeCapabilityMinor == 6 => "Ampere (GA10x)",
                8 when ComputeCapabilityMinor == 7 => "Ampere (GA10x)",
                8 when ComputeCapabilityMinor == 9 => "Ada Lovelace",
                9 when ComputeCapabilityMinor == 0 => "Hopper",
                _ => $"Unknown (CC {ComputeCapabilityMajor}.{ComputeCapabilityMinor})"
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
                ["SupportsUnifiedMemory"] = SupportsManagedMemory,

                // CUDA 13.0 specific features

                ["IsCuda13Compatible"] = IsCuda13Compatible(),
                ["SupportsSharedMemoryRegisterSpilling"] = ComputeCapabilityMajor >= 7 && ComputeCapabilityMinor >= 5,
                ["SupportsTileBasedProgramming"] = ComputeCapabilityMajor >= 8,
                ["SupportsAsyncCopyOperations"] = ComputeCapabilityMajor >= 8,
                ["SupportsL2CacheResidencyControl"] = ComputeCapabilityMajor >= 8,
                ["SupportsGraphOptimizationV2"] = ComputeCapabilityMajor >= 8,
                ["SupportsInt4TensorCores"] = ComputeCapabilityMajor >= 8 && ComputeCapabilityMinor >= 9,
                ["SupportsFP8TensorCores"] = ComputeCapabilityMajor >= 9
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
            if (deviceBuffer is Memory.CudaMemoryBuffer cudaBuffer)
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
            {

                throw new InvalidOperationException("Memory manager has not been initialized for this device");
            }


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
                _logger.LogDebugMessage("Disposed CUDA device {_deviceId}");
            }
        }
    }
}
