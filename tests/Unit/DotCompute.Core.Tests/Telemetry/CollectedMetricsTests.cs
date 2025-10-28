// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry;
using FluentAssertions;
using Xunit;

namespace DotCompute.Core.Tests.Telemetry;

/// <summary>
/// Unit tests for CollectedMetrics class.
/// </summary>
public sealed class CollectedMetricsTests
{
    [Fact]
    public void Constructor_InitializesCollections()
    {
        // Act
        var metrics = new CollectedMetrics();

        // Assert
        metrics.Counters.Should().NotBeNull();
        metrics.Gauges.Should().NotBeNull();
        metrics.Histograms.Should().NotBeNull();
    }

    [Fact]
    public void Counters_CanStoreAndRetrieveValues()
    {
        // Arrange
        var metrics = new CollectedMetrics();

        // Act
        metrics.Counters["test_counter"] = 42;

        // Assert
        metrics.Counters["test_counter"].Should().Be(42);
    }

    [Fact]
    public void Gauges_CanStoreAndRetrieveValues()
    {
        // Arrange
        var metrics = new CollectedMetrics();

        // Act
        metrics.Gauges["test_gauge"] = 3.14159;

        // Assert
        metrics.Gauges["test_gauge"].Should().BeApproximately(3.14159, 0.00001);
    }

    [Fact]
    public void Histograms_CanStoreAndRetrieveArrays()
    {
        // Arrange
        var metrics = new CollectedMetrics();
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        metrics.Histograms["test_histogram"] = values;

        // Assert
        metrics.Histograms["test_histogram"].Should().Equal(values);
    }

    [Fact]
    public void Counters_SupportsMultipleEntries()
    {
        // Arrange
        var metrics = new CollectedMetrics();

        // Act
        metrics.Counters["counter1"] = 10;
        metrics.Counters["counter2"] = 20;
        metrics.Counters["counter3"] = 30;

        // Assert
        metrics.Counters.Should().HaveCount(3);
        metrics.Counters.Values.Sum().Should().Be(60);
    }

    [Fact]
    public void Gauges_SupportsMultipleEntries()
    {
        // Arrange
        var metrics = new CollectedMetrics();

        // Act
        metrics.Gauges["gauge1"] = 1.5;
        metrics.Gauges["gauge2"] = 2.5;
        metrics.Gauges["gauge3"] = 3.5;

        // Assert
        metrics.Gauges.Should().HaveCount(3);
        metrics.Gauges.Values.Sum().Should().BeApproximately(7.5, 0.01);
    }

    [Fact]
    public void Histograms_SupportsMultipleEntries()
    {
        // Arrange
        var metrics = new CollectedMetrics();

        // Act
        metrics.Histograms["hist1"] = new[] { 1.0, 2.0 };
        metrics.Histograms["hist2"] = new[] { 3.0, 4.0 };

        // Assert
        metrics.Histograms.Should().HaveCount(2);
    }

    [Fact]
    public void Counters_CanBeUpdated()
    {
        // Arrange
        var metrics = new CollectedMetrics();
        metrics.Counters["test"] = 10;

        // Act
        metrics.Counters["test"] = 20;

        // Assert
        metrics.Counters["test"].Should().Be(20);
    }

    [Fact]
    public void Gauges_CanBeUpdated()
    {
        // Arrange
        var metrics = new CollectedMetrics();
        metrics.Gauges["test"] = 1.5;

        // Act
        metrics.Gauges["test"] = 2.5;

        // Assert
        metrics.Gauges["test"].Should().BeApproximately(2.5, 0.01);
    }
}
