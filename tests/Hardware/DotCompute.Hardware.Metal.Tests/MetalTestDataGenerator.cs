// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Test data generation helper for Metal tests.
/// Provides methods to create various test data patterns.
/// </summary>
public static class MetalTestDataGenerator
{
    /// <summary>
    /// Creates a linear sequence of test data
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="start">Starting value</param>
    /// <param name="step">Step between consecutive values</param>
    /// <returns>Array of test data</returns>
    public static float[] CreateTestData(int count, float start = 0.0f, float step = 1.0f)
    {
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = start + i * step;
        }
        return data;
    }

    /// <summary>
    /// Creates a linear sequence of test data
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="start">Starting value</param>
    /// <param name="step">Step between consecutive values</param>
    /// <returns>Array of test data</returns>
    public static float[] CreateLinearSequence(int count, float start = 0.0f, float step = 1.0f)
    {
        return CreateTestData(count, start, step);
    }

    /// <summary>
    /// Creates random test data with optional seed for reproducibility
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="seed">Random seed for reproducibility</param>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <returns>Array of random test data</returns>
    public static float[] CreateRandomData(int count, int seed = 42, float min = 0.0f, float max = 1.0f)
    {
        var random = new Random(seed);
        var data = new float[count];
        var range = max - min;

        for (var i = 0; i < count; i++)
        {
            data[i] = min + (float)random.NextDouble() * range;
        }
        return data;
    }

    /// <summary>
    /// Creates constant test data with a specific value
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="value">Constant value to fill</param>
    /// <returns>Array of constant test data</returns>
    public static float[] CreateConstantData(int count, float value)
    {
        var data = new float[count];
        Array.Fill(data, value);
        return data;
    }

    /// <summary>
    /// Creates sinusoidal test data
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="frequency">Frequency of the sine wave</param>
    /// <param name="amplitude">Amplitude of the sine wave</param>
    /// <returns>Array of sinusoidal test data</returns>
    public static float[] CreateSinusoidalData(int count, double frequency = 0.01, float amplitude = 1.0f)
    {
        var data = new float[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = amplitude * (float)Math.Sin(i * frequency);
        }
        return data;
    }

    /// <summary>
    /// Creates Gaussian noise test data
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="mean">Mean of the Gaussian distribution</param>
    /// <param name="stdDev">Standard deviation of the Gaussian distribution</param>
    /// <param name="seed">Random seed for reproducibility</param>
    /// <returns>Array of Gaussian noise test data</returns>
    public static float[] CreateGaussianNoise(int count, float mean = 0.0f, float stdDev = 1.0f, int seed = 42)
    {
        var random = new Random(seed);
        var data = new float[count];

        for (var i = 0; i < count; i += 2)
        {
            // Box-Muller transform
            var u1 = random.NextDouble();
            var u2 = random.NextDouble();
            var z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var z1 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            data[i] = mean + stdDev * (float)z0;
            if (i + 1 < count)
            {
                data[i + 1] = mean + stdDev * (float)z1;
            }
        }

        return data;
    }

    /// <summary>
    /// Creates integer sequence test data
    /// </summary>
    /// <param name="count">Number of elements to create</param>
    /// <param name="start">Starting value</param>
    /// <param name="step">Step between consecutive values</param>
    /// <returns>Array of integer test data</returns>
    public static int[] CreateIntegerSequence(int count, int start = 0, int step = 1)
    {
        var data = new int[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = start + i * step;
        }
        return data;
    }

    /// <summary>
    /// Creates random byte array for test data
    /// </summary>
    /// <param name="count">Number of bytes to create</param>
    /// <param name="seed">Random seed for reproducibility</param>
    /// <returns>Array of random bytes</returns>
    public static byte[] CreateRandomBytes(int count, int seed = 42)
    {
        var random = new Random(seed);
        var data = new byte[count];
        random.NextBytes(data);
        return data;
    }

    /// <summary>
    /// Creates a matrix of test data (stored in row-major order as a 1D array)
    /// </summary>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    /// <param name="fillPattern">Pattern to fill: 'linear', 'random', 'identity', 'constant'</param>
    /// <param name="seed">Random seed for random pattern</param>
    /// <returns>Flattened matrix as 1D array in row-major order</returns>
    public static float[] CreateMatrix(int rows, int cols, string fillPattern = "linear", int seed = 42)
    {
        var size = rows * cols;
        var data = new float[size];

        switch (fillPattern.ToLowerInvariant())
        {
            case "random":
                var random = new Random(seed);
                for (var i = 0; i < size; i++)
                {
                    data[i] = (float)random.NextDouble();
                }
                break;

            case "identity":
                for (var i = 0; i < rows && i < cols; i++)
                {
                    data[i * cols + i] = 1.0f;
                }
                break;

            case "constant":
                Array.Fill(data, 1.0f);
                break;

            case "linear":
            default:
                for (var i = 0; i < size; i++)
                {
                    data[i] = i;
                }
                break;
        }

        return data;
    }
}
