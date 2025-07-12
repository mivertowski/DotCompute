// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Memory;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
public class MemoryAllocationBenchmarks
{
    private readonly int[] _sizes = { 1024, 4096, 16384, 65536, 262144, 1048576 };
    private ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private System.Buffers.MemoryPool<byte> _memoryPool = System.Buffers.MemoryPool<byte>.Shared;
    private NativeMemoryManager? _nativeManager;
    
    [Params(1024, 4096, 16384, 65536, 262144, 1048576)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _nativeManager = new NativeMemoryManager();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nativeManager?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public byte[] ManagedArrayAllocation()
    {
        var array = new byte[Size];
        // Simulate some work
        array[0] = 1;
        array[Size - 1] = 255;
        return array;
    }

    [Benchmark]
    public byte[] ArrayPoolRent()
    {
        var array = _arrayPool.Rent(Size);
        try
        {
            // Simulate some work
            array[0] = 1;
            array[Size - 1] = 255;
            return array;
        }
        finally
        {
            _arrayPool.Return(array);
        }
    }

    [Benchmark]
    public unsafe byte* NativeAllocation()
    {
        var ptr = (byte*)NativeMemory.Alloc((nuint)Size);
        try
        {
            // Simulate some work
            ptr[0] = 1;
            ptr[Size - 1] = 255;
            return ptr;
        }
        finally
        {
            NativeMemory.Free(ptr);
        }
    }

    [Benchmark]
    public Memory<byte> MemoryPoolRent()
    {
        using var owner = _memoryPool.Rent(Size);
        var memory = owner.Memory;
        
        // Simulate some work
        var span = memory.Span;
        span[0] = 1;
        span[Size - 1] = 255;
        
        return memory;
    }

    [Benchmark]
    public unsafe Memory<byte> NativeMemoryManagerAllocation()
    {
        var memory = _nativeManager!.AllocateMemory(Size);
        var span = memory.Span;
        
        // Simulate some work
        span[0] = 1;
        span[Size - 1] = 255;
        
        return memory;
    }

    [Benchmark]
    public Span<byte> StackAllocation()
    {
        // Only for smaller sizes to avoid stack overflow
        if (Size > 8192) return Span<byte>.Empty;
        
        Span<byte> span = stackalloc byte[Size];
        // Simulate some work
        span[0] = 1;
        span[Size - 1] = 255;
        return span;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DoNotOptimize<T>(T value) => GC.KeepAlive(value);

    private class NativeMemoryManager : IDisposable
    {
        private readonly List<IntPtr> _allocations = new();
        private readonly object _lock = new();
        
        public unsafe Memory<byte> AllocateMemory(int size)
        {
            var ptr = (byte*)NativeMemory.Alloc((nuint)size);
            lock (_lock)
            {
                _allocations.Add((IntPtr)ptr);
            }
            return new UnmanagedMemoryManager<byte>(ptr, size).Memory;
        }

        public unsafe void Dispose()
        {
            lock (_lock)
            {
                foreach (var ptr in _allocations)
                {
                    NativeMemory.Free(ptr.ToPointer());
                }
                _allocations.Clear();
            }
        }
    }

    private unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        private readonly T* _pointer;
        private readonly int _length;

        public UnmanagedMemoryManager(T* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        public override Span<T> GetSpan()
        {
            return new Span<T>(_pointer, _length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            return new MemoryHandle(_pointer + elementIndex);
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}