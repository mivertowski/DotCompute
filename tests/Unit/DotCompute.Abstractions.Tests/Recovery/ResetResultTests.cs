// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Recovery;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests.Recovery;

/// <summary>
/// Comprehensive tests for ResetResult reporting.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Recovery")]
public sealed class ResetResultTests
{
    [Fact]
    public void CreateSuccess_WithMinimalData_CreatesValidResult()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "test-device",
            deviceName: "Test Device",
            backendType: "TestBackend",
            resetType: ResetType.Soft,
            timestamp: timestamp,
            duration: duration,
            wasReinitialized: false
        );

        // Assert
        result.Success.Should().BeTrue();
        result.DeviceId.Should().Be("test-device");
        result.DeviceName.Should().Be("Test Device");
        result.BackendType.Should().Be("TestBackend");
        result.ResetType.Should().Be(ResetType.Soft);
        result.Timestamp.Should().Be(timestamp);
        result.Duration.Should().Be(duration);
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void CreateSuccess_WithCompleteData_StoresAllInformation()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMilliseconds(250);
        var diagnostics = new Dictionary<string, string>
        {
            ["Platform"] = "CUDA",
            ["ComputeCapability"] = "8.9"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "cuda-0",
            deviceName: "RTX 2000",
            backendType: "CUDA",
            resetType: ResetType.Hard,
            timestamp: timestamp,
            duration: duration,
            wasReinitialized: true,
            pendingOperationsCleared: 5,
            memoryFreedBytes: 1024 * 1024 * 512, // 512 MB
            kernelsCacheCleared: 10,
            memoryPoolCleared: true,
            kernelCacheCleared: true,
            diagnosticInfo: diagnostics
        );

        // Assert
        result.Success.Should().BeTrue();
        result.WasReinitialized.Should().BeTrue();
        result.PendingOperationsCleared.Should().Be(5);
        result.MemoryFreedBytes.Should().Be(1024 * 1024 * 512);
        result.KernelsCacheCleared.Should().Be(10);
        result.MemoryPoolCleared.Should().BeTrue();
        result.KernelCacheCleared.Should().BeTrue();
        result.DiagnosticInfo.Should().ContainKey("Platform");
        result.DiagnosticInfo?["Platform"].Should().Be("CUDA");
    }

    [Fact]
    public void CreateFailure_WithMinimalData_CreatesValidResult()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        var result = ResetResult.CreateFailure(
            deviceId: "failed-device",
            deviceName: "Failed Device",
            backendType: "TestBackend",
            resetType: ResetType.Full,
            timestamp: timestamp,
            duration: duration,
            errorMessage: "Reset operation failed"
        );

        // Assert
        result.Success.Should().BeFalse();
        result.DeviceId.Should().Be("failed-device");
        result.ErrorMessage.Should().Be("Reset operation failed");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_WithException_StoresException()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMilliseconds(25);
        var exception = new InvalidOperationException("Device not available");

        // Act
        var result = ResetResult.CreateFailure(
            deviceId: "error-device",
            deviceName: "Error Device",
            backendType: "TestBackend",
            resetType: ResetType.Hard,
            timestamp: timestamp,
            duration: duration,
            errorMessage: "Operation failed",
            exception: exception
        );

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Operation failed");
        result.Exception.Should().BeSameAs(exception);
    }

    [Theory]
    [InlineData(ResetType.Soft)]
    [InlineData(ResetType.Context)]
    [InlineData(ResetType.Hard)]
    [InlineData(ResetType.Full)]
    public void CreateSuccess_WithDifferentResetTypes_StoresCorrectType(ResetType resetType)
    {
        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "test",
            deviceName: "Test",
            backendType: "Test",
            resetType: resetType,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(100),
            wasReinitialized: false
        );

        // Assert
        result.ResetType.Should().Be(resetType);
    }

    [Fact]
    public void MemoryFreedBytes_StoresCorrectly()
    {
        // Arrange
        var result = ResetResult.CreateSuccess(
            deviceId: "test",
            deviceName: "Test",
            backendType: "Test",
            resetType: ResetType.Hard,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(100),
            wasReinitialized: false,
            memoryFreedBytes: 1024 * 1024 * 256 // 256 MB
        );

        // Act
        var memoryMB = result.MemoryFreedBytes / (1024 * 1024);

        // Assert
        memoryMB.Should().Be(256);
    }

    [Fact]
    public void MemoryFreedBytes_WithZeroBytes_ReturnsZero()
    {
        // Arrange
        var result = ResetResult.CreateSuccess(
            deviceId: "test",
            deviceName: "Test",
            backendType: "Test",
            resetType: ResetType.Soft,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(100),
            wasReinitialized: false,
            memoryFreedBytes: 0
        );

        // Assert
        result.MemoryFreedBytes.Should().Be(0);
    }

    [Fact]
    public void DiagnosticInfo_CanBeNull()
    {
        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "test",
            deviceName: "Test",
            backendType: "Test",
            resetType: ResetType.Soft,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(100),
            wasReinitialized: false,
            diagnosticInfo: null
        );

        // Assert
        result.DiagnosticInfo.Should().BeNull();
    }

    [Fact]
    public void DiagnosticInfo_CanContainMultipleEntries()
    {
        // Arrange
        var diagnostics = new Dictionary<string, string>
        {
            ["Platform"] = "OpenCL",
            ["Device"] = "Intel Graphics",
            ["Vendor"] = "Intel",
            ["DriverVersion"] = "27.20.100.9466",
            ["ResetType"] = "Context - cache cleared"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "opencl-0",
            deviceName: "Intel UHD Graphics",
            backendType: "OpenCL",
            resetType: ResetType.Context,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(180),
            wasReinitialized: false,
            diagnosticInfo: diagnostics
        );

        // Assert
        result.DiagnosticInfo.Should().HaveCount(5);
        result.DiagnosticInfo.Should().ContainKeys("Platform", "Device", "Vendor", "DriverVersion", "ResetType");
    }

    [Fact]
    public void Duration_IsAccurate()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromMilliseconds(456.789);

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "test",
            deviceName: "Test",
            backendType: "Test",
            resetType: ResetType.Soft,
            timestamp: DateTimeOffset.UtcNow,
            duration: expectedDuration,
            wasReinitialized: false
        );

        // Assert
        result.Duration.Should().Be(expectedDuration);
    }
}
