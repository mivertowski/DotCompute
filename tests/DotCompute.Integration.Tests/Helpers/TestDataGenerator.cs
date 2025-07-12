// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Integration.Tests.Helpers;

public static class TestDataGenerator
{
    public static float[] GenerateRandomFloats(int count, Random? random = null, float min = -1.0f, float max = 1.0f)
    {
        random ??= new Random(42); // Deterministic for testing
        var data = new float[count];
        
        for (int i = 0; i < count; i++)
        {
            data[i] = min + (float)random.NextDouble() * (max - min);
        }
        
        return data;
    }

    public static float[] GenerateSequentialFloats(int count, float start = 0.0f, float step = 1.0f)
    {
        var data = new float[count];
        
        for (int i = 0; i < count; i++)
        {
            data[i] = start + i * step;
        }
        
        return data;
    }

    public static float[] GenerateSinusoidalData(int count, float amplitude = 1.0f, float frequency = 1.0f, float phase = 0.0f)
    {
        var data = new float[count];
        
        for (int i = 0; i < count; i++)
        {
            var x = (float)i / count * 2.0f * MathF.PI * frequency;
            data[i] = amplitude * MathF.Sin(x + phase);
        }
        
        return data;
    }

    public static int[] GenerateRandomInts(int count, Random? random = null, int min = 0, int max = 100)
    {
        random ??= new Random(42);
        var data = new int[count];
        
        for (int i = 0; i < count; i++)
        {
            data[i] = random.Next(min, max);
        }
        
        return data;
    }

    public static float[,] GenerateMatrix(int rows, int cols, Random? random = null, float min = -1.0f, float max = 1.0f)
    {
        random ??= new Random(42);
        var matrix = new float[rows, cols];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = min + (float)random.NextDouble() * (max - min);
            }
        }
        
        return matrix;
    }

    public static float[] FlattenMatrix(float[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var flattened = new float[rows * cols];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                flattened[i * cols + j] = matrix[i, j];
            }
        }
        
        return flattened;
    }

    public static float[,] UnflattenMatrix(float[] flattened, int rows, int cols)
    {
        if (flattened.Length != rows * cols)
            throw new ArgumentException("Array length doesn't match matrix dimensions");
            
        var matrix = new float[rows, cols];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = flattened[i * cols + j];
            }
        }
        
        return matrix;
    }

    public static (float[] real, float[] imaginary) GenerateComplexData(int count, Random? random = null)
    {
        random ??= new Random(42);
        var real = new float[count];
        var imaginary = new float[count];
        
        for (int i = 0; i < count; i++)
        {
            real[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            imaginary[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
        
        return (real, imaginary);
    }

    public static float[] GenerateGaussianNoise(int count, Random? random = null, float mean = 0.0f, float stdDev = 1.0f)
    {
        random ??= new Random(42);
        var data = new float[count];
        
        for (int i = 0; i < count; i++)
        {
            data[i] = (float)random.NextGaussian(mean, stdDev);
        }
        
        return data;
    }

    public static byte[] GenerateImageData(int width, int height, int channels = 3, Random? random = null)
    {
        random ??= new Random(42);
        var data = new byte[width * height * channels];
        
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)random.Next(0, 256);
        }
        
        return data;
    }

    public static float[] NormalizeToFloat(byte[] byteData)
    {
        var floatData = new float[byteData.Length];
        
        for (int i = 0; i < byteData.Length; i++)
        {
            floatData[i] = byteData[i] / 255.0f;
        }
        
        return floatData;
    }

    public static byte[] DenormalizeToBytes(float[] floatData)
    {
        var byteData = new byte[floatData.Length];
        
        for (int i = 0; i < floatData.Length; i++)
        {
            byteData[i] = (byte)Math.Clamp(floatData[i] * 255.0f, 0, 255);
        }
        
        return byteData;
    }
}