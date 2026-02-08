// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.OpenCL.Timing;

/// <summary>
/// Calibration strategies for CPU-GPU clock synchronization.
/// </summary>
public enum CalibrationStrategy
{
    /// <summary>
    /// Basic linear regression with all samples.
    /// Fast but sensitive to outliers.
    /// </summary>
    Basic,

    /// <summary>
    /// Robust linear regression with outlier rejection.
    /// Removes samples beyond 2σ and recomputes.
    /// </summary>
    Robust,

    /// <summary>
    /// Weighted least squares giving more weight to recent samples.
    /// Best for capturing clock drift trends.
    /// </summary>
    Weighted
}

/// <summary>
/// Provides CPU-GPU clock calibration for OpenCL devices.
/// Implements linear regression to compute offset and drift between time domains.
/// </summary>
public sealed partial class OpenCLClockCalibrator
{
    private readonly ILogger _logger;

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Debug,
        Message = "Calibration complete using {Strategy} strategy: samples={Samples}, drift={DriftPPM:F2}PPM, error=±{ErrorNs}ns")]
    private static partial void LogCalibrationComplete(
        ILogger logger, string strategy, int samples, double driftPPM, long errorNs);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Debug,
        Message = "Removed {Count} outliers in robust calibration iteration")]
    private static partial void LogOutliersRemoved(ILogger logger, int count);

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLClockCalibrator"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public OpenCLClockCalibrator(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Performs clock calibration using the specified strategy.
    /// </summary>
    /// <param name="cpuTimestamps">CPU timestamps in nanoseconds.</param>
    /// <param name="gpuTimestamps">Corresponding GPU timestamps in nanoseconds.</param>
    /// <param name="strategy">Calibration strategy to use.</param>
    /// <returns>Clock calibration result with offset, drift, and error bounds.</returns>
    public ClockCalibration Calibrate(
        IReadOnlyList<long> cpuTimestamps,
        IReadOnlyList<long> gpuTimestamps,
        CalibrationStrategy strategy = CalibrationStrategy.Robust)
    {
        ArgumentNullException.ThrowIfNull(cpuTimestamps);
        ArgumentNullException.ThrowIfNull(gpuTimestamps);

        if (cpuTimestamps.Count != gpuTimestamps.Count)
        {
            throw new ArgumentException("CPU and GPU timestamp counts must match");
        }

        if (cpuTimestamps.Count < 10)
        {
            throw new ArgumentException("At least 10 samples required for calibration");
        }

        return strategy switch
        {
            CalibrationStrategy.Basic => CalibrateBasic(cpuTimestamps, gpuTimestamps),
            CalibrationStrategy.Robust => CalibrateRobust(cpuTimestamps, gpuTimestamps),
            CalibrationStrategy.Weighted => CalibrateWeighted(cpuTimestamps, gpuTimestamps),
            _ => throw new ArgumentException($"Unknown calibration strategy: {strategy}")
        };
    }

    #region Basic Linear Regression

    /// <summary>
    /// Performs basic ordinary least squares linear regression.
    /// </summary>
    private ClockCalibration CalibrateBasic(IReadOnlyList<long> cpuTimes, IReadOnlyList<long> gpuTimes)
    {
        var (slope, intercept, stdError) = ComputeLinearRegression(cpuTimes, gpuTimes);

        var driftPPM = (slope - 1.0) * 1_000_000;
        var calibrationTime = Stopwatch.GetTimestamp();
        var calibrationNanos = (long)((calibrationTime / (double)Stopwatch.Frequency) * 1_000_000_000);

        LogCalibrationComplete(_logger, "Basic", cpuTimes.Count, driftPPM, (long)(stdError * 2));

        return new ClockCalibration
        {
            OffsetNanos = (long)intercept,
            DriftPPM = driftPPM,
            ErrorBoundNanos = (long)(stdError * 2), // ±2σ confidence interval
            SampleCount = cpuTimes.Count,
            CalibrationTimestampNanos = calibrationNanos
        };
    }

    #endregion

    #region Robust Linear Regression (Outlier Rejection)

    /// <summary>
    /// Performs robust linear regression with outlier rejection.
    /// Iteratively removes samples beyond 2σ until convergence.
    /// </summary>
    private ClockCalibration CalibrateRobust(IReadOnlyList<long> cpuTimes, IReadOnlyList<long> gpuTimes)
    {
        var cpuList = cpuTimes.ToList();
        var gpuList = gpuTimes.ToList();

        // Iterative outlier rejection
        int removedCount;
        var maxIterations = 5;
        var iteration = 0;

        do
        {
            removedCount = 0;
            var (slope, intercept, stdError) = ComputeLinearRegression(cpuList, gpuList);

            // Compute residuals
            var residuals = new List<double>(cpuList.Count);
            for (var i = 0; i < cpuList.Count; i++)
            {
                var predicted = slope * cpuList[i] + intercept;
                var residual = Math.Abs(gpuList[i] - predicted);
                residuals.Add(residual);
            }

            // Compute mean and std of residuals
            var meanResidual = residuals.Average();
            var stdResidual = Math.Sqrt(residuals.Select(r => Math.Pow(r - meanResidual, 2)).Average());
            var threshold = 2.0 * stdResidual;

            // Remove outliers
            for (var i = cpuList.Count - 1; i >= 0; i--)
            {
                if (residuals[i] > threshold)
                {
                    cpuList.RemoveAt(i);
                    gpuList.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                LogOutliersRemoved(_logger, removedCount);
            }

            iteration++;
        }
        while (removedCount > 0 && cpuList.Count >= 10 && iteration < maxIterations);

        // Final regression with cleaned data
        var (finalSlope, finalIntercept, finalStdError) = ComputeLinearRegression(cpuList, gpuList);

        var driftPPM = (finalSlope - 1.0) * 1_000_000;
        var calibrationTime = Stopwatch.GetTimestamp();
        var calibrationNanos = (long)((calibrationTime / (double)Stopwatch.Frequency) * 1_000_000_000);

        LogCalibrationComplete(_logger, "Robust", cpuList.Count, driftPPM, (long)(finalStdError * 2));

        return new ClockCalibration
        {
            OffsetNanos = (long)finalIntercept,
            DriftPPM = driftPPM,
            ErrorBoundNanos = (long)(finalStdError * 2),
            SampleCount = cpuList.Count,
            CalibrationTimestampNanos = calibrationNanos
        };
    }

    #endregion

    #region Weighted Linear Regression

    /// <summary>
    /// Performs weighted least squares giving more weight to recent samples.
    /// </summary>
    private ClockCalibration CalibrateWeighted(IReadOnlyList<long> cpuTimes, IReadOnlyList<long> gpuTimes)
    {
        var n = cpuTimes.Count;

        // Assign exponentially increasing weights to more recent samples
        var weights = new double[n];
        var decayFactor = 0.95;
        var totalWeight = 0.0;

        for (var i = 0; i < n; i++)
        {
            weights[i] = Math.Pow(decayFactor, n - 1 - i);
            totalWeight += weights[i];
        }

        // Normalize weights
        for (var i = 0; i < n; i++)
        {
            weights[i] /= totalWeight;
        }

        // Weighted mean
        double sumWx = 0, sumWy = 0;
        for (var i = 0; i < n; i++)
        {
            sumWx += weights[i] * cpuTimes[i];
            sumWy += weights[i] * gpuTimes[i];
        }

        // Weighted covariance and variance
        double sumWxy = 0, sumWxx = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = cpuTimes[i] - sumWx;
            var dy = gpuTimes[i] - sumWy;
            sumWxy += weights[i] * dx * dy;
            sumWxx += weights[i] * dx * dx;
        }

        var slope = sumWxx > 0 ? sumWxy / sumWxx : 1.0;
        var intercept = sumWy - slope * sumWx;

        // Compute weighted residual standard error
        double sumWeightedResiduals = 0;
        for (var i = 0; i < n; i++)
        {
            var predicted = slope * cpuTimes[i] + intercept;
            var residual = gpuTimes[i] - predicted;
            sumWeightedResiduals += weights[i] * residual * residual;
        }

        var stdError = Math.Sqrt(sumWeightedResiduals);

        var driftPPM = (slope - 1.0) * 1_000_000;
        var calibrationTime = Stopwatch.GetTimestamp();
        var calibrationNanos = (long)((calibrationTime / (double)Stopwatch.Frequency) * 1_000_000_000);

        LogCalibrationComplete(_logger, "Weighted", n, driftPPM, (long)(stdError * 2));

        return new ClockCalibration
        {
            OffsetNanos = (long)intercept,
            DriftPPM = driftPPM,
            ErrorBoundNanos = (long)(stdError * 2),
            SampleCount = n,
            CalibrationTimestampNanos = calibrationNanos
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Computes ordinary least squares linear regression.
    /// </summary>
    /// <returns>Tuple of (slope, intercept, standard error of estimate).</returns>
    private static (double slope, double intercept, double stdError) ComputeLinearRegression(
        IReadOnlyList<long> x,
        IReadOnlyList<long> y)
    {
        var n = x.Count;
        if (n < 2)
        {
            return (1.0, 0.0, 0.0);
        }

        // Compute means
        double sumX = 0, sumY = 0;
        for (var i = 0; i < n; i++)
        {
            sumX += x[i];
            sumY += y[i];
        }
        var meanX = sumX / n;
        var meanY = sumY / n;

        // Compute covariance and variance
        double covXY = 0, varX = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            covXY += dx * dy;
            varX += dx * dx;
        }

        // Handle edge case where variance is zero
        if (varX == 0)
        {
            return (1.0, meanY - meanX, 0.0);
        }

        var slope = covXY / varX;
        var intercept = meanY - slope * meanX;

        // Compute standard error of estimate
        double sumSquaredResiduals = 0;
        for (var i = 0; i < n; i++)
        {
            var predicted = slope * x[i] + intercept;
            var residual = y[i] - predicted;
            sumSquaredResiduals += residual * residual;
        }

        var stdError = Math.Sqrt(sumSquaredResiduals / (n - 2));

        return (slope, intercept, stdError);
    }

    #endregion
}
