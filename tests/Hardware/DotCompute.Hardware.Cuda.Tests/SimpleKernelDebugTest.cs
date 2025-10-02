using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Tests.Common.Specialized;
using DotCompute.Core.Extensions;

namespace DotCompute.Hardware.Cuda.Tests;

public class SimpleKernelDebugTest : CudaTestBase
{
    public SimpleKernelDebugTest(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Simplest_Kernel_Should_Set_Values()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");


        const int size = 256;
        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        // Create GPU buffer

        await using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(size);

        // Simplest possible kernel - just set each element to a constant

        const string simpleKernel = @"
            extern ""C"" __global__ void SetConstant(float* output, int count)
            {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < count)
                {
                    output[idx] = 42.0f;
                }
            }";


        await using var kernel = await accelerator.CompileKernelAsync(
            new KernelDefinition
            {

                Name = "SetConstant",

                Source = simpleKernel,
                EntryPoint = "SetConstant"
            });

        // Launch kernel

        var grid = new LaunchConfiguration
        {
            GridSize = new Dim3(1),
            BlockSize = new Dim3(256)
        };


        var args = new KernelArguments();
        args.SetLaunchConfiguration(grid);
        args.Add(outputBuffer);
        args.Add(size);


        Output.WriteLine($"Launching simplest kernel with {args.Count} arguments");
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        Output.WriteLine("Kernel execution completed");

        // Verify results

        var results = new float[size];
        await outputBuffer.CopyToAsync(results);


        Output.WriteLine($"First 10 results: {string.Join(", ", results.Take(10).Select(x => x.ToString("F1")))}");
        Output.WriteLine($"All zeros? {results.All(x => x == 0)}");
        Output.WriteLine($"All 42? {results.All(x => Math.Abs(x - 42.0f) < 0.001f)}");

        _ = results.Should().AllSatisfy(x => x.Should().BeApproximately(42.0f, 0.001f));
    }


    [SkippableFact]
    public async Task Kernel_With_Input_Should_Double_Values()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");


        const int size = 256;
        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        // Create test data

        var inputData = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

        // Create GPU buffers

        await using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(size);
        await using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(size);

        // Upload data

        await inputBuffer.CopyFromAsync(inputData);

        // Simple kernel that doubles values

        const string doubleKernel = @"
            extern ""C"" __global__ void DoubleValues(float* input, float* output, int count)
            {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < count)
                {
                    output[idx] = input[idx] * 2.0f;
                }
            }";


        await using var kernel = await accelerator.CompileKernelAsync(
            new KernelDefinition
            {

                Name = "DoubleValues",

                Source = doubleKernel,
                EntryPoint = "DoubleValues"
            });

        // Launch kernel

        var grid = new LaunchConfiguration
        {
            GridSize = new Dim3(1),
            BlockSize = new Dim3(256)
        };


        var args = new KernelArguments();
        args.SetLaunchConfiguration(grid);
        args.Add(inputBuffer);
        args.Add(outputBuffer);
        args.Add(size);


        Output.WriteLine($"Launching double kernel with {args.Count} arguments");
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        Output.WriteLine("Kernel execution completed");

        // Verify results

        var results = new float[size];
        await outputBuffer.CopyToAsync(results);


        Output.WriteLine($"First 10 inputs: {string.Join(", ", inputData.Take(10).Select(x => x.ToString("F1")))}");
        Output.WriteLine($"First 10 results: {string.Join(", ", results.Take(10).Select(x => x.ToString("F1")))}");
        Output.WriteLine($"All zeros? {results.All(x => x == 0)}");


        for (var i = 0; i < Math.Min(10, size); i++)
        {
            var expected = inputData[i] * 2.0f;
            Output.WriteLine($"[{i}] Input: {inputData[i]:F1}, Expected: {expected:F1}, Got: {results[i]:F1}");
        }

        _ = results.Should().BeEquivalentTo(inputData.Select(x => x * 2.0f), options =>

            options.Using<float>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.001f))
                   .WhenTypeIs<float>());
    }
}