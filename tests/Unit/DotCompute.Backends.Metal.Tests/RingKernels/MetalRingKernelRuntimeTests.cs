// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalRingKernelRuntime.
/// </summary>
public sealed class MetalRingKernelRuntimeTests : IDisposable
{
    private readonly MetalRingKernelCompiler _compiler;
    private readonly MetalRingKernelRuntime _runtime;

    public MetalRingKernelRuntimeTests()
    {
        _compiler = new MetalRingKernelCompiler(NullLogger<MetalRingKernelCompiler>.Instance);
        _runtime = new MetalRingKernelRuntime(NullLogger<MetalRingKernelRuntime>.Instance, _compiler);
    }

    [Fact]
    public void Constructor_Should_Initialize_Successfully()
    {
        // Arrange & Act
        using var compiler = new MetalRingKernelCompiler(NullLogger<MetalRingKernelCompiler>.Instance);
        using var runtime = new MetalRingKernelRuntime(NullLogger<MetalRingKernelRuntime>.Instance, compiler);

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Compiler_Is_Null()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MetalRingKernelRuntime(NullLogger<MetalRingKernelRuntime>.Instance, null!));
    }

    [Fact]
    public async Task LaunchAsync_Should_Throw_When_KernelId_Is_Empty()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _runtime.LaunchAsync(string.Empty, 1, 256));
    }

    [Fact]
    public async Task LaunchAsync_Should_Throw_When_GridSize_Is_Zero()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _runtime.LaunchAsync("TestKernel", 0, 256));
    }

    [Fact]
    public async Task LaunchAsync_Should_Throw_When_BlockSize_Is_Zero()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _runtime.LaunchAsync("TestKernel", 1, 0));
    }

    public void Dispose()
    {
        _runtime?.Dispose();
        _compiler?.Dispose();
    }
}
