using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DotCompute.Tests.Common;

/// <summary>
/// Utility class for generating test data.
/// </summary>
public static class TestDataGenerators
{
    /// <summary>
    /// Generates a random matrix.
    /// </summary>
    public static float[,] GenerateRandomMatrix(int rows, int cols, float min = -1.0f, float max = 1.0f, int seed = 42)
    {
        var random = new Random(seed);
        var matrix = new float[rows, cols];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = min + (float)(random.NextDouble() * (max - min));
            }
        }
        
        return matrix;
    }
    
    /// <summary>
    /// Generates a random vector.
    /// </summary>
    public static float[] GenerateRandomVector(int size, float min = -1.0f, float max = 1.0f, int seed = 42)
    {
        var random = new Random(seed);
        var vector = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            vector[i] = min + (float)(random.NextDouble() * (max - min));
        }
        
        return vector;
    }
    
    /// <summary>
    /// Generates sequential data.
    /// </summary>
    public static T[] GenerateSequentialData<T>(int count, Func<int, T> generator)
    {
        var data = new T[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = generator(i);
        }
        return data;
    }
    
    /// <summary>
    /// Generates test pattern data (checkerboard, gradient, etc.).
    /// </summary>
    public static float[,] GeneratePatternData(int width, int height, PatternType pattern)
    {
        var data = new float[height, width];
        
        switch (pattern)
        {
            case PatternType.Checkerboard:
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        data[y, x] = ((x + y) % 2 == 0) ? 1.0f : 0.0f;
                    }
                }
                break;
                
            case PatternType.Gradient:
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        data[y, x] = (float)x / width;
                    }
                }
                break;
                
            case PatternType.Sine:
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        data[y, x] = (float)Math.Sin(2 * Math.PI * x / width) * 
                                    (float)Math.Sin(2 * Math.PI * y / height);
                    }
                }
                break;
                
            case PatternType.Gaussian:
                float centerX = width / 2.0f;
                float centerY = height / 2.0f;
                float sigma = Math.Min(width, height) / 6.0f;
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float dist2 = dx * dx + dy * dy;
                        data[y, x] = (float)Math.Exp(-dist2 / (2 * sigma * sigma));
                    }
                }
                break;
        }
        
        return data;
    }
    
    /// <summary>
    /// Generates sparse data with specified density.
    /// </summary>
    public static (int[] indices, float[] values) GenerateSparseData(int size, float density, int seed = 42)
    {
        var random = new Random(seed);
        var nonZeroCount = (int)(size * density);
        
        var indices = new HashSet<int>();
        while (indices.Count < nonZeroCount)
        {
            indices.Add(random.Next(size));
        }
        
        var sortedIndices = indices.OrderBy(i => i).ToArray();
        var values = new float[nonZeroCount];
        
        for (int i = 0; i < nonZeroCount; i++)
        {
            values[i] = (float)(random.NextDouble() * 2 - 1);
        }
        
        return (sortedIndices, values);
    }
    
    /// <summary>
    /// Generates complex number data.
    /// </summary>
    public static Complex[] GenerateComplexData(int count, int seed = 42)
    {
        var random = new Random(seed);
        var data = new Complex[count];
        
        for (int i = 0; i < count; i++)
        {
            data[i] = new Complex(
                random.NextDouble() * 2 - 1,
                random.NextDouble() * 2 - 1);
        }
        
        return data;
    }
    
    /// <summary>
    /// Validates matrix multiplication result.
    /// </summary>
    public static bool ValidateMatrixMultiplication(
        float[,] a, 
        float[,] b, 
        float[,] c, 
        float tolerance = 1e-5f)
    {
        int m = a.GetLength(0);
        int k = a.GetLength(1);
        int n = b.GetLength(1);
        
        if (b.GetLength(0) != k || c.GetLength(0) != m || c.GetLength(1) != n)
        {
            return false;
        }
        
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                float expected = 0;
                for (int l = 0; l < k; l++)
                {
                    expected += a[i, l] * b[l, j];
                }
                
                if (Math.Abs(c[i, j] - expected) > tolerance)
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Validates vector addition result.
    /// </summary>
    public static bool ValidateVectorAddition(
        float[] a,
        float[] b,
        float[] c,
        float tolerance = 1e-6f)
    {
        if (a.Length != b.Length || a.Length != c.Length)
        {
            return false;
        }
        
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(c[i] - (a[i] + b[i])) > tolerance)
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Calculates checksum for data validation.
    /// </summary>
    public static double CalculateChecksum<T>(T[] data) where T : unmanaged
    {
        double sum = 0;
        
        if (typeof(T) == typeof(float))
        {
            var floatData = data as float[];
            sum = floatData!.Sum(x => Math.Abs(x));
        }
        else if (typeof(T) == typeof(double))
        {
            var doubleData = data as double[];
            sum = doubleData!.Sum(x => Math.Abs(x));
        }
        else if (typeof(T) == typeof(int))
        {
            var intData = data as int[];
            sum = intData!.Sum(x => Math.Abs(x));
        }
        else
        {
            // Generic implementation
            foreach (var item in data)
            {
                sum += Convert.ToDouble(item);
            }
        }
        
        return sum;
    }
}

/// <summary>
/// Pattern types for test data generation.
/// </summary>
public enum PatternType
{
    Checkerboard,
    Gradient,
    Sine,
    Gaussian
}