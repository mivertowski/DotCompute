using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DotCompute.Benchmarks;

/// <summary>
/// Benchmarks for common compute algorithms and data processing operations.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[RankColumn]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class AlgorithmBenchmarks
{
    private const int SmallSize = 1024;
    private const int MediumSize = 64 * 1024;
    private const int LargeSize = 1024 * 1024;

    private float[] _inputData = null!;
    private float[] _weights = null!;
    private float[] _bias = null!;
    private float[] _outputData = null!;

    [Params(SmallSize, MediumSize, LargeSize)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        
        _inputData = new float[DataSize];
        _weights = new float[DataSize];
        _bias = new float[DataSize];
        _outputData = new float[DataSize];
        
        for (int i = 0; i < DataSize; i++)
        {
            _inputData[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
            _weights[i] = (float)(random.NextDouble() * 0.5); // Range [0, 0.5]
            _bias[i] = (float)(random.NextDouble() * 0.1 - 0.05); // Range [-0.05, 0.05]
        }
    }

    [Benchmark(Baseline = true)]
    public void LinearTransformation()
    {
        // y = Wx + b
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = _weights[i] * _inputData[i] + _bias[i];
        }
    }

    [Benchmark]
    public void ReLUActivation()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = Math.Max(0, _inputData[i]);
        }
    }

    [Benchmark]
    public void SigmoidActivation()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = 1.0f / (1.0f + MathF.Exp(-_inputData[i]));
        }
    }

    [Benchmark]
    public void TanhActivation()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = MathF.Tanh(_inputData[i]);
        }
    }

    [Benchmark]
    public void SoftmaxActivation()
    {
        // Find max for numerical stability
        float max = _inputData[0];
        for (int i = 1; i < DataSize; i++)
        {
            if (_inputData[i] > max)
                max = _inputData[i];
        }
        
        // Compute exponentials and sum
        float sum = 0;
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = MathF.Exp(_inputData[i] - max);
            sum += _outputData[i];
        }
        
        // Normalize
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] /= sum;
        }
    }

    [Benchmark]
    public void ElementWiseMultiplication()
    {
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = _inputData[i] * _weights[i];
        }
    }

    [Benchmark]
    public void BatchNormalization()
    {
        // Compute mean
        float mean = 0;
        for (int i = 0; i < DataSize; i++)
        {
            mean += _inputData[i];
        }
        mean /= DataSize;
        
        // Compute variance
        float variance = 0;
        for (int i = 0; i < DataSize; i++)
        {
            float diff = _inputData[i] - mean;
            variance += diff * diff;
        }
        variance /= DataSize;
        
        // Normalize
        float stdDev = MathF.Sqrt(variance + 1e-8f); // Add epsilon for numerical stability
        for (int i = 0; i < DataSize; i++)
        {
            _outputData[i] = (_inputData[i] - mean) / stdDev;
        }
    }

    [Benchmark]
    public void Convolution1D()
    {
        const int kernelSize = 5;
        var kernel = new float[] { 0.1f, 0.2f, 0.4f, 0.2f, 0.1f };
        
        for (int i = 0; i < DataSize - kernelSize + 1; i++)
        {
            float sum = 0;
            for (int k = 0; k < kernelSize; k++)
            {
                sum += _inputData[i + k] * kernel[k];
            }
            _outputData[i] = sum;
        }
    }

    [Benchmark]
    public void FFTSimulation()
    {
        // Simplified FFT-like computation (not a real FFT)
        var real = new float[DataSize];
        var imag = new float[DataSize];
        
        Array.Copy(_inputData, real, DataSize);
        
        // Simulate frequency domain transformation
        for (int k = 0; k < DataSize; k++)
        {
            float realSum = 0, imagSum = 0;
            
            for (int n = 0; n < Math.Min(DataSize, 64); n++) // Limit for performance
            {
                float angle = -2.0f * MathF.PI * k * n / DataSize;
                float cosAngle = MathF.Cos(angle);
                float sinAngle = MathF.Sin(angle);
                
                realSum += real[n] * cosAngle - imag[n] * sinAngle;
                imagSum += real[n] * sinAngle + imag[n] * cosAngle;
            }
            
            _outputData[k] = MathF.Sqrt(realSum * realSum + imagSum * imagSum);
        }
    }

    [Benchmark]
    public void ParallelLinearTransformation()
    {
        Parallel.For(0, DataSize, i =>
        {
            _outputData[i] = _weights[i] * _inputData[i] + _bias[i];
        });
    }

    [Benchmark]
    public void ParallelReLU()
    {
        Parallel.For(0, DataSize, i =>
        {
            _outputData[i] = Math.Max(0, _inputData[i]);
        });
    }

    [Benchmark]
    public void VectorizedLinearTransformation()
    {
        var inputSpan = _inputData.AsSpan();
        var weightSpan = _weights.AsSpan();
        var biasSpan = _bias.AsSpan();
        var outputSpan = _outputData.AsSpan();
        
        for (int i = 0; i < DataSize; i++)
        {
            outputSpan[i] = weightSpan[i] * inputSpan[i] + biasSpan[i];
        }
    }

    [Benchmark]
    public void MemoryBandwidthTest()
    {
        // Test memory bandwidth with simple copy operation
        Buffer.BlockCopy(_inputData, 0, _outputData, 0, DataSize * sizeof(float));
    }

    [Benchmark]
    public void CacheEfficiencyTest()
    {
        // Access pattern that tests cache efficiency
        int stride = 1;
        for (int s = 0; s < 4; s++)
        {
            for (int i = 0; i < DataSize; i += stride)
            {
                _outputData[i] = _inputData[i] * 2.0f;
            }
            stride *= 2;
        }
    }
}