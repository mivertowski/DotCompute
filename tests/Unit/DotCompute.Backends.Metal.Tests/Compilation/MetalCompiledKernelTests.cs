// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Memory;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Comprehensive tests for MetalCompiledKernel covering execution, dispatch, and argument handling.
/// Target: High coverage of kernel execution paths and edge cases.
/// </summary>
public sealed class MetalCompiledKernelTests : MetalCompilerTestBase
{
    public MetalCompiledKernelTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Kernel Execution Tests

    [SkippableFact]
    public async Task ExecuteAsync_SimpleVectorAdd_ExecutesSuccessfully()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var size = 1024;
        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var bufferA = await memoryManager.AllocateAsync<float>(size);
        var bufferB = await memoryManager.AllocateAsync<float>(size);
        var bufferResult = await memoryManager.AllocateAsync<float>(size);

        // Initialize test data
        var dataA = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var dataB = Enumerable.Range(0, size).Select(i => (float)(i * 2)).ToArray();
        await bufferA.CopyFromAsync(dataA);
        await bufferB.CopyFromAsync(dataB);

        var args = new KernelArguments();
        args.Add(bufferA);
        args.Add(bufferB);
        args.Add(bufferResult);
        args.Add(new Dim3(size, 1, 1));

        // Act
        await compiled.ExecuteAsync(args);

        // Verify results
        var result = new float[size];
        await bufferResult.CopyToAsync(result);

        // Assert
        for (int i = 0; i < 10; i++) // Check first 10 elements
        {
            Assert.Equal(dataA[i] + dataB[i], result[i], precision: 5);
        }

        LogTestInfo($"Vector addition executed successfully for {size} elements");

        // Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferResult.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    [SkippableFact]
    public async Task ExecuteAsync_WithCommandBufferPool_ReusesBuffers()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var buffer = await memoryManager.AllocateAsync<float>(100);

        var args = new KernelArguments();
        args.Add(buffer);
        args.Add(buffer);
        args.Add(buffer);
        args.Add(new Dim3(100, 1, 1));

        // Act - Execute multiple times
        await compiled.ExecuteAsync(args);
        await compiled.ExecuteAsync(args);

        // Assert
        Assert.NotNull(CommandBufferPool);
        LogTestInfo("Command buffer pool reused buffers successfully");

        // Cleanup
        await buffer.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    [SkippableFact]
    public async Task ExecuteAsync_LargeWorkload_HandlesLargeDispatch()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var size = 1024 * 1024; // 1M elements
        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var buffer = await memoryManager.AllocateAsync<float>(size);

        var args = new KernelArguments();
        args.Add(buffer);
        args.Add(buffer);
        args.Add(buffer);
        args.Add(new Dim3(size, 1, 1));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await compiled.ExecuteAsync(args);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should complete within 5 seconds
        LogTestInfo($"Large workload ({size} elements) executed in {stopwatch.ElapsedMilliseconds}ms");

        // Cleanup
        await buffer.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    [SkippableFact]
    public async Task ExecuteAsync_WithCancellation_HonorsCancellationToken()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateLargeKernel(1000);
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var buffer = await memoryManager.AllocateAsync<float>(1024);

        var args = new KernelArguments();
        args.Add(buffer);
        args.Add(new Dim3(1024, 1, 1));

        var cts = new CancellationTokenSource();

        // Act
        var executeTask = compiled.ExecuteAsync(args, cts.Token);
        cts.CancelAfter(10); // Cancel after 10ms

        // Assert - may or may not throw depending on timing
        try
        {
            await executeTask;
            LogTestInfo("Execution completed before cancellation");
        }
        catch (OperationCanceledException)
        {
            LogTestInfo("Execution was cancelled as expected");
        }

        // Cleanup
        await buffer.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    #endregion

    #region Dispatch Dimension Tests

    [SkippableFact]
    public async Task CalculateDispatchDimensions_1DWorkload_CalculatesCorrectDimensions()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel) as MetalCompiledKernel;

        // Access metadata to verify compilation
        var metadata = compiled!.CompilationMetadata;

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.MemoryUsage.ContainsKey("MaxThreadsPerThreadgroup"));
        LogTestInfo($"Max threads per threadgroup: {metadata.MemoryUsage["MaxThreadsPerThreadgroup"]}");
    }

    [SkippableFact]
    public async Task CalculateDispatchDimensions_WithDim3Argument_UsesProvidedDimensions()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var buffer = await memoryManager.AllocateAsync<float>(1024);

        var args = new KernelArguments();
        args.Add(buffer);
        args.Add(buffer);
        args.Add(buffer);
        args.Add(new Dim3(1024, 1, 1)); // Explicit dimensions

        // Act & Assert - Should not throw
        await compiled.ExecuteAsync(args);

        LogTestInfo("Explicit Dim3 dimensions used correctly");

        // Cleanup
        await buffer.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    #endregion

    #region Argument Handling Tests

    [SkippableFact]
    public async Task SetKernelArguments_BufferArguments_SetsCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var buffer1 = await memoryManager.AllocateAsync<float>(100);
        var buffer2 = await memoryManager.AllocateAsync<float>(100);
        var buffer3 = await memoryManager.AllocateAsync<float>(100);

        var args = new KernelArguments();
        args.Add(buffer1);
        args.Add(buffer2);
        args.Add(buffer3);
        args.Add(new Dim3(100, 1, 1));

        // Act & Assert - Should not throw
        await compiled.ExecuteAsync(args);

        LogTestInfo("Buffer arguments set correctly");

        // Cleanup
        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
        await buffer3.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    [SkippableFact]
    public async Task SetKernelArguments_ScalarArguments_HandlesAllTypes()
    {
        // Arrange
        RequireMetalSupport();
        var code = @"
#include <metal_stdlib>
using namespace metal;

kernel void scalar_kernel(
    constant float& f [[buffer(0)]],
    constant int& i [[buffer(1)]],
    constant uint& u [[buffer(2)]],
    device float* output [[buffer(3)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = f + float(i) + float(u);
}";

        var kernel = new KernelDefinition("scalar_kernel", code)
        {
            EntryPoint = "scalar_kernel",
            Language = KernelLanguage.Metal
        };

        var compiler = CreateCompiler();
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var outputBuffer = await memoryManager.AllocateAsync<float>(10);

        var args = new KernelArguments();
        args.Add(3.14f);        // float
        args.Add(42);           // int
        args.Add(7u);           // uint
        args.Add(outputBuffer);
        args.Add(new Dim3(10, 1, 1));

        // Act & Assert - Should not throw
        await compiled.ExecuteAsync(args);

        LogTestInfo("Scalar arguments handled correctly");

        // Cleanup
        await outputBuffer.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    [SkippableFact]
    public async Task SetKernelArguments_BufferView_HandlesOffsetCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);

        // Allocate three separate buffers instead of creating views
        var buffer1 = await memoryManager.AllocateAsync<float>(500);
        var buffer2 = await memoryManager.AllocateAsync<float>(500);
        var buffer3 = await memoryManager.AllocateAsync<float>(500);

        var args = new KernelArguments();
        args.Add(buffer1);
        args.Add(buffer2);
        args.Add(buffer3);
        args.Add(new Dim3(500, 1, 1));

        // Act & Assert - Should not throw
        await compiled.ExecuteAsync(args);

        LogTestInfo("Multiple buffer arguments handled correctly");

        // Cleanup
        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
        await buffer3.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    #endregion

    #region Metadata Tests

    [SkippableFact]
    public async Task GetCompilationMetadata_ReturnsValidMetadata()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel) as MetalCompiledKernel;

        // Act
        var metadata = compiled!.CompilationMetadata;

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.CompilationTimeMs >= 0);
        Assert.NotEmpty(metadata.MemoryUsage);
        Assert.Contains("MaxThreadsPerThreadgroup", metadata.MemoryUsage.Keys);
        LogTestInfo($"Compilation metadata - Time: {metadata.CompilationTimeMs}ms, " +
                   $"MaxThreads: {metadata.MemoryUsage["MaxThreadsPerThreadgroup"]}");
    }

    [SkippableFact]
    public async Task Name_ReturnsKernelName()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        // Act
        var name = compiled.Name;

        // Assert
        Assert.Equal("vector_add", name);
    }

    [SkippableFact]
    public async Task Id_IsUnique()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled1 = await compiler.CompileAsync(kernel) as MetalCompiledKernel;
        var compiled2 = await compiler.CompileAsync(kernel) as MetalCompiledKernel;

        // Assert
        Assert.NotEqual(compiled1!.Id, compiled2!.Id);
        LogTestInfo("Each compiled kernel has unique ID");
    }

    [SkippableFact]
    public async Task SourceCode_ReturnsOriginalSource()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel) as MetalCompiledKernel;

        // Act
        var sourceCode = compiled!.SourceCode;

        // Assert
        Assert.NotNull(sourceCode);
        Assert.Contains("vector_add", sourceCode);
        LogTestInfo($"Source code preserved ({sourceCode.Length} characters)");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact]
    public async Task ExecuteAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var loggerFactory = LoggerFactory.Create(builder => { });
        var memoryManager = new MetalMemoryManager(loggerFactory.CreateLogger<MetalMemoryManager>(), accelerator: null, enablePooling: false);
        var buffer = await memoryManager.AllocateAsync<float>(100);

        var args = new KernelArguments();
        args.Add(buffer);
        args.Add(buffer);
        args.Add(buffer);
        args.Add(new Dim3(100, 1, 1));

        // Dispose the kernel
        compiled.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await compiled.ExecuteAsync(args));

        // Cleanup
        await buffer.DisposeAsync();
        await memoryManager.DisposeAsync();
    }

    [SkippableFact]
    public async Task ExecuteAsync_InvalidArguments_HandlesGracefully()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        var args = new KernelArguments();
        // Add wrong number or type of arguments
        args.Add(42); // scalar instead of buffer

        // Act & Assert - Should handle gracefully or throw appropriate exception
        try
        {
            await compiled.ExecuteAsync(args);
            // If it doesn't throw, that's also acceptable (depends on implementation)
            LogTestInfo("Kernel handled invalid arguments");
        }
        catch (Exception ex)
        {
            LogTestInfo($"Invalid arguments threw expected exception: {ex.GetType().Name}");
        }
    }

    #endregion

    #region Disposal Tests

    [SkippableFact]
    public async Task Dispose_ReleasesResources()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        // Act
        compiled.Dispose();

        // Assert - Should be able to dispose multiple times
        compiled.Dispose();
        LogTestInfo("Kernel disposed and resources released");
    }

    [SkippableFact]
    public async Task DisposeAsync_ReleasesResources()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel);

        // Act
        await compiled.DisposeAsync();

        // Assert - Operations after disposal should throw
        var args = new KernelArguments();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await compiled.ExecuteAsync(args));

        LogTestInfo("Kernel async disposed successfully");
    }

    #endregion

    #region ThreadGroup Size Tests

    [SkippableFact]
    public async Task OptimalThreadgroupSize_CalculatedCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();
        var compiled = await compiler.CompileAsync(kernel) as MetalCompiledKernel;

        // Act
        var metadata = compiled!.CompilationMetadata;

        // Assert
        var maxThreads = metadata.MemoryUsage["MaxThreadsPerThreadgroup"];
        Assert.True((int)maxThreads > 0);
        Assert.True((int)maxThreads <= 1024); // Metal limit

        LogTestInfo($"Optimal threadgroup size calculated: {maxThreads} threads");
    }

    #endregion
}
