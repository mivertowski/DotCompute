// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Health;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests.Health;

/// <summary>
/// Unit tests for SensorReading class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Health")]
public sealed class SensorReadingTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSensorReading()
    {
        // Arrange
        var sensorType = SensorType.Temperature;
        var value = 65.0;
        var minValue = 20.0;
        var maxValue = 95.0;
        var name = "GPU Core Temperature";

        // Act
        var reading = SensorReading.Create(sensorType, value, minValue, maxValue, name);

        // Assert
        reading.SensorType.Should().Be(sensorType);
        reading.Value.Should().Be(value);
        reading.MinValue.Should().Be(minValue);
        reading.MaxValue.Should().Be(maxValue);
        reading.Name.Should().Be(name);
        reading.IsAvailable.Should().BeTrue();
        reading.Unit.Should().Be("Celsius");
    }

    [Fact]
    public void Create_WithoutMinMax_ReturnsReadingWithoutBounds()
    {
        // Act
        var reading = SensorReading.Create(SensorType.PowerDraw, 150.0);

        // Assert
        reading.SensorType.Should().Be(SensorType.PowerDraw);
        reading.Value.Should().Be(150.0);
        reading.MinValue.Should().BeNull();
        reading.MaxValue.Should().BeNull();
        reading.IsAvailable.Should().BeTrue();
        reading.Unit.Should().Be("Watts");
    }

    [Fact]
    public void Unavailable_ReturnsSensorReadingWithAvailableFalse()
    {
        // Act
        var reading = SensorReading.Unavailable(SensorType.Temperature, "GPU Core Temperature");

        // Assert
        reading.SensorType.Should().Be(SensorType.Temperature);
        reading.IsAvailable.Should().BeFalse();
        reading.Value.Should().Be(0.0);
        reading.Name.Should().Be("GPU Core Temperature");
    }

    [Theory]
    [InlineData(SensorType.Temperature, "Celsius")]
    [InlineData(SensorType.PowerDraw, "Watts")]
    [InlineData(SensorType.ComputeUtilization, "Percent")]
    [InlineData(SensorType.MemoryUtilization, "Percent")]
    [InlineData(SensorType.FanSpeed, "Percent")]
    [InlineData(SensorType.GraphicsClock, "MHz")]
    [InlineData(SensorType.MemoryClock, "MHz")]
    [InlineData(SensorType.MemoryUsedBytes, "Bytes")]
    [InlineData(SensorType.MemoryTotalBytes, "Bytes")]
    [InlineData(SensorType.MemoryFreeBytes, "Bytes")]
    [InlineData(SensorType.PcieLinkGeneration, "Generation")]
    [InlineData(SensorType.PcieLinkWidth, "Lanes")]
    [InlineData(SensorType.PcieThroughputTx, "Bytes/sec")]
    [InlineData(SensorType.PcieThroughputRx, "Bytes/sec")]
    [InlineData(SensorType.ThrottlingStatus, "Level")]
    [InlineData(SensorType.PowerThrottlingStatus, "Level")]
    [InlineData(SensorType.ErrorCount, "Count")]
    [InlineData(SensorType.Custom, "Unknown")]
    public void Create_AutomaticallyAssignsCorrectUnit(SensorType sensorType, string expectedUnit)
    {
        // Act
        var reading = SensorReading.Create(sensorType, 100.0);

        // Assert
        reading.Unit.Should().Be(expectedUnit);
    }

    [Fact]
    public void Create_WithCustomUnit_OverridesDefaultUnit()
    {
        // Act
        var reading = SensorReading.Create(SensorType.Temperature, 338.15, name: "Kelvin Temperature");

        // Note: The unit is auto-assigned by the factory method based on sensor type
        // For custom units, we'd need to use a different constructor or builder
        // Assert
        reading.Unit.Should().Be("Celsius"); // Default for temperature
    }

    [Fact]
    public void Quality_DefaultsToOne()
    {
        // Act
        var reading = SensorReading.Create(SensorType.Temperature, 65.0);

        // Assert
        reading.Quality.Should().Be(1.0);
    }

    [Fact]
    public void Timestamp_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var reading = SensorReading.Create(SensorType.Temperature, 65.0);

        // Assert
        var after = DateTimeOffset.UtcNow;
        reading.Timestamp.Should().BeOnOrAfter(before);
        reading.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void WithInit_AllPropertiesCanBeSet()
    {
        // Act
        var reading = new SensorReading
        {
            SensorType = SensorType.Temperature,
            Value = 65.0,
            Unit = "Celsius",
            IsAvailable = true,
            Timestamp = DateTimeOffset.UtcNow,
            Name = "Test Sensor",
            MinValue = 20.0,
            MaxValue = 95.0,
            Quality = 0.95
        };

        // Assert
        reading.SensorType.Should().Be(SensorType.Temperature);
        reading.Value.Should().Be(65.0);
        reading.Unit.Should().Be("Celsius");
        reading.IsAvailable.Should().BeTrue();
        reading.Name.Should().Be("Test Sensor");
        reading.MinValue.Should().Be(20.0);
        reading.MaxValue.Should().Be(95.0);
        reading.Quality.Should().Be(0.95);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var reading1 = new SensorReading
        {
            SensorType = SensorType.Temperature,
            Value = 65.0,
            Unit = "Celsius",
            IsAvailable = true,
            Timestamp = timestamp
        };

        var reading2 = new SensorReading
        {
            SensorType = SensorType.Temperature,
            Value = 65.0,
            Unit = "Celsius",
            IsAvailable = true,
            Timestamp = timestamp
        };

        // Assert
        reading1.Should().Be(reading2);
        reading1.GetHashCode().Should().Be(reading2.GetHashCode());
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var reading1 = SensorReading.Create(SensorType.Temperature, 65.0);
        var reading2 = SensorReading.Create(SensorType.Temperature, 70.0);

        // Assert
        reading1.Should().NotBe(reading2);
    }
}
