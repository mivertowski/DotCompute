// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Builds and manages GPU-resident routing tables for inter-kernel messaging.
/// </summary>
/// <remarks>
/// <para>
/// The routing table builder performs the following operations:
/// 1. Validates kernel registrations and assigns queue indices
/// 2. Calculates optimal hash table capacity (power of 2, ~50% load factor)
/// 3. Allocates GPU memory for routing table structures
/// 4. Populates hash table with kernel ID → queue index mappings
/// 5. Returns initialized <see cref="KernelRoutingTable"/> for GPU usage
/// </para>
/// <para>
/// <b>Memory Management:</b>
/// - Hash table: capacity * 4 bytes (32-bit entries)
/// - Output queues array: kernel_count * 8 bytes (device pointers)
/// - Control blocks array: kernel_count * 64 bytes (RingKernelControlBlock)
/// </para>
/// <para>
/// <b>Thread Safety:</b> Not thread-safe. Builder should be used sequentially during initialization.
/// </para>
/// </remarks>
public sealed class RoutingTableBuilder : IDisposable
{
    private readonly CudaContext _context;
    private readonly List<KernelRegistration> _kernels = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new routing table builder for the specified CUDA context.
    /// </summary>
    /// <param name="context">CUDA context for GPU memory allocation.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    public RoutingTableBuilder(CudaContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Registers a kernel in the routing table.
    /// </summary>
    /// <param name="kernelName">Unique kernel name (will be hashed to 16-bit ID).</param>
    /// <param name="outputQueuePtr">Device pointer to kernel's output queue.</param>
    /// <param name="controlBlockPtr">Device pointer to kernel's control block.</param>
    /// <exception cref="ArgumentException">Thrown if kernel name is null/empty or already registered.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if builder has been disposed.</exception>
    /// <returns>Assigned queue index for this kernel.</returns>
    public int RegisterKernel(string kernelName, long outputQueuePtr, long controlBlockPtr)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(kernelName))
        {
            throw new ArgumentException("Kernel name cannot be null or empty.", nameof(kernelName));
        }

        var kernelId = HashKernelName(kernelName);

        // Check for duplicate kernel names
        if (_kernels.Any(k => k.KernelId == kernelId))
        {
            throw new ArgumentException($"Kernel '{kernelName}' (ID: 0x{kernelId:X4}) is already registered.", nameof(kernelName));
        }

        var queueIndex = _kernels.Count;

        _kernels.Add(new KernelRegistration
        {
            KernelName = kernelName,
            KernelId = kernelId,
            QueueIndex = queueIndex,
            OutputQueuePtr = outputQueuePtr,
            ControlBlockPtr = controlBlockPtr
        });

        return queueIndex;
    }

    /// <summary>
    /// Builds the routing table and allocates GPU memory.
    /// </summary>
    /// <returns>Initialized <see cref="KernelRoutingTable"/> with GPU-resident data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no kernels registered or validation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if builder has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <b>GPU Memory Allocation:</b>
    /// - Hash table: capacity * 4 bytes
    /// - Output queues: kernel_count * 8 bytes
    /// - Control blocks: kernel_count * 64 bytes
    /// </para>
    /// <para>
    /// <b>Hash Table Construction:</b>
    /// Uses linear probing to insert kernel ID → queue index mappings.
    /// Entry format: (kernel_id &lt;&lt; 16) | queue_index
    /// </para>
    /// </remarks>
    public KernelRoutingTable Build()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_kernels.Count == 0)
        {
            throw new InvalidOperationException("No kernels registered. Cannot build empty routing table.");
        }

        if (_kernels.Count > 65535)
        {
            throw new InvalidOperationException($"Too many kernels registered ({_kernels.Count}). Maximum is 65535.");
        }

        // Calculate optimal hash table capacity
        var capacity = KernelRoutingTable.CalculateCapacity(_kernels.Count);

        // Build hash table in host memory first
        var hashTable = BuildHashTable(capacity);

        // Allocate GPU memory for hash table
        var hashTablePtr = AllocateAndCopyHashTable(hashTable);

        // Allocate and populate output queues array
        var outputQueuesPtr = AllocateAndCopyOutputQueues();

        // Allocate and populate control blocks array
        var controlBlocksPtr = AllocateControlBlocksArray();

        // Create routing table structure
        var routingTable = new KernelRoutingTable
        {
            KernelCount = _kernels.Count,
            KernelControlBlocksPtr = controlBlocksPtr,
            OutputQueuesPtr = outputQueuesPtr,
            RoutingHashTablePtr = hashTablePtr,
            HashTableCapacity = capacity
        };

        // Validate before returning
        if (!routingTable.Validate())
        {
            throw new InvalidOperationException("Built routing table failed validation.");
        }

        return routingTable;
    }

    /// <summary>
    /// Builds the hash table with kernel ID → queue index mappings using linear probing.
    /// </summary>
    private uint[] BuildHashTable(int capacity)
    {
        var hashTable = new uint[capacity];

        foreach (var kernel in _kernels)
        {
            var entry = ((uint)kernel.KernelId << 16) | (uint)kernel.QueueIndex;

            // Linear probing to find empty slot
            var hash = kernel.KernelId % capacity;
            var probe = 0;

            while (probe < capacity)
            {
                var index = (hash + probe) % capacity;

                if (hashTable[index] == 0)
                {
                    // Empty slot found
                    hashTable[index] = entry;
                    break;
                }

                probe++;
            }

            if (probe >= capacity)
            {
                throw new InvalidOperationException($"Hash table full. Cannot insert kernel '{kernel.KernelName}' (ID: 0x{kernel.KernelId:X4}).");
            }
        }

        return hashTable;
    }

    /// <summary>
    /// Allocates GPU memory for hash table and copies data from host.
    /// </summary>
    private static long AllocateAndCopyHashTable(uint[] hashTable)
    {
        var sizeBytes = (nuint)(hashTable.Length * sizeof(uint));
        var devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        // Pin host memory for efficient copy
        GCHandle handle = GCHandle.Alloc(hashTable, GCHandleType.Pinned);
        try
        {
            var hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to copy hash table to device: {result}");
            }
        }
        finally
        {
            handle.Free();
        }

        return (long)devicePtr;
    }

    /// <summary>
    /// Allocates GPU memory for output queues array and copies device pointers.
    /// </summary>
    private long AllocateAndCopyOutputQueues()
    {
        var outputQueues = _kernels.Select(k => k.OutputQueuePtr).ToArray();
        var sizeBytes = (nuint)(outputQueues.Length * sizeof(long));
        var devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        GCHandle handle = GCHandle.Alloc(outputQueues, GCHandleType.Pinned);
        try
        {
            var hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to copy output queues to device: {result}");
            }
        }
        finally
        {
            handle.Free();
        }

        return (long)devicePtr;
    }

    /// <summary>
    /// Allocates GPU memory for control blocks array.
    /// </summary>
    /// <remarks>
    /// Control blocks are not initialized here; they are managed by individual kernels.
    /// This only allocates the array of device pointers.
    /// </remarks>
    private long AllocateControlBlocksArray()
    {
        var controlBlocks = _kernels.Select(k => k.ControlBlockPtr).ToArray();
        var sizeBytes = (nuint)(controlBlocks.Length * sizeof(long));
        var devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        GCHandle handle = GCHandle.Alloc(controlBlocks, GCHandleType.Pinned);
        try
        {
            var hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to copy control blocks to device: {result}");
            }
        }
        finally
        {
            handle.Free();
        }

        return (long)devicePtr;
    }

    /// <summary>
    /// Computes a 16-bit hash of a kernel name using FNV-1a algorithm.
    /// </summary>
    /// <param name="kernelName">Kernel name to hash.</param>
    /// <returns>16-bit hash value (0-65535).</returns>
    /// <remarks>
    /// Uses the same FNV-1a algorithm as CUDA device code for consistency.
    /// </remarks>
    private static ushort HashKernelName(string kernelName)
    {
        const uint FnvOffsetBasis = 2166136261u;
        const uint FnvPrime = 16777619u;

        var hash = FnvOffsetBasis;

        foreach (var c in kernelName)
        {
            hash ^= c;
            hash *= FnvPrime;
        }

        // Return lower 16 bits
        return (ushort)(hash & 0xFFFF);
    }

    /// <summary>
    /// Disposes the routing table builder (does not free GPU memory allocated during Build).
    /// </summary>
    /// <remarks>
    /// GPU memory allocated by <see cref="Build"/> must be freed separately using CudaInterop.cuMemFree.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _kernels.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Internal kernel registration record.
    /// </summary>
    private sealed class KernelRegistration
    {
        public required string KernelName { get; init; }
        public required ushort KernelId { get; init; }
        public required int QueueIndex { get; init; }
        public required long OutputQueuePtr { get; init; }
        public required long ControlBlockPtr { get; init; }
    }
}
