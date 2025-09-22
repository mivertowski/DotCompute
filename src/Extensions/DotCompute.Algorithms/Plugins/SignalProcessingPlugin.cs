// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.SignalProcessing;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Plugins
{

/// <summary>
/// Plugin for signal processing operations.
/// </summary>
public sealed class SignalProcessingPlugin : AlgorithmPluginBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalProcessingPlugin"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SignalProcessingPlugin(ILogger<SignalProcessingPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc/>
    public override string Id => "com.dotcompute.algorithms.dsp";

    /// <inheritdoc/>
    public override string Name => "Digital Signal Processing";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "Provides digital signal processing operations including filtering, convolution, and spectral analysis.";

    /// <inheritdoc/>
    public override AcceleratorType[] SupportedAccelerators => [
        AcceleratorType.CPU,
        AcceleratorType.CUDA,
        AcceleratorType.ROCm
    ];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedOperations => [
        "Filtering",
        "Convolution",
        "Correlation",
        "DigitalFiltering",
        "FIRFilter",
        "IIRFilter",
        "Windowing",
        "Resampling",
        "NoiseReduction",
        "FeatureExtraction"
    ];

    /// <inheritdoc/>
    public override Type[] InputTypes => [typeof(string), typeof(object)];

    /// <inheritdoc/>
    public override Type OutputType => typeof(object);

    /// <inheritdoc/>
    protected override async Task<object> OnExecuteAsync(object[] inputs, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (inputs.Length < 2)
        {
            throw new ArgumentException("Expected operation name and data.");
        }

        var operation = (string)inputs[0];
        var data = inputs[1];

        return operation.ToUpperInvariant() switch
        {
            "CONVOLVE" => await ConvolveAsync(data, parameters, cancellationToken).ConfigureAwait(false),
            "CORRELATE" => await CrossCorrelateAsync(data, parameters, cancellationToken).ConfigureAwait(false),
            "FIR" => await ApplyFIRFilterAsync(data, parameters, cancellationToken).ConfigureAwait(false),
            "IIR" => await ApplyIIRFilterAsync(data, parameters, cancellationToken).ConfigureAwait(false),
            "DESIGN_LOWPASS" => await DesignLowPassAsync(parameters, cancellationToken).ConfigureAwait(false),
            "DESIGN_HIGHPASS" => await DesignHighPassAsync(parameters, cancellationToken).ConfigureAwait(false),
            "DESIGN_BANDPASS" => await DesignBandPassAsync(parameters, cancellationToken).ConfigureAwait(false),
            "RESAMPLE" => await ResampleAsync(data, parameters, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Operation '{operation}' is not supported.")
        };
    }

    /// <inheritdoc/>
    public override bool ValidateInputs(object[] inputs)
    {
        if (!base.ValidateInputs(inputs))
        {
            return false;
        }

        if (inputs.Length < 2)
        {
            return false;
        }

        if (inputs[0] is not string operation || string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        // Validate based on operation
        return operation.ToUpperInvariant() switch
        {
            "CONVOLVE" or "CORRELATE" => inputs[1] is float[][],
            "FIR" => inputs[1] is float[][],
            "IIR" => inputs[1] is float[],
            "DESIGN_LOWPASS" or "DESIGN_HIGHPASS" or "DESIGN_BANDPASS" => true,
            "RESAMPLE" => inputs[1] is float[],
            _ => false
        };
    }

    /// <inheritdoc/>
    public override long EstimateMemoryRequirement(int[] inputSizes)
    {
        if (inputSizes.Length == 0)
        {
            return 0;
        }

        var n = inputSizes[0];
        // Signal + kernel + result + FFT workspace
        return n * sizeof(float) * 4 + 4096;
    }

    /// <inheritdoc/>
    public override AlgorithmPerformanceProfile GetPerformanceProfile()
    {
        return new AlgorithmPerformanceProfile
        {
            Complexity = "O(n²) for direct convolution, O(n log n) for FFT convolution",
            IsParallelizable = true,
            OptimalParallelism = Environment.ProcessorCount,
            IsMemoryBound = true,
            IsComputeBound = true,
            EstimatedFlops = 2, // 2n² for direct convolution
            Metadata = new Dictionary<string, object>
            {
                ["ConvolutionMethods"] = new[] { "Direct", "FFT" },
                ["FilterTypes"] = new[] { "FIR", "IIR" },
                ["WindowFunctions"] = new[] { "Rectangular", "Hamming", "Hanning", "Blackman" }
            }
        };
    }

    private static async Task<float[]> ConvolveAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (data is not float[][] arrays || arrays.Length < 2)
        {
            throw new ArgumentException("Convolution requires two float arrays.");
        }

        var signal = arrays[0];
        var kernel = arrays[1];

        return await Task.Run(() => SignalProcessor.Convolve(signal, kernel), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> CrossCorrelateAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (data is not float[][] arrays || arrays.Length < 2)
        {
            throw new ArgumentException("Cross-correlation requires two float arrays.");
        }

        var signal1 = arrays[0];
        var signal2 = arrays[1];

        return await Task.Run(() => SignalProcessor.CrossCorrelate(signal1, signal2), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> ApplyFIRFilterAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (data is not float[][] arrays || arrays.Length < 2)
        {
            throw new ArgumentException("FIR filter requires signal and coefficients arrays.");
        }

        var signal = arrays[0];
        var coefficients = arrays[1];

        return await Task.Run(() => SignalProcessor.ApplyFIRFilter(signal, coefficients), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> ApplyIIRFilterAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (data is not float[] signal)
        {
            throw new ArgumentException("IIR filter requires a signal array.");
        }

        if (parameters == null || 
            !parameters.TryGetValue("b", out var bObj) || bObj is not float[] b ||
            !parameters.TryGetValue("a", out var aObj) || aObj is not float[] a)
        {
            throw new ArgumentException("IIR filter requires 'b' and 'a' coefficient arrays in parameters.");
        }

        return await Task.Run(() => SignalProcessor.ApplyIIRFilter(signal, b, a), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> DesignLowPassAsync(Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null || 
            !parameters.TryGetValue("cutoff", out var cutoffObj) || cutoffObj is not float cutoff ||
            !parameters.TryGetValue("length", out var lengthObj) || lengthObj is not int length)
        {
            throw new ArgumentException("Low-pass filter design requires 'cutoff' and 'length' parameters.");
        }

        var windowType = WindowType.Hamming;
        if (parameters.TryGetValue("window", out var windowObj))
        {
            if (windowObj is WindowType wt)
            {
                windowType = wt;
            }
            else if (windowObj is string wtStr && Enum.TryParse<WindowType>(wtStr, true, out var parsed))
            {
                windowType = parsed;
            }
        }

        return await Task.Run(() => SignalProcessor.DesignLowPassFIR(cutoff, length, windowType), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> DesignHighPassAsync(Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null || 
            !parameters.TryGetValue("cutoff", out var cutoffObj) || cutoffObj is not float cutoff ||
            !parameters.TryGetValue("length", out var lengthObj) || lengthObj is not int length)
        {
            throw new ArgumentException("High-pass filter design requires 'cutoff' and 'length' parameters.");
        }

        var windowType = WindowType.Hamming;
        if (parameters.TryGetValue("window", out var windowObj))
        {
            if (windowObj is WindowType wt)
            {
                windowType = wt;
            }
            else if (windowObj is string wtStr && Enum.TryParse<WindowType>(wtStr, true, out var parsed))
            {
                windowType = parsed;
            }
        }

        return await Task.Run(() => SignalProcessor.DesignHighPassFIR(cutoff, length, windowType), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> DesignBandPassAsync(Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null || 
            !parameters.TryGetValue("lowFreq", out var lowObj) || lowObj is not float lowFreq ||
            !parameters.TryGetValue("highFreq", out var highObj) || highObj is not float highFreq ||
            !parameters.TryGetValue("length", out var lengthObj) || lengthObj is not int length)
        {
            throw new ArgumentException("Band-pass filter design requires 'lowFreq', 'highFreq', and 'length' parameters.");
        }

        var windowType = WindowType.Hamming;
        if (parameters.TryGetValue("window", out var windowObj))
        {
            if (windowObj is WindowType wt)
            {
                windowType = wt;
            }
            else if (windowObj is string wtStr && Enum.TryParse<WindowType>(wtStr, true, out var parsed))
            {
                windowType = parsed;
            }
        }

        return await Task.Run(() => SignalProcessor.DesignBandPassFIR(lowFreq, highFreq, length, windowType), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> ResampleAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (data is not float[] signal)
        {
            throw new ArgumentException("Resample requires a signal array.");
        }

        if (parameters == null || 
            !parameters.TryGetValue("originalRate", out var origObj) || origObj is not float originalRate ||
            !parameters.TryGetValue("targetRate", out var targetObj) || targetObj is not float targetRate)
        {
            throw new ArgumentException("Resample requires 'originalRate' and 'targetRate' parameters.");
        }

        return await Task.Run(() => SignalProcessor.Resample(signal, originalRate, targetRate), cancellationToken).ConfigureAwait(false);
    }
}}
