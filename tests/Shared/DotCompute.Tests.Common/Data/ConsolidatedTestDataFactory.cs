// Copyright (c) 2025 DotCompute Project Contributors
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.InteropServices;

namespace DotCompute.Tests.Common.Data;

/// <summary>
/// Consolidated test data factory that provides deterministic, reproducible test data
/// for all DotCompute testing scenarios. Replaces all scattered TestDataGenerator classes.
/// </summary>
public static class ConsolidatedTestDataFactory
{
    private static readonly Random _random = new(42); // Fixed seed for reproducible tests

    #region Basic Array Generation

    /// <summary>
    /// Generates an array of random integers within the specified range.
    /// </summary>
    public static int[] CreateIntArray(int length, int min = -1000, int max = 1000)
    {
        var array = new int[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = _random.Next(min, max + 1);
        }
        return array;
    }

    /// <summary>
    /// Generates an array of random float values.
    /// </summary>
    public static float[] CreateFloatArray(int length, float min = -100f, float max = 100f)
    {
        var array = new float[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = min + _random.NextSingle() * (max - min);
        }
        return array;
    }

    /// <summary>
    /// Generates an array of random double values.
    /// </summary>
    public static double[] CreateDoubleArray(int length, double min = -100.0, double max = 100.0)
    {
        var array = new double[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = min + _random.NextDouble() * (max - min);
        }
        return array;
    }

    /// <summary>
    /// Generates an array of sequential integers starting from the specified value.
    /// </summary>
    public static int[] CreateSequentialArray(int length, int start = 0) => [.. Enumerable.Range(start, length)];

    /// <summary>
    /// Generates an array filled with the specified value.
    /// </summary>
    public static T[] CreateFilledArray<T>(int length, T value)
    {
        var array = new T[length];
        Array.Fill(array, value);
        return array;
    }

    /// <summary>
    /// Generates normalized float values (0.0 to 1.0).
    /// </summary>
    public static float[] CreateNormalizedFloatArray(int length)
    {
        var array = new float[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = _random.NextSingle();
        }
        return array;
    }

    #endregion

    #region Matrix Generation

    /// <summary>
    /// Generates a 2D matrix of random integers.
    /// </summary>
    public static int[,] CreateIntMatrix(int rows, int cols, int min = -100, int max = 100)
    {
        var matrix = new int[rows, cols];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = _random.Next(min, max + 1);
            }
        }
        return matrix;
    }

    /// <summary>
    /// Generates a 2D matrix of random float values.
    /// </summary>
    public static float[,] CreateFloatMatrix(int rows, int cols, float min = -1f, float max = 1f)
    {
        var matrix = new float[rows, cols];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = min + _random.NextSingle() * (max - min);
            }
        }
        return matrix;
    }

    /// <summary>
    /// Generates an identity matrix of the specified size.
    /// </summary>
    public static float[,] CreateIdentityMatrix(int size)
    {
        var matrix = new float[size, size];
        for (var i = 0; i < size; i++)
        {
            matrix[i, i] = 1.0f;
        }
        return matrix;
    }

    /// <summary>
    /// Generates a jagged array representing a matrix.
    /// </summary>
    public static float[][] CreateJaggedMatrix(int rows, int cols, float min = -1f, float max = 1f)
    {
        var matrix = new float[rows][];
        for (var i = 0; i < rows; i++)
        {
            matrix[i] = CreateFloatArray(cols, min, max);
        }
        return matrix;
    }

    #endregion

    #region Vector Types

    /// <summary>
    /// Generates an array of Vector2 structures.
    /// </summary>
    public static Vector2[] CreateVector2Array(int length, float min = -1f, float max = 1f)
    {
        var vectors = new Vector2[length];
        for (var i = 0; i < length; i++)
        {
            vectors[i] = new Vector2(
                min + _random.NextSingle() * (max - min),
                min + _random.NextSingle() * (max - min)
            );
        }
        return vectors;
    }

    /// <summary>
    /// Generates an array of Vector3 structures.
    /// </summary>
    public static Vector3[] CreateVector3Array(int length, float min = -1f, float max = 1f)
    {
        var vectors = new Vector3[length];
        for (var i = 0; i < length; i++)
        {
            vectors[i] = new Vector3(
                min + _random.NextSingle() * (max - min),
                min + _random.NextSingle() * (max - min),
                min + _random.NextSingle() * (max - min)
            );
        }
        return vectors;
    }

    /// <summary>
    /// Generates an array of Vector4 structures for SIMD operations.
    /// </summary>
    public static Vector4[] CreateVector4Array(int length, float min = -1f, float max = 1f)
    {
        var vectors = new Vector4[length];
        for (var i = 0; i < length; i++)
        {
            vectors[i] = new Vector4(
                min + _random.NextSingle() * (max - min),
                min + _random.NextSingle() * (max - min),
                min + _random.NextSingle() * (max - min),
                min + _random.NextSingle() * (max - min)
            );
        }
        return vectors;
    }

    /// <summary>
    /// Generates complex numbers for advanced mathematical operations.
    /// </summary>
    public static Complex[] CreateComplexArray(int length)
    {
        var array = new Complex[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = new Complex(
                _random.NextDouble() * 2 - 1,
                _random.NextDouble() * 2 - 1
            );
        }
        return array;
    }

    #endregion

    #region Specialized Data

    /// <summary>
    /// Generates sparse data with specified density.
    /// </summary>
    public static float[] CreateSparseFloatArray(int length, double sparsity = 0.1, float min = 1f, float max = 10f)
    {
        var array = new float[length];
        var nonZeroCount = (int)(length * sparsity);
        var indices = Enumerable.Range(0, length)
            .OrderBy(_ => _random.Next())
            .Take(nonZeroCount);

        foreach (var index in indices)
        {
            array[index] = min + _random.NextSingle() * (max - min);
        }
        return array;
    }

    /// <summary>
    /// Generates image-like data (3D array representing height x width x channels).
    /// </summary>
    public static float[,,] CreateImageData(int height, int width, int channels = 3)
    {
        var image = new float[height, width, channels];
        for (var h = 0; h < height; h++)
        {
            for (var w = 0; w < width; w++)
            {
                for (var c = 0; c < channels; c++)
                {
                    image[h, w, c] = _random.NextSingle();
                }
            }
        }
        return image;
    }

    /// <summary>
    /// Generates time series data with specified trend and noise.
    /// </summary>
    public static float[] CreateTimeSeriesData(int length, float trend = 0.01f, float noise = 0.1f)
    {
        var data = new float[length];
        var value = 0f;

        for (var i = 0; i < length; i++)
        {
            value += trend + (_random.NextSingle() - 0.5f) * noise;
            data[i] = value;
        }

        return data;
    }

    /// <summary>
    /// Generates arrays with specific memory alignment requirements.
    /// </summary>
    public static T[] CreateAlignedArray<T>(int length, int alignment = 16) where T : struct
    {
        var elementSize = Marshal.SizeOf<T>();
        var alignedLength = (length * elementSize + alignment - 1) / alignment * alignment / elementSize;
        return new T[alignedLength];
    }

    #endregion

    #region Performance Testing Data

    /// <summary>
    /// Common sizes for performance testing across all backend types.
    /// </summary>
    public static readonly int[] CommonPerformanceSizes = [1024, 4096, 16384, 65536, 262144, 1048576];

    /// <summary>
    /// Generates performance test data for the specified size.
    /// </summary>
    public static (float[] input1, float[] input2) CreatePerformanceDataPair(int size) => (CreateFloatArray(size, -100f, 100f), CreateFloatArray(size, -100f, 100f));

    /// <summary>
    /// Generates large matrix data for performance testing.
    /// </summary>
    public static (float[,] matrix1, float[,] matrix2) CreateLargeMatrixPair(int size) => (CreateFloatMatrix(size, size, -10f, 10f), CreateFloatMatrix(size, size, -10f, 10f));

    /// <summary>
    /// Generates large datasets for memory stress testing.
    /// </summary>
    public static float[] CreateLargeDataset(long sizeInMB, float min = -1f, float max = 1f)
    {
        const int floatSize = sizeof(float);
        var elementCount = (int)(sizeInMB * 1024 * 1024 / floatSize);
        return CreateFloatArray(elementCount, min, max);
    }

    #endregion

    #region Benchmark Data with Known Results

    /// <summary>
    /// Generates benchmark datasets with known properties for validation.
    /// </summary>
    public static (float[] input, float expected) CreateBenchmarkData(string operation, int size)
    {
        var input = operation switch
        {
            "sum" => CreateFloatArray(size, 0f, 1f),
            "product" => CreateFloatArray(size, 0.9f, 1.1f),
            "average" => CreateFloatArray(size, -10f, 10f),
            "variance" => CreateNormalizedFloatArray(size),
            _ => CreateFloatArray(size)
        };

        var expected = operation switch
        {
            "sum" => input.Sum(),
            "product" => input.Aggregate(1f, (acc, x) => acc * x),
            "average" => input.Average(),
            "variance" => CalculateVariance(input),
            _ => 0f
        };

        return (input, expected);
    }

    private static float CalculateVariance(float[] data)
    {
        var mean = data.Average();
        return data.Sum(x => (x - mean) * (x - mean)) / data.Length;
    }

    #endregion

    #region Kernel Arguments Generation

    /// <summary>
    /// Generates kernel arguments for testing various kernel signatures.
    /// </summary>
    public static object[] CreateKernelArguments(params Type[] argumentTypes)
    {
        var args = new object[argumentTypes.Length];

        for (var i = 0; i < argumentTypes.Length; i++)
        {
            args[i] = argumentTypes[i] switch
            {
                Type t when t == typeof(int) => _random.Next(-1000, 1000),
                Type t when t == typeof(float) => _random.NextSingle() * 200f - 100f,
                Type t when t == typeof(double) => _random.NextDouble() * 200.0 - 100.0,
                Type t when t == typeof(int[]) => CreateIntArray(100),
                Type t when t == typeof(float[]) => CreateFloatArray(100),
                Type t when t == typeof(double[]) => CreateDoubleArray(100),
                Type t when t == typeof(Vector4) => new Vector4(_random.NextSingle(), _random.NextSingle(), _random.NextSingle(), _random.NextSingle()),
                _ => throw new ArgumentException($"Unsupported argument type: {argumentTypes[i]}")
            };
        }

        return args;
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Provides various edge case values for comprehensive testing.
    /// </summary>
    public static class EdgeCases
    {
        /// <summary>
        /// Float edge cases including special values.
        /// </summary>
        public static readonly float[] FloatValues = [
            0.0f, -0.0f, 1.0f, -1.0f,
            float.Epsilon, -float.Epsilon,
            float.MinValue, float.MaxValue,
            float.PositiveInfinity, float.NegativeInfinity,
            float.NaN
        ];

        /// <summary>
        /// Double edge cases including special values.
        /// </summary>
        public static readonly double[] DoubleValues = [
            0.0, -0.0, 1.0, -1.0,
            double.Epsilon, -double.Epsilon,
            double.MinValue, double.MaxValue,
            double.PositiveInfinity, double.NegativeInfinity,
            double.NaN
        ];

        /// <summary>
        /// Integer edge cases.
        /// </summary>
        public static readonly int[] IntValues = [
            0, 1, -1, int.MinValue, int.MaxValue
        ];

        /// <summary>
        /// Creates arrays containing edge case values mixed with normal values.
        /// </summary>
        public static float[] CreateMixedEdgeCaseArray(int length)
        {
            var array = CreateFloatArray(length);
            var edgeCaseCount = Math.Min(length, FloatValues.Length);

            for (var i = 0; i < edgeCaseCount; i++)
            {
                array[i] = FloatValues[i];
            }

            return array;
        }
    }

    #endregion

    #region Hardware-Specific Data

    /// <summary>
    /// CUDA-specific test helper methods.
    /// </summary>
    public static class CudaHelpers
    {
        /// <summary>
        /// Calculates optimal grid and block dimensions for CUDA kernel execution.
        /// </summary>
        public static (int grid, int block) CalculateOptimalDimensions(int dataSize, int maxThreadsPerBlock = 256)
        {
            var blockSize = Math.Min(dataSize, maxThreadsPerBlock);
            var gridSize = (dataSize + blockSize - 1) / blockSize;
            return (gridSize, blockSize);
        }

        /// <summary>
        /// Estimates shared memory requirements for a kernel.
        /// </summary>
        public static int EstimateSharedMemoryRequirement(int elementsPerThread, int threadsPerBlock, int bytesPerElement) => elementsPerThread * threadsPerBlock * bytesPerElement;

        /// <summary>
        /// Creates execution parameters for CUDA kernels.
        /// </summary>
        public static object CreateExecutionParameters(int gridSize, int blockSize, int sharedMemoryBytes = 0) => new { GridSize = gridSize, BlockSize = blockSize, SharedMemoryBytes = sharedMemoryBytes };
    }

    /// <summary>
    /// Metal-specific test helper methods.
    /// </summary>
    public static class MetalHelpers
    {
        /// <summary>
        /// Calculates optimal threadgroup size for Metal compute kernels.
        /// </summary>
        public static (int width, int height) CalculateOptimalThreadgroupSize(int dataSize, int maxThreadsPerGroup = 1024)
        {
            var threadsPerGroup = Math.Min(dataSize, maxThreadsPerGroup);
            var width = (int)Math.Sqrt(threadsPerGroup);
            var height = threadsPerGroup / width;
            return (width, height);
        }

        /// <summary>
        /// Creates threadgroup execution parameters for Metal.
        /// </summary>
        public static object CreateThreadgroupParameters(int threadsPerGroup, int threadgroupMemoryLength = 0) => new { ThreadsPerThreadgroup = threadsPerGroup, ThreadgroupMemoryLength = threadgroupMemoryLength };
    }

    #endregion

    #region String Generation (for ID and naming tests)

    /// <summary>
    /// Generates random strings for testing purposes.
    /// </summary>
    public static string CreateRandomString(int length, string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
    {
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = charset[_random.Next(charset.Length)];
        }
        return new string(result);
    }

    /// <summary>
    /// Generates an array of random strings.
    /// </summary>
    public static string[] CreateRandomStrings(int count, int minLength = 5, int maxLength = 20)
    {
        var strings = new string[count];
        for (var i = 0; i < count; i++)
        {
            var length = _random.Next(minLength, maxLength + 1);
            strings[i] = CreateRandomString(length);
        }
        return strings;
    }

    #endregion

    #region Pipeline and Streaming Data (from PipelineTestDataGenerator)

    /// <summary>
    /// Data distribution patterns for testing.
    /// </summary>
    public enum DataPattern
    {
        Random,
        Sequential,
        Uniform,
        Normal,
        Sparse,
        Clustered
    }

    /// <summary>
    /// Generates numeric data with configurable patterns for testing.
    /// </summary>
    /// <typeparam name="T">Numeric type to generate</typeparam>
    /// <param name="size">Number of elements</param>
    /// <param name="pattern">Data distribution pattern</param>
    /// <returns>Generated test data array</returns>
    public static T[] GenerateNumericDataWithPattern<T>(int size, DataPattern pattern = DataPattern.Random) where T : struct
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        return pattern switch
        {
            DataPattern.Random => GenerateRandomDataWithPattern<T>(size),
            DataPattern.Sequential => GenerateSequentialDataWithPattern<T>(size),
            DataPattern.Uniform => GenerateUniformDataWithPattern<T>(size),
            DataPattern.Normal => GenerateNormalDataWithPattern<T>(size),
            DataPattern.Sparse => GenerateSparseDataWithPattern<T>(size),
            DataPattern.Clustered => GenerateClusteredDataWithPattern<T>(size),
            _ => throw new ArgumentException($"Unsupported pattern: {pattern}")
        };
    }

    /// <summary>
    /// Generates streaming data for real-time processing tests.
    /// </summary>
    /// <param name="batchSize">Size of each batch</param>
    /// <param name="batches">Number of batches to generate</param>
    /// <param name="delay">Delay between batches in milliseconds</param>
    /// <returns>Async enumerable of streaming data</returns>
    public static async IAsyncEnumerable<float[]> GenerateStreamingData(int batchSize, int batches, int delay = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batches);

        for (var i = 0; i < batches; i++)
        {
            var batch = CreateFloatArray(batchSize);

            // Simulate streaming delay
            if (delay > 0)
            {
                await Task.Delay(delay);
            }

            yield return batch;
        }
    }

    /// <summary>
    /// Generates flattened image processing test data.
    /// </summary>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="channels">Number of color channels (1=grayscale, 3=RGB, 4=RGBA)</param>
    /// <returns>Flattened image data for processing</returns>
    public static float[] GenerateImageProcessingData(int width, int height, int channels = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        var size = width * height * channels;
        var imageData = new float[size];

        for (var i = 0; i < size; i++)
        {
            // Generate realistic image-like data (0.0 to 1.0 range)
            imageData[i] = _random.NextSingle();
        }

        return imageData;
    }

    private static T[] GenerateRandomDataWithPattern<T>(int size) where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            var result = CreateFloatArray(size);
            return [.. result.Cast<T>()];
        }
        if (typeof(T) == typeof(int))
        {
            var result = CreateIntArray(size);
            return [.. result.Cast<T>()];
        }
        if (typeof(T) == typeof(double))
        {
            var result = CreateDoubleArray(size);
            return [.. result.Cast<T>()];
        }

        throw new ArgumentException($"Unsupported type: {typeof(T)}");
    }

    private static T[] GenerateSequentialDataWithPattern<T>(int size) where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            var result = new float[size];
            for (var i = 0; i < size; i++)
                result[i] = i;
            return [.. result.Cast<T>()];
        }
        if (typeof(T) == typeof(int))
        {
            var result = CreateSequentialArray(size);
            return [.. result.Cast<T>()];
        }

        throw new ArgumentException($"Unsupported type: {typeof(T)}");
    }

    private static T[] GenerateUniformDataWithPattern<T>(int size) where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            var result = CreateFilledArray(size, 1.0f);
            return [.. result.Cast<T>()];
        }
        if (typeof(T) == typeof(int))
        {
            var result = CreateFilledArray(size, 1);
            return [.. result.Cast<T>()];
        }

        throw new ArgumentException($"Unsupported type: {typeof(T)}");
    }

    private static T[] GenerateNormalDataWithPattern<T>(int size) where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            var result = new float[size];
            for (var i = 0; i < size; i++)
            {
                // Box-Muller transform for normal distribution
                var u1 = 1.0f - _random.NextSingle();
                var u2 = 1.0f - _random.NextSingle();
                var normal = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
                result[i] = normal;
            }
            return [.. result.Cast<T>()];
        }

        throw new ArgumentException($"Unsupported type: {typeof(T)}");
    }

    private static T[] GenerateSparseDataWithPattern<T>(int size) where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            var result = CreateSparseFloatArray(size, 0.1);
            return [.. result.Cast<T>()];
        }

        throw new ArgumentException($"Unsupported type: {typeof(T)}");
    }

    private static T[] GenerateClusteredDataWithPattern<T>(int size) where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            var result = new float[size];
            var clusterCount = Math.Max(1, size / 100);

            for (var i = 0; i < size; i++)
            {
                var cluster = i % clusterCount;
                var clusterValue = cluster * 10.0f;
                result[i] = clusterValue + _random.NextSingle() * 2.0f - 1.0f;
            }
            return [.. result.Cast<T>()];
        }

        throw new ArgumentException($"Unsupported type: {typeof(T)}");
    }

    #endregion

    #region Deterministic Reset

    /// <summary>
    /// Resets the random number generator to ensure deterministic test results.
    /// Call this at the beginning of test methods that require exact reproducibility.
    /// </summary>
    public static void ResetToKnownSeed(int seed = 42)
    {
        // Create new random instance with specified seed
        var field = typeof(ConsolidatedTestDataFactory).GetField("_random",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field?.SetValue(null, new Random(seed));
    }

    #endregion
}