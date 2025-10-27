// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory.Types;
using FluentAssertions;
using Xunit;

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
        options.ChunkSize.Should().Be(64 * 1024 * 1024); // 64MB
        options.EnableCompression.Should().BeFalse();
        options.CompressionLevel.Should().Be(6);
        options.EnableMemoryMapping.Should().BeFalse();
        options.MemoryMappingThreshold.Should().Be(256 * 1024 * 1024); // 256MB
        options.VerifyIntegrity.Should().BeFalse();
        options.IntegritySampleRate.Should().Be(0.01); // 1%
        options.IntegritySampleSize.Should().Be(1000);
        options.MemoryOptions.Should().Be(MemoryOptions.None);
        options.MaxRetryAttempts.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.UseExponentialBackoff.Should().BeTrue();
        options.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        options.PinMemory.Should().BeFalse();
        options.UseAsyncIO.Should().BeTrue();
        options.BufferPoolSize.Should().Be(10);
        options.EnableProgressReporting.Should().BeFalse();
        options.ProgressReportInterval.Should().Be(TimeSpan.FromMilliseconds(100));
        options.Priority.Should().Be(5);
        options.OptimizeForThroughput.Should().BeTrue();
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
        options.ChunkSize.Should().Be(newSize);
    }

    [Fact]
    public void EnableCompression_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.EnableCompression = true;

        // Assert
        options.EnableCompression.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void CompressionLevel_CanBeSet(int level)
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.CompressionLevel = level;

        // Assert
        options.CompressionLevel.Should().Be(level);
    }

    [Fact]
    public void EnableMemoryMapping_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.EnableMemoryMapping = true;

        // Assert
        options.EnableMemoryMapping.Should().BeTrue();
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
        options.MemoryMappingThreshold.Should().Be(newThreshold);
    }

    [Fact]
    public void VerifyIntegrity_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.VerifyIntegrity = true;

        // Assert
        options.VerifyIntegrity.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.05)]
    [InlineData(0.10)]
    [InlineData(1.0)]
    public void IntegritySampleRate_CanBeSet(double rate)
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.IntegritySampleRate = rate;

        // Assert
        options.IntegritySampleRate.Should().Be(rate);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void IntegritySampleSize_CanBeSet(int size)
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.IntegritySampleSize = size;

        // Assert
        options.IntegritySampleSize.Should().Be(size);
    }

    [Fact]
    public void MemoryOptions_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.MemoryOptions = MemoryOptions.Pinned;

        // Assert
        options.MemoryOptions.Should().Be(MemoryOptions.Pinned);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void MaxRetryAttempts_CanBeSet(int attempts)
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.MaxRetryAttempts = attempts;

        // Assert
        options.MaxRetryAttempts.Should().Be(attempts);
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
        options.RetryDelay.Should().Be(newDelay);
    }

    [Fact]
    public void UseExponentialBackoff_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.UseExponentialBackoff = false;

        // Assert
        options.UseExponentialBackoff.Should().BeFalse();
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
        options.Timeout.Should().Be(newTimeout);
    }

    [Fact]
    public void PinMemory_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.PinMemory = true;

        // Assert
        options.PinMemory.Should().BeTrue();
    }

    [Fact]
    public void UseAsyncIO_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.UseAsyncIO = false;

        // Assert
        options.UseAsyncIO.Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void BufferPoolSize_CanBeSet(int size)
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.BufferPoolSize = size;

        // Assert
        options.BufferPoolSize.Should().Be(size);
    }

    [Fact]
    public void EnableProgressReporting_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.EnableProgressReporting = true;

        // Assert
        options.EnableProgressReporting.Should().BeTrue();
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
        options.ProgressReportInterval.Should().Be(newInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void Priority_CanBeSet(int priority)
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.Priority = priority;

        // Assert
        options.Priority.Should().Be(priority);
    }

    [Fact]
    public void OptimizeForThroughput_CanBeSet()
    {
        // Arrange
        var options = new TransferOptions();

        // Act
        options.OptimizeForThroughput = false;

        // Assert
        options.OptimizeForThroughput.Should().BeFalse();
    }

    #endregion

    #region Static Preset Tests

    [Fact]
    public void Default_ReturnsNewInstance()
    {
        // Act
        var options = TransferOptions.Default;

        // Assert
        options.Should().NotBeNull();
        options.ChunkSize.Should().Be(64 * 1024 * 1024);
        options.EnableCompression.Should().BeFalse();
        options.OptimizeForThroughput.Should().BeTrue();
    }

    [Fact]
    public void SmallTransfer_HasCorrectSettings()
    {
        // Act
        var options = TransferOptions.SmallTransfer;

        // Assert
        options.Should().NotBeNull();
        options.ChunkSize.Should().Be(64 * 1024); // 64KB
        options.EnableCompression.Should().BeFalse();
        options.EnableMemoryMapping.Should().BeFalse();
        options.PinMemory.Should().BeTrue();
        options.OptimizeForThroughput.Should().BeFalse();
    }

    [Fact]
    public void LargeTransfer_HasCorrectSettings()
    {
        // Act
        var options = TransferOptions.LargeTransfer;

        // Assert
        options.Should().NotBeNull();
        options.ChunkSize.Should().Be(256 * 1024 * 1024); // 256MB
        options.EnableCompression.Should().BeTrue();
        options.EnableMemoryMapping.Should().BeTrue();
        options.VerifyIntegrity.Should().BeTrue();
        options.OptimizeForThroughput.Should().BeTrue();
    }

    [Fact]
    public void Streaming_HasCorrectSettings()
    {
        // Act
        var options = TransferOptions.Streaming;

        // Assert
        options.Should().NotBeNull();
        options.ChunkSize.Should().Be(4 * 1024 * 1024); // 4MB
        options.EnableCompression.Should().BeFalse();
        options.UseAsyncIO.Should().BeTrue();
        options.BufferPoolSize.Should().Be(20);
        options.OptimizeForThroughput.Should().BeTrue();
    }

    [Fact]
    public void Default_MultipleCallsReturnDifferentInstances()
    {
        // Act
        var options1 = TransferOptions.Default;
        var options2 = TransferOptions.Default;

        // Assert
        options1.Should().NotBeSameAs(options2);
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
        cloned.Should().NotBeSameAs(original);
        cloned.ChunkSize.Should().Be(original.ChunkSize);
        cloned.EnableCompression.Should().Be(original.EnableCompression);
        cloned.CompressionLevel.Should().Be(original.CompressionLevel);
        cloned.VerifyIntegrity.Should().Be(original.VerifyIntegrity);
        cloned.PinMemory.Should().Be(original.PinMemory);
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
        cloned.ChunkSize.Should().Be(64 * 1024 * 1024);
        cloned.EnableCompression.Should().BeFalse();
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
        original.ChunkSize.Should().Be(64 * 1024 * 1024);
        original.EnableCompression.Should().BeFalse();
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
        cloned.ChunkSize.Should().Be(original.ChunkSize);
        cloned.EnableCompression.Should().Be(original.EnableCompression);
        cloned.CompressionLevel.Should().Be(original.CompressionLevel);
        cloned.EnableMemoryMapping.Should().Be(original.EnableMemoryMapping);
        cloned.MemoryMappingThreshold.Should().Be(original.MemoryMappingThreshold);
        cloned.VerifyIntegrity.Should().Be(original.VerifyIntegrity);
        cloned.IntegritySampleRate.Should().Be(original.IntegritySampleRate);
        cloned.IntegritySampleSize.Should().Be(original.IntegritySampleSize);
        cloned.MemoryOptions.Should().Be(original.MemoryOptions);
        cloned.MaxRetryAttempts.Should().Be(original.MaxRetryAttempts);
        cloned.RetryDelay.Should().Be(original.RetryDelay);
        cloned.UseExponentialBackoff.Should().Be(original.UseExponentialBackoff);
        cloned.Timeout.Should().Be(original.Timeout);
        cloned.PinMemory.Should().Be(original.PinMemory);
        cloned.UseAsyncIO.Should().Be(original.UseAsyncIO);
        cloned.BufferPoolSize.Should().Be(original.BufferPoolSize);
        cloned.EnableProgressReporting.Should().Be(original.EnableProgressReporting);
        cloned.ProgressReportInterval.Should().Be(original.ProgressReportInterval);
        cloned.Priority.Should().Be(original.Priority);
        cloned.OptimizeForThroughput.Should().Be(original.OptimizeForThroughput);
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
        options.ChunkSize.Should().BeLessThan(1024 * 1024); // Less than 1MB
        options.EnableCompression.Should().BeFalse();
        options.PinMemory.Should().BeTrue();
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
        options.ChunkSize.Should().BeGreaterThan(100 * 1024 * 1024); // Greater than 100MB
        options.EnableCompression.Should().BeTrue();
        options.EnableMemoryMapping.Should().BeTrue();
        options.VerifyIntegrity.Should().BeTrue();
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
        options.ChunkSize.Should().BeInRange(1 * 1024 * 1024, 10 * 1024 * 1024);
        options.UseAsyncIO.Should().BeTrue();
        options.BufferPoolSize.Should().BeGreaterThan(10);
    }

    #endregion
}
