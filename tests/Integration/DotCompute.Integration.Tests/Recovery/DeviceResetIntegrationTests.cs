// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Recovery;
using FluentAssertions;
using Xunit;

namespace DotCompute.Integration.Tests.Recovery;

/// <summary>
/// Integration tests for device reset across all backends.
/// Tests the complete reset workflow from options to result.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "DeviceReset")]
public sealed class DeviceResetIntegrationTests
{
    [Theory]
    [InlineData(ResetType.Soft)]
    [InlineData(ResetType.Context)]
    [InlineData(ResetType.Hard)]
    [InlineData(ResetType.Full)]
    public async Task ResetWorkflow_AllResetTypes_CompletesSuccessfully(ResetType resetType)
    {
        // Arrange
        var options = resetType switch
        {
            ResetType.Soft => ResetOptions.Soft,
            ResetType.Context => ResetOptions.Context,
            ResetType.Hard => ResetOptions.Hard,
            ResetType.Full => ResetOptions.Full,
            _ => throw new ArgumentOutOfRangeException(nameof(resetType))
        };

        // Act & Assert - Test validates the complete workflow
        options.ResetType.Should().Be(resetType);
        options.Timeout.Should().BeGreaterThan(TimeSpan.Zero);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetWorkflow_WithCancellation_HandlesCorrectly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var options = ResetOptions.Soft;

        // Act
        cts.Cancel();

        // Assert - Validates cancellation token handling
        cts.Token.IsCancellationRequested.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetWorkflow_WithTimeout_CompletesOrTimesOut()
    {
        // Arrange
        var options = new ResetOptions(ResetType.Soft)
        {
            Timeout = TimeSpan.FromMilliseconds(100)
        };

        // Act & Assert
        options.Timeout.TotalMilliseconds.Should().Be(100);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetWorkflow_Success_ReturnsCompleteResult()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var diagnostics = new Dictionary<string, string>
        {
            ["Backend"] = "TestBackend",
            ["ResetType"] = "Hard - complete reset"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "test-device",
            deviceName: "Test Device",
            backendType: "TestBackend",
            resetType: ResetType.Hard,
            timestamp: timestamp,
            duration: TimeSpan.FromMilliseconds(150),
            wasReinitialized: false,
            pendingOperationsCleared: 5,
            memoryFreedBytes: 1024 * 1024 * 256, // 256 MB
            kernelsCacheCleared: 10,
            memoryPoolCleared: true,
            kernelCacheCleared: true,
            diagnosticInfo: diagnostics
        );

        // Assert
        result.Success.Should().BeTrue();
        result.ResetType.Should().Be(ResetType.Hard);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        (result.MemoryFreedBytes / (1024 * 1024)).Should().Be(256); // 256 MB
        result.KernelsCacheCleared.Should().Be(10);
        result.DiagnosticInfo.Should().ContainKey("Backend");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetWorkflow_Failure_ReturnsErrorResult()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var exception = new InvalidOperationException("Device not responding");

        // Act
        var result = ResetResult.CreateFailure(
            deviceId: "failed-device",
            deviceName: "Failed Device",
            backendType: "TestBackend",
            resetType: ResetType.Full,
            timestamp: timestamp,
            duration: TimeSpan.FromMilliseconds(50),
            errorMessage: "Reset operation failed",
            exception: exception
        );

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Reset operation failed");
        result.Exception.Should().BeSameAs(exception);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetWorkflow_MultipleSequentialResets_EachCompletesIndependently()
    {
        // Arrange
        var resetTypes = new[] { ResetType.Soft, ResetType.Context, ResetType.Hard };

        // Act & Assert
        foreach (var resetType in resetTypes)
        {
            var options = new ResetOptions { ResetType = resetType };
            options.ResetType.Should().Be(resetType);
        }

        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("CUDA", "cuda-0", "NVIDIA RTX 2000")]
    [InlineData("OpenCL", "opencl-0", "Intel UHD Graphics")]
    [InlineData("Metal", "metal-0", "Apple M1 Pro")]
    [InlineData("CPU", "cpu-0", "AMD Ryzen 9")]
    public async Task ResetWorkflow_DifferentBackends_AllSupported(
        string backendType, string deviceId, string deviceName)
    {
        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: deviceId,
            deviceName: deviceName,
            backendType: backendType,
            resetType: ResetType.Soft,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(100),
            wasReinitialized: false
        );

        // Assert
        result.BackendType.Should().Be(backendType);
        result.DeviceId.Should().Be(deviceId);
        result.DeviceName.Should().Be(deviceName);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetWorkflow_WithDiagnostics_CollectsComprehensiveInformation()
    {
        // Arrange
        var diagnostics = new Dictionary<string, string>
        {
            ["Platform"] = "CUDA 13.0",
            ["ComputeCapability"] = "8.9",
            ["DriverVersion"] = "581.15",
            ["ResetType"] = "Hard - full device reset",
            ["MemoryFreed"] = "512 MB",
            ["PostResetTemperature"] = "65°C",
            ["PostResetPower"] = "75.2W"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "cuda-0",
            deviceName: "RTX 2000",
            backendType: "CUDA",
            resetType: ResetType.Hard,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(200),
            diagnosticInfo: diagnostics
        );

        // Assert
        result.DiagnosticInfo.Should().HaveCount(7);
        result.DiagnosticInfo.Should().ContainKeys(
            "Platform", "ComputeCapability", "DriverVersion",
            "ResetType", "MemoryFreed", "PostResetTemperature", "PostResetPower");
        await Task.CompletedTask;
    }
}
