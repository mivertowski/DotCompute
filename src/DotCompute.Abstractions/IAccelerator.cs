using System;
using System.Runtime.CompilerServices;

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a compute accelerator device that can execute tensor operations.
/// This interface is designed to be AOT-compatible with no virtual dispatch overhead.
/// </summary>
public interface IAccelerator : IDisposable
{
    /// <summary>
    /// Gets information about this accelerator device.
    /// </summary>
    AcceleratorInfo Info { get; }
    
    /// <summary>
    /// Gets the memory manager for this accelerator.
    /// </summary>
    IMemoryManager MemoryManager { get; }
    
    /// <summary>
    /// Gets the current device index for multi-device systems.
    /// </summary>
    int DeviceIndex { get; }
    
    /// <summary>
    /// Compiles and caches a kernel for execution on this accelerator.
    /// </summary>
    /// <typeparam name="TKernel">The kernel type to compile.</typeparam>
    /// <param name="definition">The kernel definition containing compilation parameters.</param>
    /// <returns>A compiled kernel handle.</returns>
    CompiledKernel CompileKernel<TKernel>(KernelDefinition definition) where TKernel : IKernel;
    
    /// <summary>
    /// Executes a compiled kernel on this accelerator.
    /// </summary>
    /// <param name="kernel">The compiled kernel to execute.</param>
    /// <param name="args">Arguments to pass to the kernel.</param>
    void ExecuteKernel(CompiledKernel kernel, KernelArguments args);
    
    /// <summary>
    /// Synchronizes all pending operations on this accelerator.
    /// </summary>
    void Synchronize();
    
    /// <summary>
    /// Checks if this accelerator supports a specific feature.
    /// </summary>
    /// <param name="feature">The feature to check for.</param>
    /// <returns>True if the feature is supported; otherwise, false.</returns>
    bool SupportsFeature(AcceleratorFeature feature);
}

/// <summary>
/// Represents a compiled kernel ready for execution.
/// This is a value type for AOT compatibility and performance.
/// </summary>
public readonly struct CompiledKernel
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
}

/// <summary>
/// Represents kernel execution configuration.
/// </summary>
public readonly struct KernelConfiguration
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
}

/// <summary>
/// Represents 3D dimensions for kernel configuration.
/// </summary>
public readonly struct Dim3
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
}

/// <summary>
/// Represents arguments to pass to a kernel.
/// </summary>
public readonly struct KernelArguments
{
    private readonly object[] _args;
    
    public KernelArguments(params object[] args)
    {
        _args = args ?? Array.Empty<object>();
    }
    
    public ReadOnlySpan<object> GetArguments() => _args;
}

/// <summary>
/// Defines accelerator features that may or may not be supported.
/// </summary>
[Flags]
public enum AcceleratorFeature
{
    None = 0,
    Float16 = 1 << 0,
    Float64 = 1 << 1,
    Int64 = 1 << 2,
    TensorCores = 1 << 3,
    UnifiedMemory = 1 << 4,
    CooperativeGroups = 1 << 5,
    DynamicParallelism = 1 << 6,
    AtomicOperations = 1 << 7,
    Bfloat16 = 1 << 8,
    Int8 = 1 << 9,
    MixedPrecision = 1 << 10
}