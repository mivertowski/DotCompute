// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.DeviceManagement;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Production-grade CUDA Unified Memory manager with managed allocation,
/// prefetching, migration hints, and page fault handling.
/// </summary>
public sealed class CudaUnifiedMemoryManagerProduction : BaseMemoryManager
{
    private readonly CudaContext _context;
    private readonly CudaDeviceManager _deviceManager;
    private readonly ILogger<CudaUnifiedMemoryManagerProduction> _logger;
    private readonly ConcurrentDictionary<IntPtr, UnifiedMemoryInfo> _allocations;
    private readonly ConcurrentDictionary<IntPtr, AccessPattern> _accessPatterns;
    private readonly UnifiedMemoryConfig _config;
    private readonly PerformanceCounter _performanceCounter;
    private bool _unifiedMemorySupported;
    private bool _concurrentAccessSupported;
    private bool _pageFaultHandlingSupported;

    public CudaUnifiedMemoryManagerProduction(
        CudaContext context,
        CudaDeviceManager deviceManager,
        ILogger<CudaUnifiedMemoryManagerProduction> logger,
        UnifiedMemoryConfig? config = null)
        : base(logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _logger = logger;
        _config = config ?? new UnifiedMemoryConfig();
        _allocations = new ConcurrentDictionary<IntPtr, UnifiedMemoryInfo>();
        _accessPatterns = new ConcurrentDictionary<IntPtr, AccessPattern>();
        _performanceCounter = new PerformanceCounter();
        
        InitializeUnifiedMemorySupport();
    }

    /// <summary>
    /// Gets whether unified memory is supported.
    /// </summary>
    public bool UnifiedMemorySupported => _unifiedMemorySupported;

    /// <summary>
    /// Gets whether concurrent access is supported.
    /// </summary>
    public bool ConcurrentAccessSupported => _concurrentAccessSupported;

    /// <summary>
    /// Initializes unified memory support detection.
    /// </summary>
    private void InitializeUnifiedMemorySupport()
    {
        try
        {
            var device = _deviceManager.GetDevice(_context.DeviceId);
            
            _unifiedMemorySupported = device.ManagedMemory;
            _concurrentAccessSupported = device.ConcurrentManagedAccess;
            _pageFaultHandlingSupported = device.PageableMemoryAccess;
            
            _logger.LogInformation(
                "Unified Memory initialized - Supported: {Supported}, Concurrent: {Concurrent}, Page Faults: {PageFaults}",
                _unifiedMemorySupported, _concurrentAccessSupported, _pageFaultHandlingSupported);
            
            if (_unifiedMemorySupported)
            {
                ConfigureUnifiedMemorySettings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize unified memory support");
            _unifiedMemorySupported = false;
        }
    }

    /// <summary>
    /// Configures optimal unified memory settings.
    /// </summary>
    private void ConfigureUnifiedMemorySettings()
    {
        try
        {
            // Set managed memory preferred location if supported
            if (_concurrentAccessSupported && _config.PreferredLocation != null)
            {
                var result = CudaRuntime.cudaDeviceSetMemPool(
                    _config.PreferredLocation.Value);
                
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to set preferred memory location: {Error}", result);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure unified memory settings");
        }
    }

    /// <summary>
    /// Allocates unified memory accessible by both host and device.
    /// </summary>
    public async ValueTask<IMemoryBuffer> AllocateManagedAsync(
        long sizeInBytes,
        ManagedMemoryFlags flags = ManagedMemoryFlags.AttachGlobal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);
        
        if (!_unifiedMemorySupported)
        {
            throw new NotSupportedException("Unified memory is not supported on this device");
        }

        try
        {
            // Allocate managed memory
            var result = CudaRuntime.cudaMallocManaged(
                out IntPtr devicePtr,
                (ulong)sizeInBytes,
                (uint)flags);
            
            CudaRuntime.CheckError(result, $"allocating {sizeInBytes} bytes of managed memory");
            
            // Create allocation info
            var allocationInfo = new UnifiedMemoryInfo
            {
                Pointer = devicePtr,
                Size = sizeInBytes,
                Flags = flags,
                AllocatedAt = DateTimeOffset.UtcNow,
                LastAccessedDevice = _context.DeviceId
            };
            
            _allocations.TryAdd(devicePtr, allocationInfo);
            
            // Apply initial memory advice based on configuration
            if (_config.InitialAdvice != CudaMemoryAdvise.Unset)
            {
                ApplyMemoryAdvice(devicePtr, sizeInBytes, _config.InitialAdvice);
            }
            
            // Prefetch if requested
            if (_config.PrefetchToDevice)
            {
                await PrefetchAsync(devicePtr, sizeInBytes, _context.DeviceId, cancellationToken);
            }
            
            // Create buffer wrapper
            var buffer = new CudaUnifiedMemoryBuffer(
                devicePtr, sizeInBytes, this, flags);
            
            TrackBuffer(buffer, sizeInBytes);
            
            _logger.LogDebug("Allocated {Size} bytes of unified memory with flags {Flags}",
                sizeInBytes, flags);
            
            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {Size} bytes of unified memory", sizeInBytes);
            throw new InvalidOperationException($"Unified memory allocation failed for {sizeInBytes} bytes", ex);
        }
    }

    /// <summary>
    /// Prefetches unified memory to a specific device.
    /// </summary>
    public async ValueTask PrefetchAsync(
        IntPtr devicePtr,
        long sizeInBytes,
        int targetDevice,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_unifiedMemorySupported || !_pageFaultHandlingSupported)
        {
            return; // Prefetching not supported
        }

        await Task.Run(() =>
        {
            var stream = IntPtr.Zero; // Use default stream
            var result = CudaRuntime.cudaMemPrefetchAsync(
                devicePtr,
                (ulong)sizeInBytes,
                targetDevice,
                stream);
            
            if (result == CudaError.Success)
            {
                _logger.LogDebug("Prefetched {Size} bytes to device {Device}",
                    sizeInBytes, targetDevice);
                
                // Update access pattern
                UpdateAccessPattern(devicePtr, targetDevice);
            }
            else if (result != CudaError.NotSupported)
            {
                _logger.LogWarning("Failed to prefetch memory: {Error}", result);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Applies memory advice for optimization.
    /// </summary>
    public void ApplyMemoryAdvice(
        IntPtr devicePtr,
        long sizeInBytes,
        CudaMemoryAdvise advice,
        int targetDevice = -1)
    {
        ThrowIfDisposed();
        
        if (!_unifiedMemorySupported)
            return;

        if (targetDevice < 0)
            targetDevice = _context.DeviceId;

        var result = CudaRuntime.cudaMemAdvise(
            devicePtr,
            (ulong)sizeInBytes,
            advice,
            targetDevice);
        
        if (result == CudaError.Success)
        {
            _logger.LogDebug("Applied memory advice {Advice} for {Size} bytes on device {Device}",
                advice, sizeInBytes, targetDevice);
            
            // Track advice for optimization
            if (_allocations.TryGetValue(devicePtr, out var info))
            {
                info.LastAdvice = advice;
                info.LastAdviceTime = DateTimeOffset.UtcNow;
            }
        }
        else if (result != CudaError.NotSupported)
        {
            _logger.LogWarning("Failed to apply memory advice: {Error}", result);
        }
    }

    /// <summary>
    /// Sets preferred location for unified memory.
    /// </summary>
    public void SetPreferredLocation(
        IntPtr devicePtr,
        long sizeInBytes,
        int preferredDevice)
    {
        ThrowIfDisposed();
        
        ApplyMemoryAdvice(
            devicePtr,
            sizeInBytes,
            CudaMemoryAdvise.SetPreferredLocation,
            preferredDevice);
    }

    /// <summary>
    /// Sets memory access permissions.
    /// </summary>
    public void SetAccessedBy(
        IntPtr devicePtr,
        long sizeInBytes,
        int accessingDevice)
    {
        ThrowIfDisposed();
        
        ApplyMemoryAdvice(
            devicePtr,
            sizeInBytes,
            CudaMemoryAdvise.SetAccessedBy,
            accessingDevice);
    }

    /// <summary>
    /// Optimizes memory placement based on access patterns.
    /// </summary>
    public async ValueTask OptimizeMemoryPlacementAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_unifiedMemorySupported || _allocations.IsEmpty)
            return;

        _logger.LogInformation("Optimizing unified memory placement based on access patterns");
        
        var optimizationTasks = new List<Task>();
        
        foreach (var (ptr, info) in _allocations)
        {
            if (_accessPatterns.TryGetValue(ptr, out var pattern))
            {
                // Determine optimal placement
                var optimalDevice = DetermineOptimalDevice(pattern);
                
                if (optimalDevice != info.LastAccessedDevice)
                {
                    // Prefetch to optimal device
                    optimizationTasks.Add(Task.Run(async () =>
                    {
                        await PrefetchAsync(ptr, info.Size, optimalDevice, cancellationToken);
                        
                        // Apply appropriate advice
                        if (pattern.AccessFrequency > AccessFrequency.Medium)
                        {
                            SetPreferredLocation(ptr, info.Size, optimalDevice);
                        }
                        
                        if (pattern.IsReadMostly)
                        {
                            ApplyMemoryAdvice(ptr, info.Size, CudaMemoryAdvise.SetReadMostly);
                        }
                    }, cancellationToken));
                }
            }
        }
        
        await Task.WhenAll(optimizationTasks);
        
        _logger.LogInformation("Completed unified memory optimization for {Count} allocations",
            optimizationTasks.Count);
    }

    /// <summary>
    /// Migrates memory between devices.
    /// </summary>
    public async ValueTask MigrateMemoryAsync(
        IntPtr devicePtr,
        long sizeInBytes,
        int fromDevice,
        int toDevice,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (fromDevice == toDevice)
            return;
        
        _logger.LogDebug("Migrating {Size} bytes from device {From} to device {To}",
            sizeInBytes, fromDevice, toDevice);
        
        // Ensure accessibility
        if (!_deviceManager.CanAccessPeer(toDevice, fromDevice))
        {
            _deviceManager.EnablePeerAccess(toDevice, fromDevice);
        }
        
        // Prefetch to target device
        await PrefetchAsync(devicePtr, sizeInBytes, toDevice, cancellationToken);
        
        // Update tracking
        if (_allocations.TryGetValue(devicePtr, out var info))
        {
            info.LastAccessedDevice = toDevice;
            info.MigrationCount++;
        }
    }

    /// <summary>
    /// Gets memory residency information.
    /// </summary>
    public MemoryResidency GetMemoryResidency(IntPtr devicePtr, long sizeInBytes)
    {
        if (!_unifiedMemorySupported)
        {
            return new MemoryResidency { IsResident = false };
        }

        try
        {
            // Query memory range attributes
            var result = CudaRuntime.cudaMemRangeGetAttribute(
                out int isResident,
                CudaMemRangeAttribute.PreferredLocation,
                devicePtr,
                (ulong)sizeInBytes);
            
            if (result == CudaError.Success)
            {
                return new MemoryResidency
                {
                    IsResident = isResident != 0,
                    PreferredLocation = isResident
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query memory residency");
        }
        
        return new MemoryResidency { IsResident = false };
    }

    /// <summary>
    /// Updates access pattern tracking.
    /// </summary>
    private void UpdateAccessPattern(IntPtr devicePtr, int accessingDevice)
    {
        var pattern = _accessPatterns.AddOrUpdate(devicePtr,
            _ => new AccessPattern { LastDevice = accessingDevice },
            (_, existing) =>
            {
                existing.AccessCount++;
                existing.LastAccessTime = DateTimeOffset.UtcNow;
                
                if (existing.LastDevice != accessingDevice)
                {
                    existing.DeviceSwitchCount++;
                }
                
                existing.LastDevice = accessingDevice;
                
                // Calculate access frequency
                var timeSinceFirst = DateTimeOffset.UtcNow - existing.FirstAccessTime;
                if (timeSinceFirst.TotalSeconds > 0)
                {
                    var accessesPerSecond = existing.AccessCount / timeSinceFirst.TotalSeconds;
                    existing.AccessFrequency = accessesPerSecond switch
                    {
                        > 1000 => AccessFrequency.VeryHigh,
                        > 100 => AccessFrequency.High,
                        > 10 => AccessFrequency.Medium,
                        > 1 => AccessFrequency.Low,
                        _ => AccessFrequency.VeryLow
                    };
                }
                
                return existing;
            });
        
        _performanceCounter.RecordAccess(accessingDevice);
    }

    /// <summary>
    /// Determines optimal device based on access pattern.
    /// </summary>
    private int DetermineOptimalDevice(AccessPattern pattern)
    {
        // If mostly accessed by one device, prefer that device
        if (pattern.DeviceSwitchCount < pattern.AccessCount / 10)
        {
            return pattern.LastDevice;
        }
        
        // For frequently switching access, prefer CPU for easier sharing
        if (pattern.DeviceSwitchCount > pattern.AccessCount / 2)
        {
            return -1; // CPU
        }
        
        // Default to last accessed device
        return pattern.LastDevice;
    }

    /// <summary>
    /// Gets unified memory statistics.
    /// </summary>
    public UnifiedMemoryStatistics GetUnifiedStatistics()
    {
        var stats = new UnifiedMemoryStatistics
        {
            TotalAllocations = _allocations.Count,
            TotalBytesAllocated = _allocations.Values.Sum(a => a.Size),
            TotalMigrations = _allocations.Values.Sum(a => a.MigrationCount),
            AccessPatternsCached = _accessPatterns.Count,
            SupportsUnifiedMemory = _unifiedMemorySupported,
            SupportsConcurrentAccess = _concurrentAccessSupported,
            SupportsPageFaults = _pageFaultHandlingSupported
        };
        
        if (_performanceCounter != null)
        {
            stats.TotalAccessCount = _performanceCounter.TotalAccesses;
            stats.AverageAccessLatency = _performanceCounter.AverageLatency;
        }
        
        return stats;
    }

    /// <summary>
    /// Frees unified memory.
    /// </summary>
    public void FreeUnifiedMemory(IntPtr devicePtr)
    {
        ThrowIfDisposed();
        
        if (devicePtr == IntPtr.Zero)
            return;

        try
        {
            var result = CudaRuntime.cudaFree(devicePtr);
            
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to free unified memory: {Error}", result);
            }
            
            _allocations.TryRemove(devicePtr, out _);
            _accessPatterns.TryRemove(devicePtr, out _);
            
            _logger.LogDebug("Freed unified memory at {Ptr}", devicePtr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error freeing unified memory");
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<IMemoryBuffer> AllocateBufferCoreAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        // Default to managed allocation for unified memory manager
        return await AllocateManagedAsync(
            sizeInBytes,
            ManagedMemoryFlags.AttachGlobal,
            cancellationToken);
    }

    /// <inheritdoc/>
    protected override IMemoryBuffer CreateViewCore(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is CudaUnifiedMemoryBuffer unifiedBuffer)
        {
            return new CudaUnifiedMemoryBufferView(unifiedBuffer, offset, length);
        }
        
        throw new ArgumentException("Buffer must be a unified memory buffer", nameof(buffer));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free all unified allocations
            foreach (var allocation in _allocations.Values)
            {
                try
                {
                    FreeUnifiedMemory(allocation.Pointer);
                }
                catch { /* Best effort */ }
            }
            
            _allocations.Clear();
            _accessPatterns.Clear();
        }
        
        base.Dispose(disposing);
    }

    /// <summary>
    /// Unified memory allocation information.
    /// </summary>
    private sealed class UnifiedMemoryInfo
    {
        public IntPtr Pointer { get; init; }
        public long Size { get; init; }
        public ManagedMemoryFlags Flags { get; init; }
        public DateTimeOffset AllocatedAt { get; init; }
        public int LastAccessedDevice { get; set; }
        public int MigrationCount { get; set; }
        public CudaMemoryAdvise LastAdvice { get; set; }
        public DateTimeOffset LastAdviceTime { get; set; }
    }

    /// <summary>
    /// Memory access pattern tracking.
    /// </summary>
    private sealed class AccessPattern
    {
        public int LastDevice { get; set; }
        public int AccessCount { get; set; } = 1;
        public int DeviceSwitchCount { get; set; }
        public DateTimeOffset FirstAccessTime { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastAccessTime { get; set; } = DateTimeOffset.UtcNow;
        public AccessFrequency AccessFrequency { get; set; } = AccessFrequency.Low;
        public bool IsReadMostly { get; set; }
    }

    /// <summary>
    /// Performance counter for access tracking.
    /// </summary>
    private sealed class PerformanceCounter
    {
        private long _totalAccesses;
        private double _totalLatency;
        
        public long TotalAccesses => _totalAccesses;
        public double AverageLatency => _totalAccesses > 0 ? _totalLatency / _totalAccesses : 0;
        
        public void RecordAccess(int device, double latency = 0)
        {
            Interlocked.Increment(ref _totalAccesses);
            if (latency > 0)
            {
                Interlocked.Exchange(ref _totalLatency, _totalLatency + latency);
            }
        }
    }
}

/// <summary>
/// Unified memory configuration options.
/// </summary>
public sealed class UnifiedMemoryConfig
{
    public bool PrefetchToDevice { get; init; } = true;
    public int? PreferredLocation { get; init; }
    public CudaMemoryAdvise InitialAdvice { get; init; } = CudaMemoryAdvise.Unset;
    public bool EnableAccessPatternTracking { get; init; } = true;
    public bool AutoOptimizePlacement { get; init; } = true;
}

/// <summary>
/// Managed memory allocation flags.
/// </summary>
[Flags]
public enum ManagedMemoryFlags : uint
{
    AttachGlobal = 0x01,
    AttachHost = 0x02,
    AttachSingle = 0x04,
    AttachMempool = 0x08
}

/// <summary>
/// Access frequency levels.
/// </summary>
public enum AccessFrequency
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// Memory residency information.
/// </summary>
public sealed class MemoryResidency
{
    public bool IsResident { get; init; }
    public int PreferredLocation { get; init; }
}

/// <summary>
/// Unified memory statistics.
/// </summary>
public sealed class UnifiedMemoryStatistics
{
    public int TotalAllocations { get; init; }
    public long TotalBytesAllocated { get; init; }
    public int TotalMigrations { get; init; }
    public int AccessPatternsCached { get; init; }
    public long TotalAccessCount { get; init; }
    public double AverageAccessLatency { get; init; }
    public bool SupportsUnifiedMemory { get; init; }
    public bool SupportsConcurrentAccess { get; init; }
    public bool SupportsPageFaults { get; init; }
}

/// <summary>
/// CUDA unified memory buffer implementation.
/// </summary>
internal sealed class CudaUnifiedMemoryBuffer : IMemoryBuffer
{
    private readonly IntPtr _devicePtr;
    private readonly long _sizeInBytes;
    private readonly CudaUnifiedMemoryManagerProduction _manager;
    private readonly ManagedMemoryFlags _flags;
    private bool _disposed;

    public CudaUnifiedMemoryBuffer(
        IntPtr devicePtr,
        long sizeInBytes,
        CudaUnifiedMemoryManagerProduction manager,
        ManagedMemoryFlags flags)
    {
        _devicePtr = devicePtr;
        _sizeInBytes = sizeInBytes;
        _manager = manager;
        _flags = flags;
    }

    public IntPtr DevicePointer => _devicePtr;
    public long SizeInBytes => _sizeInBytes;
    public MemoryOptions Options => MemoryOptions.AutoMigrate;
    public bool IsDisposed => _disposed;

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        // Direct memory copy for unified memory
        var destPtr = _devicePtr + (nint)offset;
        var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
        
        using var pinned = source.Pin();
        unsafe
        {
            Buffer.MemoryCopy(
                pinned.Pointer,
                destPtr.ToPointer(),
                _sizeInBytes - offset,
                sizeInBytes);
        }
        
        await Task.CompletedTask;
    }

    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        // Direct memory copy for unified memory
        var srcPtr = _devicePtr + (nint)offset;
        var sizeInBytes = destination.Length * Unsafe.SizeOf<T>();
        
        using var pinned = destination.Pin();
        unsafe
        {
            Buffer.MemoryCopy(
                srcPtr.ToPointer(),
                pinned.Pointer,
                sizeInBytes,
                sizeInBytes);
        }
        
        await Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _manager.FreeUnifiedMemory(_devicePtr);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await Task.Run(() => _manager.FreeUnifiedMemory(_devicePtr));
        }
    }
}

/// <summary>
/// View over a unified memory buffer.
/// </summary>
internal sealed class CudaUnifiedMemoryBufferView : IMemoryBuffer
{
    private readonly CudaUnifiedMemoryBuffer _parent;
    private readonly long _offset;
    private readonly long _length;

    public CudaUnifiedMemoryBufferView(CudaUnifiedMemoryBuffer parent, long offset, long length)
    {
        _parent = parent;
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public void Dispose() { /* View doesn't own memory */ }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}