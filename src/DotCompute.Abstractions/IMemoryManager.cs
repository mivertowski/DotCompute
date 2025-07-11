using System;
using System.Buffers;

namespace DotCompute.Abstractions;

/// <summary>
/// Manages memory allocation and data transfer for an accelerator device.
/// Designed for zero-copy operations and AOT compatibility.
/// </summary>
public interface IMemoryManager : IDisposable
{
    /// <summary>
    /// Gets the accelerator associated with this memory manager.
    /// </summary>
    IAccelerator Accelerator { get; }
    
    /// <summary>
    /// Allocates memory on the accelerator device.
    /// </summary>
    /// <param name="sizeInBytes">The size of memory to allocate in bytes.</param>
    /// <returns>A handle to the allocated memory.</returns>
    DeviceMemory Allocate(long sizeInBytes);
    
    /// <summary>
    /// Allocates memory with a specific alignment requirement.
    /// </summary>
    /// <param name="sizeInBytes">The size of memory to allocate in bytes.</param>
    /// <param name="alignment">The alignment requirement in bytes.</param>
    /// <returns>A handle to the allocated memory.</returns>
    DeviceMemory AllocateAligned(long sizeInBytes, int alignment);
    
    /// <summary>
    /// Frees previously allocated device memory.
    /// </summary>
    /// <param name="memory">The memory to free.</param>
    void Free(DeviceMemory memory);
    
    /// <summary>
    /// Copies data from host memory to device memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source data on the host.</param>
    /// <param name="destination">The destination memory on the device.</param>
    void CopyToDevice<T>(ReadOnlySpan<T> source, DeviceMemory destination) where T : unmanaged;
    
    /// <summary>
    /// Copies data from device memory to host memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source memory on the device.</param>
    /// <param name="destination">The destination buffer on the host.</param>
    void CopyToHost<T>(DeviceMemory source, Span<T> destination) where T : unmanaged;
    
    /// <summary>
    /// Copies data between device memory regions.
    /// </summary>
    /// <param name="source">The source memory on the device.</param>
    /// <param name="destination">The destination memory on the device.</param>
    /// <param name="sizeInBytes">The number of bytes to copy.</param>
    void CopyDeviceToDevice(DeviceMemory source, DeviceMemory destination, long sizeInBytes);
    
    /// <summary>
    /// Asynchronously copies data from host to device.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source data on the host.</param>
    /// <param name="destination">The destination memory on the device.</param>
    /// <param name="stream">The stream to use for the async operation.</param>
    void CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, DeviceMemory destination, AcceleratorStream stream) where T : unmanaged;
    
    /// <summary>
    /// Asynchronously copies data from device to host.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source memory on the device.</param>
    /// <param name="destination">The destination buffer on the host.</param>
    /// <param name="stream">The stream to use for the async operation.</param>
    void CopyToHostAsync<T>(DeviceMemory source, Memory<T> destination, AcceleratorStream stream) where T : unmanaged;
    
    /// <summary>
    /// Gets the total available memory on the device in bytes.
    /// </summary>
    /// <returns>The available memory in bytes.</returns>
    long GetAvailableMemory();
    
    /// <summary>
    /// Gets the total memory on the device in bytes.
    /// </summary>
    /// <returns>The total memory in bytes.</returns>
    long GetTotalMemory();
    
    /// <summary>
    /// Attempts to allocate pinned host memory for efficient transfers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>A memory owner for the pinned memory.</returns>
    IMemoryOwner<T> AllocatePinnedHost<T>(int length) where T : unmanaged;
}

/// <summary>
/// Represents a handle to memory allocated on an accelerator device.
/// This is a value type for performance and AOT compatibility.
/// </summary>
public readonly struct DeviceMemory : IEquatable<DeviceMemory>
{
    /// <summary>
    /// Gets the native pointer to the device memory.
    /// </summary>
    public IntPtr NativePointer { get; }
    
    /// <summary>
    /// Gets the size of the allocated memory in bytes.
    /// </summary>
    public long SizeInBytes { get; }
    
    /// <summary>
    /// Gets whether this memory handle is valid.
    /// </summary>
    public bool IsValid => NativePointer != IntPtr.Zero && SizeInBytes > 0;
    
    public DeviceMemory(IntPtr nativePointer, long sizeInBytes)
    {
        NativePointer = nativePointer;
        SizeInBytes = sizeInBytes;
    }
    
    /// <summary>
    /// Gets an invalid memory handle.
    /// </summary>
    public static DeviceMemory Invalid => default;
    
    public bool Equals(DeviceMemory other)
    {
        return NativePointer == other.NativePointer && SizeInBytes == other.SizeInBytes;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is DeviceMemory other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(NativePointer, SizeInBytes);
    }
    
    public static bool operator ==(DeviceMemory left, DeviceMemory right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(DeviceMemory left, DeviceMemory right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Represents an execution stream on an accelerator for asynchronous operations.
/// </summary>
public readonly struct AcceleratorStream : IEquatable<AcceleratorStream>
{
    /// <summary>
    /// Gets the native stream handle.
    /// </summary>
    public IntPtr NativeHandle { get; }
    
    /// <summary>
    /// Gets whether this is the default stream.
    /// </summary>
    public bool IsDefault => NativeHandle == IntPtr.Zero;
    
    public AcceleratorStream(IntPtr nativeHandle)
    {
        NativeHandle = nativeHandle;
    }
    
    /// <summary>
    /// Gets the default stream.
    /// </summary>
    public static AcceleratorStream Default => default;
    
    public bool Equals(AcceleratorStream other)
    {
        return NativeHandle == other.NativeHandle;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is AcceleratorStream other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return NativeHandle.GetHashCode();
    }
    
    public static bool operator ==(AcceleratorStream left, AcceleratorStream right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(AcceleratorStream left, AcceleratorStream right)
    {
        return !left.Equals(right);
    }
}