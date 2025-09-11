// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Numerics;

namespace DotCompute.Integration.Tests.Utilities;

/// <summary>
/// Generates test data for various data types and scenarios.
/// </summary>
public class TestDataGenerator
{
    private readonly Random _random = new(42); // Fixed seed for reproducible tests

    /// <summary>
    /// Generates an array of random float values.
    /// </summary>
    public float[] GenerateFloatArray(int size, float min = -100f, float max = 100f)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = _random.NextSingle() * (max - min) + min;
        }
        return array;
    }

    /// <summary>
    /// Generates an array of random double values.
    /// </summary>
    public double[] GenerateDoubleArray(int size, double min = -100.0, double max = 100.0)
    {
        var array = new double[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = _random.NextDouble() * (max - min) + min;
        }
        return array;
    }

    /// <summary>
    /// Generates an array of random integer values.
    /// </summary>
    public int[] GenerateIntArray(int size, int min = -1000, int max = 1000)
    {
        var array = new int[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = _random.Next(min, max);
        }
        return array;
    }

    /// <summary>
    /// Generates an array of sequential values.
    /// </summary>
    public T[] GenerateSequentialArray<T>(int size, Func<int, T> generator)
    {
        var array = new T[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = generator(i);
        }
        return array;
    }

    /// <summary>
    /// Generates a 2D matrix of float values.
    /// </summary>
    public float[,] GenerateFloatMatrix(int rows, int cols, float min = -1f, float max = 1f)
    {
        var matrix = new float[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = _random.NextSingle() * (max - min) + min;
            }
        }
        return matrix;
    }

    /// <summary>
    /// Generates test vectors for SIMD operations.
    /// </summary>
    public Vector4[] GenerateVector4Array(int size)
    {
        var array = new Vector4[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = new Vector4(
                _random.NextSingle(),
                _random.NextSingle(),
                _random.NextSingle(),
                _random.NextSingle()
            );
        }
        return array;
    }

    /// <summary>
    /// Generates complex numbers for advanced mathematical operations.
    /// </summary>
    public Complex[] GenerateComplexArray(int size)
    {
        var array = new Complex[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = new Complex(_random.NextDouble() * 2 - 1, _random.NextDouble() * 2 - 1);
        }
        return array;
    }

    /// <summary>
    /// Generates normalized float values (0.0 to 1.0).
    /// </summary>
    public float[] GenerateNormalizedFloatArray(int size)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = _random.NextSingle();
        }
        return array;
    }

    /// <summary>
    /// Generates sparse data with specified density.
    /// </summary>
    public float[] GenerateSparseFloatArray(int size, double sparsity = 0.1)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = _random.NextDouble() < sparsity ? _random.NextSingle() : 0f;
        }
        return array;
    }

    /// <summary>
    /// Generates image-like data (3D array representing height x width x channels).
    /// </summary>
    public float[,,] GenerateImageData(int height, int width, int channels = 3)
    {
        var image = new float[height, width, channels];
        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                for (int c = 0; c < channels; c++)
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
    public float[] GenerateTimeSeriesData(int length, float trend = 0.01f, float noise = 0.1f)
    {
        var data = new float[length];
        float value = 0f;
        
        for (int i = 0; i < length; i++)
        {
            value += trend + (_random.NextSingle() - 0.5f) * noise;
            data[i] = value;
        }
        
        return data;
    }

    /// <summary>
    /// Generates kernel arguments for testing various kernel signatures.
    /// </summary>
    public object[] GenerateKernelArguments(params Type[] argumentTypes)
    {
        var args = new object[argumentTypes.Length];
        
        for (int i = 0; i < argumentTypes.Length; i++)
        {
            args[i] = argumentTypes[i] switch
            {
                Type t when t == typeof(int) => _random.Next(-1000, 1000),
                Type t when t == typeof(float) => _random.NextSingle() * 200f - 100f,
                Type t when t == typeof(double) => _random.NextDouble() * 200.0 - 100.0,
                Type t when t == typeof(int[]) => GenerateIntArray(100),
                Type t when t == typeof(float[]) => GenerateFloatArray(100),
                Type t when t == typeof(double[]) => GenerateDoubleArray(100),
                _ => throw new ArgumentException($"Unsupported argument type: {argumentTypes[i]}")
            };
        }
        
        return args;
    }

    /// <summary>
    /// Generates large datasets for performance testing.
    /// </summary>
    public float[] GenerateLargeDataset(long sizeInMB, float min = -1f, float max = 1f)
    {
        const int floatSize = sizeof(float);
        var elementCount = (int)(sizeInMB * 1024 * 1024 / floatSize);
        
        return GenerateFloatArray(elementCount, min, max);
    }

    /// <summary>
    /// Generates benchmark datasets with known properties for validation.
    /// </summary>
    public (float[] input, float expected) GenerateBenchmarkData(string operation, int size)
    {
        var input = operation switch
        {
            "sum" => GenerateFloatArray(size, 0f, 1f),
            "product" => GenerateFloatArray(size, 0.9f, 1.1f),
            "average" => GenerateFloatArray(size, -10f, 10f),
            "variance" => GenerateNormalizedFloatArray(size),
            _ => GenerateFloatArray(size)
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
}