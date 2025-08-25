// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Options for memory allocation and management.
/// </summary>
[Flags]
public enum MemoryOptions
{
    /// <summary>
    /// No special options.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Memory should be pinned in physical memory.
    /// </summary>
    Pinned = 1 << 0,
    
    /// <summary>
    /// Memory should be accessible from host.
    /// </summary>
    HostAccessible = 1 << 1,
    
    /// <summary>
    /// Memory should be cached.
    /// </summary>
    Cached = 1 << 2,
    
    /// <summary>
    /// Memory should be write-combined.
    /// </summary>
    WriteCombined = 1 << 3,
    
    /// <summary>
    /// Memory should be allocated from the pool.
    /// </summary>
    Pooled = 1 << 4,
    
    /// <summary>
    /// Memory should be zero-initialized.
    /// </summary>
    ZeroInitialized = 1 << 5,
    
    /// <summary>
    /// Memory should be aligned to cache lines.
    /// </summary>
    CacheAligned = 1 << 6,
    
    /// <summary>
    /// Memory should be accessible from all devices.
    /// </summary>
    UnifiedMemory = 1 << 7,
    
    /// <summary>
    /// Memory should be optimized for read access.
    /// </summary>
    ReadOptimized = 1 << 8,
    
    /// <summary>
    /// Memory should be optimized for write access.
    /// </summary>
    WriteOptimized = 1 << 9
}

/// <summary>
/// Memory types supported by the system.
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// Device local memory.
    /// </summary>
    Device,
    
    /// <summary>
    /// Host memory.
    /// </summary>
    Host,
    
    /// <summary>
    /// Shared memory accessible by both host and device.
    /// </summary>
    Shared,
    
    /// <summary>
    /// Unified memory automatically managed.
    /// </summary>
    Unified,
    
    /// <summary>
    /// Pinned host memory.
    /// </summary>
    Pinned,
    
    /// <summary>
    /// Constant memory.
    /// </summary>
    Constant,
    
    /// <summary>
    /// Texture memory.
    /// </summary>
    Texture
}

/// <summary>
/// Memory mapping modes.
/// </summary>
public enum MapMode
{
    /// <summary>
    /// Map for read access.
    /// </summary>
    Read,
    
    /// <summary>
    /// Map for write access.
    /// </summary>
    Write,
    
    /// <summary>
    /// Map for read and write access.
    /// </summary>
    ReadWrite,
    
    /// <summary>
    /// Map for write access, discarding previous contents.
    /// </summary>
    WriteDiscard,
    
    /// <summary>
    /// Map for write access without waiting.
    /// </summary>
    WriteNoOverwrite
}

/// <summary>
/// Represents mapped memory for direct access.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class MappedMemory<T> : IDisposable where T : unmanaged
{
    private readonly Action _unmapAction;
    private bool _disposed;
    
    /// <summary>
    /// Gets the mapped memory span.
    /// </summary>
    public Span<T> Span { get; }
    
    /// <summary>
    /// Gets the pointer to the mapped memory.
    /// </summary>
    public unsafe T* Pointer { get; }
    
    /// <summary>
    /// Gets the number of elements.
    /// </summary>
    public int Length { get; }
    
    /// <summary>
    /// Initializes a new instance of the MappedMemory class.
    /// </summary>
    public unsafe MappedMemory(T* pointer, int length, Action unmapAction)
    {
        Pointer = pointer;
        Length = length;
        Span = new Span<T>(pointer, length);
        _unmapAction = unmapAction ?? throw new ArgumentNullException(nameof(unmapAction));
    }
    
    /// <summary>
    /// Unmaps the memory.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _unmapAction();
            _disposed = true;
        }
    }
}