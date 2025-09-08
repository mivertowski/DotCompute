using System.Numerics;
using System.Runtime.InteropServices;

namespace DotCompute.Tests.Common.Helpers;

/// <summary>
/// Provides utilities for generating test data for various scenarios.
/// Includes methods for creating arrays, matrices, and complex data structures for testing.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Random _random = new(42); // Fixed seed for reproducible tests
    
    #region Array Generation
    
    /// <summary>
    /// Generates an array of random integers within the specified range.
    /// </summary>
    public static int[] GenerateIntArray(int length, int min = 0, int max = 100)
    {
        var array = new int[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = _random.Next(min, max + 1);
        }
        return array;
    }
    
    /// <summary>
    /// Generates an array of random single-precision floating-point numbers.
    /// </summary>
    public static float[] GenerateFloatArray(int length, float min = 0.0f, float max = 1.0f)
    {
        var array = new float[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = min + (float)_random.NextDouble() * (max - min);
        }
        return array;
    }
    
    /// <summary>
    /// Generates an array of random double-precision floating-point numbers.
    /// </summary>
    public static double[] GenerateDoubleArray(int length, double min = 0.0, double max = 1.0)
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
    public static int[] GenerateSequentialIntArray(int length, int start = 0) => Enumerable.Range(start, length).ToArray();


    /// <summary>
    /// Generates an array filled with the specified value.
    /// </summary>
    public static T[] GenerateFilledArray<T>(int length, T value)
    {
        var array = new T[length];
        Array.Fill(array, value);
        return array;
    }
    
    #endregion
    
    #region Matrix Generation
    
    /// <summary>
    /// Generates a 2D matrix of random integers.
    /// </summary>
    public static int[,] GenerateIntMatrix(int rows, int cols, int min = 0, int max = 100)
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
    /// Generates a 2D matrix of random single-precision floating-point numbers.
    /// </summary>
    public static float[,] GenerateFloatMatrix(int rows, int cols, float min = 0.0f, float max = 1.0f)
    {
        var matrix = new float[rows, cols];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = min + (float)_random.NextDouble() * (max - min);
            }
        }
        return matrix;
    }
    
    /// <summary>
    /// Generates an identity matrix of the specified size.
    /// </summary>
    public static float[,] GenerateIdentityMatrix(int size)
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
    public static float[][] GenerateJaggedMatrix(int rows, int cols, float min = 0.0f, float max = 1.0f)
    {
        var matrix = new float[rows][];
        for (var i = 0; i < rows; i++)
        {
            matrix[i] = GenerateFloatArray(cols, min, max);
        }
        return matrix;
    }
    
    #endregion
    
    #region Complex Data Types
    
    /// <summary>
    /// Generates an array of Vector2 structures.
    /// </summary>
    public static Vector2[] GenerateVector2Array(int length, float min = -1.0f, float max = 1.0f)
    {
        var vectors = new Vector2[length];
        for (var i = 0; i < length; i++)
        {
            vectors[i] = new Vector2(
                min + (float)_random.NextDouble() * (max - min),
                min + (float)_random.NextDouble() * (max - min)
            );
        }
        return vectors;
    }
    
    /// <summary>
    /// Generates an array of Vector3 structures.
    /// </summary>
    public static Vector3[] GenerateVector3Array(int length, float min = -1.0f, float max = 1.0f)
    {
        var vectors = new Vector3[length];
        for (var i = 0; i < length; i++)
        {
            vectors[i] = new Vector3(
                min + (float)_random.NextDouble() * (max - min),
                min + (float)_random.NextDouble() * (max - min),
                min + (float)_random.NextDouble() * (max - min)
            );
        }
        return vectors;
    }
    
    /// <summary>
    /// Generates an array of Vector4 structures.
    /// </summary>
    public static Vector4[] GenerateVector4Array(int length, float min = -1.0f, float max = 1.0f)
    {
        var vectors = new Vector4[length];
        for (var i = 0; i < length; i++)
        {
            vectors[i] = new Vector4(
                min + (float)_random.NextDouble() * (max - min),
                min + (float)_random.NextDouble() * (max - min),
                min + (float)_random.NextDouble() * (max - min),
                min + (float)_random.NextDouble() * (max - min)
            );
        }
        return vectors;
    }
    
    #endregion
    
    #region Sparse Data Generation
    
    /// <summary>
    /// Generates a sparse array where only a percentage of elements are non-zero.
    /// </summary>
    public static float[] GenerateSparseFloatArray(int length, double sparsity = 0.1, float min = 1.0f, float max = 10.0f)
    {
        var array = new float[length];
        var nonZeroCount = (int)(length * sparsity);
        
        var indices = Enumerable.Range(0, length).OrderBy(_ => _random.Next()).Take(nonZeroCount);
        
        foreach (var index in indices)
        {
            array[index] = min + (float)_random.NextDouble() * (max - min);
        }
        
        return array;
    }
    
    #endregion
    
    #region Performance Testing Data
    
    /// <summary>
    /// Generates test data suitable for performance benchmarks.
    /// </summary>
    public static class Performance
    {
        /// <summary>
        /// Common sizes for performance testing.
        /// </summary>
        public static readonly int[] CommonSizes = [1024, 4096, 16384, 65536, 262144, 1048576];

        /// <summary>
        /// Generates performance test data for the specified size.
        /// </summary>
        public static (float[] input1, float[] input2) GeneratePerformanceData(int size) => (GenerateFloatArray(size, -100.0f, 100.0f), GenerateFloatArray(size, -100.0f, 100.0f));


        /// <summary>
        /// Generates large matrix data for performance testing.
        /// </summary>
        public static (float[,] matrix1, float[,] matrix2) GenerateLargeMatrixData(int size) => (GenerateFloatMatrix(size, size, -10.0f, 10.0f), GenerateFloatMatrix(size, size, -10.0f, 10.0f));
    }
    
    #endregion
    
    #region Edge Cases
    
    /// <summary>
    /// Generates arrays containing edge case values for floating-point testing.
    /// </summary>
    public static class EdgeCases
    {
        /// <summary>
        /// Gets an array containing various floating-point edge cases.
        /// </summary>
        public static float[] FloatEdgeCases =>
        [
            0.0f, -0.0f, 1.0f, -1.0f,
            float.Epsilon, -float.Epsilon,
            float.MinValue, float.MaxValue,
            float.PositiveInfinity, float.NegativeInfinity,
            float.NaN
        ];
        
        /// <summary>
        /// Gets an array containing various double-precision edge cases.
        /// </summary>
        public static double[] DoubleEdgeCases =>
        [
            0.0, -0.0, 1.0, -1.0,
            double.Epsilon, -double.Epsilon,
            double.MinValue, double.MaxValue,
            double.PositiveInfinity, double.NegativeInfinity,
            double.NaN
        ];
        
        /// <summary>
        /// Gets an array containing integer edge cases.
        /// </summary>
        public static int[] IntEdgeCases =>
        [
            0, 1, -1, int.MinValue, int.MaxValue
        ];
    }
    
    #endregion
    
    #region Memory Alignment
    
    /// <summary>
    /// Generates arrays with specific memory alignment requirements.
    /// </summary>
    public static T[] GenerateAlignedArray<T>(int length, int alignment = 16) where T : struct
    {
        var elementSize = Marshal.SizeOf<T>();
        var alignedLength = (length * elementSize + alignment - 1) / alignment * alignment / elementSize;
        return new T[alignedLength];
    }
    
    #endregion
    
    #region String Generation
    
    /// <summary>
    /// Generates random strings for testing purposes.
    /// </summary>
    public static string GenerateRandomString(int length, string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
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
    public static string[] GenerateRandomStrings(int count, int minLength = 5, int maxLength = 20)
    {
        var strings = new string[count];
        for (var i = 0; i < count; i++)
        {
            var length = _random.Next(minLength, maxLength + 1);
            strings[i] = GenerateRandomString(length);
        }
        return strings;
    }
    
    #endregion
}