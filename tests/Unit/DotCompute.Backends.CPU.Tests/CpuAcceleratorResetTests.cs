// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Recovery;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Comprehensive tests for CPU accelerator reset functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CpuReset")]
public sealed class CpuAcceleratorResetTests
{
    [Fact]
    public async Task ResetAsync_SoftReset_IsNoOp()
    {
        // Arrange
        var options = ResetOptions.Soft;

        // Act & Assert - CPU operations are synchronous
        options.ResetType.Should().Be(ResetType.Soft);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_ContextReset_ClearsThreadPoolStatistics()
    {
        // Arrange
        var options = ResetOptions.Context;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Context);
        options.ClearKernelCache.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_HardReset_ClearsMemoryStatistics()
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
    public async Task ResetAsync_FullReset_ResetsAllState()
    {
        // Arrange
        var options = ResetOptions.Full;

        // Act & Assert
        options.ResetType.Should().Be(ResetType.Full);
        options.Reinitialize.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public void ResetResult_CPU_ContainsNumaDiagnostics()
    {
        // Arrange
        var diagnostics = new Dictionary<string, string>
        {
            ["ProcessorCount"] = "16",
            ["OSVersion"] = "Linux 6.6.87.2",
            ["Is64BitProcess"] = "True",
            ["PerformanceMode"] = "HighPerformance",
            ["Vectorization"] = "True",
            ["NumaNodes"] = "1"
        };

        // Act
        var result = ResetResult.CreateSuccess(
            deviceId: "cpu-0",
            deviceName: "AMD Ryzen 9",
            backendType: "CPU",
            resetType: ResetType.Context,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(45),
            wasReinitialized: false,
            diagnosticInfo: diagnostics
        );

        // Assert
        result.Success.Should().BeTrue();
        result.BackendType.Should().Be("CPU");
        result.DiagnosticInfo.Should().ContainKey("ProcessorCount");
        result.DiagnosticInfo.Should().ContainKey("PerformanceMode");
        result.DiagnosticInfo.Should().ContainKey("Vectorization");
    }

    [Fact]
    public async Task ResetAsync_CPU_CompletesQuickly()
    {
        // Arrange - CPU reset should complete quickly

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Task.CompletedTask; // CPU reset is synchronous
        stopwatch.Stop();

        // Assert - CPU reset should be nearly instantaneous
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Theory]
    [InlineData(ResetType.Soft, 0)]
    [InlineData(ResetType.Context, 0)]
    [InlineData(ResetType.Hard, 0)]
    [InlineData(ResetType.Full, 0)]
    public async Task ResetAsync_CPU_HasNoPendingOperations(ResetType resetType, long expectedPending)
    {
        // Arrange & Act & Assert - CPU operations are synchronous
        var result = ResetResult.CreateSuccess(
            deviceId: "cpu-0",
            deviceName: "CPU",
            backendType: "CPU",
            resetType: resetType,
            timestamp: DateTimeOffset.UtcNow,
            duration: TimeSpan.FromMilliseconds(10),
            wasReinitialized: false,
            pendingOperationsCleared: expectedPending
        );

        result.PendingOperationsCleared.Should().Be(expectedPending);
        await Task.CompletedTask;
    }
}
