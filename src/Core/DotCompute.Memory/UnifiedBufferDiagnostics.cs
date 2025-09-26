// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DeviceMemory = DotCompute.Abstractions.DeviceMemory;

namespace DotCompute.Memory;

/// <summary>
/// Diagnostics and monitoring implementation for unified buffers.
/// Provides state tracking, performance metrics, and debugging support.
/// </summary>
public sealed partial class UnifiedBuffer<T> : IDisposable
{
    private readonly Stopwatch _transferStopwatch = new();
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
        Interlocked.Increment(ref _hostToDeviceTransfers);
        Interlocked.Add(ref _totalTransferTime, duration.Milliseconds);
        _lastAccessTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Tracks a device-to-host transfer for performance monitoring.
    /// </summary>
    private void TrackDeviceToHostTransfer(TimeSpan duration)
    {
        Interlocked.Increment(ref _deviceToHostTransfers);
        Interlocked.Add(ref _totalTransferTime, duration.Milliseconds);
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
            Issues = new List<string>(),
            Warnings = new List<string>()
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
    public long HostToDeviceTransfers { get; init; }
    public long DeviceToHostTransfers { get; init; }
    public long TotalTransfers { get; init; }
    public long TotalTransferTimeMs { get; init; }
    public long AverageTransferTimeMs { get; init; }
    public DateTime LastAccessTime { get; init; }
    public BufferState CurrentState { get; init; }
}

/// <summary>
/// Comprehensive diagnostic information for a buffer.
/// </summary>
public sealed class BufferDiagnosticInfo
{
    public int Length { get; init; }
    public long SizeInBytes { get; init; }
    public string ElementType { get; init; } = "";
    public BufferState State { get; init; }
    public bool IsDisposed { get; init; }
    public BufferMemoryInfo MemoryInfo { get; init; } = null!;
    public BufferTransferStats TransferStats { get; init; } = null!;
    public bool IsIntegrityValid { get; init; }
    public DateTime CreationTime { get; init; }
    public DateTime LastModifiedTime { get; init; }
}

/// <summary>
/// Snapshot of buffer state at a specific point in time.
/// </summary>
public sealed class BufferSnapshot
{
    public Guid Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public int Length { get; init; }
    public long SizeInBytes { get; init; }
    public BufferState State { get; init; }
    public bool IsOnHost { get; init; }
    public bool IsOnDevice { get; init; }
    public bool IsDirty { get; init; }
    public long TransferCount { get; init; }
    public DateTime LastAccessTime { get; init; }
}

/// <summary>
/// Results of buffer validation checks.
/// </summary>
public sealed class BufferValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}