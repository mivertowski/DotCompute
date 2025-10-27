// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Tests for Metal kernel compilation and execution
    /// </summary>
    [Trait("Category", "RequiresMetal")]
    public class MetalKernelExecutionTests : MetalTestBase
    {
        public MetalKernelExecutionTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task Simple_Kernel_Should_Compile_And_Execute()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            // Define a simple Metal kernel
            var kernelDefinition = new KernelDefinition
            {
                Name = "vector_add",
                Language = KernelLanguage.Metal,
                Code = @"
                    #include <metal_stdlib>
                    using namespace metal;
                    
                    kernel void vector_add(
                        device const float* a [[buffer(0)]],
                        device const float* b [[buffer(1)]],
                        device float* result [[buffer(2)]],
                        uint id [[thread_position_in_grid]]
                    ) {
                        result[id] = a[id] + b[id];
                    }
                "
            };

            // Compile the kernel
            var compiledKernel = await accelerator!.CompileKernelAsync(kernelDefinition);
            compiledKernel.Should().NotBeNull();
            
            Output.WriteLine($"Successfully compiled kernel: {kernelDefinition.Name}");
            // Note: CompilationDuration is not available on ICompiledKernel interface
            
            // Test data
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
            
            // Allocate device buffers
            var bufferA = await accelerator.Memory.AllocateAsync<float>(size);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
            var bufferResult = await accelerator.Memory.AllocateAsync<float>(size);
            
            // Copy data to device
            await bufferA.CopyFromAsync(a.AsMemory());
            await bufferB.CopyFromAsync(b.AsMemory());
            
            // Execute kernel using KernelArguments
            var arguments = new KernelArguments();
            arguments.AddBuffer(bufferA);
            arguments.AddBuffer(bufferB);
            arguments.AddBuffer(bufferResult);
            arguments.AddScalar(size);
            
            await compiledKernel.ExecuteAsync(arguments);
            
            // Copy result back
            var result = new float[size];
            await bufferResult.CopyToAsync(result.AsMemory());
            
            // Verify results (check first 1000 elements for performance)
            for (int i = 0; i < Math.Min(1000, size); i++)
            {
                result[i].Should().BeApproximately(expected[i], 0.001f, 
                    $"Element {i} should match expected value");
            }
            
            var throughput = (size * 3 * sizeof(float)) / 1024.0 / 1024.0 / 1024.0;
            Output.WriteLine($"Vector addition kernel executed successfully:");
            Output.WriteLine($"  Elements: {size:N0}");
            Output.WriteLine($"  Data size: {throughput:F2} GB");
            
            // Cleanup
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferResult.DisposeAsync();
        }

        [SkippableFact]
        public async Task Matrix_Multiplication_Kernel_Should_Work()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            // Define matrix multiplication kernel
            var kernelDefinition = new KernelDefinition
            {
                Name = "matrix_multiply",
                Language = KernelLanguage.Metal,
                Code = @"
                    #include <metal_stdlib>
                    using namespace metal;
                    
                    kernel void matrix_multiply(
                        device const float* a [[buffer(0)]],
                        device const float* b [[buffer(1)]],
                        device float* c [[buffer(2)]],
                        constant uint& M [[buffer(3)]],
                        constant uint& N [[buffer(4)]],
                        constant uint& K [[buffer(5)]],
                        uint2 id [[thread_position_in_grid]]
                    ) {
                        uint row = id.y;
                        uint col = id.x;
                        
                        if (row >= M || col >= N) return;
                        
                        float sum = 0.0f;
                        for (uint k = 0; k < K; k++) {
                            sum += a[row * K + k] * b[k * N + col];
                        }
                        c[row * N + col] = sum;
                    }
                "
            };

            // Compile the kernel
            var compiledKernel = await accelerator!.CompileKernelAsync(kernelDefinition);
            compiledKernel.Should().NotBeNull();
            
            // Small matrices for testing
            const int M = 4, N = 4, K = 4;
            
            var a = new float[M * K];
            var b = new float[K * N];
            var expected = new float[M * N];
            
            // Initialize with simple values
            for (int i = 0; i < M * K; i++)
            {
                a[i] = i;
            }
            for (int i = 0; i < K * N; i++)
            {
                b[i] = i;
            }
            
            // Calculate expected result
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < K; k++)
                    {
                        sum += a[i * K + k] * b[k * N + j];
                    }
                    expected[i * N + j] = sum;
                }
            }
            
            // Execute on Metal
            var bufferA = await accelerator.Memory.AllocateAsync<float>(M * K);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(K * N);
            var bufferC = await accelerator.Memory.AllocateAsync<float>(M * N);
            
            await bufferA.CopyFromAsync(a.AsMemory());
            await bufferB.CopyFromAsync(b.AsMemory());
            
            var arguments = new KernelArguments();
            arguments.AddBuffer(bufferA);
            arguments.AddBuffer(bufferB);
            arguments.AddBuffer(bufferC);
            arguments.AddScalar(M);
            arguments.AddScalar(N);
            arguments.AddScalar(K);
            
            await compiledKernel.ExecuteAsync(arguments);
            
            var result = new float[M * N];
            await bufferC.CopyToAsync(result.AsMemory());
            
            // Verify
            for (int i = 0; i < M * N; i++)
            {
                result[i].Should().BeApproximately(expected[i], 0.001f);
            }
            
            var gflops = (2.0 * M * N * K) / 1e9; // Rough GFLOP count
            Output.WriteLine($"Matrix multiplication performance:");
            Output.WriteLine($"  Matrix Size: {M}x{K} * {K}x{N}");
            Output.WriteLine($"  Operations: ~{gflops:F2} GFLOP");
            
            // Cleanup
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
        }

        [SkippableFact]
        public async Task Kernel_Compilation_Should_Be_Cached()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            var kernelDefinition = new KernelDefinition
            {
                Name = "simple_copy",
                Language = KernelLanguage.Metal,
                Code = @"
                    #include <metal_stdlib>
                    using namespace metal;
                    
                    kernel void simple_copy(
                        device const float* input [[buffer(0)]],
                        device float* output [[buffer(1)]],
                        uint id [[thread_position_in_grid]]
                    ) {
                        output[id] = input[id];
                    }
                "
            };

            // First compilation
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var compiledKernel1 = await accelerator!.CompileKernelAsync(kernelDefinition);
            sw1.Stop();
            
            // Second compilation (should be cached)
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var compiledKernel2 = await accelerator.CompileKernelAsync(kernelDefinition);
            sw2.Stop();
            
            compiledKernel1.Should().NotBeNull();
            compiledKernel2.Should().NotBeNull();
            
            // Cached compilation should be much faster
            sw2.ElapsedMilliseconds.Should().BeLessThan(sw1.ElapsedMilliseconds / 2,
                "Cached compilation should be significantly faster");
            
            Output.WriteLine($"First compilation: {sw1.ElapsedMilliseconds}ms");
            Output.WriteLine($"Cached compilation: {sw2.ElapsedMilliseconds}ms");
            Output.WriteLine($"Speed improvement: {sw1.ElapsedMilliseconds / (double)Math.Max(1, sw2.ElapsedMilliseconds):F2}x");
        }
    }
}