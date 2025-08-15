// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;

namespace DotCompute.Tests.Shared;

/// <summary>
/// Provides consolidated test data generation utilities for all DotCompute test projects.
/// </summary>
#pragma warning disable CA5394 // Do not use insecure randomness - Test code uses deterministic Random for reproducible tests
public static class TestDataGenerator
{
    private static readonly Random Random = new(42); // Deterministic for reproducible tests

    /// <summary>
    /// Generates an array of _random integers.
    /// </summary>
    public static int[] GenerateIntArray(int length, int minValue = int.MinValue, int maxValue = int.MaxValue)
    {
        var data = new int[length];
        for(var i = 0; i < length; i++)
        {
            data[i] = Random.Next(minValue, maxValue);
        }
        return data;
    }

    /// <summary>
    /// Generates an array of _random floats.
    /// </summary>
    public static float[] GenerateFloatArray(int length, float minValue = -1000f, float maxValue = 1000f)
    {
        var data = new float[length];
        for(var i = 0; i < length; i++)
        {
            data[i] = minValue +(float)(Random.NextDouble() *(maxValue - minValue));
        }
        return data;
    }

    /// <summary>
    /// Generates an array of _random doubles.
    /// </summary>
    public static double[] GenerateDoubleArray(int length, double minValue = -1000.0, double maxValue = 1000.0)
    {
        var data = new double[length];
        for(var i = 0; i < length; i++)
        {
            data[i] = minValue + Random.NextDouble() *(maxValue - minValue);
        }
        return data;
    }

    /// <summary>
    /// Generates test cases for different array sizes.
    /// </summary>
#pragma warning disable CA1024 // Use properties where appropriate - This method uses yield return which cannot be a property
    public static IEnumerable<object[]> GetArraySizeTestCases()
    {
        yield return new object[] { 1 };
        yield return new object[] { 16 };
        yield return new object[] { 256 };
        yield return new object[] { 1024 };
        yield return new object[] { 4096 };
        yield return new object[] { 16384 };
        yield return new object[] { 65536 };
    }
#pragma warning restore CA1024

    /// <summary>
    /// Generates test cases for edge case array sizes.
    /// </summary>
#pragma warning disable CA1024 // Use properties where appropriate - This method uses yield return which cannot be a property
    public static IEnumerable<object[]> GetEdgeCaseArraySizes()
    {
        yield return new object[] { 0 };
        yield return new object[] { 1 };
        yield return new object[] { 2 };
        yield return new object[] { 15 };
        yield return new object[] { 16 };
        yield return new object[] { 17 };
        yield return new object[] { 31 };
        yield return new object[] { 32 };
        yield return new object[] { 33 };
    }
#pragma warning restore CA1024

    /// <summary>
    /// Generates a 2D array with _random values.
    /// </summary>
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional - Multidimensional arrays are required for matrix operations
    public static T[,] Generate2DArray<T>(int rows, int cols, Func<T> generator)
    {
        var array = new T[rows, cols];
        for(var i = 0; i < rows; i++)
        {
            for(var j = 0; j < cols; j++)
            {
                array[i, j] = generator();
            }
        }
        return array;
    }
#pragma warning restore CA1814

    /// <summary>
    /// Generates test matrices for multiplication.
    /// </summary>
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional - Multidimensional arrays are required for matrix operations
    public static(float[,] a, float[,] b, float[,] expected) GenerateMatrixMultiplicationTestCase(int m, int n, int k)
    {
        var a = Generate2DArray(m, n, () => (float)Random.Next(-10, 10));
        var b = Generate2DArray(n, k, () => (float)Random.Next(-10, 10));
        var expected = new float[m, k];

        // Calculate expected result
        for(var i = 0; i < m; i++)
        {
            for(var j = 0; j < k; j++)
            {
                float sum = 0;
                for(var l = 0; l < n; l++)
                {
                    sum += a[i, l] * b[l, j];
                }
                expected[i, j] = sum;
            }
        }

        return(a, b, expected);
    }
#pragma warning restore CA1814

    /// <summary>
    /// Generates sparse array data.
    /// </summary>
    public static(int[] indices, T[] values, int length) GenerateSparseArray<T>(int length, double sparsity, Func<T> generator)
    {
        var nonZeroCount = (int)(length * (1 - sparsity));
        var indices = Enumerable.Range(0, length)
            .OrderBy(_ => Random.Next())
            .Take(nonZeroCount)
            .OrderBy(x => x)
            .ToArray();

        var values = new T[nonZeroCount];
        for(var i = 0; i < nonZeroCount; i++)
        {
            values[i] = generator();
        }

        return(indices, values, length);
    }

    /// <summary>
    /// Generates an array using custom generator function.
    /// </summary>
    public static T[] GenerateArray<T>(int length, Func<int, T> generator)
    {
        var result = new T[length];
        for(var i = 0; i < length; i++)
        {
            result[i] = generator(i);
        }
        return result;
    }

    /// <summary>
    /// Generates _random bytes.
    /// </summary>
    public static byte[] GenerateRandomBytes(int length)
    {
        var result = new byte[length];
        Random.NextBytes(result);
        return result;
    }

    /// <summary>
    /// Generates _random floats in specified range.
    /// </summary>
    public static float[] GenerateRandomFloats(int length, float min = 0.0f, float max = 1.0f) => GenerateArray(length, i => (float)(Random.NextDouble() * (max - min) + min));

    /// <summary>
    /// Generates _random integers in specified range.
    /// </summary>
    public static int[] GenerateRandomInts(int length, int min = 0, int max = 1000) => GenerateArray(length, i => Random.Next(min, max));

    /// <summary>
    /// Generates sequential array with incremental values.
    /// </summary>
    public static T[] GenerateSequentialArray<T>(int length, T start, Func<T, T> increment)
    {
        var result = new T[length];
        var current = start;
        for(var i = 0; i < length; i++)
        {
            result[i] = current;
            current = increment(current);
        }
        return result;
    }

    /// <summary>
    /// Generates a 2D float matrix with random values.
    /// </summary>
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional - Multidimensional arrays are required for matrix operations
    public static float[,] GenerateFloatMatrix(int rows, int cols)
    {
        var matrix = new float[rows, cols];
        for(var i = 0; i < rows; i++)
        {
            for(var j = 0; j < cols; j++)
            {
                matrix[i, j] =(float)Random.NextDouble() * 2.0f - 1.0f; // Range: -1.0 to 1.0
            }
        }
        return matrix;
    }
#pragma warning restore CA1814

    /// <summary>
    /// Fills an existing byte array with random bytes.
    /// </summary>
    public static void FillRandomBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Random.NextBytes(data);
    }
}
#pragma warning restore CA5394
