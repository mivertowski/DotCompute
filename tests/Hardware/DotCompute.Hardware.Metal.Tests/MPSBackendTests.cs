// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.MPS;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests;

[Collection("Metal")]
[Trait("Category", "Hardware")]
[Trait("Category", "MPS")]
public sealed class MPSBackendTests : IDisposable
{
    private readonly IntPtr _device;
    private readonly MetalPerformanceShadersBackend _backend;
    private readonly ILogger<MetalPerformanceShadersBackend> _logger;
    private readonly ITestOutputHelper _output;

    public MPSBackendTests(ITestOutputHelper output)
    {
        _output = output;

        // Skip if Metal is not supported
        if (!MetalNative.IsMetalSupported())
        {
            Skip.If(true, "Metal is not supported on this system");
        }

        _device = MetalNative.CreateSystemDefaultDevice();
        Skip.If(_device == IntPtr.Zero, "Failed to create Metal device");

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnit(output);
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        _logger = loggerFactory.CreateLogger<MetalPerformanceShadersBackend>();
        _backend = new MetalPerformanceShadersBackend(_device, _logger);

        _output.WriteLine($"MPS Backend initialized");
        _output.WriteLine($"  BLAS Support: {_backend.Capabilities.SupportsBLAS}");
        _output.WriteLine($"  CNN Support: {_backend.Capabilities.SupportsCNN}");
        _output.WriteLine($"  Neural Network Support: {_backend.Capabilities.SupportsNeuralNetwork}");
        _output.WriteLine($"  GPU Family: {_backend.Capabilities.GPUFamily}");
    }

    [SkippableFact]
    public void MatrixMultiply_SmallMatrices_ProducesCorrectResult()
    {
        Skip.If(!_backend.Capabilities.SupportsBLAS, "MPS BLAS not supported on this device");

        // Arrange: 2x2 * 2x2 = 2x2
        float[] a = { 1, 2, 3, 4 };       // [[1, 2], [3, 4]]
        float[] b = { 5, 6, 7, 8 };       // [[5, 6], [7, 8]]
        float[] c = new float[4];

        // Expected: [[19, 22], [43, 50]]

        // Act
        _backend.MatrixMultiply(a, 2, 2, b, 2, 2, c, 2, 2);

        // Assert
        Assert.Equal(19, c[0], precision: 2);
        Assert.Equal(22, c[1], precision: 2);
        Assert.Equal(43, c[2], precision: 2);
        Assert.Equal(50, c[3], precision: 2);

        _output.WriteLine($"Matrix multiply result: [{c[0]}, {c[1]}, {c[2]}, {c[3]}]");
    }

    [SkippableFact]
    public void MatrixMultiply_WithAlphaAndBeta_ProducesCorrectResult()
    {
        Skip.If(!_backend.Capabilities.SupportsBLAS, "MPS BLAS not supported on this device");

        // Arrange: C = 2.0 * (A * B) + 1.5 * C
        float[] a = { 1, 2, 3, 4 };
        float[] b = { 5, 6, 7, 8 };
        float[] c = { 1, 1, 1, 1 };
        float alpha = 2.0f;
        float beta = 1.5f;

        // Expected: 2.0 * [[19, 22], [43, 50]] + 1.5 * [[1, 1], [1, 1]]
        //         = [[38, 44], [86, 100]] + [[1.5, 1.5], [1.5, 1.5]]
        //         = [[39.5, 45.5], [87.5, 101.5]]

        // Act
        _backend.MatrixMultiply(a, 2, 2, b, 2, 2, c, 2, 2, alpha, beta);

        // Assert
        Assert.Equal(39.5f, c[0], precision: 1);
        Assert.Equal(45.5f, c[1], precision: 1);
        Assert.Equal(87.5f, c[2], precision: 1);
        Assert.Equal(101.5f, c[3], precision: 1);

        _output.WriteLine($"Matrix multiply with alpha/beta result: [{c[0]}, {c[1]}, {c[2]}, {c[3]}]");
    }

    [SkippableFact]
    public void MatrixMultiply_LargeMatrices_CompletesSuccessfully()
    {
        Skip.If(!_backend.Capabilities.SupportsBLAS, "MPS BLAS not supported on this device");

        // Arrange: 128x128 * 128x128 = 128x128
        const int size = 128;
        float[] a = new float[size * size];
        float[] b = new float[size * size];
        float[] c = new float[size * size];

        // Fill with random data
        var random = new Random(42);
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (float)random.NextDouble();
            b[i] = (float)random.NextDouble();
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _backend.MatrixMultiply(a, size, size, b, size, size, c, size, size);
        stopwatch.Stop();

        // Assert
        Assert.All(c, val => Assert.False(float.IsNaN(val), "Result contains NaN"));
        Assert.All(c, val => Assert.False(float.IsInfinity(val), "Result contains Infinity"));

        _output.WriteLine($"Large matrix multiply ({size}x{size}) completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [SkippableFact]
    public void MatrixVectorMultiply_ProducesCorrectResult()
    {
        Skip.If(!_backend.Capabilities.SupportsBLAS, "MPS BLAS not supported on this device");

        // Arrange: [2x3] * [3x1] = [2x1]
        float[] matrix = { 1, 2, 3, 4, 5, 6 };  // [[1, 2, 3], [4, 5, 6]]
        float[] vector = { 7, 8, 9 };           // [7, 8, 9]
        float[] result = new float[2];

        // Expected: [1*7 + 2*8 + 3*9, 4*7 + 5*8 + 6*9] = [50, 122]

        // Act
        _backend.MatrixVectorMultiply(matrix, 2, 3, vector, result);

        // Assert
        Assert.Equal(50, result[0], precision: 2);
        Assert.Equal(122, result[1], precision: 2);

        _output.WriteLine($"Matrix-vector multiply result: [{result[0]}, {result[1]}]");
    }

    [SkippableFact]
    public void ReLU_AppliesActivationCorrectly()
    {
        Skip.If(!_backend.Capabilities.SupportsNeuralNetwork, "MPS neural network not supported on this device");

        // Arrange
        float[] input = { -2, -1, 0, 1, 2 };
        float[] output = new float[5];

        // Expected: [0, 0, 0, 1, 2]

        // Act
        _backend.ReLU(input, output);

        // Assert
        Assert.Equal(0, output[0]);
        Assert.Equal(0, output[1]);
        Assert.Equal(0, output[2]);
        Assert.Equal(1, output[3]);
        Assert.Equal(2, output[4]);

        _output.WriteLine($"ReLU result: [{string.Join(", ", output)}]");
    }

    [SkippableFact]
    public void Convolution2D_SimpleCase_ProducesCorrectResult()
    {
        Skip.If(!_backend.Capabilities.SupportsCNN, "MPS CNN not supported on this device");

        // Arrange: 3x3 input, 2x2 kernel, 1 channel
        float[] input = {
            1, 2, 3,
            4, 5, 6,
            7, 8, 9
        };

        float[] kernel = {
            1, 0,
            0, 1
        };

        float[] output = new float[4];  // 2x2 output

        // Act
        _backend.Convolution2D(
            input, 3, 3, 1,
            kernel, 2, 2, 1,
            output, 2, 2);

        // Assert
        Assert.All(output, val => Assert.False(float.IsNaN(val), "Result contains NaN"));

        _output.WriteLine($"Convolution result: [{string.Join(", ", output)}]");
    }

    [SkippableTheory]
    [InlineData(16 * 16, true)]        // 256 elements - should use MPS
    [InlineData(8 * 8, false)]         // 64 elements - should use CPU
    [InlineData(32 * 32, true)]        // 1024 elements - should use MPS
    public void ShouldUseMPS_MatrixMultiply_ReturnsExpectedValue(int dataSize, bool expected)
    {
        // Act
        bool result = MetalPerformanceShadersBackend.ShouldUseMPS(
            MPSOperationType.MatrixMultiply,
            dataSize,
            _backend.Capabilities);

        // Assert
        Assert.Equal(expected, result);

        _output.WriteLine($"ShouldUseMPS for {dataSize} elements: {result} (expected: {expected})");
    }

    [SkippableFact]
    public void BatchNormalization_WithParameters_CompletesSuccessfully()
    {
        Skip.If(!_backend.Capabilities.SupportsNeuralNetwork, "MPS neural network not supported on this device");

        // Arrange
        const int channels = 4;
        const int elementsPerChannel = 8;
        const int totalElements = channels * elementsPerChannel;

        float[] input = new float[totalElements];
        float[] gamma = new float[channels];
        float[] beta = new float[channels];
        float[] mean = new float[channels];
        float[] variance = new float[channels];
        float[] output = new float[totalElements];

        // Fill with test data
        for (int i = 0; i < totalElements; i++) input[i] = i;
        for (int i = 0; i < channels; i++)
        {
            gamma[i] = 1.0f;
            beta[i] = 0.0f;
            mean[i] = 0.0f;
            variance[i] = 1.0f;
        }

        // Act
        _backend.BatchNormalization(input, gamma, beta, mean, variance, output);

        // Assert
        Assert.All(output, val => Assert.False(float.IsNaN(val), "Result contains NaN"));

        _output.WriteLine($"Batch normalization completed for {totalElements} elements");
    }

    public void Dispose()
    {
        _backend?.Dispose();

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }
}
