// <copyright file="ConcurrentTransferResultComprehensiveTests.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for the <see cref="Types.ConcurrentTransferResult"/> class.
/// Tests all properties, calculated properties, collections, and edge cases.
/// </summary>
public class ConcurrentTransferResultComprehensiveTests
{
    #region Constructor and Initialization

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var result = new Types.ConcurrentTransferResult();

        // Assert
        result.Success.Should().BeFalse();
        result.StartTime.Should().Be(default(DateTimeOffset));
        result.Duration.Should().Be(TimeSpan.Zero);
        result.TransferCount.Should().Be(0);
        result.SuccessfulTransfers.Should().Be(0);
        result.FailedTransfers.Should().Be(0);
        result.TotalBytesTransferred.Should().Be(0);
        result.AggregateThroughputMBps.Should().Be(0);
        result.AverageThroughputMBps.Should().Be(0);
        result.PeakThroughputMBps.Should().Be(0);
        result.MinThroughputMBps.Should().Be(0);
        result.MaxConcurrency.Should().Be(0);
        result.AverageConcurrency.Should().Be(0);
        result.PeakMemoryPressure.Should().Be(0);
        result.UsedMemoryMapping.Should().BeFalse();
        result.UsedStreaming.Should().BeFalse();
        result.UsedCompression.Should().BeFalse();
        result.ConcurrencyBenefit.Should().Be(0);
        result.Metadata.Should().BeNull();
    }

    [Fact]
    public void Constructor_InitializesCollections()
    {
        // Act
        var result = new Types.ConcurrentTransferResult();

        // Assert
        result.IndividualResults.Should().NotBeNull().And.BeEmpty();
        result.Errors.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesReadOnlyResultsAlias()
    {
        // Act
        var result = new Types.ConcurrentTransferResult();

        // Assert
        result.Results.Should().NotBeNull().And.BeEmpty();
        result.Results.Should().BeSameAs(result.IndividualResults);
    }

    #endregion

    #region Property Setters - Boolean Properties

    [Fact]
    public void Success_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.Success = true;

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void UsedMemoryMapping_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.UsedMemoryMapping = true;

        // Assert
        result.UsedMemoryMapping.Should().BeTrue();
    }

    [Fact]
    public void UsedStreaming_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.UsedStreaming = true;

        // Assert
        result.UsedStreaming.Should().BeTrue();
    }

    [Fact]
    public void UsedCompression_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.UsedCompression = true;

        // Assert
        result.UsedCompression.Should().BeTrue();
    }

    #endregion

    #region Property Setters - Time-Related Properties

    [Fact]
    public void StartTime_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        var startTime = DateTimeOffset.UtcNow;

        // Act
        result.StartTime = startTime;

        // Assert
        result.StartTime.Should().Be(startTime);
    }

    [Fact]
    public void Duration_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        var duration = TimeSpan.FromSeconds(5.5);

        // Act
        result.Duration = duration;

        // Assert
        result.Duration.Should().Be(duration);
    }

    #endregion

    #region Property Setters - Transfer Count Properties

    [Fact]
    public void TransferCount_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.TransferCount = 100;

        // Assert
        result.TransferCount.Should().Be(100);
    }

    [Fact]
    public void SuccessfulTransfers_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.SuccessfulTransfers = 85;

        // Assert
        result.SuccessfulTransfers.Should().Be(85);
    }

    [Fact]
    public void FailedTransfers_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.FailedTransfers = 15;

        // Assert
        result.FailedTransfers.Should().Be(15);
    }

    #endregion

    #region Property Setters - Byte and Throughput Properties

    [Fact]
    public void TotalBytesTransferred_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.TotalBytesTransferred = 1_073_741_824; // 1 GB

        // Assert
        result.TotalBytesTransferred.Should().Be(1_073_741_824);
    }

    [Fact]
    public void AggregateThroughputMBps_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.AggregateThroughputMBps = 1500.75;

        // Assert
        result.AggregateThroughputMBps.Should().Be(1500.75);
    }

    [Fact]
    public void AverageThroughputMBps_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.AverageThroughputMBps = 125.5;

        // Assert
        result.AverageThroughputMBps.Should().Be(125.5);
    }

    [Fact]
    public void PeakThroughputMBps_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.PeakThroughputMBps = 250.0;

        // Assert
        result.PeakThroughputMBps.Should().Be(250.0);
    }

    [Fact]
    public void MinThroughputMBps_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.MinThroughputMBps = 50.0;

        // Assert
        result.MinThroughputMBps.Should().Be(50.0);
    }

    #endregion

    #region Property Setters - Concurrency Properties

    [Fact]
    public void MaxConcurrency_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.MaxConcurrency = 16;

        // Assert
        result.MaxConcurrency.Should().Be(16);
    }

    [Fact]
    public void AverageConcurrency_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.AverageConcurrency = 12.5;

        // Assert
        result.AverageConcurrency.Should().Be(12.5);
    }

    [Fact]
    public void ConcurrencyBenefit_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.ConcurrencyBenefit = 0.85;

        // Assert
        result.ConcurrencyBenefit.Should().Be(0.85);
    }

    #endregion

    #region Property Setters - Memory Pressure

    [Fact]
    public void PeakMemoryPressure_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.PeakMemoryPressure = 0.75;

        // Assert
        result.PeakMemoryPressure.Should().Be(0.75);
    }

    [Fact]
    public void PeakMemoryPressure_AcceptsBoundaryValues()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.PeakMemoryPressure = 0.0;
        var zeroValue = result.PeakMemoryPressure;

        result.PeakMemoryPressure = 1.0;
        var maxValue = result.PeakMemoryPressure;

        // Assert
        zeroValue.Should().Be(0.0);
        maxValue.Should().Be(1.0);
    }

    #endregion

    #region Property Setters - Metadata

    [Fact]
    public void Metadata_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        var metadata = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 42
        };

        // Act
        result.Metadata = metadata;

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().HaveCount(2);
        result.Metadata!["Key1"].Should().Be("Value1");
        result.Metadata["Key2"].Should().Be(42);
    }

    [Fact]
    public void Metadata_CanBeSetToNull()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            Metadata = new Dictionary<string, object> { ["Test"] = "Value" }
        };

        // Act
        result.Metadata = null;

        // Assert
        result.Metadata.Should().BeNull();
    }

    #endregion

    #region TotalBytes Alias Property

    [Fact]
    public void TotalBytes_Getter_ReturnsTransferredBytes()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TotalBytesTransferred = 5_368_709_120 // 5 GB
        };

        // Act
        var totalBytes = result.TotalBytes;

        // Assert
        totalBytes.Should().Be(5_368_709_120);
    }

    [Fact]
    public void TotalBytes_Setter_UpdatesTransferredBytes()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.TotalBytes = 2_147_483_648; // 2 GB

        // Assert
        result.TotalBytes.Should().Be(2_147_483_648);
        result.TotalBytesTransferred.Should().Be(2_147_483_648);
    }

    #endregion

    #region SuccessRate Calculation

    [Fact]
    public void SuccessRate_WithAllSuccessful_Returns100()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 50,
            SuccessfulTransfers = 50,
            FailedTransfers = 0
        };

        // Act
        var successRate = result.SuccessRate;

        // Assert
        successRate.Should().Be(100.0);
    }

    [Fact]
    public void SuccessRate_WithPartialSuccess_ReturnsCorrectPercentage()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 20,
            SuccessfulTransfers = 15,
            FailedTransfers = 5
        };

        // Act
        var successRate = result.SuccessRate;

        // Assert
        successRate.Should().Be(75.0);
    }

    [Fact]
    public void SuccessRate_WithAllFailed_ReturnsZero()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 10,
            SuccessfulTransfers = 0,
            FailedTransfers = 10
        };

        // Act
        var successRate = result.SuccessRate;

        // Assert
        successRate.Should().Be(0.0);
    }

    [Fact]
    public void SuccessRate_WithZeroTransfers_ReturnsZero()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 0,
            SuccessfulTransfers = 0,
            FailedTransfers = 0
        };

        // Act
        var successRate = result.SuccessRate;

        // Assert
        successRate.Should().Be(0.0);
    }

    [Fact]
    public void SuccessRate_WithOneTransfer_ReturnsCorrectPercentage()
    {
        // Arrange
        var successfulResult = new Types.ConcurrentTransferResult
        {
            TransferCount = 1,
            SuccessfulTransfers = 1
        };

        var failedResult = new Types.ConcurrentTransferResult
        {
            TransferCount = 1,
            SuccessfulTransfers = 0
        };

        // Act & Assert
        successfulResult.SuccessRate.Should().Be(100.0);
        failedResult.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public void SuccessRate_WithFractionalResult_ReturnsCorrectPercentage()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 3,
            SuccessfulTransfers = 2
        };

        // Act
        var successRate = result.SuccessRate;

        // Assert
        successRate.Should().BeApproximately(66.666666, 0.00001);
    }

    #endregion

    #region EfficiencyRatio Calculation

    [Fact]
    public void EfficiencyRatio_WithValidValues_ReturnsCorrectRatio()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            AggregateThroughputMBps = 1200,
            AverageThroughputMBps = 100,
            AverageConcurrency = 10
        };

        // Act
        var efficiency = result.EfficiencyRatio;

        // Assert
        efficiency.Should().BeApproximately(1.2, 0.01);
    }

    [Fact]
    public void EfficiencyRatio_WithPerfectEfficiency_ReturnsOne()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            AggregateThroughputMBps = 1000,
            AverageThroughputMBps = 100,
            AverageConcurrency = 10
        };

        // Act
        var efficiency = result.EfficiencyRatio;

        // Assert
        efficiency.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void EfficiencyRatio_WithZeroAverageConcurrency_ReturnsZero()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            AggregateThroughputMBps = 1000,
            AverageThroughputMBps = 100,
            AverageConcurrency = 0
        };

        // Act
        var efficiency = result.EfficiencyRatio;

        // Assert
        efficiency.Should().Be(0.0);
    }

    [Fact]
    public void EfficiencyRatio_WithZeroAverageThroughput_HandlesEdgeCase()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            AggregateThroughputMBps = 1000,
            AverageThroughputMBps = 0,
            AverageConcurrency = 10
        };

        // Act
        var efficiency = result.EfficiencyRatio;

        // Assert - Division by zero results in infinity or NaN
        efficiency.Should().Match(x => double.IsInfinity((double)x) || double.IsNaN((double)x));
    }

    [Fact]
    public void EfficiencyRatio_WithLowConcurrency_ReturnsHighRatio()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            AggregateThroughputMBps = 500,
            AverageThroughputMBps = 100,
            AverageConcurrency = 2
        };

        // Act
        var efficiency = result.EfficiencyRatio;

        // Assert
        efficiency.Should().BeApproximately(2.5, 0.01);
    }

    [Fact]
    public void EfficiencyRatio_WithHighConcurrency_ReturnsLowRatio()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            AggregateThroughputMBps = 500,
            AverageThroughputMBps = 100,
            AverageConcurrency = 20
        };

        // Act
        var efficiency = result.EfficiencyRatio;

        // Assert
        efficiency.Should().BeApproximately(0.25, 0.01);
    }

    #endregion

    #region Collection Properties - IndividualResults

    [Fact]
    public void IndividualResults_CanAddItems()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        var individual = Substitute.For<Types.AdvancedTransferResult>();

        // Act
        result.IndividualResults.Add(individual);

        // Assert
        result.IndividualResults.Should().HaveCount(1);
        result.IndividualResults[0].Should().BeSameAs(individual);
    }

    [Fact]
    public void IndividualResults_CanAddMultipleItems()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        var individual1 = Substitute.For<Types.AdvancedTransferResult>();
        var individual2 = Substitute.For<Types.AdvancedTransferResult>();
        var individual3 = Substitute.For<Types.AdvancedTransferResult>();

        // Act
        result.IndividualResults.Add(individual1);
        result.IndividualResults.Add(individual2);
        result.IndividualResults.Add(individual3);

        // Assert
        result.IndividualResults.Should().HaveCount(3);
        result.IndividualResults.Should().ContainInOrder(individual1, individual2, individual3);
    }

    [Fact]
    public void Results_ReflectsIndividualResults()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        var individual = Substitute.For<Types.AdvancedTransferResult>();

        // Act
        result.IndividualResults.Add(individual);

        // Assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Should().BeSameAs(individual);
    }

    #endregion

    #region Collection Properties - Errors

    [Fact]
    public void Errors_CanAddItems()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.Errors.Add("Error 1");

        // Assert
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Be("Error 1");
    }

    [Fact]
    public void Errors_CanAddMultipleItems()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.Errors.Add("Transfer timeout on buffer 0");
        result.Errors.Add("Device not found for buffer 1");
        result.Errors.Add("Out of memory on buffer 2");

        // Assert
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Transfer timeout on buffer 0");
        result.Errors.Should().Contain("Device not found for buffer 1");
        result.Errors.Should().Contain("Out of memory on buffer 2");
    }

    [Fact]
    public void Errors_CanRemoveItems()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        result.Errors.Add("Error to remove");
        result.Errors.Add("Error to keep");

        // Act
        result.Errors.Remove("Error to remove");

        // Assert
        result.Errors.Should().HaveCount(1);
        result.Errors.Should().Contain("Error to keep");
    }

    [Fact]
    public void Errors_CanBeCleared()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();
        result.Errors.Add("Error 1");
        result.Errors.Add("Error 2");

        // Act
        result.Errors.Clear();

        // Assert
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region ToString Method

    [Fact]
    public void ToString_WithValidData_ReturnsFormattedString()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 10,
            SuccessfulTransfers = 8,
            TotalBytesTransferred = 1_073_741_824, // 1 GB
            Duration = TimeSpan.FromSeconds(2),
            AggregateThroughputMBps = 500,
            AverageThroughputMBps = 50,
            MaxConcurrency = 8,
            AverageConcurrency = 6
        };

        // Act
        var text = result.ToString();

        // Assert
        text.Should().Contain("10 transfers");
        text.Should().Contain("1.00 GB");
        text.Should().Contain("2.00s");
        text.Should().Contain("500.00 MB/s");
        text.Should().Contain("50.00 MB/s");
        text.Should().Contain("80.0%"); // Success rate
        text.Should().Contain("Max Concurrency: 8");
    }

    [Fact]
    public void ToString_WithZeroValues_HandlesGracefully()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        var text = result.ToString();

        // Assert
        text.Should().Contain("0 transfers");
        text.Should().Contain("0.00 GB");
        text.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToString_WithLargeValues_FormatsCorrectly()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 1000,
            TotalBytesTransferred = 107_374_182_400, // 100 GB
            Duration = TimeSpan.FromMinutes(5),
            AggregateThroughputMBps = 5000,
            AverageThroughputMBps = 500
        };

        // Act
        var text = result.ToString();

        // Assert
        text.Should().Contain("1000 transfers");
        text.Should().Contain("100.00 GB");
        text.Should().Contain("300.00s"); // 5 minutes
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public void Properties_WithNegativeValues_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act - Setting negative values (should not throw, validation is caller's responsibility)
        result.TransferCount = -1;
        result.TotalBytesTransferred = -1000;
        result.AggregateThroughputMBps = -100.5;

        // Assert - Values are stored as-is
        result.TransferCount.Should().Be(-1);
        result.TotalBytesTransferred.Should().Be(-1000);
        result.AggregateThroughputMBps.Should().Be(-100.5);
    }

    [Fact]
    public void Properties_WithMaxValues_CanBeSet()
    {
        // Arrange
        var result = new Types.ConcurrentTransferResult();

        // Act
        result.TransferCount = int.MaxValue;
        result.TotalBytesTransferred = long.MaxValue;
        result.AggregateThroughputMBps = double.MaxValue;

        // Assert
        result.TransferCount.Should().Be(int.MaxValue);
        result.TotalBytesTransferred.Should().Be(long.MaxValue);
        result.AggregateThroughputMBps.Should().Be(double.MaxValue);
    }

    [Fact]
    public void SuccessRate_WithMismatchedCounts_ReturnsBasedOnTransferCount()
    {
        // Arrange - Edge case where successful + failed != total
        var result = new Types.ConcurrentTransferResult
        {
            TransferCount = 10,
            SuccessfulTransfers = 7,
            FailedTransfers = 2 // 7 + 2 = 9, not 10
        };

        // Act
        var successRate = result.SuccessRate;

        // Assert - Calculation only uses SuccessfulTransfers and TransferCount
        successRate.Should().Be(70.0);
    }

    [Fact]
    public void CompleteScenario_RealisticTransferData()
    {
        // Arrange & Act - Realistic concurrent transfer scenario
        var result = new Types.ConcurrentTransferResult
        {
            Success = true,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            Duration = TimeSpan.FromMinutes(5),
            TransferCount = 16,
            SuccessfulTransfers = 15,
            FailedTransfers = 1,
            TotalBytesTransferred = 8_589_934_592, // 8 GB
            AggregateThroughputMBps = 1600,
            AverageThroughputMBps = 100,
            PeakThroughputMBps = 150,
            MinThroughputMBps = 80,
            MaxConcurrency = 16,
            AverageConcurrency = 12.5,
            PeakMemoryPressure = 0.65,
            UsedMemoryMapping = true,
            UsedStreaming = false,
            UsedCompression = true,
            ConcurrencyBenefit = 0.92,
            Metadata = new Dictionary<string, object>
            {
                ["TransferType"] = "GPU-to-GPU",
                ["DeviceCount"] = 4
            }
        };

        result.Errors.Add("Transfer 12 timed out after 30 seconds");

        // Assert - Verify all properties and calculations
        result.Success.Should().BeTrue();
        result.TransferCount.Should().Be(16);
        result.SuccessRate.Should().BeApproximately(93.75, 0.01);
        result.EfficiencyRatio.Should().BeApproximately(1.28, 0.01);
        result.TotalBytes.Should().Be(8_589_934_592);
        result.Errors.Should().HaveCount(1);
        result.Metadata.Should().ContainKey("TransferType");
    }

    #endregion
}
