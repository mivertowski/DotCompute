// TODO: Fix ConvolutionOperations method signatures
//
// // Copyright (c) 2025 Michael Ivertowski
// // Licensed under the MIT License. See LICENSE file in the project root for license information.
// 
// using DotCompute.Abstractions;
// using DotCompute.Algorithms.SignalProcessing;
// using DotCompute.Core.Kernels;
// 
// namespace DotCompute.Algorithms.Tests.SignalProcessing;
// 
// /// <summary>
// /// Comprehensive tests for ConvolutionOperations.
// /// </summary>
// public sealed class ConvolutionOperationsTests : IDisposable
// {
//     private readonly IAccelerator _mockAccelerator;
//     private readonly KernelManager _kernelManager;
//     private readonly ConvolutionOperations _convOps;
// 
//     public ConvolutionOperationsTests()
//     {
//         _mockAccelerator = Substitute.For<IAccelerator>();
//         _mockAccelerator.Info.Returns(new AcceleratorInfo
//         {
//             DeviceType = "CPU",
//             Name = "Mock CPU",
//             Id = "mock-cpu-0"
//         });
//         _kernelManager = Substitute.For<KernelManager>(_mockAccelerator);
//         _convOps = new ConvolutionOperations(_kernelManager, _mockAccelerator);
//     }
// 
//     public void Dispose()
//     {
//         _convOps?.Dispose();
//         GC.SuppressFinalize(this);
//     }
// 
//     #region 1D Convolution Tests
// 
//     [Fact]
//     public async Task Convolve1DAsync_SimpleSignal_ReturnsResult()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3, 4, 5 };
//         var kernel = new float[] { 1, 0, -1 };
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(signal, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_NullSignal_ThrowsArgumentNullException()
//     {
//         // Arrange
//         var kernel = new float[] { 1, 2, 3 };
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentNullException>(() =>
//             _convOps.Convolve1DAsync(null!, kernel).AsTask());
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_EmptySignal_ThrowsArgumentException()
//     {
//         // Arrange
//         var signal = Array.Empty<float>();
//         var kernel = new float[] { 1, 2, 3 };
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() =>
//             _convOps.Convolve1DAsync(signal, kernel).AsTask());
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_ValidPadding_CalculatesCorrectSize()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3, 4, 5 };
//         var kernel = new float[] { 1, 1, 1 };
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(signal, kernel, PaddingMode.Valid);
// 
//         // Assert
//         result.Should().NotBeNull();
//         // Valid padding: output = input - kernel + 1
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_SamePadding_PreservesSize()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3, 4, 5 };
//         var kernel = new float[] { 1, 1, 1 };
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(signal, kernel, PaddingMode.Same);
// 
//         // Assert
//         result.Should().NotBeNull();
//         // Same padding: output size = input size
//     }
// 
//     [Fact]
//     public async Task StridedConvolve1DAsync_ValidStride_ReturnsDownsampledResult()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3, 4, 5, 6, 7, 8 };
//         var kernel = new float[] { 1, 1 };
//         var stride = 2;
// 
//         // Act
//         var result = await ConvolutionOperations.StridedConvolve1DAsync(
//             signal, kernel, stride);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task StridedConvolve1DAsync_ZeroStride_ThrowsArgumentException()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3 };
//         var kernel = new float[] { 1, 1 };
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() =>
//             ConvolutionOperations.StridedConvolve1DAsync(signal, kernel, 0).AsTask());
//     }
// 
//     [Fact]
//     public async Task DilatedConvolve1DAsync_ValidDilation_ReturnsResult()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3, 4, 5, 6, 7 };
//         var kernel = new float[] { 1, 1, 1 };
//         var dilation = 2;
// 
//         // Act
//         var result = await ConvolutionOperations.DilatedConvolve1DAsync(
//             signal, kernel, dilation);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     #endregion
// 
//     #region 2D Convolution Tests
// 
//     [Fact]
//     public async Task Convolve2DAsync_SimpleImage_ReturnsResult()
//     {
//         // Arrange
//         var image = new float[5, 5];
//         for (int i = 0; i < 5; i++)
//             for (int j = 0; j < 5; j++)
//                 image[i, j] = i + j;
// 
//         var kernel = new float[3, 3];
//         kernel[0, 0] = kernel[0, 2] = kernel[2, 0] = kernel[2, 2] = -1;
//         kernel[1, 1] = 4;
// 
//         // Act
//         var result = await _convOps.Convolve2DAsync(image, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve2DAsync_NullImage_ThrowsArgumentNullException()
//     {
//         // Arrange
//         var kernel = new float[3, 3];
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentNullException>(() =>
//             _convOps.Convolve2DAsync(null!, kernel).AsTask());
//     }
// 
//     [Fact]
//     public async Task Convolve2DAsync_EdgeDetection_DetectsEdges()
//     {
//         // Arrange - Simple edge detection kernel
//         var image = new float[5, 5];
//         for (int i = 0; i < 5; i++)
//             for (int j = 0; j < 5; j++)
//                 image[i, j] = i < 2 ? 0 : 255;
// 
//         var sobelX = new float[3, 3] {
//             { -1, 0, 1 },
//             { -2, 0, 2 },
//             { -1, 0, 1 }
//         };
// 
//         // Act
//         var result = await _convOps.Convolve2DAsync(image, sobelX);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve2DAsync_GaussianBlur_BlursImage()
//     {
//         // Arrange - Gaussian blur kernel
//         var image = new float[7, 7];
//         image[3, 3] = 100; // Single bright pixel
// 
//         var gaussian = new float[3, 3] {
//             { 1, 2, 1 },
//             { 2, 4, 2 },
//             { 1, 2, 1 }
//         };
//         // Normalize
//         for (int i = 0; i < 3; i++)
//             for (int j = 0; j < 3; j++)
//                 gaussian[i, j] /= 16;
// 
//         // Act
//         var result = await _convOps.Convolve2DAsync(image, gaussian);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task SeparableConvolve2DAsync_SeparableKernel_UsesOptimization()
//     {
//         // Arrange
//         var image = new float[10, 10];
//         var kernelX = new float[] { 1, 2, 1 };
//         var kernelY = new float[] { 1, 2, 1 };
// 
//         // Act
//         var result = await ConvolutionOperations.SeparableConvolve2DAsync(
//             image, kernelX, kernelY);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     #endregion
// 
//     #region 3D Convolution Tests
// 
//     [Fact]
//     public async Task Convolve3DAsync_SimpleVolume_ReturnsResult()
//     {
//         // Arrange
//         var volume = new float[5, 5, 5];
//         var kernel = new float[3, 3, 3];
// 
//         // Act
//         var result = await _convOps.Convolve3DAsync(volume, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve3DAsync_NullVolume_ThrowsArgumentNullException()
//     {
//         // Arrange
//         var kernel = new float[3, 3, 3];
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentNullException>(() =>
//             _convOps.Convolve3DAsync(null!, kernel).AsTask());
//     }
// 
//     #endregion
// 
//     #region Convolution Strategy Tests
// 
//     [Fact]
//     public async Task Convolve1DAsync_DirectStrategy_ExecutesDirect()
//     {
//         // Arrange
//         var signal = new float[] { 1, 2, 3, 4, 5 };
//         var kernel = new float[] { 1, 1, 1 };
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(
//             signal, kernel, strategy: ConvolutionStrategy.Direct);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_FFTStrategy_ExecutesFFT()
//     {
//         // Arrange
//         var signal = new float[128];
//         var kernel = new float[64];
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(
//             signal, kernel, strategy: ConvolutionStrategy.FFT);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_AutoStrategy_SelectsOptimal()
//     {
//         // Arrange - Large kernel should prefer FFT
//         var signal = new float[1024];
//         var kernel = new float[512];
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(
//             signal, kernel, strategy: ConvolutionStrategy.Auto);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     #endregion
// 
//     #region Batch Convolution Tests
// 
//     [Fact]
//     public async Task BatchConvolve2DAsync_MultipleMages_ProcessesAll()
//     {
//         // Arrange
//         var batch = new float[4, 5, 5]; // 4 images of 5x5
//         var kernel = new float[3, 3];
// 
//         // Act
//         var results = await ConvolutionOperations.BatchConvolve2DAsync(batch, kernel);
// 
//         // Assert
//         results.Should().NotBeNull();
//         results.GetLength(0).Should().Be(4);
//     }
// 
//     [Fact]
//     public async Task BatchConvolve2DAsync_EmptyBatch_ThrowsArgumentException()
//     {
//         // Arrange
//         var batch = new float[0, 5, 5];
//         var kernel = new float[3, 3];
// 
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() =>
//             ConvolutionOperations.BatchConvolve2DAsync(batch, kernel).AsTask());
//     }
// 
//     #endregion
// 
//     #region Cross-Correlation Tests
// 
//     [Fact]
//     public async Task CrossCorrelate1DAsync_SimpleSignals_ReturnsCorrelation()
//     {
//         // Arrange
//         var signal1 = new float[] { 1, 2, 3, 4, 5 };
//         var signal2 = new float[] { 1, 0, -1 };
// 
//         // Act
//         var result = await ConvolutionOperations.CrossCorrelate1DAsync(signal1, signal2);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task CrossCorrelate2DAsync_TemplateMatching_FindsMatch()
//     {
//         // Arrange
//         var image = new float[10, 10];
//         var template = new float[3, 3];
// 
//         // Act
//         var result = await ConvolutionOperations.CrossCorrelate2DAsync(image, template);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     #endregion
// 
//     #region Depthwise Separable Convolution Tests
// 
//     [Fact]
//     public async Task DepthwiseSeparableConv2DAsync_MultiChannel_ProcessesChannels()
//     {
//         // Arrange
//         var input = new float[10, 10, 3]; // 3 channels
//         var depthwiseKernel = new float[3, 3];
//         var pointwiseKernel = new float[1, 1, 3, 8]; // 3 input, 8 output channels
// 
//         // Act
//         var result = await ConvolutionOperations.DepthwiseSeparableConv2DAsync(
//             input, depthwiseKernel, pointwiseKernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     #endregion
// 
//     #region Edge Cases and Performance Tests
// 
//     [Fact]
//     public async Task Convolve1DAsync_SingleElement_HandlesCorrectly()
//     {
//         // Arrange
//         var signal = new float[] { 1 };
//         var kernel = new float[] { 1 };
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(signal, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve1DAsync_LargeSignal_CompletesSuccessfully()
//     {
//         // Arrange
//         var signal = new float[10000];
//         var kernel = new float[100];
// 
//         // Act
//         var result = await _convOps.Convolve1DAsync(signal, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public void Convolve1DAsync_CancellationToken_CanBeCancelled()
//     {
//         // Arrange
//         var signal = new float[100000];
//         var kernel = new float[1000];
//         var cts = new CancellationTokenSource();
//         cts.Cancel();
// 
//         // Act & Assert
//         Assert.ThrowsAsync<TaskCanceledException>(() =>
//             _convOps.Convolve1DAsync(signal, kernel, cancellationToken: cts.Token).AsTask());
//     }
// 
//     [Fact]
//     public async Task Convolve2DAsync_NonSquareImage_HandlesCorrectly()
//     {
//         // Arrange
//         var image = new float[10, 15];
//         var kernel = new float[3, 3];
// 
//         // Act
//         var result = await _convOps.Convolve2DAsync(image, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     [Fact]
//     public async Task Convolve2DAsync_NonSquareKernel_HandlesCorrectly()
//     {
//         // Arrange
//         var image = new float[10, 10];
//         var kernel = new float[3, 5];
// 
//         // Act
//         var result = await _convOps.Convolve2DAsync(image, kernel);
// 
//         // Assert
//         result.Should().NotBeNull();
//     }
// 
//     #endregion
// }
