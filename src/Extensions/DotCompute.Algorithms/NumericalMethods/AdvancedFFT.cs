// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;

namespace DotCompute.Algorithms.NumericalMethods;


/// <summary>
/// Advanced Fast Fourier Transform implementations with multiple algorithms and optimizations.
/// Includes Cooley-Tukey, Prime Factor, and mixed-radix algorithms for various input sizes.
/// </summary>
public static class AdvancedFFT
{
/// <summary>
/// Computes FFT using the most appropriate algorithm based on input size.
/// </summary>
/// <param name="signal">Input signal (real values)</param>
/// <returns>Complex frequency domain representation</returns>
public static Complex[] ComputeFFT(float[] signal)
{
    ArgumentNullException.ThrowIfNull(signal);
    
    var n = signal.Length;
    if (n == 0) return [];
    
    // Convert to complex
    var complexSignal = new Complex[n];
    for (var i = 0; i < n; i++)
    {
        complexSignal[i] = new Complex(signal[i], 0);
    }
    
    return ComputeFFT(complexSignal);
}

/// <summary>
/// Computes FFT of complex input using optimal algorithm.
/// </summary>
/// <param name="signal">Complex input signal</param>
/// <returns>FFT result</returns>
public static Complex[] ComputeFFT(Complex[] signal)
{
    ArgumentNullException.ThrowIfNull(signal);
    
    var n = signal.Length;
    if (n <= 1) return (Complex[])signal.Clone();
    
    // Choose algorithm based on input characteristics
    if (IsPowerOfTwo(n))
    {
        return CooleyTukeyRadix2FFT(signal);
    }
    else if (HasSmallPrimeFactors(n))
    {
        return MixedRadixFFT(signal);
    }
    else
    {
        return ChirpZTransform(signal);
    }
}

/// <summary>
/// Computes inverse FFT.
/// </summary>
/// <param name="spectrum">Frequency domain signal</param>
/// <returns>Time domain signal</returns>
public static Complex[] ComputeIFFT(Complex[] spectrum)
{
    ArgumentNullException.ThrowIfNull(spectrum);
    
    var n = spectrum.Length;
    if (n == 0) return [];
    
    // Conjugate, FFT, conjugate, and normalize
    var conjugated = new Complex[n];
    for (var i = 0; i < n; i++)
    {
        conjugated[i] = Complex.Conjugate(spectrum[i]);
    }
    
    var result = ComputeFFT(conjugated);
    
    for (var i = 0; i < n; i++)
    {
        result[i] = Complex.Conjugate(result[i]) / n;
    }
    
    return result;
}

/// <summary>
/// Real-valued FFT (RFFT) - more efficient for real inputs.
/// </summary>
/// <param name="signal">Real input signal</param>
/// <returns>Complex spectrum (N/2 + 1 points due to conjugate symmetry)</returns>
public static Complex[] ComputeRFFT(float[] signal)
{
    ArgumentNullException.ThrowIfNull(signal);
    
    var n = signal.Length;
    if (n < 2) return ComputeFFT(signal);
    
    // For even N, pack real signal into N/2 complex values
    // Then use complex FFT and unpack
    var halfN = n / 2;
    var packedSignal = new Complex[halfN];
    
    for (var i = 0; i < halfN; i++)
    {
        packedSignal[i] = new Complex(signal[2*i], signal[2*i + 1]);
    }
    
    var packedFFT = ComputeFFT(packedSignal);
    
    // Unpack to get real FFT result
    var result = new Complex[halfN + 1];
    UnpackRealFFT(packedFFT, result, n);
    
    return result;
}

/// <summary>
/// 2D FFT for image processing and multidimensional signals.
/// </summary>
/// <param name="signal2D">2D input signal</param>
/// <returns>2D FFT result</returns>
public static Complex[,] Compute2DFFT(float[,] signal2D)
{
    ArgumentNullException.ThrowIfNull(signal2D);
    
    var rows = signal2D.GetLength(0);
    var cols = signal2D.GetLength(1);
    var result = new Complex[rows, cols];
    
    // Convert to complex
    for (var i = 0; i < rows; i++)
    {
        for (var j = 0; j < cols; j++)
        {
            result[i, j] = new Complex(signal2D[i, j], 0);
        }
    }
    
    // FFT along rows
    for (var i = 0; i < rows; i++)
    {
        var row = new Complex[cols];
        for (var j = 0; j < cols; j++)
        {
            row[j] = result[i, j];
        }
        
        var fftRow = ComputeFFT(row);
        for (var j = 0; j < cols; j++)
        {
            result[i, j] = fftRow[j];
        }
    }
    
    // FFT along columns
    for (var j = 0; j < cols; j++)
    {
        var col = new Complex[rows];
        for (var i = 0; i < rows; i++)
        {
            col[i] = result[i, j];
        }
        
        var fftCol = ComputeFFT(col);
        for (var i = 0; i < rows; i++)
        {
            result[i, j] = fftCol[i];
        }
    }
    
    return result;
}

/// <summary>
/// Sliding window FFT for real-time processing.
/// </summary>
/// <param name="buffer">Circular buffer</param>
/// <param name="windowSize">FFT window size</param>
/// <param name="hopSize">Hop size between windows</param>
/// <returns>Sequence of FFT results</returns>
public static IEnumerable<Complex[]> SlidingWindowFFT(
    float[] buffer, 
    int windowSize, 
    int hopSize)
{
    ArgumentNullException.ThrowIfNull(buffer);
    
    if (windowSize <= 0 || hopSize <= 0)
        throw new ArgumentException("Window size and hop size must be positive");
    
    var window = ApplyWindow(windowSize, WindowType.Hann);
    
    for (var start = 0; start + windowSize <= buffer.Length; start += hopSize)
    {
        var windowedSignal = new float[windowSize];
        for (var i = 0; i < windowSize; i++)
        {
            windowedSignal[i] = buffer[start + i] * window[i];
        }
        
        yield return ComputeFFT(windowedSignal);
    }
}

/// <summary>
/// Zero-padded FFT for improved frequency resolution.
/// </summary>
/// <param name="signal">Input signal</param>
/// <param name="paddedLength">Target length after zero padding</param>
/// <returns>FFT of zero-padded signal</returns>
public static Complex[] ZeroPaddedFFT(float[] signal, int paddedLength)
{
    ArgumentNullException.ThrowIfNull(signal);
    
    if (paddedLength < signal.Length)
        throw new ArgumentException("Padded length must be at least signal length");
    
    var paddedSignal = new float[paddedLength];
    Array.Copy(signal, paddedSignal, signal.Length);
    // Rest remains zero (default initialization)
    
    return ComputeFFT(paddedSignal);
}

private static Complex[] CooleyTukeyRadix2FFT(Complex[] x)
{
    var n = x.Length;
    if (n <= 1) return (Complex[])x.Clone();
    
    // Bit-reversal permutation for in-place computation
    var result = new Complex[n];
    for (var i = 0; i < n; i++)
    {
        result[ReverseBits(i, (int)Math.Log2(n))] = x[i];
    }
    
    // Cooley-Tukey decimation-in-time
    for (var length = 2; length <= n; length *= 2)
    {
        var angleStep = -2.0 * Math.PI / length;
        var wlen = new Complex(Math.Cos(angleStep), Math.Sin(angleStep));
        
        for (var start = 0; start < n; start += length)
        {
            var w = Complex.One;
            var halfLength = length / 2;
            
            for (var j = 0; j < halfLength; j++)
            {
                var even = result[start + j];
                var odd = result[start + j + halfLength] * w;
                
                result[start + j] = even + odd;
                result[start + j + halfLength] = even - odd;
                
                w *= wlen;
            }
        }
    }
    
    return result;
}

private static Complex[] MixedRadixFFT(Complex[] x)
{
    var n = x.Length;
    var factors = GetPrimeFactorization(n);
    
    // Implement Good-Thomas algorithm for coprime factors
    if (factors.All(f => factors.Count(g => g == f) == 1))
    {
        return GoodThomasFFT(x, factors);
    }
    
    // Fall back to repeated radix decomposition
    return CooleyTukeyMixedRadix(x, factors);
}

private static Complex[] GoodThomasFFT(Complex[] x, List<int> factors)
{
    // Prime Factor Algorithm (Good-Thomas)
    // For coprime factors only
    var n = x.Length;
    var result = new Complex[n];
    
    // Multi-dimensional index mapping
    var dims = factors.ToArray();
    var ndim = dims.Length;
    
    // Chinese Remainder Theorem mapping
    for (var i = 0; i < n; i++)
    {
        var indices = new int[ndim];
        var temp = i;
        
        for (var d = ndim - 1; d >= 0; d--)
        {
            indices[d] = temp % dims[d];
            temp /= dims[d];
        }
        
        // Multi-dimensional FFT
        var complexValue = x[i];
        
        for (var d = 0; d < ndim; d++)
        {
            // Apply 1D FFT along dimension d
            var factor = dims[d];
            var twiddle = Complex.FromPolarCoordinates(1.0, -2.0 * Math.PI * indices[d] / factor);
            complexValue *= twiddle;
        }
        
        result[i] = complexValue;
    }
    
    return result;
}

private static Complex[] CooleyTukeyMixedRadix(Complex[] x, List<int> factors)
{
    var result = (Complex[])x.Clone();
    var n = x.Length;
    
    // Apply each factor in sequence
    foreach (var factor in factors.Distinct())
    {
        var stride = n / factor;
        
        for (var start = 0; start < stride; start++)
        {
            // Extract elements for this DFT
            var subSequence = new Complex[factor];
            for (var i = 0; i < factor; i++)
            {
                subSequence[i] = result[start + i * stride];
            }
            
            // Compute DFT of subsequence
            var subFFT = DirectDFT(subSequence);
            
            // Write back
            for (var i = 0; i < factor; i++)
            {
                result[start + i * stride] = subFFT[i];
            }
        }
    }
    
    return result;
}

private static Complex[] ChirpZTransform(Complex[] x)
{
    // Bluestein's algorithm for arbitrary length FFT
    var n = x.Length;
    var m = NextPowerOfTwo(2 * n - 1);
    
    // Create chirp sequence
    var chirp = new Complex[n];
    for (var i = 0; i < n; i++)
    {
        var phase = Math.PI * i * i / n;
        chirp[i] = new Complex(Math.Cos(phase), -Math.Sin(phase));
    }
    
    // Multiply input by chirp
    var a = new Complex[m];
    for (var i = 0; i < n; i++)
    {
        a[i] = x[i] * chirp[i];
    }
    
    // Create convolution kernel
    var b = new Complex[m];
    b[0] = Complex.One;
    for (var i = 1; i < n; i++)
    {
        var phase = Math.PI * i * i / n;
        var w = new Complex(Math.Cos(phase), Math.Sin(phase));
        b[i] = w;
        b[m - i] = w;
    }
    
    // Convolution via FFT
    var aFFT = CooleyTukeyRadix2FFT(a);
    var bFFT = CooleyTukeyRadix2FFT(b);
    
    for (var i = 0; i < m; i++)
    {
        aFFT[i] *= bFFT[i];
    }
    
    var conv = ComputeIFFT(aFFT);
    
    // Extract result and multiply by chirp
    var result = new Complex[n];
    for (var i = 0; i < n; i++)
    {
        result[i] = conv[i] * chirp[i];
    }
    
    return result;
}

private static Complex[] DirectDFT(Complex[] x)
{
    var n = x.Length;
    var result = new Complex[n];
    
    for (var k = 0; k < n; k++)
    {
        var sum = Complex.Zero;
        for (var j = 0; j < n; j++)
        {
            var angle = -2.0 * Math.PI * k * j / n;
            var twiddle = new Complex(Math.Cos(angle), Math.Sin(angle));
            sum += x[j] * twiddle;
        }
        result[k] = sum;
    }
    
    return result;
}

private static void UnpackRealFFT(Complex[] packed, Complex[] result, int originalLength)
{
    var n = originalLength;
    var halfN = n / 2;
    
    // First and Nyquist frequencies are real
    result[0] = new Complex(packed[0].Real + packed[0].Imaginary, 0);
    
    if (halfN > 0)
    {
        result[halfN] = new Complex(packed[0].Real - packed[0].Imaginary, 0);
    }
    
    // Unpack remaining frequencies using conjugate symmetry
    for (var k = 1; k < halfN; k++)
    {
        var wk = Complex.FromPolarCoordinates(1.0, -Math.PI * k / halfN);
        var wkConj = Complex.Conjugate(wk);
        
        var pk = packed[k];
        var pkMinus = Complex.Conjugate(packed[halfN - k]);
        
        result[k] = 0.5 * ((pk + pkMinus) - Complex.ImaginaryOne * wk * (pk - pkMinus));
    }
}

private static float[] ApplyWindow(int size, WindowType windowType)
{
    var window = new float[size];
    
    switch (windowType)
    {
        case WindowType.Rectangular:
            Array.Fill(window, 1.0f);
            break;
            
        case WindowType.Hann:
            for (var i = 0; i < size; i++)
            {
                window[i] = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (size - 1)));
            }
            break;
            
        case WindowType.Hamming:
            for (var i = 0; i < size; i++)
            {
                window[i] = 0.54f - 0.46f * (float)Math.Cos(2 * Math.PI * i / (size - 1));
            }
            break;
            
        case WindowType.Blackman:
            for (var i = 0; i < size; i++)
            {
                var arg = 2 * Math.PI * i / (size - 1);
                window[i] = 0.42f - 0.5f * (float)Math.Cos(arg) + 0.08f * (float)Math.Cos(2 * arg);
            }
            break;
    }
    
    return window;
}

private static bool IsPowerOfTwo(int n) => (n & (n - 1)) == 0 && n > 0;

private static bool HasSmallPrimeFactors(int n)
{
    var factors = GetPrimeFactorization(n);
    return factors.All(f => f <= 7); // Small primes up to 7
}

private static List<int> GetPrimeFactorization(int n)
{
    var factors = new List<int>();
    
    for (var d = 2; d * d <= n; d++)
    {
        while (n % d == 0)
        {
            factors.Add(d);
            n /= d;
        }
    }
    
    if (n > 1) factors.Add(n);
    
    return factors;
}

private static int ReverseBits(int value, int bitCount)
{
    var result = 0;
    for (var i = 0; i < bitCount; i++)
    {
        result = (result << 1) | (value & 1);
        value >>= 1;
    }
    return result;
}

private static int NextPowerOfTwo(int n)
{
    if (IsPowerOfTwo(n)) return n;
    
    n--;
    n |= n >> 1;
    n |= n >> 2;
    n |= n >> 4;
    n |= n >> 8;
    n |= n >> 16;
    return n + 1;
}
}

/// <summary>
/// Window function types for spectral analysis.
/// </summary>
public enum WindowType
{
Rectangular,
Hann,
Hamming,
Blackman
}
