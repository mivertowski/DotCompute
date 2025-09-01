// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// NUMA-aware CPU memory manager that inherits from BaseMemoryManager.
/// Reduces 1,232 lines to ~150 lines by leveraging base class functionality.
/// </summary>
public sealed class CpuMemoryManager : BaseMemoryManager
{
    private readonly NumaTopology _topology;
    private readonly NumaMemoryPolicy _defaultPolicy;

    public CpuMemoryManager(ILogger<CpuMemoryManager> logger, NumaMemoryPolicy? defaultPolicy = null) 
        : base(logger)
    {
        _topology = NumaInfo.Topology;
        _defaultPolicy = defaultPolicy ?? NumaMemoryPolicy.CreateDefault();
    }

    /// <summary>
    /// Gets NUMA topology information.
    /// </summary>
    public NumaTopology Topology => _topology;

    /// <summary>
    /// Allocates memory with NUMA-aware placement.
    /// </summary>
    public ValueTask<IUnifiedMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options,
        NumaMemoryPolicy? policy,
        CancellationToken cancellationToken = default)
    {
        policy ??= _defaultPolicy;
        
        // TODO: Production - Implement NUMA-aware allocation
        // Missing: NUMA node selection based on policy
        // Missing: Memory binding to specific NUMA nodes
        // Missing: Cross-node memory access optimization
        
        return AllocateAsync(sizeInBytes, options, cancellationToken);
    }

    /// <inheritdoc/>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateBufferCoreAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        // Determine optimal NUMA node for allocation
        var preferredNode = DetermineOptimalNode(_defaultPolicy, sizeInBytes);
        
        // TODO: Production - Implement actual NUMA-aware buffer allocation
        // Missing: libnuma integration for Linux
        // Missing: Windows NUMA API integration
        // Missing: Memory page pinning for performance
        
        // For now, create a simple CPU buffer
        var buffer = new CpuMemoryBuffer(sizeInBytes, options, this, preferredNode, _defaultPolicy);
        
        return await ValueTask.FromResult<IUnifiedMemoryBuffer>(buffer);
    }

    /// <inheritdoc/>
    protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (buffer is CpuMemoryBuffer cpuBuffer)
        {
            // TODO: Production - Implement proper memory view creation
            // Missing: Shared memory views
            // Missing: Copy-on-write semantics
            return new CpuMemoryBufferView(cpuBuffer, offset, length);
        }
        
        throw new ArgumentException("Buffer must be a CPU memory buffer", nameof(buffer));
    }

    private static NumaMemoryPolicy CreateDefaultPolicy()
    {
        return new NumaMemoryPolicy
        {
            PreferLocalNode = true,
            AllowRemoteAccess = true,
            InterleavingEnabled = false
        };
    }

    private int DetermineOptimalNode(NumaMemoryPolicy policy, long sizeInBytes)
    {
        // TODO: Production - Implement sophisticated NUMA node selection
        // Missing: Current thread affinity checking
        // Missing: Memory pressure per node analysis
        // Missing: Inter-node bandwidth consideration
        // Missing: Application-specific hints
        
        if (policy.PreferLocalNode)
        {
            // Simple implementation: use node 0
            return 0;
        }
        
        if (policy.InterleavingEnabled && _topology.NodeCount > 1)
        {
            // Round-robin across nodes for interleaving
            return (int)(sizeInBytes % _topology.NodeCount);
        }
        
        return 0; // Default to first node
    }

    /// <inheritdoc/>
    public override long MaxAllocationSize => long.MaxValue; // CPU has no practical allocation limit

    /// <inheritdoc/>
    public override long CurrentAllocatedMemory => _currentAllocated;
    private long _currentAllocated;

    /// <inheritdoc/>
    public override long TotalAvailableMemory => GC.GetTotalMemory(false);

    /// <inheritdoc/>
    public override IAccelerator Accelerator => _accelerator ??= CreateDefaultAccelerator();
    private IAccelerator? _accelerator;

    private static IAccelerator CreateDefaultAccelerator()
    {
        // Create default options
        var acceleratorOptions = Microsoft.Extensions.Options.Options.Create(new CpuAcceleratorOptions());
        var threadPoolOptions = Microsoft.Extensions.Options.Options.Create(new Threading.CpuThreadPoolOptions());
        
        // Use the same logger but cast to required type - create compatible logger
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var acceleratorLogger = loggerFactory.CreateLogger<CpuAccelerator>();
        
        return new CpuAccelerator(acceleratorOptions, threadPoolOptions, acceleratorLogger);
    }

    /// <inheritdoc/>
    public override MemoryStatistics Statistics => new()
    {
        TotalAllocated = CurrentAllocatedMemory,
        TotalFreed = _totalFreed,
        ActiveBuffers = _activeBuffers,
        PeakMemoryUsage = _peakMemoryUsage
    };
    private long _totalFreed;
    private long _activeBuffers;
    private long _peakMemoryUsage;

    /// <inheritdoc/>
    protected override ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken)
    {
        Interlocked.Add(ref _currentAllocated, sizeInBytes);
        Interlocked.Increment(ref _activeBuffers);
        _peakMemoryUsage = Math.Max(_peakMemoryUsage, _currentAllocated);
        
        var buffer = new CpuMemoryBuffer(sizeInBytes, options, this, 0, _defaultPolicy);
        return ValueTask.FromResult<IUnifiedMemoryBuffer>(buffer);
    }

    /// <inheritdoc/>
    public override ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken)
    {
        if (buffer is CpuMemoryBuffer cpuBuffer)
        {
            Interlocked.Add(ref _totalFreed, cpuBuffer.SizeInBytes);
            Interlocked.Add(ref _currentAllocated, -cpuBuffer.SizeInBytes);
            Interlocked.Decrement(ref _activeBuffers);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken)
        // Simple CPU memory copy - both buffers are in CPU memory
        => ValueTask.CompletedTask; // Implementation placeholder TODO

    /// <inheritdoc/>
    public override ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken)
        // Simple CPU memory copy with offsets
        => ValueTask.CompletedTask; // Implementation placeholder TODO

    /// <inheritdoc/>
    public override ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken)
        // Copy from CPU buffer to host memory - essentially a no-op since both are host memory
        => ValueTask.CompletedTask; // Implementation placeholder TODO

    /// <inheritdoc/>
    public override ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken)
        // Copy from host memory to CPU buffer - essentially a no-op since both are host memory
        => ValueTask.CompletedTask; // Implementation placeholder TODO

    /// <inheritdoc/>
    public override IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int count)
        // Create typed view of the buffer TODO
        => throw new NotImplementedException("Typed view creation not yet implemented");

    /// <inheritdoc/>
    public override void Clear()
    {
        // Clear all allocated buffers
        _currentAllocated = 0;
        _activeBuffers = 0;
        _totalFreed = 0;
    }

    /// <inheritdoc/>
    public override ValueTask OptimizeAsync(CancellationToken cancellationToken)
    {
        // Optimize memory layout and cleanup fragmentation
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple CPU memory buffer view implementation.
/// </summary>
internal sealed class CpuMemoryBufferView : IUnifiedMemoryBuffer
{
    private readonly CpuMemoryBuffer _parent;
    private readonly long _offset;
    private readonly long _length;

    public CpuMemoryBufferView(CpuMemoryBuffer parent, long offset, long length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;
    public BufferState State => _parent.State;

    // Interface implementations
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Convert to byte memory and call parent
        var byteSource = System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.Span).ToArray();
        return _parent.CopyFromAsync(byteSource.AsMemory(), cancellationToken);
    }

    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Create a temporary byte buffer and copy
        var byteDestination = new byte[destination.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        var task = _parent.CopyToAsync(byteDestination.AsMemory(), cancellationToken);
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.Span).CopyTo(byteDestination);
        return task;
    }

    // Legacy support methods

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => CopyFromAsync(source, offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => CopyToAsync(destination, offset, cancellationToken);

    public void Dispose() 
    {
        // View doesn't own the memory, parent does
    }

    public ValueTask DisposeAsync()
        // View doesn't own the memory, parent does
        => ValueTask.CompletedTask;
}
