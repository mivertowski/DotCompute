// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using CompilationOptions = DotCompute.Abstractions.CompilationOptions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using KernelDefinition = DotCompute.Abstractions.KernelDefinition;

namespace DotCompute.Core;

/// <summary>
/// Represents a compute device capable of executing kernels.
/// </summary>
public interface IComputeDevice : IAsyncDisposable
{
    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the device name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the device type.
    /// </summary>
    public ComputeDeviceType Type { get; }

    /// <summary>
    /// Gets device capabilities.
    /// </summary>
    public IDeviceCapabilities Capabilities { get; }

    /// <summary>
    /// Gets current device status.
    /// </summary>
    public DeviceStatus Status { get; }

    /// <summary>
    /// Gets device memory information.
    /// </summary>
    public IDeviceMemoryInfo MemoryInfo { get; }

    /// <summary>
    /// Compiles a kernel for this device.
    /// </summary>
    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition kernel,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Allocates memory on the device.
    /// </summary>
    public ValueTask<IDeviceMemory> AllocateMemoryAsync(
        long sizeInBytes,
        Memory.MemoryAccess access,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a command queue for kernel execution.
    /// </summary>
    public ICommandQueue CreateCommandQueue(CommandQueueOptions? options = null);

    /// <summary>
    /// Synchronizes all pending operations on the device.
    /// </summary>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets device metrics.
    /// </summary>
    public IDeviceMetrics GetMetrics();
}

/// <summary>
/// Types of compute devices.
/// </summary>
public enum ComputeDeviceType
{
    /// <summary>
    /// CPU device.
    /// </summary>
    CPU,

    /// <summary>
    /// GPU device.
    /// </summary>
    GPU,

    /// <summary>
    /// FPGA device.
    /// </summary>
    FPGA,

    /// <summary>
    /// Custom accelerator.
    /// </summary>
    Accelerator,

    /// <summary>
    /// Virtual device.
    /// </summary>
    Virtual
}

/// <summary>
/// Device status.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is available.
    /// </summary>
    Available,

    /// <summary>
    /// Device is busy.
    /// </summary>
    Busy,

    /// <summary>
    /// Device is offline.
    /// </summary>
    Offline,

    /// <summary>
    /// Device has an error.
    /// </summary>
    Error,

    /// <summary>
    /// Device is initializing.
    /// </summary>
    Initializing
}

/// <summary>
/// Device capabilities information.
/// </summary>
public interface IDeviceCapabilities
{
    /// <summary>
    /// Gets the compute capability version.
    /// </summary>
    public Version ComputeCapability { get; }

    /// <summary>
    /// Gets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; }

    /// <summary>
    /// Gets the maximum work item dimensions.
    /// </summary>
    public int MaxWorkItemDimensions { get; }

    /// <summary>
    /// Gets the maximum work item sizes per dimension.
    /// </summary>
    public long[] MaxWorkItemSizes { get; }

    /// <summary>
    /// Gets the number of compute units.
    /// </summary>
    public int ComputeUnits { get; }

    /// <summary>
    /// Gets the clock frequency in MHz.
    /// </summary>
    public int ClockFrequency { get; }

    /// <summary>
    /// Gets supported features.
    /// </summary>
    public DeviceFeatures SupportedFeatures { get; }

    /// <summary>
    /// Gets supported data types.
    /// </summary>
    public DataTypeSupport SupportedDataTypes { get; }

    /// <summary>
    /// Checks if a specific feature is supported.
    /// </summary>
    public bool IsFeatureSupported(DeviceFeature feature);
}

/// <summary>
/// Device features flags.
/// </summary>
[Flags]
public enum DeviceFeatures
{
    /// <summary>
    /// No special features.
    /// </summary>
    None = 0,

    /// <summary>
    /// Supports double precision.
    /// </summary>
    DoublePrecision = 1 << 0,

    /// <summary>
    /// Supports half precision.
    /// </summary>
    HalfPrecision = 1 << 1,

    /// <summary>
    /// Supports atomic operations.
    /// </summary>
    Atomics = 1 << 2,

    /// <summary>
    /// Supports local memory.
    /// </summary>
    LocalMemory = 1 << 3,

    /// <summary>
    /// Supports images.
    /// </summary>
    Images = 1 << 4,

    /// <summary>
    /// Supports 3D images.
    /// </summary>
    Images3D = 1 << 5,

    /// <summary>
    /// Supports unified memory.
    /// </summary>
    UnifiedMemory = 1 << 6,

    /// <summary>
    /// Supports dynamic parallelism.
    /// </summary>
    DynamicParallelism = 1 << 7,

    /// <summary>
    /// Supports tensor cores.
    /// </summary>
    TensorCores = 1 << 8
}

/// <summary>
/// Individual device feature.
/// </summary>
public enum DeviceFeature
{
    /// <summary>
    /// Double precision support.
    /// </summary>
    DoublePrecision,

    /// <summary>
    /// Half precision support.
    /// </summary>
    HalfPrecision,

    /// <summary>
    /// Atomic operations support.
    /// </summary>
    Atomics,

    /// <summary>
    /// Local memory support.
    /// </summary>
    LocalMemory,

    /// <summary>
    /// Image support.
    /// </summary>
    Images,

    /// <summary>
    /// 3D image support.
    /// </summary>
    Images3D,

    /// <summary>
    /// Unified memory support.
    /// </summary>
    UnifiedMemory,

    /// <summary>
    /// Dynamic parallelism support.
    /// </summary>
    DynamicParallelism,

    /// <summary>
    /// Tensor cores support.
    /// </summary>
    TensorCores
}

/// <summary>
/// Supported data types.
/// </summary>
[Flags]
public enum DataTypeSupport
{
    /// <summary>
    /// 8-bit integer.
    /// </summary>
    Int8 = 1 << 0,

    /// <summary>
    /// 16-bit integer.
    /// </summary>
    Int16 = 1 << 1,

    /// <summary>
    /// 32-bit integer.
    /// </summary>
    Int32 = 1 << 2,

    /// <summary>
    /// 64-bit integer.
    /// </summary>
    Int64 = 1 << 3,

    /// <summary>
    /// 16-bit float.
    /// </summary>
    Float16 = 1 << 4,

    /// <summary>
    /// 32-bit float.
    /// </summary>
    Float32 = 1 << 5,

    /// <summary>
    /// 64-bit float.
    /// </summary>
    Float64 = 1 << 6,

    /// <summary>
    /// Brain float 16.
    /// </summary>
    BFloat16 = 1 << 7
}

/// <summary>
/// Device memory information.
/// </summary>
public interface IDeviceMemoryInfo
{
    /// <summary>
    /// Gets total global memory in bytes.
    /// </summary>
    public long TotalGlobalMemory { get; }

    /// <summary>
    /// Gets available global memory in bytes.
    /// </summary>
    public long AvailableGlobalMemory { get; }

    /// <summary>
    /// Gets total local memory per work group in bytes.
    /// </summary>
    public long LocalMemoryPerWorkGroup { get; }

    /// <summary>
    /// Gets memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidth { get; }

    /// <summary>
    /// Gets cache sizes.
    /// </summary>
    public ICacheSizes CacheSizes { get; }

    /// <summary>
    /// Gets memory allocation granularity.
    /// </summary>
    public long AllocationGranularity { get; }
}

/// <summary>
/// Cache size information.
/// </summary>
public interface ICacheSizes
{
    /// <summary>
    /// Gets L1 cache size in bytes.
    /// </summary>
    public long L1Size { get; }

    /// <summary>
    /// Gets L2 cache size in bytes.
    /// </summary>
    public long L2Size { get; }

    /// <summary>
    /// Gets L3 cache size in bytes.
    /// </summary>
    public long L3Size { get; }

    /// <summary>
    /// Gets texture cache size in bytes.
    /// </summary>
    public long TextureCacheSize { get; }

    /// <summary>
    /// Gets constant cache size in bytes.
    /// </summary>
    public long ConstantCacheSize { get; }
}

/// <summary>
/// Device memory allocation.
/// </summary>
public interface IDeviceMemory : IAsyncDisposable
{
    /// <summary>
    /// Gets the memory size in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the device that owns this memory.
    /// </summary>
    public IComputeDevice Device { get; }

    /// <summary>
    /// Gets the memory access mode.
    /// </summary>
    public Memory.MemoryAccess AccessMode { get; }

    /// <summary>
    /// Copies data from host to device memory.
    /// </summary>
    public ValueTask WriteAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies data from device to host memory.
    /// </summary>
    public ValueTask ReadAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Fills memory with a pattern.
    /// </summary>
    public ValueTask FillAsync<T>(
        T pattern,
        long offset = 0,
        long? count = null,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies to another device memory.
    /// </summary>
    public ValueTask CopyToAsync(
        IDeviceMemory destination,
        long sourceOffset = 0,
        long destinationOffset = 0,
        long? sizeInBytes = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Command queue for kernel execution.
/// </summary>
public interface ICommandQueue : IAsyncDisposable
{
    /// <summary>
    /// Gets the queue identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the associated device.
    /// </summary>
    public IComputeDevice Device { get; }

    /// <summary>
    /// Enqueues a kernel for execution.
    /// </summary>
    public ValueTask EnqueueKernelAsync(
        ICompiledKernel kernel,
        KernelExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a memory copy operation.
    /// </summary>
    public ValueTask EnqueueCopyAsync(
        IDeviceMemory source,
        IDeviceMemory destination,
        long sourceOffset = 0,
        long destinationOffset = 0,
        long? sizeInBytes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a barrier to synchronize operations.
    /// </summary>
    public ValueTask EnqueueBarrierAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for all enqueued operations to complete.
    /// </summary>
    public ValueTask FinishAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes the command queue.
    /// </summary>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for command queue creation.
/// </summary>
public sealed class CommandQueueOptions
{
    /// <summary>
    /// Gets or sets whether to enable profiling.
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets whether to enable out-of-order execution.
    /// </summary>
    public bool EnableOutOfOrderExecution { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public QueuePriority Priority { get; set; } = QueuePriority.Normal;

    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static CommandQueueOptions Default { get; } = new();
}

/// <summary>
/// Command queue priorities.
/// </summary>
public enum QueuePriority
{
    /// <summary>
    /// Low priority.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority.
    /// </summary>
    High
}

/// <summary>
/// Device performance metrics.
/// </summary>
public interface IDeviceMetrics
{
    /// <summary>
    /// Gets current utilization percentage.
    /// </summary>
    public double Utilization { get; }

    /// <summary>
    /// Gets current memory usage percentage.
    /// </summary>
    public double MemoryUsage { get; }

    /// <summary>
    /// Gets current temperature in Celsius.
    /// </summary>
    public double? Temperature { get; }

    /// <summary>
    /// Gets current power consumption in watts.
    /// </summary>
    public double? PowerConsumption { get; }

    /// <summary>
    /// Gets kernel execution count.
    /// </summary>
    public long KernelExecutionCount { get; }

    /// <summary>
    /// Gets total compute time.
    /// </summary>
    public TimeSpan TotalComputeTime { get; }

    /// <summary>
    /// Gets average kernel execution time.
    /// </summary>
    public TimeSpan AverageKernelTime { get; }

    /// <summary>
    /// Gets memory transfer statistics.
    /// </summary>
    public IMemoryTransferStats TransferStats { get; }
}

/// <summary>
/// Memory transfer statistics.
/// </summary>
public interface IMemoryTransferStats
{
    /// <summary>
    /// Gets total bytes transferred to device.
    /// </summary>
    public long BytesToDevice { get; }

    /// <summary>
    /// Gets total bytes transferred from device.
    /// </summary>
    public long BytesFromDevice { get; }

    /// <summary>
    /// Gets average transfer rate to device in GB/s.
    /// </summary>
    public double AverageRateToDevice { get; }

    /// <summary>
    /// Gets average transfer rate from device in GB/s.
    /// </summary>
    public double AverageRateFromDevice { get; }

    /// <summary>
    /// Gets total transfer time.
    /// </summary>
    public TimeSpan TotalTransferTime { get; }
}
