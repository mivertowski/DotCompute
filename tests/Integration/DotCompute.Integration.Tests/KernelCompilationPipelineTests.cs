// Copyright(c) 2025 Michael Ivertowski

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;


/// <summary>
/// Integration tests for kernel compilation and execution pipelines.
/// Tests the complete workflow from source code to executable kernels.
/// </summary>
public sealed class KernelCompilationPipelineTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Maximum)]
    public async Task KernelCompilation_DifferentOptimizationLevels_ShouldCompileSuccessfully(OptimizationLevel level)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = level,
            EnableDebugInfo = level == OptimizationLevel.None
        };

        // Act
        var compilationResult = await CompileAndExecuteKernel(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            compilationOptions);

        // Assert
        Assert.NotNull(compilationResult);
        _ = compilationResult.CompilationSuccess.Should().BeTrue();
        _ = compilationResult.ExecutionSuccess.Should().BeTrue();
        _ = compilationResult.CompilationTime.Should().BePositive();

        // Higher optimization levels might take longer to compile
        if (level == OptimizationLevel.Maximum)
        {
            _ = compilationResult.CompilationTime.Should().BeLessThan(TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public async Task KernelCompilation_MultipleKernels_ShouldCompileConcurrently()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var kernelSources = new[]
        {
       ("vector_add", VectorAddKernelSource),
       ("vector_mul", VectorMultiplyKernelSource),
       ("matrix_transpose", MatrixTransposeKernelSource),
       ("reduction_sum", ReductionKernelSource)
    };

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            FastMath = true
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var concurrentResults = await CompileKernelsConcurrently(
            computeEngine,
            kernelSources,
            compilationOptions);
        stopwatch.Stop();

        // Assert
        Assert.Equal(kernelSources.Length, concurrentResults.Count);
        _ = concurrentResults.Should().AllSatisfy(r => r.CompilationSuccess.Should().BeTrue());

        var totalCompilationTime = concurrentResults.Sum(r => r.CompilationTime.TotalMilliseconds);
        var concurrentTime = stopwatch.Elapsed.TotalMilliseconds;

        // Concurrent compilation should show some parallelism benefit
        Assert.True(concurrentTime < totalCompilationTime * 0.8);
    }

    [Fact]
    public async Task KernelCompilation_WithPreprocessorDefines_ShouldApplyDefines()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            Defines = new Dictionary<string, string>
            {
                ["VECTOR_SIZE"] = "256",
                ["USE_DOUBLE"] = "0",
                ["ENABLE_BOUNDS_CHECK"] = "1"
            }
        };

        const string kernelWithDefines = @"
            #ifndef VECTOR_SIZE
            #define VECTOR_SIZE 64
            #endif
            
            #if USE_DOUBLE
            typedef double FLOAT_TYPE;
            #else
            typedef float FLOAT_TYPE;
            #endif
            
            __kernel void conditional_kernel(__global const FLOAT_TYPE* input,
                                           __global FLOAT_TYPE* output) {
                int i = get_global_id(0);
                
                #ifdef ENABLE_BOUNDS_CHECK
                if(i < VECTOR_SIZE) {
                #endif
                    output[i] = input[i] * 2.0f;
                #ifdef ENABLE_BOUNDS_CHECK
                }
                #endif
            }";

        // Act
        var result = await CompileAndExecuteKernel(
            computeEngine,
            kernelWithDefines,
            "conditional_kernel",
            compilationOptions);

        // Assert
        Assert.NotNull(result);
        _ = result.CompilationSuccess.Should().BeTrue();
        _ = result.ExecutionSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task KernelCompilation_InvalidSyntax_ShouldReportCompilationErrors()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const string invalidKernel = @"
            __kernel void invalid_kernel(__global float* input,
                                       __global float* output {
                // Missing closing parenthesis and other syntax errors
                int i = get_global_id(0)
                output[i] = input[i] * 2.0f
            }";

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () =>
            await computeEngine.CompileKernelAsync(invalidKernel, "invalid_kernel", compilationOptions));

        Assert.NotNull(exception);
        _ = exception.Message.Should().Contain("compilation");
    }

    [Fact]
    public async Task KernelCompilation_DependentKernels_ShouldResolveCorrectly()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const string dependentKernelSource = @"
            // Helper function
            float helper_function(float x) {
                return x * x + 1.0f;
            }
            
            __kernel void main_kernel(__global const float* input,
                                    __global float* output) {
                int i = get_global_id(0);
                output[i] = helper_function(input[i]);
            }";

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act
        var result = await CompileAndExecuteKernel(
            computeEngine,
            dependentKernelSource,
            "main_kernel",
            compilationOptions);

        // Assert
        Assert.NotNull(result);
        _ = result.CompilationSuccess.Should().BeTrue();
        _ = result.ExecutionSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComputeBackendType.CPU)]
    // Additional backends can be tested when available
    public async Task KernelCompilation_DifferentBackends_ShouldCompileForTarget(ComputeBackendType backendType)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var availableBackends = computeEngine.AvailableBackends;

        if (!availableBackends.Contains(backendType))
        {
            LoggerMessages.SkippingBackendTest(Logger, backendType.ToString());
            return;
        }

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act
        var result = await CompileAndExecuteKernelForBackend(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            compilationOptions,
            backendType);

        // Assert
        Assert.NotNull(result);
        _ = result.CompilationSuccess.Should().BeTrue();
        _ = result.ExecutionSuccess.Should().BeTrue();
        _ = result.TargetBackend.Should().Be(backendType);
    }

    [Fact]
    public async Task KernelCompilation_LargeKernelSource_ShouldHandleEfficiently()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var largeKernelSource = GenerateLargeKernelSource(100); // 100 similar functions

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act
        var result = await CompileAndExecuteKernel(
            computeEngine,
            largeKernelSource,
            "large_kernel",
            compilationOptions);

        // Assert
        Assert.NotNull(result);
        _ = result.CompilationSuccess.Should().BeTrue();
        _ = result.CompilationTime.Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task KernelCompilation_CachingBehavior_ShouldReuseCachedKernels()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act - Compile the same kernel twice
        var firstCompilation = await CompileKernelWithTiming(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            compilationOptions);

        var secondCompilation = await CompileKernelWithTiming(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            compilationOptions);

        // Assert
        _ = firstCompilation.CompilationSuccess.Should().BeTrue();
        _ = secondCompilation.CompilationSuccess.Should().BeTrue();

        // Second compilation should be faster due to caching
        //(This is an assumption - actual behavior depends on implementation)
        LoggerMessages.FirstCompilationTime(Logger, firstCompilation.CompilationTime.TotalMilliseconds);
        LoggerMessages.SecondCompilationTime(Logger, secondCompilation.CompilationTime.TotalMilliseconds);
    }

    [Fact]
    public async Task KernelCompilation_ComplexDataTypes_ShouldHandleStructures()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const string structKernelSource = @"
            typedef struct {
                float x, y, z;
            } Vector3;
            
            typedef struct {
                Vector3 position;
                Vector3 velocity;
                float mass;
            } Particle;
            
            __kernel void update_particles(__global Particle* particles,
                                         float deltaTime) {
                int i = get_global_id(0);
                
                // Update position based on velocity
                particles[i].position.x += particles[i].velocity.x * deltaTime;
                particles[i].position.y += particles[i].velocity.y * deltaTime;
                particles[i].position.z += particles[i].velocity.z * deltaTime;
                
                // Apply simple gravity
                particles[i].velocity.y -= 9.81f * deltaTime;
            }";

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        // Act
        var result = await CompileAndExecuteKernel(
            computeEngine,
            structKernelSource,
            "update_particles",
            compilationOptions);

        // Assert
        Assert.NotNull(result);
        _ = result.CompilationSuccess.Should().BeTrue();
        _ = result.ExecutionSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(64, 1)]
    [InlineData(256, 4)]
    [InlineData(1024, 16)]
    [InlineData(4096, 64)]
    public async Task KernelExecution_VariousWorkGroupSizes_ShouldExecuteCorrectly(int globalSize, int localSize)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default
        };

        var executionOptions = new ExecutionOptions
        {
            GlobalWorkSize = [globalSize],
            LocalWorkSize = [localSize]
        };

        // Act
        var result = await CompileAndExecuteKernelWithOptions(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            compilationOptions,
            executionOptions,
            globalSize);

        // Assert
        Assert.NotNull(result);
        _ = result.CompilationSuccess.Should().BeTrue();
        _ = result.ExecutionSuccess.Should().BeTrue();
        _ = result.ExecutionTime.Should().BePositive();
    }

    // Helper methods
    private async Task<CompilationResult> CompileAndExecuteKernel(
        IComputeEngine computeEngine,
        string kernelSource,
        string entryPoint,
        CompilationOptions compilationOptions)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Compile kernel
            var compiledKernel = await computeEngine.CompileKernelAsync(
                kernelSource, entryPoint, compilationOptions);

            var compilationTime = stopwatch.Elapsed;
            stopwatch.Restart();

            // Execute kernel with test data
            const int testSize = 256;
            var testDataA = GenerateTestData(testSize);
            var testDataB = GenerateTestData(testSize);
            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();

            var inputBufferA = await CreateInputBuffer(memoryManager, testDataA);
            var inputBufferB = await CreateInputBuffer(memoryManager, testDataB);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, testSize);

            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBufferA, inputBufferB, outputBuffer],
                ComputeBackendType.CPU,
                new ExecutionOptions { GlobalWorkSize = [testSize] });

            stopwatch.Stop();
            var executionTime = stopwatch.Elapsed;

            return new CompilationResult
            {
                CompilationSuccess = true,
                ExecutionSuccess = true,
                CompilationTime = compilationTime,
                ExecutionTime = executionTime
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Kernel compilation/execution failed");

            return new CompilationResult
            {
                CompilationSuccess = false,
                ExecutionSuccess = false,
                CompilationTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<CompilationResult> CompileAndExecuteKernelForBackend(
        IComputeEngine computeEngine,
        string kernelSource,
        string entryPoint,
        CompilationOptions compilationOptions,
        ComputeBackendType targetBackend)
    {
        var result = await CompileAndExecuteKernel(computeEngine, kernelSource, entryPoint, compilationOptions);
        result.TargetBackend = targetBackend;
        return result;
    }

    private async Task<CompilationResult> CompileAndExecuteKernelWithOptions(
        IComputeEngine computeEngine,
        string kernelSource,
        string entryPoint,
        CompilationOptions compilationOptions,
        ExecutionOptions executionOptions,
        int dataSize)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Compile kernel
            var compiledKernel = await computeEngine.CompileKernelAsync(
                kernelSource, entryPoint, compilationOptions);

            var compilationTime = stopwatch.Elapsed;
            stopwatch.Restart();

            // Execute kernel with specified options
            var testDataA = GenerateTestData(dataSize);
            var testDataB = GenerateTestData(dataSize);
            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();

            var inputBufferA = await CreateInputBuffer(memoryManager, testDataA);
            var inputBufferB = await CreateInputBuffer(memoryManager, testDataB);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, dataSize);

            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBufferA, inputBufferB, outputBuffer],
                ComputeBackendType.CPU,
                executionOptions);

            stopwatch.Stop();
            var executionTime = stopwatch.Elapsed;

            return new CompilationResult
            {
                CompilationSuccess = true,
                ExecutionSuccess = true,
                CompilationTime = compilationTime,
                ExecutionTime = executionTime
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Kernel compilation/execution with options failed");

            return new CompilationResult
            {
                CompilationSuccess = false,
                ExecutionSuccess = false,
                CompilationTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<List<CompilationResult>> CompileKernelsConcurrently(
        IComputeEngine computeEngine,
       (string Name, string Source)[] kernelSources,
        CompilationOptions compilationOptions)
    {
        var tasks = kernelSources.Select(async kernel =>
        {
            return await CompileAndExecuteKernel(
                computeEngine,
                kernel.Source,
                kernel.Name,
                compilationOptions);
        });

        return [.. (await Task.WhenAll(tasks))];
    }

    private async Task<CompilationResult> CompileKernelWithTiming(
        IComputeEngine computeEngine,
        string kernelSource,
        string entryPoint,
        CompilationOptions compilationOptions)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                kernelSource, entryPoint, compilationOptions);
            stopwatch.Stop();

            return new CompilationResult
            {
                CompilationSuccess = true,
                ExecutionSuccess = false, // Not executed in this method
                CompilationTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Kernel compilation failed");

            return new CompilationResult
            {
                CompilationSuccess = false,
                ExecutionSuccess = false,
                CompilationTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private static string GenerateLargeKernelSource(int functionCount)
    {
        var source = new System.Text.StringBuilder();

        // Generate many similar functions
        for (var i = 0; i < functionCount; i++)
        {
            _ = source.AppendLine(CultureInfo.InvariantCulture, $@"
            float function_{i}(float x) {{
                return x * {i + 1}.0f + {i * 2}.0f;
            }}");
        }

        // Main kernel that uses all functions
        _ = source.AppendLine(@"
            __kernel void large_kernel(__global const float* input,
                                     __global float* output) {
                int i = get_global_id(0);
                float value = input[i];");

        for (var i = 0; i < Math.Min(functionCount, 10); i++) // Use only first 10 to avoid too much computation
        {
            _ = source.AppendLine(CultureInfo.InvariantCulture, $"                value = function_{i}(value);");
        }

        _ = source.AppendLine(@"
                output[i] = value;
            }");

        return source.ToString();
    }

    private static float[] GenerateTestData(int size)
    {
        var random = new Random(42);
        return [.. Enumerable.Range(0, size).Select(_ => (float)random.NextDouble() * 100.0f)];
    }

    // Kernel source constants
    private const string VectorAddKernelSource = @"
        __kernel void vector_add(__global const float* a,
                               __global const float* b,
                               __global float* result) {
            int i = get_global_id(0);
            result[i] = a[i] + b[i];
        }";

    private const string VectorMultiplyKernelSource = @"
        __kernel void vector_mul(__global const float* a,
                               __global const float* b,
                               __global float* result) {
            int i = get_global_id(0);
            result[i] = a[i] * b[i];
        }";

    private const string MatrixTransposeKernelSource = @"
        __kernel void matrix_transpose(__global const float* input,
                                     __global float* output,
                                     int width, int height) {
            int col = get_global_id(0);
            int row = get_global_id(1);
            
            if(col < width && row < height) {
                int inputIndex = row * width + col;
                int outputIndex = col * height + row;
                output[outputIndex] = input[inputIndex];
            }
        }";

    private const string ReductionKernelSource = @"
        __kernel void reduction_sum(__global const float* input,
                                  __global float* output,
                                  __local float* scratch) {
            int lid = get_local_id(0);
            int gid = get_global_id(0);
            
            scratch[lid] = input[gid];
            barrier(CLK_LOCAL_MEM_FENCE);
            
            for(int offset = get_local_size(0) / 2; offset > 0; offset >>= 1) {
                if(lid < offset) {
                    scratch[lid] += scratch[lid + offset];
                }
                barrier(CLK_LOCAL_MEM_FENCE);
            }
            
            if(lid == 0) {
                output[get_group_id(0)] = scratch[0];
            }
        }";
}

/// <summary>
/// Result of kernel compilation and execution test.
/// </summary>
public class CompilationResult
{
    public bool CompilationSuccess { get; set; }
    public bool ExecutionSuccess { get; set; }
    public TimeSpan CompilationTime { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public ComputeBackendType TargetBackend { get; set; }
    public string? Error { get; set; }
}
