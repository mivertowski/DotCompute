// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Core.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.SignalProcessing;

/// <summary>
/// Provides GPU-accelerated convolution operations for signal processing and deep learning.
/// Supports 1D, 2D, and 3D convolutions with various optimization strategies.
/// </summary>
public sealed class ConvolutionOperations : IDisposable
{
    private readonly KernelManager _kernelManager;
    private readonly IAccelerator _accelerator;
    private readonly ILogger<ConvolutionOperations>? _logger;
    private readonly ConvolutionStrategy _defaultStrategy;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvolutionOperations"/> class.
    /// </summary>
    /// <param name="kernelManager">The kernel manager for GPU operations.</param>
    /// <param name="accelerator">The GPU accelerator to use.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="defaultStrategy">Default optimization strategy.</param>
    public ConvolutionOperations(
        KernelManager kernelManager,
        IAccelerator accelerator,
        ILogger<ConvolutionOperations>? logger = null,
        ConvolutionStrategy defaultStrategy = ConvolutionStrategy.Auto)
    {
        _kernelManager = kernelManager ?? throw new ArgumentNullException(nameof(kernelManager));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger;
        _defaultStrategy = defaultStrategy;
    }

    #region 1D Convolution

    /// <summary>
    /// Performs 1D convolution using GPU acceleration.
    /// </summary>
    /// <param name="signal">Input signal data.</param>
    /// <param name="kernel">Convolution kernel.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="strategy">Optimization strategy to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Convolution result.</returns>
    public async ValueTask<float[]> Convolve1DAsync(
        float[] signal,
        float[] kernel,
        PaddingMode padding = PaddingMode.Valid,
        ConvolutionStrategy strategy = ConvolutionStrategy.Auto,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(signal, kernel);

        strategy = SelectOptimalStrategy(strategy, kernel.Length, 1);
        
        return strategy switch
        {
            ConvolutionStrategy.Direct => await DirectConvolve1DAsync(signal, kernel, padding, cancellationToken),
            ConvolutionStrategy.FFT => await FFTConvolve1DAsync(signal, kernel, padding, cancellationToken),
            ConvolutionStrategy.Separable => await DirectConvolve1DAsync(signal, kernel, padding, cancellationToken), // Fallback for 1D
            _ => await AutoConvolve1DAsync(signal, kernel, padding, cancellationToken)
        };
    }

    /// <summary>
    /// Performs 1D strided convolution.
    /// </summary>
    /// <param name="signal">Input signal data.</param>
    /// <param name="kernel">Convolution kernel.</param>
    /// <param name="stride">Stride value.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Strided convolution result.</returns>
    public async ValueTask<float[]> StridedConvolve1DAsync(
        float[] signal,
        float[] kernel,
        int stride,
        PaddingMode padding = PaddingMode.Valid,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(signal, kernel);
        if (stride <= 0) throw new ArgumentException("Stride must be positive.", nameof(stride));

        var outputLength = CalculateOutputLength(signal.Length, kernel.Length, padding, stride);
        var result = new float[outputLength];
        
        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "StridedConvolution1D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "signal", Type = typeof(float[]), Value = signal },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "signalLength", Type = typeof(int), Value = signal.Length },
            new KernelArgument { Name = "kernelLength", Type = typeof(int), Value = kernel.Length },
            new KernelArgument { Name = "stride", Type = typeof(int), Value = stride },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    /// <summary>
    /// Performs 1D dilated (atrous) convolution.
    /// </summary>
    /// <param name="signal">Input signal data.</param>
    /// <param name="kernel">Convolution kernel.</param>
    /// <param name="dilation">Dilation rate.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dilated convolution result.</returns>
    public async ValueTask<float[]> DilatedConvolve1DAsync(
        float[] signal,
        float[] kernel,
        int dilation,
        PaddingMode padding = PaddingMode.Valid,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(signal, kernel);
        if (dilation <= 0) throw new ArgumentException("Dilation must be positive.", nameof(dilation));

        var effectiveKernelSize = (kernel.Length - 1) * dilation + 1;
        var outputLength = CalculateOutputLength(signal.Length, effectiveKernelSize, padding, 1);
        var result = new float[outputLength];
        
        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "DilatedConvolution1D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "signal", Type = typeof(float[]), Value = signal },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "signalLength", Type = typeof(int), Value = signal.Length },
            new KernelArgument { Name = "kernelLength", Type = typeof(int), Value = kernel.Length },
            new KernelArgument { Name = "dilation", Type = typeof(int), Value = dilation },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    #endregion

    #region 2D Convolution

    /// <summary>
    /// Performs 2D convolution for image processing and CNN operations.
    /// </summary>
    /// <param name="input">Input image data (Height x Width).</param>
    /// <param name="kernel">2D convolution kernel.</param>
    /// <param name="inputWidth">Width of input image.</param>
    /// <param name="inputHeight">Height of input image.</param>
    /// <param name="kernelWidth">Width of kernel.</param>
    /// <param name="kernelHeight">Height of kernel.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="stride">Stride values (strideY, strideX).</param>
    /// <param name="strategy">Optimization strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>2D convolution result.</returns>
    public async ValueTask<float[]> Convolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding = PaddingMode.Valid,
        (int y, int x) stride = default,
        ConvolutionStrategy strategy = ConvolutionStrategy.Auto,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs2D(input, kernel, inputWidth, inputHeight, kernelWidth, kernelHeight);
        
        if (stride == default) stride = (1, 1);
        strategy = SelectOptimalStrategy(strategy, Math.Max(kernelWidth, kernelHeight), 2);

        return strategy switch
        {
            ConvolutionStrategy.Direct => await DirectConvolve2DAsync(input, kernel, inputWidth, inputHeight, 
                kernelWidth, kernelHeight, padding, stride, cancellationToken),
            ConvolutionStrategy.Winograd when IsWinogradSuitable(kernelWidth, kernelHeight) => 
                await WinogradConvolve2DAsync(input, kernel, inputWidth, inputHeight, 
                kernelWidth, kernelHeight, padding, stride, cancellationToken),
            ConvolutionStrategy.Im2Col => await Im2ColConvolve2DAsync(input, kernel, inputWidth, inputHeight, 
                kernelWidth, kernelHeight, padding, stride, cancellationToken),
            ConvolutionStrategy.FFT => await FFTConvolve2DAsync(input, kernel, inputWidth, inputHeight, 
                kernelWidth, kernelHeight, padding, stride, cancellationToken),
            _ => await AutoConvolve2DAsync(input, kernel, inputWidth, inputHeight, 
                kernelWidth, kernelHeight, padding, stride, cancellationToken)
        };
    }

    /// <summary>
    /// Performs separable 2D convolution using two 1D kernels.
    /// </summary>
    /// <param name="input">Input image data.</param>
    /// <param name="kernelX">Horizontal kernel.</param>
    /// <param name="kernelY">Vertical kernel.</param>
    /// <param name="inputWidth">Width of input image.</param>
    /// <param name="inputHeight">Height of input image.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Separable convolution result.</returns>
    public async ValueTask<float[]> SeparableConvolve2DAsync(
        float[] input,
        float[] kernelX,
        float[] kernelY,
        int inputWidth,
        int inputHeight,
        PaddingMode padding = PaddingMode.Valid,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(kernelX, kernelY);
        if (input.Length != inputWidth * inputHeight)
            throw new ArgumentException("Input size mismatch.");

        // First pass: convolve rows with kernelX
        var tempResult = new float[inputWidth * inputHeight];
        await ConvolveRows2DAsync(input, kernelX, tempResult, inputWidth, inputHeight, padding, cancellationToken);

        // Second pass: convolve columns with kernelY
        var result = new float[inputWidth * inputHeight];
        await ConvolveColumns2DAsync(tempResult, kernelY, result, inputWidth, inputHeight, padding, cancellationToken);

        return result;
    }

    /// <summary>
    /// Performs depthwise 2D convolution (each channel convolved separately).
    /// </summary>
    /// <param name="input">Input data (Channels x Height x Width).</param>
    /// <param name="kernels">Depthwise kernels (Channels x KernelHeight x KernelWidth).</param>
    /// <param name="channels">Number of input channels.</param>
    /// <param name="inputWidth">Width of input.</param>
    /// <param name="inputHeight">Height of input.</param>
    /// <param name="kernelWidth">Width of kernels.</param>
    /// <param name="kernelHeight">Height of kernels.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="stride">Stride values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Depthwise convolution result.</returns>
    public async ValueTask<float[]> DepthwiseConvolve2DAsync(
        float[] input,
        float[] kernels,
        int channels,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding = PaddingMode.Valid,
        (int y, int x) stride = default,
        CancellationToken cancellationToken = default)
    {
        if (input.Length != channels * inputHeight * inputWidth)
            throw new ArgumentException("Input size mismatch.");
        if (kernels.Length != channels * kernelHeight * kernelWidth)
            throw new ArgumentException("Kernel size mismatch.");

        if (stride == default) stride = (1, 1);

        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var result = new float[channels * outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "DepthwiseConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernels", Type = typeof(float[]), Value = kernels },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "channels", Type = typeof(int), Value = channels },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    #endregion

    #region 3D Convolution

    /// <summary>
    /// Performs 3D convolution for volumetric data processing.
    /// </summary>
    /// <param name="input">Input volume data (Depth x Height x Width).</param>
    /// <param name="kernel">3D convolution kernel.</param>
    /// <param name="inputWidth">Width of input volume.</param>
    /// <param name="inputHeight">Height of input volume.</param>
    /// <param name="inputDepth">Depth of input volume.</param>
    /// <param name="kernelWidth">Width of kernel.</param>
    /// <param name="kernelHeight">Height of kernel.</param>
    /// <param name="kernelDepth">Depth of kernel.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="stride">Stride values (strideZ, strideY, strideX).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>3D convolution result.</returns>
    public async ValueTask<float[]> Convolve3DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int inputDepth,
        int kernelWidth,
        int kernelHeight,
        int kernelDepth,
        PaddingMode padding = PaddingMode.Valid,
        (int z, int y, int x) stride = default,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs3D(input, kernel, inputWidth, inputHeight, inputDepth, kernelWidth, kernelHeight, kernelDepth);
        
        if (stride == default) stride = (1, 1, 1);

        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputDepth = CalculateOutputLength(inputDepth, kernelDepth, padding, stride.z);
        var result = new float[outputDepth * outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "Convolution3D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "inputDepth", Type = typeof(int), Value = inputDepth },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "kernelDepth", Type = typeof(int), Value = kernelDepth },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "strideZ", Type = typeof(int), Value = stride.z },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    #endregion

    #region Transposed Convolution (Deconvolution)

    /// <summary>
    /// Performs 2D transposed convolution (deconvolution).
    /// </summary>
    /// <param name="input">Input data.</param>
    /// <param name="kernel">Convolution kernel.</param>
    /// <param name="inputWidth">Width of input.</param>
    /// <param name="inputHeight">Height of input.</param>
    /// <param name="kernelWidth">Width of kernel.</param>
    /// <param name="kernelHeight">Height of kernel.</param>
    /// <param name="stride">Stride values.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="outputPadding">Output padding values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transposed convolution result.</returns>
    public async ValueTask<float[]> TransposedConvolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        (int y, int x) stride,
        PaddingMode padding = PaddingMode.Valid,
        (int y, int x) outputPadding = default,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs2D(input, kernel, inputWidth, inputHeight, kernelWidth, kernelHeight);

        var outputHeight = (inputHeight - 1) * stride.y - 2 * GetPaddingSize(padding, kernelHeight) + kernelHeight + outputPadding.y;
        var outputWidth = (inputWidth - 1) * stride.x - 2 * GetPaddingSize(padding, kernelWidth) + kernelWidth + outputPadding.x;
        var result = new float[outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "TransposedConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "outputPaddingX", Type = typeof(int), Value = outputPadding.x },
            new KernelArgument { Name = "outputPaddingY", Type = typeof(int), Value = outputPadding.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    #endregion

    #region Batch Operations

    /// <summary>
    /// Performs batch 2D convolution for efficient processing of multiple images.
    /// </summary>
    /// <param name="batchInput">Batch input data (Batch x Channels x Height x Width).</param>
    /// <param name="kernels">Convolution kernels (OutputChannels x InputChannels x KernelHeight x KernelWidth).</param>
    /// <param name="batchSize">Batch size.</param>
    /// <param name="inputChannels">Number of input channels.</param>
    /// <param name="outputChannels">Number of output channels.</param>
    /// <param name="inputWidth">Width of input.</param>
    /// <param name="inputHeight">Height of input.</param>
    /// <param name="kernelWidth">Width of kernels.</param>
    /// <param name="kernelHeight">Height of kernels.</param>
    /// <param name="padding">Padding strategy.</param>
    /// <param name="stride">Stride values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch convolution result.</returns>
    public async ValueTask<float[]> BatchConvolve2DAsync(
        float[] batchInput,
        float[] kernels,
        int batchSize,
        int inputChannels,
        int outputChannels,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding = PaddingMode.Valid,
        (int y, int x) stride = default,
        CancellationToken cancellationToken = default)
    {
        if (batchInput.Length != batchSize * inputChannels * inputHeight * inputWidth)
            throw new ArgumentException("Batch input size mismatch.");
        if (kernels.Length != outputChannels * inputChannels * kernelHeight * kernelWidth)
            throw new ArgumentException("Kernel size mismatch.");

        if (stride == default) stride = (1, 1);

        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var result = new float[batchSize * outputChannels * outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "BatchConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "batchInput", Type = typeof(float[]), Value = batchInput },
            new KernelArgument { Name = "kernels", Type = typeof(float[]), Value = kernels },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "batchSize", Type = typeof(int), Value = batchSize },
            new KernelArgument { Name = "inputChannels", Type = typeof(int), Value = inputChannels },
            new KernelArgument { Name = "outputChannels", Type = typeof(int), Value = outputChannels },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    #endregion

    #region Private Implementation Methods

    private async ValueTask<float[]> DirectConvolve1DAsync(
        float[] signal,
        float[] kernel,
        PaddingMode padding,
        CancellationToken cancellationToken)
    {
        var outputLength = CalculateOutputLength(signal.Length, kernel.Length, padding, 1);
        var result = new float[outputLength];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "DirectConvolution1D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "signal", Type = typeof(float[]), Value = signal },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "signalLength", Type = typeof(int), Value = signal.Length },
            new KernelArgument { Name = "kernelLength", Type = typeof(int), Value = kernel.Length },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    private async ValueTask<float[]> FFTConvolve1DAsync(
        float[] signal,
        float[] kernel,
        PaddingMode padding,
        CancellationToken cancellationToken)
    {
        // Use existing FFT implementation from SignalProcessor for CPU fallback
        // GPU implementation would use cuFFT or similar
        var outputLength = CalculateOutputLength(signal.Length, kernel.Length, padding, 1);
        
        // For now, delegate to CPU FFT implementation asynchronously
        var cpuResult = await Task.Run(() => SignalProcessor.Convolve(signal, kernel), cancellationToken);
        
        // Apply padding logic if needed
        return ApplyPadding1D(cpuResult, signal.Length, kernel.Length, padding);
    }

    private async ValueTask<float[]> AutoConvolve1DAsync(
        float[] signal,
        float[] kernel,
        PaddingMode padding,
        CancellationToken cancellationToken)
    {
        // Choose best strategy based on sizes
        if (kernel.Length <= 64)
        {
            return await DirectConvolve1DAsync(signal, kernel, padding, cancellationToken);
        }
        else
        {
            return await FFTConvolve1DAsync(signal, kernel, padding, cancellationToken);
        }
    }

    private async ValueTask<float[]> DirectConvolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding,
        (int y, int x) stride,
        CancellationToken cancellationToken)
    {
        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var result = new float[outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "DirectConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    private async ValueTask<float[]> WinogradConvolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding,
        (int y, int x) stride,
        CancellationToken cancellationToken)
    {
        // Winograd F(2x2,3x3) or F(4x4,3x3) for 3x3 kernels
        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var result = new float[outputHeight * outputWidth];

        var context = new KernelGenerationContext
        {
            DeviceInfo = _accelerator.Info,
            UseSharedMemory = _accelerator.Info.LocalMemorySize > 0,
            UseVectorTypes = true,
            Precision = PrecisionMode.Single,
            WorkGroupDimensions = GetOptimalWorkGroupSize(),
            Metadata = new Dictionary<string, object>
            {
                ["WinogradTileSize"] = kernelWidth == 3 ? "F(4x4,3x3)" : "F(2x2,3x3)"
            }
        };

        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "WinogradConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    private async ValueTask<float[]> Im2ColConvolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding,
        (int y, int x) stride,
        CancellationToken cancellationToken)
    {
        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var result = new float[outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "Im2ColConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    private async ValueTask<float[]> FFTConvolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding,
        (int y, int x) stride,
        CancellationToken cancellationToken)
    {
        // 2D FFT-based convolution - would use cuFFT for GPU implementation
        var outputHeight = CalculateOutputLength(inputHeight, kernelHeight, padding, stride.y);
        var outputWidth = CalculateOutputLength(inputWidth, kernelWidth, padding, stride.x);
        var result = new float[outputHeight * outputWidth];

        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "FFTConvolution2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "result", Type = typeof(float[]), Value = result },
            new KernelArgument { Name = "inputWidth", Type = typeof(int), Value = inputWidth },
            new KernelArgument { Name = "inputHeight", Type = typeof(int), Value = inputHeight },
            new KernelArgument { Name = "kernelWidth", Type = typeof(int), Value = kernelWidth },
            new KernelArgument { Name = "kernelHeight", Type = typeof(int), Value = kernelHeight },
            new KernelArgument { Name = "strideX", Type = typeof(int), Value = stride.x },
            new KernelArgument { Name = "strideY", Type = typeof(int), Value = stride.y },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
        return result;
    }

    private async ValueTask<float[]> AutoConvolve2DAsync(
        float[] input,
        float[] kernel,
        int inputWidth,
        int inputHeight,
        int kernelWidth,
        int kernelHeight,
        PaddingMode padding,
        (int y, int x) stride,
        CancellationToken cancellationToken)
    {
        // Choose optimal strategy based on kernel size and hardware capabilities
        if (IsWinogradSuitable(kernelWidth, kernelHeight))
        {
            return await WinogradConvolve2DAsync(input, kernel, inputWidth, inputHeight,
                kernelWidth, kernelHeight, padding, stride, cancellationToken);
        }
        else if (kernelWidth * kernelHeight <= 25) // Small kernels
        {
            return await DirectConvolve2DAsync(input, kernel, inputWidth, inputHeight,
                kernelWidth, kernelHeight, padding, stride, cancellationToken);
        }
        else if (inputWidth * inputHeight >= 1024) // Large images
        {
            return await Im2ColConvolve2DAsync(input, kernel, inputWidth, inputHeight,
                kernelWidth, kernelHeight, padding, stride, cancellationToken);
        }
        else
        {
            return await FFTConvolve2DAsync(input, kernel, inputWidth, inputHeight,
                kernelWidth, kernelHeight, padding, stride, cancellationToken);
        }
    }

    private async ValueTask ConvolveRows2DAsync(
        float[] input,
        float[] kernel,
        float[] output,
        int width,
        int height,
        PaddingMode padding,
        CancellationToken cancellationToken)
    {
        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "ConvolveRows2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "output", Type = typeof(float[]), Value = output },
            new KernelArgument { Name = "width", Type = typeof(int), Value = width },
            new KernelArgument { Name = "height", Type = typeof(int), Value = height },
            new KernelArgument { Name = "kernelLength", Type = typeof(int), Value = kernel.Length },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
    }

    private async ValueTask ConvolveColumns2DAsync(
        float[] input,
        float[] kernel,
        float[] output,
        int width,
        int height,
        PaddingMode padding,
        CancellationToken cancellationToken)
    {
        var context = CreateKernelGenerationContext();
        var compiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
            "ConvolveColumns2D",
            new[] { typeof(float[]), typeof(float[]) },
            typeof(float[]),
            _accelerator,
            context,
            cancellationToken: cancellationToken);

        var arguments = new[]
        {
            new KernelArgument { Name = "input", Type = typeof(float[]), Value = input },
            new KernelArgument { Name = "kernel", Type = typeof(float[]), Value = kernel },
            new KernelArgument { Name = "output", Type = typeof(float[]), Value = output },
            new KernelArgument { Name = "width", Type = typeof(int), Value = width },
            new KernelArgument { Name = "height", Type = typeof(int), Value = height },
            new KernelArgument { Name = "kernelLength", Type = typeof(int), Value = kernel.Length },
            new KernelArgument { Name = "padding", Type = typeof(int), Value = (int)padding }
        };

        await _kernelManager.ExecuteKernelAsync(compiledKernel, arguments, _accelerator, cancellationToken: cancellationToken);
    }

    #endregion

    #region Utility Methods

    private KernelGenerationContext CreateKernelGenerationContext()
    {
        return new KernelGenerationContext
        {
            DeviceInfo = _accelerator.Info,
            UseSharedMemory = _accelerator.Info.LocalMemorySize > 0,
            UseVectorTypes = true,
            Precision = PrecisionMode.Single,
            WorkGroupDimensions = GetOptimalWorkGroupSize()
        };
    }

    private int[] GetOptimalWorkGroupSize()
    {
        // Choose work group size based on device capabilities
        var maxWorkGroupSize = _accelerator.Info.MaxThreadsPerBlock;
        
        if (maxWorkGroupSize >= 256)
            return new[] { 16, 16 }; // 2D work groups
        else if (maxWorkGroupSize >= 64)
            return new[] { 8, 8 };
        else
            return new[] { 4, 4 };
    }

    private static ConvolutionStrategy SelectOptimalStrategy(ConvolutionStrategy strategy, int maxKernelSize, int dimensions)
    {
        if (strategy != ConvolutionStrategy.Auto)
            return strategy;

        return (dimensions, maxKernelSize) switch
        {
            (1, <= 64) => ConvolutionStrategy.Direct,
            (1, > 64) => ConvolutionStrategy.FFT,
            (2, 3) => ConvolutionStrategy.Winograd,
            (2, <= 7) => ConvolutionStrategy.Direct,
            (2, <= 15) => ConvolutionStrategy.Im2Col,
            (2, > 15) => ConvolutionStrategy.FFT,
            (3, <= 5) => ConvolutionStrategy.Direct,
            (3, > 5) => ConvolutionStrategy.FFT,
            _ => ConvolutionStrategy.Direct
        };
    }

    private static bool IsWinogradSuitable(int kernelWidth, int kernelHeight)
    {
        // Winograd is efficient for 3x3 and 5x5 kernels
        return (kernelWidth == 3 && kernelHeight == 3) || (kernelWidth == 5 && kernelHeight == 5);
    }

    private static int CalculateOutputLength(int inputLength, int kernelLength, PaddingMode padding, int stride)
    {
        var effectiveInputLength = padding switch
        {
            PaddingMode.Valid => inputLength,
            PaddingMode.Same => inputLength + kernelLength - 1,
            PaddingMode.Full => inputLength + 2 * (kernelLength - 1),
            PaddingMode.Causal => inputLength + kernelLength - 1,
            _ => inputLength
        };

        return (effectiveInputLength - kernelLength) / stride + 1;
    }

    private static int GetPaddingSize(PaddingMode padding, int kernelSize)
    {
        return padding switch
        {
            PaddingMode.Valid => 0,
            PaddingMode.Same => kernelSize / 2,
            PaddingMode.Full => kernelSize - 1,
            PaddingMode.Causal => kernelSize - 1,
            _ => 0
        };
    }

    private static float[] ApplyPadding1D(float[] input, int originalSignalLength, int kernelLength, PaddingMode padding)
    {
        return padding switch
        {
            PaddingMode.Valid => input.Take(originalSignalLength - kernelLength + 1).ToArray(),
            PaddingMode.Same => input.Take(originalSignalLength).ToArray(),
            PaddingMode.Full => input,
            PaddingMode.Causal => input.Take(originalSignalLength).ToArray(),
            _ => input
        };
    }

    #endregion

    #region Validation Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateInputs(float[] signal, float[] kernel)
    {
        if (signal == null || signal.Length == 0) throw new ArgumentException("Signal cannot be null or empty.", nameof(signal));
        if (kernel == null || kernel.Length == 0) throw new ArgumentException("Kernel cannot be null or empty.", nameof(kernel));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateInputs2D(float[] input, float[] kernel,
        int inputWidth, int inputHeight, int kernelWidth, int kernelHeight)
    {
        if (input.Length != inputWidth * inputHeight)
            throw new ArgumentException("Input size mismatch.");
        if (kernel.Length != kernelWidth * kernelHeight)
            throw new ArgumentException("Kernel size mismatch.");
        if (inputWidth <= 0 || inputHeight <= 0)
            throw new ArgumentException("Input dimensions must be positive.");
        if (kernelWidth <= 0 || kernelHeight <= 0)
            throw new ArgumentException("Kernel dimensions must be positive.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateInputs3D(float[] input, float[] kernel,
        int inputWidth, int inputHeight, int inputDepth, int kernelWidth, int kernelHeight, int kernelDepth)
    {
        if (input.Length != inputWidth * inputHeight * inputDepth)
            throw new ArgumentException("Input size mismatch.");
        if (kernel.Length != kernelWidth * kernelHeight * kernelDepth)
            throw new ArgumentException("Kernel size mismatch.");
        if (inputWidth <= 0 || inputHeight <= 0 || inputDepth <= 0)
            throw new ArgumentException("Input dimensions must be positive.");
        if (kernelWidth <= 0 || kernelHeight <= 0 || kernelDepth <= 0)
            throw new ArgumentException("Kernel dimensions must be positive.");
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the convolution operations instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // KernelManager and accelerator are not owned by this class
            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// Convolution optimization strategies.
/// </summary>
public enum ConvolutionStrategy
{
    /// <summary>
    /// Automatically select the best strategy.
    /// </summary>
    Auto,

    /// <summary>
    /// Direct convolution implementation.
    /// </summary>
    Direct,

    /// <summary>
    /// FFT-based convolution for large kernels.
    /// </summary>
    FFT,

    /// <summary>
    /// Winograd algorithm for small kernels (3x3, 5x5).
    /// </summary>
    Winograd,

    /// <summary>
    /// Im2col approach followed by GEMM.
    /// </summary>
    Im2Col,

    /// <summary>
    /// Separable convolution using 1D kernels.
    /// </summary>
    Separable
}

/// <summary>
/// Padding strategies for convolution operations.
/// </summary>
public enum PaddingMode
{
    /// <summary>
    /// No padding - output size = input size - kernel size + 1.
    /// </summary>
    Valid,

    /// <summary>
    /// Pad to maintain input size - output size = input size.
    /// </summary>
    Same,

    /// <summary>
    /// Full padding - output size = input size + kernel size - 1.
    /// </summary>
    Full,

    /// <summary>
    /// Causal padding for time series (pad only on one side).
    /// </summary>
    Causal
}