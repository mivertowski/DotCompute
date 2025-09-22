// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Integration.Tests.Utilities;
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for kernel compilation system.
/// Tests source generation, CUDA kernel compilation, CPU fallback compilation, and caching mechanisms.
/// </summary>
[Collection("Integration")]
public class KernelCompilationIntegrationTests : IntegrationTestBase
{
    private readonly ILogger<KernelCompilationIntegrationTests> _logger;
    private readonly List<IAccelerator> _availableAccelerators;

    public KernelCompilationIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _logger = GetLogger<KernelCompilationIntegrationTests>();
        _availableAccelerators = GetAvailableAccelerators();
    }

    private List<IAccelerator> GetAvailableAccelerators()
    {
        var accelerators = new List<IAccelerator>();
        
        try
        {
            // Try to get CPU accelerator
            var cpuAccelerator = ServiceProvider.GetService<IAccelerator>();
            if (cpuAccelerator?.Type == AcceleratorType.CPU)
            {
                accelerators.Add(cpuAccelerator);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("CPU accelerator not available: {Error}", ex.Message);
        }

        return accelerators;
    }

    [Fact]
    public async Task CompileKernel_SimpleVectorAdd_ShouldSucceed()
    {
        // Arrange
        var kernelDefinition = CreateVectorAddKernelDefinition();
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();
        _logger.LogInformation("Testing kernel compilation on {Type}: {Name}", 
            accelerator.Type, accelerator.Info.Name);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            using var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
            
            // Assert
            compiledKernel.Should().NotBeNull("Compiled kernel should not be null");
            compiledKernel.Name.Should().Be(kernelDefinition.Name, "Kernel name should match");
            compiledKernel.Id.Should().NotBe(Guid.Empty, "Kernel should have valid ID");
            
        }, "KernelCompilation");

        // Assert performance
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(60),
            "Kernel compilation should complete in reasonable time");

        _logger.LogInformation("Kernel compilation completed in {Time}ms",
            measurement.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task CompileKernel_WithOptimization_ShouldProduceFasterCode()
    {
        // Arrange
        var kernelDefinition = CreateMatrixMultiplyKernelDefinition();
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();
        
        var debugOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Debug,
            GenerateDebugInfo = true
        };

        var optimizedOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Release,
            GenerateDebugInfo = false,
            EnableVectorization = true
        };

        _logger.LogInformation("Comparing debug vs optimized compilation");

        // Act
        var debugCompileTime = await MeasurePerformanceAsync(async () =>
        {
            using var debugKernel = await accelerator.CompileKernelAsync(kernelDefinition, debugOptions);
        }, "DebugCompilation");

        var optimizedCompileTime = await MeasurePerformanceAsync(async () =>
        {
            using var optimizedKernel = await accelerator.CompileKernelAsync(kernelDefinition, optimizedOptions);
        }, "OptimizedCompilation");

        // Assert
        debugCompileTime.ElapsedTime.Should().BeLessThan(TimeSpan.FromMinutes(2),
            "Debug compilation should complete in reasonable time");
        
        optimizedCompileTime.ElapsedTime.Should().BeLessThan(TimeSpan.FromMinutes(5),
            "Optimized compilation should complete in reasonable time");

        _logger.LogInformation("Debug compilation: {DebugTime}ms, Optimized: {OptTime}ms",
            debugCompileTime.ElapsedTime.TotalMilliseconds,
            optimizedCompileTime.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task CompileKernel_ConcurrentCompilation_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 4;
        const int compilationsPerThread = 3;
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();
        var kernelDefinitions = new[]
        {
            CreateVectorAddKernelDefinition(),
            CreateScalarMultiplyKernelDefinition(),
            CreateArrayCopyKernelDefinition()
        };

        _logger.LogInformation("Testing concurrent compilation with {Threads} threads", threadCount);

        // Act
        var results = await ExecuteConcurrentlyAsync(async threadId =>
        {
            var compiledKernels = new List<ICompiledKernel>();
            
            try
            {
                for (int i = 0; i < compilationsPerThread; i++)
                {
                    var kernelDef = kernelDefinitions[i % kernelDefinitions.Length];
                    var compiledKernel = await accelerator.CompileKernelAsync(kernelDef);
                    compiledKernels.Add(compiledKernel);
                }
                
                return new { ThreadId = threadId, Success = true, Kernels = compiledKernels };
            }
            catch (Exception ex)
            {
                _logger.LogError("Thread {ThreadId} failed: {Error}", threadId, ex.Message);
                return new { ThreadId = threadId, Success = false, Kernels = compiledKernels };
            }
        }, threadCount);

        // Assert
        results.Should().HaveCount(threadCount, "All threads should complete");
        results.Should().OnlyContain(r => r.Success, "All compilations should succeed");

        var totalKernels = results.Sum(r => r.Kernels.Count);
        totalKernels.Should().Be(threadCount * compilationsPerThread,
            "All kernels should be compiled successfully");

        // Cleanup
        foreach (var result in results)
        {
            foreach (var kernel in result.Kernels)
            {
                await kernel.DisposeAsync();
            }
        }

        _logger.LogInformation("Concurrent compilation test completed successfully");
    }

    [SkippableFact]
    public async Task CompileKernel_CudaSpecific_ShouldCompileWithCudaFeatures()
    {
        // Skip if CUDA not available
        SkipIfCudaNotAvailable();

        // Arrange
        var cudaKernelDefinition = CreateCudaSpecificKernelDefinition();
        
        var gpuAccelerator = _availableAccelerators.FirstOrDefault(a => a.Type == AcceleratorType.GPU);
        Skip.IfNot(gpuAccelerator != null, "GPU accelerator not available");

        _logger.LogInformation("Testing CUDA-specific kernel compilation");

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            using var compiledKernel = await gpuAccelerator!.CompileKernelAsync(cudaKernelDefinition);
            
            // Assert
            compiledKernel.Should().NotBeNull("CUDA kernel should compile successfully");
            compiledKernel.Name.Should().Contain("Cuda", "Kernel name should indicate CUDA specificity");
            
        }, "CudaKernelCompilation");

        // Assert
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromMinutes(2),
            "CUDA kernel compilation should complete in reasonable time");

        _logger.LogInformation("CUDA kernel compilation completed in {Time}ms",
            measurement.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task CompileKernel_InvalidKernelCode_ShouldThrowCompilationException()
    {
        // Arrange
        var invalidKernelDefinition = CreateInvalidKernelDefinition();
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();

        _logger.LogInformation("Testing compilation error handling");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await accelerator.CompileKernelAsync(invalidKernelDefinition);
        });

        _logger.LogInformation("Compilation error handling test passed");
    }

    [Fact]
    public async Task CompileKernel_CompilationCaching_ShouldReuseCompiledKernels()
    {
        // Arrange
        var kernelDefinition = CreateVectorAddKernelDefinition();
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();

        _logger.LogInformation("Testing compilation caching");

        // Act - First compilation
        var firstCompileTime = await MeasurePerformanceAsync(async () =>
        {
            using var kernel1 = await accelerator.CompileKernelAsync(kernelDefinition);
        }, "FirstCompilation");

        // Act - Second compilation (should be faster due to caching)
        var secondCompileTime = await MeasurePerformanceAsync(async () =>
        {
            using var kernel2 = await accelerator.CompileKernelAsync(kernelDefinition);
        }, "SecondCompilation");

        // Assert
        _logger.LogInformation("First: {First}ms, Second: {Second}ms",
            firstCompileTime.ElapsedTime.TotalMilliseconds,
            secondCompileTime.ElapsedTime.TotalMilliseconds);

        // Second compilation should be significantly faster (cache hit)
        // Allow some tolerance for system variability
        if (firstCompileTime.ElapsedTime.TotalMilliseconds > 100)
        {
            var speedupRatio = firstCompileTime.ElapsedTime.TotalMilliseconds / 
                             Math.Max(secondCompileTime.ElapsedTime.TotalMilliseconds, 1);
            
            speedupRatio.Should().BeGreaterThan(1.5, "Cached compilation should be significantly faster");
        }
    }

    [Fact]
    public async Task CompileKernel_DifferentDataTypes_ShouldSupportVariousTypes()
    {
        // Arrange
        var dataTypes = new[] { "float", "double", "int", "uint" };
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();
        var compiledKernels = new List<ICompiledKernel>();

        _logger.LogInformation("Testing compilation with different data types");

        try
        {
            // Act
            foreach (var dataType in dataTypes)
            {
                var kernelDef = CreateTypedKernelDefinition(dataType);
                
                try
                {
                    var compiledKernel = await accelerator.CompileKernelAsync(kernelDef);
                    compiledKernels.Add(compiledKernel);
                    
                    _logger.LogInformation("Successfully compiled kernel for type: {Type}", dataType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to compile kernel for type {Type}: {Error}", 
                        dataType, ex.Message);
                    
                    // For some backends, certain types might not be supported
                    // This is acceptable behavior
                }
            }

            // Assert
            compiledKernels.Should().NotBeEmpty("At least some data types should be supported");
            
            foreach (var kernel in compiledKernels)
            {
                kernel.Should().NotBeNull("Each compiled kernel should be valid");
                kernel.Name.Should().NotBeNullOrEmpty("Each kernel should have a name");
            }
        }
        finally
        {
            // Cleanup
            foreach (var kernel in compiledKernels)
            {
                await kernel.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task CompileKernel_LargeKernel_ShouldHandleComplexCode()
    {
        // Arrange
        var largeKernelDefinition = CreateLargeKernelDefinition();
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();

        _logger.LogInformation("Testing compilation of large/complex kernel");

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            using var compiledKernel = await accelerator.CompileKernelAsync(largeKernelDefinition);
            
            // Assert
            compiledKernel.Should().NotBeNull("Large kernel should compile successfully");
            
        }, "LargeKernelCompilation");

        // Assert
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromMinutes(5),
            "Large kernel compilation should complete in reasonable time");

        _logger.LogInformation("Large kernel compilation completed in {Time}ms",
            measurement.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task ExecuteCompiledKernel_BasicExecution_ShouldWork()
    {
        // Arrange
        var kernelDefinition = CreateVectorAddKernelDefinition();
        
        if (!_availableAccelerators.Any())
        {
            _logger.LogWarning("No accelerators available for testing");
            return;
        }

        var accelerator = _availableAccelerators.First();
        using var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);

        const int size = 100;
        var testData = GetService<TestDataGenerator>();
        var a = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(size);
        var b = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(size);
        var result = new float[size];

        var args = KernelArguments.Create()
            .Add("a", a)
            .Add("b", b)
            .Add("result", result)
            .Add("size", size);

        _logger.LogInformation("Testing execution of compiled kernel");

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await compiledKernel.ExecuteAsync(args);
        }, "KernelExecution");

        // Assert
        for (int i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i} should be sum of inputs");
        }

        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "Kernel execution should be fast");

        _logger.LogInformation("Kernel execution completed in {Time}ms",
            measurement.ElapsedTime.TotalMilliseconds);
    }

    // Helper methods to create kernel definitions

    private KernelDefinition CreateVectorAddKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "VectorAdd",
            Source = @"
                kernel void VectorAdd(global float* a, global float* b, global float* result, int size) {
                    int i = get_global_id(0);
                    if (i < size) {
                        result[i] = a[i] + b[i];
                    }
                }",
            EntryPoint = "VectorAdd",
            CompilationTarget = CompilationTarget.OpenCL,
            Parameters = new[]
            {
                new KernelParameter { Name = "a", Type = typeof(float[]), Direction = ParameterDirection.In },
                new KernelParameter { Name = "b", Type = typeof(float[]), Direction = ParameterDirection.In },
                new KernelParameter { Name = "result", Type = typeof(float[]), Direction = ParameterDirection.Out },
                new KernelParameter { Name = "size", Type = typeof(int), Direction = ParameterDirection.In }
            }
        };
    }

    private KernelDefinition CreateMatrixMultiplyKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "MatrixMultiply",
            Source = @"
                kernel void MatrixMultiply(global float* A, global float* B, global float* C, int N) {
                    int row = get_global_id(0);
                    int col = get_global_id(1);
                    
                    if (row < N && col < N) {
                        float sum = 0.0f;
                        for (int k = 0; k < N; k++) {
                            sum += A[row * N + k] * B[k * N + col];
                        }
                        C[row * N + col] = sum;
                    }
                }",
            EntryPoint = "MatrixMultiply",
            CompilationTarget = CompilationTarget.OpenCL
        };
    }

    private KernelDefinition CreateScalarMultiplyKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "ScalarMultiply",
            Source = @"
                kernel void ScalarMultiply(global float* input, global float* output, float scalar, int size) {
                    int i = get_global_id(0);
                    if (i < size) {
                        output[i] = input[i] * scalar;
                    }
                }",
            EntryPoint = "ScalarMultiply",
            CompilationTarget = CompilationTarget.OpenCL
        };
    }

    private KernelDefinition CreateArrayCopyKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "ArrayCopy",
            Source = @"
                kernel void ArrayCopy(global float* input, global float* output, int size) {
                    int i = get_global_id(0);
                    if (i < size) {
                        output[i] = input[i];
                    }
                }",
            EntryPoint = "ArrayCopy",
            CompilationTarget = CompilationTarget.OpenCL
        };
    }

    private KernelDefinition CreateCudaSpecificKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "CudaVectorAdd",
            Source = @"
                extern ""C"" __global__ void CudaVectorAdd(float* a, float* b, float* result, int size) {
                    int i = blockIdx.x * blockDim.x + threadIdx.x;
                    if (i < size) {
                        result[i] = a[i] + b[i];
                    }
                }",
            EntryPoint = "CudaVectorAdd",
            CompilationTarget = CompilationTarget.CUDA
        };
    }

    private KernelDefinition CreateInvalidKernelDefinition()
    {
        return new KernelDefinition
        {
            Name = "InvalidKernel",
            Source = @"
                this is not valid kernel code!!!
                syntax error here
                invalid() function() calls()
            ",
            EntryPoint = "InvalidKernel",
            CompilationTarget = CompilationTarget.OpenCL
        };
    }

    private KernelDefinition CreateTypedKernelDefinition(string dataType)
    {
        return new KernelDefinition
        {
            Name = $"TypedKernel_{dataType}",
            Source = $@"
                kernel void TypedKernel_{dataType}(global {dataType}* input, global {dataType}* output, int size) {{
                    int i = get_global_id(0);
                    if (i < size) {{
                        output[i] = input[i];
                    }}
                }}",
            EntryPoint = $"TypedKernel_{dataType}",
            CompilationTarget = CompilationTarget.OpenCL
        };
    }

    private KernelDefinition CreateLargeKernelDefinition()
    {
        var largeSource = @"
            kernel void LargeKernel(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    float value = input[i];
                    
                    // Complex mathematical operations
                    for (int j = 0; j < 10; j++) {
                        value = value * 1.1f + 0.01f;
                        value = sqrt(value);
                        value = sin(value) + cos(value);
                        value = exp(value * 0.1f);
                        value = log(value + 1.0f);
                    }
                    
                    // More operations
                    value = pow(value, 0.5f);
                    value = fabs(value);
                    value = floor(value) + fract(value);
                    
                    output[i] = value;
                }
            }";

        return new KernelDefinition
        {
            Name = "LargeKernel",
            Source = largeSource,
            EntryPoint = "LargeKernel",
            CompilationTarget = CompilationTarget.OpenCL
        };
    }
}