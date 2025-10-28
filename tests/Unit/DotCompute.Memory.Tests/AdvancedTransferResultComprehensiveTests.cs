using Xunit;
using FluentAssertions;
using DotCompute.Memory;
using DotCompute.Memory.Types;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using NSubstitute;
using System;
using System.Collections.Generic;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for AdvancedTransferResult class covering all 19 properties and edge cases.
/// Target: 95%+ pass rate with 30-40 tests.
/// </summary>
public class AdvancedTransferResultComprehensiveTests
{
    #region Constructor and Default Values

    [Fact]
    public void Constructor_SetsDefaultCompressionRatio()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.CompressionRatio.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.Success.Should().BeFalse();
        result.RetryCount.Should().Be(0);
        result.ChunkCount.Should().Be(0);
        result.TotalBytes.Should().Be(0);
        result.UsedStreaming.Should().BeFalse();
        result.UsedCompression.Should().BeFalse();
        result.UsedMemoryMapping.Should().BeFalse();
        result.IntegrityVerified.Should().BeFalse();
    }

    #endregion

    #region Timestamp Properties

    [Fact]
    public void StartTime_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var time = DateTimeOffset.UtcNow;

        // Act
        result.StartTime = time;

        // Assert
        result.StartTime.Should().Be(time);
    }

    [Fact]
    public void EndTime_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var time = DateTimeOffset.UtcNow.AddSeconds(10);

        // Act
        result.EndTime = time;

        // Assert
        result.EndTime.Should().Be(time);
    }

    [Fact]
    public void Duration_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var duration = TimeSpan.FromSeconds(5);

        // Act
        result.Duration = duration;

        // Assert
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void Duration_ReflectsTimeDifference()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(5);
        var result = new AdvancedTransferResult
        {
            StartTime = startTime,
            EndTime = endTime
        };

        // Act
        result.Duration = result.EndTime - result.StartTime;

        // Assert
        result.Duration.TotalSeconds.Should().BeApproximately(5, 0.1);
    }

    #endregion

    #region Property Setters - Numeric Properties

    [Fact]
    public void TotalBytes_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.TotalBytes = 1048576; // 1 MB

        // Assert
        result.TotalBytes.Should().Be(1048576);
    }

    [Fact]
    public void ChunkCount_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ChunkCount = 10;

        // Assert
        result.ChunkCount.Should().Be(10);
    }

    [Fact]
    public void CompressionRatio_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.CompressionRatio = 0.5;

        // Assert
        result.CompressionRatio.Should().Be(0.5);
    }

    [Fact]
    public void ThroughputBytesPerSecond_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ThroughputBytesPerSecond = 1048576; // 1 MB/s

        // Assert
        result.ThroughputBytesPerSecond.Should().Be(1048576);
    }

    [Fact]
    public void ThroughputMBps_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ThroughputMBps = 100.5;

        // Assert
        result.ThroughputMBps.Should().Be(100.5);
    }

    [Fact]
    public void RetryCount_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.RetryCount = 3;

        // Assert
        result.RetryCount.Should().Be(3);
    }

    #endregion

    #region Boolean Flags

    [Fact]
    public void Success_DefaultsToFalse()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Success_CanBeSetToTrue()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.Success = true;

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void UsedStreaming_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.UsedStreaming = true;

        // Assert
        result.UsedStreaming.Should().BeTrue();
    }

    [Fact]
    public void UsedCompression_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.UsedCompression = true;

        // Assert
        result.UsedCompression.Should().BeTrue();
    }

    [Fact]
    public void UsedMemoryMapping_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.UsedMemoryMapping = true;

        // Assert
        result.UsedMemoryMapping.Should().BeTrue();
    }

    [Fact]
    public void IntegrityVerified_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.IntegrityVerified = true;

        // Assert
        result.IntegrityVerified.Should().BeTrue();
    }

    [Fact]
    public void BooleanFlags_AllStartFalse()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.UsedStreaming.Should().BeFalse();
        result.UsedCompression.Should().BeFalse();
        result.UsedMemoryMapping.Should().BeFalse();
        result.IntegrityVerified.Should().BeFalse();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void BooleanFlags_CanBeSetIndependently()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.UsedStreaming = true;
        result.UsedCompression = false;
        result.UsedMemoryMapping = true;

        // Assert
        result.UsedStreaming.Should().BeTrue();
        result.UsedCompression.Should().BeFalse();
        result.UsedMemoryMapping.Should().BeTrue();
    }

    #endregion

    #region Nullable Properties

    [Fact]
    public void IntegrityCheckPassed_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.IntegrityCheckPassed.Should().BeNull();
    }

    [Fact]
    public void IntegrityCheckPassed_CanBeSetToTrue()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.IntegrityCheckPassed = true;

        // Assert
        result.IntegrityCheckPassed.Should().BeTrue();
    }

    [Fact]
    public void IntegrityCheckPassed_CanBeSetToFalse()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.IntegrityCheckPassed = false;

        // Assert
        result.IntegrityCheckPassed.Should().BeFalse();
    }

    [Fact]
    public void TransferredBuffer_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.TransferredBuffer.Should().BeNull();
    }

    [Fact]
    public void TransferredBuffer_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();

        // Act
        result.TransferredBuffer = buffer;

        // Assert
        result.TransferredBuffer.Should().BeSameAs(buffer);
    }

    [Fact]
    public void Error_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Error_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var exception = new InvalidOperationException("Test error");

        // Act
        result.Error = exception;

        // Assert
        result.Error.Should().BeSameAs(exception);
        result.Error!.Message.Should().Be("Test error");
    }

    [Fact]
    public void Metadata_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        result.Metadata.Should().BeNull();
    }

    [Fact]
    public void Metadata_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var metadata = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        result.Metadata = metadata;

        // Assert
        result.Metadata.Should().BeSameAs(metadata);
        result.Metadata.Should().ContainKey("key1");
        result.Metadata!["key2"].Should().Be(42);
    }

    // NOTE: Metrics property tests removed - property doesn't exist in AdvancedTransferResult

    #endregion

    #region Throughput Calculations

    [Fact]
    public void ThroughputBytesPerSecond_WithLargeValue()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ThroughputBytesPerSecond = 10_737_418_240; // 10 GB/s

        // Assert
        result.ThroughputBytesPerSecond.Should().Be(10_737_418_240);
    }

    [Fact]
    public void ThroughputMBps_WithDecimalValue()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ThroughputMBps = 1234.5678;

        // Assert
        result.ThroughputMBps.Should().Be(1234.5678);
    }

    [Fact]
    public void ThroughputBytesPerSecond_WithZero()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ThroughputBytesPerSecond = 0;

        // Assert
        result.ThroughputBytesPerSecond.Should().Be(0);
    }

    [Fact]
    public void ThroughputMBps_WithZero()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ThroughputMBps = 0;

        // Assert
        result.ThroughputMBps.Should().Be(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CompressionRatio_WithHighCompression_ReflectsRatio()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.CompressionRatio = 0.25; // 75% compression

        // Assert
        result.CompressionRatio.Should().Be(0.25);
    }

    [Fact]
    public void CompressionRatio_WithNoCompression_RemainsOne()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act - Don't set compression ratio, use default

        // Assert
        result.CompressionRatio.Should().Be(1.0);
    }

    [Fact]
    public void RetryCount_CanBeIncremented()
    {
        // Arrange
        var result = new AdvancedTransferResult { RetryCount = 0 };

        // Act
        result.RetryCount++;
        result.RetryCount++;

        // Assert
        result.RetryCount.Should().Be(2);
    }

    [Fact]
    public void TotalBytes_WithZero()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.TotalBytes = 0;

        // Assert
        result.TotalBytes.Should().Be(0);
    }

    [Fact]
    public void CompleteSuccessfulTransfer_AllPropertiesSet()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(2);
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();

        // Act
        var result = new AdvancedTransferResult
        {
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime - startTime,
            TotalBytes = 2_097_152, // 2 MB
            ChunkCount = 4,
            UsedStreaming = true,
            UsedCompression = true,
            CompressionRatio = 0.5,
            ThroughputBytesPerSecond = 1_048_576, // 1 MB/s
            ThroughputMBps = 1.0,
            IntegrityVerified = true,
            IntegrityCheckPassed = true,
            TransferredBuffer = buffer,
            Success = true,
            RetryCount = 0
        };

        // Assert
        result.Success.Should().BeTrue();
        result.TotalBytes.Should().Be(2_097_152);
        result.ChunkCount.Should().Be(4);
        result.UsedStreaming.Should().BeTrue();
        result.UsedCompression.Should().BeTrue();
        result.CompressionRatio.Should().Be(0.5);
        result.IntegrityVerified.Should().BeTrue();
        result.IntegrityCheckPassed.Should().BeTrue();
        result.TransferredBuffer.Should().BeSameAs(buffer);
        result.RetryCount.Should().Be(0);
    }

    [Fact]
    public void FailedTransfer_WithError()
    {
        // Arrange
        var exception = new TimeoutException("Transfer timed out");

        // Act
        var result = new AdvancedTransferResult
        {
            Success = false,
            Error = exception,
            RetryCount = 3,
            TotalBytes = 1024
        };

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeSameAs(exception);
        result.Error!.Message.Should().Be("Transfer timed out");
        result.RetryCount.Should().Be(3);
    }

    [Fact]
    public void Metadata_WithMultipleTypes()
    {
        // Arrange
        var result = new AdvancedTransferResult();
        var metadata = new Dictionary<string, object>
        {
            ["string_value"] = "test",
            ["int_value"] = 42,
            ["double_value"] = 3.14,
            ["bool_value"] = true,
            ["datetime_value"] = DateTime.UtcNow
        };

        // Act
        result.Metadata = metadata;

        // Assert
        result.Metadata.Should().HaveCount(5);
        result.Metadata!["string_value"].Should().Be("test");
        result.Metadata["int_value"].Should().Be(42);
        result.Metadata["double_value"].Should().Be(3.14);
        result.Metadata["bool_value"].Should().Be(true);
        result.Metadata.Should().ContainKey("datetime_value");
    }

    [Fact]
    public void IntegrityCheckPassed_CanBeSetToNullAfterValue()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            IntegrityCheckPassed = true
        };

        // Act
        result.IntegrityCheckPassed = null;

        // Assert
        result.IntegrityCheckPassed.Should().BeNull();
    }

    [Fact]
    public void ChunkCount_WithHighValue()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.ChunkCount = 1000;

        // Assert
        result.ChunkCount.Should().Be(1000);
    }

    [Fact]
    public void UsedMemoryMapping_WithOtherFlags()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act
        result.UsedMemoryMapping = true;
        result.UsedStreaming = false;
        result.UsedCompression = true;

        // Assert
        result.UsedMemoryMapping.Should().BeTrue();
        result.UsedStreaming.Should().BeFalse();
        result.UsedCompression.Should().BeTrue();
    }

    #endregion
}
