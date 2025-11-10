// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA.Timing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Timing;

/// <summary>
/// Comprehensive unit tests for CudaClockCalibrator.
/// Tests all four calibration strategies and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CudaTiming")]
public sealed class CudaClockCalibratorTests
{
    private readonly CudaClockCalibrator _calibrator;

    public CudaClockCalibratorTests()
    {
        _calibrator = new CudaClockCalibrator(NullLogger.Instance);
    }

    #region ClockCalibration Structure Tests

    [Fact]
    public void ClockCalibration_DefaultValues_AreZero()
    {
        // Arrange & Act
        var calibration = new ClockCalibration();

        // Assert
        calibration.OffsetNanos.Should().Be(0);
        calibration.DriftPPM.Should().Be(0.0);
        calibration.ErrorBoundNanos.Should().Be(0);
        calibration.SampleCount.Should().Be(0);
        calibration.CalibrationTimestampNanos.Should().Be(0);
    }

    [Fact]
    public void ClockCalibration_WithValues_StoresCorrectly()
    {
        // Arrange & Act
        var calibration = new ClockCalibration
        {
            OffsetNanos = 1_000_000,
            DriftPPM = 5.5,
            ErrorBoundNanos = 100,
            SampleCount = 50,
            CalibrationTimestampNanos = 1234567890
        };

        // Assert
        calibration.OffsetNanos.Should().Be(1_000_000);
        calibration.DriftPPM.Should().Be(5.5);
        calibration.ErrorBoundNanos.Should().Be(100);
        calibration.SampleCount.Should().Be(50);
        calibration.CalibrationTimestampNanos.Should().Be(1234567890);
    }

    [Theory]
    [InlineData(100_000, 1.0, 50, 50_000)]
    [InlineData(-50_000, -0.5, 100, 100_000)]
    [InlineData(0, 0.0, 10, 10_000)]
    public void ClockCalibration_VariousScenarios_HandlesCorrectly(
        long offset, double drift, int samples, long error)
    {
        // Arrange & Act
        var calibration = new ClockCalibration
        {
            OffsetNanos = offset,
            DriftPPM = drift,
            SampleCount = samples,
            ErrorBoundNanos = error
        };

        // Assert
        calibration.OffsetNanos.Should().Be(offset);
        calibration.DriftPPM.Should().Be(drift);
        calibration.SampleCount.Should().Be(samples);
        calibration.ErrorBoundNanos.Should().Be(error);
    }

    #endregion

    #region Basic Strategy Tests

    [Fact]
    public void Calibrate_BasicStrategy_WithPerfectLinearData_ReturnsAccurateResults()
    {
        // Arrange - Perfect linear relationship: GPU = CPU + 1000
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select(t => t + 1000).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Basic);

        // Assert
        result.OffsetNanos.Should().BeInRange(950, 1050); // ±50ns tolerance
        result.DriftPPM.Should().BeInRange(-100, 100); // Near-zero drift
        result.SampleCount.Should().Be(50);
        result.ErrorBoundNanos.Should().BeGreaterThanOrEqualTo(0); // Perfect data may have 0 error
    }

    [Fact]
    public void Calibrate_BasicStrategy_WithConstantDrift_DetectsDriftCorrectly()
    {
        // Arrange - Linear with drift: GPU = 1.00001 * CPU + 1000 (10 PPM drift)
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i * 1_000_000).ToList();
        var gpuTimes = cpuTimes.Select(t => (long)(1.00001 * t + 1000)).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Basic);

        // Assert
        result.OffsetNanos.Should().BeInRange(900, 1100);
        result.DriftPPM.Should().BeInRange(8, 12); // ~10 PPM expected
        result.SampleCount.Should().Be(50);
    }

    [Fact]
    public void Calibrate_BasicStrategy_MinimumSamples_Succeeds()
    {
        // Arrange - Exactly 10 samples (minimum required)
        var cpuTimes = Enumerable.Range(0, 10).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select(t => t + 500).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Basic);

        // Assert
        result.SampleCount.Should().Be(10);
        result.OffsetNanos.Should().BeInRange(400, 600);
    }

    #endregion

    #region Robust Strategy Tests

    [Fact]
    public void Calibrate_RobustStrategy_WithOutliers_RemovesOutliersCorrectly()
    {
        // Arrange - Good linear data with 5 outliers
        var cpuTimes = new List<long>();
        var gpuTimes = new List<long>();

        // Add 45 good samples
        for (int i = 0; i < 45; i++)
        {
            cpuTimes.Add(i * 1000);
            gpuTimes.Add(i * 1000 + 1000);
        }

        // Add 5 outliers
        for (int i = 45; i < 50; i++)
        {
            cpuTimes.Add(i * 1000);
            gpuTimes.Add(i * 1000 + 50000); // Large offset outliers
        }

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Robust);

        // Assert
        result.SampleCount.Should().BeLessThan(50); // Some outliers should be removed
        result.SampleCount.Should().BeGreaterThanOrEqualTo(10); // But not below minimum
        result.OffsetNanos.Should().BeInRange(900, 1100); // Should match clean data
    }

    [Fact]
    public void Calibrate_RobustStrategy_WithAllGoodData_KeepsAllSamples()
    {
        // Arrange - All good samples
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select(t => t + 1000).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Robust);

        // Assert
        result.SampleCount.Should().Be(50); // All samples retained
        result.OffsetNanos.Should().BeInRange(950, 1050);
    }

    [Fact]
    public void Calibrate_RobustStrategy_ComparedToBasic_HasSmallerError()
    {
        // Arrange - Data with moderate noise
        var random = new Random(42);
        var cpuTimes = Enumerable.Range(0, 100).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select(t => t + 1000 + random.Next(-100, 100)).ToList();

        // Add outliers
        gpuTimes[10] += 10000;
        gpuTimes[50] -= 10000;
        gpuTimes[90] += 15000;

        // Act
        var basicResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Basic);
        var robustResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Robust);

        // Assert
        robustResult.ErrorBoundNanos.Should().BeLessThan(basicResult.ErrorBoundNanos);
    }

    [Fact]
    public void Calibrate_RobustStrategy_IterativeRemoval_ConvergesCorrectly()
    {
        // Arrange - Progressive outliers
        var cpuTimes = Enumerable.Range(0, 60).Select(i => (long)i * 1000).ToList();
        var gpuTimes = new List<long>();

        for (int i = 0; i < 60; i++)
        {
            if (i % 10 == 0 && i > 0)
            {
                gpuTimes.Add(cpuTimes[i] + 20000); // Outlier every 10 samples
            }
            else
            {
                gpuTimes.Add(cpuTimes[i] + 1000); // Normal
            }
        }

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Robust);

        // Assert
        result.SampleCount.Should().BeLessThan(60); // Outliers removed
        result.OffsetNanos.Should().BeInRange(900, 1100); // Clean estimate
    }

    #endregion

    #region Weighted Strategy Tests

    [Fact]
    public void Calibrate_WeightedStrategy_FavorsRecentSamples()
    {
        // Arrange - Drift that changes over time
        var cpuTimes = Enumerable.Range(0, 100).Select(i => (long)i * 10000).ToList();
        var gpuTimes = new List<long>();

        // First 50 samples: offset = 1000
        for (int i = 0; i < 50; i++)
        {
            gpuTimes.Add(cpuTimes[i] + 1000);
        }

        // Last 50 samples: offset = 2000 (drift changed)
        for (int i = 50; i < 100; i++)
        {
            gpuTimes.Add(cpuTimes[i] + 2000);
        }

        // Act
        var weightedResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Weighted);
        var basicResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Basic);

        // Assert - Weighted should favor recent samples (closer to 2000 than basic's ~1500)
        weightedResult.OffsetNanos.Should().BeGreaterThan(basicResult.OffsetNanos);
        // Weighted regression with 0.95 decay won't fully reach 2000, but should be > basic
        weightedResult.OffsetNanos.Should().BeInRange(1000, 1600);
    }

    [Fact]
    public void Calibrate_WeightedStrategy_WithConstantData_MatchesBasic()
    {
        // Arrange - Consistent data
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select(t => t + 1000).ToList();

        // Act
        var weightedResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Weighted);
        var basicResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Basic);

        // Assert - Should be very close when data is consistent
        Math.Abs(weightedResult.OffsetNanos - basicResult.OffsetNanos).Should().BeLessThan(50);
    }

    [Fact]
    public void Calibrate_WeightedStrategy_DetectsDriftTrends()
    {
        // Arrange - Progressive drift increase
        var cpuTimes = Enumerable.Range(0, 100).Select(i => (long)i * 10000).ToList();
        var gpuTimes = cpuTimes.Select((t, i) =>
        {
            double driftFactor = 1.0 + (i * 0.000001); // Increasing drift
            return (long)(driftFactor * t + 1000);
        }).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Weighted);

        // Assert
        result.DriftPPM.Should().BeGreaterThan(0); // Should detect positive drift
    }

    #endregion

    #region RANSAC Strategy Tests

    [Fact]
    public void Calibrate_RANSACStrategy_WithManyOutliers_FindsInliers()
    {
        // Arrange - 60% good data, 40% outliers
        var random = new Random(42);
        var cpuTimes = Enumerable.Range(0, 100).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select((t, i) =>
        {
            if (i % 5 == 0 || i % 7 == 0) // ~40% outliers
            {
                return t + random.Next(10000, 50000);
            }
            return t + 1000;
        }).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.RANSAC);

        // Assert
        result.SampleCount.Should().BeGreaterThanOrEqualTo(10); // Found inliers
        result.OffsetNanos.Should().BeInRange(800, 1200); // Accurate despite outliers
    }

    [Fact]
    public void Calibrate_RANSACStrategy_ComparesToRobust_BetterWithExtremeOutliers()
    {
        // Arrange - Extreme outliers
        var random = new Random(42);
        var cpuTimes = Enumerable.Range(0, 100).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select((t, i) =>
        {
            if (i < 20)
            {
                return t + random.Next(100000, 500000); // Extreme outliers
            }

            return t + 1000; // Good data
        }).ToList();

        // Act
        var ransacResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.RANSAC);
        var robustResult = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.Robust);

        // Assert - RANSAC should handle extreme outliers at least as well as Robust
        Math.Abs(ransacResult.OffsetNanos - 1000).Should().BeLessThanOrEqualTo(
            Math.Abs(robustResult.OffsetNanos - 1000));
    }

    [Fact]
    public void Calibrate_RANSACStrategy_WithInsufficientInliers_FallsBackToBasic()
    {
        // Arrange - Mostly outliers (less than 10 inliers possible)
        var random = new Random(42);
        var cpuTimes = Enumerable.Range(0, 20).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select((t, i) =>
        {
            if (i < 15)
            {
                return t + random.Next(100000, 500000); // Too many outliers
            }

            return t + 1000;
        }).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, CalibrationStrategy.RANSAC);

        // Assert - Should not crash, returns some result
        result.Should().NotBeNull();
        result.SampleCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Edge Cases and Validation Tests

    [Fact]
    public void Calibrate_WithNullCpuTimestamps_ThrowsArgumentNullException()
    {
        // Arrange
        List<long>? cpuTimes = null;
        var gpuTimes = new List<long> { 1, 2, 3 };

        // Act
        Action act = () => _calibrator.Calibrate(cpuTimes!, gpuTimes);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cpuTimestamps");
    }

    [Fact]
    public void Calibrate_WithNullGpuTimestamps_ThrowsArgumentNullException()
    {
        // Arrange
        var cpuTimes = new List<long> { 1, 2, 3 };
        List<long>? gpuTimes = null;

        // Act
        Action act = () => _calibrator.Calibrate(cpuTimes, gpuTimes!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gpuTimestamps");
    }

    [Fact]
    public void Calibrate_WithMismatchedCounts_ThrowsArgumentException()
    {
        // Arrange
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i).ToList();
        var gpuTimes = Enumerable.Range(0, 40).Select(i => (long)i).ToList();

        // Act
        Action act = () => _calibrator.Calibrate(cpuTimes, gpuTimes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must match*");
    }

    [Fact]
    public void Calibrate_WithInsufficientSamples_ThrowsArgumentException()
    {
        // Arrange - Only 5 samples (need at least 10)
        var cpuTimes = Enumerable.Range(0, 5).Select(i => (long)i).ToList();
        var gpuTimes = Enumerable.Range(0, 5).Select(i => (long)i).ToList();

        // Act
        Action act = () => _calibrator.Calibrate(cpuTimes, gpuTimes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*10 samples*");
    }

    [Theory]
    [InlineData(CalibrationStrategy.Basic)]
    [InlineData(CalibrationStrategy.Robust)]
    [InlineData(CalibrationStrategy.Weighted)]
    [InlineData(CalibrationStrategy.RANSAC)]
    public void Calibrate_AllStrategies_ReturnValidResults(CalibrationStrategy strategy)
    {
        // Arrange
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i * 1000).ToList();
        var gpuTimes = cpuTimes.Select(t => t + 1000).ToList();

        // Act
        var result = _calibrator.Calibrate(cpuTimes, gpuTimes, strategy);

        // Assert
        result.Should().NotBeNull();
        result.SampleCount.Should().BeGreaterThan(0);
        result.ErrorBoundNanos.Should().BeGreaterThanOrEqualTo(0); // Perfect data may have 0 error
        result.CalibrationTimestampNanos.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calibrate_WithUnknownStrategy_ThrowsArgumentException()
    {
        // Arrange
        var cpuTimes = Enumerable.Range(0, 50).Select(i => (long)i).ToList();
        var gpuTimes = Enumerable.Range(0, 50).Select(i => (long)i).ToList();
        var invalidStrategy = (CalibrationStrategy)999;

        // Act
        Action act = () => _calibrator.Calibrate(cpuTimes, gpuTimes, invalidStrategy);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown calibration strategy*");
    }

    #endregion
}
