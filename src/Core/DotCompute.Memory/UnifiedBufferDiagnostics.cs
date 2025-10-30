// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Diagnostics and monitoring implementation for unified buffers.
/// Provides state tracking, performance metrics, and debugging support.
/// </summary>
public sealed partial class UnifiedBuffer<T> : IDisposable
{
    private long _hostToDeviceTransfers;
    private long _deviceToHostTransfers;
    private long _totalTransferTime;
    private DateTime _lastAccessTime = DateTime.UtcNow;

    /// <summary>
    /// Gets transfer statistics for this buffer.
    /// </summary>
    /// <returns>Transfer performance statistics.</returns>
    public BufferTransferStats GetTransferStats()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new BufferTransferStats
        {
            HostToDeviceTransfers = _hostToDeviceTransfers,
            DeviceToHostTransfers = _deviceToHostTransfers,
            TotalTransfers = _hostToDeviceTransfers + _deviceToHostTransfers,
            TotalTransferTimeMs = _totalTransferTime,
            AverageTransferTimeMs = (_hostToDeviceTransfers + _deviceToHostTransfers) > 0

                ? _totalTransferTime / (_hostToDeviceTransfers + _deviceToHostTransfers)

                : 0,
            LastAccessTime = _lastAccessTime,
            CurrentState = _state
        };
    }

    /// <summary>
    /// Resets transfer statistics.
    /// </summary>
    public void ResetTransferStats()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hostToDeviceTransfers = 0;
        _deviceToHostTransfers = 0;
        _totalTransferTime = 0;
        _lastAccessTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates the integrity of the buffer data.
    /// </summary>
    /// <returns>True if buffer data is consistent, false otherwise.</returns>
    public bool ValidateIntegrity()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Validate memory state
            if (!ValidateMemoryState())
            {
                return false;
            }

            // Check for state consistency
            switch (_state)
            {
                case BufferState.HostOnly:
                    return _hostArray != null && _pinnedHandle.IsAllocated && _deviceMemory.Handle == IntPtr.Zero;


                case BufferState.DeviceOnly:
                    return _deviceMemory.Handle != IntPtr.Zero;


                case BufferState.Synchronized:
                    return _hostArray != null && _pinnedHandle.IsAllocated && _deviceMemory.Handle != IntPtr.Zero;


                case BufferState.HostDirty:
                    return _hostArray != null && _pinnedHandle.IsAllocated;


                case BufferState.DeviceDirty:
                    return _deviceMemory.Handle != IntPtr.Zero;


                case BufferState.Uninitialized:
                    return _hostArray == null && _deviceMemory.Handle == IntPtr.Zero;


                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets detailed diagnostic information about the buffer.
    /// </summary>
    /// <returns>Comprehensive buffer diagnostic information.</returns>
    public BufferDiagnosticInfo GetDiagnosticInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var memoryInfo = GetMemoryInfo();
        var transferStats = GetTransferStats();

        return new BufferDiagnosticInfo
        {
            Length = Length,
            SizeInBytes = SizeInBytes,
            ElementType = typeof(T).Name,
            State = _state,
            IsDisposed = _disposed,
            MemoryInfo = memoryInfo,
            TransferStats = transferStats,
            IsIntegrityValid = ValidateIntegrity(),
            CreationTime = DateTime.UtcNow, // This would be tracked from constructor in full implementation
            LastModifiedTime = _lastAccessTime
        };
    }

    /// <summary>
    /// Tracks a host-to-device transfer for performance monitoring.
    /// </summary>
    private void TrackHostToDeviceTransfer(TimeSpan duration)
    {
        _ = Interlocked.Increment(ref _hostToDeviceTransfers);
        _ = Interlocked.Add(ref _totalTransferTime, duration.Milliseconds);
        _lastAccessTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Tracks a device-to-host transfer for performance monitoring.
    /// </summary>
    private void TrackDeviceToHostTransfer(TimeSpan duration)
    {
        _ = Interlocked.Increment(ref _deviceToHostTransfers);
        _ = Interlocked.Add(ref _totalTransferTime, duration.Milliseconds);
        _lastAccessTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a snapshot of the current buffer state.
    /// </summary>
    /// <returns>A snapshot containing current buffer information.</returns>
    public BufferSnapshot CreateSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new BufferSnapshot
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Length = Length,
            SizeInBytes = SizeInBytes,
            State = _state,
            IsOnHost = IsOnHost,
            IsOnDevice = IsOnDevice,
            IsDirty = IsDirty,
            TransferCount = _hostToDeviceTransfers + _deviceToHostTransfers,
            LastAccessTime = _lastAccessTime
        };
    }

    /// <summary>
    /// Performs a deep validation of buffer consistency.
    /// </summary>
    /// <returns>Detailed validation results.</returns>
    public BufferValidationResult PerformDeepValidation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new BufferValidationResult
        {
            IsValid = true,
            Issues = [],
            Warnings = []
        };

        // Check basic properties
        if (Length <= 0)
        {
            result.Issues.Add("Buffer length is not positive");
            result.IsValid = false;
        }

        if (SizeInBytes != Length * Unsafe.SizeOf<T>())
        {
            result.Issues.Add("Size calculation mismatch");
            result.IsValid = false;
        }

        // Check state consistency
        if (!ValidateIntegrity())
        {
            result.Issues.Add("Buffer integrity validation failed");
            result.IsValid = false;
        }

        // Check memory allocations
        if (_state != BufferState.Uninitialized)
        {
            if (IsOnHost && (_hostArray == null || !_pinnedHandle.IsAllocated))
            {
                result.Issues.Add("Host allocation inconsistent with state");
                result.IsValid = false;
            }

            if (IsOnDevice && _deviceMemory.Handle == IntPtr.Zero)
            {
                result.Issues.Add("Device allocation inconsistent with state");
                result.IsValid = false;
            }
        }

        // Performance warnings
        var transferStats = GetTransferStats();
        if (transferStats.TotalTransfers > 100)
        {
            result.Warnings.Add($"High transfer count detected: {transferStats.TotalTransfers}");
        }

        if (transferStats.AverageTransferTimeMs > 10)
        {
            result.Warnings.Add($"Slow average transfer time: {transferStats.AverageTransferTimeMs}ms");
        }

        // Memory usage warnings
        if (IsOnHost && IsOnDevice && SizeInBytes > 1024 * 1024) // 1MB
        {
            result.Warnings.Add("Large buffer duplicated in both host and device memory");
        }

        return result;
    }

    /// <summary>
    /// Disposes the buffer and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Deallocate memory resources
        DeallocateDeviceMemory();
        DeallocateHostMemory();

        // Dispose synchronization primitives
        _asyncLock.Dispose();

        // Clear device buffer reference
        _deviceBuffer = null;
    }

    /// <summary>
    /// Asynchronously disposes the buffer and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Deallocate memory resources
        DeallocateDeviceMemory();
        DeallocateHostMemory();

        // Dispose synchronization primitives
        _asyncLock.Dispose();

        // Clear device buffer reference
        _deviceBuffer = null;

        await ValueTask.CompletedTask;
    }
}

/// <summary>
/// Performance statistics for buffer transfers.
/// </summary>
public sealed class BufferTransferStats
{
    /// <summary>
    /// Gets or sets the host to device transfers.
    /// </summary>
    /// <value>The host to device transfers.</value>
    public long HostToDeviceTransfers { get; init; }
    /// <summary>
    /// Gets or sets the device to host transfers.
    /// </summary>
    /// <value>The device to host transfers.</value>
    public long DeviceToHostTransfers { get; init; }
    /// <summary>
    /// Gets or sets the total transfers.
    /// </summary>
    /// <value>The total transfers.</value>
    public long TotalTransfers { get; init; }
    /// <summary>
    /// Gets or sets the total transfer time ms.
    /// </summary>
    /// <value>The total transfer time ms.</value>
    public long TotalTransferTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the average transfer time ms.
    /// </summary>
    /// <value>The average transfer time ms.</value>
    public long AverageTransferTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the last access time.
    /// </summary>
    /// <value>The last access time.</value>
    public DateTime LastAccessTime { get; init; }
    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    /// <value>The current state.</value>
    public BufferState CurrentState { get; init; }
}

/// <summary>
/// Comprehensive diagnostic information for a buffer.
/// </summary>
public sealed class BufferDiagnosticInfo
{
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length { get; init; }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; init; }
    /// <summary>
    /// Gets or sets the element type.
    /// </summary>
    /// <value>The element type.</value>
    public string ElementType { get; init; } = "";
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed { get; init; }
    /// <summary>
    /// Gets or sets the memory info.
    /// </summary>
    /// <value>The memory info.</value>
    public BufferMemoryInfo MemoryInfo { get; init; } = null!;
    /// <summary>
    /// Gets or sets the transfer stats.
    /// </summary>
    /// <value>The transfer stats.</value>
    public BufferTransferStats TransferStats { get; init; } = null!;
    /// <summary>
    /// Gets or sets a value indicating whether integrity valid.
    /// </summary>
    /// <value>The is integrity valid.</value>
    public bool IsIntegrityValid { get; init; }
    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>The creation time.</value>
    public DateTime CreationTime { get; init; }
    /// <summary>
    /// Gets or sets the last modified time.
    /// </summary>
    /// <value>The last modified time.</value>
    public DateTime LastModifiedTime { get; init; }
}

/// <summary>
/// Snapshot of buffer state at a specific point in time.
/// </summary>
public sealed class BufferSnapshot
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public Guid Id { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length { get; init; }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; init; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty { get; init; }
    /// <summary>
    /// Gets or sets the transfer count.
    /// </summary>
    /// <value>The transfer count.</value>
    public long TransferCount { get; init; }
    /// <summary>
    /// Gets or sets the last access time.
    /// </summary>
    /// <value>The last access time.</value>
    public DateTime LastAccessTime { get; init; }
}

/// <summary>
/// Results of buffer validation checks.
/// </summary>
public sealed class BufferValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public List<string> Issues { get; init; } = [];
    /// <summary>
    /// Gets or sets the warnings.
    /// </summary>
    /// <value>The warnings.</value>
    public List<string> Warnings { get; init; } = [];
}
