// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Represents a compute accelerator device.
    /// </summary>
    public interface IAccelerator : IAsyncDisposable
    {
        /// <summary>Gets device information.</summary>
        public AcceleratorInfo Info { get; }

        /// <summary>Gets the accelerator type.</summary>
        public AcceleratorType Type { get; }

        /// <summary>Gets memory manager for this accelerator.</summary>
        public IUnifiedMemoryManager Memory { get; }

        /// <summary>Gets the accelerator context.</summary>
        public AcceleratorContext Context { get; }

        /// <summary>Compiles a kernel for execution.</summary>
        public ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Synchronizes all pending operations.</summary>
        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents information about an accelerator device.
    /// </summary>
    public class AcceleratorInfo
    {
        /// <summary>Gets or sets the unique identifier for this device.</summary>
        public required string Id { get; init; }

        /// <summary>Gets or sets the friendly name of this device.</summary>
        public required string Name { get; init; }

        /// <summary>Gets or sets the device type (e.g., "CPU", "GPU", "TPU").</summary>
        public required string DeviceType { get; init; }

        /// <summary>Gets or sets the vendor name.</summary>
        public required string Vendor { get; init; }

        /// <summary>Gets or sets the device type for legacy compatibility.</summary>
        public string Type => DeviceType;

        /// <summary>Gets or sets the driver version.</summary>
        public string? DriverVersion { get; init; }

        /// <summary>
        /// Legacy constructor for backward compatibility with tests.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo(AcceleratorType type, string name, string driverVersion, long memorySize)
        {
            Id = $"{type}_{name}";
            Name = name;
            DeviceType = type.ToString();
            Vendor = "Unknown";
            DriverVersion = driverVersion;
            TotalMemory = memorySize;
            AvailableMemory = memorySize;
            MaxSharedMemoryPerBlock = memorySize / 4;
            MaxMemoryAllocationSize = memorySize;
            LocalMemorySize = memorySize / 8;
            IsUnifiedMemory = type == AcceleratorType.CPU;
            MaxThreadsPerBlock = 1024; // Default value for legacy compatibility
        }

        /// <summary>
        /// Constructor for tests with full parameters.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo(string name, string vendor, string driverVersion, AcceleratorType type,
                              double computeCapability, int maxThreadsPerBlock, int maxSharedMemory,
                              long totalMemory, long availableMemory)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrEmpty(vendor))
            {
                throw new ArgumentException("Vendor cannot be null or empty", nameof(vendor));
            }

            if (string.IsNullOrEmpty(driverVersion))
            {
                throw new ArgumentException("DriverVersion cannot be null or empty", nameof(driverVersion));
            }

            if (computeCapability <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(computeCapability), "ComputeCapability must be positive");
            }

            if (maxThreadsPerBlock <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxThreadsPerBlock), "MaxThreadsPerBlock must be positive");
            }

            if (maxSharedMemory < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSharedMemory), "MaxSharedMemory cannot be negative");
            }

            if (totalMemory <= 0 || availableMemory <= 0 || availableMemory > totalMemory)
            {
                throw new ArgumentException("Invalid memory sizes");
            }

            Id = $"{type}_{name}";
            Name = name;
            DeviceType = type.ToString();
            Vendor = vendor;
            DriverVersion = driverVersion;
            TotalMemory = totalMemory;
            AvailableMemory = availableMemory;
            MaxSharedMemoryPerBlock = maxSharedMemory;
            MaxMemoryAllocationSize = totalMemory;
            LocalMemorySize = totalMemory / 8;
            IsUnifiedMemory = type == AcceleratorType.CPU;
            MaxThreadsPerBlock = maxThreadsPerBlock;
            ComputeCapability = new Version((int)computeCapability, (int)((computeCapability % 1) * 10));
        }

        /// <summary>
        /// Extended constructor for tests.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo(AcceleratorType type, string name, string driverVersion, long memorySize,
                              int computeUnits, int maxClockFrequency, Version? computeCapability,
                              long maxSharedMemoryPerBlock, bool isUnifiedMemory)
        {
            Id = $"{type}_{name}";
            Name = name;
            DeviceType = type.ToString();
            Vendor = "Unknown";
            DriverVersion = driverVersion;
            TotalMemory = memorySize;
            AvailableMemory = memorySize;
            MaxSharedMemoryPerBlock = maxSharedMemoryPerBlock;
            MaxMemoryAllocationSize = memorySize;
            LocalMemorySize = memorySize / 8;
            IsUnifiedMemory = isUnifiedMemory;
            ComputeUnits = computeUnits;
            MaxClockFrequency = maxClockFrequency;
            ComputeCapability = computeCapability;
        }

        /// <summary>Gets or sets the compute capability or version.</summary>
        public Version? ComputeCapability { get; init; }

        /// <summary>Gets or sets the total device memory in bytes.</summary>
        public long TotalMemory { get; init; }

        /// <summary>Gets or sets the total device memory in bytes (alias for TotalMemory).</summary>
        public long MemorySize => TotalMemory;

        /// <summary>Gets or sets the available device memory in bytes.</summary>
        public long AvailableMemory { get; init; }

        /// <summary>Gets or sets the maximum shared memory per block in bytes.</summary>
        public long MaxSharedMemoryPerBlock { get; init; }

        /// <summary>Gets or sets the maximum memory allocation size in bytes.</summary>
        public long MaxMemoryAllocationSize { get; init; }

        /// <summary>Gets or sets the local memory size in bytes.</summary>
        public long LocalMemorySize { get; init; }

        /// <summary>Gets or sets whether the device uses unified memory.</summary>
        public bool IsUnifiedMemory { get; init; }

        /// <summary>Gets or sets the number of compute units.</summary>
        public int ComputeUnits { get; init; }

        /// <summary>Gets or sets the maximum clock frequency in MHz.</summary>
        public int MaxClockFrequency { get; init; }

        /// <summary>Gets or sets the maximum threads per block.</summary>
        public int MaxThreadsPerBlock { get; init; }

        /// <summary>Gets or sets device-specific capabilities.</summary>
        public Dictionary<string, object>? Capabilities { get; init; }

        /// <summary>
        /// Parameterless constructor for test compatibility.
        /// Creates a default AcceleratorInfo for testing.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public AcceleratorInfo()
        {
            Id = "test_device";
            Name = "Test Device";
            DeviceType = "Test";
            Vendor = "Test Vendor";
            DriverVersion = "1.0.0";
            TotalMemory = 1024 * 1024 * 1024; // 1 GB
            AvailableMemory = 1024 * 1024 * 1024;
            MaxSharedMemoryPerBlock = 48 * 1024; // 48 KB
            MaxMemoryAllocationSize = 1024 * 1024 * 1024;
            LocalMemorySize = 64 * 1024; // 64 KB
            IsUnifiedMemory = false;
            ComputeUnits = 8;
            MaxClockFrequency = 1500;
            MaxThreadsPerBlock = 1024;
            ComputeCapability = new Version(7, 5);
        }

        /// <summary>
        /// Gets or sets the maximum work group size (alias for MaxThreadsPerBlock).
        /// </summary>
        public int MaxWorkGroupSize
        {
            get => MaxThreadsPerBlock;
            init => MaxThreadsPerBlock = value;
        }
    }

    /// <summary>
    /// Represents a compiled kernel ready for execution.
    /// </summary>
    public interface ICompiledKernel : IAsyncDisposable
    {
        /// <summary>Gets the kernel name.</summary>
        public string Name { get; }

        /// <summary>Executes the kernel with given arguments.</summary>
        public ValueTask ExecuteAsync(
            KernelArguments arguments,
            CancellationToken cancellationToken = default);
    }
}
