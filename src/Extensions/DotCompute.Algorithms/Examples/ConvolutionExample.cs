// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.SignalProcessing;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Examples
{

/// <summary>
/// Demonstrates usage of GPU-accelerated convolution operations.
/// </summary>
public static class ConvolutionExample
{
    /// <summary>
    /// Demonstrates 1D convolution for signal processing applications.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="accelerator">The GPU accelerator.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>Task representing the async operation.</returns>
    public static async Task DemonstrateSignalProcessing1DAsync(
        KernelManager kernelManager,
        IAccelerator accelerator,
        ILogger<ConvolutionOperations>? logger = null)
    {
        using var convolution = new ConvolutionOperations(kernelManager, accelerator, logger);

        // Create a test signal (sine wave with noise)
        const int signalLength = 1024;
        const float sampleRate = 1000.0f;
        const float frequency = 50.0f; // 50 Hz
        
        var signal = new float[signalLength];
        var random = new Random(42);
        
        for (var i = 0; i < signalLength; i++)
        {
            var t = i / sampleRate;
            signal[i] = MathF.Sin(2 * MathF.PI * frequency * t) + 
                       0.3f * (float)(random.NextDouble() - 0.5); // Add noise
        }

        // Create a low-pass filter kernel (simple moving average)
        const int kernelLength = 32;
        var kernel = Enumerable.Repeat(1.0f / kernelLength, kernelLength).ToArray();

        logger?.LogInformation("Performing 1D convolution on {SignalLength} samples with {KernelLength} kernel",
            signalLength, kernelLength);

        // Perform convolution using different strategies
        var directResult = await convolution.Convolve1DAsync(
            signal, kernel, PaddingMode.Same, ConvolutionStrategy.Direct);
        
        var fftResult = await convolution.Convolve1DAsync(
            signal, kernel, PaddingMode.Same, ConvolutionStrategy.FFT);

        logger?.LogInformation("Direct convolution result length: {Length}", directResult.Length);
        logger?.LogInformation("FFT convolution result length: {Length}", fftResult.Length);

        // Demonstrate dilated convolution
        var dilatedResult = await convolution.DilatedConvolve1DAsync(
            signal, kernel, dilation: 2, PaddingMode.Valid);

        logger?.LogInformation("Dilated convolution result length: {Length}", dilatedResult.Length);
    }

    /// <summary>
    /// Demonstrates 2D convolution for image processing applications.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="accelerator">The GPU accelerator.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>Task representing the async operation.</returns>
    public static async Task DemonstrateImageProcessing2DAsync(
        KernelManager kernelManager,
        IAccelerator accelerator,
        ILogger<ConvolutionOperations>? logger = null)
    {
        using var convolution = new ConvolutionOperations(kernelManager, accelerator, logger);

        // Create a test image (checkerboard pattern)
        const int imageWidth = 512;
        const int imageHeight = 512;
        var image = new float[imageHeight * imageWidth];

        for (var y = 0; y < imageHeight; y++)
        {
            for (var x = 0; x < imageWidth; x++)
            {
                var isWhite = ((x / 32) + (y / 32)) % 2 == 0;
                image[y * imageWidth + x] = isWhite ? 1.0f : 0.0f;
            }
        }

        // Sobel edge detection kernels
        var sobelX = new float[]
        {
            -1, 0, 1,
            -2, 0, 2,
            -1, 0, 1
        };
        
        var sobelY = new float[]
        {
             1,  2,  1,
             0,  0,  0,
            -1, -2, -1
        };

        logger?.LogInformation("Performing edge detection on {Width}x{Height} image", imageWidth, imageHeight);

        // Apply Sobel edge detection
        var edgesX = await convolution.Convolve2DAsync(
            image, sobelX, imageWidth, imageHeight, 3, 3,
            PaddingMode.Same, (1, 1), ConvolutionStrategy.Direct);

        var edgesY = await convolution.Convolve2DAsync(
            image, sobelY, imageWidth, imageHeight, 3, 3,
            PaddingMode.Same, (1, 1), ConvolutionStrategy.Direct);

        // Gaussian blur kernel (approximation)
        var gaussian = new float[]
        {
            1, 2, 1,
            2, 4, 2,
            1, 2, 1
        };
        
        // Normalize
        var sum = gaussian.Sum();
        for (var i = 0; i < gaussian.Length; i++)
        {
            gaussian[i] /= sum;
        }

        // Apply Gaussian blur using Winograd convolution for 3x3 kernel
        var blurred = await convolution.Convolve2DAsync(
            image, gaussian, imageWidth, imageHeight, 3, 3,
            PaddingMode.Same, (1, 1), ConvolutionStrategy.Winograd);

        logger?.LogInformation("Edge detection and blur completed");

        // Demonstrate separable convolution
        var gaussianX = new float[] { 1, 2, 1 };
        var gaussianY = new float[] { 1, 2, 1 };
        
        // Normalize
        var sumX = gaussianX.Sum();
        var sumY = gaussianY.Sum();
        for (var i = 0; i < gaussianX.Length; i++) gaussianX[i] /= sumX;
        for (var i = 0; i < gaussianY.Length; i++) gaussianY[i] /= sumY;

        var separableBlur = await convolution.SeparableConvolve2DAsync(
            image, gaussianX, gaussianY, imageWidth, imageHeight, PaddingMode.Same);

        logger?.LogInformation("Separable convolution completed");
    }

    /// <summary>
    /// Demonstrates deep learning convolution operations.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="accelerator">The GPU accelerator.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>Task representing the async operation.</returns>
    public static async Task DemonstrateDeepLearningConvolutionsAsync(
        KernelManager kernelManager,
        IAccelerator accelerator,
        ILogger<ConvolutionOperations>? logger = null)
    {
        using var convolution = new ConvolutionOperations(kernelManager, accelerator, logger);

        // Simulate CNN layer parameters
        const int batchSize = 4;
        const int inputChannels = 3;  // RGB
        const int outputChannels = 32;
        const int imageWidth = 224;
        const int imageHeight = 224;
        const int kernelSize = 3;

        logger?.LogInformation("Simulating CNN layer: batch={Batch}, in_channels={InChannels}, out_channels={OutChannels}",
            batchSize, inputChannels, outputChannels);

        // Create random input batch (normally would be actual images)
        var batchInput = new float[batchSize * inputChannels * imageHeight * imageWidth];
        var random = new Random(42);
        for (var i = 0; i < batchInput.Length; i++)
        {
            batchInput[i] = (float)(random.NextDouble() - 0.5) * 2; // [-1, 1]
        }

        // Create random kernels (normally would be trained weights)
        var kernels = new float[outputChannels * inputChannels * kernelSize * kernelSize];
        for (var i = 0; i < kernels.Length; i++)
        {
            kernels[i] = (float)(random.NextGaussian() * 0.1); // Small random weights
        }

        // Perform batch convolution
        var batchOutput = await convolution.BatchConvolve2DAsync(
            batchInput, kernels,
            batchSize, inputChannels, outputChannels,
            imageWidth, imageHeight, kernelSize, kernelSize,
            PaddingMode.Same, (1, 1));

        logger?.LogInformation("Batch convolution output size: {Size}", batchOutput.Length);

        // Demonstrate depthwise convolution (used in MobileNets)
        var depthwiseKernels = new float[inputChannels * kernelSize * kernelSize];
        for (var i = 0; i < depthwiseKernels.Length; i++)
        {
            depthwiseKernels[i] = (float)(random.NextGaussian() * 0.1);
        }

        // Take first image from batch for depthwise demo
        var singleImageSize = inputChannels * imageHeight * imageWidth;
        var singleImage = batchInput.Take(singleImageSize).ToArray();

        var depthwiseOutput = await convolution.DepthwiseConvolve2DAsync(
            singleImage, depthwiseKernels,
            inputChannels, imageWidth, imageHeight, kernelSize, kernelSize,
            PaddingMode.Same, (1, 1));

        logger?.LogInformation("Depthwise convolution output size: {Size}", depthwiseOutput.Length);

        // Demonstrate strided convolution (for downsampling)
        var stridedOutput = await convolution.BatchConvolve2DAsync(
            batchInput, kernels,
            batchSize, inputChannels, outputChannels,
            imageWidth, imageHeight, kernelSize, kernelSize,
            PaddingMode.Valid, (2, 2)); // Stride of 2 reduces spatial dimensions by half

        logger?.LogInformation("Strided convolution output size: {Size}", stridedOutput.Length);

        // Demonstrate transposed convolution (for upsampling)
        var smallInput = new float[batchSize * outputChannels * 56 * 56]; // 1/4 size
        for (var i = 0; i < smallInput.Length; i++)
        {
            smallInput[i] = (float)(random.NextDouble() - 0.5);
        }

        var upsampledOutput = await convolution.TransposedConvolve2DAsync(
            [.. smallInput.Take(outputChannels * 56 * 56)], // Single image for demo
            [.. kernels.Take(inputChannels * kernelSize * kernelSize)], // Single kernel
            56, 56, kernelSize, kernelSize,
            (2, 2), PaddingMode.Same, (0, 0));

        logger?.LogInformation("Transposed convolution output size: {Size}", upsampledOutput.Length);
    }

    /// <summary>
    /// Demonstrates 3D convolution for volumetric data processing.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="accelerator">The GPU accelerator.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>Task representing the async operation.</returns>
    public static async Task DemonstrateVolumetricProcessing3DAsync(
        KernelManager kernelManager,
        IAccelerator accelerator,
        ILogger<ConvolutionOperations>? logger = null)
    {
        using var convolution = new ConvolutionOperations(kernelManager, accelerator, logger);

        // Create a test 3D volume (medical imaging, video, etc.)
        const int volumeWidth = 64;
        const int volumeHeight = 64;
        const int volumeDepth = 32;
        var volume = new float[volumeDepth * volumeHeight * volumeWidth];

        // Create a 3D sphere in the center
        var centerX = volumeWidth / 2.0f;
        var centerY = volumeHeight / 2.0f;
        var centerZ = volumeDepth / 2.0f;
        var radius = Math.Min(volumeWidth, Math.Min(volumeHeight, volumeDepth)) / 4.0f;

        for (var z = 0; z < volumeDepth; z++)
        {
            for (var y = 0; y < volumeHeight; y++)
            {
                for (var x = 0; x < volumeWidth; x++)
                {
                    var distance = MathF.Sqrt(
                        (x - centerX) * (x - centerX) +
                        (y - centerY) * (y - centerY) +
                        (z - centerZ) * (z - centerZ));

                    volume[z * volumeHeight * volumeWidth + y * volumeWidth + x] = 
                        distance <= radius ? 1.0f : 0.0f;
                }
            }
        }

        // 3D smoothing kernel
        var smoothingKernel = new float[]
        {
            // First slice (z=0)
            1, 2, 1,
            2, 4, 2,
            1, 2, 1,
            
            // Second slice (z=1)
            2, 4, 2,
            4, 8, 4,
            2, 4, 2,
            
            // Third slice (z=2)
            1, 2, 1,
            2, 4, 2,
            1, 2, 1
        };

        // Normalize
        var sum = smoothingKernel.Sum();
        for (var i = 0; i < smoothingKernel.Length; i++)
        {
            smoothingKernel[i] /= sum;
        }

        logger?.LogInformation("Performing 3D convolution on {W}x{H}x{D} volume", 
            volumeWidth, volumeHeight, volumeDepth);

        // Apply 3D smoothing
        var smoothedVolume = await convolution.Convolve3DAsync(
            volume, smoothingKernel,
            volumeWidth, volumeHeight, volumeDepth,
            3, 3, 3, // 3x3x3 kernel
            PaddingMode.Same, (1, 1, 1));

        logger?.LogInformation("3D convolution completed, output size: {Size}", smoothedVolume.Length);

        // Demonstrate 3D edge detection
        var edgeKernel = new float[]
        {
            // Laplacian-like 3D kernel
            0, 0, 0,   0, -1, 0,   0, 0, 0,
            0, -1, 0,  -1, 6, -1,  0, -1, 0,
            0, 0, 0,   0, -1, 0,   0, 0, 0
        };

        var edges = await convolution.Convolve3DAsync(
            volume, edgeKernel,
            volumeWidth, volumeHeight, volumeDepth,
            3, 3, 3,
            PaddingMode.Valid, (1, 1, 1));

        logger?.LogInformation("3D edge detection completed, output size: {Size}", edges.Length);
    }

    /// <summary>
    /// Comprehensive performance comparison of different convolution strategies.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="accelerator">The GPU accelerator.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>Task representing the async operation.</returns>
    public static async Task PerformanceComparisonAsync(
        KernelManager kernelManager,
        IAccelerator accelerator,
        ILogger<ConvolutionOperations>? logger = null)
    {
        using var convolution = new ConvolutionOperations(kernelManager, accelerator, logger);

        const int imageWidth = 1024;
        const int imageHeight = 1024;
        var image = new float[imageHeight * imageWidth];
        var random = new Random(42);

        // Fill with random data
        for (var i = 0; i < image.Length; i++)
        {
            image[i] = (float)random.NextDouble();
        }

        // Test different kernel sizes
        var kernelSizes = new[] { 3, 5, 7, 11, 15 };
        var strategies = new[]
        {
            ConvolutionStrategy.Direct,
            ConvolutionStrategy.Winograd,
            ConvolutionStrategy.Im2Col,
            ConvolutionStrategy.Auto
        };

        logger?.LogInformation("Starting performance comparison on {W}x{H} image", imageWidth, imageHeight);

        foreach (var kernelSize in kernelSizes)
        {
            // Create random kernel
            var kernel = new float[kernelSize * kernelSize];
            for (var i = 0; i < kernel.Length; i++)
            {
                kernel[i] = (float)(random.NextGaussian() * 0.1);
            }

            logger?.LogInformation("Testing {KernelSize}x{KernelSize} kernel", kernelSize, kernelSize);

            foreach (var strategy in strategies)
            {
                // Skip Winograd for non-3x3 and non-5x5 kernels
                if (strategy == ConvolutionStrategy.Winograd && kernelSize != 3 && kernelSize != 5)
                    continue;

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var result = await convolution.Convolve2DAsync(
                        image, kernel, imageWidth, imageHeight, kernelSize, kernelSize,
                        PaddingMode.Valid, (1, 1), strategy);

                    stopwatch.Stop();

                    logger?.LogInformation("  {Strategy}: {ElapsedMs:F2} ms, Output: {OutputSize}",
                        strategy, stopwatch.Elapsed.TotalMilliseconds, result.Length);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    logger?.LogWarning("  {Strategy}: Failed - {Error}",
                        strategy, ex.Message);
                }
            }

            logger?.LogInformation("");
        }
    }
}

/// <summary>
/// Extension methods for random number generation.
/// </summary>
internal static class RandomExtensions
{
    /// <summary>
    /// Generates a random number from a standard normal distribution.
    /// </summary>
    public static double NextGaussian(this Random random, double mean = 0.0, double stdDev = 1.0)
    {
        // Box-Muller transform
        static double GenerateStandardNormal(Random rng)
        {
            var u1 = 1.0 - rng.NextDouble(); // Uniform(0,1]
            var u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        return mean + stdDev * GenerateStandardNormal(random);
    }
}}
