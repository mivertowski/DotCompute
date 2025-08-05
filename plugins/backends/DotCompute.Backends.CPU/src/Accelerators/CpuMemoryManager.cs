// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Threading;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// NUMA-aware memory manager for CPU-based compute with advanced allocation strategies.
/// </summary>
public sealed class CpuMemoryManager : IMemoryManager, IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<WeakReference<CpuMemoryBuffer>> _buffers = [];
    private readonly NumaTopology _topology;
    private readonly NumaMemoryPolicy _defaultPolicy;
    private long _totalAllocated;
    private int _disposed;

    public CpuMemoryManager(NumaMemoryPolicy? defaultPolicy = null)
    {
        _topology = NumaInfo.Topology;
        _defaultPolicy = defaultPolicy ?? CreateDefaultPolicy();
    }

    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocated);

    /// <summary>
    /// Gets NUMA topology information.
    /// </summary>
    public NumaTopology Topology => _topology;

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

#pragma warning disable CA2000 // Dispose objects before losing scope - buffer ownership is transferred to caller
        var buffer = new CpuMemoryBuffer(sizeInBytes, options, this);
#pragma warning restore CA2000

        lock (_lock)
        {
            _buffers.Add(new WeakReference<CpuMemoryBuffer>(buffer));
        }

        Interlocked.Add(ref _totalAllocated, sizeInBytes);

        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <summary>
    /// Allocates memory with NUMA-aware placement.
    /// </summary>
    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        NumaMemoryPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        policy ??= _defaultPolicy;
        var preferredNode = DetermineOptimalNode(policy, sizeInBytes);

#pragma warning disable CA2000 // Dispose objects before losing scope - buffer ownership is transferred to caller
        var buffer = new CpuMemoryBuffer(sizeInBytes, options, this, preferredNode, policy);
#pragma warning restore CA2000

        lock (_lock)
        {
            _buffers.Add(new WeakReference<CpuMemoryBuffer>(buffer));
        }

        Interlocked.Add(ref _totalAllocated, sizeInBytes);

        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * Marshal.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);

        await buffer.CopyFromHostAsync(source, 0, cancellationToken).ConfigureAwait(false);

        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer is not CpuMemoryBuffer cpuBuffer)
        {
            throw new ArgumentException("Buffer must be a CPU memory buffer", nameof(buffer));
        }

        return cpuBuffer.CreateView(offset, length);
    }

    internal void OnBufferDisposed(long sizeInBytes) => Interlocked.Add(ref _totalAllocated, -sizeInBytes);

    /// <summary>
    /// Creates a memory policy for the current thread's CPU affinity.
    /// </summary>
    public NumaMemoryPolicy CreatePolicyForCurrentThread()
    {
        var currentCpu = GetCurrentProcessorNumber();
        var preferredNode = _topology.GetNodeForProcessor(currentCpu);

        return new NumaMemoryPolicy
        {
            Strategy = NumaAllocationStrategy.LocalPreferred,
            PreferredNodes = new[] { preferredNode },
            FallbackStrategy = NumaFallbackStrategy.ClosestFirst
        };
    }

    /// <summary>
    /// Creates a memory policy for specific processor affinity.
    /// </summary>
    public NumaMemoryPolicy CreatePolicyForProcessors(IEnumerable<int> processorIds)
    {
        var processors = processorIds.ToArray();
        var optimalNode = _topology.GetOptimalNodeForProcessors(processors);

        return new NumaMemoryPolicy
        {
            Strategy = NumaAllocationStrategy.LocalPreferred,
            PreferredNodes = new[] { optimalNode },
            FallbackStrategy = NumaFallbackStrategy.ClosestFirst
        };
    }

    /// <summary>
    /// Gets memory usage statistics per NUMA node.
    /// </summary>
    public NumaMemoryStatistics GetNumaStatistics()
    {
        var nodeStats = new Dictionary<int, NumaNodeStatistics>();

        lock (_lock)
        {
            foreach (var weakRef in _buffers)
            {
                if (weakRef.TryGetTarget(out var buffer))
                {
                    var nodeId = buffer.PreferredNode;
                    if (!nodeStats.TryGetValue(nodeId, out var stats))
                    {
                        stats = new NumaNodeStatistics { NodeId = nodeId };
                        nodeStats[nodeId] = stats;
                    }

                    stats.AllocatedBytes += buffer.SizeInBytes;
                    stats.BufferCount++;
                }
            }
        }

        return new NumaMemoryStatistics
        {
            TotalAllocatedBytes = TotalAllocatedBytes,
            NodeStatistics = [.. nodeStats.Values]
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_lock)
        {
            // Force dispose all remaining buffers
            foreach (var weakRef in _buffers)
            {
                if (weakRef.TryGetTarget(out var buffer))
                {
                    buffer.Dispose();
                }
            }
            _buffers.Clear();
        }
    }

    internal NumaMemoryPolicy CreateDefaultPolicy()
    {
        return new NumaMemoryPolicy
        {
            Strategy = _topology.NodeCount > 1 ? NumaAllocationStrategy.LocalPreferred : NumaAllocationStrategy.FirstFit,
            PreferredNodes = [],
            FallbackStrategy = NumaFallbackStrategy.ClosestFirst
        };
    }

    private int DetermineOptimalNode(NumaMemoryPolicy policy, long sizeInBytes)
    {
        return policy.Strategy switch
        {
            NumaAllocationStrategy.LocalPreferred => GetLocalPreferredNode(policy),
            NumaAllocationStrategy.FirstFit => GetFirstFitNode(sizeInBytes),
            NumaAllocationStrategy.BestFit => GetBestFitNode(sizeInBytes),
            NumaAllocationStrategy.Interleaved => GetInterleavedNode(policy),
            NumaAllocationStrategy.Explicit => GetExplicitNode(policy),
            _ => 0
        };
    }

    private int GetLocalPreferredNode(NumaMemoryPolicy policy)
    {
        if (policy.PreferredNodes.Length > 0)
        {
            return policy.PreferredNodes[0];
        }

        var currentCpu = GetCurrentProcessorNumber();
        return _topology.GetNodeForProcessor(currentCpu);
    }

    private static int GetFirstFitNode(long sizeInBytes) => 0; // Simple first-fit allocation

    private int GetBestFitNode(long sizeInBytes) => _topology.GetNodesByAvailableMemory().FirstOrDefault(); // Select node with most available memory

    private int GetInterleavedNode(NumaMemoryPolicy policy)
    {
        // Round-robin across specified nodes or all nodes
        var nodes = policy.PreferredNodes.Length > 0 ? policy.PreferredNodes : Enumerable.Range(0, _topology.NodeCount);
        var nodeArray = nodes.ToArray();

        // Use thread ID for deterministic but distributed allocation
        var threadId = Environment.CurrentManagedThreadId;
        return nodeArray[threadId % nodeArray.Length];
    }

    private static int GetExplicitNode(NumaMemoryPolicy policy) => policy.PreferredNodes.FirstOrDefault();

    private static int GetCurrentProcessorNumber()
    {
        // Platform-specific implementation to get current processor
        if (OperatingSystem.IsWindows())
        {
            return (int)(GetCurrentProcessorNumberWin32() % Environment.ProcessorCount);
        }
        else if (OperatingSystem.IsLinux())
        {
            return GetCurrentProcessorNumberLinux();
        }

        return 0; // Fallback
    }

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcessorNumber")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetCurrentProcessorNumberWin32Api();

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static uint GetCurrentProcessorNumberWin32()
    {
        try
        {
            return GetCurrentProcessorNumberWin32Api();
        }
        catch
        {
            return 0;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static int GetCurrentProcessorNumberLinux()
    {
        try
        {
            // Use sched_getcpu() if available
            return sched_getcpu();
        }
        catch
        {
            return 0;
        }
    }

    [DllImport("c", EntryPoint = "sched_getcpu")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int sched_getcpu();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}

/// <summary>
/// NUMA-aware CPU memory buffer implementation.
/// </summary>
internal sealed class CpuMemoryBuffer : IMemoryBuffer
{
    private readonly CpuMemoryManager _manager;
    private readonly IMemoryOwner<byte> _memoryOwner;
    private readonly long _sizeInBytes;
    private readonly MemoryOptions _options;
    private readonly long _viewOffset;
    private readonly long _viewLength;
    private readonly int _preferredNode;
    private readonly NumaMemoryPolicy _policy;
    private int _disposed;

    public CpuMemoryBuffer(long sizeInBytes, MemoryOptions options, CpuMemoryManager manager)
        : this(sizeInBytes, options, manager, 0, manager.CreateDefaultPolicy())
    {
    }

    public CpuMemoryBuffer(long sizeInBytes, MemoryOptions options, CpuMemoryManager manager, int preferredNode, NumaMemoryPolicy policy)
    {
        _sizeInBytes = sizeInBytes;
        _options = options;
        _manager = manager;
        _viewOffset = 0;
        _viewLength = sizeInBytes;
        _preferredNode = preferredNode;
        _policy = policy;

        // Use NUMA-aware memory allocation
        _memoryOwner = AllocateNumaAwareMemory(sizeInBytes, preferredNode, policy);
    }

    private CpuMemoryBuffer(CpuMemoryBuffer parent, long offset, long length)
    {
        _memoryOwner = parent._memoryOwner;
        _sizeInBytes = parent._sizeInBytes;
        _options = parent._options;
        _manager = parent._manager;
        _viewOffset = parent._viewOffset + offset;
        _viewLength = length;
        _preferredNode = parent._preferredNode;
        _policy = parent._policy;
    }

    public long SizeInBytes => _viewLength;

    public MemoryOptions Options => _options;

    /// <summary>
    /// Gets the NUMA node this buffer is allocated on.
    /// </summary>
    public int PreferredNode => _preferredNode;

    /// <summary>
    /// Gets the memory policy used for this buffer.
    /// </summary>
    public NumaMemoryPolicy Policy => _policy;

    public Memory<byte> GetMemory()
    {
        ThrowIfDisposed();

        var memory = _memoryOwner.Memory;
        if (_viewOffset > 0 || _viewLength < _sizeInBytes)
        {
            memory = memory.Slice((int)_viewOffset, (int)_viewLength);
        }

        return memory;
    }

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        if ((_options & MemoryOptions.WriteOnly) != 0 && (_options & MemoryOptions.ReadOnly) != 0)
        {
            throw new InvalidOperationException("Cannot write to read-only buffer");
        }

        var elementSize = Marshal.SizeOf<T>();
        var bytesToCopy = source.Length * elementSize;

        if (offset < 0 || offset + bytesToCopy > _viewLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var destMemory = GetMemory().Slice((int)offset, bytesToCopy);
        var sourceSpan = MemoryMarshal.AsBytes(source.Span);

        // Use optimized NUMA-aware memory copy
        if (bytesToCopy >= 64)
        {
            OptimizedNumaMemoryCopy(sourceSpan, destMemory.Span, _preferredNode);
        }
        else
        {
            sourceSpan.CopyTo(destMemory.Span);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        if ((_options & MemoryOptions.ReadOnly) != 0 && (_options & MemoryOptions.WriteOnly) != 0)
        {
            throw new InvalidOperationException("Cannot read from write-only buffer");
        }

        var elementSize = Marshal.SizeOf<T>();
        var bytesToCopy = destination.Length * elementSize;

        if (offset < 0 || offset + bytesToCopy > _viewLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var sourceMemory = GetMemory().Slice((int)offset, bytesToCopy);
        var destSpan = MemoryMarshal.AsBytes(destination.Span);

        // Use optimized NUMA-aware memory copy
        if (bytesToCopy >= 64)
        {
            OptimizedNumaMemoryCopy(sourceMemory.Span, destSpan, _preferredNode);
        }
        else
        {
            sourceMemory.Span.CopyTo(destSpan);
        }

        return ValueTask.CompletedTask;
    }

    public CpuMemoryBuffer CreateView(long offset, long length)
    {
        ThrowIfDisposed();

        if (offset < 0 || length < 0 || offset + length > _viewLength)
        {
            throw new ArgumentOutOfRangeException(offset < 0 || offset + length > _viewLength ? nameof(offset) : nameof(length), "Invalid buffer view range");
        }

        return new CpuMemoryBuffer(this, offset, length);
    }

    /// <summary>
    /// Allocates NUMA-aware memory using platform-specific methods.
    /// </summary>
    private static IMemoryOwner<byte> AllocateNumaAwareMemory(long sizeInBytes, int preferredNode, NumaMemoryPolicy policy)
    {
        // For large allocations, use NUMA-aware native memory
        const int largeAllocationThreshold = 64 * 1024; // 64KB threshold

        if (sizeInBytes >= largeAllocationThreshold && NumaInfo.IsNumaSystem)
        {
            return new NumaAwareMemoryOwner(sizeInBytes, preferredNode, policy);
        }

        // For smaller allocations, use regular pooled memory
        const int maxArrayLength = 1024 * 1024 * 1024; // 1GB limit
        if (sizeInBytes <= maxArrayLength)
        {
            return MemoryPool<byte>.Shared.Rent((int)sizeInBytes);
        }
        else
        {
            return new NativeMemoryOwner(sizeInBytes);
        }
    }

    /// <summary>
    /// NUMA-aware optimized memory copy with prefetching and streaming hints.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OptimizedNumaMemoryCopy(ReadOnlySpan<byte> source, Span<byte> destination, int preferredNode)
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination must have the same length");
        }

        // Performance optimization: Use different strategies based on size
        var length = source.Length;

        // For very small copies, just use the built-in copy
        if (length < 256)
        {
            source.CopyTo(destination);
            return;
        }

        // Add NUMA-aware prefetching for large transfers
        if (length >= 256 * 1024) // 256KB threshold (reduced for better cache utilization)
        {
            PrefetchForNumaTransfer(source, destination, preferredNode);
        }

        // Use the largest available SIMD width for copying
        if (Vector512.IsHardwareAccelerated && length >= 64)
        {
            MemoryCopyAvx512(source, destination);
        }
        else if (Vector256.IsHardwareAccelerated && length >= 32)
        {
            MemoryCopyAvx2(source, destination);
        }
        else if (Vector128.IsHardwareAccelerated && length >= 16)
        {
            MemoryCopyVector128(source, destination);
        }
        else
        {
            source.CopyTo(destination);
        }
    }

    /// <summary>
    /// Prefetches memory for NUMA-aware transfers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrefetchForNumaTransfer(ReadOnlySpan<byte> source, Span<byte> destination, int preferredNode)
    {
        // Prefetch data in cache-line sized chunks
        const int prefetchDistance = 64; // Cache line size
        const int maxPrefetchLines = 8;  // Prefetch up to 8 cache lines ahead

        unsafe
        {
            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = destination)
            {
                var length = Math.Min(source.Length, destination.Length);
                var prefetchLines = Math.Min(maxPrefetchLines, (length + prefetchDistance - 1) / prefetchDistance);

                for (var i = 0; i < prefetchLines; i++)
                {
                    var offset = i * prefetchDistance;
                    if (offset < length)
                    {
                        // Prefetch for reading
                        if (Sse.IsSupported)
                        {
                            Sse.Prefetch0(srcPtr + offset);
                        }

                        // Prefetch for writing
                        if (Sse.IsSupported)
                        {
                            Sse.Prefetch0(dstPtr + offset);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Optimized memory copy using SIMD instructions for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OptimizedMemoryCopy(ReadOnlySpan<byte> source, Span<byte> destination) => OptimizedNumaMemoryCopy(source, destination, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MemoryCopyAvx512(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        const int VectorSize = 64; // 512 bits = 64 bytes
        var vectorCount = source.Length / VectorSize;

        ref var srcRef = ref MemoryMarshal.GetReference(source);
        ref var dstRef = ref MemoryMarshal.GetReference(destination);

        // Performance optimization: Unroll loop for better throughput
        var unrolledCount = vectorCount / 4;
        var remainingVectors = vectorCount % 4;

        // Process 4 vectors at a time (256 bytes)
        for (var i = 0; i < unrolledCount; i++)
        {
            var baseOffset = i * VectorSize * 4;

            // Load all 4 vectors
            var data0 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, baseOffset));
            var data1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, baseOffset + VectorSize));
            var data2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, baseOffset + VectorSize * 2));
            var data3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, baseOffset + VectorSize * 3));

            // Store all 4 vectors
            data0.StoreUnsafe(ref Unsafe.Add(ref dstRef, baseOffset));
            data1.StoreUnsafe(ref Unsafe.Add(ref dstRef, baseOffset + VectorSize));
            data2.StoreUnsafe(ref Unsafe.Add(ref dstRef, baseOffset + VectorSize * 2));
            data3.StoreUnsafe(ref Unsafe.Add(ref dstRef, baseOffset + VectorSize * 3));
        }

        // Process remaining vectors
        var remainingOffset = unrolledCount * VectorSize * 4;
        for (var i = 0; i < remainingVectors; i++)
        {
            var offset = remainingOffset + i * VectorSize;
            var data = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, offset));
            data.StoreUnsafe(ref Unsafe.Add(ref dstRef, offset));
        }

        // Handle remainder bytes
        var remainder = source.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            var remainingSource = source.Slice(lastOffset, remainder);
            var remainingDest = destination.Slice(lastOffset, remainder);

            // Use AVX2 or SSE for remainder if possible
            if (remainder >= 32 && Vector256.IsHardwareAccelerated)
            {
                MemoryCopyAvx2(remainingSource, remainingDest);
            }
            else if (remainder >= 16 && Vector128.IsHardwareAccelerated)
            {
                MemoryCopyVector128(remainingSource, remainingDest);
            }
            else
            {
                remainingSource.CopyTo(remainingDest);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MemoryCopyAvx2(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        const int VectorSize = 32; // 256 bits = 32 bytes
        var vectorCount = source.Length / VectorSize;

        ref var srcRef = ref MemoryMarshal.GetReference(source);
        ref var dstRef = ref MemoryMarshal.GetReference(destination);

        // Performance optimization: Unroll by 2 for AVX2
        var unrolledCount = vectorCount / 2;
        var remainingVectors = vectorCount % 2;

        // Process 2 vectors at a time (64 bytes)
        for (var i = 0; i < unrolledCount; i++)
        {
            var baseOffset = i * VectorSize * 2;

            var data0 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, baseOffset));
            var data1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, baseOffset + VectorSize));

            data0.StoreUnsafe(ref Unsafe.Add(ref dstRef, baseOffset));
            data1.StoreUnsafe(ref Unsafe.Add(ref dstRef, baseOffset + VectorSize));
        }

        // Process remaining vector
        if (remainingVectors > 0)
        {
            var offset = unrolledCount * VectorSize * 2;
            var data = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, offset));
            data.StoreUnsafe(ref Unsafe.Add(ref dstRef, offset));
        }

        // Handle remainder
        var remainder = source.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            var remainingSource = source.Slice(lastOffset, remainder);
            var remainingDest = destination.Slice(lastOffset, remainder);

            if (remainder >= 16 && Vector128.IsHardwareAccelerated)
            {
                MemoryCopyVector128(remainingSource, remainingDest);
            }
            else
            {
                remainingSource.CopyTo(remainingDest);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void MemoryCopyVector128(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        const int VectorSize = 16; // 128 bits = 16 bytes
        var vectorCount = source.Length / VectorSize;

        ref var srcRef = ref MemoryMarshal.GetReference(source);
        ref var dstRef = ref MemoryMarshal.GetReference(destination);

        // Copy 16 bytes at a time using SSE/NEON
        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var data = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, offset));
            data.StoreUnsafe(ref Unsafe.Add(ref dstRef, offset));
        }

        // Handle remainder
        var remainder = source.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            var remainingSource = source.Slice(lastOffset, remainder);
            var remainingDest = destination.Slice(lastOffset, remainder);
            remainingSource.CopyTo(remainingDest);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Only dispose if we're the original buffer, not a view
        if (_viewOffset == 0 && _viewLength == _sizeInBytes)
        {
            _memoryOwner?.Dispose();
            _manager.OnBufferDisposed(_sizeInBytes);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}

/// <summary>
/// Native memory owner for large allocations that exceed array limits.
/// Uses NativeMemory for allocations larger than 1GB.
/// </summary>
internal sealed class NativeMemoryOwner : IMemoryOwner<byte>
{
    private unsafe byte* _ptr;
    private readonly long _size;
    private bool _disposed;

    public unsafe NativeMemoryOwner(long size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        _size = size;
        _ptr = (byte*)NativeMemory.AlignedAlloc((nuint)size, 64); // 64-byte aligned for SIMD

        if (_ptr == null)
        {
            throw new InvalidOperationException($"Failed to allocate {size} bytes of native memory");
        }
    }

    public Memory<byte> Memory
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                // Create a Memory<byte> from native pointer
                return new UnmanagedMemoryManager<byte>(_ptr, (int)Math.Min(_size, int.MaxValue)).Memory;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            unsafe
            {
                if (_ptr != null)
                {
                    NativeMemory.AlignedFree(_ptr);
                    _ptr = null;
                }
            }
            _disposed = true;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Memory manager for unmanaged memory pointers.
/// </summary>
internal sealed unsafe class UnmanagedMemoryManager<T>(T* ptr, int length) : MemoryManager<T> where T : unmanaged
{
    private readonly T* _ptr = ptr;
    private readonly int _length = length;

    public override Span<T> GetSpan() => new(_ptr, _length);

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= _length)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        return new MemoryHandle(_ptr + elementIndex);
    }

    public override void Unpin() { }

    protected override void Dispose(bool disposing) { }
}

/// <summary>
/// NUMA-aware memory owner that allocates memory on specific NUMA nodes.
/// </summary>
internal sealed class NumaAwareMemoryOwner : IMemoryOwner<byte>
{
    private unsafe byte* _ptr;
    private readonly long _size;
    private readonly int _preferredNode;
    private readonly NumaMemoryPolicy _policy;
    private bool _disposed;

    public unsafe NumaAwareMemoryOwner(long size, int preferredNode, NumaMemoryPolicy policy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        _size = size;
        _preferredNode = preferredNode;
        _policy = policy;

        // Allocate memory using NUMA-aware allocation
        _ptr = AllocateNumaMemory(size, preferredNode, policy);

        if (_ptr == null)
        {
            throw new InvalidOperationException($"Failed to allocate {size} bytes on NUMA node {preferredNode}");
        }

        // Perform first-touch allocation to ensure pages are allocated on the correct node
        PerformFirstTouchAllocation();
    }

    public Memory<byte> Memory
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                return new UnmanagedMemoryManager<byte>(_ptr, (int)Math.Min(_size, int.MaxValue)).Memory;
            }
        }
    }

    private static unsafe byte* AllocateNumaMemory(long size, int preferredNode, NumaMemoryPolicy policy)
    {
        // Try platform-specific NUMA allocation
        if (OperatingSystem.IsWindows())
        {
            return AllocateNumaMemoryWindows(size, preferredNode);
        }
        else if (OperatingSystem.IsLinux())
        {
            return AllocateNumaMemoryLinux(size, preferredNode, policy);
        }

        // Fallback to regular aligned allocation
        return (byte*)NativeMemory.AlignedAlloc((nuint)size, 64);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static unsafe byte* AllocateNumaMemoryWindows(long size, int preferredNode)
    {
        try
        {
            // Use VirtualAllocExNuma for Windows NUMA allocation
            var ptr = VirtualAllocExNuma(
                GetCurrentProcess(),
                IntPtr.Zero,
                (nuint)size,
                MEM_COMMIT | MEM_RESERVE,
                PAGE_READWRITE,
                (uint)preferredNode);

            return (byte*)ptr;
        }
        catch
        {
            // Fallback to regular allocation
            return (byte*)NativeMemory.AlignedAlloc((nuint)size, 64);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static unsafe byte* AllocateNumaMemoryLinux(long size, int preferredNode, NumaMemoryPolicy policy)
    {
        try
        {
            // Use numa_alloc_onnode for Linux NUMA allocation
            var ptr = numa_alloc_onnode((nuint)size, preferredNode);
            if (ptr != null)
            {
                return (byte*)ptr;
            }
        }
        catch
        {
            // Fallback to regular allocation
        }

        return (byte*)NativeMemory.AlignedAlloc((nuint)size, 64);
    }

    private unsafe void PerformFirstTouchAllocation()
    {
        // Touch each page to ensure it's allocated on the correct NUMA node
        const int pageSize = 4096; // Standard page size
        var pages = (int)((_size + pageSize - 1) / pageSize);

        for (var i = 0; i < pages; i++)
        {
            var offset = i * pageSize;
            if (offset < _size)
            {
                _ptr[offset] = 0; // Touch the page
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            unsafe
            {
                if (_ptr != null)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        _ = VirtualFree((IntPtr)_ptr, 0, MEM_RELEASE);
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        try
                        {
                            numa_free(_ptr, (nuint)_size);
                        }
                        catch
                        {
                            NativeMemory.AlignedFree(_ptr);
                        }
                    }
                    else
                    {
                        NativeMemory.AlignedFree(_ptr);
                    }
                    _ptr = null;
                }
            }
            _disposed = true;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    #region Windows NUMA API

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr VirtualAllocExNuma(
        IntPtr hProcess,
        IntPtr lpAddress,
        nuint dwSize,
        uint flAllocationType,
        uint flProtect,
        uint nndPreferred);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool VirtualFree(IntPtr lpAddress, nuint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetCurrentProcess();

    #endregion

    #region Linux NUMA API

    [DllImport("numa", EntryPoint = "numa_alloc_onnode")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern unsafe void* numa_alloc_onnode(nuint size, int node);

    [DllImport("numa", EntryPoint = "numa_free")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern unsafe void numa_free(void* ptr, nuint size);

    #endregion
}

/// <summary>
/// NUMA memory allocation policy.
/// </summary>
public sealed class NumaMemoryPolicy
{
    /// <summary>
    /// Gets or sets the allocation strategy.
    /// </summary>
    public required NumaAllocationStrategy Strategy { get; init; }

    /// <summary>
    /// Gets or sets the preferred NUMA nodes for allocation.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays - Required for NUMA policy configuration
    public required int[] PreferredNodes { get; init; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets the fallback strategy when preferred nodes are unavailable.
    /// </summary>
    public required NumaFallbackStrategy FallbackStrategy { get; init; }
}

/// <summary>
/// NUMA allocation strategies.
/// </summary>
public enum NumaAllocationStrategy
{
    /// <summary>
    /// Prefer local node, fallback to other nodes.
    /// </summary>
    LocalPreferred,

    /// <summary>
    /// Use first-fit allocation.
    /// </summary>
    FirstFit,

    /// <summary>
    /// Use best-fit allocation based on available memory.
    /// </summary>
    BestFit,

    /// <summary>
    /// Interleave allocation across multiple nodes.
    /// </summary>
    Interleaved,

    /// <summary>
    /// Allocate only on explicitly specified nodes.
    /// </summary>
    Explicit
}

/// <summary>
/// NUMA fallback strategies.
/// </summary>
public enum NumaFallbackStrategy
{
    /// <summary>
    /// Try closest nodes first based on NUMA distance.
    /// </summary>
    ClosestFirst,

    /// <summary>
    /// Try any available node.
    /// </summary>
    AnyNode,

    /// <summary>
    /// Fail if preferred nodes are unavailable.
    /// </summary>
    Strict
}

/// <summary>
/// NUMA memory usage statistics.
/// </summary>
public sealed class NumaMemoryStatistics
{
    /// <summary>
    /// Gets the total allocated bytes across all nodes.
    /// </summary>
    public required long TotalAllocatedBytes { get; init; }

    /// <summary>
    /// Gets per-node statistics.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays - Required for NUMA statistics representation
    public required NumaNodeStatistics[] NodeStatistics { get; init; }
#pragma warning restore CA1819
}

/// <summary>
/// Memory statistics for a specific NUMA node.
/// </summary>
public sealed class NumaNodeStatistics
{
    /// <summary>
    /// Gets the node ID.
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// Gets the allocated bytes on this node.
    /// </summary>
    public long AllocatedBytes { get; set; }

    /// <summary>
    /// Gets the number of buffers allocated on this node.
    /// </summary>
    public int BufferCount { get; set; }
}
