using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Core.Extensions;
using DotCompute.Tests.Common.Specialized;

namespace DotCompute.Hardware.Cuda.Tests;

public class ArgumentPassingDebugTest : CudaTestBase
{
    public ArgumentPassingDebugTest(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Simple_Argument_Passing_Should_Work()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");


        const int size = 1024;
        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        // Create test data

        var inputData = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var expectedOutput = new float[size];
        for (var i = 0; i < size; i++)
        {
            expectedOutput[i] = inputData[i] * 2.0f + 5.0f;
        }

        // Create GPU buffers

        await using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(size);
        await using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(size);

        // Upload data

        await inputBuffer.CopyFromAsync(inputData);

        // Simple kernel that tests all argument types

        const string testKernel = @"
            extern ""C"" __global__ void TestArguments(float* input, float* output, float scale, int offset, int count)
            {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < count)
                {
                    output[idx] = input[idx] * scale + (float)offset;
                }
            }";


        await using var kernel = await accelerator.CompileKernelAsync(
            new KernelDefinition
            {

                Name = "TestArguments",

                Source = testKernel,
                EntryPoint = "TestArguments"
            });

        // Launch kernel with mixed argument types

        var gridSize = (size + 255) / 256;
        var grid = new LaunchConfiguration
        {
            GridSize = new Dim3(gridSize),
            BlockSize = new Dim3(256)
        };
        Output.WriteLine($"Grid size: {gridSize}, Block size: 256, Total threads: {gridSize * 256}");


        var args = new KernelArguments();
        args.SetLaunchConfiguration(grid);
        args.Add(inputBuffer);    // float* input
        args.Add(outputBuffer);   // float* output  
        args.Add(2.0f);          // float scale
        args.Add(5);             // int offset
        args.Add(size);          // int count


        Output.WriteLine($"Launching kernel with {args.Count} arguments");
        Output.WriteLine($"Grid: {grid.GridSize.X}x{grid.GridSize.Y}x{grid.GridSize.Z}");
        Output.WriteLine($"Block: {grid.BlockSize.X}x{grid.BlockSize.Y}x{grid.BlockSize.Z}");


        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();


        Output.WriteLine("Kernel execution completed");

        // Verify results

        var results = new float[size];
        await outputBuffer.CopyToAsync(results);

        // Debug output

        Output.WriteLine($"First 10 results: {string.Join(", ", results.Take(10).Select(x => x.ToString("F2")))}");

        _ = results.Should().BeEquivalentTo(expectedOutput, options =>

            options.Using<float>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.001f))
                   .WhenTypeIs<float>());


        Output.WriteLine($"Test passed! Successfully passed {args.Count} arguments of mixed types.");
    }


    [SkippableFact]
    public async Task Matrix_Style_Arguments_Should_Work()
    {
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");


        const int size = 64; // Small 64x64 matrix
        const int elementCount = size * size;


        using var factory = new CudaAcceleratorFactory();
        await using var accelerator = factory.CreateProductionAccelerator(0);

        // Create simple identity copy kernel

        const string copyKernel = @"
            extern ""C"" __global__ void MatrixCopy(float* input, float* output, int N)
            {
                int row = blockIdx.y * blockDim.y + threadIdx.y;
                int col = blockIdx.x * blockDim.x + threadIdx.x;
                
                if (row < N && col < N)
                {
                    int idx = row * N + col;
                    output[idx] = input[idx];
                }
            }";


        await using var kernel = await accelerator.CompileKernelAsync(
            new KernelDefinition
            {

                Name = "MatrixCopy",

                Source = copyKernel,
                EntryPoint = "MatrixCopy"
            });

        // Create test data

        var inputData = Enumerable.Range(0, elementCount).Select(i => (float)i).ToArray();

        // Create GPU buffers

        await using var inputBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var outputBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

        // Upload data

        await inputBuffer.CopyFromAsync(inputData);

        // Launch with 2D grid like matrix multiplication

        var gridX = (size + 15) / 16;
        var gridY = (size + 15) / 16;
        var grid = new LaunchConfiguration
        {
            GridSize = new Dim3(gridX, gridY),
            BlockSize = new Dim3(16, 16)
        };
        Output.WriteLine($"2D Grid: {gridX}x{gridY}, Block: 16x16");


        var args = new KernelArguments();
        args.SetLaunchConfiguration(grid);
        args.Add(inputBuffer);
        args.Add(outputBuffer);
        args.Add(size);


        Output.WriteLine($"Launching kernel with {args.Count} arguments");
        Output.WriteLine($"Grid: {grid.GridSize.X}x{grid.GridSize.Y}x{grid.GridSize.Z}");
        Output.WriteLine($"Block: {grid.BlockSize.X}x{grid.BlockSize.Y}x{grid.BlockSize.Z}");


        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();


        Output.WriteLine("Kernel execution completed");

        // Verify results

        var results = new float[elementCount];
        await outputBuffer.CopyToAsync(results);

        _ = results.Should().BeEquivalentTo(inputData, options =>

            options.Using<float>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.001f))
                   .WhenTypeIs<float>());


        Output.WriteLine($"Matrix copy test passed for {size}x{size} matrix!");
    }
}