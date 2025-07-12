using System;
using System.Threading;
using System.Threading.Tasks;

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
    
    /// <summary>Gets or sets the compute capability or version.</summary>
    public Version? ComputeCapability { get; init; }
    
    /// <summary>Gets or sets the total device memory in bytes.</summary>
    public long TotalMemory { get; init; }
    
    /// <summary>Gets or sets the number of compute units.</summary>
    public int ComputeUnits { get; init; }
    
    /// <summary>Gets or sets the maximum clock frequency in MHz.</summary>
    public int MaxClockFrequency { get; init; }
    
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
}

/// <summary>
/// Represents a compiled kernel ready for execution.
/// This is a value type for AOT compatibility and performance.
/// </summary>
public readonly struct CompiledKernel : IEquatable<CompiledKernel>
{
    /// <summary>
    /// Gets the unique identifier for this kernel.
    /// </summary>
    public Guid Id { get; }
    
    /// <summary>
    /// Gets the native handle to the compiled kernel code.
    /// </summary>
    public IntPtr NativeHandle { get; }
    
    /// <summary>
    /// Gets the kernel's required shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; }
    
    /// <summary>
    /// Gets the kernel's thread configuration.
    /// </summary>
    public KernelConfiguration Configuration { get; }
    
    public CompiledKernel(Guid id, IntPtr nativeHandle, int sharedMemorySize, KernelConfiguration configuration)
    {
        Id = id;
        NativeHandle = nativeHandle;
        SharedMemorySize = sharedMemorySize;
        Configuration = configuration;
    }
    
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
    public override readonly bool Equals(object? obj)
    {
        return obj is CompiledKernel other && Equals(other);
    }
    
    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Id, NativeHandle, SharedMemorySize, Configuration);
    }
    
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
public readonly struct KernelConfiguration : IEquatable<KernelConfiguration>
{
    /// <summary>
    /// Gets the number of thread blocks in each dimension.
    /// </summary>
    public Dim3 GridDimensions { get; }
    
    /// <summary>
    /// Gets the number of threads per block in each dimension.
    /// </summary>
    public Dim3 BlockDimensions { get; }
    
    public KernelConfiguration(Dim3 gridDimensions, Dim3 blockDimensions)
    {
        GridDimensions = gridDimensions;
        BlockDimensions = blockDimensions;
    }
    
    /// <summary>
    /// Determines whether this instance is equal to another KernelConfiguration.
    /// </summary>
    public readonly bool Equals(KernelConfiguration other)
    {
        return GridDimensions.Equals(other.GridDimensions) && BlockDimensions.Equals(other.BlockDimensions);
    }
    
    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj)
    {
        return obj is KernelConfiguration other && Equals(other);
    }
    
    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(GridDimensions, BlockDimensions);
    }
    
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
public readonly struct Dim3 : IEquatable<Dim3>
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    
    public Dim3(int x, int y = 1, int z = 1)
    {
        X = x;
        Y = y;
        Z = z;
    }
    
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
    public readonly bool Equals(Dim3 other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }
    
    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj)
    {
        return obj is Dim3 other && Equals(other);
    }
    
    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }
    
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
public readonly struct KernelArguments : IEquatable<KernelArguments>
{
    private readonly object[] _args;
    
    public KernelArguments(params object[] args)
    {
        _args = args ?? Array.Empty<object>();
    }
    
    /// <summary>
    /// Gets the arguments as a read-only span.
    /// </summary>
    public readonly ReadOnlySpan<object> Arguments => _args;
    
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
        
        for (int i = 0; i < _args.Length; i++)
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
    public override readonly bool Equals(object? obj)
    {
        return obj is KernelArguments other && Equals(other);
    }
    
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