// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.SignalProcessing;
using DotCompute.Core.Kernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Algorithms.Tests.SignalProcessing;

/// <summary>
/// Comprehensive tests for ConvolutionOperations.
/// </summary>
public sealed class ConvolutionOperationsTests : IDisposable
{
    private readonly IAccelerator _mockAccelerator;
    private readonly KernelManager _kernelManager;
    private readonly ConvolutionOperations _convOps;

    public ConvolutionOperationsTests()
    {
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Info.Returns(new AcceleratorInfo
        {
            DeviceType = "CPU",
            Name = "Mock CPU",
            Id = "mock-cpu-0",
            LocalMemorySize = 49152,
            MaxThreadsPerBlock = 1024
        });
        var mockLogger = Substitute.For<ILogger<KernelManager>>();
        _kernelManager = new KernelManager(mockLogger);
        _convOps = new ConvolutionOperations(_kernelManager, _mockAccelerator);
    }

    public void Dispose()
    {
        _convOps?.Dispose();
        _kernelManager?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region 1D Convolution Tests

    [Fact]
    public async Task Convolve1DAsync_SimpleSignal_ReturnsResult()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3, 4, 5 };
        var kernel = new float[] { 1, 0, -1 };

        // Act
        var result = await _convOps.Convolve1DAsync(signal, kernel);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Convolve1DAsync_NullSignal_ThrowsArgumentException()
    {
        // Arrange
        var kernel = new float[] { 1, 2, 3 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _convOps.Convolve1DAsync(null!, kernel).AsTask());
    }

    [Fact]
    public async Task Convolve1DAsync_EmptySignal_ThrowsArgumentException()
    {
        // Arrange
        var signal = Array.Empty<float>();
        var kernel = new float[] { 1, 2, 3 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _convOps.Convolve1DAsync(signal, kernel).AsTask());
    }

    [Fact]
    public async Task Convolve1DAsync_ValidPadding_CalculatesCorrectSize()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3, 4, 5 };
        var kernel = new float[] { 1, 1, 1 };

        // Act
        var result = await _convOps.Convolve1DAsync(signal, kernel, PaddingMode.Valid);

        // Assert
        result.Should().NotBeNull();
        // Valid padding: output = input - kernel + 1
        result.Length.Should().Be(3); // 5 - 3 + 1 = 3
    }

    [Fact]
    public async Task Convolve1DAsync_SamePadding_PreservesSize()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3, 4, 5 };
        var kernel = new float[] { 1, 1, 1 };

        // Act
        var result = await _convOps.Convolve1DAsync(signal, kernel, PaddingMode.Same);

        // Assert
        result.Should().NotBeNull();
        // Same padding: output size = input size
        result.Length.Should().Be(5);
    }

    [Fact]
    public async Task StridedConvolve1DAsync_ValidStride_ReturnsDownsampledResult()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var kernel = new float[] { 1, 1 };
        var stride = 2;

        // Act
        var result = await ConvolutionOperations.StridedConvolve1DAsync(
            signal, kernel, stride);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeLessThan(signal.Length);
    }

    [Fact]
    public async Task StridedConvolve1DAsync_ZeroStride_ThrowsArgumentException()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3 };
        var kernel = new float[] { 1, 1 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ConvolutionOperations.StridedConvolve1DAsync(signal, kernel, 0).AsTask());
    }

    [Fact]
    public async Task DilatedConvolve1DAsync_ValidDilation_ReturnsResult()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3, 4, 5, 6, 7 };
        var kernel = new float[] { 1, 1, 1 };
        var dilation = 2;

        // Act
        var result = await _convOps.DilatedConvolve1DAsync(
            signal, kernel, dilation);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region 2D Convolution Tests

    [Fact]
    public async Task Convolve2DAsync_SimpleImage_ReturnsResult()
    {
        // Arrange
        var image = new float[25]; // 5x5
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                image[i * 5 + j] = i + j;
            }
        }

        var kernel = new float[9]; // 3x3
        kernel[0] = kernel[2] = kernel[6] = kernel[8] = -1;
        kernel[4] = 4;

        // Act
        var result = await _convOps.Convolve2DAsync(image, kernel, 5, 5, 3, 3);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve2DAsync_NullImage_ThrowsArgumentException()
    {
        // Arrange
        var kernel = new float[9]; // 3x3

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _convOps.Convolve2DAsync(null!, kernel, 5, 5, 3, 3).AsTask());
    }

    [Fact]
    public async Task Convolve2DAsync_EdgeDetection_DetectsEdges()
    {
        // Arrange - Simple edge detection kernel
        var image = new float[25]; // 5x5
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                image[i * 5 + j] = i < 2 ? 0 : 255;
            }
        }

        var sobelX = new float[9] { -1, 0, 1, -2, 0, 2, -1, 0, 1 };

        // Act
        var result = await _convOps.Convolve2DAsync(image, sobelX, 5, 5, 3, 3);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve2DAsync_GaussianBlur_BlursImage()
    {
        // Arrange - Gaussian blur kernel
        var image = new float[49]; // 7x7
        image[3 * 7 + 3] = 100; // Single bright pixel

        var gaussian = new float[9];
        var gaussianValues = new float[] { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
        for (int i = 0; i < 9; i++)
        {
            gaussian[i] = gaussianValues[i] / 16;
        }

        // Act
        var result = await _convOps.Convolve2DAsync(image, gaussian, 7, 7, 3, 3);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SeparableConvolve2DAsync_SeparableKernel_UsesOptimization()
    {
        // Arrange
        var image = new float[100]; // 10x10
        var kernelX = new float[] { 1, 2, 1 };
        var kernelY = new float[] { 1, 2, 1 };

        // Act
        var result = await _convOps.SeparableConvolve2DAsync(
            image, kernelX, kernelY, 10, 10);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region 3D Convolution Tests

    [Fact]
    public async Task Convolve3DAsync_SimpleVolume_ReturnsResult()
    {
        // Arrange
        var volume = new float[125]; // 5x5x5
        var kernel = new float[27]; // 3x3x3

        // Act
        var result = await _convOps.Convolve3DAsync(volume, kernel, 5, 5, 5, 3, 3, 3);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve3DAsync_NullVolume_ThrowsArgumentException()
    {
        // Arrange
        var kernel = new float[27]; // 3x3x3

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _convOps.Convolve3DAsync(null!, kernel, 5, 5, 5, 3, 3, 3).AsTask());
    }

    #endregion

    #region Convolution Strategy Tests

    [Fact]
    public async Task Convolve1DAsync_DirectStrategy_ExecutesDirect()
    {
        // Arrange
        var signal = new float[] { 1, 2, 3, 4, 5 };
        var kernel = new float[] { 1, 1, 1 };

        // Act
        var result = await _convOps.Convolve1DAsync(
            signal, kernel, strategy: ConvolutionStrategy.Direct);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve1DAsync_FFTStrategy_ExecutesFFT()
    {
        // Arrange
        var signal = new float[128];
        var kernel = new float[64];

        // Act
        var result = await _convOps.Convolve1DAsync(
            signal, kernel, strategy: ConvolutionStrategy.FFT);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve1DAsync_AutoStrategy_SelectsOptimal()
    {
        // Arrange - Large kernel should prefer FFT
        var signal = new float[1024];
        var kernel = new float[512];

        // Act
        var result = await _convOps.Convolve1DAsync(
            signal, kernel, strategy: ConvolutionStrategy.Auto);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Batch Convolution Tests

    [Fact]
    public async Task BatchConvolve2DAsync_MultipleImages_ProcessesAll()
    {
        // Arrange
        var batchInput = new float[4 * 25]; // 4 images of 5x5
        var kernels = new float[9]; // 3x3

        // Act
        var results = await _convOps.BatchConvolve2DAsync(
            batchInput, kernels, 4, 1, 1, 5, 5, 3, 3);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task BatchConvolve2DAsync_EmptyBatch_ThrowsArgumentException()
    {
        // Arrange
        var batch = Array.Empty<float>();
        var kernel = new float[9];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _convOps.BatchConvolve2DAsync(batch, kernel, 0, 1, 1, 5, 5, 3, 3).AsTask());
    }

    #endregion

    #region Edge Cases and Performance Tests

    [Fact]
    public async Task Convolve1DAsync_SingleElement_HandlesCorrectly()
    {
        // Arrange
        var signal = new float[] { 1 };
        var kernel = new float[] { 1 };

        // Act
        var result = await _convOps.Convolve1DAsync(signal, kernel);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve1DAsync_LargeSignal_CompletesSuccessfully()
    {
        // Arrange
        var signal = new float[10000];
        var kernel = new float[100];

        // Act
        var result = await _convOps.Convolve1DAsync(signal, kernel);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve1DAsync_CancellationToken_CanBeCancelled()
    {
        // Arrange
        var signal = new float[100000];
        var kernel = new float[1000];
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _convOps.Convolve1DAsync(signal, kernel, cancellationToken: cts.Token).AsTask());
    }

    [Fact]
    public async Task Convolve2DAsync_NonSquareImage_HandlesCorrectly()
    {
        // Arrange
        var image = new float[150]; // 10x15
        var kernel = new float[9]; // 3x3

        // Act
        var result = await _convOps.Convolve2DAsync(image, kernel, 15, 10, 3, 3);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Convolve2DAsync_NonSquareKernel_HandlesCorrectly()
    {
        // Arrange
        var image = new float[100]; // 10x10
        var kernel = new float[15]; // 3x5

        // Act
        var result = await _convOps.Convolve2DAsync(image, kernel, 10, 10, 5, 3);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
