// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.SignalProcessing;


/// <summary>
/// Provides signal processing operations.
/// </summary>
public static class SignalProcessor
{
    /// <summary>
    /// Performs convolution of two signals.
    /// </summary>
    /// <param name="signal">The input signal.</param>
    /// <param name="kernel">The convolution kernel.</param>
    /// <returns>The convolved signal.</returns>
    public static float[] Convolve(ReadOnlySpan<float> signal, ReadOnlySpan<float> kernel)
    {
        if (signal.Length == 0 || kernel.Length == 0)
        {
            return [];
        }

        var resultLength = signal.Length + kernel.Length - 1;
        var result = new float[resultLength];

        // Direct convolution for small kernels
        if (kernel.Length <= 64)
        {
            DirectConvolve(signal, kernel, result);
        }
        else
        {
            // Use FFT convolution for larger kernels
            FFTConvolve(signal, kernel, result);
        }

        return result;
    }

    /// <summary>
    /// Performs cross-correlation of two signals.
    /// </summary>
    /// <param name="signal1">The first signal.</param>
    /// <param name="signal2">The second signal.</param>
    /// <returns>The cross-correlation result.</returns>
    public static float[] CrossCorrelate(ReadOnlySpan<float> signal1, ReadOnlySpan<float> signal2)
    {
        if (signal1.Length == 0 || signal2.Length == 0)
        {
            return [];
        }

        // Cross-correlation is convolution with reversed kernel
        var reversedSignal2 = new float[signal2.Length];
        for (var i = 0; i < signal2.Length; i++)
        {
            reversedSignal2[i] = signal2[signal2.Length - 1 - i];
        }

        return Convolve(signal1, reversedSignal2);
    }

    /// <summary>
    /// Applies a finite impulse response (FIR) filter to a signal.
    /// </summary>
    /// <param name="signal">The input signal.</param>
    /// <param name="coefficients">The filter coefficients.</param>
    /// <returns>The filtered signal.</returns>
    public static float[] ApplyFIRFilter(ReadOnlySpan<float> signal, ReadOnlySpan<float> coefficients) => Convolve(signal, coefficients);

    /// <summary>
    /// Applies an infinite impulse response (IIR) filter to a signal.
    /// </summary>
    /// <param name="signal">The input signal.</param>
    /// <param name="b">The feedforward coefficients.</param>
    /// <param name="a">The feedback coefficients (a[0] should be 1).</param>
    /// <returns>The filtered signal.</returns>
    public static float[] ApplyIIRFilter(ReadOnlySpan<float> signal, ReadOnlySpan<float> b, ReadOnlySpan<float> a)
    {
        if (signal.Length == 0)
        {
            return [];
        }

        if (a.Length == 0 || Math.Abs(a[0]) < float.Epsilon)
        {
            throw new ArgumentException("Feedback coefficients must have at least one element with a[0] != 0.");
        }

        var result = new float[signal.Length];
        var delayLineX = new float[b.Length];
        var delayLineY = new float[a.Length];

        for (var n = 0; n < signal.Length; n++)
        {
            // Shift delay lines
            for (var i = delayLineX.Length - 1; i > 0; i--)
            {
                delayLineX[i] = delayLineX[i - 1];
            }
            delayLineX[0] = signal[n];

            // Calculate feedforward part
            float y = 0;
            for (var i = 0; i < b.Length && i < delayLineX.Length; i++)
            {
                y += b[i] * delayLineX[i];
            }

            // Calculate feedback part
            for (var i = 1; i < a.Length && i < delayLineY.Length; i++)
            {
                y -= a[i] * delayLineY[i];
            }

            y /= a[0];
            result[n] = y;

            // Update output delay line
            for (var i = delayLineY.Length - 1; i > 0; i--)
            {
                delayLineY[i] = delayLineY[i - 1];
            }
            delayLineY[0] = y;
        }

        return result;
    }

    /// <summary>
    /// Designs a low-pass FIR filter using the windowed sinc method.
    /// </summary>
    /// <param name="cutoffFreq">Normalized cutoff frequency (0 to 0.5).</param>
    /// <param name="filterLength">The filter length (should be odd).</param>
    /// <param name="windowType">The window type to apply.</param>
    /// <returns>The filter coefficients.</returns>
    public static float[] DesignLowPassFIR(float cutoffFreq, int filterLength, WindowType windowType = WindowType.Hamming)
    {
        if (cutoffFreq is <= 0 or >= 0.5f)
        {
            throw new ArgumentException("Cutoff frequency must be between 0 and 0.5 (Nyquist).");
        }

        if (filterLength % 2 == 0)
        {
            filterLength++; // Make it odd
        }

        var coefficients = new float[filterLength];
        var center = filterLength / 2;
        var wc = 2 * MathF.PI * cutoffFreq;

        // Generate ideal sinc function
        for (var i = 0; i < filterLength; i++)
        {
            var n = i - center;
            if (n == 0)
            {
                coefficients[i] = 2 * cutoffFreq;
            }
            else
            {
                coefficients[i] = MathF.Sin(wc * n) / (MathF.PI * n);
            }
        }

        // Apply window
        FFT.ApplyWindow(coefficients, windowType);

        // Normalize
        float sum = 0;
        for (var i = 0; i < filterLength; i++)
        {
            sum += coefficients[i];
        }

        if (Math.Abs(sum) > float.Epsilon)
        {
            for (var i = 0; i < filterLength; i++)
            {
                coefficients[i] /= sum;
            }
        }

        return coefficients;
    }

    /// <summary>
    /// Designs a high-pass FIR filter using spectral inversion.
    /// </summary>
    /// <param name="cutoffFreq">Normalized cutoff frequency (0 to 0.5).</param>
    /// <param name="filterLength">The filter length (should be odd).</param>
    /// <param name="windowType">The window type to apply.</param>
    /// <returns>The filter coefficients.</returns>
    public static float[] DesignHighPassFIR(float cutoffFreq, int filterLength, WindowType windowType = WindowType.Hamming)
    {
        // Design low-pass filter
        var lowPass = DesignLowPassFIR(cutoffFreq, filterLength, windowType);

        // Spectral inversion
        var center = filterLength / 2;
        for (var i = 0; i < filterLength; i++)
        {
            lowPass[i] = -lowPass[i];
        }
        lowPass[center] += 1.0f;

        return lowPass;
    }

    /// <summary>
    /// Designs a band-pass FIR filter.
    /// </summary>
    /// <param name="lowFreq">Normalized lower cutoff frequency.</param>
    /// <param name="highFreq">Normalized upper cutoff frequency.</param>
    /// <param name="filterLength">The filter length (should be odd).</param>
    /// <param name="windowType">The window type to apply.</param>
    /// <returns>The filter coefficients.</returns>
    public static float[] DesignBandPassFIR(float lowFreq, float highFreq, int filterLength, WindowType windowType = WindowType.Hamming)
    {
        if (lowFreq >= highFreq)
        {
            throw new ArgumentException("Lower frequency must be less than upper frequency.");
        }

        // Band-pass = low-pass(high) - low-pass(low)
        var highPass = DesignLowPassFIR(highFreq, filterLength, windowType);
        var lowPass = DesignLowPassFIR(lowFreq, filterLength, windowType);

        var result = new float[filterLength];
        for (var i = 0; i < filterLength; i++)
        {
            result[i] = highPass[i] - lowPass[i];
        }

        return result;
    }

    /// <summary>
    /// Resamples a signal to a new sample rate.
    /// </summary>
    /// <param name="signal">The input signal.</param>
    /// <param name="originalRate">The original sample rate.</param>
    /// <param name="targetRate">The target sample rate.</param>
    /// <returns>The resampled signal.</returns>
    public static float[] Resample(ReadOnlySpan<float> signal, float originalRate, float targetRate)
    {
        if (signal.Length == 0)
        {
            return [];
        }

        var ratio = targetRate / originalRate;
        var outputLength = (int)(signal.Length * ratio);
        var result = new float[outputLength];

        // Simple linear interpolation for now
        for (var i = 0; i < outputLength; i++)
        {
            var sourceIndex = i / ratio;
            var index1 = (int)sourceIndex;
            var index2 = Math.Min(index1 + 1, signal.Length - 1);
            var fraction = sourceIndex - index1;

            if (index1 < signal.Length)
            {
                result[i] = signal[index1] * (1 - fraction) + signal[index2] * fraction;
            }
        }

        return result;
    }

    private static void DirectConvolve(ReadOnlySpan<float> signal, ReadOnlySpan<float> kernel, Span<float> result)
    {
        for (var n = 0; n < result.Length; n++)
        {
            float sum = 0;
            for (var k = 0; k < kernel.Length; k++)
            {
                var signalIndex = n - k;
                if (signalIndex >= 0 && signalIndex < signal.Length)
                {
                    sum += signal[signalIndex] * kernel[k];
                }
            }
            result[n] = sum;
        }
    }

    private static void FFTConvolve(ReadOnlySpan<float> signal, ReadOnlySpan<float> kernel, Span<float> result)
    {
        // Find next power of 2
        var fftSize = 1;
        while (fftSize < result.Length)
        {
            fftSize <<= 1;
        }

        // Pad signals to FFT size
        var paddedSignal = new Complex[fftSize];
        var paddedKernel = new Complex[fftSize];

        for (var i = 0; i < signal.Length; i++)
        {
            paddedSignal[i] = new Complex(signal[i], 0);
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            paddedKernel[i] = new Complex(kernel[i], 0);
        }

        // Perform FFT
        FFT.Forward(paddedSignal);
        FFT.Forward(paddedKernel);

        // Multiply in frequency domain
        for (var i = 0; i < fftSize; i++)
        {
            paddedSignal[i] = paddedSignal[i] * paddedKernel[i];
        }

        // Inverse FFT
        FFT.Inverse(paddedSignal);

        // Copy result
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = paddedSignal[i].Real;
        }
    }
}
