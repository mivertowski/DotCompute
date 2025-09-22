// Copyright (c) 2025 DotCompute Project Contributors
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;

namespace DotCompute.Tests.Common.Utilities;

/// <summary>
/// Provides test data generation utilities for hardware tests.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Random Random = new(42);

    /// <summary>
    /// Creates an array of random float values.
    /// </summary>
    public static float[] GenerateRandomFloatArray(int size, float minValue = -100f, float maxValue = 100f)
    {
        var array = new float[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = (float)(Random.NextDouble() * (maxValue - minValue) + minValue);
        }
        return array;
    }

    /// <summary>
    /// Creates an array of random double values.
    /// </summary>
    public static double[] GenerateRandomDoubleArray(int size, double minValue = -100.0, double maxValue = 100.0)
    {
        var array = new double[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = Random.NextDouble() * (maxValue - minValue) + minValue;
        }
        return array;
    }

    /// <summary>
    /// Creates an array of random integer values.
    /// </summary>
    public static int[] GenerateRandomIntArray(int size, int minValue = -1000, int maxValue = 1000)
    {
        var array = new int[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = Random.Next(minValue, maxValue);
        }
        return array;
    }

    /// <summary>
    /// Creates a sequential array of integers.
    /// </summary>
    public static int[] GenerateSequentialIntArray(int size, int startValue = 0)
    {
        return Enumerable.Range(startValue, size).ToArray();
    }

    /// <summary>
    /// Creates a matrix of random float values.
    /// </summary>
    public static float[,] GenerateRandomFloatMatrix(int rows, int cols, float minValue = -100f, float maxValue = 100f)
    {
        var matrix = new float[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = (float)(Random.NextDouble() * (maxValue - minValue) + minValue);
            }
        }
        return matrix;
    }
}

/// <summary>
/// Provides CUDA-specific test helper methods.
/// </summary>
public static class CudaTestHelpers
{
    /// <summary>
    /// Calculates optimal grid and block dimensions for CUDA kernel execution.
    /// </summary>
    public static (int grid, int block) CalculateOptimalDimensions(int dataSize, int maxThreadsPerBlock = 256)
    {
        int blockSize = Math.Min(dataSize, maxThreadsPerBlock);
        int gridSize = (dataSize + blockSize - 1) / blockSize;
        return (gridSize, blockSize);
    }

    /// <summary>
    /// Creates kernel execution parameters.
    /// </summary>
    public static object CreateExecutionParameters(int gridSize, int blockSize, int sharedMemoryBytes = 0)
    {
        return new { GridSize = gridSize, BlockSize = blockSize, SharedMemoryBytes = sharedMemoryBytes };
    }

    /// <summary>
    /// Estimates shared memory requirements for a kernel.
    /// </summary>
    public static int EstimateSharedMemoryRequirement(int elementsPerThread, int threadsPerBlock, int bytesPerElement)
    {
        return elementsPerThread * threadsPerBlock * bytesPerElement;
    }
}

/// <summary>
/// Represents a performance measurement for benchmarking.
/// </summary>
public class PerformanceMeasurement
{
    public TimeSpan ElapsedTime { get; set; }
    public long MemoryUsed { get; set; }
    public double Throughput { get; set; }
    public string? Notes { get; set; }

    public PerformanceMeasurement()
    {
        ElapsedTime = TimeSpan.Zero;
        MemoryUsed = 0;
        Throughput = 0.0;
    }
}