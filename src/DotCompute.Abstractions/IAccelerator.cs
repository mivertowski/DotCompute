// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a compute accelerator device.
/// </summary>
public interface IAccelerator : IAsyncDisposable
{
    /// <summary>Gets device information.</summary>
    public AcceleratorInfo Info { get; }

    /// <summary>Gets memory manager for this accelerator.</summary>
    public IMemoryManager Memory { get; }

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

/// <summary>
/// Represents kernel compilation options.
/// </summary>
public class CompilationOptions
{
    /// <summary>Gets or sets optimization level.</summary>
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.Default;

    /// <summary>Gets or sets whether to enable debug information.</summary>
    public bool EnableDebugInfo { get; init; }

    /// <summary>Gets or sets additional compiler flags.</summary>
    public string[]? AdditionalFlags { get; init; }

    /// <summary>Gets or sets preprocessor defines.</summary>
    public Dictionary<string, string>? Defines { get; init; }

    /// <summary>Gets or sets whether to enable fast math optimizations.</summary>
    public bool FastMath { get; init; }

    /// <summary>Gets or sets whether to enable loop unrolling.</summary>
    public bool UnrollLoops { get; init; }
}

/// <summary>
/// Optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>No optimization.</summary>
    None,

    /// <summary>Default optimization level.</summary>
    Default,

    /// <summary>Maximum optimization.</summary>
    Maximum
}

/// <summary>
/// Represents a kernel definition.
/// </summary>
public class KernelDefinition
{
    /// <summary>Gets or sets the kernel name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the kernel source code or bytecode.</summary>
    public required byte[] Code { get; init; }

    /// <summary>Gets or sets the kernel entry point.</summary>
    public string? EntryPoint { get; init; }

    /// <summary>Gets or sets kernel metadata.</summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Initializes a new instance of the KernelDefinition class.
    /// </summary>
    /// <param name="name">The kernel name.</param>
    /// <param name="source">The kernel source.</param>
    /// <param name="options">The compilation options.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public KernelDefinition(string name, IKernelSource source, CompilationOptions options)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Name = name;
        Code = System.Text.Encoding.UTF8.GetBytes(source.Code);
        EntryPoint = source.EntryPoint;
        Metadata = new Dictionary<string, object>
        {
            ["Language"] = source.Language.ToString(),
            ["Dependencies"] = source.Dependencies,
            ["CompilationOptions"] = options
        };
    }
}

/// <summary>
/// Represents a compiled kernel ready for execution.
/// This is a value type for AOT compatibility and performance.
/// </summary>
public readonly struct CompiledKernel(Guid id, IntPtr nativeHandle, int sharedMemorySize, KernelConfiguration configuration) : IEquatable<CompiledKernel>
{
    /// <summary>
    /// Gets the unique identifier for this kernel.
    /// </summary>
    public Guid Id { get; } = id;

    /// <summary>
    /// Gets the native handle to the compiled kernel code.
    /// </summary>
    public IntPtr NativeHandle { get; } = nativeHandle;

    /// <summary>
    /// Gets the kernel's required shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; } = sharedMemorySize;

    /// <summary>
    /// Gets the kernel's thread configuration.
    /// </summary>
    public KernelConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Determines whether this instance is equal to another CompiledKernel.
    /// </summary>
    public readonly bool Equals(CompiledKernel other)
    {
        return Id.Equals(other.Id) && NativeHandle.Equals(other.NativeHandle) &&
               SharedMemorySize == other.SharedMemorySize && Configuration.Equals(other.Configuration);
    }

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj) => obj is CompiledKernel other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode() => HashCode.Combine(Id, NativeHandle, SharedMemorySize, Configuration);

    /// <summary>
    /// Determines whether two CompiledKernel instances are equal.
    /// </summary>
    public static bool operator ==(CompiledKernel left, CompiledKernel right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two CompiledKernel instances are not equal.
    /// </summary>
    public static bool operator !=(CompiledKernel left, CompiledKernel right)
    {
        return !(left == right);
    }
}

/// <summary>
/// Represents kernel execution configuration.
/// </summary>
public readonly struct KernelConfiguration(Dim3 gridDimensions, Dim3 blockDimensions) : IEquatable<KernelConfiguration>
{
    /// <summary>
    /// Gets the number of thread blocks in each dimension.
    /// </summary>
    public Dim3 GridDimensions { get; } = gridDimensions;

    /// <summary>
    /// Gets the number of threads per block in each dimension.
    /// </summary>
    public Dim3 BlockDimensions { get; } = blockDimensions;

    /// <summary>
    /// Determines whether this instance is equal to another KernelConfiguration.
    /// </summary>
    public readonly bool Equals(KernelConfiguration other) => GridDimensions.Equals(other.GridDimensions) && BlockDimensions.Equals(other.BlockDimensions);

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj) => obj is KernelConfiguration other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode() => HashCode.Combine(GridDimensions, BlockDimensions);

    /// <summary>
    /// Determines whether two KernelConfiguration instances are equal.
    /// </summary>
    public static bool operator ==(KernelConfiguration left, KernelConfiguration right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two KernelConfiguration instances are not equal.
    /// </summary>
    public static bool operator !=(KernelConfiguration left, KernelConfiguration right)
    {
        return !(left == right);
    }
}

/// <summary>
/// Represents 3D dimensions for kernel configuration.
/// </summary>
public readonly struct Dim3(int x, int y = 1, int z = 1) : IEquatable<Dim3>
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;

    public static implicit operator Dim3(int value) => new(value);
    public static implicit operator Dim3((int x, int y) value) => new(value.x, value.y);
    public static implicit operator Dim3((int x, int y, int z) value) => new(value.x, value.y, value.z);

    /// <summary>
    /// Creates a Dim3 from an integer value.
    /// </summary>
    public static Dim3 FromInt32(int value) => new(value);

    /// <summary>
    /// Creates a Dim3 from a value tuple with x and y components.
    /// </summary>
    public static Dim3 FromValueTuple((int x, int y) value) => new(value.x, value.y);

    /// <summary>
    /// Creates a Dim3 from a value tuple with x, y, and z components.
    /// </summary>
    public static Dim3 FromValueTuple((int x, int y, int z) value) => new(value.x, value.y, value.z);

    /// <summary>
    /// Determines whether this instance is equal to another Dim3.
    /// </summary>
    public readonly bool Equals(Dim3 other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj) => obj is Dim3 other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Determines whether two Dim3 instances are equal.
    /// </summary>
    public static bool operator ==(Dim3 left, Dim3 right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two Dim3 instances are not equal.
    /// </summary>
    public static bool operator !=(Dim3 left, Dim3 right)
    {
        return !(left == right);
    }
}

/// <summary>
/// Represents arguments to pass to a kernel.
/// </summary>
public struct KernelArguments(params object[] args) : IEquatable<KernelArguments>
{
    private readonly object[] _args = args ?? [];

    /// <summary>
    /// Gets the arguments as a read-only span.
    /// </summary>
    public readonly ReadOnlySpan<object> Arguments => _args;

    /// <summary>
    /// Sets an argument at the specified index.
    /// </summary>
    /// <param name="index">The index of the argument.</param>
    /// <param name="value">The value to set.</param>
    public void Set(int index, object value)
    {
        if (_args == null)
        {
            throw new InvalidOperationException("KernelArguments not initialized with proper capacity");
        }

        if (index < 0 || index >= _args.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _args[index] = value;
    }

    /// <summary>
    /// Gets an argument at the specified index.
    /// </summary>
    /// <param name="index">The index of the argument.</param>
    /// <returns>The argument at the specified index.</returns>
    public object Get(int index)
    {
        if (_args == null)
        {
            throw new InvalidOperationException("KernelArguments not initialized");
        }

        if (index < 0 || index >= _args.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _args[index];
    }

    /// <summary>
    /// Creates a new KernelArguments with the specified capacity.
    /// </summary>
    /// <param name="capacity">The number of arguments to allocate space for.</param>
    /// <returns>A new KernelArguments instance.</returns>
    public static KernelArguments Create(int capacity) => new(new object[capacity]);

    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int Length => _args?.Length ?? 0;

    /// <summary>
    /// Determines whether this instance is equal to another KernelArguments.
    /// </summary>
    public readonly bool Equals(KernelArguments other)
    {
        if (_args == other._args)
        {
            return true;
        }
        if (_args == null || other._args == null)
        {
            return false;
        }
        if (_args.Length != other._args.Length)
        {
            return false;
        }

        for (var i = 0; i < _args.Length; i++)
        {
            if (!Equals(_args[i], other._args[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj) => obj is KernelArguments other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode()
    {
        if (_args == null)
        {
            return 0;
        }
        var hashCode = new HashCode();
        foreach (var arg in _args)
        {
            hashCode.Add(arg);
        }
        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Determines whether two KernelArguments instances are equal.
    /// </summary>
    public static bool operator ==(KernelArguments left, KernelArguments right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two KernelArguments instances are not equal.
    /// </summary>
    public static bool operator !=(KernelArguments left, KernelArguments right)
    {
        return !(left == right);
    }
}

/// <summary>
/// Defines accelerator features that may or may not be supported.
/// </summary>
[Flags]
public enum AcceleratorFeature
{
    None = 0,
    Float16 = 1 << 0,
    DoublePrecision = 1 << 1,
    LongInteger = 1 << 2,
    TensorCores = 1 << 3,
    UnifiedMemory = 1 << 4,
    CooperativeGroups = 1 << 5,
    DynamicParallelism = 1 << 6,
    AtomicOperations = 1 << 7,
    Bfloat16 = 1 << 8,
    SignedByte = 1 << 9,
    MixedPrecision = 1 << 10
}
