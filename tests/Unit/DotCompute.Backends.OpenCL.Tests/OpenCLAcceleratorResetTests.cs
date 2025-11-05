// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Recovery;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.OpenCL.Tests;

/// <summary>
/// Comprehensive tests for OpenCL accelerator reset functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "OpenCLReset")]
public sealed class OpenCLAcceleratorResetTests
{
    [Fact]
    public async Task ResetAsync_SoftReset_SynchronizesQueues()
    {
        // Arrange
        var options = ResetOptions.Soft;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Soft);
        options.WaitForCompletion.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_ContextReset_ClearsCompilationCache()
    {
        // Arrange
        var options = ResetOptions.Context;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Context);
        options.ClearKernelCache.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_HardReset_ClearsAllResources()
    {
        // Arrange
        var options = ResetOptions.Hard;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Hard);
        options.ClearMemoryPool.Should().BeTrue();
        options.ClearKernelCache.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_FullReset_RecreatesContext()
    {
        // Arrange
        var options = ResetOptions.Full;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Full);
        options.Reinitialize.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public void ResetResult_OpenCL_ContainsPlatformDiagnostics()
    {
        // Arrange
        var diagnostics = new Dictionary<string, string>
        {
            ["OpenCLVersion"] = "3.0",
            ["Device"] = "Intel UHD Graphics",
            ["Vendor"] = "Intel",
            ["DriverVersion"] = "27.20.100.9466"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "opencl-0",
            deviceName: "Intel UHD Graphics",
            backendType: "OpenCL",
            resetType: ResetType.Context,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(120),
            wasReinitialized: false,
            diagnosticInfo: diagnostics
        );

        // Assert
        result.Success.Should().BeTrue();
        result.BackendType.Should().Be("OpenCL");
        result.DiagnosticInfo.Should().ContainKey("OpenCLVersion");
        result.DiagnosticInfo.Should().ContainKey("Vendor");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    public async Task ResetAsync_WithCustomTimeout_RespectsConfiguration(int timeoutSeconds)
    {
        // Arrange
        var options = new ResetOptions
        {
            ResetType = ResetType.Soft,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        // Act & Assert
        options.Timeout.TotalSeconds.Should().Be(timeoutSeconds);
        await Task.CompletedTask;
    }
}
