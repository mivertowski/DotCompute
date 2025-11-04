// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Complex = DotCompute.Algorithms.SignalProcessing.Complex;
using DotCompute.Algorithms.SignalProcessing;

namespace DotCompute.Algorithms.Tests.SignalProcessing;

/// <summary>
/// Comprehensive tests for FFT operations.
/// </summary>
public sealed class FFTTests
{
    #region Forward FFT Tests

    [Fact]
    public void Forward_PowerOfTwo_CompletesSuccessfully()
    {
        // Arrange
        var data = new Complex[8];
        for (int i = 0; i < 8; i++)
        {
            data[i] = new Complex(i, 0);
        }

        // Act
        FFT.Forward(data);

        // Assert
        data.Should().NotBeNull();
        data.Length.Should().Be(8);
    }

    [Fact]
    public void Forward_NonPowerOfTwo_ThrowsArgumentException()
    {
        // Arrange
        var data = new Complex[7];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FFT.Forward(data));
    }

    [Fact]
    public void Forward_SingleElement_ReturnsElement()
    {
        // Arrange
        var data = new Complex[] { new Complex(5, 3) };

        // Act
        FFT.Forward(data);

        // Assert
        data[0].Real.Should().BeApproximately((double)5, (double)0.0001);
        data[0].Imaginary.Should().BeApproximately((double)3, (double)0.0001);
    }

    [Fact]
    public void Forward_RealSignal_ProducesHermitianSymmetry()
    {
        // Arrange - Real signal
        var data = new Complex[8];
        for (int i = 0; i < 8; i++)
        {
            data[i] = new Complex(i, 0);
        }

        // Act
        FFT.Forward(data);

        // Assert - Hermitian symmetry: X[k] = conj(X[N-k])
        for (int i = 1; i < 4; i++)
        {
            var k = i;
            var nMinusK = 8 - i;
            data[k].Real.Should().BeApproximately((double)data[nMinusK].Real, (double)0.01);
            data[k].Imaginary.Should().BeApproximately((double)-data[nMinusK].Imaginary, (double)0.01);
        }
    }

    [Fact]
    public void Forward_DCSignal_ProducesNonZeroDCComponent()
    {
        // Arrange - DC signal (all ones)
        var data = new Complex[8];
        for (int i = 0; i < 8; i++)
        {
            data[i] = new Complex(1, 0);
        }

        // Act
        FFT.Forward(data);

        // Assert
        data[0].Real.Should().BeApproximately((double)8, (double)0.001); // DC component
        for (int i = 1; i < 8; i++)
        {
            data[i].Magnitude.Should().BeLessThan((double)0.001); // AC components near zero
        }
    }

    #endregion

    #region Inverse FFT Tests

    [Fact]
    public void Inverse_AfterForward_ReconstructsSignal()
    {
        // Arrange
        var original = new Complex[8];
        for (int i = 0; i < 8; i++)
        {
            original[i] = new Complex(i, 0);
        }
        var data = (Complex[])original.Clone();

        // Act
        FFT.Forward(data);
        FFT.Inverse(data);

        // Assert
        for (int i = 0; i < 8; i++)
        {
            data[i].Real.Should().BeApproximately((double)original[i].Real, (double)0.01);
            data[i].Imaginary.Should().BeApproximately((double)original[i].Imaginary, (double)0.01);
        }
    }

    [Fact]
    public void Inverse_NonPowerOfTwo_ThrowsArgumentException()
    {
        // Arrange
        var data = new Complex[5];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FFT.Inverse(data));
    }

    [Fact]
    public void Inverse_ScalesCorrectly()
    {
        // Arrange
        var data = new Complex[4];
        data[0] = new Complex(4, 0); // DC component

        // Act
        FFT.Inverse(data);

        // Assert
        // After inverse, DC should be scaled by 1/N
        data[0].Real.Should().BeApproximately((double)1, (double)0.0001);
    }

    #endregion

    #region Real FFT Tests

    [Fact]
    public void RealFFT_RealSignal_ReturnsHalfSpectrum()
    {
        // Arrange
        var realData = new float[16];
        for (int i = 0; i < 16; i++)
        {
            realData[i] = MathF.Sin(2 * MathF.PI * i / 16);
        }

        // Act
        var result = FFT.RealFFT(realData);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(9); // N/2 + 1 for N=16
    }

    [Fact]
    public void RealFFT_NonPowerOfTwo_ThrowsArgumentException()
    {
        // Arrange
        var realData = new float[15];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FFT.RealFFT(realData));
    }

    [Fact]
    public void RealFFT_DCSignal_ProducesDCComponent()
    {
        // Arrange
        var realData = new float[8];
        for (int i = 0; i < 8; i++)
        {
            realData[i] = 5.0f;
        }

        // Act
        var result = FFT.RealFFT(realData);

        // Assert
        result[0].Real.Should().BeApproximately((double)40, (double)0.1); // 8 * 5
        for (int i = 1; i < result.Length; i++)
        {
            result[i].Magnitude.Should().BeLessThan((double)0.01);
        }
    }

    [Fact]
    public void RealFFT_SineWave_ProducesCorrectFrequencyPeak()
    {
        // Arrange - Single frequency sine wave
        var size = 64;
        var frequency = 4; // 4 cycles in 64 samples
        var realData = new float[size];
        for (int i = 0; i < size; i++)
        {
            realData[i] = MathF.Sin(2 * MathF.PI * frequency * i / size);
        }

        // Act
        var result = FFT.RealFFT(realData);

        // Assert - Peak should be at frequency bin 4
        var maxMagnitude = result.Select(c => c.Magnitude).Max();
        result[frequency].Magnitude.Should().BeGreaterThan((double)maxMagnitude * 0.9);
    }

    #endregion

    #region Inverse Real FFT Tests

    [Fact]
    public void InverseRealFFT_AfterRealFFT_ReconstructsSignal()
    {
        // Arrange
        var original = new float[16];
        for (int i = 0; i < 16; i++)
        {
            original[i] = i;
        }

        // Act
        var spectrum = FFT.RealFFT(original);
        var reconstructed = FFT.InverseRealFFT(spectrum, original.Length);

        // Assert
        for (int i = 0; i < original.Length; i++)
        {
            ((double)reconstructed[i]).Should().BeApproximately((double)original[i], (double)0.1);
        }
    }

    [Fact]
    public void InverseRealFFT_WrongLength_ThrowsArgumentException()
    {
        // Arrange
        var spectrum = new Complex[5];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            FFT.InverseRealFFT(spectrum, 10));
    }

    #endregion

    #region FFT Shift Tests

    [Fact]
    public void FFTShift_EvenLength_ShiftsCorrectly()
    {
        // Arrange
        var data = new Complex[8];
        for (int i = 0; i < 8; i++)
        {
            data[i] = new Complex(i, 0);
        }

        // Act
        FFT.FFTShift(data);

        // Assert
        data[0].Real.Should().BeApproximately((double)4, (double)0.0001);
        data[4].Real.Should().BeApproximately((double)0, (double)0.0001);
    }

    [Fact]
    public void FFTShift_OddLength_ShiftsCorrectly()
    {
        // Arrange
        var data = new Complex[7];
        for (int i = 0; i < 7; i++)
        {
            data[i] = new Complex(i, 0);
        }

        // Act
        FFT.FFTShift(data);

        // Assert
        data[0].Real.Should().BeApproximately((double)4, (double)0.0001);
    }

    #endregion

    #region Windowing Tests

    [Fact]
    public void ApplyHammingWindow_ModifiesSignal()
    {
        // Arrange
        var data = new float[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = 1.0f;
        }

        // Act
        FFT.ApplyHammingWindow(data);

        // Assert
        // Edges should be attenuated
        ((double)data[0]).Should().BeLessThan((double)0.1f);
        ((double)data[15]).Should().BeLessThan((double)0.1f);
        // Center should be close to 1
        ((double)data[8]).Should().BeGreaterThan((double)0.9f);
    }

    [Fact]
    public void ApplyHannWindow_ModifiesSignal()
    {
        // Arrange
        var data = new float[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = 1.0f;
        }

        // Act
        FFT.ApplyHannWindow(data);

        // Assert
        // Hann window should go to zero at edges
        data[0].Should().Be(0);
        ((double)data[15]).Should().BeApproximately((double)0, (double)0.0001f);
    }

    [Fact]
    public void ApplyBlackmanWindow_ModifiesSignal()
    {
        // Arrange
        var data = new float[32];
        for (int i = 0; i < 32; i++)
        {
            data[i] = 1.0f;
        }

        // Act
        FFT.ApplyBlackmanWindow(data);

        // Assert
        // Blackman window has very low edges
        ((double)data[0]).Should().BeLessThan((double)0.01f);
        ((double)data[31]).Should().BeLessThan((double)0.01f);
    }

    #endregion

    #region Power Spectrum Tests

    [Fact]
    public void PowerSpectrum_RealSignal_ReturnsPositiveValues()
    {
        // Arrange
        var data = new float[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = MathF.Sin(2 * MathF.PI * i / 16);
        }

        // Act
        var power = FFT.PowerSpectrum(data);

        // Assert
        power.Should().NotBeNull();
        power.All(p => p >= 0).Should().BeTrue();
    }

    [Fact]
    public void MagnitudeSpectrum_RealSignal_ReturnsNonNegativeValues()
    {
        // Arrange
        var data = new float[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = i;
        }

        // Act
        var magnitude = FFT.MagnitudeSpectrum(data);

        // Assert
        magnitude.Should().NotBeNull();
        magnitude.All(m => m >= 0).Should().BeTrue();
    }

    [Fact]
    public void PhaseSpectrum_RealSignal_ReturnsAngles()
    {
        // Arrange
        var data = new float[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = MathF.Cos(2 * MathF.PI * i / 16);
        }

        // Act
        var phase = FFT.PhaseSpectrum(data);

        // Assert
        phase.Should().NotBeNull();
        // Phase should be within [-π, π]
        phase.All(p => p >= -MathF.PI && p <= MathF.PI).Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Performance Tests

    [Fact]
    public void Forward_LargeArray_CompletesSuccessfully()
    {
        // Arrange
        var data = new Complex[4096];
        for (int i = 0; i < 4096; i++)
        {
            data[i] = new Complex(MathF.Sin(i * 0.1f), 0);
        }

        // Act
        FFT.Forward(data);

        // Assert
        data.Should().NotBeNull();
    }

    [Fact]
    public void RealFFT_EmptyArray_ThrowsArgumentException()
    {
        // Arrange
        var data = Array.Empty<float>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => FFT.RealFFT(data));
    }

    [Fact]
    public void Forward_ComplexSignal_PreservesEnergy()
    {
        // Arrange
        var data = new Complex[32];
        for (int i = 0; i < 32; i++)
        {
            data[i] = new Complex(MathF.Sin(i), MathF.Cos(i));
        }
        var originalEnergy = data.Sum(c => c.Magnitude * c.Magnitude);

        // Act
        FFT.Forward(data);
        var transformedEnergy = data.Sum(c => c.Magnitude * c.Magnitude) / 32;

        // Assert - Parseval's theorem: energy is preserved
        transformedEnergy.Should().BeApproximately((float)originalEnergy, 0.1f);
    }

    [Fact]
    public void Forward_MultipleTransforms_ProducesSameResult()
    {
        // Arrange
        var data1 = new Complex[16];
        var data2 = new Complex[16];
        for (int i = 0; i < 16; i++)
        {
            data1[i] = data2[i] = new Complex(i, 0);
        }

        // Act
        FFT.Forward(data1);
        FFT.Forward(data2);

        // Assert
        for (int i = 0; i < 16; i++)
        {
            data1[i].Real.Should().BeApproximately((double)data2[i].Real, (double)0.0001);
            data1[i].Imaginary.Should().BeApproximately((double)data2[i].Imaginary, (double)0.0001);
        }
    }

    #endregion
}
