// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Timing;

/// <summary>
/// Represents calibration data for converting between CPU and GPU time domains.
/// </summary>
/// <remarks>
/// <para>
/// Clock calibration is performed by collecting paired CPU-GPU timestamps and fitting
/// a linear model: GPU_time = offset + drift * CPU_time. This compensates for:
/// <list type="bullet">
/// <item><description><strong>Clock Offset</strong>: Different initialization times</description></item>
/// <item><description><strong>Clock Drift</strong>: Frequency differences (typically 50-200 PPM)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Typical Drift Rates:</strong>
/// <list type="bullet">
/// <item><description>50 PPM: 180μs drift per hour</description></item>
/// <item><description>100 PPM: 360μs drift per hour</description></item>
/// <item><description>200 PPM: 720μs drift per hour</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Recommended Usage:</strong>
/// <code>
/// // Initial calibration
/// var calibration = await timingProvider.CalibrateAsync(100);
///
/// // Convert GPU time to CPU time domain
/// long gpuTime = await timingProvider.GetGpuTimestampAsync();
/// long cpuTime = calibration.GpuToCpuTime(gpuTime);
///
/// // Get uncertainty bounds
/// var (min, max) = calibration.GetUncertaintyRange(gpuTime);
///
/// // Recalibrate periodically (every 5-10 minutes)
/// if (DateTime.UtcNow - lastCalibration > TimeSpan.FromMinutes(5))
/// {
///     calibration = await timingProvider.CalibrateAsync(100);
/// }
/// </code>
/// </para>
/// </remarks>
public readonly struct ClockCalibration : IEquatable<ClockCalibration>
{
    /// <summary>
    /// Gets the offset between GPU and CPU clocks in nanoseconds (GPU_time - CPU_time).
    /// </summary>
    /// <value>
    /// The clock offset in nanoseconds. Positive values indicate the GPU clock is ahead
    /// of the CPU clock. This offset accounts for different initialization times.
    /// </value>
    /// <remarks>
    /// This offset is the intercept of the linear regression: GPU_time = offset + drift * CPU_time.
    /// </remarks>
    public long OffsetNanos { get; init; }

    /// <summary>
    /// Gets the clock drift rate in parts per million (PPM).
    /// </summary>
    /// <value>
    /// The drift rate in PPM. Positive values indicate the GPU clock runs faster than
    /// the CPU clock. Typical range: 50-200 PPM.
    /// </value>
    /// <remarks>
    /// <para>
    /// PPM (parts per million) represents the frequency difference between clocks:
    /// <list type="bullet">
    /// <item><description>50 PPM = 0.005% frequency difference = 50μs drift per second</description></item>
    /// <item><description>100 PPM = 0.01% frequency difference = 100μs drift per second</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Over time, drift accumulates. For 100 PPM drift:
    /// <list type="bullet">
    /// <item><description>1 minute: 6μs accumulated drift</description></item>
    /// <item><description>1 hour: 360μs accumulated drift</description></item>
    /// <item><description>1 day: 8.64ms accumulated drift</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public double DriftPPM { get; init; }

    /// <summary>
    /// Gets the error bound (±uncertainty) of the calibration in nanoseconds.
    /// </summary>
    /// <value>
    /// The error bound in nanoseconds, representing ±1 standard deviation from the
    /// linear regression fit. Smaller values indicate higher calibration accuracy.
    /// </value>
    /// <remarks>
    /// <para>
    /// The error bound is computed from regression residuals and represents the typical
    /// uncertainty in time conversions. For a 68% confidence interval, the actual time
    /// is within [estimated - error, estimated + error].
    /// </para>
    /// <para>
    /// <strong>Factors Affecting Error:</strong>
    /// <list type="bullet">
    /// <item><description>Sample count: More samples → lower error</description></item>
    /// <item><description>System load: Lower load → lower error</description></item>
    /// <item><description>Clock stability: Stable clocks → lower error</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public long ErrorBoundNanos { get; init; }

    /// <summary>
    /// Gets the number of timestamp pairs used for calibration.
    /// </summary>
    /// <value>
    /// The sample count. Higher counts improve accuracy but increase calibration time.
    /// Typical values: 50-200 samples.
    /// </value>
    public int SampleCount { get; init; }

    /// <summary>
    /// Gets the timestamp when this calibration was performed (CPU time in nanoseconds).
    /// </summary>
    /// <value>
    /// The calibration timestamp in nanoseconds since an implementation-defined epoch.
    /// Used to determine when recalibration is needed.
    /// </value>
    public long CalibrationTimestampNanos { get; init; }

    /// <summary>
    /// Converts a GPU timestamp to the CPU time domain.
    /// </summary>
    /// <param name="gpuTimeNanos">The GPU timestamp in nanoseconds.</param>
    /// <returns>
    /// The corresponding CPU time in nanoseconds, accounting for offset and drift.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Conversion formula: CPU_time = (GPU_time - offset) / (1 + drift_ppm / 1_000_000)
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// long gpuTime = 1_000_000_000; // 1 second in GPU time
    /// long cpuTime = calibration.GpuToCpuTime(gpuTime);
    /// // cpuTime ≈ 999_900_000 (assuming 100 PPM drift and 0 offset)
    /// </code>
    /// </para>
    /// </remarks>
    public long GpuToCpuTime(long gpuTimeNanos)
    {
        // Adjust for offset
        var adjusted = gpuTimeNanos - OffsetNanos;

        // Adjust for drift: CPU_time = GPU_time / (1 + drift_fraction)
        var driftFactor = 1.0 + (DriftPPM / 1_000_000.0);

        return (long)(adjusted / driftFactor);
    }

    /// <summary>
    /// Converts a CPU timestamp to the GPU time domain.
    /// </summary>
    /// <param name="cpuTimeNanos">The CPU timestamp in nanoseconds.</param>
    /// <returns>
    /// The corresponding GPU time in nanoseconds, accounting for offset and drift.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Conversion formula: GPU_time = CPU_time * (1 + drift_ppm / 1_000_000) + offset
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// long cpuTime = 1_000_000_000; // 1 second in CPU time
    /// long gpuTime = calibration.CpuToGpuTime(cpuTime);
    /// // gpuTime ≈ 1_000_100_000 (assuming 100 PPM drift and 0 offset)
    /// </code>
    /// </para>
    /// </remarks>
    public long CpuToGpuTime(long cpuTimeNanos)
    {
        // Adjust for drift: GPU_time = CPU_time * (1 + drift_fraction)
        var driftFactor = 1.0 + (DriftPPM / 1_000_000.0);
        var adjusted = (long)(cpuTimeNanos * driftFactor);

        // Adjust for offset
        return adjusted + OffsetNanos;
    }

    /// <summary>
    /// Gets the uncertainty range for a GPU timestamp conversion.
    /// </summary>
    /// <param name="gpuTimeNanos">The GPU timestamp in nanoseconds.</param>
    /// <returns>
    /// A tuple containing the minimum and maximum CPU time bounds (inclusive),
    /// representing a ±1 standard deviation confidence interval.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The uncertainty range accounts for calibration error and provides a confidence
    /// interval for time conversions. For normally distributed errors:
    /// <list type="bullet">
    /// <item><description>68% of measurements fall within [min, max]</description></item>
    /// <item><description>95% of measurements fall within [min - error, max + error]</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// long gpuTime = 1_000_000_000;
    /// var (min, max) = calibration.GetUncertaintyRange(gpuTime);
    /// // With error bound of 1000ns:
    /// // min ≈ 999_999_000, max ≈ 1_000_001_000
    /// // True time is likely within this range (68% confidence)
    /// </code>
    /// </para>
    /// </remarks>
    public (long min, long max) GetUncertaintyRange(long gpuTimeNanos)
    {
        var cpuTime = GpuToCpuTime(gpuTimeNanos);
        return (cpuTime - ErrorBoundNanos, cpuTime + ErrorBoundNanos);
    }

    /// <summary>
    /// Determines whether this calibration should be refreshed.
    /// </summary>
    /// <param name="currentTimeNanos">
    /// The current CPU time in nanoseconds (same epoch as <see cref="CalibrationTimestampNanos"/>).
    /// </param>
    /// <param name="maxAgeMinutes">
    /// Maximum calibration age in minutes before refresh is recommended (default: 5 minutes).
    /// </param>
    /// <returns>
    /// True if the calibration age exceeds <paramref name="maxAgeMinutes"/>, false otherwise.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Clock drift accumulates over time, so periodic recalibration is recommended.
    /// For typical drift rates (100 PPM):
    /// <list type="bullet">
    /// <item><description>5 minutes: 30μs accumulated drift</description></item>
    /// <item><description>10 minutes: 60μs accumulated drift</description></item>
    /// <item><description>30 minutes: 180μs accumulated drift</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Usage:</strong>
    /// <code>
    /// long currentTime = GetCurrentCpuTimeNanos();
    /// if (calibration.ShouldRecalibrate(currentTime, maxAgeMinutes: 5))
    /// {
    ///     calibration = await timingProvider.CalibrateAsync(100);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public bool ShouldRecalibrate(long currentTimeNanos, double maxAgeMinutes = 5.0)
    {
        var elapsedNanos = currentTimeNanos - CalibrationTimestampNanos;
        var elapsedMinutes = elapsedNanos / (60.0 * 1_000_000_000.0);
        return elapsedMinutes >= maxAgeMinutes;
    }

    /// <summary>
    /// Determines whether the specified ClockCalibration is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>True if the specified object is equal to the current instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is ClockCalibration other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified ClockCalibration is equal to the current instance.
    /// </summary>
    /// <param name="other">The ClockCalibration to compare with the current instance.</param>
    /// <returns>True if the specified ClockCalibration is equal to the current instance; otherwise, false.</returns>
    public bool Equals(ClockCalibration other)
    {
        return OffsetNanos == other.OffsetNanos
            && DriftPPM == other.DriftPPM
            && ErrorBoundNanos == other.ErrorBoundNanos
            && SampleCount == other.SampleCount
            && CalibrationTimestampNanos == other.CalibrationTimestampNanos;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(OffsetNanos, DriftPPM, ErrorBoundNanos, SampleCount, CalibrationTimestampNanos);
    }

    /// <summary>
    /// Determines whether two ClockCalibration instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>True if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(ClockCalibration left, ClockCalibration right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two ClockCalibration instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>True if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(ClockCalibration left, ClockCalibration right)
    {
        return !(left == right);
    }
}
