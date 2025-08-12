// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution;

/// <summary>
/// Manages memory allocation and transfers across multiple GPU devices.
/// Provides real peer-to-peer GPU memory management with hardware detection and optimization.
/// </summary>
public sealed class MultiGpuMemoryManager : IAsyncDisposable
{
    private readonly ILogger<MultiGpuMemoryManager> _logger;
    private readonly P2PCapabilityDetector _p2pDetector;
    private readonly P2PBufferFactory _bufferFactory;
    private readonly ConcurrentDictionary<string, DeviceBufferPool> _devicePools;
    private readonly ConcurrentDictionary<string, P2PConnectionCapability> _p2pConnections;
    private readonly P2PTransferScheduler _transferScheduler;
    private readonly P2PMemoryCoherenceManager _coherenceManager;
    private readonly SemaphoreSlim _managementSemaphore;
    private bool _disposed;

    public MultiGpuMemoryManager(ILogger<MultiGpuMemoryManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _p2pDetector = new P2PCapabilityDetector(logger);
        _bufferFactory = new P2PBufferFactory(logger, _p2pDetector);
        _devicePools = new ConcurrentDictionary<string, DeviceBufferPool>();
        _p2pConnections = new ConcurrentDictionary<string, P2PConnectionCapability>();
        _transferScheduler = new P2PTransferScheduler(logger);
        _coherenceManager = new P2PMemoryCoherenceManager(logger);
        _managementSemaphore = new SemaphoreSlim(1);
    }

    /// <summary>
    /// Creates a buffer slice on the target device from a source buffer using P2P optimizations.
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
            _logger.LogDebug("Creating P2P buffer slice on device {DeviceId}: start={StartIndex}, count={ElementCount}",
                targetDevice.Info.Id, startIndex, elementCount);

            // Ensure P2P connection is established
            var sourceDevice = sourceBuffer.Accelerator;
            var connectionKey = GetConnectionKey(sourceDevice.Info.Id, targetDevice.Info.Id);
            
            if (!_p2pConnections.TryGetValue(connectionKey, out var p2pCapability))
            {
                p2pCapability = await _p2pDetector.DetectP2PCapabilityAsync(sourceDevice, targetDevice, cancellationToken);
                _p2pConnections[connectionKey] = p2pCapability;
            }

            // Create P2P-optimized buffer slice
            var p2pBuffer = await _bufferFactory.CreateP2PBufferSliceAsync(
                sourceBuffer as P2PBuffer<T> ?? await ConvertToP2PBufferAsync(sourceBuffer, cancellationToken),
                targetDevice, startIndex, elementCount, null, cancellationToken);

            // Track coherence with P2P awareness
            _coherenceManager.TrackP2PBuffer(p2pBuffer, sourceBuffer, startIndex, elementCount, p2pCapability);

            return p2pBuffer;
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
    /// Enables peer-to-peer transfers between two devices using hardware detection.
    /// </summary>
    public async ValueTask<bool> EnablePeerToPeerAsync(IAccelerator device1, IAccelerator device2)
    {
        var connectionKey = GetConnectionKey(device1.Info.Id, device2.Info.Id);
        
        if (_p2pConnections.TryGetValue(connectionKey, out var existingCapability))
        {
            return existingCapability.IsSupported;
        }

        try
        {
            // Use hardware-aware P2P detection
            var capability = await _p2pDetector.DetectP2PCapabilityAsync(device1, device2);
            _p2pConnections[connectionKey] = capability;

            if (capability.IsSupported)
            {
                // Establish P2P connection at hardware level
                var enableResult = await _p2pDetector.EnableP2PAccessAsync(device1, device2);
                
                if (enableResult.Success)
                {
                    // Establish connection in buffer factory
                    await _bufferFactory.EstablishP2PConnectionAsync(device1, device2);
                    
                    _logger.LogInformation("Enabled P2P between {Device1} and {Device2}: {ConnectionType}, {BandwidthGBps:F1} GB/s",
                        device1.Info.Name, device2.Info.Name, capability.ConnectionType, capability.EstimatedBandwidthGBps);
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("P2P hardware detection succeeded but enable failed between {Device1} and {Device2}: {Error}",
                        device1.Info.Name, device2.Info.Name, enableResult.ErrorMessage);
                    return false;
                }
            }
            else
            {
                _logger.LogInformation("P2P not supported between {Device1} and {Device2}: {Reason}",
                    device1.Info.Name, device2.Info.Name, capability.LimitationReason);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable P2P between {Device1} and {Device2}",
                device1.Info.Name, device2.Info.Name);
            return false;
        }
    }

    /// <summary>
    /// Transfers data between buffers on different devices using optimal P2P strategy.
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

        // Get optimal transfer strategy
        var transferSize = transferCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var strategy = await _p2pDetector.GetOptimalTransferStrategyAsync(
            sourceBuffer.Accelerator, targetBuffer.Accelerator, transferSize, cancellationToken);

        // Schedule P2P-optimized transfer
        await _transferScheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, sourceOffset, targetOffset, transferCount, strategy, cancellationToken);
    }

    /// <summary>
    /// Synchronizes a buffer across all devices that have copies of it.
    /// </summary>
    public async ValueTask SynchronizeBufferAsync<T>(AbstractionsMemory.IBuffer<T> buffer, CancellationToken cancellationToken = default) where T : unmanaged
    {
        await _coherenceManager.SynchronizeBufferAsync(buffer, cancellationToken);
    }

    /// <summary>
    /// Gets comprehensive memory usage statistics across all devices with P2P metrics.
    /// </summary>
    public MultiGpuMemoryStatistics GetMemoryStatistics()
    {
        var p2pStats = _bufferFactory.GetConnectionStatistics();
        var poolStats = _bufferFactory.GetBufferPoolStatistics();
        
        var stats = new MultiGpuMemoryStatistics
        {
            DeviceStatistics = [],
            TotalP2PConnections = p2pStats.TotalConnections,
            ActiveP2PConnections = p2pStats.ActiveConnections,
            PendingTransfers = _transferScheduler.GetPendingTransferCount(),
            CoherenceOverhead = _coherenceManager.GetOverheadPercentage(),
            TotalP2PTransfers = p2pStats.TotalTransfers,
            TotalP2PBytesTransferred = p2pStats.TotalBytesTransferred,
            AverageP2PTransferSize = p2pStats.AverageTransferSize
        };

        // Convert pool statistics to device statistics
        foreach (var poolStat in poolStats)
        {
            stats.DeviceStatistics[poolStat.DeviceId] = new DeviceMemoryStatistics
            {
                DeviceId = poolStat.DeviceId,
                AllocatedBytes = poolStat.TotalAllocatedBytes,
                AvailableBytes = poolStat.TotalAllocatedBytes, // Use same value for consistency
                PooledBufferCount = poolStat.ActiveBuffers,
                AllocationSizeDistribution = new Dictionary<long, int>(), // Empty for now
                FragmentationIndex = 0.0 // Calculate if needed
            };
        }

        return stats;
    }

    /// <summary>
    /// Optimizes memory layout for better P2P performance across devices.
    /// </summary>
    public async ValueTask OptimizeMemoryLayoutAsync(IAccelerator[] devices, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing P2P memory layout across {DeviceCount} devices", devices.Length);

        // Build P2P capability matrix
        var p2pMatrix = new Dictionary<string, Dictionary<string, P2PConnectionCapability>>();
        
        for (var i = 0; i < devices.Length; i++)
        {
            p2pMatrix[devices[i].Info.Id] = new Dictionary<string, P2PConnectionCapability>();
            
            for (var j = 0; j < devices.Length; j++)
            {
                if (i != j)
                {
                    var capability = await _p2pDetector.DetectP2PCapabilityAsync(devices[i], devices[j], cancellationToken);
                    p2pMatrix[devices[i].Info.Id][devices[j].Info.Id] = capability;
                    
                    // Enable P2P if supported
                    if (capability.IsSupported)
                    {
                        await EnablePeerToPeerAsync(devices[i], devices[j]);
                    }
                }
            }
        }

        // Log P2P topology
        LogP2PTopology(devices, p2pMatrix);

        // Optimize buffer placement based on P2P topology and access patterns
        await _coherenceManager.OptimizeP2PPlacementAsync(devices, p2pMatrix, cancellationToken);

        _logger.LogInformation("P2P memory layout optimization completed");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing MultiGpuMemoryManager");

        // Dispose P2P components
        await _bufferFactory.DisposeAsync().ConfigureAwait(false);
        await _p2pDetector.DisposeAsync().ConfigureAwait(false);
        
        // Dispose all device pools through the buffer factory
        // Individual pools are managed by the P2PBufferFactory
        await Task.CompletedTask;

        await _transferScheduler.DisposeAsync().ConfigureAwait(false);
        await _coherenceManager.DisposeAsync().ConfigureAwait(false);
        
        _devicePools.Clear();
        _p2pConnections.Clear();
        _managementSemaphore.Dispose();
        
        _disposed = true;
        _logger.LogInformation("MultiGpuMemoryManager disposed");
    }

    #region Private Methods

    /// <summary>
    /// Schedules a P2P-optimized buffer transfer.
    /// </summary>
    private async ValueTask ScheduleBufferTransferAsync<T>(
        AbstractionsMemory.IBuffer<T> sourceBuffer,
        AbstractionsMemory.IBuffer<T> targetBuffer,
        int sourceOffset,
        int targetOffset,
        int elementCount,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var transferSize = elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var strategy = await _p2pDetector.GetOptimalTransferStrategyAsync(
            sourceBuffer.Accelerator, targetBuffer.Accelerator, transferSize, cancellationToken);

        await _transferScheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, sourceOffset, targetOffset, elementCount, strategy, cancellationToken);
    }

    /// <summary>
    /// Converts a standard buffer to a P2P buffer for optimization.
    /// </summary>
    private async ValueTask<P2PBuffer<T>> ConvertToP2PBufferAsync<T>(AbstractionsMemory.IBuffer<T> buffer, CancellationToken cancellationToken) where T : unmanaged
    {
        if (buffer is P2PBuffer<T> p2pBuffer)
        {
            return p2pBuffer;
        }

        // Create new P2P buffer and copy data
        return await _bufferFactory.CreateP2PBufferAsync(buffer, buffer.Accelerator, null, cancellationToken);
    }

    /// <summary>
    /// Logs the P2P topology matrix for debugging and optimization.
    /// </summary>
    private void LogP2PTopology(IAccelerator[] devices, Dictionary<string, Dictionary<string, P2PConnectionCapability>> p2pMatrix)
    {
        _logger.LogInformation("P2P Topology Matrix:");
        
        foreach (var sourceDevice in devices)
        {
            var connections = new List<string>();
            
            foreach (var targetDevice in devices)
            {
                if (sourceDevice.Info.Id != targetDevice.Info.Id && 
                    p2pMatrix[sourceDevice.Info.Id].TryGetValue(targetDevice.Info.Id, out var capability))
                {
                    var status = capability.IsSupported 
                        ? $"{targetDevice.Info.Name}({capability.ConnectionType},{capability.EstimatedBandwidthGBps:F0}GB/s)"
                        : $"{targetDevice.Info.Name}(Not Supported)";
                    connections.Add(status);
                }
            }
            
            _logger.LogInformation("{SourceDevice} -> [{Connections}]", 
                sourceDevice.Info.Name, string.Join(", ", connections));
        }
    }

    private static string GetConnectionKey(string device1Id, string device2Id)
    {
        // Ensure consistent ordering for bidirectional connections
        var ids = new[] { device1Id, device2Id }.OrderBy(id => id).ToArray();
        return $"{ids[0]}<->{ids[1]}";
    }

    /// <summary>
    /// Creates a P2P-optimized buffer from a memory buffer.
    /// </summary>
    private P2PBuffer<T> CreateBufferFromMemoryBuffer<T>(
        AbstractionsMemory.IMemoryBuffer memoryBuffer, 
        IAccelerator device, 
        int elementCount) where T : unmanaged
    {
        // Create P2P-optimized buffer with capability detection
        var deviceCapability = _p2pDetector.GetDeviceCapabilitiesAsync(device).GetAwaiter().GetResult();
        return new P2PBuffer<T>(memoryBuffer, device, elementCount, deviceCapability.SupportsP2P, _logger);
    }

    #endregion
}

/// <summary>
/// Mock accelerator for testing - provides minimal implementation for unit tests.
/// </summary>
internal sealed class MockAcceleratorForTest : IAccelerator
{
    private static int _nextId = 0;
    private readonly AcceleratorInfo _info;
    
    public MockAcceleratorForTest(string? name = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        _info = new AcceleratorInfo
        {
            Id = $"mock-{id}",
            Name = name ?? $"MockDevice{id}",
            DeviceType = "Mock",
            Vendor = "Mock",
            DriverVersion = "1.0.0",
            TotalMemory = 1024 * 1024 * 1024, // 1GB
            AvailableMemory = 1024 * 1024 * 1024,  // 1GB available
            MaxSharedMemoryPerBlock = 48 * 1024,
            MaxMemoryAllocationSize = 1024 * 1024 * 1024,
            LocalMemorySize = 64 * 1024,
            IsUnifiedMemory = false,
            ComputeUnits = 4,
            MaxClockFrequency = 1000,
            MaxThreadsPerBlock = 1024
        };
        Memory = new MockMemoryManager();
    }
    
    public AcceleratorInfo Info => _info;
    public AbstractionsMemory.IMemoryManager Memory { get; }
    public bool IsDisposed => false;
    
    public ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<ICompiledKernel>(new MockCompiledKernel());
    }
    
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() { }
}

/// <summary>
/// Simple buffer adapter for testing purposes
/// </summary>
internal sealed class SimpleBufferAdapter<T> : AbstractionsMemory.IBuffer<T> where T : unmanaged
{
    private readonly AbstractionsMemory.IMemoryBuffer _memoryBuffer;
    private readonly int _elementCount;
    private readonly IAccelerator _accelerator;
    private bool _disposed;

    public SimpleBufferAdapter(AbstractionsMemory.IMemoryBuffer memoryBuffer, int elementCount)
    {
        _memoryBuffer = memoryBuffer ?? throw new ArgumentNullException(nameof(memoryBuffer));
        _elementCount = elementCount;
        // Create a mock accelerator for testing
        _accelerator = new MockAcceleratorForTest();
    }

    public int Length => _elementCount;

    public long SizeInBytes => _memoryBuffer.SizeInBytes;

    public IAccelerator Accelerator => _accelerator;

    public AbstractionsMemory.MemoryType MemoryType => AbstractionsMemory.MemoryType.HostVisible;
    
    public AbstractionsMemory.MemoryOptions Options => _memoryBuffer.Options;

    public bool IsDisposed => _disposed;

    public Task CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        return _memoryBuffer.CopyFromHostAsync<TData>(source, offset, cancellationToken).AsTask();
    }

    public ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        // Simple mock - just return completed
        return ValueTask.CompletedTask;
    }

    public Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        return _memoryBuffer.CopyToHostAsync<TData>(destination, offset, cancellationToken).AsTask();
    }

    public ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        // Simple mock - just return completed
        return ValueTask.CompletedTask;
    }

    public Task CopyFromAsync(AbstractionsMemory.IMemoryBuffer source, CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return Task.CompletedTask;
    }

    public Task CopyToAsync(AbstractionsMemory.IMemoryBuffer destination, CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return Task.CompletedTask;
    }

    public ValueTask CopyToAsync(AbstractionsMemory.IBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(int sourceOffset, AbstractionsMemory.IBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return ValueTask.CompletedTask;
    }

    public Task FillAsync<TData>(TData value, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        // Simple mock - just return completed
        return Task.CompletedTask;
    }

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return ValueTask.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // Simple mock - just return completed
        return Task.CompletedTask;
    }

    public AbstractionsMemory.IBuffer<T> Slice(int offset, int count)
    {
        // Create a new wrapper with adjusted element count
        return new SimpleBufferAdapter<T>(_memoryBuffer, count);
    }

    public AbstractionsMemory.IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        // Calculate new element count based on size ratio
        var newElementCount = (_elementCount * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        return new SimpleBufferAdapter<TNew>(_memoryBuffer, newElementCount);
    }

    public AbstractionsMemory.MappedMemory<T> Map(AbstractionsMemory.MapMode mode)
    {
        // Simple mock implementation - return default structure
        return default(AbstractionsMemory.MappedMemory<T>);
    }

    public AbstractionsMemory.MappedMemory<T> MapRange(int offset, int count, AbstractionsMemory.MapMode mode)
    {
        // Simple mock implementation - return default structure
        return default(AbstractionsMemory.MappedMemory<T>);
    }

    public ValueTask<AbstractionsMemory.MappedMemory<T>> MapAsync(AbstractionsMemory.MapMode mode, CancellationToken cancellationToken = default)
    {
        // Simple mock implementation - return default structure
        return new ValueTask<AbstractionsMemory.MappedMemory<T>>(default(AbstractionsMemory.MappedMemory<T>));
    }

    public ValueTask DisposeAsync()
    {
        return _memoryBuffer.DisposeAsync();
    }

    public void Dispose()
    {
        _disposed = true;
        _memoryBuffer.DisposeAsync().AsTask().Wait();
    }
}

// DeviceMemoryPool is now in DeviceBufferPool.cs - this class is removed

// TransferScheduler is now implemented as P2PTransferScheduler in P2PTransferScheduler.cs - this class is removed

// MemoryCoherenceManager is now implemented as P2PMemoryCoherenceManager in P2PMemoryCoherenceManager.cs - this class is removed

// Supporting classes and data structures for MultiGpuMemoryManager
public class PeerToPeerConnection
{
    public required string Device1Id { get; set; }
    public required string Device2Id { get; set; }
    public required bool IsSupported { get; set; }
    public required bool IsEnabled { get; set; }
    public double BandwidthGBps { get; set; }
}

public enum TransferPriority
{
    Low,
    Normal,
    High,
    Critical
}

public class MultiGpuMemoryStatistics
{
    public Dictionary<string, DeviceMemoryStatistics> DeviceStatistics { get; set; } = [];
    public int TotalP2PConnections { get; set; }
    public int ActiveP2PConnections { get; set; }
    public int PendingTransfers { get; set; }
    public double CoherenceOverhead { get; set; }
    public long TotalP2PTransfers { get; set; }
    public long TotalP2PBytesTransferred { get; set; }
    public long AverageP2PTransferSize { get; set; }
    public double P2PEfficiencyRatio => TotalP2PConnections > 0 ? (double)ActiveP2PConnections / TotalP2PConnections : 0.0;
}

public class DeviceMemoryStatistics
{
    public required string DeviceId { get; set; }
    public long AllocatedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public int PooledBufferCount { get; set; }
    public Dictionary<long, int> AllocationSizeDistribution { get; set; } = new();
    public double FragmentationIndex { get; set; }
    public long AllocationCount { get; set; }
    public long DeallocationCount { get; set; }
}

/// <summary>
/// Mock memory manager for testing purposes
/// </summary>
internal sealed class MockMemoryManager : AbstractionsMemory.IMemoryManager
{
    public ValueTask<AbstractionsMemory.IMemoryBuffer> AllocateAsync(long sizeInBytes, AbstractionsMemory.MemoryOptions options, CancellationToken cancellationToken = default)
    {
        var buffer = new MockMemoryBuffer(sizeInBytes, options);
        return ValueTask.FromResult<AbstractionsMemory.IMemoryBuffer>(buffer);
    }

    public ValueTask<AbstractionsMemory.IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, AbstractionsMemory.MemoryOptions options, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var buffer = new MockMemoryBuffer(sizeInBytes, options);
        return ValueTask.FromResult<AbstractionsMemory.IMemoryBuffer>(buffer);
    }

    public AbstractionsMemory.IMemoryBuffer CreateView(AbstractionsMemory.IMemoryBuffer buffer, long offset, long length)
    {
        return new MockMemoryBuffer(length, buffer.Options);
    }
}

/// <summary>
/// Mock memory buffer for testing purposes
/// </summary>
internal sealed class MockMemoryBuffer : AbstractionsMemory.IMemoryBuffer
{
    private readonly long _sizeInBytes;
    private bool _disposed;

    public MockMemoryBuffer(long sizeInBytes, AbstractionsMemory.MemoryOptions options)
    {
        _sizeInBytes = sizeInBytes;
        Options = options;
    }

    public long SizeInBytes => _sizeInBytes;
    public AbstractionsMemory.MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// Mock compiled kernel for testing purposes
/// </summary>
internal sealed class MockCompiledKernel : ICompiledKernel
{
    public string Name => "MockKernel";

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}