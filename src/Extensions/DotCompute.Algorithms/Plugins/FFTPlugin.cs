// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.SignalProcessing;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.SignalProcessing;
using Microsoft.Extensions.Logging;
using Complex = DotCompute.Algorithms.Types.SignalProcessing.Complex;

namespace DotCompute.Algorithms.Plugins
{

    /// <summary>
    /// Plugin for Fast Fourier Transform operations.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FFTPlugin"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
    public sealed class FFTPlugin(ILogger<FFTPlugin> logger) : AlgorithmPluginBase(logger)
    {

        /// <inheritdoc/>
        public override string Id => "com.dotcompute.algorithms.fft";

        /// <inheritdoc/>
        public override string Name => "Fast Fourier Transform";

        /// <inheritdoc/>
        public override Version Version => new(1, 0, 0);

        /// <inheritdoc/>
        public override string Description => "Provides Fast Fourier Transform operations for signal processing and spectral analysis.";

        /// <inheritdoc/>
        public override ImmutableArray<AcceleratorType> SupportedAcceleratorTypes => [
            AcceleratorType.CPU,
        AcceleratorType.CUDA,
        AcceleratorType.ROCm
        ];

        /// <inheritdoc/>
        public override IReadOnlyList<string> SupportedOperations => [
            "FFT",
        "IFFT",
        "RealFFT",
        "InverseRealFFT",
        "PowerSpectrum",
        "MagnitudeSpectrum",
        "PhaseSpectrum",
        "WindowFunction",
        "SpectralAnalysis"
        ];

        /// <inheritdoc/>
        public override ImmutableArray<Type> InputTypes => [typeof(string), typeof(object)];

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
                "FORWARD" => await ForwardFFTAsync(data, parameters, cancellationToken).ConfigureAwait(false),
                "INVERSE" => await InverseFFTAsync(data, parameters, cancellationToken).ConfigureAwait(false),
                "REAL" => await RealFFTAsync(data, cancellationToken).ConfigureAwait(false),
                "INVERSEREAL" => await InverseRealFFTAsync(data, parameters, cancellationToken).ConfigureAwait(false),
                "POWER" => await PowerSpectrumAsync(data, cancellationToken).ConfigureAwait(false),
                "MAGNITUDE" => await MagnitudeSpectrumAsync(data, cancellationToken).ConfigureAwait(false),
                "PHASE" => await PhaseSpectrumAsync(data, cancellationToken).ConfigureAwait(false),
                "WINDOW" => await ApplyWindowAsync(data, parameters, cancellationToken).ConfigureAwait(false),
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
                "FORWARD" or "INVERSE" => inputs[1] is Complex[],
                "REAL" or "WINDOW" => inputs[1] is float[],
                "INVERSEREAL" => inputs[1] is Complex[],
                "POWER" or "MAGNITUDE" or "PHASE" => inputs[1] is Complex[],
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
            // Complex array + workspace + twiddle factors
            return n * sizeof(float) * 2 * 3 + 4096; // Extra for overhead
        }

        /// <inheritdoc/>
        public override AlgorithmPerformanceProfile GetPerformanceProfile()
        {
            return new AlgorithmPerformanceProfile
            {
                Complexity = "O(n log n)",
                IsParallelizable = true,
                OptimalParallelism = Environment.ProcessorCount,
                IsMemoryBound = false,
                IsComputeBound = true,
                EstimatedFlops = 5, // ~5n log n for complex FFT
                Metadata = new Dictionary<string, object>
                {
                    ["Algorithm"] = "Cooley-Tukey",
                    ["InPlace"] = true,
                    ["RadixSupport"] = new[] { 2 },
                    ["SimdAccelerated"] = true
                }
            };
        }

        private static async Task<Complex[]> ForwardFFTAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
        {
            var complexData = (Complex[])data;
            var result = new Complex[complexData.Length];
            Array.Copy(complexData, result, complexData.Length);

            await Task.Run(() =>
            {
                FFT.Forward(result);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        private static async Task<Complex[]> InverseFFTAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
        {
            var complexData = (Complex[])data;
            var result = new Complex[complexData.Length];
            Array.Copy(complexData, result, complexData.Length);

            await Task.Run(() =>
            {
                FFT.Inverse(result);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        private static async Task<Complex[]> RealFFTAsync(object data, CancellationToken cancellationToken)
        {
            var realData = (float[])data;

            return await Task.Run(() =>
            {
                return FFT.RealFFT(realData);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<float[]> InverseRealFFTAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
        {
            var complexData = (Complex[])data;

            if (parameters == null || !parameters.TryGetValue("outputLength", out var lengthObj) || lengthObj is not int outputLength)
            {
                throw new ArgumentException("outputLength parameter is required for inverse real FFT.");
            }

            return await Task.Run(() =>
            {
                return FFT.InverseRealFFT(complexData, outputLength);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<float[]> PowerSpectrumAsync(object data, CancellationToken cancellationToken)
        {
            var complexData = (Complex[])data;

            return await Task.Run(() =>
            {
                return FFT.PowerSpectrum(complexData);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<float[]> MagnitudeSpectrumAsync(object data, CancellationToken cancellationToken)
        {
            var complexData = (Complex[])data;

            return await Task.Run(() =>
            {
                return FFT.MagnitudeSpectrum(complexData);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<float[]> PhaseSpectrumAsync(object data, CancellationToken cancellationToken)
        {
            var complexData = (Complex[])data;

            return await Task.Run(() =>
            {
                return FFT.PhaseSpectrum(complexData);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<float[]> ApplyWindowAsync(object data, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
        {
            var floatData = (float[])data;
            var result = new float[floatData.Length];
            Array.Copy(floatData, result, floatData.Length);

            var windowType = WindowType.Hamming; // Default
            if (parameters != null && parameters.TryGetValue("windowType", out var typeObj))
            {
                if (typeObj is WindowType wt)
                {
                    windowType = wt;
                }
                else if (typeObj is string wtStr && Enum.TryParse<WindowType>(wtStr, true, out var parsed))
                {
                    windowType = parsed;
                }
            }

            await Task.Run(() =>
            {
                FFT.ApplyWindow(result, windowType);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }
    }
}
