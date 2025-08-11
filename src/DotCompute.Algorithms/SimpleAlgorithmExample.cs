// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms;

/// <summary>
/// Example of a simple algorithm that demonstrates the plugin pattern.
/// </summary>
public partial class VectorAddAlgorithm
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger<VectorAddAlgorithm> _logger;
    
    [LoggerMessage(Level = LogLevel.Trace, Message = "Using accelerator: {AcceleratorName}")]
    private partial void LogUsingAccelerator(string acceleratorName);

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorAddAlgorithm"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="logger">The logger instance.</param>
    public VectorAddAlgorithm(IAccelerator accelerator, ILogger<VectorAddAlgorithm> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds two vectors element-wise.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The result vector.</returns>
    public async Task<float[]> AddAsync(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length.");
        }

        var length = a.Length;
        var result = new float[length];

        // Add vectors using the accelerator (CPU for now)
        var deviceInfo = _accelerator.Info;
        LogUsingAccelerator(deviceInfo.Name);

        // For now, simple CPU implementation
        await Task.Run(() =>
        {
            for (var i = 0; i < length; i++)
            {
                result[i] = a[i] + b[i];
            }
        });

        return result;
    }

    /// <summary>
    /// Computes the dot product of two vectors.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The dot product.</returns>
    public async Task<float> DotProductAsync(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length.");
        }

        var length = a.Length;
        var result = 0.0f;

        // Compute dot product using the accelerator (CPU for now)
        var deviceInfo = _accelerator.Info;
        LogUsingAccelerator(deviceInfo.Name);

        // For now, simple CPU implementation
        await Task.Run(() =>
        {
            for (var i = 0; i < length; i++)
            {
                result += a[i] * b[i];
            }
        });

        return result;
    }
}

/// <summary>
/// Example of a matrix operations algorithm.
/// </summary>
public partial class MatrixOperations
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger<MatrixOperations> _logger;
    
    [LoggerMessage(Level = LogLevel.Trace, Message = "Using accelerator: {AcceleratorName}")]
    private partial void LogUsingAccelerator(string acceleratorName);

    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixOperations"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="logger">The logger instance.</param>
    public MatrixOperations(IAccelerator accelerator, ILogger<MatrixOperations> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Multiplies two matrices.
    /// </summary>
    /// <param name="a">The first matrix (m x n).</param>
    /// <param name="b">The second matrix (n x p).</param>
    /// <param name="m">Number of rows in first matrix.</param>
    /// <param name="n">Number of columns in first matrix / rows in second matrix.</param>
    /// <param name="p">Number of columns in second matrix.</param>
    /// <returns>The result matrix (m x p).</returns>
    public async Task<float[]> MultiplyAsync(float[] a, float[] b, int m, int n, int p)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != m * n)
        {
            throw new ArgumentException("Matrix A dimensions don't match array length.");
        }

        if (b.Length != n * p)
        {
            throw new ArgumentException("Matrix B dimensions don't match array length.");
        }

        var result = new float[m * p];

        // Multiply matrices using the accelerator (CPU for now)
        var deviceInfo = _accelerator.Info;
        LogUsingAccelerator(deviceInfo.Name);

        // Simple CPU implementation for now
        await Task.Run(() =>
        {
            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < p; j++)
                {
                    float sum = 0;
                    for (var k = 0; k < n; k++)
                    {
                        sum += a[i * n + k] * b[k * p + j];
                    }
                    result[i * p + j] = sum;
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Transposes a matrix.
    /// </summary>
    /// <param name="matrix">The matrix to transpose.</param>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <returns>The transposed matrix.</returns>
    public async Task<float[]> TransposeAsync(float[] matrix, int rows, int cols)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (matrix.Length != rows * cols)
        {
            throw new ArgumentException("Matrix dimensions don't match array length.");
        }

        var result = new float[rows * cols];

        // Transpose matrix using the accelerator (CPU for now)
        var deviceInfo = _accelerator.Info;
        LogUsingAccelerator(deviceInfo.Name);

        // Simple CPU implementation for now
        await Task.Run(() =>
        {
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[j * rows + i] = matrix[i * cols + j];
                }
            }
        });

        return result;
    }
}