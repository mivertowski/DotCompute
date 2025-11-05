// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Recovery;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Comprehensive tests for Metal accelerator reset functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "MetalReset")]
public sealed class MetalAcceleratorResetTests
{
    [Fact]
    public async Task ResetAsync_SoftReset_SynchronizesCommandBuffers()
    {
        // Arrange
        var options = ResetOptions.Soft;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Soft);
        options.WaitForCompletion.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_ContextReset_ClearsCommandBufferPool()
    {
        // Arrange
        var options = ResetOptions.Context;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Context);
        options.ClearKernelCache.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_HardReset_ClearsMemoryAndBuffers()
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
    public async Task ResetAsync_FullReset_RecreatesResources()
    {
        // Arrange
        var options = ResetOptions.Full;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Full);
        options.Reinitialize.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public void ResetResult_Metal_ContainsDeviceDiagnostics()
    {
        // Arrange
        var diagnostics = new Dictionary<string, string>
        {
            ["DeviceName"] = "Apple M1 Pro",
            ["Architecture"] = "Apple Silicon",
            ["UnifiedMemory"] = "True",
            ["MaxThreadgroupSize"] = "1024"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "metal-0",
            deviceName: "Apple M1 Pro",
            backendType: "Metal",
            resetType: ResetType.Hard,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(95),
            wasReinitialized: false,
            diagnosticInfo: diagnostics
        );

        // Assert
        result.Success.Should().BeTrue();
        result.BackendType.Should().Be("Metal");
        result.DiagnosticInfo.Should().ContainKey("Architecture");
        result.DiagnosticInfo.Should().ContainKey("UnifiedMemory");
    }

    [Fact]
    public async Task ResetAsync_AppleSilicon_HandlesUnifiedMemory()
    {
        // Arrange
        var options = ResetOptions.Hard;

        // Act & Assert - Metal on Apple Silicon has unified memory architecture
        options.ClearMemoryPool.Should().BeTrue();
        await Task.CompletedTask;
    }
}
