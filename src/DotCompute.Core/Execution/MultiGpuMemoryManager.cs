// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution;

/// <summary>
/// Manages memory allocation and transfers across multiple GPU devices.
/// </summary>
public sealed class MultiGpuMemoryManager : IAsyncDisposable
{
    private readonly ILogger<MultiGpuMemoryManager> _logger;
    private readonly ConcurrentDictionary<string, DeviceMemoryPool> _devicePools;
    private readonly ConcurrentDictionary<string, PeerToPeerConnection> _p2pConnections;
    private readonly TransferScheduler _transferScheduler;
    private readonly MemoryCoherenceManager _coherenceManager;
    private readonly SemaphoreSlim _managementSemaphore;
    private bool _disposed;

    public MultiGpuMemoryManager(ILogger<MultiGpuMemoryManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _devicePools = new ConcurrentDictionary<string, DeviceMemoryPool>();
        _p2pConnections = new ConcurrentDictionary<string, PeerToPeerConnection>();
        _transferScheduler = new TransferScheduler(logger);
        _coherenceManager = new MemoryCoherenceManager(logger);
        _managementSemaphore = new SemaphoreSlim(1);
    }

    /// <summary>
    /// Creates a buffer slice on the target device from a source buffer.
    /// </summary>
    public async ValueTask<AbstractionsMemory.IBuffer<T>> CreateBufferSliceAsync<T>(
        AbstractionsMemory.IBuffer<T> sourceBuffer,
        IAccelerator targetDevice,
        int startIndex,
        int elementCount,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        await _managementSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Creating buffer slice on device {DeviceId}: start={StartIndex}, count={ElementCount}",
                targetDevice.Info.Id, startIndex, elementCount);

            // Get or create memory pool for the target device
            var devicePool = _devicePools.GetOrAdd(targetDevice.Info.Id, 
                _ => new DeviceMemoryPool(targetDevice, _logger));

            // Allocate buffer on target device
            var sizeInBytes = elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
            var memoryBuffer = await targetDevice.Memory.AllocateAsync(sizeInBytes, AbstractionsMemory.MemoryOptions.None, cancellationToken);
            
            // Create a buffer wrapper - this is simplified, real implementation would use proper factory
            var targetBuffer = CreateBufferFromMemoryBuffer<T>(memoryBuffer, targetDevice, elementCount);

            // Schedule data transfer from source to target
            await ScheduleBufferTransferAsync(
                sourceBuffer, targetBuffer, startIndex, 0, elementCount, cancellationToken);

            // Track coherence
            _coherenceManager.TrackBuffer(targetBuffer, sourceBuffer, startIndex, elementCount);

            return targetBuffer;
        }
        finally
        {
            _managementSemaphore.Release();
        }
    }

    /// <summary>
    /// Waits for all pending transfers to a specific device to complete.
    /// </summary>
    public async ValueTask WaitForTransfersAsync(IAccelerator device, CancellationToken cancellationToken = default)
    {
        await _transferScheduler.WaitForDeviceTransfersAsync(device.Info.Id, cancellationToken);
    }

    /// <summary>
    /// Enables peer-to-peer transfers between two devices if supported.
    /// </summary>
    public async ValueTask<bool> EnablePeerToPeerAsync(IAccelerator device1, IAccelerator device2)
    {
        var connectionKey = GetConnectionKey(device1.Info.Id, device2.Info.Id);
        
        if (_p2pConnections.TryGetValue(connectionKey, out var existingConnection))
        {
            return existingConnection.IsEnabled;
        }

        try
        {
            // Check if P2P is supported (this would be device-specific implementation)
            var isSupported = await CheckPeerToPeerSupportAsync(device1, device2);
            
            var connection = new PeerToPeerConnection
            {
                Device1Id = device1.Info.Id,
                Device2Id = device2.Info.Id,
                IsEnabled = isSupported,
                IsSupported = isSupported,
                BandwidthGBps = isSupported ? EstimatePeerToPeerBandwidth(device1, device2) : 0
            };

            _p2pConnections[connectionKey] = connection;

            if (isSupported)
            {
                _logger.LogInformation("Enabled P2P between {Device1} and {Device2} with bandwidth {BandwidthGBps:F1} GB/s",
                    device1.Info.Name, device2.Info.Name, connection.BandwidthGBps);
            }
            else
            {
                _logger.LogWarning("P2P not supported between {Device1} and {Device2}",
                    device1.Info.Name, device2.Info.Name);
            }

            return isSupported;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable P2P between {Device1} and {Device2}",
                device1.Info.Name, device2.Info.Name);
            return false;
        }
    }

    /// <summary>
    /// Transfers data between buffers on different devices.
    /// </summary>
    public async ValueTask TransferBufferAsync<T>(
        AbstractionsMemory.IBuffer<T> sourceBuffer,
        AbstractionsMemory.IBuffer<T> targetBuffer,
        int sourceOffset = 0,
        int targetOffset = 0,
        int? elementCount = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceElementCount = (int)(sourceBuffer.SizeInBytes / System.Runtime.InteropServices.Marshal.SizeOf<T>());
        var targetElementCount = (int)(targetBuffer.SizeInBytes / System.Runtime.InteropServices.Marshal.SizeOf<T>());
        var transferCount = elementCount ?? Math.Min(
            sourceElementCount - sourceOffset,
            targetElementCount - targetOffset);

        await ScheduleBufferTransferAsync(
            sourceBuffer, targetBuffer, sourceOffset, targetOffset, transferCount, cancellationToken);
    }

    /// <summary>
    /// Synchronizes a buffer across all devices that have copies of it.
    /// </summary>
    public async ValueTask SynchronizeBufferAsync<T>(AbstractionsMemory.IBuffer<T> buffer, CancellationToken cancellationToken = default) where T : unmanaged
    {
        await _coherenceManager.SynchronizeBufferAsync(buffer, cancellationToken);
    }

    /// <summary>
    /// Gets memory usage statistics across all devices.
    /// </summary>
    public MultiGpuMemoryStatistics GetMemoryStatistics()
    {
        var stats = new MultiGpuMemoryStatistics
        {
            DeviceStatistics = new Dictionary<string, DeviceMemoryStatistics>(),
            TotalP2PConnections = _p2pConnections.Count,
            ActiveP2PConnections = _p2pConnections.Count(kvp => kvp.Value.IsEnabled),
            PendingTransfers = _transferScheduler.GetPendingTransferCount(),
            CoherenceOverhead = _coherenceManager.GetOverheadPercentage()
        };

        foreach (var kvp in _devicePools)
        {
            stats.DeviceStatistics[kvp.Key] = kvp.Value.GetStatistics();
        }

        return stats;
    }

    /// <summary>
    /// Optimizes memory layout for better performance across devices.
    /// </summary>
    public async ValueTask OptimizeMemoryLayoutAsync(IAccelerator[] devices, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing memory layout across {DeviceCount} devices", devices.Length);

        // Enable P2P between all device pairs where possible
        for (int i = 0; i < devices.Length; i++)
        {
            for (int j = i + 1; j < devices.Length; j++)
            {
                await EnablePeerToPeerAsync(devices[i], devices[j]);
            }
        }

        // Optimize buffer placement based on access patterns
        await _coherenceManager.OptimizePlacementAsync(devices, cancellationToken);

        _logger.LogInformation("Memory layout optimization completed");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing MultiGpuMemoryManager");

        // Dispose all device pools
        var disposeTasks = _devicePools.Values.Select(pool => pool.DisposeAsync()).ToArray();
        foreach (var task in disposeTasks)
        {
            await task.ConfigureAwait(false);
        }

        await _transferScheduler.DisposeAsync().ConfigureAwait(false);
        await _coherenceManager.DisposeAsync().ConfigureAwait(false);
        
        _devicePools.Clear();
        _p2pConnections.Clear();
        _managementSemaphore.Dispose();
        
        _disposed = true;
        _logger.LogInformation("MultiGpuMemoryManager disposed");
    }

    #region Private Methods

    private async ValueTask ScheduleBufferTransferAsync<T>(
        AbstractionsMemory.IBuffer<T> sourceBuffer,
        AbstractionsMemory.IBuffer<T> targetBuffer,
        int sourceOffset,
        int targetOffset,
        int elementCount,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var transfer = new BufferTransfer<T>
        {
            SourceBuffer = sourceBuffer,
            TargetBuffer = targetBuffer,
            SourceOffset = sourceOffset,
            TargetOffset = targetOffset,
            ElementCount = elementCount,
            Priority = TransferPriority.Normal
        };

        await _transferScheduler.ScheduleTransferAsync(transfer, cancellationToken);
    }

    private async ValueTask<bool> CheckPeerToPeerSupportAsync(IAccelerator device1, IAccelerator device2)
    {
        // This is a simplified check - real implementation would query device capabilities
        if (device1.Info.DeviceType != device2.Info.DeviceType)
            return false;

        if (device1.Info.DeviceType == "CUDA")
        {
            // For CUDA, check if devices are on the same node and support P2P
            return await CheckCudaPeerToPeerSupportAsync(device1, device2);
        }
        
        if (device1.Info.DeviceType == "ROCm")
        {
            // For ROCm, check XGMI connectivity
            return await CheckRocmPeerToPeerSupportAsync(device1, device2);
        }

        // Default: assume no P2P support for other types
        return false;
    }

    private async ValueTask<bool> CheckCudaPeerToPeerSupportAsync(IAccelerator device1, IAccelerator device2)
    {
        // Placeholder for CUDA P2P capability check
        await Task.CompletedTask;
        return true; // Assume supported for demonstration
    }

    private async ValueTask<bool> CheckRocmPeerToPeerSupportAsync(IAccelerator device1, IAccelerator device2)
    {
        // Placeholder for ROCm XGMI connectivity check
        await Task.CompletedTask;
        return true; // Assume supported for demonstration
    }

    private double EstimatePeerToPeerBandwidth(IAccelerator device1, IAccelerator device2)
    {
        // Simplified bandwidth estimation based on device types
        var deviceType = device1.Info.DeviceType;
        
        return deviceType switch
        {
            "CUDA" => 600.0, // NVLink bandwidth estimate
            "ROCm" => 400.0, // XGMI bandwidth estimate
            _ => 16.0 // PCIe 4.0 x16 bandwidth estimate
        };
    }

    private static string GetConnectionKey(string device1Id, string device2Id)
    {
        // Ensure consistent ordering for bidirectional connections
        var ids = new[] { device1Id, device2Id }.OrderBy(id => id).ToArray();
        return $"{ids[0]}<->{ids[1]}";
    }

    private AbstractionsMemory.IBuffer<T> CreateBufferFromMemoryBuffer<T>(
        AbstractionsMemory.IMemoryBuffer memoryBuffer, 
        IAccelerator device, 
        int elementCount) where T : unmanaged
    {
        // Create a simple buffer wrapper for the memory buffer
        // In a production implementation, this would use a proper buffer factory
        return new SimpleDeviceBuffer<T>(memoryBuffer, device, elementCount);
    }
    
    /// <summary>
    /// Simple device buffer implementation for internal use.
    /// </summary>
    private sealed class SimpleDeviceBuffer<T> : AbstractionsMemory.IBuffer<T>, AbstractionsMemory.IMemoryBuffer where T : unmanaged
    {
        private readonly AbstractionsMemory.IMemoryBuffer _memoryBuffer;
        private readonly IAccelerator _device;
        private readonly int _elementCount;
        
        public SimpleDeviceBuffer(AbstractionsMemory.IMemoryBuffer memoryBuffer, IAccelerator device, int elementCount)
        {
            _memoryBuffer = memoryBuffer ?? throw new ArgumentNullException(nameof(memoryBuffer));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _elementCount = elementCount;
        }
        
        public int Length => _elementCount;
        public long SizeInBytes => _memoryBuffer.SizeInBytes;
        public AbstractionsMemory.MemoryLocation Location => AbstractionsMemory.MemoryLocation.Device;
        public AbstractionsMemory.MemoryAccess Access => AbstractionsMemory.MemoryAccess.ReadWrite;
        public AbstractionsMemory.MemoryOptions Options => AbstractionsMemory.MemoryOptions.None;
        public IAccelerator Accelerator => _device;
        
        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        {
            return _memoryBuffer.CopyFromHostAsync(source, 0, cancellationToken);
        }
        
        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        {
            return _memoryBuffer.CopyToHostAsync(destination, 0, cancellationToken);
        }
        
        public ValueTask CopyFromAsync(AbstractionsMemory.IBuffer<T> source, CancellationToken cancellationToken = default)
        {
            // Simplified implementation - would need proper device-to-device copy
            return ValueTask.CompletedTask;
        }
        
        public ValueTask CopyToAsync(AbstractionsMemory.IBuffer<T> destination, CancellationToken cancellationToken = default)
        {
            // Simplified implementation - would need proper device-to-device copy
            return ValueTask.CompletedTask;
        }
        
        public ValueTask CopyToAsync(int sourceOffset, AbstractionsMemory.IBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
        {
            // Simplified implementation for partial copy
            return ValueTask.CompletedTask;
        }
        
        public ValueTask<T[]> ToArrayAsync(CancellationToken cancellationToken = default)
        {
            var array = new T[_elementCount];
            return ValueTask.FromResult(array);
        }
        
        public AbstractionsMemory.IBuffer<T> Slice(int start, int length)
        {
            // Return a new buffer representing the slice
            return new SimpleDeviceBuffer<T>(_memoryBuffer, _device, length);
        }
        
        public AbstractionsMemory.IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        {
            // Create a new buffer with reinterpreted type
            var newElementCount = (int)(SizeInBytes / System.Runtime.InteropServices.Marshal.SizeOf<TNew>());
            return new SimpleDeviceBuffer<TNew>(_memoryBuffer, _device, newElementCount);
        }
        
        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        {
            // Simplified implementation - would fill entire buffer with value
            return ValueTask.CompletedTask;
        }
        
        public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
        {
            // Simplified implementation - would fill partial buffer with value
            return ValueTask.CompletedTask;
        }
        
        public AbstractionsMemory.MappedMemory<T> Map(AbstractionsMemory.MapMode mode)
        {
            // Simplified implementation - return default for testing
            // In production, would use proper memory mapping via device-specific APIs
            return default(AbstractionsMemory.MappedMemory<T>);
        }
        
        public AbstractionsMemory.MappedMemory<T> MapRange(int offset, int length, AbstractionsMemory.MapMode mode)
        {
            // Simplified implementation - return default for testing
            // In production, would use proper memory mapping via device-specific APIs
            return default(AbstractionsMemory.MappedMemory<T>);
        }
        
        public ValueTask<AbstractionsMemory.MappedMemory<T>> MapAsync(AbstractionsMemory.MapMode mode, CancellationToken cancellationToken = default)
        {
            // Simplified implementation - return default for testing
            // In production, would use proper memory mapping via device-specific APIs
            return ValueTask.FromResult(default(AbstractionsMemory.MappedMemory<T>));
        }
        
        // IMemoryBuffer implementation
        public ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset = 0, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            return _memoryBuffer.CopyFromHostAsync(source, offset, cancellationToken);
        }
        
        public ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset = 0, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            return _memoryBuffer.CopyToHostAsync(destination, offset, cancellationToken);
        }
        
        public void Dispose()
        {
            // Nothing to dispose in this simple implementation
        }
        
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}

/// <summary>
/// Manages memory pool for a specific device.
/// </summary>
public sealed class DeviceMemoryPool : IAsyncDisposable
{
    private readonly IAccelerator _device;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<AbstractionsMemory.IMemoryBuffer> _availableBuffers;
    private readonly object _statisticsLock = new();
    private DeviceMemoryStatistics _statistics;
    private bool _disposed;

    public DeviceMemoryPool(IAccelerator device, ILogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _availableBuffers = new ConcurrentQueue<AbstractionsMemory.IMemoryBuffer>();
        _statistics = new DeviceMemoryStatistics { DeviceId = device.Info.Id };
    }

    public DeviceMemoryStatistics GetStatistics()
    {
        lock (_statisticsLock)
        {
            return new DeviceMemoryStatistics
            {
                DeviceId = _statistics.DeviceId,
                TotalAllocatedBytes = _statistics.TotalAllocatedBytes,
                AvailableBuffers = _statistics.AvailableBuffers,
                PeakUsageBytes = _statistics.PeakUsageBytes,
                AllocationCount = _statistics.AllocationCount,
                DeallocationCount = _statistics.DeallocationCount
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        // Dispose all available buffers
        while (_availableBuffers.TryDequeue(out var buffer))
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
        }

        _disposed = true;
    }
}

/// <summary>
/// Schedules and manages data transfers between devices.
/// </summary>
public sealed class TransferScheduler : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<PendingTransfer> _transferQueue;
    private readonly ConcurrentDictionary<string, List<TaskCompletionSource<bool>>> _deviceTransferCompletions;
    private readonly Task _schedulerTask;
    private readonly CancellationTokenSource _shutdownTokenSource;
    private bool _disposed;

    public TransferScheduler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transferQueue = new ConcurrentQueue<PendingTransfer>();
        _deviceTransferCompletions = new ConcurrentDictionary<string, List<TaskCompletionSource<bool>>>();
        _shutdownTokenSource = new CancellationTokenSource();
        
        _schedulerTask = Task.Run(ProcessTransferQueueAsync);
    }

    public async ValueTask ScheduleTransferAsync<T>(BufferTransfer<T> transfer, CancellationToken cancellationToken) where T : unmanaged
    {
        var pendingTransfer = new PendingTransfer
        {
            Transfer = transfer,
            CompletionSource = new TaskCompletionSource<bool>(),
            ScheduledAt = DateTimeOffset.UtcNow
        };

        _transferQueue.Enqueue(pendingTransfer);
        await pendingTransfer.CompletionSource.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask WaitForDeviceTransfersAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (_deviceTransferCompletions.TryGetValue(deviceId, out var completions))
        {
            var tasks = completions.Select(tcs => tcs.Task).ToArray();
            await Task.WhenAll(tasks).WaitAsync(cancellationToken);
        }
    }

    public int GetPendingTransferCount() => _transferQueue.Count;

    private async Task ProcessTransferQueueAsync()
    {
        while (!_shutdownTokenSource.Token.IsCancellationRequested)
        {
            if (_transferQueue.TryDequeue(out var pendingTransfer))
            {
                try
                {
                    await ExecuteTransferAsync(pendingTransfer);
                    pendingTransfer.CompletionSource.SetResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transfer failed");
                    pendingTransfer.CompletionSource.SetException(ex);
                }
            }
            else
            {
                await Task.Delay(1, _shutdownTokenSource.Token);
            }
        }
    }

    private async Task ExecuteTransferAsync(PendingTransfer pendingTransfer)
    {
        // This is a simplified transfer execution - real implementation would be type-specific
        _logger.LogTrace("Executing buffer transfer");
        await Task.Delay(10, _shutdownTokenSource.Token); // Simulate transfer time
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _shutdownTokenSource.Cancel();
        
        try
        {
            await _schedulerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }

        _shutdownTokenSource.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Manages memory coherence across multiple devices.
/// </summary>
public sealed class MemoryCoherenceManager : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<object, BufferCoherenceInfo> _bufferTracking;
    private bool _disposed;

    public MemoryCoherenceManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bufferTracking = new ConcurrentDictionary<object, BufferCoherenceInfo>();
    }

    public void TrackBuffer<T>(AbstractionsMemory.IBuffer<T> buffer, AbstractionsMemory.IBuffer<T> sourceBuffer, int offset, int count) where T : unmanaged
    {
        var coherenceInfo = new BufferCoherenceInfo
        {
            SourceBuffer = sourceBuffer,
            Offset = offset,
            ElementCount = count,
            LastModified = DateTimeOffset.UtcNow,
            IsCoherent = true
        };

        _bufferTracking[buffer] = coherenceInfo;
    }

    public async ValueTask SynchronizeBufferAsync<T>(AbstractionsMemory.IBuffer<T> buffer, CancellationToken cancellationToken) where T : unmanaged
    {
        if (_bufferTracking.TryGetValue(buffer, out var coherenceInfo))
        {
            if (!coherenceInfo.IsCoherent)
            {
                // Synchronize buffer with source
                _logger.LogDebug("Synchronizing buffer coherence");
                // Implementation would perform actual synchronization
                coherenceInfo.IsCoherent = true;
                coherenceInfo.LastModified = DateTimeOffset.UtcNow;
            }
        }

        await ValueTask.CompletedTask;
    }

    public async ValueTask OptimizePlacementAsync(IAccelerator[] devices, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Optimizing buffer placement across {DeviceCount} devices", devices.Length);
        // Implementation would analyze access patterns and optimize placement
        await Task.CompletedTask;
    }

    public double GetOverheadPercentage()
    {
        var totalBuffers = _bufferTracking.Count;
        var incoherentBuffers = _bufferTracking.Count(kvp => !kvp.Value.IsCoherent);
        
        return totalBuffers > 0 ? (double)incoherentBuffers / totalBuffers * 100 : 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _bufferTracking.Clear();
        _disposed = true;
        await ValueTask.CompletedTask;
    }
}

// Supporting classes and data structures
public class PeerToPeerConnection
{
    public required string Device1Id { get; set; }
    public required string Device2Id { get; set; }
    public required bool IsSupported { get; set; }
    public required bool IsEnabled { get; set; }
    public double BandwidthGBps { get; set; }
}

public class BufferTransfer<T> where T : unmanaged
{
    public required AbstractionsMemory.IBuffer<T> SourceBuffer { get; set; }
    public required AbstractionsMemory.IBuffer<T> TargetBuffer { get; set; }
    public required int SourceOffset { get; set; }
    public required int TargetOffset { get; set; }
    public required int ElementCount { get; set; }
    public TransferPriority Priority { get; set; } = TransferPriority.Normal;
}

public enum TransferPriority
{
    Low,
    Normal,
    High,
    Critical
}

public class PendingTransfer
{
    public required object Transfer { get; set; }
    public required TaskCompletionSource<bool> CompletionSource { get; set; }
    public required DateTimeOffset ScheduledAt { get; set; }
}

public class BufferCoherenceInfo
{
    public required object SourceBuffer { get; set; }
    public required int Offset { get; set; }
    public required int ElementCount { get; set; }
    public required DateTimeOffset LastModified { get; set; }
    public required bool IsCoherent { get; set; }
}

public class MultiGpuMemoryStatistics
{
    public Dictionary<string, DeviceMemoryStatistics> DeviceStatistics { get; set; } = new();
    public int TotalP2PConnections { get; set; }
    public int ActiveP2PConnections { get; set; }
    public int PendingTransfers { get; set; }
    public double CoherenceOverhead { get; set; }
}

public class DeviceMemoryStatistics
{
    public required string DeviceId { get; set; }
    public long TotalAllocatedBytes { get; set; }
    public int AvailableBuffers { get; set; }
    public long PeakUsageBytes { get; set; }
    public long AllocationCount { get; set; }
    public long DeallocationCount { get; set; }
}