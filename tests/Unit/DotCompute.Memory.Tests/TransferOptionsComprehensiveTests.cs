// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Memory.Types;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for TransferOptions covering all functionality.
/// Target: 100% coverage for 214-line configuration class.
/// </summary>
public class TransferOptionsComprehensiveTests
{
    #region Default Property Values Tests

    [Fact]
    public void DefaultConstructor_InitializesWithExpectedDefaults()
    {
        // Act
        var options = new TransferOptions();

        // Assert
        _ = options.ChunkSize.Should().Be(64 * 1024 * 1024); // 64MB
        _ = options.EnableCompression.Should().BeFalse();
        _ = options.CompressionLevel.Should().Be(6);
        _ = options.EnableMemoryMapping.Should().BeFalse();
        _ = options.MemoryMappingThreshold.Should().Be(256 * 1024 * 1024); // 256MB
        _ = options.VerifyIntegrity.Should().BeFalse();
        _ = options.IntegritySampleRate.Should().Be(0.01); // 1%
        _ = options.IntegritySampleSize.Should().Be(1000);
        _ = options.MemoryOptions.Should().Be(MemoryOptions.None);
        _ = options.MaxRetryAttempts.Should().Be(3);
        _ = options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        _ = options.UseExponentialBackoff.Should().BeTrue();
        _ = options.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        _ = options.PinMemory.Should().BeFalse();
        _ = options.UseAsyncIO.Should().BeTrue();
        _ = options.BufferPoolSize.Should().Be(10);
        _ = options.EnableProgressReporting.Should().BeFalse();
        _ = options.ProgressReportInterval.Should().Be(TimeSpan.FromMilliseconds(100));
        _ = options.Priority.Should().Be(5);
        _ = options.OptimizeForThroughput.Should().BeTrue();
    }

    #endregion

    #region Property Setter Tests

    [Fact]
    public void ChunkSize_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();
        const int newSize = 128 * 1024 * 1024;

        // Act
        options.ChunkSize = newSize;

        // Assert
        _ = options.ChunkSize.Should().Be(newSize);
    }

    [Fact]
    public void EnableCompression_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            EnableCompression = true
        };

        // Assert
        _ = options.EnableCompression.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void CompressionLevel_CanBeSet(int level)
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            CompressionLevel = level
        };

        // Assert
        _ = options.CompressionLevel.Should().Be(level);
    }

    [Fact]
    public void EnableMemoryMapping_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            EnableMemoryMapping = true
        };

        // Assert
        _ = options.EnableMemoryMapping.Should().BeTrue();
    }

    [Fact]
    public void MemoryMappingThreshold_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();
        const long newThreshold = 512L * 1024 * 1024;

        // Act
        options.MemoryMappingThreshold = newThreshold;

        // Assert
        _ = options.MemoryMappingThreshold.Should().Be(newThreshold);
    }

    [Fact]
    public void VerifyIntegrity_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            VerifyIntegrity = true
        };

        // Assert
        _ = options.VerifyIntegrity.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.05)]
    [InlineData(0.10)]
    [InlineData(1.0)]
    public void IntegritySampleRate_CanBeSet(double rate)
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            IntegritySampleRate = rate
        };

        // Assert
        _ = options.IntegritySampleRate.Should().Be(rate);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void IntegritySampleSize_CanBeSet(int size)
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            IntegritySampleSize = size
        };

        // Assert
        _ = options.IntegritySampleSize.Should().Be(size);
    }

    [Fact]
    public void MemoryOptions_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            MemoryOptions = MemoryOptions.Pinned
        };

        // Assert
        _ = options.MemoryOptions.Should().Be(MemoryOptions.Pinned);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void MaxRetryAttempts_CanBeSet(int attempts)
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            MaxRetryAttempts = attempts
        };

        // Assert
        _ = options.MaxRetryAttempts.Should().Be(attempts);
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();
        var newDelay = TimeSpan.FromSeconds(2);

        // Act
        options.RetryDelay = newDelay;

        // Assert
        _ = options.RetryDelay.Should().Be(newDelay);
    }

    [Fact]
    public void UseExponentialBackoff_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            UseExponentialBackoff = false
        };

        // Assert
        _ = options.UseExponentialBackoff.Should().BeFalse();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();
        var newTimeout = TimeSpan.FromMinutes(10);

        // Act
        options.Timeout = newTimeout;

        // Assert
        _ = options.Timeout.Should().Be(newTimeout);
    }

    [Fact]
    public void PinMemory_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            PinMemory = true
        };

        // Assert
        _ = options.PinMemory.Should().BeTrue();
    }

    [Fact]
    public void UseAsyncIO_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            UseAsyncIO = false
        };

        // Assert
        _ = options.UseAsyncIO.Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void BufferPoolSize_CanBeSet(int size)
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            BufferPoolSize = size
        };

        // Assert
        _ = options.BufferPoolSize.Should().Be(size);
    }

    [Fact]
    public void EnableProgressReporting_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            EnableProgressReporting = true
        };

        // Assert
        _ = options.EnableProgressReporting.Should().BeTrue();
    }

    [Fact]
    public void ProgressReportInterval_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();
        var newInterval = TimeSpan.FromMilliseconds(50);

        // Act
        options.ProgressReportInterval = newInterval;

        // Assert
        _ = options.ProgressReportInterval.Should().Be(newInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void Priority_CanBeSet(int priority)
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            Priority = priority
        };

        // Assert
        _ = options.Priority.Should().Be(priority);
    }

    [Fact]
    public void OptimizeForThroughput_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions
        {
            // Act
            OptimizeForThroughput = false
        };

        // Assert
        _ = options.OptimizeForThroughput.Should().BeFalse();
    }

    #endregion

    #region Static Preset Tests

    [Fact]
    public void Default_ReturnsNewInstance()
    {
        // Act
        var options = TransferOptions.Default;

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ChunkSize.Should().Be(64 * 1024 * 1024);
        _ = options.EnableCompression.Should().BeFalse();
        _ = options.OptimizeForThroughput.Should().BeTrue();
    }

    [Fact]
    public void SmallTransfer_HasCorrectSettings()
    {
        // Act
        var options = TransferOptions.SmallTransfer;

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ChunkSize.Should().Be(64 * 1024); // 64KB
        _ = options.EnableCompression.Should().BeFalse();
        _ = options.EnableMemoryMapping.Should().BeFalse();
        _ = options.PinMemory.Should().BeTrue();
        _ = options.OptimizeForThroughput.Should().BeFalse();
    }

    [Fact]
    public void LargeTransfer_HasCorrectSettings()
    {
        // Act
        var options = TransferOptions.LargeTransfer;

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ChunkSize.Should().Be(256 * 1024 * 1024); // 256MB
        _ = options.EnableCompression.Should().BeTrue();
        _ = options.EnableMemoryMapping.Should().BeTrue();
        _ = options.VerifyIntegrity.Should().BeTrue();
        _ = options.OptimizeForThroughput.Should().BeTrue();
    }

    [Fact]
    public void Streaming_HasCorrectSettings()
    {
        // Act
        var options = TransferOptions.Streaming;

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.ChunkSize.Should().Be(4 * 1024 * 1024); // 4MB
        _ = options.EnableCompression.Should().BeFalse();
        _ = options.UseAsyncIO.Should().BeTrue();
        _ = options.BufferPoolSize.Should().Be(20);
        _ = options.OptimizeForThroughput.Should().BeTrue();
    }

    [Fact]
    public void Default_MultipleCallsReturnDifferentInstances()
    {
        // Act
        var options1 = TransferOptions.Default;
        var options2 = TransferOptions.Default;

        // Assert
        _ = options1.Should().NotBeSameAs(options2);
    }

    #endregion

    #region Clone Method Tests

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new TransferOptions
        {
            ChunkSize = 128 * 1024 * 1024,
            EnableCompression = true,
            CompressionLevel = 8,
            VerifyIntegrity = true,
            PinMemory = true
        };

        // Act
        var cloned = original.Clone();

        // Assert
        _ = cloned.Should().NotBeSameAs(original);
        _ = cloned.ChunkSize.Should().Be(original.ChunkSize);
        _ = cloned.EnableCompression.Should().Be(original.EnableCompression);
        _ = cloned.CompressionLevel.Should().Be(original.CompressionLevel);
        _ = cloned.VerifyIntegrity.Should().Be(original.VerifyIntegrity);
        _ = cloned.PinMemory.Should().Be(original.PinMemory);
    }

    [Fact]
    public void Clone_ChangesToOriginalDoNotAffectClone()
    {
        // Arrange
        var original = new TransferOptions { ChunkSize = 64 * 1024 * 1024 };
        var cloned = original.Clone();

        // Act
        original.ChunkSize = 128 * 1024 * 1024;
        original.EnableCompression = true;

        // Assert
        _ = cloned.ChunkSize.Should().Be(64 * 1024 * 1024);
        _ = cloned.EnableCompression.Should().BeFalse();
    }

    [Fact]
    public void Clone_ChangesToCloneDoNotAffectOriginal()
    {
        // Arrange
        var original = new TransferOptions { ChunkSize = 64 * 1024 * 1024 };
        var cloned = original.Clone();

        // Act
        cloned.ChunkSize = 128 * 1024 * 1024;
        cloned.EnableCompression = true;

        // Assert
        _ = original.ChunkSize.Should().Be(64 * 1024 * 1024);
        _ = original.EnableCompression.Should().BeFalse();
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        // Arrange
        var original = new TransferOptions
        {
            ChunkSize = 32 * 1024 * 1024,
            EnableCompression = true,
            CompressionLevel = 7,
            EnableMemoryMapping = true,
            MemoryMappingThreshold = 100 * 1024 * 1024,
            VerifyIntegrity = true,
            IntegritySampleRate = 0.05,
            IntegritySampleSize = 2000,
            MemoryOptions = MemoryOptions.Pinned,
            MaxRetryAttempts = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            UseExponentialBackoff = false,
            Timeout = TimeSpan.FromMinutes(10),
            PinMemory = true,
            UseAsyncIO = false,
            BufferPoolSize = 15,
            EnableProgressReporting = true,
            ProgressReportInterval = TimeSpan.FromMilliseconds(200),
            Priority = 8,
            OptimizeForThroughput = false
        };

        // Act
        var cloned = original.Clone();

        // Assert
        _ = cloned.ChunkSize.Should().Be(original.ChunkSize);
        _ = cloned.EnableCompression.Should().Be(original.EnableCompression);
        _ = cloned.CompressionLevel.Should().Be(original.CompressionLevel);
        _ = cloned.EnableMemoryMapping.Should().Be(original.EnableMemoryMapping);
        _ = cloned.MemoryMappingThreshold.Should().Be(original.MemoryMappingThreshold);
        _ = cloned.VerifyIntegrity.Should().Be(original.VerifyIntegrity);
        _ = cloned.IntegritySampleRate.Should().Be(original.IntegritySampleRate);
        _ = cloned.IntegritySampleSize.Should().Be(original.IntegritySampleSize);
        _ = cloned.MemoryOptions.Should().Be(original.MemoryOptions);
        _ = cloned.MaxRetryAttempts.Should().Be(original.MaxRetryAttempts);
        _ = cloned.RetryDelay.Should().Be(original.RetryDelay);
        _ = cloned.UseExponentialBackoff.Should().Be(original.UseExponentialBackoff);
        _ = cloned.Timeout.Should().Be(original.Timeout);
        _ = cloned.PinMemory.Should().Be(original.PinMemory);
        _ = cloned.UseAsyncIO.Should().Be(original.UseAsyncIO);
        _ = cloned.BufferPoolSize.Should().Be(original.BufferPoolSize);
        _ = cloned.EnableProgressReporting.Should().Be(original.EnableProgressReporting);
        _ = cloned.ProgressReportInterval.Should().Be(original.ProgressReportInterval);
        _ = cloned.Priority.Should().Be(original.Priority);
        _ = cloned.OptimizeForThroughput.Should().Be(original.OptimizeForThroughput);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Options_ForSmallFile_ConfiguredCorrectly()
    {
        // Arrange & Act
        var options = new TransferOptions
        {
            ChunkSize = 64 * 1024, // 64KB
            EnableCompression = false,
            PinMemory = true,
            OptimizeForThroughput = false
        };

        // Assert
        _ = options.ChunkSize.Should().BeLessThan(1024 * 1024); // Less than 1MB
        _ = options.EnableCompression.Should().BeFalse();
        _ = options.PinMemory.Should().BeTrue();
    }

    [Fact]
    public void Options_ForLargeFile_ConfiguredCorrectly()
    {
        // Arrange & Act
        var options = new TransferOptions
        {
            ChunkSize = 256 * 1024 * 1024, // 256MB
            EnableCompression = true,
            EnableMemoryMapping = true,
            VerifyIntegrity = true
        };

        // Assert
        _ = options.ChunkSize.Should().BeGreaterThan(100 * 1024 * 1024); // Greater than 100MB
        _ = options.EnableCompression.Should().BeTrue();
        _ = options.EnableMemoryMapping.Should().BeTrue();
        _ = options.VerifyIntegrity.Should().BeTrue();
    }

    [Fact]
    public void Options_ForStreaming_ConfiguredCorrectly()
    {
        // Arrange & Act
        var options = new TransferOptions
        {
            ChunkSize = 4 * 1024 * 1024, // 4MB
            UseAsyncIO = true,
            BufferPoolSize = 20,
            EnableProgressReporting = true
        };

        // Assert
        _ = options.ChunkSize.Should().BeInRange(1 * 1024 * 1024, 10 * 1024 * 1024);
        _ = options.UseAsyncIO.Should().BeTrue();
        _ = options.BufferPoolSize.Should().BeGreaterThan(10);
    }

    #endregion
}
