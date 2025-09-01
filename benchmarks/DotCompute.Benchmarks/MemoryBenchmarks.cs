using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Memory;
using System.Runtime.InteropServices;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for DotCompute memory operations including unified buffers and transfers.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class MemoryBenchmarks
{
    private const int SmallSize = 1024;
    private const int MediumSize = 1024 * 1024;  // 1MB
    private const int LargeSize = 16 * 1024 * 1024;  // 16MB

    private float[] _sourceData = null!;
    private float[] _targetData = null!;

    [Params(SmallSize, MediumSize, LargeSize)]
    public int BufferSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sourceData = new float[BufferSize];
        _targetData = new float[BufferSize];
        
        // Initialize with sample data
        for (var i = 0; i < BufferSize; i++)
        {
            _sourceData[i] = i * 0.5f;
        }
    }

    [Benchmark(Baseline = true)]
    public void StandardArrayCopy() => Array.Copy(_sourceData, _targetData, BufferSize);

    [Benchmark]
    public void SpanBasedCopy() => _sourceData.AsSpan().CopyTo(_targetData.AsSpan());

    [Benchmark]
    public void MemoryBasedCopy() => _sourceData.AsMemory().CopyTo(_targetData.AsMemory());

    [Benchmark]
    public void UnsafeMemoryCopy()
    {
        unsafe
        {
            fixed (float* src = _sourceData)
            fixed (float* dst = _targetData)
            {
                Buffer.MemoryCopy(src, dst, BufferSize * sizeof(float), BufferSize * sizeof(float));
            }
        }
    }

    [Benchmark]
    public void PinnedMemoryAllocation()
    {
        var handle = GCHandle.Alloc(new float[BufferSize], GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            // Use the pointer to prevent optimization
            if (ptr == IntPtr.Zero)
            {
                throw new InvalidOperationException();
            }
        }
        finally
        {
            handle.Free();
        }
    }

    [Benchmark]
    public void MemoryFillSpan() => _targetData.AsSpan().Fill(1.0f);

    [Benchmark]
    public void ParallelArrayCopy()
    {
        Parallel.For(0, BufferSize, i =>
        {
            _targetData[i] = _sourceData[i];
        });
    }

    [Benchmark]
    public void BlockCopyMemory() => Buffer.BlockCopy(_sourceData, 0, _targetData, 0, BufferSize * sizeof(float));

    [Benchmark]
    public void LinqBasedCopy() => _targetData = _sourceData.ToArray();

    [Benchmark]
    public void MemoryMarshalCast()
    {
        var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(_sourceData.AsSpan());
        var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(byteSpan);
        floatSpan.CopyTo(_targetData.AsSpan());
    }

    [Benchmark]
    public void StackallocSmallBuffer()
    {
        if (BufferSize > 1024)
        {
            return; // Only for small buffers
        }
        
        unsafe
        {
            var buffer = stackalloc float[BufferSize];
            for (var i = 0; i < BufferSize; i++)
            {
                buffer[i] = _sourceData[i];
            }
            for (var i = 0; i < BufferSize; i++)
            {
                _targetData[i] = buffer[i];
            }
        }
    }

    [Benchmark]
    public void ArrayPoolRentReturn()
    {
        var pool = System.Buffers.ArrayPool<float>.Shared;
        var rented = pool.Rent(BufferSize);
        try
        {
            _sourceData.AsSpan().CopyTo(rented.AsSpan(0, BufferSize));
            rented.AsSpan(0, BufferSize).CopyTo(_targetData.AsSpan());
        }
        finally
        {
            pool.Return(rented);
        }
    }

    [Benchmark]
    public void MemoryStreamOperations()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        using var reader = new BinaryReader(stream);
        
        // Write data
        foreach (var value in _sourceData)
        {
            writer.Write(value);
        }
        
        // Read data back
        stream.Position = 0;
        for (var i = 0; i < BufferSize; i++)
        {
            _targetData[i] = reader.ReadSingle();
        }
    }
}