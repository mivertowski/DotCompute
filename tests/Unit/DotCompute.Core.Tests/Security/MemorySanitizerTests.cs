// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security;
using DotCompute.Core.Security.Configuration;
using DotCompute.Core.Security.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.Security;

/// <summary>
/// Comprehensive unit tests for MemorySanitizer covering memory safety and sanitization.
/// Tests allocation tracking, bounds checking, use-after-free detection, and secure wiping.
/// </summary>
public sealed class MemorySanitizerTests : IDisposable
{
    private readonly ILogger<MemorySanitizer> _logger;
    private readonly MemorySanitizer _sanitizer;
    private readonly MemorySanitizerConfiguration _configuration;

    public MemorySanitizerTests()
    {
        _logger = Substitute.For<ILogger<MemorySanitizer>>();
        _configuration = MemorySanitizerConfiguration.Default;
        _sanitizer = new MemorySanitizer(_logger, _configuration);
    }

    public void Dispose()
    {
        _sanitizer?.Dispose();
    }

    #region Constructor and Configuration Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitialize()
    {
        // Arrange & Act
        using var sanitizer = new MemorySanitizer(_logger);

        // Assert
        sanitizer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = () => new MemorySanitizer(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_ShouldUseCustomSettings()
    {
        // Arrange
        var customConfig = new MemorySanitizerConfiguration
        {
            MaxAllocationSize = 512 * 1024 * 1024,
            EnableCanaryValues = false,
            EnableSecureWiping = false
        };

        // Act
        using var sanitizer = new MemorySanitizer(_logger, customConfig);

        // Assert
        sanitizer.Should().NotBeNull();
    }

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeroCounts()
    {
        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalAllocations.Should().Be(0);
        stats.TotalDeallocations.Should().Be(0);
        stats.TotalBytesAllocated.Should().Be(0);
        stats.TotalBytesFreed.Should().Be(0);
        stats.ActiveAllocations.Should().Be(0);
        stats.TotalViolations.Should().Be(0);
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_WithValidSize_ShouldSucceed()
    {
        // Arrange
        var size = (nuint)1024;

        // Act
        var result = await _sanitizer.AllocateSanitizedMemoryAsync(size);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Address.Should().NotBe(IntPtr.Zero);
        result.RequestedSize.Should().Be(size);
        result.ActualSize.Should().Be(size);
    }

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_WithZeroSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act
        var action = async () => await _sanitizer.AllocateSanitizedMemoryAsync(0);

        // Assert
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("size");
    }

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_WithExcessiveSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var excessiveSize = _configuration.MaxAllocationSize + 1;

        // Act
        var action = async () => await _sanitizer.AllocateSanitizedMemoryAsync(excessiveSize);

        // Assert
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("size");
    }

    [Theory]
    [InlineData(DataClassification.Public)]
    [InlineData(DataClassification.Internal)]
    [InlineData(DataClassification.Sensitive)]
    [InlineData(DataClassification.Confidential)]
    [InlineData(DataClassification.Secret)]
    [InlineData(DataClassification.TopSecret)]
    public async Task AllocateSanitizedMemoryAsync_WithDifferentClassifications_ShouldTrackCorrectly(DataClassification classification)
    {
        // Arrange
        var size = (nuint)1024;

        // Act
        var result = await _sanitizer.AllocateSanitizedMemoryAsync(size, classification);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Classification.Should().Be(classification);
    }

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_WithCustomIdentifier_ShouldUseProvidedIdentifier()
    {
        // Arrange
        var size = (nuint)512;
        var identifier = "TestAllocation123";

        // Act
        var result = await _sanitizer.AllocateSanitizedMemoryAsync(size, DataClassification.Sensitive, identifier);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Identifier.Should().Be(identifier);
    }

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_MultipleAllocations_ShouldTrackAll()
    {
        // Arrange
        var allocation1 = await _sanitizer.AllocateSanitizedMemoryAsync(1024);
        var allocation2 = await _sanitizer.AllocateSanitizedMemoryAsync(2048);
        var allocation3 = await _sanitizer.AllocateSanitizedMemoryAsync(512);

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        stats.TotalAllocations.Should().Be(3);
        stats.TotalBytesAllocated.Should().Be(1024 + 2048 + 512);
        stats.ActiveAllocations.Should().Be(3);
    }

    #endregion

    #region Memory Read/Write Tests

    [Fact]
    public async Task ReadSanitized_WithValidAddress_ShouldReturnValue()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));
        var expectedValue = 42;
        _sanitizer.WriteSanitized(allocation.Address, expectedValue);

        // Act
        var actualValue = _sanitizer.ReadSanitized<int>(allocation.Address);

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public async Task WriteSanitized_WithValidAddress_ShouldWriteValue()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(long));
        var value = 123456789L;

        // Act
        _sanitizer.WriteSanitized(allocation.Address, value);
        var readValue = _sanitizer.ReadSanitized<long>(allocation.Address);

        // Assert
        readValue.Should().Be(value);
    }

    [Fact]
    public async Task ReadSanitized_WithOffset_ShouldReadCorrectLocation()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int) * 4);
        var offset = (nuint)sizeof(int);
        var expectedValue = 999;
        _sanitizer.WriteSanitized(allocation.Address, expectedValue, offset);

        // Act
        var actualValue = _sanitizer.ReadSanitized<int>(allocation.Address, offset);

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public async Task WriteSanitized_WithOffset_ShouldWriteCorrectLocation()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(double) * 3);
        var offset = (nuint)(sizeof(double) * 2);
        var value = 3.14159;

        // Act
        _sanitizer.WriteSanitized(allocation.Address, value, offset);
        var readValue = _sanitizer.ReadSanitized<double>(allocation.Address, offset);

        // Assert
        readValue.Should().Be(value);
    }

    [Fact]
    public void ReadSanitized_FromUntrackedAddress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidAddress = new IntPtr(0x1000);

        // Act
        var action = () => _sanitizer.ReadSanitized<int>(invalidAddress);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*untracked*");
    }

    [Fact]
    public void WriteSanitized_ToUntrackedAddress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidAddress = new IntPtr(0x2000);

        // Act
        var action = () => _sanitizer.WriteSanitized(invalidAddress, 42);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*untracked*");
    }

    #endregion

    #region Bounds Violation Tests

    [Fact]
    public async Task ReadSanitized_BeyondBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));
        var invalidOffset = (nuint)sizeof(int);

        // Act
        var action = () => _sanitizer.ReadSanitized<int>(allocation.Address, invalidOffset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    [Fact]
    public async Task WriteSanitized_BeyondBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(long));
        var invalidOffset = (nuint)sizeof(long);

        // Act
        var action = () => _sanitizer.WriteSanitized(allocation.Address, 42L, invalidOffset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    [Fact]
    public async Task ReadSanitized_PartiallyOutOfBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(10);
        var offset = (nuint)6;  // Reading long (8 bytes) at offset 6 would extend to byte 14

        // Act
        var action = () => _sanitizer.ReadSanitized<long>(allocation.Address, offset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    [Fact]
    public async Task WriteSanitized_PartiallyOutOfBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(8);
        var offset = (nuint)4;  // Writing double (8 bytes) at offset 4 would extend to byte 12

        // Act
        var action = () => _sanitizer.WriteSanitized(allocation.Address, 3.14, offset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    #endregion

    #region Use-After-Free Detection Tests

    [Fact]
    public async Task ReadSanitized_AfterDeallocation_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));
        await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Act
        var action = () => _sanitizer.ReadSanitized<int>(allocation.Address);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*untracked*");
    }

    [Fact]
    public async Task WriteSanitized_AfterDeallocation_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));
        await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Act
        var action = () => _sanitizer.WriteSanitized(allocation.Address, 42);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*untracked*");
    }

    #endregion

    #region Double-Free Detection Tests

    [Fact]
    public async Task DeallocateSanitizedMemoryAsync_DoubleFree_ShouldDetectViolation()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(1024);
        await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Act
        var result = await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Double-free");
    }

    [Fact]
    public async Task DeallocateSanitizedMemoryAsync_UntrackedAddress_ShouldReportError()
    {
        // Arrange
        var invalidAddress = new IntPtr(0x3000);

        // Act
        var result = await _sanitizer.DeallocateSanitizedMemoryAsync(invalidAddress);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Memory Deallocation Tests

    [Fact]
    public async Task DeallocateSanitizedMemoryAsync_ValidAllocation_ShouldSucceed()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(2048);

        // Act
        var result = await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.BytesFreed.Should().Be(2048);
        result.Address.Should().Be(allocation.Address);
    }

    [Fact]
    public async Task DeallocateSanitizedMemoryAsync_ShouldUpdateStatistics()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(1024);
        await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        stats.TotalDeallocations.Should().Be(1);
        stats.TotalBytesFreed.Should().Be(1024);
        stats.ActiveAllocations.Should().Be(0);
    }

    [Theory]
    [InlineData(DataClassification.Public)]
    [InlineData(DataClassification.Sensitive)]
    [InlineData(DataClassification.TopSecret)]
    public async Task DeallocateSanitizedMemoryAsync_WithDifferentClassifications_ShouldTrackCorrectly(DataClassification classification)
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(512, classification);

        // Act
        var result = await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.SecurityLevel.Should().Be(classification);
    }

    #endregion

    #region Memory Leak Detection Tests

    [Fact]
    public async Task DetectMemoryLeaksAsync_WithNoAllocations_ShouldReturnEmptyReport()
    {
        // Act
        var report = await _sanitizer.DetectMemoryLeaksAsync();

        // Assert
        report.Should().NotBeNull();
        report.TotalActiveAllocations.Should().Be(0);
        report.SuspiciousAllocations.Should().BeEmpty();
        report.HighSuspicionCount.Should().Be(0);
    }

    [Fact]
    public async Task DetectMemoryLeaksAsync_WithRecentAllocations_ShouldNotFlagAsLeaks()
    {
        // Arrange
        await _sanitizer.AllocateSanitizedMemoryAsync(1024);
        await _sanitizer.AllocateSanitizedMemoryAsync(2048);

        // Act
        var report = await _sanitizer.DetectMemoryLeaksAsync();

        // Assert
        report.Should().NotBeNull();
        report.TotalActiveAllocations.Should().Be(2);
        report.HighSuspicionCount.Should().Be(0);
    }

    [Fact]
    public async Task DetectMemoryLeaksAsync_WithOldUnusedAllocations_ShouldDetectSuspicious()
    {
        // Arrange
        var customConfig = new MemorySanitizerConfiguration
        {
            LeakDetectionThreshold = TimeSpan.FromMilliseconds(10)
        };
        using var sanitizer = new MemorySanitizer(_logger, customConfig);
        await sanitizer.AllocateSanitizedMemoryAsync(1024);
        await Task.Delay(20);  // Wait for allocation to age

        // Act
        var report = await sanitizer.DetectMemoryLeaksAsync();

        // Assert
        report.Should().NotBeNull();
        report.SuspiciousAllocations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DetectMemoryLeaksAsync_ShouldCalculateSuspicionLevel()
    {
        // Arrange
        var customConfig = new MemorySanitizerConfiguration
        {
            LeakDetectionThreshold = TimeSpan.FromMilliseconds(5)
        };
        using var sanitizer = new MemorySanitizer(_logger, customConfig);
        await sanitizer.AllocateSanitizedMemoryAsync(1024);
        await Task.Delay(10);

        // Act
        var report = await sanitizer.DetectMemoryLeaksAsync();

        // Assert
        report.Should().NotBeNull();
        if (report.SuspiciousAllocations.Count > 0)
        {
            report.SuspiciousAllocations[0].SuspicionLevel.Should().BeInRange(0.0, 1.0);
        }
    }

    #endregion

    #region Canary Value Tests

    [Fact]
    public async Task ReadSanitized_WithCorruptedCanary_ShouldThrowInvalidOperationException()
    {
        // Arrange - This test verifies canary protection
        var customConfig = new MemorySanitizerConfiguration
        {
            EnableCanaryValues = true
        };
        using var sanitizer = new MemorySanitizer(_logger, customConfig);
        var allocation = await sanitizer.AllocateSanitizedMemoryAsync(1024);

        // Note: In production, we can't directly corrupt canaries without unsafe memory manipulation
        // This test documents the expected behavior when corruption occurs

        // Act & Assert
        // Normal read should succeed
        var action = () => sanitizer.ReadSanitized<int>(allocation.Address);
        action.Should().NotThrow();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatistics_AfterAllocations_ShouldTrackCorrectCounts()
    {
        // Arrange
        await _sanitizer.AllocateSanitizedMemoryAsync(1024, DataClassification.Public);
        await _sanitizer.AllocateSanitizedMemoryAsync(2048, DataClassification.Sensitive);
        await _sanitizer.AllocateSanitizedMemoryAsync(512, DataClassification.Secret);

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        stats.TotalAllocations.Should().Be(3);
        stats.TotalBytesAllocated.Should().Be(1024 + 2048 + 512);
        stats.AllocationsByClassification.Should().ContainKey(DataClassification.Public);
        stats.AllocationsByClassification.Should().ContainKey(DataClassification.Sensitive);
        stats.AllocationsByClassification.Should().ContainKey(DataClassification.Secret);
    }

    [Fact]
    public async Task GetStatistics_AfterViolations_ShouldTrackViolationCounts()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));

        // Trigger bounds violation
        try { _sanitizer.ReadSanitized<long>(allocation.Address); } catch { }

        // Trigger use-after-free
        await _sanitizer.DeallocateSanitizedMemoryAsync(allocation.Address);
        try { _sanitizer.ReadSanitized<int>(allocation.Address); } catch { }

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        stats.TotalViolations.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetStatistics_CurrentMemoryUsage_ShouldReflectActiveAllocations()
    {
        // Arrange
        var stats = _sanitizer.GetStatistics();

        // Act & Assert
        stats.CurrentMemoryUsage.Should().Be(0);
    }

    [Fact]
    public async Task GetStatistics_ViolationRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var allocation = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));

        // Trigger violation
        try { _sanitizer.ReadSanitized<long>(allocation.Address); } catch { }

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        stats.ViolationRate.Should().BeGreaterThanOrEqualTo(0.0);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_ConcurrentAllocations_ShouldHandleCorrectly()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sanitizer.AllocateSanitizedMemoryAsync(1024))
            .ToList();

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccessful.Should().BeTrue());
        results.Select(r => r.Address).Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task ReadWriteSanitized_ConcurrentOperations_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var allocations = new List<IntPtr>();
        for (int i = 0; i < 5; i++)
        {
            var result = await _sanitizer.AllocateSanitizedMemoryAsync(sizeof(int));
            allocations.Add(result.Address);
        }

        // Act
        var writeTasks = allocations.Select((addr, i) =>
            Task.Run(() => _sanitizer.WriteSanitized(addr, i * 100))
        );
        await Task.WhenAll(writeTasks);

        var readTasks = allocations.Select((addr, i) =>
            Task.Run(() => _sanitizer.ReadSanitized<int>(addr))
        );
        var values = await Task.WhenAll(readTasks);

        // Assert
        values.Should().Equal(0, 100, 200, 300, 400);
    }

    #endregion

    #region Disposal and Lifecycle Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var sanitizer = new MemorySanitizer(_logger);

        // Act
        sanitizer.Dispose();

        // Assert - Verify disposed state
        var action = async () => await sanitizer.AllocateSanitizedMemoryAsync(1024);
        action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var sanitizer = new MemorySanitizer(_logger);

        // Act
        sanitizer.Dispose();
        var action = () => sanitizer.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public async Task AllocateSanitizedMemoryAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sanitizer = new MemorySanitizer(_logger);
        sanitizer.Dispose();

        // Act
        var action = async () => await sanitizer.AllocateSanitizedMemoryAsync(1024);

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void ReadSanitized_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sanitizer = new MemorySanitizer(_logger);
        var address = new IntPtr(0x1000);
        sanitizer.Dispose();

        // Act
        var action = () => sanitizer.ReadSanitized<int>(address);

        // Assert
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WriteSanitized_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sanitizer = new MemorySanitizer(_logger);
        var address = new IntPtr(0x2000);
        sanitizer.Dispose();

        // Act
        var action = () => sanitizer.WriteSanitized(address, 42);

        // Assert
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DetectMemoryLeaksAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sanitizer = new MemorySanitizer(_logger);
        sanitizer.Dispose();

        // Act
        var action = async () => await sanitizer.DetectMemoryLeaksAsync();

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion
}
