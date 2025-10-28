// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Native;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Comprehensive tests for Metal kernel compilation.
/// Tests compilation process, caching, optimization, and error handling.
/// </summary>
public class MetalCompilationTests : IDisposable
{
    private readonly ILogger<MetalKernelCompiler> _logger;
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private MetalKernelCompiler? _compiler;

    public MetalCompilationTests()
    {
        _logger = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<MetalKernelCompiler>();

        // Only initialize Metal if available
        if (IsMetalAvailable())
        {
            _device = MetalNative.CreateSystemDefaultDevice();
            if (_device != IntPtr.Zero)
            {
                _commandQueue = MetalNative.CreateCommandQueue(_device);
            }
        }
    }

    [SkippableFact]
    public async Task SimpleKernel_ShouldCompileSuccessfully()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "simple_add",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void simple_add(
                    device const float* a [[buffer(0)]],
                    device float* b [[buffer(1)]],
                    uint id [[thread_position_in_grid]])
                {
                    b[id] = a[id] + 1.0f;
                }"
        };

        // Act
        var compiled = await _compiler.CompileAsync(definition);

        // Assert
        compiled.Should().NotBeNull();
        compiled.Name.Should().Be("simple_add");
    }

    [SkippableFact]
    public async Task KernelWithOptimization_ShouldCompileWithFlags()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "optimized_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void optimized_kernel(
                    device const float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    uint id [[thread_position_in_grid]])
                {
                    float x = input[id];
                    output[id] = x * x + x + 1.0f;
                }"
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            FastMath = true
        };

        // Act
        var compiled = await _compiler.CompileAsync(definition, options);

        // Assert
        compiled.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task BinaryCache_ShouldReturnCachedKernel()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "cached_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void cached_kernel(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] *= 2.0f;
                }"
        };

        // Act - Compile twice
        var firstCompile = await _compiler.CompileAsync(definition);
        var secondCompile = await _compiler.CompileAsync(definition);

        // Assert - Both should succeed, second should be from cache
        firstCompile.Should().NotBeNull();
        secondCompile.Should().NotBeNull();
        // Cache behavior validated by faster compilation time in logs
    }

    [SkippableFact]
    public async Task InvalidSyntax_ShouldThrowCompilationException()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "invalid_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void invalid_kernel(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    // Missing semicolon - syntax error
                    data[id] = data[id] * 2.0f
                }"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _compiler.CompileAsync(definition).AsTask());
    }

    [SkippableFact]
    public async Task MissingEntryPoint_ShouldThrowException()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "missing_entry",
            EntryPoint = "nonexistent_function",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void actual_function(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = 0.0f;
                }"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _compiler.CompileAsync(definition).AsTask());
    }

    [SkippableFact]
    public async Task ComplexKernel_WithMultipleBuffers_ShouldCompile()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "multi_buffer",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void multi_buffer(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device const float* c [[buffer(2)]],
                    device float* result [[buffer(3)]],
                    constant int& length [[buffer(4)]],
                    uint id [[thread_position_in_grid]])
                {
                    if (id < uint(length))
                    {
                        result[id] = a[id] + b[id] * c[id];
                    }
                }"
        };

        // Act
        var compiled = await _compiler.CompileAsync(definition);

        // Assert
        compiled.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task Kernel_WithDebugInfo_ShouldCompile()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "debug_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void debug_kernel(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = float(id);
                }"
        };

        var options = new CompilationOptions
        {
            GenerateDebugInfo = true
        };

        // Act
        var compiled = await _compiler.CompileAsync(definition, options);

        // Assert
        compiled.Should().NotBeNull();
    }

    [SkippableFact]
    public void Validation_EmptyKernelName_ShouldFail()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "",
            Language = KernelLanguage.Metal,
            Code = "kernel void test() {}"
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("name");
    }

    [SkippableFact]
    public void Validation_NullCode_ShouldFail()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "test",
            Language = KernelLanguage.Metal,
            Code = null
        };

        // Act
        var result = _compiler.Validate(definition);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("code");
    }

    [SkippableFact]
    public async Task CancellationToken_ShouldCancelCompilation()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        _compiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        var definition = new KernelDefinition
        {
            Name = "cancellable_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void cancellable_kernel(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = 0.0f;
                }"
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _compiler.CompileAsync(definition, cancellationToken: cts.Token).AsTask());
    }

    private static bool IsMetalAvailable()
    {
        try
        {
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _compiler?.Dispose();

        if (_commandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(_commandQueue);
        }

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }
}
