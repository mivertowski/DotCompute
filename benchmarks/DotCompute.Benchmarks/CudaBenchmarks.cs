using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Globalization;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for CUDA backend operations and GPU-specific functionality.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class CudaBenchmarks
{
    private const int SmallSize = 1024;
    private const int MediumSize = 1024 * 1024;
    private const int LargeSize = 16 * 1024 * 1024;

    private float[] _hostData = null!;
    private float[] _resultData = null!;

    [Params(SmallSize, MediumSize, LargeSize)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _hostData = new float[DataSize];
        _resultData = new float[DataSize];
        
        // Initialize with test data
        for (var i = 0; i < DataSize; i++)
        {
            _hostData[i] = (float)Math.Sin(i * 0.01);
        }
    }

    [Benchmark(Baseline = true)]
    public void CpuVectorAddition()
    {
        for (var i = 0; i < DataSize; i++)
        {
            _resultData[i] = _hostData[i] + _hostData[i];
        }
    }

    [Benchmark]
    public void CpuParallelVectorAddition()
    {
        _ = Parallel.For(0, DataSize, i =>
        {
            _resultData[i] = _hostData[i] + _hostData[i];
        });
    }

    [Benchmark]
    public void SimulatedGpuVectorAddition()
    {
        // Simulate GPU computation with some overhead
        Task.Delay(1).GetAwaiter().GetResult(); // Simulate kernel launch overhead

        _ = Parallel.For(0, DataSize, i =>
        {
            _resultData[i] = _hostData[i] + _hostData[i];
        });
        
        Task.Delay(1).GetAwaiter().GetResult(); // Simulate synchronization overhead
    }

    [Benchmark]
    public void CpuMatrixMultiplication()
    {
        var size = (int)Math.Sqrt(DataSize);
        if (size * size != DataSize)
        {
            size = 32; // Fallback to small matrix
        }
        
        var matrixA = new float[size][];
        var matrixB = new float[size][];
        var result = new float[size][];
        
        // Initialize jagged arrays
        for (var i = 0; i < size; i++)
        {
            matrixA[i] = new float[size];
            matrixB[i] = new float[size];
            result[i] = new float[size];
        }
        
        // Initialize matrices
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                matrixA[i][j] = i + j;
                matrixB[i][j] = i * j + 1;
            }
        }
        
        // Matrix multiplication
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                float sum = 0;
                for (var k = 0; k < size; k++)
                {
                    sum += matrixA[i][k] * matrixB[k][j];
                }
                result[i][j] = sum;
            }
        }
    }

    [Benchmark]
    public void SimulatedCudaMatrixMultiplication()
    {
        var size = (int)Math.Sqrt(DataSize);
        if (size * size != DataSize)
        {
            size = 32; // Fallback to small matrix
        }
        
        // Simulate GPU memory allocation overhead
        Task.Delay(2).GetAwaiter().GetResult();
        
        var matrixA = new float[size][];
        var matrixB = new float[size][];
        var result = new float[size][];
        
        // Initialize jagged arrays
        for (var i = 0; i < size; i++)
        {
            matrixA[i] = new float[size];
            matrixB[i] = new float[size];
            result[i] = new float[size];
        }
        
        // Initialize matrices (simulate host-to-device transfer)
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                matrixA[i][j] = i + j;
                matrixB[i][j] = i * j + 1;
            }
        }

        // Simulate GPU computation (faster than CPU for large matrices)
        _ = Parallel.For(0, size, i =>
        {
            for (var j = 0; j < size; j++)
            {
                float sum = 0;
                for (var k = 0; k < size; k++)
                {
                    sum += matrixA[i][k] * matrixB[k][j];
                }
                result[i][j] = sum;
            }
        });
        
        // Simulate device-to-host transfer
        Task.Delay(1).GetAwaiter().GetResult();
    }

    [Benchmark]
    public void CpuReduction()
    {
        float sum = 0;
        for (var i = 0; i < DataSize; i++)
        {
            sum += _hostData[i];
        }
        
        // Prevent optimization
        if (float.IsNaN(sum))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void CpuParallelReduction()
    {
        var sum = _hostData.AsParallel().Sum();
        
        // Prevent optimization
        if (float.IsNaN(sum))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void SimulatedGpuReduction()
    {
        // Simulate GPU reduction with tree reduction pattern
        var data = _hostData.ToArray();
        
        // Simulate kernel launch overhead
        Task.Delay(1).GetAwaiter().GetResult();
        
        // Tree reduction simulation
        while (data.Length > 1)
        {
            var nextLevel = new float[(data.Length + 1) / 2];
            _ = Parallel.For(0, nextLevel.Length, i =>
            {
                var idx = i * 2;
                nextLevel[i] = data[idx] + (idx + 1 < data.Length ? data[idx + 1] : 0);
            });
            data = nextLevel;
        }
        
        // Prevent optimization
        if (float.IsNaN(data[0]))
        {
            throw new InvalidOperationException();
        }
    }

    [Benchmark]
    public void CudaKernelCompilationSimulation()
    {
        // Simulate NVRTC compilation overhead
        var kernelSource = """
            extern "C" __global__ void vectorAdd(float* a, float* b, float* c, int n) {
                int i = blockIdx.x * blockDim.x + threadIdx.x;
                if (i < n) {
                    c[i] = a[i] + b[i];
                }
            }
            """;
        
        // Simulate compilation time
        Task.Delay(10).GetAwaiter().GetResult();
        
        // Simulate JIT overhead
        var hash = kernelSource.GetHashCode(StringComparison.Ordinal);
        if (hash == 0)
        {
            throw new InvalidOperationException();
        }
    }
}