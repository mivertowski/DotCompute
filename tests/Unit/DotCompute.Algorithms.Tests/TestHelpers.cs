// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra;

namespace DotCompute.Algorithms.Tests;

/// <summary>
/// Helper methods for test setup and mocking.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a mock CPU accelerator for testing.
    /// </summary>
    public static IAccelerator CreateMockAccelerator()
    {
        var mock = Substitute.For<IAccelerator>();
        mock.Info.Returns(new AcceleratorInfo
        {
            Id = "mock-cpu-0",
            Name = "Mock CPU",
            DeviceType = "CPU",
            Vendor = "Mock",
            MaxComputeUnits = 8
        });
        mock.DeviceType.Returns("CPU");
        mock.IsAvailable.Returns(true);
        return mock;
    }

    /// <summary>
    /// Creates a random matrix for testing.
    /// </summary>
    public static Matrix CreateRandomMatrix(int rows, int cols, Random? random = null)
    {
        random ??= new Random(42); // Fixed seed for reproducibility
        var data = new float[rows * cols];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)(random.NextDouble() * 10);
        }
        return new Matrix(rows, cols, data);
    }

    /// <summary>
    /// Creates an identity matrix.
    /// </summary>
    public static Matrix CreateIdentityMatrix(int size)
    {
        return Matrix.Identity(size);
    }

    /// <summary>
    /// Creates a zero matrix.
    /// </summary>
    public static Matrix CreateZeroMatrix(int rows, int cols)
    {
        return new Matrix(rows, cols);
    }
}
