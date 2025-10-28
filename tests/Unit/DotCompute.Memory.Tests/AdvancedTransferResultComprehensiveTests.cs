using DotCompute.Memory.Types;
using DotCompute.Abstractions;
using NSubstitute;

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
        _ = result.CompressionRatio.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.Success.Should().BeFalse();
        _ = result.RetryCount.Should().Be(0);
        _ = result.ChunkCount.Should().Be(0);
        _ = result.TotalBytes.Should().Be(0);
        _ = result.UsedStreaming.Should().BeFalse();
        _ = result.UsedCompression.Should().BeFalse();
        _ = result.UsedMemoryMapping.Should().BeFalse();
        _ = result.IntegrityVerified.Should().BeFalse();
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
        _ = result.StartTime.Should().Be(time);
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
        _ = result.EndTime.Should().Be(time);
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
        _ = result.Duration.Should().Be(duration);
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
        _ = result.Duration.TotalSeconds.Should().BeApproximately(5, 0.1);
    }

    #endregion

    #region Property Setters - Numeric Properties

    [Fact]
    public void TotalBytes_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            TotalBytes = 1048576 // 1 MB
        };

        // Assert
        _ = result.TotalBytes.Should().Be(1048576);
    }

    [Fact]
    public void ChunkCount_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ChunkCount = 10
        };

        // Assert
        _ = result.ChunkCount.Should().Be(10);
    }

    [Fact]
    public void CompressionRatio_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            CompressionRatio = 0.5
        };

        // Assert
        _ = result.CompressionRatio.Should().Be(0.5);
    }

    [Fact]
    public void ThroughputBytesPerSecond_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ThroughputBytesPerSecond = 1048576 // 1 MB/s
        };

        // Assert
        _ = result.ThroughputBytesPerSecond.Should().Be(1048576);
    }

    [Fact]
    public void ThroughputMBps_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ThroughputMBps = 100.5
        };

        // Assert
        _ = result.ThroughputMBps.Should().Be(100.5);
    }

    [Fact]
    public void RetryCount_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            RetryCount = 3
        };

        // Assert
        _ = result.RetryCount.Should().Be(3);
    }

    #endregion

    #region Boolean Flags

    [Fact]
    public void Success_DefaultsToFalse()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.Success.Should().BeFalse();
    }

    [Fact]
    public void Success_CanBeSetToTrue()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            Success = true
        };

        // Assert
        _ = result.Success.Should().BeTrue();
    }

    [Fact]
    public void UsedStreaming_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            UsedStreaming = true
        };

        // Assert
        _ = result.UsedStreaming.Should().BeTrue();
    }

    [Fact]
    public void UsedCompression_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            UsedCompression = true
        };

        // Assert
        _ = result.UsedCompression.Should().BeTrue();
    }

    [Fact]
    public void UsedMemoryMapping_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            UsedMemoryMapping = true
        };

        // Assert
        _ = result.UsedMemoryMapping.Should().BeTrue();
    }

    [Fact]
    public void IntegrityVerified_CanBeSet()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            IntegrityVerified = true
        };

        // Assert
        _ = result.IntegrityVerified.Should().BeTrue();
    }

    [Fact]
    public void BooleanFlags_AllStartFalse()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.UsedStreaming.Should().BeFalse();
        _ = result.UsedCompression.Should().BeFalse();
        _ = result.UsedMemoryMapping.Should().BeFalse();
        _ = result.IntegrityVerified.Should().BeFalse();
        _ = result.Success.Should().BeFalse();
    }

    [Fact]
    public void BooleanFlags_CanBeSetIndependently()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            UsedStreaming = true,
            UsedCompression = false,
            UsedMemoryMapping = true
        };

        // Assert
        _ = result.UsedStreaming.Should().BeTrue();
        _ = result.UsedCompression.Should().BeFalse();
        _ = result.UsedMemoryMapping.Should().BeTrue();
    }

    #endregion

    #region Nullable Properties

    [Fact]
    public void IntegrityCheckPassed_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.IntegrityCheckPassed.Should().BeNull();
    }

    [Fact]
    public void IntegrityCheckPassed_CanBeSetToTrue()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            IntegrityCheckPassed = true
        };

        // Assert
        _ = result.IntegrityCheckPassed.Should().BeTrue();
    }

    [Fact]
    public void IntegrityCheckPassed_CanBeSetToFalse()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            IntegrityCheckPassed = false
        };

        // Assert
        _ = result.IntegrityCheckPassed.Should().BeFalse();
    }

    [Fact]
    public void TransferredBuffer_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.TransferredBuffer.Should().BeNull();
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
        _ = result.TransferredBuffer.Should().BeSameAs(buffer);
    }

    [Fact]
    public void Error_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.Error.Should().BeNull();
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
        _ = result.Error.Should().BeSameAs(exception);
        _ = result.Error!.Message.Should().Be("Test error");
    }

    [Fact]
    public void Metadata_DefaultsToNull()
    {
        // Act
        var result = new AdvancedTransferResult();

        // Assert
        _ = result.Metadata.Should().BeNull();
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
        _ = result.Metadata.Should().BeSameAs(metadata);
        _ = result.Metadata.Should().ContainKey("key1");
        _ = result.Metadata!["key2"].Should().Be(42);
    }

    // NOTE: Metrics property tests removed - property doesn't exist in AdvancedTransferResult

    #endregion

    #region Throughput Calculations

    [Fact]
    public void ThroughputBytesPerSecond_WithLargeValue()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ThroughputBytesPerSecond = 10_737_418_240 // 10 GB/s
        };

        // Assert
        _ = result.ThroughputBytesPerSecond.Should().Be(10_737_418_240);
    }

    [Fact]
    public void ThroughputMBps_WithDecimalValue()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ThroughputMBps = 1234.5678
        };

        // Assert
        _ = result.ThroughputMBps.Should().Be(1234.5678);
    }

    [Fact]
    public void ThroughputBytesPerSecond_WithZero()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ThroughputBytesPerSecond = 0
        };

        // Assert
        _ = result.ThroughputBytesPerSecond.Should().Be(0);
    }

    [Fact]
    public void ThroughputMBps_WithZero()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ThroughputMBps = 0
        };

        // Assert
        _ = result.ThroughputMBps.Should().Be(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CompressionRatio_WithHighCompression_ReflectsRatio()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            CompressionRatio = 0.25 // 75% compression
        };

        // Assert
        _ = result.CompressionRatio.Should().Be(0.25);
    }

    [Fact]
    public void CompressionRatio_WithNoCompression_RemainsOne()
    {
        // Arrange
        var result = new AdvancedTransferResult();

        // Act - Don't set compression ratio, use default

        // Assert
        _ = result.CompressionRatio.Should().Be(1.0);
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
        _ = result.RetryCount.Should().Be(2);
    }

    [Fact]
    public void TotalBytes_WithZero()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            TotalBytes = 0
        };

        // Assert
        _ = result.TotalBytes.Should().Be(0);
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
        _ = result.Success.Should().BeTrue();
        _ = result.TotalBytes.Should().Be(2_097_152);
        _ = result.ChunkCount.Should().Be(4);
        _ = result.UsedStreaming.Should().BeTrue();
        _ = result.UsedCompression.Should().BeTrue();
        _ = result.CompressionRatio.Should().Be(0.5);
        _ = result.IntegrityVerified.Should().BeTrue();
        _ = result.IntegrityCheckPassed.Should().BeTrue();
        _ = result.TransferredBuffer.Should().BeSameAs(buffer);
        _ = result.RetryCount.Should().Be(0);
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
        _ = result.Success.Should().BeFalse();
        _ = result.Error.Should().BeSameAs(exception);
        _ = result.Error!.Message.Should().Be("Transfer timed out");
        _ = result.RetryCount.Should().Be(3);
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
        _ = result.Metadata.Should().HaveCount(5);
        _ = result.Metadata!["string_value"].Should().Be("test");
        _ = result.Metadata["int_value"].Should().Be(42);
        _ = result.Metadata["double_value"].Should().Be(3.14);
        _ = result.Metadata["bool_value"].Should().Be(true);
        _ = result.Metadata.Should().ContainKey("datetime_value");
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
        _ = result.IntegrityCheckPassed.Should().BeNull();
    }

    [Fact]
    public void ChunkCount_WithHighValue()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            ChunkCount = 1000
        };

        // Assert
        _ = result.ChunkCount.Should().Be(1000);
    }

    [Fact]
    public void UsedMemoryMapping_WithOtherFlags()
    {
        // Arrange
        var result = new AdvancedTransferResult
        {
            // Act
            UsedMemoryMapping = true,
            UsedStreaming = false,
            UsedCompression = true
        };

        // Assert
        _ = result.UsedMemoryMapping.Should().BeTrue();
        _ = result.UsedStreaming.Should().BeFalse();
        _ = result.UsedCompression.Should().BeTrue();
    }

    #endregion
}
