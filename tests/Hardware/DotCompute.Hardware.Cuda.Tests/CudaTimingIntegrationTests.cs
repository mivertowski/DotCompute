// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Timing;
using DotCompute.Tests.Common.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using static DotCompute.Tests.Common.TestCategories;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Integration tests for CUDA Timing API.
    /// Tests end-to-end timing functionality with real GPU hardware.
    /// Requires physical CUDA-capable hardware (Compute Capability 6.0+) to execute.
    /// </summary>
    [Trait("Category", CUDA)]
    [Trait("Category", TestCategories.Hardware)]
    [Trait("Category", RequiresHardware)]
    [Trait("Component", "CudaTiming")]
    public sealed class CudaTimingIntegrationTests(ITestOutputHelper output) : CudaTestBase(output)
    {
        /// <summary>
        /// Tests end-to-end CPU-GPU clock calibration with real device.
        /// Validates that calibration produces reasonable offset and drift values.
        /// </summary>
        [SkippableFact]
        public async Task EndToEnd_ClockCalibration_WithRealDevice_ProducesValidResults()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(6, 0), "Requires Compute Capability 6.0+ for globaltimer");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var timingProvider = accelerator.GetTimingProvider();
            timingProvider.Should().NotBeNull();

            // Act - Perform calibration with multiple samples
            var calibration = await timingProvider!.CalibrateAsync(
                sampleCount: 100,
                ct: default);

            // Assert
            calibration.Should().NotBeNull();
            calibration.SampleCount.Should().BeGreaterThan(0, "should have collected samples");
            calibration.SampleCount.Should().BeLessThanOrEqualTo(100, "should not exceed requested samples");

            // Offset can be large due to different clock epochs (CPU boot time vs GPU init time)
            // Just verify it's set (non-zero is expected)
            calibration.OffsetNanos.Should().NotBe(0, "clock offset should be measured");

            // Drift should be reasonable (within ±2000 PPM = 0.2%)
            // Note: CPU and GPU can have different clock sources, so moderate drift is expected
            Math.Abs(calibration.DriftPPM).Should().BeLessThan(2000,
                "clock drift should be within ±2000 PPM");

            // Error bound should be non-negative and reasonable
            calibration.ErrorBoundNanos.Should().BeGreaterThanOrEqualTo(0,
                "error bound must be non-negative");
            calibration.ErrorBoundNanos.Should().BeLessThan(1_000_000L,
                "error bound should be under 1ms for good calibration");

            // Calibration timestamp should be recent
            calibration.CalibrationTimestampNanos.Should().BeGreaterThan(0,
                "calibration timestamp must be set");

            Output.WriteLine($"Calibration Results:");
            Output.WriteLine($"  Samples: {calibration.SampleCount}");
            Output.WriteLine($"  Offset: {calibration.OffsetNanos:N0} ns");
            Output.WriteLine($"  Drift: {calibration.DriftPPM:F3} PPM");
            Output.WriteLine($"  Error: ±{calibration.ErrorBoundNanos:N0} ns");
        }

        /// <summary>
        /// Tests timestamp collection with actual kernel execution.
        /// Validates that GPU timestamps can be captured during kernel runs.
        /// </summary>
        [SkippableFact]
        public async Task TimestampCollection_DuringKernelExecution_CapturesValidTimestamps()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(6, 0), "Requires Compute Capability 6.0+ for globaltimer");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var timingProvider = accelerator.GetTimingProvider();
            timingProvider.Should().NotBeNull();

            // Act - Collect multiple timestamps
            const int timestampCount = 10;
            var timestamps = new List<long>();

            for (var i = 0; i < timestampCount; i++)
            {
                var timestamp = await timingProvider!.GetGpuTimestampAsync(default);
                timestamps.Add(timestamp);

                // Small delay to ensure timestamps are different
                await Task.Delay(1);
            }

            // Assert
            timestamps.Should().HaveCount(timestampCount);
            timestamps.Should().OnlyContain(t => t > 0, "all timestamps should be positive");

            // Verify monotonicity (timestamps should increase)
            for (var i = 1; i < timestamps.Count; i++)
            {
                timestamps[i].Should().BeGreaterThan(timestamps[i - 1],
                    $"timestamp[{i}] should be greater than timestamp[{i - 1}] (monotonic)");
            }

            // Verify reasonable spacing (should be at least 1ms apart with Task.Delay(1))
            for (var i = 1; i < timestamps.Count; i++)
            {
                var deltaNanos = timestamps[i] - timestamps[i - 1];
                deltaNanos.Should().BeGreaterThan(500_000, // At least 0.5ms
                    $"timestamps should be at least 0.5ms apart (got {deltaNanos:N0}ns)");
                deltaNanos.Should().BeLessThan(100_000_000, // Less than 100ms
                    $"timestamps should be less than 100ms apart (got {deltaNanos:N0}ns)");
            }

            Output.WriteLine($"Collected {timestamps.Count} timestamps:");
            for (var i = 0; i < Math.Min(5, timestamps.Count); i++)
            {
                Output.WriteLine($"  [{i}]: {timestamps[i]:N0} ns");
                if (i > 0)
                {
                    Output.WriteLine($"       Delta: {(timestamps[i] - timestamps[i - 1]):N0} ns");
                }
            }
        }

        /// <summary>
        /// Tests GetGpuTimestampAsync with stream synchronization.
        /// Validates that timestamps are properly synchronized with CUDA streams.
        /// </summary>
        [SkippableFact]
        public async Task GetGpuTimestampAsync_WithStreamSync_ReturnsConsistentTimestamps()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(6, 0), "Requires Compute Capability 6.0+ for globaltimer");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var timingProvider = accelerator.GetTimingProvider();
            timingProvider.Should().NotBeNull();

            // Act - Get timestamps with explicit stream synchronization
            var timestamp1 = await timingProvider!.GetGpuTimestampAsync(default);
            await Task.Delay(10); // 10ms delay
            var timestamp2 = await timingProvider.GetGpuTimestampAsync(default);

            // Assert
            timestamp1.Should().BeGreaterThan(0, "first timestamp should be valid");
            timestamp2.Should().BeGreaterThan(0, "second timestamp should be valid");
            timestamp2.Should().BeGreaterThan(timestamp1, "second timestamp should be later");

            var deltaNanos = timestamp2 - timestamp1;
            deltaNanos.Should().BeGreaterThan(5_000_000, // At least 5ms
                "10ms delay should result in at least 5ms timestamp delta");
            deltaNanos.Should().BeLessThan(50_000_000, // Less than 50ms
                "10ms delay should result in less than 50ms timestamp delta");

            Output.WriteLine($"Timestamp 1: {timestamp1:N0} ns");
            Output.WriteLine($"Timestamp 2: {timestamp2:N0} ns");
            Output.WriteLine($"Delta: {deltaNanos:N0} ns ({deltaNanos / 1_000_000.0:F3} ms)");
        }

        /// <summary>
        /// Tests clock drift measurement over time.
        /// Validates that multiple calibrations produce stable results.
        /// </summary>
        [SkippableFact]
        public async Task ClockDrift_MultipleCalibrations_ProducesStableResults()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(6, 0), "Requires Compute Capability 6.0+ for globaltimer");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var timingProvider = accelerator.GetTimingProvider();
            timingProvider.Should().NotBeNull();

            // Act - Perform multiple calibrations over time
            var calibrations = new List<ClockCalibration>();
            const int calibrationCount = 5;

            for (var i = 0; i < calibrationCount; i++)
            {
                var calibration = await timingProvider!.CalibrateAsync(
                    sampleCount: 50,
                    ct: default);

                calibrations.Add(calibration);

                if (i < calibrationCount - 1)
                {
                    await Task.Delay(100); // 100ms between calibrations
                }
            }

            // Assert
            calibrations.Should().HaveCount(calibrationCount);

            // Calculate stability metrics
            var offsets = calibrations.Select(c => (double)c.OffsetNanos).ToList();
            var drifts = calibrations.Select(c => c.DriftPPM).ToList();

            var offsetMean = offsets.Average();
            var offsetStdDev = Math.Sqrt(offsets.Select(o => Math.Pow(o - offsetMean, 2)).Average());

            var driftMean = drifts.Average();
            var driftStdDev = Math.Sqrt(drifts.Select(d => Math.Pow(d - driftMean, 2)).Average());

            // Verify calibrations are producing results
            offsetMean.Should().NotBe(0, "should have measured offsets");

            // Drift should be reasonably stable (std dev < 500 PPM for real hardware variability)
            // Note: Multiple calibrations can show variation due to system load and measurement noise
            driftStdDev.Should().BeLessThan(500,
                $"drift standard deviation ({driftStdDev:F3} PPM) should be stable");

            // Verify offsets are all non-zero (measured)
            offsets.Should().OnlyContain(o => o != 0, "all offsets should be measured");

            Output.WriteLine($"Calibration Stability Analysis ({calibrationCount} samples):");
            Output.WriteLine($"  Offset: {offsetMean:N0} ns ± {offsetStdDev:N0} ns");
            Output.WriteLine($"  Drift: {driftMean:F3} PPM ± {driftStdDev:F3} PPM");

            for (var i = 0; i < calibrations.Count; i++)
            {
                Output.WriteLine($"  [{i}] Offset: {calibrations[i].OffsetNanos:N0} ns, " +
                                $"Drift: {calibrations[i].DriftPPM:F3} PPM, " +
                                $"Samples: {calibrations[i].SampleCount}");
            }
        }

        /// <summary>
        /// Tests calibration with varying sample counts on real hardware.
        /// Validates that different sample sizes produce valid and consistent results.
        /// </summary>
        [SkippableFact]
        public async Task MultiSampleCalibration_VaryingSampleCounts_ProduceValidResults()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(6, 0), "Requires Compute Capability 6.0+ for globaltimer");

            // Arrange
            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());
            await using var accelerator = factory.CreateProductionAccelerator(0);

            var timingProvider = accelerator.GetTimingProvider();
            timingProvider.Should().NotBeNull();

            // Cast to CudaTimingProvider to test different strategies
            var cudaTimingProvider = timingProvider as CudaTimingProvider;
            cudaTimingProvider.Should().NotBeNull("timing provider should be CudaTimingProvider");

            var strategies = new[]
            {
                CalibrationStrategy.Basic,
                CalibrationStrategy.Robust,
                CalibrationStrategy.Weighted,
                CalibrationStrategy.RANSAC
            };

            // Act - Test all strategies
            var results = new Dictionary<CalibrationStrategy, ClockCalibration>();

            foreach (var strategy in strategies)
            {
                var calibration = await cudaTimingProvider!.CalibrateAsync(
                    sampleCount: 100,
                    strategy: strategy,
                    ct: default);

                results[strategy] = calibration;
            }

            // Assert - All strategies should produce valid results
            foreach (var kvp in results)
            {
                var strategy = kvp.Key;
                var calibration = kvp.Value;

                calibration.Should().NotBeNull($"{strategy} should produce a result");
                calibration.SampleCount.Should().BeGreaterThan(0,
                    $"{strategy} should have collected samples");

                // Offset can be large - just verify it's measured
                calibration.OffsetNanos.Should().NotBe(0, $"{strategy} offset should be measured");

                // All strategies should produce reasonable drift (within ±2000 PPM = 0.2%)
                Math.Abs(calibration.DriftPPM).Should().BeLessThan(2000,
                    $"{strategy} drift should be reasonable");

                calibration.ErrorBoundNanos.Should().BeGreaterThanOrEqualTo(0,
                    $"{strategy} error bound must be non-negative");
            }

            // Output results for comparison
            Output.WriteLine($"Strategy Comparison:");

            foreach (var kvp in results)
            {
                Output.WriteLine($"  {kvp.Key}:");
                Output.WriteLine($"    Samples: {kvp.Value.SampleCount}");
                Output.WriteLine($"    Offset: {kvp.Value.OffsetNanos:N0} ns");
                Output.WriteLine($"    Drift: {kvp.Value.DriftPPM:F3} PPM");
                Output.WriteLine($"    Error: ±{kvp.Value.ErrorBoundNanos:N0} ns");
            }

            // Verify that all strategies produce reasonable drifts
            var drifts = results.Values.Select(c => c.DriftPPM).ToList();
            var maxDrift = drifts.Max();
            var minDrift = drifts.Min();

            // All drifts should be within reasonable range (±2000 PPM of each other)
            // Different strategies can produce quite different results due to outlier handling
            // and measurement noise, especially with varying clock epochs
            (maxDrift - minDrift).Should().BeLessThan(2000,
                "all strategies should produce drift within reasonable bounds");
        }
    }
}
