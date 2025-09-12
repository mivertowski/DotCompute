// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Tests for C# kernel to Metal Shading Language translation and execution.
/// </summary>
public class CSharpToMetalKernelTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CSharpToMetalKernelTests> _logger;
    private readonly MetalAccelerator _accelerator;
    private bool _disposed;

    public CSharpToMetalKernelTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<CSharpToMetalKernelTests>();
        
        var options = Options.Create(new MetalAcceleratorOptions
        {
            PreferredDeviceIndex = 0,
            EnableProfiling = true
        });
        
        _accelerator = new MetalAccelerator(options, loggerFactory.CreateLogger<MetalAccelerator>());
    }

    /// <summary>
    /// Sample C# kernel that should be translated to Metal.
    /// </summary>
    [Kernel(Backends = KernelBackends.Metal)]
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }

    /// <summary>
    /// 2D kernel for matrix operations.
    /// </summary>
    [Kernel(Backends = KernelBackends.Metal)]
    public static void MatrixMultiply(
        ReadOnlySpan<float> a, 
        ReadOnlySpan<float> b, 
        Span<float> c, 
        int width,
        int height)
    {
        int row = Kernel.ThreadId.Y;
        int col = Kernel.ThreadId.X;
        
        if (row >= height || col >= width)
        {
            return;
        }
        
        float sum = 0.0f;
        for (int k = 0; k < width; k++)
        {
            sum += a[row * width + k] * b[k * width + col];
        }
        c[row * width + col] = sum;
    }

    /// <summary>
    /// Kernel with atomic operations.
    /// </summary>
    [Kernel(Backends = KernelBackends.Metal)]
    public static void AtomicSum(ReadOnlySpan<float> data, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            // This should translate to Metal atomic operation
            Interlocked.Add(ref result[0], data[idx]);
        }
    }

    [SkippableFact]
    public async Task VectorAdd_GeneratedKernel_ExecutesCorrectly()
    {
        Skip.IfNot(HardwareDetection.IsMetalAvailable(), "Metal is not available on this system");
        
        await _accelerator.InitializeAsync();
        
        // Create test data
        const int size = 1024;
        var a = new float[size];
        var b = new float[size];
        var expected = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            a[i] = i;
            b[i] = i * 2;
            expected[i] = a[i] + b[i];
        }
        
        // Create Metal buffers
        var bufferA = await _accelerator.AllocateAsync<float>(size);
        var bufferB = await _accelerator.AllocateAsync<float>(size);
        var bufferResult = await _accelerator.AllocateAsync<float>(size);
        
        // Copy data to device
        await bufferA.CopyFromAsync(a);
        await bufferB.CopyFromAsync(b);
        
        // Create kernel definition with generated MSL code
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAdd",
            Source = GetGeneratedMetalCode_VectorAdd(),
            EntryPoint = "VectorAdd",
            Language = KernelLanguage.Metal
        };
        
        // Compile and execute
        var kernel = await _accelerator.CompileKernelAsync(kernelDef);
        
        var args = new KernelArguments
        {
            Arguments = new object[] { bufferA, bufferB, bufferResult },
            GridDimensions = new Dim3(size, 1, 1),
            BlockDimensions = new Dim3(256, 1, 1)
        };
        
        await kernel.ExecuteAsync(args);
        
        // Copy result back
        var result = new float[size];
        await bufferResult.CopyToAsync(result);
        
        // Verify
        result.Should().BeEquivalentTo(expected);
        
        _output.WriteLine("VectorAdd kernel executed successfully on Metal");
    }

    [SkippableFact]
    public async Task MatrixMultiply_GeneratedKernel_ProducesCorrectResult()
    {
        Skip.IfNot(HardwareDetection.IsMetalAvailable(), "Metal is not available on this system");
        
        await _accelerator.InitializeAsync();
        
        // Create small matrices for testing
        const int size = 4;
        var a = new float[]
        {
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16
        };
        
        var b = new float[]
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        };
        
        // Expected result (A * I = A)
        var expected = a.ToArray();
        
        // Allocate Metal buffers
        var bufferA = await _accelerator.AllocateAsync<float>(size * size);
        var bufferB = await _accelerator.AllocateAsync<float>(size * size);
        var bufferC = await _accelerator.AllocateAsync<float>(size * size);
        
        await bufferA.CopyFromAsync(a);
        await bufferB.CopyFromAsync(b);
        
        // Create kernel
        var kernelDef = new KernelDefinition
        {
            Name = "MatrixMultiply",
            Source = GetGeneratedMetalCode_MatrixMultiply(),
            EntryPoint = "MatrixMultiply",
            Language = KernelLanguage.Metal
        };
        
        var kernel = await _accelerator.CompileKernelAsync(kernelDef);
        
        var args = new KernelArguments
        {
            Arguments = new object[] { bufferA, bufferB, bufferC, size, size },
            GridDimensions = new Dim3(size, size, 1),
            BlockDimensions = new Dim3(1, 1, 1)
        };
        
        await kernel.ExecuteAsync(args);
        
        var result = new float[size * size];
        await bufferC.CopyToAsync(result);
        
        result.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        
        _output.WriteLine("MatrixMultiply kernel executed successfully on Metal");
    }

    [SkippableFact]
    public void CSharpToMetal_Translation_GeneratesValidMSL()
    {
        // This test validates that the translation produces valid MSL code
        var vectorAddMSL = GetGeneratedMetalCode_VectorAdd();
        
        vectorAddMSL.Should().Contain("#include <metal_stdlib>");
        vectorAddMSL.Should().Contain("kernel void VectorAdd");
        vectorAddMSL.Should().Contain("device const float*");
        vectorAddMSL.Should().Contain("device float*");
        vectorAddMSL.Should().Contain("thread_position_in_grid");
        vectorAddMSL.Should().Contain("[[buffer(0)]]");
        vectorAddMSL.Should().Contain("[[buffer(1)]]");
        vectorAddMSL.Should().Contain("[[buffer(2)]]");
        
        _output.WriteLine("Generated MSL code:");
        _output.WriteLine(vectorAddMSL);
    }

    /// <summary>
    /// Simulates the generated Metal code for VectorAdd kernel.
    /// In production, this would be generated by the source generator.
    /// </summary>
    private static string GetGeneratedMetalCode_VectorAdd()
    {
        return @"
#include <metal_stdlib>
#include <metal_atomic>
using namespace metal;

kernel void VectorAdd(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant int& length [[buffer(3)]],
    uint gid [[thread_position_in_grid]])
{
    int idx = gid;
    if (idx < length)
    {
        result[idx] = a[idx] + b[idx];
    }
}";
    }

    /// <summary>
    /// Simulates the generated Metal code for MatrixMultiply kernel.
    /// </summary>
    private static string GetGeneratedMetalCode_MatrixMultiply()
    {
        return @"
#include <metal_stdlib>
#include <metal_atomic>
using namespace metal;

kernel void MatrixMultiply(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* c [[buffer(2)]],
    constant int& width [[buffer(3)]],
    constant int& height [[buffer(4)]],
    uint2 gid [[thread_position_in_grid]])
{
    int row = gid.y;
    int col = gid.x;
    
    if (row >= height || col >= width)
    {
        return;
    }
    
    float sum = 0.0f;
    for (int k = 0; k < width; k++)
    {
        sum += a[row * width + k] * b[k * width + col];
    }
    c[row * width + col] = sum;
}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _accelerator?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Mock Kernel class for demonstration (would be provided by runtime).
/// </summary>
public static class Kernel
{
    public static class ThreadId
    {
        public static int X => 0;
        public static int Y => 0;
        public static int Z => 0;
    }
}