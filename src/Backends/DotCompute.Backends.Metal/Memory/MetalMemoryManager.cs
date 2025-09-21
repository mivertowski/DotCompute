// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Memory;
using DotCompute.Core.Utilities;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Metal-specific memory manager implementation with real Metal API integration.
/// Consolidated using BaseMemoryManager to eliminate duplicate patterns.
/// </summary>
public sealed class MetalMemoryManager : BaseMemoryManager
{
    private readonly IntPtr _device;
    private readonly bool _isAppleSilicon;
    private readonly ConcurrentDictionary<IntPtr, MetalAllocationInfo> _activeAllocations;
    private WeakReference<IAccelerator>? _acceleratorRef;

    // Private fields for BaseMemoryManager
    private long _totalAllocatedBytes;
    private long _peakAllocatedBytes;
    private long _totalAllocations;
    private bool _disposed;

    /// <summary>
    /// Gets the Metal device used by this manager.
    /// </summary>
    public IntPtr Device => _device;

    /// <summary>
    /// Sets the accelerator reference after construction.
    /// </summary>
    /// <param name="accelerator">The accelerator to reference.</param>
    public void SetAcceleratorReference(IAccelerator accelerator)
    {
        _acceleratorRef = new WeakReference<IAccelerator>(accelerator);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="accelerator">Optional accelerator reference.</param>
    public MetalMemoryManager(ILogger<MetalMemoryManager> logger, IAccelerator? accelerator = null) : base(logger)
    {
        _device = GetOrCreateDevice();
        _isAppleSilicon = DetectAppleSilicon();
        _activeAllocations = new ConcurrentDictionary<IntPtr, MetalAllocationInfo>();
        
        if (accelerator != null)
        {
            _acceleratorRef = new WeakReference<IAccelerator>(accelerator);
        }
        
        var logger2 = logger as ILogger; // Access the base logger
        logger2?.LogInformation("Metal Memory Manager initialized for {Architecture} with device {DeviceId:X}",
            _isAppleSilicon ? "Apple Silicon" : "Intel Mac", _device.ToInt64());
    }
    
    private static IntPtr GetOrCreateDevice()
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device for memory manager");
        }
        return device;
    }

    /// <inheritdoc/>
    public override long MaxAllocationSize
    {
        get
        {
            if (_device != IntPtr.Zero)
            {
                try
                {
                    var deviceInfo = MetalNative.GetDeviceInfo(_device);
                    return (long)deviceInfo.MaxBufferLength;
                }
                catch
                {
                    // Fallback to conservative estimate
                    return 1L << 32; // 4GB
                }
            }
            return 1L << 32; // 4GB default
        }
    }

    /// <inheritdoc/>
    public override long CurrentAllocatedMemory => Interlocked.Read(ref _totalAllocatedBytes);

    /// <inheritdoc/>
    public override long TotalAvailableMemory
    {
        get
        {
            if (_isAppleSilicon)
            {
                return GetUnifiedMemorySize();
            }
            else
            {
                // Intel Mac with discrete GPU
                return MaxAllocationSize;
            }
        }
    }

    /// <inheritdoc/>
    public override IAccelerator Accelerator
    {
        get
        {
            if (_acceleratorRef != null && _acceleratorRef.TryGetTarget(out var accelerator))
            {
                return accelerator;
            }
            throw new InvalidOperationException("Accelerator reference not available or has been garbage collected");
        }
    }

    /// <inheritdoc/>
    public override MemoryStatistics Statistics
    {
        get
        {
            var totalAllocated = CurrentAllocatedMemory;
            var peakAllocated = Interlocked.Read(ref _peakAllocatedBytes);
            var totalAllocs = Interlocked.Read(ref _totalAllocations);
            
            return new MemoryStatistics
            {
                TotalAllocated = totalAllocated,
                PeakMemoryUsage = peakAllocated,
                AllocationCount = (int)Math.Min(totalAllocs, int.MaxValue),
                AvailableMemory = TotalAvailableMemory - totalAllocated
            };
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (sizeInBytes <= 0)
        {

            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive");
        }


        if (sizeInBytes > MaxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), $"Size {sizeInBytes} exceeds maximum allocation size {MaxAllocationSize}");
        }


        _ = Interlocked.Increment(ref _totalAllocations);
        
        // Allocate Metal buffer directly with real Metal API
        var buffer = new MetalMemoryBuffer(sizeInBytes, options, _device);
        await buffer.InitializeAsync(cancellationToken);
        
        TrackAllocation(buffer, sizeInBytes);
        return buffer;
    }

    /// <inheritdoc/>
    public override ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken)
    {
        if (buffer == null)
        {
            return ValueTask.CompletedTask;
        }


        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Track deallocation
        if (buffer is MetalMemoryBuffer metalBuffer && 
            _activeAllocations.TryRemove(metalBuffer.NativeHandle, out var info))
        {
            _ = Interlocked.Add(ref _totalAllocatedBytes, -info.SizeInBytes);
        }
        
        // Dispose the buffer
        buffer.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (source is MetalMemoryBuffer srcMetal && destination is MetalMemoryBuffer destMetal)
        {
            // Direct Metal buffer copy for optimal performance
            var elementSize = Unsafe.SizeOf<T>();
            var copySize = Math.Min(source.Length * elementSize, destination.Length * elementSize);
            
            MetalNative.CopyBuffer(srcMetal.Buffer, 0, destMetal.Buffer, 0, copySize);
        }
        else
        {
            // Fallback to host memory staging
            var tempData = new T[source.Length];
            await source.CopyToAsync(tempData, cancellationToken: cancellationToken);
            await destination.CopyFromAsync(tempData, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (source is MetalMemoryBuffer srcMetal && destination is MetalMemoryBuffer destMetal)
        {
            var elementSize = Unsafe.SizeOf<T>();
            var srcOffsetBytes = sourceOffset * elementSize;
            var destOffsetBytes = destinationOffset * elementSize;
            var copySize = count * elementSize;
            
            MetalNative.CopyBuffer(srcMetal.Buffer, srcOffsetBytes, destMetal.Buffer, destOffsetBytes, copySize);
        }
        else
        {
            // Fallback implementation using temporary buffer
            var tempData = new T[count];
            await source.CopyToAsync(tempData.AsMemory(), cancellationToken);
            await destination.CopyFromAsync(tempData.AsMemory(), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Standard device-to-host copy
        await source.CopyToAsync(destination, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Standard host-to-device copy
        await destination.CopyFromAsync(source, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public override IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        
        // Calculate byte offsets
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var offsetBytes = (long)offset * elementSize;
        var lengthBytes = (long)count * elementSize;
        
        // Use the non-generic CreateViewCore which handles the actual view creation
        var view = CreateViewCore(buffer, offsetBytes, lengthBytes);
        
        // Wrap in a typed buffer wrapper if needed
        if (view is IUnifiedMemoryBuffer<T> typedView)
        {
            return typedView;
        }
        
        // This shouldn't happen with our implementation, but handle it gracefully
        throw new InvalidOperationException($"Failed to create typed view for buffer of type {buffer.GetType()}");
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Clear tracking information
        _activeAllocations.Clear();
        
        // Reset statistics
        Interlocked.Exchange(ref _totalAllocatedBytes, 0);
        Interlocked.Exchange(ref _peakAllocatedBytes, 0);
        Interlocked.Exchange(ref _totalAllocations, 0);
    }

    /// <inheritdoc/>
    public override ValueTask OptimizeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        return buffer switch
        {
            MetalMemoryBuffer metalBuffer => new MetalMemoryBufferView(metalBuffer, offset, length),
            _ => throw new ArgumentException("Buffer must be a Metal memory buffer", nameof(buffer))
        };
    }
    
    /// <summary>
    /// Detects if running on Apple Silicon.
    /// </summary>
    private static bool DetectAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }


        try
        {
            // Check architecture - Apple Silicon typically reports as ARM64
            return RuntimeInformation.OSArchitecture == Architecture.Arm64;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Tracks a new allocation for statistics.
    /// </summary>
    private void TrackAllocation(IUnifiedMemoryBuffer buffer, long sizeInBytes)
    {
        if (buffer is MetalMemoryBuffer metalBuffer)
        {
            var info = new MetalAllocationInfo
            {
                SizeInBytes = sizeInBytes,
                AllocatedAt = DateTimeOffset.UtcNow,
                MemoryType = MetalMemoryType.Device
            };
            
            _activeAllocations.TryAdd(metalBuffer.NativeHandle, info);
            
            var totalBytes = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
            
            // Update peak if necessary
            long currentPeak;
            do
            {
                currentPeak = _peakAllocatedBytes;
                if (totalBytes <= currentPeak)
                {
                    break;
                }

            } while (Interlocked.CompareExchange(ref _peakAllocatedBytes, totalBytes, currentPeak) != currentPeak);
        }
    }
    
    /// <summary>
    /// Gets the unified memory size on Apple Silicon.
    /// </summary>
    private long GetUnifiedMemorySize()
    {
        if (!_isAppleSilicon)
        {
            return 0;
        }

        // This would query the actual unified memory size from the system
        // For now, return a reasonable default based on typical Apple Silicon configurations

        return 16L * 1024 * 1024 * 1024; // 16GB unified memory
    }
    
    /// <summary>
    /// Disposes the memory manager and all resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }


        if (disposing)
        {
            try
            {
                // Clear all resources
                Clear();
                
                // Release Metal device
                if (_device != IntPtr.Zero)
                {
                    try
                    {
                        MetalNative.ReleaseDevice(_device);
                    }
                    catch (Exception ex)
                    {
                        var logger = base.Logger as ILogger;
                        logger?.LogWarning(ex, "Failed to release Metal device during disposal");
                    }
                }
                
                var logger2 = base.Logger as ILogger;
                logger2?.LogInformation(
                    "MetalMemoryManager disposed - Total allocations: {TotalAllocs}, Peak usage: {PeakMB:F1} MB",
                    _totalAllocations, 
                    _peakAllocatedBytes / (1024.0 * 1024.0));
            }
            catch (Exception ex)
            {
                var logger = base.Logger as ILogger;
                logger?.LogError(ex, "Error during MetalMemoryManager disposal");
            }
        }
        
        _disposed = true;
        base.Dispose(disposing);
    }
}

#region Supporting Types

/// <summary>
/// Information about a Metal memory allocation.
/// </summary>
internal sealed record MetalAllocationInfo
{
    public required long SizeInBytes { get; init; }
    public required DateTimeOffset AllocatedAt { get; init; }
    public required MetalMemoryType MemoryType { get; init; }
}

/// <summary>
/// Types of Metal memory allocations.
/// </summary>
internal enum MetalMemoryType
{
    Device,
    Pinned,
    Unified,
    Mapped
}

#endregion