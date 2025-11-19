// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalRingKernelCompiler.
/// </summary>
public sealed class MetalRingKernelCompilerTests : IDisposable
{
    private readonly MetalRingKernelCompiler _compiler;

    public MetalRingKernelCompilerTests()
    {
        _compiler = new MetalRingKernelCompiler(NullLogger<MetalRingKernelCompiler>.Instance);
    }

    [Fact]
    public void Constructor_Should_Initialize_Successfully()
    {
        // Arrange & Act
        using var compiler = new MetalRingKernelCompiler(NullLogger<MetalRingKernelCompiler>.Instance);

        // Assert
        Assert.NotNull(compiler);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MetalRingKernelCompiler(null!));
    }

    public void Dispose()
    {
        _compiler?.Dispose();
    }
}
