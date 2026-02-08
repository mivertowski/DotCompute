
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using Complex = DotCompute.Algorithms.SignalProcessing.Complex;

namespace DotCompute.Algorithms.SignalProcessing;


/// <summary>
/// Provides Fast Fourier Transform (FFT) operations.
/// </summary>
public static class FFT
{
    private const float TwoPi = 2.0f * MathF.PI;

    /// <summary>
    /// Performs a forward FFT on complex data.
    /// </summary>
    /// <param name="data">The complex data array (will be modified in-place).</param>
    public static void Forward(Span<Complex> data)
    {
        var n = data.Length;

        // Verify power of 2
        if ((n & (n - 1)) != 0)
        {
            throw new ArgumentException("FFT length must be a power of 2.");
        }

        // Bit reversal
        BitReverseReorder(data);

        // Cooley-Tukey FFT
        CooleyTukeyFFT(data, false);
    }

    /// <summary>
    /// Performs an inverse FFT on complex data.
    /// </summary>
    /// <param name="data">The complex data array (will be modified in-place).</param>
    public static void Inverse(Span<Complex> data)
    {
        var n = data.Length;

        // Verify power of 2
        if ((n & (n - 1)) != 0)
        {
            throw new ArgumentException("FFT length must be a power of 2.");
        }

        // Bit reversal
        BitReverseReorder(data);

        // Cooley-Tukey inverse FFT
        CooleyTukeyFFT(data, true);

        // Scale by 1/N
        var scale = 1.0f / n;
        for (var i = 0; i < n; i++)
        {
            data[i] = data[i] * scale;
        }
    }

    /// <summary>
    /// Performs a real-valued FFT.
    /// </summary>
    /// <param name="realData">The real-valued input data.</param>
    /// <returns>Complex FFT result (only first N/2+1 elements are unique).</returns>
    public static Complex[] RealFFT(ReadOnlySpan<float> realData)
    {
        var n = realData.Length;

        // Check for empty array
        if (n == 0)
        {
            throw new ArgumentException("Input data cannot be empty.", nameof(realData));
        }

        // Verify power of 2
        if ((n & (n - 1)) != 0)
        {
            throw new ArgumentException("FFT length must be a power of 2.", nameof(realData));
        }

        // Convert to complex
        var complexData = new Complex[n];
        for (var i = 0; i < n; i++)
        {
            complexData[i] = new Complex(realData[i], 0);
        }

        // Perform FFT
        Forward(complexData);

        // For real input, only first N/2+1 elements are unique
        // due to Hermitian symmetry
        var result = new Complex[n / 2 + 1];
        Array.Copy(complexData, result, n / 2 + 1);

        return result;
    }

    /// <summary>
    /// Performs an inverse real-valued FFT.
    /// </summary>
    /// <param name="complexData">The complex FFT data.</param>
    /// <param name="outputLength">The desired output length.</param>
    /// <returns>Real-valued time domain signal.</returns>
    public static float[] InverseRealFFT(ReadOnlySpan<Complex> complexData, int outputLength)
    {
        // Verify power of 2
        if ((outputLength & (outputLength - 1)) != 0)
        {
            throw new ArgumentException("Output length must be a power of 2.");
        }

        // Reconstruct full complex array using Hermitian symmetry
        var fullComplex = new Complex[outputLength];

        // Copy provided data
        var providedLength = Math.Min(complexData.Length, outputLength / 2 + 1);
        for (var i = 0; i < providedLength; i++)
        {
            fullComplex[i] = complexData[i];
        }

        // Fill using Hermitian symmetry
        for (var i = 1; i < outputLength / 2; i++)
        {
            fullComplex[outputLength - i] = fullComplex[i].Conjugate;
        }

        // Perform inverse FFT
        Inverse(fullComplex);

        // Extract real parts
        var result = new float[outputLength];
        for (var i = 0; i < outputLength; i++)
        {
            result[i] = fullComplex[i].Real;
        }

        return result;
    }

    /// <summary>
    /// Computes the power spectrum from FFT data.
    /// </summary>
    /// <param name="fftData">The FFT data.</param>
    /// <returns>Power spectrum (magnitude squared).</returns>
    public static float[] PowerSpectrum(ReadOnlySpan<Complex> fftData)
    {
        var result = new float[fftData.Length];
        for (var i = 0; i < fftData.Length; i++)
        {
            var c = fftData[i];
            result[i] = c.Real * c.Real + c.Imaginary * c.Imaginary;
        }
        return result;
    }

    /// <summary>
    /// Computes the magnitude spectrum from FFT data.
    /// </summary>
    /// <param name="fftData">The FFT data.</param>
    /// <returns>Magnitude spectrum.</returns>
    public static float[] MagnitudeSpectrum(ReadOnlySpan<Complex> fftData)
    {
        var result = new float[fftData.Length];
        for (var i = 0; i < fftData.Length; i++)
        {
            result[i] = fftData[i].Magnitude;
        }
        return result;
    }

    /// <summary>
    /// Computes the phase spectrum from FFT data.
    /// </summary>
    /// <param name="fftData">The FFT data.</param>
    /// <returns>Phase spectrum in radians.</returns>
    public static float[] PhaseSpectrum(ReadOnlySpan<Complex> fftData)
    {
        var result = new float[fftData.Length];
        for (var i = 0; i < fftData.Length; i++)
        {
            result[i] = fftData[i].Phase;
        }
        return result;
    }

    private static void BitReverseReorder(Span<Complex> data)
    {
        var n = data.Length;
        var j = 0;

        for (var i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }

            var k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }
    }

    private static void CooleyTukeyFFT(Span<Complex> data, bool inverse)
    {
        var n = data.Length;
        var logN = BitOperations.Log2((uint)n);

        // Precompute twiddle factors for better performance
        var angleSign = inverse ? 1.0f : -1.0f;

        // Cooley-Tukey decimation-in-time
        for (var s = 1; s <= logN; s++)
        {
            var m = 1 << s;        // 2^s
            var m2 = m >> 1;       // m/2

            // Twiddle factor
            var angle = angleSign * TwoPi / m;
            var w = Complex.Exp(angle);
            var wm = new Complex(1, 0); // Complex.One

            for (var j = 0; j < m2; j++)
            {
                for (var k = j; k < n; k += m)
                {
                    var t = k + m2;
                    var u = data[k];
                    var v = data[t] * wm;
                    data[k] = u + v;
                    data[t] = u - v;
                }
                wm = wm * w;
            }
        }
    }

    /// <summary>
    /// Shifts the zero-frequency component to the center of the spectrum.
    /// </summary>
    /// <param name="data">The FFT data to shift.</param>
    public static void FFTShift(Span<Complex> data)
    {
        var n = data.Length;
        var half = n / 2;

        // Swap the two halves
        for (var i = 0; i < half; i++)
        {
            (data[i], data[i + half]) = (data[i + half], data[i]);
        }

        // For odd lengths, shift one more element
        if ((n & 1) != 0)
        {
            var temp = data[half];
            for (var i = half; i > 0; i--)
            {
                data[i] = data[i - 1];
            }
            data[0] = temp;
        }
    }

    /// <summary>
    /// Applies a Hamming window to the data.
    /// </summary>
    /// <param name="data">The data to window.</param>
    public static void ApplyHammingWindow(Span<float> data)
    {
        ApplyWindow(data, WindowType.Hamming);
    }

    /// <summary>
    /// Applies a Hann (Hanning) window to the data.
    /// </summary>
    /// <param name="data">The data to window.</param>
    public static void ApplyHannWindow(Span<float> data)
    {
        ApplyWindow(data, WindowType.Hanning);
    }

    /// <summary>
    /// Applies a Blackman window to the data.
    /// </summary>
    /// <param name="data">The data to window.</param>
    public static void ApplyBlackmanWindow(Span<float> data)
    {
        ApplyWindow(data, WindowType.Blackman);
    }

    /// <summary>
    /// Computes the power spectrum directly from real data.
    /// </summary>
    /// <param name="realData">The real-valued input data.</param>
    /// <returns>Power spectrum (magnitude squared).</returns>
    public static float[] PowerSpectrum(ReadOnlySpan<float> realData)
    {
        var fftData = RealFFT(realData);
        return PowerSpectrum(fftData);
    }

    /// <summary>
    /// Computes the magnitude spectrum directly from real data.
    /// </summary>
    /// <param name="realData">The real-valued input data.</param>
    /// <returns>Magnitude spectrum.</returns>
    public static float[] MagnitudeSpectrum(ReadOnlySpan<float> realData)
    {
        var fftData = RealFFT(realData);
        return MagnitudeSpectrum(fftData);
    }

    /// <summary>
    /// Computes the phase spectrum directly from real data.
    /// </summary>
    /// <param name="realData">The real-valued input data.</param>
    /// <returns>Phase spectrum in radians.</returns>
    public static float[] PhaseSpectrum(ReadOnlySpan<float> realData)
    {
        var fftData = RealFFT(realData);
        return PhaseSpectrum(fftData);
    }

    /// <summary>
    /// Gets the frequency bin for a given index in FFT output.
    /// </summary>
    /// <param name="index">The FFT bin index.</param>
    /// <param name="fftSize">The FFT size.</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    /// <returns>The frequency in Hz.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetFrequency(int index, int fftSize, float sampleRate) => index * sampleRate / fftSize;

    /// <summary>
    /// Applies a window function to the data before FFT.
    /// </summary>
    /// <param name="data">The data to window.</param>
    /// <param name="windowType">The window type.</param>
    public static void ApplyWindow(Span<float> data, WindowType windowType)
    {
        var n = data.Length;

        switch (windowType)
        {
            case WindowType.Hamming:
                for (var i = 0; i < n; i++)
                {
                    data[i] *= 0.54f - 0.46f * MathF.Cos(TwoPi * i / (n - 1));
                }
                break;

            case WindowType.Hanning:
                for (var i = 0; i < n; i++)
                {
                    data[i] *= 0.5f * (1 - MathF.Cos(TwoPi * i / (n - 1)));
                }
                break;

            case WindowType.Blackman:
                for (var i = 0; i < n; i++)
                {
                    var t = TwoPi * i / (n - 1);
                    data[i] *= 0.42f - 0.5f * MathF.Cos(t) + 0.08f * MathF.Cos(2 * t);
                }
                break;

            case WindowType.Rectangular:
            default:
                // No windowing
                break;
        }
    }
}

/// <summary>
/// Defines window function types for FFT.
/// </summary>
public enum WindowType
{
    /// <summary>
    /// Rectangular (no) window.
    /// </summary>
    Rectangular,

    /// <summary>
    /// Hamming window.
    /// </summary>
    Hamming,

    /// <summary>
    /// Hanning (Hann) window.
    /// </summary>
    Hanning,

    /// <summary>
    /// Blackman window.
    /// </summary>
    Blackman
}
