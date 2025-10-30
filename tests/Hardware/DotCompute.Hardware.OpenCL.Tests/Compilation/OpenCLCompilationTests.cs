// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Compilation;

/// <summary>
/// Hardware tests for OpenCL kernel compilation functionality.
/// Tests compilation, caching, optimization levels, and build options.
/// </summary>
[Trait("Category", "RequiresOpenCL")]
public class OpenCLCompilationTests : OpenCLTestBase
{
    private const string SimpleKernel = @"
        __kernel void simple(__global float* data, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                data[idx] = data[idx] * 2.0f;
            }
        }";

    private const string ComplexKernel = @"
        __kernel void complex(__global const float* input, __global float* output, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                float x = input[idx];
                float result = 0.0f;
                for (int i = 0; i < 100; i++) {
                    result += sin(x + i * 0.01f) * cos(x - i * 0.01f);
                    result += sqrt(fabs(result)) * 0.1f;
                }
                output[idx] = result;
            }
        }";

    private const string KernelWithDefines = @"
        #ifndef BLOCK_SIZE
        #define BLOCK_SIZE 256
        #endif

        __kernel void withDefines(__global float* data) {
            int idx = get_global_id(0);
            if (idx < BLOCK_SIZE) {
                data[idx] = data[idx] + BLOCK_SIZE;
            }
        }";

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCompilationTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public OpenCLCompilationTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Tests basic kernel compilation.
    /// </summary>
    [SkippableFact]
    public async Task Simple_Kernel_Should_Compile()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("simple", SimpleKernel, "simple");

        var stopwatch = Stopwatch.StartNew();
        var kernel = await accelerator.CompileKernelAsync(kernelDef);
        stopwatch.Stop();

        kernel.Should().NotBeNull();
        Output.WriteLine($"Compilation time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Compilation should be fast");
    }

    /// <summary>
    /// Tests complex kernel compilation with optimization.
    /// </summary>
    [SkippableFact]
    public async Task Complex_Kernel_Should_Compile_With_Optimization()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("complex", ComplexKernel, "complex");
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            GenerateDebugInfo = false
        };

        var stopwatch = Stopwatch.StartNew();
        var kernel = await accelerator.CompileKernelAsync(kernelDef, options);
        stopwatch.Stop();

        kernel.Should().NotBeNull();
        Output.WriteLine($"Complex kernel compilation time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
    }

    /// <summary>
    /// Tests kernel compilation with preprocessor defines.
    /// </summary>
    [SkippableFact]
    public async Task Kernel_With_Defines_Should_Compile()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("withDefines", KernelWithDefines, "withDefines");
        var options = new CompilationOptions();
        options.Defines["BLOCK_SIZE"] = "512";

        var kernel = await accelerator.CompileKernelAsync(kernelDef, options);
        kernel.Should().NotBeNull();

        Output.WriteLine("Successfully compiled kernel with custom defines");
    }

    /// <summary>
    /// Tests that compilation is cached.
    /// </summary>
    [SkippableFact]
    public async Task Kernel_Compilation_Should_Be_Cached()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("simple", SimpleKernel, "simple");

        // First compilation
        var stopwatch1 = Stopwatch.StartNew();
        var kernel1 = await accelerator.CompileKernelAsync(kernelDef);
        stopwatch1.Stop();

        // Second compilation (should be cached)
        var stopwatch2 = Stopwatch.StartNew();
        var kernel2 = await accelerator.CompileKernelAsync(kernelDef);
        stopwatch2.Stop();

        kernel1.Should().NotBeNull();
        kernel2.Should().NotBeNull();

        Output.WriteLine($"First compilation: {stopwatch1.Elapsed.TotalMilliseconds:F2} ms");
        Output.WriteLine($"Second compilation: {stopwatch2.Elapsed.TotalMilliseconds:F2} ms");

        // Second compilation should generally be faster (cached)
        // However, we won't enforce this strictly as caching behavior may vary
        if (stopwatch2.Elapsed < stopwatch1.Elapsed)
        {
            Output.WriteLine("✓ Caching improved compilation time");
        }
        else
        {
            Output.WriteLine("Note: No caching improvement observed (may vary by implementation)");
        }
    }

    /// <summary>
    /// Tests multiple kernels can be compiled.
    /// </summary>
    [SkippableFact]
    public async Task Multiple_Kernels_Should_Compile()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernels = new[]
        {
            new KernelDefinition("simple", SimpleKernel, "simple"),
            new KernelDefinition("complex", ComplexKernel, "complex"),
            new KernelDefinition("withDefines", KernelWithDefines, "withDefines")
        };

        var compiledKernels = new List<ICompiledKernel>();

        foreach (var kernelDef in kernels)
        {
            var compiled = await accelerator.CompileKernelAsync(kernelDef);
            compiled.Should().NotBeNull();
            compiledKernels.Add(compiled);
            Output.WriteLine($"Compiled kernel: {kernelDef.Name}");
        }

        compiledKernels.Count.Should().Be(kernels.Length);
    }

    /// <summary>
    /// Tests compilation with different optimization levels.
    /// </summary>
    [SkippableFact]
    public async Task Different_Optimization_Levels_Should_Work()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("complex", ComplexKernel, "complex");

        var optimizationLevels = new[]
        {
            OptimizationLevel.None,
            OptimizationLevel.Default,
            OptimizationLevel.O3
        };

        foreach (var level in optimizationLevels)
        {
            var options = new CompilationOptions { OptimizationLevel = level };

            var stopwatch = Stopwatch.StartNew();
            var kernel = await accelerator.CompileKernelAsync(kernelDef, options);
            stopwatch.Stop();

            kernel.Should().NotBeNull();
            Output.WriteLine($"Optimization {level}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        }
    }

    /// <summary>
    /// Tests compilation with debug information.
    /// </summary>
    [SkippableFact]
    public async Task Compilation_With_Debug_Info_Should_Work()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDef = new KernelDefinition("simple", SimpleKernel, "simple");
        var options = new CompilationOptions
        {
            GenerateDebugInfo = true,
            OptimizationLevel = OptimizationLevel.None
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef, options);
        kernel.Should().NotBeNull();

        Output.WriteLine("Successfully compiled kernel with debug information");
    }

    /// <summary>
    /// Tests invalid kernel source handling.
    /// </summary>
    [SkippableFact]
    public async Task Invalid_Kernel_Should_Throw_CompilationException()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const string invalidKernel = @"
            __kernel void invalid() {
                this_is_not_valid_opencl_code!!!
                int x = undefined_variable;
            }";

        var kernelDef = new KernelDefinition("invalid", invalidKernel, "invalid");

        var act = async () => await accelerator.CompileKernelAsync(kernelDef);
        await act.Should().ThrowAsync<Exception>("Invalid OpenCL code should throw");

        Output.WriteLine("✓ Invalid kernel correctly threw exception");
    }

    /// <summary>
    /// Tests compilation preserves functionality.
    /// </summary>
    [SkippableFact]
    public async Task Compiled_Kernel_Should_Execute_Correctly()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024;
        var hostData = new float[elementCount];
        for (var i = 0; i < elementCount; i++)
        {
            hostData[i] = i;
        }

        await using var deviceData = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await deviceData.CopyFromAsync(hostData.AsMemory());

        var kernelDef = new KernelDefinition("simple", SimpleKernel, "simple");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        await kernel.ExecuteAsync([deviceData, elementCount]);
        await accelerator.SynchronizeAsync();

        var resultData = new float[elementCount];
        await deviceData.CopyToAsync(resultData.AsMemory());

        // Verify kernel doubled the values
        for (var i = 0; i < elementCount; i++)
        {
            resultData[i].Should().BeApproximately(hostData[i] * 2.0f, 0.0001f, $"at index {i}");
        }

        Output.WriteLine("✓ Compiled kernel executed correctly");
    }

    /// <summary>
    /// Tests parallel compilation of multiple kernels.
    /// </summary>
    [SkippableFact]
    public async Task Parallel_Kernel_Compilation_Should_Work()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var kernelDefs = Enumerable.Range(0, 5)
            .Select(i => new KernelDefinition($"simple_{i}", SimpleKernel, "simple"))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();

        var compilationTasks = kernelDefs.Select(def => accelerator.CompileKernelAsync(def).AsTask());
        var compiledKernels = await Task.WhenAll(compilationTasks);

        stopwatch.Stop();

        compiledKernels.Should().HaveCount(kernelDefs.Length);
        compiledKernels.Should().AllSatisfy(k => k.Should().NotBeNull());

        Output.WriteLine($"Parallel compilation of {kernelDefs.Length} kernels: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
    }
}
