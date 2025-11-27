// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels.Profiling;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests.RingKernels.Profiling;

/// <summary>
/// Unit tests for LatencyHistogram class.
/// Tests HDR histogram functionality for latency percentile tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Profiling")]
public sealed class LatencyHistogramTests
{
    [Fact]
    public void Record_SingleValue_UpdatesCountAndStatistics()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Act
        histogram.Record(1000); // 1 microsecond

        // Assert
        histogram.TotalCount.Should().Be(1);
        histogram.MinNanos.Should().Be(1000);
        histogram.MaxNanos.Should().Be(1000);
    }

    [Fact]
    public void Record_MultipleValues_TracksMeanCorrectly()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Act
        histogram.Record(1000);
        histogram.Record(2000);
        histogram.Record(3000);

        // Assert
        histogram.TotalCount.Should().Be(3);
        histogram.MeanNanos.Should().Be(2000);
    }

    [Fact]
    public void Record_NegativeValue_IsIgnored()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Act
        histogram.Record(-100);

        // Assert
        histogram.TotalCount.Should().Be(0);
    }

    [Fact]
    public void GetPercentile_P50_ReturnsMedian()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Add values: 1us, 2us, 3us, 4us, 5us, 6us, 7us, 8us, 9us, 10us
        for (long i = 1; i <= 10; i++)
        {
            histogram.Record(i * 1000); // microseconds to nanoseconds
        }

        // Act
        var p50 = histogram.P50Nanos;

        // Assert - P50 should be around 5-6us (5000-6000 ns)
        p50.Should().BeGreaterThanOrEqualTo(4000);
        p50.Should().BeLessThanOrEqualTo(7000);
    }

    [Fact]
    public void GetPercentile_P99_ReturnsHighPercentile()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Add 100 values from 1us to 100us
        for (long i = 1; i <= 100; i++)
        {
            histogram.Record(i * 1000);
        }

        // Act
        var p99 = histogram.P99Nanos;

        // Assert - P99 should be around 99us
        p99.Should().BeGreaterThanOrEqualTo(95000);
        p99.Should().BeLessThanOrEqualTo(105000);
    }

    [Fact]
    public void GetPercentile_EmptyHistogram_ReturnsZero()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Act
        var p50 = histogram.P50Nanos;

        // Assert
        p50.Should().Be(0);
    }

    [Fact]
    public void GetPercentile_InvalidPercentile_ThrowsException()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        histogram.Record(1000);

        // Act & Assert
        var act1 = () => histogram.GetPercentile(-1);
        var act2 = () => histogram.GetPercentile(101);

        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetPercentiles_ReturnsAllPercentileStatistics()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        for (long i = 1; i <= 1000; i++)
        {
            histogram.Record(i * 1000);
        }

        // Act
        var percentiles = histogram.GetPercentiles();

        // Assert
        percentiles.TotalCount.Should().Be(1000);
        percentiles.MinNanos.Should().BeGreaterThan(0);
        percentiles.MaxNanos.Should().BeGreaterThan(0);
        percentiles.P50Nanos.Should().BeGreaterThan(0);
        percentiles.P90Nanos.Should().BeGreaterThanOrEqualTo(percentiles.P50Nanos);
        percentiles.P95Nanos.Should().BeGreaterThanOrEqualTo(percentiles.P90Nanos);
        percentiles.P99Nanos.Should().BeGreaterThanOrEqualTo(percentiles.P95Nanos);
        percentiles.P999Nanos.Should().BeGreaterThanOrEqualTo(percentiles.P99Nanos);
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        histogram.Record(1000);
        histogram.Record(2000);

        // Act
        histogram.Reset();

        // Assert
        histogram.TotalCount.Should().Be(0);
        histogram.MinNanos.Should().Be(0);
        histogram.MaxNanos.Should().Be(0);
        histogram.MeanNanos.Should().Be(0);
    }

    [Fact]
    public void Merge_CombinesHistograms()
    {
        // Arrange
        var histogram1 = new LatencyHistogram();
        var histogram2 = new LatencyHistogram();

        histogram1.Record(1000);
        histogram1.Record(2000);
        histogram2.Record(3000);
        histogram2.Record(4000);

        // Act
        histogram1.Merge(histogram2);

        // Assert
        histogram1.TotalCount.Should().Be(4);
        histogram1.MinNanos.Should().BeLessThanOrEqualTo(1000);
        histogram1.MaxNanos.Should().BeGreaterThanOrEqualTo(4000);
    }

    [Fact]
    public void RecordBatch_RecordsMultipleValues()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        var latencies = new long[] { 1000, 2000, 3000, 4000, 5000 };

        // Act
        histogram.RecordBatch(latencies);

        // Assert
        histogram.TotalCount.Should().Be(5);
        histogram.MeanNanos.Should().Be(3000);
    }

    [Fact]
    public void SubMicrosecondValues_AreRecordedCorrectly()
    {
        // Arrange
        var histogram = new LatencyHistogram();

        // Act - record 100ns, 500ns, 900ns
        histogram.Record(100);
        histogram.Record(500);
        histogram.Record(900);

        // Assert
        histogram.TotalCount.Should().Be(3);
        histogram.MinNanos.Should().Be(100);
        histogram.MaxNanos.Should().Be(900);
    }

    [Fact]
    public void MillisecondValues_AreRecordedCorrectly()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        const long oneMillisecond = 1_000_000; // 1ms in nanoseconds

        // Act
        histogram.Record(oneMillisecond);
        histogram.Record(10 * oneMillisecond);
        histogram.Record(100 * oneMillisecond);

        // Assert
        histogram.TotalCount.Should().Be(3);
        histogram.MinNanos.Should().Be(oneMillisecond);
        histogram.MaxNanos.Should().Be(100 * oneMillisecond);
    }

    [Fact]
    public void SecondValues_AreRecordedInOverflowBucket()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        const long oneSecond = 1_000_000_000;

        // Act - record values in second range
        histogram.Record(oneSecond);
        histogram.Record(5 * oneSecond);

        // Assert
        histogram.TotalCount.Should().Be(2);
        histogram.MaxNanos.Should().Be(5 * oneSecond);
    }

    [Fact]
    public void MicrosConversion_ReturnsCorrectValues()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        histogram.Record(1000); // 1us
        histogram.Record(99000); // 99us

        // Act
        var percentiles = histogram.GetPercentiles();

        // Assert
        percentiles.MinMicros.Should().BeApproximately(1.0, 0.5);
        percentiles.MaxMicros.Should().BeApproximately(99.0, 5.0);
    }

    [Theory]
    [InlineData(50, 0.5)]
    [InlineData(90, 0.9)]
    [InlineData(95, 0.95)]
    [InlineData(99, 0.99)]
    public void GetPercentile_VariousPercentiles_ReturnsAppropriateValues(double percentile, double expectedRatio)
    {
        // Arrange
        var histogram = new LatencyHistogram();
        const int count = 1000;

        // Add values 1-1000 microseconds
        for (long i = 1; i <= count; i++)
        {
            histogram.Record(i * 1000);
        }

        // Act
        var result = histogram.GetPercentile(percentile);

        // Assert
        var expectedNanos = (long)(expectedRatio * count * 1000);
        result.Should().BeInRange(expectedNanos - 50000, expectedNanos + 50000);
    }

    [Fact]
    public void ThreadSafety_ConcurrentRecording_MaintainsConsistency()
    {
        // Arrange
        var histogram = new LatencyHistogram();
        const int threadsCount = 4;
        const int recordsPerThread = 10000;

        // Act - Record from multiple threads
        var tasks = Enumerable.Range(0, threadsCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < recordsPerThread; i++)
                {
                    histogram.Record((i + 1) * 1000);
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // Assert - Total count should be exact
        histogram.TotalCount.Should().Be(threadsCount * recordsPerThread);
    }
}
