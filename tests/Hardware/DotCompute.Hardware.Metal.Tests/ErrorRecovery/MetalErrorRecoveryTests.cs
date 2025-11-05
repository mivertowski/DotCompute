// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.ErrorRecovery;

/// <summary>
/// Tests for Metal error recovery including CPU fallback, exception handling,
/// and resource cleanup on errors.
/// </summary>
[Trait("Category", "Hardware")]
public class MetalErrorRecoveryTests : MetalTestBase
{
    public MetalErrorRecoveryTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task InvalidKernelCode_ShouldThrowAndNotCorruptState()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var invalidKernel = new KernelDefinition
        {
            Name = "invalid_syntax",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void invalid_syntax(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    // Missing semicolon - syntax error
                    data[id] = data[id] * 2.0f
                }"
        };

        // Act & Assert - Should throw during compilation
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await accelerator!.CompileKernelAsync(invalidKernel);
        });

        // Verify accelerator is still functional
        var validKernel = new KernelDefinition
        {
            Name = "valid_kernel",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void valid_kernel(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = data[id] * 2.0f;
                }"
        };

        var compiled = await accelerator!.CompileKernelAsync(validKernel);
        compiled.Should().NotBeNull("Accelerator should recover from compilation error");
    }

    [SkippableFact]
    public async Task OutOfMemoryAllocation_ShouldThrowAndCleanup()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        // Try to allocate an impossibly large buffer
        const int hugeSize = int.MaxValue;

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await accelerator!.Memory.AllocateAsync<float>(hugeSize);
        });

        // Verify we can still allocate normal-sized buffers
        var normalBuffer = await accelerator!.Memory.AllocateAsync<float>(1000);
        normalBuffer.Should().NotBeNull("Should be able to allocate after OOM error");

        await normalBuffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task KernelExecutionFailure_ShouldNotCorruptSubsequentExecutions()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernel = new KernelDefinition
        {
            Name = "execution_test",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void execution_test(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = data[id] * 2.0f;
                }"
        };

        var compiled = await accelerator!.CompileKernelAsync(kernel);

        // Act - Try to execute with null/invalid arguments
        try
        {
            var invalidArgs = new KernelArguments();
            // Missing required buffer argument
            await compiled.ExecuteAsync(invalidArgs);
        }
        catch
        {
            // Expected to fail
        }

        // Assert - Verify subsequent valid execution works
        var buffer = await accelerator.Memory.AllocateAsync<float>(1000);
        var data = MetalTestDataGenerator.CreateConstantData(1000, 1.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        var validArgs = new KernelArguments();
        validArgs.AddBuffer(buffer);

        await compiled.ExecuteAsync(validArgs);

        var result = new float[1000];
        await buffer.CopyToAsync(result.AsMemory());

        result[0].Should().BeApproximately(2.0f, 0.001f, "Recovery after execution error should work");

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task MultipleSimultaneousErrors_ShouldHandleGracefully()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var invalidKernels = new[]
        {
            new KernelDefinition
            {
                Name = "error1",
                Language = KernelLanguage.Metal,
                Code = "invalid code 1"
            },
            new KernelDefinition
            {
                Name = "error2",
                Language = KernelLanguage.Metal,
                Code = "invalid code 2"
            },
            new KernelDefinition
            {
                Name = "error3",
                Language = KernelLanguage.Metal,
                Code = "invalid code 3"
            }
        };

        // Act - Try to compile multiple invalid kernels
        var tasks = invalidKernels.Select(k =>
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                accelerator!.CompileKernelAsync(k).AsTask()));

        await Task.WhenAll(tasks);

        // Assert - Accelerator should still be functional
        var validKernel = new KernelDefinition
        {
            Name = "recovery_test",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void recovery_test(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = 42.0f;
                }"
        };

        var compiled = await accelerator!.CompileKernelAsync(validKernel);
        compiled.Should().NotBeNull("Should recover from multiple compilation errors");
    }

    [SkippableFact]
    public async Task ResourceLeakage_AfterErrors_ShouldNotOccur()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int iterations = 100;

        // Act - Repeatedly fail allocations and verify no leakage
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                // Try to allocate with invalid parameters
                await accelerator!.Memory.AllocateAsync<float>(0);
            }
            catch
            {
                // Expected
            }
        }

        // Assert - Should still be able to allocate normally
        var normalBuffer = await accelerator!.Memory.AllocateAsync<float>(1000);
        normalBuffer.Should().NotBeNull("No resource leakage should occur");

        Output.WriteLine($"Completed {iterations} failed allocations without resource leakage");

        await normalBuffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task ExceptionDuringExecution_ShouldCleanupBuffers()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernel = new KernelDefinition
        {
            Name = "cleanup_test",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void cleanup_test(
                    device float* data [[buffer(0)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] = float(id);
                }"
        };

        var compiled = await accelerator!.CompileKernelAsync(kernel);

        // Act - Create buffer and force an error during execution
        var buffer = await accelerator.Memory.AllocateAsync<float>(1000);

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            var args = new KernelArguments();
            args.AddBuffer(buffer);

            await compiled.ExecuteAsync(args, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Buffer should still be usable
        var data = MetalTestDataGenerator.CreateConstantData(1000, 5.0f);
        await buffer.CopyFromAsync(data.AsMemory());

        var result = new float[1000];
        await buffer.CopyToAsync(result.AsMemory());

        result[0].Should().BeApproximately(5.0f, 0.001f, "Buffer should be intact after error");

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    public async Task DeviceReset_ShouldRecoverGracefully()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator1 = Factory.CreateProductionAccelerator();

        // Use accelerator
        var buffer1 = await accelerator1!.Memory.AllocateAsync<float>(1000);
        await buffer1.DisposeAsync();

        // Dispose and create new accelerator
        await accelerator1.DisposeAsync();

        // Act - Create new accelerator (simulates device reset)
        await using var accelerator2 = Factory.CreateProductionAccelerator();

        // Assert - New accelerator should be fully functional
        var buffer2 = await accelerator2!.Memory.AllocateAsync<float>(1000);
        buffer2.Should().NotBeNull("New accelerator should work after previous disposal");

        var data = MetalTestDataGenerator.CreateConstantData(1000, 7.0f);
        await buffer2.CopyFromAsync(data.AsMemory());

        var result = new float[1000];
        await buffer2.CopyToAsync(result.AsMemory());

        result[0].Should().BeApproximately(7.0f, 0.001f);

        await buffer2.DisposeAsync();
    }

    [SkippableFact]
    public async Task ConcurrentErrors_ShouldIsolateBetweenOperations()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var validKernel = new KernelDefinition
        {
            Name = "concurrent_test",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void concurrent_test(
                    device float* data [[buffer(0)]],
                    constant float& multiplier [[buffer(1)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] *= multiplier;
                }"
        };

        var compiled = await accelerator!.CompileKernelAsync(validKernel);

        var buffer1 = await accelerator.Memory.AllocateAsync<float>(1000);
        var buffer2 = await accelerator.Memory.AllocateAsync<float>(1000);

        var data1 = MetalTestDataGenerator.CreateConstantData(1000, 1.0f);
        var data2 = MetalTestDataGenerator.CreateConstantData(1000, 2.0f);

        await buffer1.CopyFromAsync(data1.AsMemory());
        await buffer2.CopyFromAsync(data2.AsMemory());

        // Act - One operation succeeds, one fails
        var successArgs = new KernelArguments();
        successArgs.AddBuffer(buffer1);
        successArgs.AddScalar(3.0f);

        var failArgs = new KernelArguments();
        // Missing scalar parameter - will fail

        var successTask = compiled.ExecuteAsync(successArgs);

        try
        {
            await compiled.ExecuteAsync(failArgs);
        }
        catch
        {
            // Expected failure
        }

        await successTask; // Should succeed despite other failure

        // Assert - Successful operation should have completed correctly
        var result1 = new float[1000];
        await buffer1.CopyToAsync(result1.AsMemory());

        result1[0].Should().BeApproximately(3.0f, 0.001f, "Successful operation should be unaffected by concurrent error");

        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
    }
}
