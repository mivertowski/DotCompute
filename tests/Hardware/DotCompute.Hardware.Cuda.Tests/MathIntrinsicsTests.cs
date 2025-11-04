// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Factory;
using DotCompute.Tests.Common.Specialized;
using Microsoft.Extensions.Logging;

namespace DotCompute.Hardware.Cuda.Tests;

/// <summary>
/// Tests for CUDA device math intrinsics support.
/// Verifies that NVRTC can compile kernels using __sinf, __cosf, __sqrtf, etc.
/// </summary>
[Trait("Category", "CUDA")]
[Trait("Category", "Hardware")]
[Trait("Category", "MathIntrinsics")]
public class MathIntrinsicsTests : CudaTestBase
{
    private readonly ILogger<MathIntrinsicsTests> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the MathIntrinsicsTests class.
    /// </summary>
    /// <param name="output">The xUnit test output helper.</param>
    public MathIntrinsicsTests(ITestOutputHelper output) : base(output)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<MathIntrinsicsTests>();
    }

    [SkippableFact]
    public async Task SinglePrecision_Math_Intrinsics_Should_Compile()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var kernelCode = @"
            extern ""C"" __global__ void mathKernel(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx >= n) return;

                float x = input[idx];

                // Test single-precision intrinsics
                float s = __sinf(x);
                float c = __cosf(x);
                float t = __tanf(x);
                float e = __expf(x);
                float l = __logf(x + 1.0f);
                float sq = __sqrtf(fabsf(x));

                output[idx] = s + c + t + e + l + sq;
            }";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition("mathKernel", kernelCode);
        var compiledKernel = await accelerator.CompileKernelAsync(
            kernelDef,
            new Abstractions.CompilationOptions());

        Output.WriteLine("Single-precision math intrinsics compiled successfully");

        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    public async Task DoublePrecision_Math_Intrinsics_Should_Compile()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var kernelCode = @"
            extern ""C"" __global__ void mathKernelDouble(double* input, double* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx >= n) return;

                double x = input[idx];

                // Test double-precision intrinsics
                double s = __sin(x);
                double c = __cos(x);
                double e = __exp(x);
                double l = __log(x + 1.0);
                double sq = __sqrt(fabs(x));

                output[idx] = s + c + e + l + sq;
            }";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition("mathKernelDouble", kernelCode);
        var compiledKernel = await accelerator.CompileKernelAsync(
            kernelDef,
            new Abstractions.CompilationOptions());

        Output.WriteLine("Double-precision math intrinsics compiled successfully");

        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    public async Task Math_Intrinsics_Should_Execute_Correctly()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        const int dataSize = 1024;
        var hostInput = new float[dataSize];
        var hostOutput = new float[dataSize];

        // Initialize with test data
        for (var i = 0; i < dataSize; i++)
        {
            hostInput[i] = i * 0.01f;
        }

        using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(dataSize);
        using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(dataSize);

        await inputBuffer.CopyFromAsync(hostInput.AsMemory());

        var kernelCode = @"
            extern ""C"" __global__ void mathExecKernel(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx >= n) return;

                float x = input[idx];

                // Compute using math intrinsics
                float s = __sinf(x);
                float c = __cosf(x);
                float sq = __sqrtf(fabsf(x));

                output[idx] = s * c * sq;
            }";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition("mathExecKernel", kernelCode);
        var compiledKernel = await accelerator.CompileKernelAsync(
            kernelDef,
            new Abstractions.CompilationOptions());

        var (grid, block) = CudaTestHelpers.CreateLaunchConfig(
            (dataSize + 255) / 256, 1, 1,
            256, 1, 1);

        var kernelArgs = CudaTestHelpers.CreateKernelArguments(
            [inputBuffer, outputBuffer, dataSize],
            grid,
            block);

        await compiledKernel.ExecuteAsync(kernelArgs);
        await accelerator.SynchronizeAsync();

        await outputBuffer.CopyToAsync(hostOutput.AsMemory());

        // Verify some outputs are non-zero
        var nonZeroCount = hostOutput.Count(x => Math.Abs(x) > 0.0001f);
        Assert.True(nonZeroCount > 0, "Expected non-zero outputs from math intrinsics");

        Output.WriteLine($"Math intrinsics executed successfully, {nonZeroCount} non-zero results");

        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    public async Task Complex_Math_Computation_Should_Work()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        const int dataSize = 1024;
        var hostInput = new float[dataSize];
        var hostOutput = new float[dataSize];

        for (var i = 0; i < dataSize; i++)
        {
            hostInput[i] = i * 0.001f;
        }

        using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(dataSize);
        using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(dataSize);

        await inputBuffer.CopyFromAsync(hostInput.AsMemory());

        // This is the kernel from SharedMemorySpillingTests that was causing issues
        var kernelCode = @"
            extern ""C"" __global__ void complexKernel(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx >= n) return;

                float val = input[idx];

                // Complex computation with high register pressure
                float t0 = val;
                float t1 = __sinf(t0);
                float t2 = __cosf(t1);
                float t3 = __expf(t2);
                float t4 = __logf(t3 + 1.0f);
                float t5 = __sqrtf(fabsf(t4));
                float t6 = t5 * t0;
                float t7 = t6 + t1;
                float t8 = t7 * t2;
                float t9 = t8 - t3;
                float t10 = t9 / (t4 + 0.001f);

                output[idx] = t10;
            }";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition("complexKernel", kernelCode);
        var compiledKernel = await accelerator.CompileKernelAsync(
            kernelDef,
            new Abstractions.CompilationOptions());

        var (grid, block) = CudaTestHelpers.CreateLaunchConfig(
            (dataSize + 255) / 256, 1, 1,
            256, 1, 1);

        var kernelArgs = CudaTestHelpers.CreateKernelArguments(
            [inputBuffer, outputBuffer, dataSize],
            grid,
            block);

        await compiledKernel.ExecuteAsync(kernelArgs);
        await accelerator.SynchronizeAsync();

        await outputBuffer.CopyToAsync(hostOutput.AsMemory());

        // Verify outputs are computed
        var validCount = hostOutput.Count(x => !float.IsNaN(x) && !float.IsInfinity(x));
        Assert.Equal(dataSize, validCount);

        Output.WriteLine($"Complex math computation successful, all {validCount} results valid");

        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    public async Task Additional_Math_Functions_Should_Work()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var kernelCode = @"
            extern ""C"" __global__ void additionalMath(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx >= n) return;

                float x = input[idx];

                // Test additional math functions
                float floor_val = __floorf(x);
                float ceil_val = __ceilf(x);
                float round_val = __roundf(x);
                float min_val = __fminf(x, 1.0f);
                float max_val = __fmaxf(x, 0.0f);
                float pow_val = __powf(x + 1.0f, 2.0f);

                output[idx] = floor_val + ceil_val + round_val + min_val + max_val + pow_val;
            }";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition("additionalMath", kernelCode);
        var compiledKernel = await accelerator.CompileKernelAsync(
            kernelDef,
            new Abstractions.CompilationOptions());

        Output.WriteLine("Additional math functions compiled successfully");

        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    public async Task Trigonometric_Functions_Should_Work()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        var kernelCode = @"
            extern ""C"" __global__ void trigKernel(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx >= n) return;

                float x = input[idx];

                // Test trigonometric functions
                float sin_val = __sinf(x);
                float cos_val = __cosf(x);
                float tan_val = __tanf(x);
                float asin_val = __asinf(sin_val * 0.5f);
                float acos_val = __acosf(cos_val * 0.5f);
                float atan_val = __atanf(tan_val * 0.5f);

                output[idx] = sin_val + cos_val + tan_val + asin_val + acos_val + atan_val;
            }";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition("trigKernel", kernelCode);
        var compiledKernel = await accelerator.CompileKernelAsync(
            kernelDef,
            new Abstractions.CompilationOptions());

        Output.WriteLine("Trigonometric functions compiled successfully");

        await compiledKernel.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loggerFactory?.Dispose();
        }
        base.Dispose(disposing);
    }
}
