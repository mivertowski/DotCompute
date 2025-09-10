using System;
using System.Collections.Generic;
using System.Linq;

namespace DotCompute.Linq.Integration.Tests.Utilities;

/// <summary>
/// Generates test data for various scenarios and workloads.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Random Random = new(42); // Deterministic for testing

    /// <summary>
    /// Generates float arrays of various sizes for testing.
    /// </summary>
    public static IEnumerable<float[]> GenerateFloatArrays(params int[] sizes)
    {
        foreach (var size in sizes)
        {
            yield return GenerateFloatArray(size);
        }
    }

    /// <summary>
    /// Generates a single float array with random values.
    /// </summary>
    public static float[] GenerateFloatArray(int size, float min = 0f, float max = 100f)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = Random.NextSingle() * (max - min) + min;
        }
        return array;
    }

    /// <summary>
    /// Generates integer arrays with random values.
    /// </summary>
    public static int[] GenerateIntArray(int size, int min = 0, int max = 1000)
    {
        var array = new int[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = Random.Next(min, max);
        }
        return array;
    }

    /// <summary>
    /// Generates double arrays with random values.
    /// </summary>
    public static double[] GenerateDoubleArray(int size, double min = 0.0, double max = 1000.0)
    {
        var array = new double[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = Random.NextDouble() * (max - min) + min;
        }
        return array;
    }

    /// <summary>
    /// Generates sequential arrays for testing deterministic operations.
    /// </summary>
    public static float[] GenerateSequentialFloatArray(int size, float start = 0f, float step = 1f)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = start + i * step;
        }
        return array;
    }

    /// <summary>
    /// Generates matrices as 2D arrays for matrix operations testing.
    /// </summary>
    public static float[,] GenerateFloatMatrix(int rows, int cols, float min = 0f, float max = 1f)
    {
        var matrix = new float[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = Random.NextSingle() * (max - min) + min;
            }
        }
        return matrix;
    }

    /// <summary>
    /// Generates sparse data (mostly zeros) for testing sparse algorithms.
    /// </summary>
    public static float[] GenerateSparseFloatArray(int size, double sparsity = 0.9, float min = 1f, float max = 100f)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            if (Random.NextDouble() > sparsity)
            {
                array[i] = Random.NextSingle() * (max - min) + min;
            }
            // else remains 0
        }
        return array;
    }

    /// <summary>
    /// Generates test data with specific patterns for pattern recognition testing.
    /// </summary>
    public static float[] GeneratePatternedArray(int size, Pattern pattern)
    {
        var array = new float[size];
        
        switch (pattern)
        {
            case Pattern.Sine:
                for (int i = 0; i < size; i++)
                {
                    array[i] = (float)Math.Sin(2 * Math.PI * i / 50.0);
                }
                break;
                
            case Pattern.Cosine:
                for (int i = 0; i < size; i++)
                {
                    array[i] = (float)Math.Cos(2 * Math.PI * i / 50.0);
                }
                break;
                
            case Pattern.Sawtooth:
                for (int i = 0; i < size; i++)
                {
                    array[i] = (i % 100) / 100.0f;
                }
                break;
                
            case Pattern.Square:
                for (int i = 0; i < size; i++)
                {
                    array[i] = (i / 50) % 2 == 0 ? 1.0f : -1.0f;
                }
                break;
                
            case Pattern.Linear:
                for (int i = 0; i < size; i++)
                {
                    array[i] = i * 0.1f;
                }
                break;
                
            case Pattern.Exponential:
                for (int i = 0; i < size; i++)
                {
                    array[i] = (float)Math.Exp(i * 0.01);
                }
                break;
                
            default:
                return GenerateFloatArray(size);
        }
        
        return array;
    }

    /// <summary>
    /// Generates performance test scenarios with varying characteristics.
    /// </summary>
    public static IEnumerable<PerformanceTestCase> GeneratePerformanceTestCases()
    {
        yield return new PerformanceTestCase
        {
            Name = "Small Dataset - CPU Optimal",
            DataSize = 1000,
            ComputeIntensity = ComputeIntensity.Low,
            ExpectedOptimalBackend = ComputeBackend.CPU,
            Data = GenerateFloatArray(1000)
        };

        yield return new PerformanceTestCase
        {
            Name = "Medium Dataset - Balanced",
            DataSize = 50000,
            ComputeIntensity = ComputeIntensity.Medium,
            ExpectedOptimalBackend = ComputeBackend.CPU,
            Data = GenerateFloatArray(50000)
        };

        yield return new PerformanceTestCase
        {
            Name = "Large Dataset - GPU Optimal",
            DataSize = 1000000,
            ComputeIntensity = ComputeIntensity.High,
            ExpectedOptimalBackend = ComputeBackend.GPU,
            Data = GenerateFloatArray(1000000)
        };

        yield return new PerformanceTestCase
        {
            Name = "Very Large Dataset - GPU Required",
            DataSize = 10000000,
            ComputeIntensity = ComputeIntensity.VeryHigh,
            ExpectedOptimalBackend = ComputeBackend.GPU,
            Data = GenerateFloatArray(10000000)
        };

        yield return new PerformanceTestCase
        {
            Name = "Sparse Dataset",
            DataSize = 1000000,
            ComputeIntensity = ComputeIntensity.Medium,
            ExpectedOptimalBackend = ComputeBackend.CPU,
            Data = GenerateSparseFloatArray(1000000, 0.95)
        };
    }

    /// <summary>
    /// Generates streaming test data scenarios.
    /// </summary>
    public static IEnumerable<StreamingTestCase> GenerateStreamingTestCases()
    {
        yield return new StreamingTestCase
        {
            Name = "High Frequency Small Batches",
            BatchSize = 100,
            BatchCount = 1000,
            IntervalMs = 1,
            DataGenerator = () => GenerateFloatArray(100)
        };

        yield return new StreamingTestCase
        {
            Name = "Low Frequency Large Batches", 
            BatchSize = 10000,
            BatchCount = 100,
            IntervalMs = 100,
            DataGenerator = () => GenerateFloatArray(10000)
        };

        yield return new StreamingTestCase
        {
            Name = "Variable Size Batches",
            BatchSize = 0, // Will vary
            BatchCount = 500,
            IntervalMs = 10,
            DataGenerator = () => GenerateFloatArray(Random.Next(50, 5000))
        };

        yield return new StreamingTestCase
        {
            Name = "Real-time Audio Simulation",
            BatchSize = 1024, // Typical audio buffer size
            BatchCount = 1000,
            IntervalMs = 23, // ~44.1kHz sample rate
            DataGenerator = () => GeneratePatternedArray(1024, Pattern.Sine)
        };
    }

    /// <summary>
    /// Generates optimization test workloads.
    /// </summary>
    public static IEnumerable<OptimizationTestWorkload> GenerateOptimizationWorkloads()
    {
        yield return new OptimizationTestWorkload
        {
            Name = "Simple Map Operation",
            Operations = new[] { "Select(x => x * 2)" },
            InputSize = 100000,
            ExpectedOptimizations = new[] { "Vectorization", "InPlace" },
            Data = GenerateFloatArray(100000)
        };

        yield return new OptimizationTestWorkload
        {
            Name = "Chain of Map Operations",
            Operations = new[] { "Select(x => x * 2)", "Select(x => x + 1)", "Select(x => x / 3)" },
            InputSize = 100000,
            ExpectedOptimizations = new[] { "KernelFusion", "Vectorization" },
            Data = GenerateFloatArray(100000)
        };

        yield return new OptimizationTestWorkload
        {
            Name = "Reduction Operation",
            Operations = new[] { "Sum()" },
            InputSize = 1000000,
            ExpectedOptimizations = new[] { "TreeReduction", "SharedMemory" },
            Data = GenerateFloatArray(1000000)
        };

        yield return new OptimizationTestWorkload
        {
            Name = "Filter and Map",
            Operations = new[] { "Where(x => x > 0)", "Select(x => x * x)" },
            InputSize = 500000,
            ExpectedOptimizations = new[] { "KernelFusion", "BranchOptimization" },
            Data = GenerateFloatArray(500000, -50, 50)
        };
    }

    /// <summary>
    /// Generates error scenarios for robustness testing.
    /// </summary>
    public static IEnumerable<ErrorTestCase> GenerateErrorTestCases()
    {
        yield return new ErrorTestCase
        {
            Name = "Null Input",
            Data = null,
            ExpectedErrorType = typeof(ArgumentNullException)
        };

        yield return new ErrorTestCase
        {
            Name = "Empty Array",
            Data = Array.Empty<float>(),
            ExpectedErrorType = typeof(ArgumentException)
        };

        yield return new ErrorTestCase
        {
            Name = "Very Large Array",
            Data = new float[int.MaxValue / 10], // Large but not max to avoid OutOfMemoryException
            ExpectedErrorType = typeof(OutOfMemoryException)
        };

        yield return new ErrorTestCase
        {
            Name = "Array with NaN values",
            Data = new float[] { 1.0f, float.NaN, 3.0f },
            ExpectedErrorType = typeof(InvalidOperationException)
        };

        yield return new ErrorTestCase
        {
            Name = "Array with Infinity values",
            Data = new float[] { 1.0f, float.PositiveInfinity, 3.0f },
            ExpectedErrorType = typeof(InvalidOperationException)
        };
    }
}

/// <summary>
/// Pattern types for generating patterned test data.
/// </summary>
public enum Pattern
{
    Sine,
    Cosine,
    Sawtooth,
    Square,
    Linear,
    Exponential
}

/// <summary>
/// Test case for performance testing.
/// </summary>
public class PerformanceTestCase
{
    public required string Name { get; set; }
    public required int DataSize { get; set; }
    public required ComputeIntensity ComputeIntensity { get; set; }
    public required ComputeBackend ExpectedOptimalBackend { get; set; }
    public required float[] Data { get; set; }
}

/// <summary>
/// Test case for streaming scenarios.
/// </summary>
public class StreamingTestCase
{
    public required string Name { get; set; }
    public required int BatchSize { get; set; }
    public required int BatchCount { get; set; }
    public required int IntervalMs { get; set; }
    public required Func<float[]> DataGenerator { get; set; }
}

/// <summary>
/// Test workload for optimization testing.
/// </summary>
public class OptimizationTestWorkload
{
    public required string Name { get; set; }
    public required string[] Operations { get; set; }
    public required int InputSize { get; set; }
    public required string[] ExpectedOptimizations { get; set; }
    public required float[] Data { get; set; }
}

/// <summary>
/// Test case for error handling scenarios.
/// </summary>
public class ErrorTestCase
{
    public required string Name { get; set; }
    public required float[]? Data { get; set; }
    public required Type ExpectedErrorType { get; set; }
}