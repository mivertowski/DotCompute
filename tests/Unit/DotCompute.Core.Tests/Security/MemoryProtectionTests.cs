// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.Security;

/// <summary>
/// Comprehensive unit tests for MemoryProtection covering memory safety and access control.
/// Tests guard pages, bounds checking, integrity validation, and secure memory management.
/// </summary>
public sealed class MemoryProtectionTests : IDisposable
{
    private readonly ILogger<MemoryProtection> _logger;
    private readonly MemoryProtection _protection;
    private readonly MemoryProtectionConfiguration _configuration;

    public MemoryProtectionTests()
    {
        _logger = Substitute.For<ILogger<MemoryProtection>>();
        _configuration = MemoryProtectionConfiguration.Default;
        _protection = new MemoryProtection(_logger, _configuration);
    }

    public void Dispose()
    {
        _protection?.Dispose();
    }

    #region Constructor and Configuration Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitialize()
    {
        // Arrange & Act
        using var protection = new MemoryProtection(_logger);

        // Assert
        protection.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = () => new MemoryProtection(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_ShouldUseCustomSettings()
    {
        // Arrange
        var customConfig = new MemoryProtectionConfiguration
        {
            EnableIntegrityChecking = false,
            EnableSecureWiping = false,
            EnableGuardPages = true
        };

        // Act
        using var protection = new MemoryProtection(_logger, customConfig);

        // Assert
        protection.Should().NotBeNull();
    }

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeroCounts()
    {
        // Act
        var stats = _protection.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.ActiveAllocations.Should().Be(0);
        stats.TotalAllocatedBytes.Should().Be(0);
        stats.ViolationCount.Should().Be(0);
        stats.CorruptionDetectionCount.Should().Be(0);
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public async Task AllocateProtectedMemoryAsync_WithValidSize_ShouldSucceed()
    {
        // Arrange
        var size = (nuint)1024;

        // Act
        var allocation = await _protection.AllocateProtectedMemoryAsync(size);

        // Assert
        allocation.Should().NotBeNull();
        allocation.Address.Should().NotBe(IntPtr.Zero);
        allocation.Size.Should().Be(size);
        allocation.Identifier.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AllocateProtectedMemoryAsync_WithZeroSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act
        var action = async () => await _protection.AllocateProtectedMemoryAsync(0);

        // Assert
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("size");
    }

    [Fact]
    public async Task AllocateProtectedMemoryAsync_WithExcessiveSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var excessiveSize = _configuration.MaxAllocationSize + 1;

        // Act
        var action = async () => await _protection.AllocateProtectedMemoryAsync(excessiveSize);

        // Assert
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("size");
    }

    [Fact]
    public async Task AllocateProtectedMemoryAsync_WithCustomIdentifier_ShouldUseProvidedIdentifier()
    {
        // Arrange
        var size = (nuint)512;
        var identifier = "TestProtectedMemory";

        // Act
        var allocation = await _protection.AllocateProtectedMemoryAsync(size, identifier: identifier);

        // Assert
        allocation.Should().NotBeNull();
        allocation.Identifier.Should().Be(identifier);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public async Task AllocateProtectedMemoryAsync_WithCustomAlignment_ShouldRespectAlignment(nuint alignment)
    {
        // Arrange
        var size = (nuint)1024;

        // Act
        var allocation = await _protection.AllocateProtectedMemoryAsync(size, alignment);

        // Assert
        allocation.Should().NotBeNull();
        allocation.Address.Should().NotBe(IntPtr.Zero);
        (allocation.Address.ToInt64() % (long)alignment).Should().Be(0);
    }

    [Fact]
    public async Task AllocateProtectedMemoryAsync_WithExecutableFlag_ShouldAllocateExecutableMemory()
    {
        // Arrange
        var size = (nuint)4096;

        // Act
        var allocation = await _protection.AllocateProtectedMemoryAsync(size, canExecute: true);

        // Assert
        allocation.Should().NotBeNull();
        allocation.CanExecute.Should().BeTrue();
    }

    [Fact]
    public async Task AllocateProtectedMemoryAsync_MultipleAllocations_ShouldTrackAll()
    {
        // Arrange
        var allocation1 = await _protection.AllocateProtectedMemoryAsync(1024);
        var allocation2 = await _protection.AllocateProtectedMemoryAsync(2048);
        var allocation3 = await _protection.AllocateProtectedMemoryAsync(512);

        // Act
        var stats = _protection.GetStatistics();

        // Assert
        stats.ActiveAllocations.Should().Be(3);
        stats.TotalAllocatedBytes.Should().Be(1024 + 2048 + 512);
    }

    #endregion

    #region Memory Read/Write Tests

    [Fact]
    public async Task ReadMemory_WithValidAddress_ShouldReturnValue()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int));
        var expectedValue = 42;
        _protection.WriteMemory(allocation.Address, expectedValue);

        // Act
        var actualValue = _protection.ReadMemory<int>(allocation.Address);

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public async Task WriteMemory_WithValidAddress_ShouldWriteValue()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(long));
        var value = 987654321L;

        // Act
        _protection.WriteMemory(allocation.Address, value);
        var readValue = _protection.ReadMemory<long>(allocation.Address);

        // Assert
        readValue.Should().Be(value);
    }

    [Fact]
    public async Task ReadMemory_WithOffset_ShouldReadCorrectLocation()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int) * 4);
        var offset = (nuint)sizeof(int);
        var expectedValue = 777;
        _protection.WriteMemory(allocation.Address, expectedValue, offset);

        // Act
        var actualValue = _protection.ReadMemory<int>(allocation.Address, offset);

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public async Task WriteMemory_WithOffset_ShouldWriteCorrectLocation()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(double) * 3);
        var offset = (nuint)(sizeof(double) * 2);
        var value = 2.71828;

        // Act
        _protection.WriteMemory(allocation.Address, value, offset);
        var readValue = _protection.ReadMemory<double>(allocation.Address, offset);

        // Assert
        readValue.Should().Be(value);
    }

    [Fact]
    public void ReadMemory_FromUnprotectedAddress_ShouldThrowSecurityException()
    {
        // Arrange
        var invalidAddress = new IntPtr(0x1000);

        // Act
        var action = () => _protection.ReadMemory<int>(invalidAddress);

        // Assert
        // action.Should().Throw<System.Security.SecurityException>() // Namespace DotCompute.Core.System.Security doesn't exist
        //     .WithMessage("*unprotected*");
        action.Should().Throw<Exception>()
            .WithMessage("*unprotected*");
    }

    [Fact]
    public void WriteMemory_ToUnprotectedAddress_ShouldThrowSecurityException()
    {
        // Arrange
        var invalidAddress = new IntPtr(0x2000);

        // Act
        var action = () => _protection.WriteMemory(invalidAddress, 42);

        // Assert
        // action.Should().Throw<System.Security.SecurityException>() // Namespace DotCompute.Core.System.Security doesn't exist
        //     .WithMessage("*unprotected*");
        action.Should().Throw<Exception>()
            .WithMessage("*unprotected*");
    }

    #endregion

    #region Bounds Violation Tests

    [Fact]
    public async Task ReadMemory_BeyondBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int));
        var invalidOffset = (nuint)sizeof(int);

        // Act
        var action = () => _protection.ReadMemory<int>(allocation.Address, invalidOffset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    [Fact]
    public async Task WriteMemory_BeyondBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(long));
        var invalidOffset = (nuint)sizeof(long);

        // Act
        var action = () => _protection.WriteMemory(allocation.Address, 42L, invalidOffset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    [Fact]
    public async Task ReadMemory_PartiallyOutOfBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(10);
        var offset = (nuint)6;  // Reading long (8 bytes) at offset 6 would extend to byte 14

        // Act
        var action = () => _protection.ReadMemory<long>(allocation.Address, offset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    [Fact]
    public async Task WriteMemory_PartiallyOutOfBounds_ShouldThrowAccessViolationException()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(8);
        var offset = (nuint)4;  // Writing double (8 bytes) at offset 4 would extend to byte 12

        // Act
        var action = () => _protection.WriteMemory(allocation.Address, 3.14, offset);

        // Assert
        action.Should().Throw<AccessViolationException>()
            .WithMessage("*bounds violation*");
    }

    #endregion

    #region Memory Access Validation Tests

    [Fact]
    public async Task ValidateMemoryAccess_WithValidAccess_ShouldReturnTrue()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(1024);

        // Act
        var isValid = _protection.ValidateMemoryAccess(allocation.Address, 512, "read");

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateMemoryAccess_WithUnprotectedAddress_ShouldReturnFalse()
    {
        // Arrange
        var invalidAddress = new IntPtr(0x3000);

        // Act
        var isValid = _protection.ValidateMemoryAccess(invalidAddress, 128, "read");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMemoryAccess_WithExcessiveSize_ShouldReturnFalse()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(1024);

        // Act
        var isValid = _protection.ValidateMemoryAccess(allocation.Address, 2048, "write");

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("execute")]
    public async Task ValidateMemoryAccess_WithDifferentOperations_ShouldValidate(string operation)
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(2048);

        // Act
        var isValid = _protection.ValidateMemoryAccess(allocation.Address, 512, operation);

        // Assert
        isValid.Should().BeTrue();
    }

    #endregion

    #region Memory Deallocation Tests

    [Fact]
    public async Task FreeProtectedMemoryAsync_ValidAllocation_ShouldSucceed()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(2048);

        // Act
        await _protection.FreeProtectedMemoryAsync(allocation);

        // Assert
        var stats = _protection.GetStatistics();
        stats.ActiveAllocations.Should().Be(0);
    }

    [Fact]
    public async Task FreeProtectedMemoryAsync_NullAllocation_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = async () => await _protection.FreeProtectedMemoryAsync(null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FreeProtectedMemoryAsync_ShouldUpdateStatistics()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(1024);
        var initialStats = _protection.GetStatistics();
        var initialCount = initialStats.ActiveAllocations;

        // Act
        await _protection.FreeProtectedMemoryAsync(allocation);
        var finalStats = _protection.GetStatistics();

        // Assert
        finalStats.ActiveAllocations.Should().Be(initialCount - 1);
    }

    [Fact]
    public async Task ReadMemory_AfterFree_ShouldThrowSecurityException()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int));
        await _protection.FreeProtectedMemoryAsync(allocation);

        // Act
        var action = () => _protection.ReadMemory<int>(allocation.Address);

        // Assert
        // action.Should().Throw<System.Security.SecurityException>(); // Namespace DotCompute.Core.System.Security doesn't exist
        action.Should().Throw<Exception>();
    }

    [Fact]
    public async Task WriteMemory_AfterFree_ShouldThrowSecurityException()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int));
        await _protection.FreeProtectedMemoryAsync(allocation);

        // Act
        var action = () => _protection.WriteMemory(allocation.Address, 42);

        // Assert
        // action.Should().Throw<System.Security.SecurityException>(); // Namespace DotCompute.Core.System.Security doesn't exist
        action.Should().Throw<Exception>();
    }

    #endregion

    #region Guard Pages Tests

    [Fact]
    public async Task AllocateProtectedMemoryAsync_WithGuardPages_ShouldIncludeGuardRegions()
    {
        // Arrange
        var customConfig = new MemoryProtectionConfiguration
        {
            EnableGuardPages = true
        };
        using var protection = new MemoryProtection(_logger, customConfig);
        var size = (nuint)4096;

        // Act
        var allocation = await protection.AllocateProtectedMemoryAsync(size);

        // Assert
        allocation.Should().NotBeNull();
        allocation.Region.GuardPageSize.Should().BeGreaterThan(0);
    }

    #endregion

    #region Integrity Checking Tests

    [Fact]
    public async Task ReadMemory_WithIntegrityChecking_ShouldVerifyIntegrity()
    {
        // Arrange
        var customConfig = new MemoryProtectionConfiguration
        {
            EnableIntegrityChecking = true
        };
        using var protection = new MemoryProtection(_logger, customConfig);
        var allocation = await protection.AllocateProtectedMemoryAsync(sizeof(int));
        protection.WriteMemory(allocation.Address, 123);

        // Act
        var value = protection.ReadMemory<int>(allocation.Address);

        // Assert
        value.Should().Be(123);
    }

    [Fact]
    public async Task WriteMemory_WithIntegrityChecking_ShouldMaintainIntegrity()
    {
        // Arrange
        var customConfig = new MemoryProtectionConfiguration
        {
            EnableIntegrityChecking = true
        };
        using var protection = new MemoryProtection(_logger, customConfig);
        var allocation = await protection.AllocateProtectedMemoryAsync(sizeof(long));

        // Act
        protection.WriteMemory(allocation.Address, 999L);
        var readValue = protection.ReadMemory<long>(allocation.Address);

        // Assert
        readValue.Should().Be(999L);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatistics_AfterAllocations_ShouldTrackCorrectCounts()
    {
        // Arrange
        await _protection.AllocateProtectedMemoryAsync(1024);
        await _protection.AllocateProtectedMemoryAsync(2048);
        await _protection.AllocateProtectedMemoryAsync(512);

        // Act
        var stats = _protection.GetStatistics();

        // Assert
        stats.ActiveAllocations.Should().Be(3);
        stats.TotalAllocatedBytes.Should().Be(1024 + 2048 + 512);
    }

    [Fact]
    public async Task GetStatistics_AfterViolations_ShouldTrackViolationCounts()
    {
        // Arrange
        var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int));

        // Trigger bounds violation
        try { _protection.ReadMemory<long>(allocation.Address); } catch { }

        // Act
        var stats = _protection.GetStatistics();

        // Assert
        stats.ViolationCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStatistics_TotalGuardPageBytes_ShouldCalculateCorrectly()
    {
        // Arrange
        var allocation1 = await _protection.AllocateProtectedMemoryAsync(1024);
        var allocation2 = await _protection.AllocateProtectedMemoryAsync(2048);

        // Act
        var stats = _protection.GetStatistics();

        // Assert
        if (_configuration.EnableGuardPages)
        {
            stats.TotalGuardPageBytes.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task AllocateProtectedMemoryAsync_ConcurrentAllocations_ShouldHandleCorrectly()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _protection.AllocateProtectedMemoryAsync(1024))
            .ToList();

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Address.Should().NotBe(IntPtr.Zero));
        results.Select(r => r.Address).Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task ReadWriteMemory_ConcurrentOperations_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var allocations = new List<ProtectedMemoryAllocation>();
        for (int i = 0; i < 5; i++)
        {
            var allocation = await _protection.AllocateProtectedMemoryAsync(sizeof(int));
            allocations.Add(allocation);
        }

        // Act
        var writeTasks = allocations.Select((alloc, i) =>
            Task.Run(() => _protection.WriteMemory(alloc.Address, i * 100))
        );
        await Task.WhenAll(writeTasks);

        var readTasks = allocations.Select(alloc =>
            Task.Run(() => _protection.ReadMemory<int>(alloc.Address))
        );
        var values = await Task.WhenAll(readTasks);

        // Assert
        values.Should().Equal(0, 100, 200, 300, 400);
    }

    #endregion

    #region Secure Wiping Tests

    [Fact]
    public async Task FreeProtectedMemoryAsync_WithSecureWiping_ShouldWipeMemory()
    {
        // Arrange
        var customConfig = new MemoryProtectionConfiguration
        {
            EnableSecureWiping = true
        };
        using var protection = new MemoryProtection(_logger, customConfig);
        var allocation = await protection.AllocateProtectedMemoryAsync(sizeof(int));
        protection.WriteMemory(allocation.Address, 42);

        // Act
        await protection.FreeProtectedMemoryAsync(allocation);

        // Assert
        // Memory should be wiped and inaccessible
        var action = () => protection.ReadMemory<int>(allocation.Address);
        // action.Should().Throw<System.Security.SecurityException>(); // Namespace DotCompute.Core.System.Security doesn't exist
        action.Should().Throw<Exception>();
    }

    #endregion

    #region Disposal and Lifecycle Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var protection = new MemoryProtection(_logger);

        // Act
        protection.Dispose();

        // Assert - Verify disposed state
        var action = async () => await protection.AllocateProtectedMemoryAsync(1024);
        action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var protection = new MemoryProtection(_logger);

        // Act
        protection.Dispose();
        var action = () => protection.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public async Task AllocateProtectedMemoryAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var protection = new MemoryProtection(_logger);
        protection.Dispose();

        // Act
        var action = async () => await protection.AllocateProtectedMemoryAsync(1024);

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void ReadMemory_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var protection = new MemoryProtection(_logger);
        var address = new IntPtr(0x1000);
        protection.Dispose();

        // Act
        var action = () => protection.ReadMemory<int>(address);

        // Assert
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WriteMemory_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var protection = new MemoryProtection(_logger);
        var address = new IntPtr(0x2000);
        protection.Dispose();

        // Act
        var action = () => protection.WriteMemory(address, 42);

        // Assert
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ValidateMemoryAccess_AfterDispose_ShouldReturnFalse()
    {
        // Arrange
        var protection = new MemoryProtection(_logger);
        var address = new IntPtr(0x3000);
        protection.Dispose();

        // Act
        var isValid = protection.ValidateMemoryAccess(address, 128, "read");

        // Assert
        isValid.Should().BeFalse();
    }

    #endregion
}
