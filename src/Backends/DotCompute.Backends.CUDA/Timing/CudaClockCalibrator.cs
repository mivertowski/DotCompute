// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Timing;

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
    Weighted,

    /// <summary>
    /// RANSAC-based robust fitting.
    /// Most robust to outliers but slower.
    /// </summary>
    RANSAC
}

/// <summary>
/// Provides CPU-GPU clock calibration with multiple strategies.
/// Implements linear regression to compute offset and drift between time domains.
/// </summary>
public sealed partial class CudaClockCalibrator
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaClockCalibrator"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public CudaClockCalibrator(ILogger? logger = null)
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
            CalibrationStrategy.RANSAC => CalibrateRANSAC(cpuTimestamps, gpuTimestamps),
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
        var originalCount = cpuList.Count;

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

            // Remove outliers (beyond 2σ)
            var threshold = stdError * 2;
            for (var i = cpuList.Count - 1; i >= 0; i--)
            {
                if (residuals[i] > threshold)
                {
                    cpuList.RemoveAt(i);
                    gpuList.RemoveAt(i);
                    removedCount++;
                }
            }

            iteration++;
        }
        while (removedCount > 0 && iteration < maxIterations && cpuList.Count >= 10);

        // Final regression on cleaned data
        var (finalSlope, finalIntercept, finalStdError) = ComputeLinearRegression(cpuList, gpuList);
        var driftPPM = (finalSlope - 1.0) * 1_000_000;

        var calibrationTime = Stopwatch.GetTimestamp();
        var calibrationNanos = (long)((calibrationTime / (double)Stopwatch.Frequency) * 1_000_000_000);

        var outliersRemoved = originalCount - cpuList.Count;
        LogRobustCalibration(_logger, cpuList.Count, outliersRemoved, driftPPM, (long)(finalStdError * 2));

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

    #region Weighted Least Squares

    /// <summary>
    /// Performs weighted least squares with exponential decay weights.
    /// More recent samples get higher weights to capture drift trends.
    /// </summary>
    private ClockCalibration CalibrateWeighted(IReadOnlyList<long> cpuTimes, IReadOnlyList<long> gpuTimes)
    {
        var n = cpuTimes.Count;
        var decayFactor = 0.95; // Exponential decay

        // Compute weights (newer samples get higher weight)
        var weights = new double[n];
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

        // Weighted means
        double cpuMean = 0, gpuMean = 0;
        for (var i = 0; i < n; i++)
        {
            cpuMean += weights[i] * cpuTimes[i];
            gpuMean += weights[i] * gpuTimes[i];
        }

        // Weighted covariance and variance
        double numerator = 0, denominator = 0;
        for (var i = 0; i < n; i++)
        {
            var cpuDelta = cpuTimes[i] - cpuMean;
            var gpuDelta = gpuTimes[i] - gpuMean;
            numerator += weights[i] * cpuDelta * gpuDelta;
            denominator += weights[i] * cpuDelta * cpuDelta;
        }

        var slope = numerator / denominator;
        var intercept = gpuMean - slope * cpuMean;

        // Weighted residuals for error estimation
        double sumSquaredResiduals = 0;
        for (var i = 0; i < n; i++)
        {
            var predicted = slope * cpuTimes[i] + intercept;
            var residual = gpuTimes[i] - predicted;
            sumSquaredResiduals += weights[i] * residual * residual;
        }

        var stdError = Math.Sqrt(sumSquaredResiduals / (n - 2));
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

    #region RANSAC Robust Fitting

    /// <summary>
    /// Performs RANSAC-based robust linear regression.
    /// Most robust to outliers but computationally expensive.
    /// </summary>
    private ClockCalibration CalibrateRANSAC(IReadOnlyList<long> cpuTimes, IReadOnlyList<long> gpuTimes)
    {
        var n = cpuTimes.Count;
        var iterations = Math.Min(100, n * 2);
        var inlierThreshold = 1000.0; // 1μs threshold for inliers
        var random = new Random(42); // Fixed seed for reproducibility

        double bestSlope = 0, bestIntercept = 0;
        var bestInlierCount = 0;
        var bestInliers = new List<int>();

        // RANSAC iterations
        for (var iter = 0; iter < iterations; iter++)
        {
            // Randomly select 2 points
            var idx1 = random.Next(n);
            var idx2 = random.Next(n);
            while (idx2 == idx1)
            {
                idx2 = random.Next(n);
            }

            // Fit line through these 2 points
            var x1 = cpuTimes[idx1];
            var y1 = gpuTimes[idx1];
            var x2 = cpuTimes[idx2];
            var y2 = gpuTimes[idx2];

            // Avoid division by zero
            if (x2 == x1)
            {
                continue;
            }

            var slope = (double)(y2 - y1) / (x2 - x1);
            var intercept = y1 - slope * x1;

            // Count inliers
            var inliers = new List<int>();
            for (var i = 0; i < n; i++)
            {
                var predicted = slope * cpuTimes[i] + intercept;
                var error = Math.Abs(gpuTimes[i] - predicted);

                if (error < inlierThreshold)
                {
                    inliers.Add(i);
                }
            }

            // Update best model if this has more inliers
            if (inliers.Count > bestInlierCount)
            {
                bestInlierCount = inliers.Count;
                bestSlope = slope;
                bestIntercept = intercept;
                bestInliers = inliers;
            }
        }

        // Refine with least squares on inliers
        if (bestInliers.Count >= 10)
        {
            var inlierCpuTimes = bestInliers.Select(i => cpuTimes[i]).ToList();
            var inlierGpuTimes = bestInliers.Select(i => gpuTimes[i]).ToList();
            var (slope, intercept, stdError) = ComputeLinearRegression(inlierCpuTimes, inlierGpuTimes);

            bestSlope = slope;
            bestIntercept = intercept;

            var driftPPM = (slope - 1.0) * 1_000_000;
            var calibrationTime = Stopwatch.GetTimestamp();
            var calibrationNanos = (long)((calibrationTime / (double)Stopwatch.Frequency) * 1_000_000_000);

            LogRANSACCalibration(_logger, bestInliers.Count, n - bestInliers.Count, driftPPM, (long)(stdError * 2));

            return new ClockCalibration
            {
                OffsetNanos = (long)bestIntercept,
                DriftPPM = driftPPM,
                ErrorBoundNanos = (long)(stdError * 2),
                SampleCount = bestInliers.Count,
                CalibrationTimestampNanos = calibrationNanos
            };
        }

        // Fallback to basic if RANSAC failed
        LogRANSACFallback(_logger);
        return CalibrateBasic(cpuTimes, gpuTimes);
    }

    #endregion

    #region Linear Regression Core

    /// <summary>
    /// Computes ordinary least squares linear regression.
    /// Returns (slope, intercept, standard_error).
    /// </summary>
    private static (double slope, double intercept, double standardError) ComputeLinearRegression(
        IReadOnlyList<long> xValues,
        IReadOnlyList<long> yValues)
    {
        var n = xValues.Count;

        // Compute means
        double xMean = 0, yMean = 0;
        for (var i = 0; i < n; i++)
        {
            xMean += xValues[i];
            yMean += yValues[i];
        }
        xMean /= n;
        yMean /= n;

        // Compute slope and intercept
        double numerator = 0, denominator = 0;
        for (var i = 0; i < n; i++)
        {
            var xDelta = xValues[i] - xMean;
            var yDelta = yValues[i] - yMean;
            numerator += xDelta * yDelta;
            denominator += xDelta * xDelta;
        }

        var slope = numerator / denominator;
        var intercept = yMean - slope * xMean;

        // Compute standard error of residuals
        double sumSquaredResiduals = 0;
        for (var i = 0; i < n; i++)
        {
            var predicted = slope * xValues[i] + intercept;
            var residual = yValues[i] - predicted;
            sumSquaredResiduals += residual * residual;
        }

        var standardError = Math.Sqrt(sumSquaredResiduals / (n - 2));

        return (slope, intercept, standardError);
    }

    #endregion

    #region Logging

    [LoggerMessage(
        EventId = 7200,
        Level = LogLevel.Information,
        Message = "Clock calibration complete ({Strategy}): {SampleCount} samples, drift={DriftPPM:F2} PPM, error=±{ErrorNanos}ns")]
    private static partial void LogCalibrationComplete(
        ILogger logger,
        string strategy,
        int sampleCount,
        double driftPPM,
        long errorNanos);

    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Information,
        Message = "Robust calibration: {InlierCount} inliers, {OutlierCount} outliers removed, drift={DriftPPM:F2} PPM, error=±{ErrorNanos}ns")]
    private static partial void LogRobustCalibration(
        ILogger logger,
        int inlierCount,
        int outlierCount,
        double driftPPM,
        long errorNanos);

    [LoggerMessage(
        EventId = 7202,
        Level = LogLevel.Information,
        Message = "RANSAC calibration: {InlierCount} inliers, {OutlierCount} outliers, drift={DriftPPM:F2} PPM, error=±{ErrorNanos}ns")]
    private static partial void LogRANSACCalibration(
        ILogger logger,
        int inlierCount,
        int outlierCount,
        double driftPPM,
        long errorNanos);

    [LoggerMessage(
        EventId = 7203,
        Level = LogLevel.Warning,
        Message = "RANSAC failed to find sufficient inliers, falling back to basic calibration")]
    private static partial void LogRANSACFallback(ILogger logger);

    #endregion
}
