// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DotCompute.Performance.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
public class TransferBenchmarks
{
    private byte[] _sourceData = Array.Empty<byte>();
    private byte[] _destinationData = Array.Empty<byte>();
    private Memory<byte> _sourceMemory;
    private Memory<byte> _destinationMemory;
    private ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>();
    
    [Params(1024, 4096, 16384, 65536, 262144, 1048576)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sourceData = new byte[Size];
        _destinationData = new byte[Size];
        
        var random = new Random(42);
        random.NextBytes(_sourceData);
        
        _sourceMemory = new Memory<byte>(_sourceData);
        _destinationMemory = new Memory<byte>(_destinationData);
    }

    [Benchmark(Baseline = true)]
    public void ArrayCopy()
    {
        Array.Copy(_sourceData, 0, _destinationData, 0, Size);
    }

    [Benchmark]
    public void BufferBlockCopy()
    {
        Buffer.BlockCopy(_sourceData, 0, _destinationData, 0, Size);
    }

    [Benchmark]
    public void SpanCopy()
    {
        _sourceData.AsSpan().CopyTo(_destinationData.AsSpan());
    }

    [Benchmark]
    public void MemoryCopy()
    {
        _sourceMemory.CopyTo(_destinationMemory);
    }

    [Benchmark]
    public unsafe void UnsafeMemoryCopy()
    {
        fixed (byte* src = _sourceData, dst = _destinationData)
        {
            Buffer.MemoryCopy(src, dst, Size, Size);
        }
    }

    [Benchmark]
    public unsafe void NativeMemoryCopy()
    {
        fixed (byte* src = _sourceData, dst = _destinationData)
        {
            NativeMemory.Copy(src, dst, (nuint)Size);
        }
    }

    [Benchmark]
    public void ParallelArrayCopy()
    {
        const int blockSize = 4096;
        var blocks = Size / blockSize;
        
        Parallel.For(0, blocks, i =>
        {
            var offset = i * blockSize;
            var length = Math.Min(blockSize, Size - offset);
            Array.Copy(_sourceData, offset, _destinationData, offset, length);
        });
    }

    [Benchmark]
    public void ArrayPoolTransfer()
    {
        var buffer = _arrayPool.Rent(Size);
        try
        {
            Array.Copy(_sourceData, 0, buffer, 0, Size);
            Array.Copy(buffer, 0, _destinationData, 0, Size);
        }
        finally
        {
            _arrayPool.Return(buffer);
        }
    }

    [Benchmark]
    public async Task ChannelTransfer()
    {
        var writer = _channel.Writer;
        var reader = _channel.Reader;
        
        // Write data
        await writer.WriteAsync(_sourceData);
        
        // Read data
        var receivedData = await reader.ReadAsync();
        Array.Copy(receivedData, _destinationData, Size);
    }

    [Benchmark]
    public void StreamTransfer()
    {
        using var sourceStream = new MemoryStream(_sourceData);
        using var destinationStream = new MemoryStream(_destinationData);
        
        sourceStream.CopyTo(destinationStream);
    }

    [Benchmark]
    public async Task AsyncStreamTransfer()
    {
        using var sourceStream = new MemoryStream(_sourceData);
        using var destinationStream = new MemoryStream(_destinationData);
        
        await sourceStream.CopyToAsync(destinationStream);
    }

    [Benchmark]
    public void PinnedMemoryTransfer()
    {
        var sourceHandle = GCHandle.Alloc(_sourceData, GCHandleType.Pinned);
        var destinationHandle = GCHandle.Alloc(_destinationData, GCHandleType.Pinned);
        
        try
        {
            unsafe
            {
                var src = (byte*)sourceHandle.AddrOfPinnedObject();
                var dst = (byte*)destinationHandle.AddrOfPinnedObject();
                Buffer.MemoryCopy(src, dst, Size, Size);
            }
        }
        finally
        {
            sourceHandle.Free();
            destinationHandle.Free();
        }
    }

    [Benchmark]
    public void ChunkedTransfer()
    {
        const int chunkSize = 1024;
        var chunks = Size / chunkSize;
        
        for (int i = 0; i < chunks; i++)
        {
            var offset = i * chunkSize;
            var length = Math.Min(chunkSize, Size - offset);
            Array.Copy(_sourceData, offset, _destinationData, offset, length);
        }
    }
}