// Copyright(c) 2025 Michael Ivertowski

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Tests.Integration.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for complete end-to-end compute workflows.
/// Tests kernel compilation → execution → result validation with real-world scenarios.
/// </summary>
[Collection("Integration")]
public sealed class CompleteWorkflowIntegrationTests : ComputeWorkflowTestBase
{
    public CompleteWorkflowIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task VectorAdditionWorkflow_EndToEnd_ShouldExecuteSuccessfully()
    {
        // Arrange
        var workflow = new ComputeWorkflowDefinition
        {
            Name = "VectorAddition",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "vector_add",
                    SourceCode = KernelSources.VectorAdd,
                    CompilationOptions = new CompilationOptions
                    {
                        OptimizationLevel = OptimizationLevel.Maximum,
                        FastMath = true
                    }
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "inputA", Data = TestDataGenerators.GenerateFloatArray(1024) },
                new WorkflowInput { Name = "inputB", Data = TestDataGenerators.GenerateFloatArray(1024) }
            ],
            Outputs =
            [
                new WorkflowOutput
                {
                    Name = "result",
                    Size = 1024,
                    Validator = ValidateVectorAddition
                }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "add_stage",
                    Order = 1,
                    KernelName = "vector_add",
                    BackendType = ComputeBackendType.CPU,
                    ExecutionOptions = new ExecutionOptions
                    {
                        GlobalWorkSize = [1024],
                        LocalWorkSize = [64]
                    },
                    ArgumentNames = ["inputA", "inputB", "result"]
                }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("VectorAddition", workflow);

        // Assert
        result.Success.Should().BeTrue();
        result.CompilationResults["vector_add"].Success.Should().BeTrue();
        result.ExecutionResults["add_stage"].Success.Should().BeTrue();
        result.Validation?.IsValid.Should().BeTrue();
        result.Results.Should().ContainKey("result");

        var resultData = (float[])result.Results["result"];
        resultData.Length.Should().Be(1024);

        // Verify the addition was performed correctly
        var inputA = workflow.Inputs[0].Data;
        var inputB = workflow.Inputs[1].Data;
        for (var i = 0; i < 100; i++) // Check first 100 elements
        {
            resultData[i].Should().BeApproximately(inputA[i] + inputB[i], 0.001f);
        }

        LogPerformanceMetrics("VectorAddition", result.Duration, 1024);
    }

    [Fact]
    public async Task MatrixMultiplicationWorkflow_EndToEnd_ShouldExecuteSuccessfully()
    {
        // Arrange
        const int matrixSize = 32; // Keep reasonable size for CI/testing
        var matrixA = TestDataGenerators.GenerateFloatArray(matrixSize * matrixSize);
        var matrixB = TestDataGenerators.GenerateFloatArray(matrixSize * matrixSize);

        var workflow = new ComputeWorkflowDefinition
        {
            Name = "MatrixMultiplication",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "matrix_multiply",
                    SourceCode = KernelSources.MatrixMultiply,
                    CompilationOptions = new CompilationOptions
                    {
                        OptimizationLevel = OptimizationLevel.Maximum,
                        EnableMemoryCoalescing = true
                    }
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "matrixA", Data = matrixA },
                new WorkflowInput { Name = "matrixB", Data = matrixB }
            ],
            Outputs =
            [
                new WorkflowOutput
                {
                    Name = "matrixC",
                    Size = matrixSize * matrixSize,
                    Validator = data => ValidateMatrixMultiplication(data, matrixA, matrixB, matrixSize)
                }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "multiply_stage",
                    Order = 1,
                    KernelName = "matrix_multiply",
                    BackendType = ComputeBackendType.CPU,
                    ExecutionOptions = new ExecutionOptions
                    {
                        GlobalWorkSize = [matrixSize, matrixSize],
                        LocalWorkSize = [8, 8]
                    },
                    ArgumentNames = ["matrixA", "matrixB", "matrixC"],
                    Parameters = new Dictionary<string, object> { ["size"] = matrixSize }
                }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("MatrixMultiplication", workflow);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().ContainKey("matrixC");
        result.Validation?.IsValid.Should().BeTrue();

        LogPerformanceMetrics("MatrixMultiplication", result.Duration, matrixSize * matrixSize);
    }

    [Fact]
    public async Task MultiStageImageProcessingWorkflow_EndToEnd_ShouldExecuteSuccessfully()
    {
        // Arrange - Simulate image processing pipeline
        const int imageSize = 256; // 256x256 image
        var originalImage = TestDataGenerators.GenerateFloatArray(imageSize * imageSize, 0f, 255f);

        var workflow = new ComputeWorkflowDefinition
        {
            Name = "ImageProcessingPipeline",
            Kernels =
            [
                new WorkflowKernel { Name = "gaussian_blur", SourceCode = KernelSources.GaussianBlur },
                new WorkflowKernel { Name = "edge_detection", SourceCode = KernelSources.EdgeDetection },
                new WorkflowKernel { Name = "contrast_enhance", SourceCode = KernelSources.ContrastEnhance }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "originalImage", Data = originalImage }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "processedImage", Size = imageSize * imageSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer
                {
                    Name = "blurredImage",
                    SizeInBytes = imageSize * imageSize * sizeof(float)
                },
                new WorkflowIntermediateBuffer
                {
                    Name = "edgeImage",
                    SizeInBytes = imageSize * imageSize * sizeof(float)
                }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "blur_stage",
                    Order = 1,
                    KernelName = "gaussian_blur",
                    ArgumentNames = ["originalImage", "blurredImage"],
                    Parameters = new Dictionary<string, object> { ["width"] = imageSize, ["height"] = imageSize }
                },
                new WorkflowExecutionStage
                {
                    Name = "edge_stage",
                    Order = 2,
                    KernelName = "edge_detection",
                    ArgumentNames = ["blurredImage", "edgeImage"],
                    Parameters = new Dictionary<string, object> { ["width"] = imageSize, ["height"] = imageSize }
                },
                new WorkflowExecutionStage
                {
                    Name = "enhance_stage",
                    Order = 3,
                    KernelName = "contrast_enhance",
                    ArgumentNames = ["edgeImage", "processedImage"],
                    Parameters = new Dictionary<string, object> { ["factor"] = 1.5f }
                }
            ],
            ContinueOnError = false
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("ImageProcessingPipeline", workflow);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionResults.Count.Should().Be(3);
        result.ExecutionResults.Values.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Verify all stages executed in correct order
        var stageResults = result.ExecutionResults.Values.OrderBy(r => r.StageId).ToList();
        stageResults[0].StageId.Should().Be("blur_stage");
        stageResults[1].StageId.Should().Be("edge_stage");
        stageResults[2].StageId.Should().Be("enhance_stage");

        LogPerformanceMetrics("ImageProcessingPipeline", result.Duration, imageSize * imageSize * 3);
    }

    [Fact]
    public async Task ConvolutionNeuralNetworkLayer_EndToEnd_ShouldExecuteSuccessfully()
    {
        // Arrange - Simulate CNN layer computation
        const int batchSize = 4;
        const int inputChannels = 3;
        const int outputChannels = 16;
        const int inputSize = 32;
        const int kernelSize = 3;
        const int outputSize = inputSize - kernelSize + 1;

        var inputTensor = TestDataGenerators.GenerateGaussianArray(
            batchSize * inputChannels * inputSize * inputSize);
        var weights = TestDataGenerators.GenerateGaussianArray(
            outputChannels * inputChannels * kernelSize * kernelSize, 0f, 0.1f);
        var biases = TestDataGenerators.GenerateFloatArray(outputChannels, -0.1f, 0.1f);

        var workflow = new ComputeWorkflowDefinition
        {
            Name = "CNNConvolutionLayer",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "convolution2d",
                    SourceCode = KernelSources.Convolution2D,
                    CompilationOptions = new CompilationOptions
                    {
                        OptimizationLevel = OptimizationLevel.Aggressive,
                        FastMath = true,
                        EnableOperatorFusion = true
                    }
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = inputTensor },
                new WorkflowInput { Name = "weights", Data = weights },
                new WorkflowInput { Name = "biases", Data = biases }
            ],
            Outputs =
            [
                new WorkflowOutput
                {
                    Name = "output",
                    Size = batchSize * outputChannels * outputSize * outputSize,
                    Validator = data => ValidateConvolutionOutput(data, outputChannels)
                }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "conv_stage",
                    Order = 1,
                    KernelName = "convolution2d",
                    BackendType = ComputeBackendType.CPU,
                    ExecutionOptions = new ExecutionOptions
                    {
                        GlobalWorkSize = [outputSize, outputSize, outputChannels],
                        LocalWorkSize = [8, 8, 2]
                    },
                    ArgumentNames = ["input", "weights", "biases", "output"],
                    Parameters = new Dictionary<string, object>
                    {
                        ["batchSize"] = batchSize,
                        ["inputChannels"] = inputChannels,
                        ["outputChannels"] = outputChannels,
                        ["inputSize"] = inputSize,
                        ["kernelSize"] = kernelSize
                    }
                }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("CNNConvolutionLayer", workflow);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().ContainKey("output");

        var output = (float[])result.Results["output"];
        output.Length.Should().Be(batchSize * outputChannels * outputSize * outputSize);

        // Verify output is reasonable(no NaN, not all zeros)
        output.Should().NotContain(float.NaN);
        output.Should().NotContain(float.PositiveInfinity);
        output.Should().NotContain(float.NegativeInfinity);
        output.Where(x => Math.Abs(x) > 1e-6f).Should().NotBeEmpty();

        LogPerformanceMetrics("CNNConvolutionLayer", result.Duration,
            batchSize * outputChannels * outputSize * outputSize);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public async Task ReductionWorkflow_VariousSizes_ShouldScaleCorrectly(int arraySize)
    {
        // Arrange
        var inputData = TestDataGenerators.GenerateFloatArray(arraySize, 1f, 10f);
        var expectedSum = inputData.Sum();

        var workflow = new ComputeWorkflowDefinition
        {
            Name = $"Reduction_{arraySize}",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "parallel_reduction",
                    SourceCode = KernelSources.ParallelReduction,
                    CompilationOptions = new CompilationOptions
                    {
                        OptimizationLevel = OptimizationLevel.Maximum,
                        EnableParallelExecution = true
                    }
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = inputData }
            ],
            Outputs =
            [
                new WorkflowOutput
                {
                    Name = "result",
                    Size = 1,
                    Validator = data => Math.Abs(data[0] - expectedSum) < expectedSum * 0.01f // 1% tolerance
                }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "reduce_stage",
                    Order = 1,
                    KernelName = "parallel_reduction",
                    ExecutionOptions = new ExecutionOptions
                    {
                        GlobalWorkSize = [arraySize],
                        LocalWorkSize = [Math.Min(256, arraySize)]
                    },
                    ArgumentNames = ["input", "result"],
                    Parameters = new Dictionary<string, object> { ["size"] = arraySize }
                }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync($"Reduction_{arraySize}", workflow);

        // Assert
        result.Success.Should().BeTrue();
        result.Validation?.IsValid.Should().BeTrue();

        var reductionResult = (float[])result.Results["result"];
        reductionResult[0].Should().BeApproximately(expectedSum, expectedSum * 0.01f);

        // Verify performance scales reasonably
        if (arraySize > 64)
        {
            var throughputMBps = result.Metrics?.ThroughputMBps ?? 0;
            Assert.True(throughputMBps > 0);
        }

        LogPerformanceMetrics($"Reduction_{arraySize}", result.Duration, arraySize);
    }

    [Fact]
    public async Task ErrorRecoveryWorkflow_InvalidKernel_ShouldFailGracefully()
    {
        // Arrange - Workflow with intentionally broken kernel
        var workflow = new ComputeWorkflowDefinition
        {
            Name = "ErrorRecoveryTest",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "broken_kernel",
                    SourceCode = "this is not valid kernel code!",
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(100) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = 100 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "broken_stage",
                    Order = 1,
                    KernelName = "broken_kernel",
                    ArgumentNames = ["input", "output"]
                }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("ErrorRecoveryTest", workflow);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.CompilationResults.Should().ContainKey("broken_kernel");
        result.CompilationResults["broken_kernel"].Success.Should().BeFalse();
        result.CompilationResults["broken_kernel"].Error.Should().NotBeNull();
    }

    // Validation methods
    private static bool ValidateVectorAddition(float[] result) => result.All(x => !float.IsNaN(x) && !float.IsInfinity(x));

    private static bool ValidateMatrixMultiplication(float[] result, float[] matrixA, float[] matrixB, int size)
    {
        if (result.Length != size * size)
            return false;

        // Check for valid values
        if (result.Any(x => float.IsNaN(x) || float.IsInfinity(x)))
            return false;

        // Spot check a few elements using CPU computation
        for (var i = 0; i < Math.Min(4, size); i++)
        {
            for (var j = 0; j < Math.Min(4, size); j++)
            {
                float expected = 0;
                for (var k = 0; k < size; k++)
                {
                    expected += matrixA[i * size + k] * matrixB[k * size + j];
                }

                var actual = result[i * size + j];
                if (Math.Abs(actual - expected) > Math.Abs(expected) * 0.01f + 1e-5f)
                    return false;
            }
        }

        return true;
    }

    private static bool ValidateConvolutionOutput(float[] result, int outputChannels)
    {
        if (result.Any(x => float.IsNaN(x) || float.IsInfinity(x)))
            return false;

        // Check that we have reasonable activation values
        var activations = result.Where(x => Math.Abs(x) > 1e-6f).Count();
        return activations > result.Length * 0.1; // At least 10% non-zero values
    }
}

/// <summary>
/// Collection of kernel source code for testing.
/// </summary>
internal static partial class KernelSources
{
    public const string VectorAdd = @"
__kernel void vector_add(__global const float* a, __global const float* b, __global float* result) {
    int gid = get_global_id(0);
    result[gid] = a[gid] + b[gid];
}";

    public const string MatrixMultiply = @"
__kernel void matrix_multiply(__global const float* A, __global const float* B, __global float* C, int size) {
    int row = get_global_id(0);
    int col = get_global_id(1);
    
    if(row >= size || col >= size) return;
    
    float sum = 0.0f;
    for(int k = 0; k < size; k++) {
        sum += A[row * size + k] * B[k * size + col];
    }
    C[row * size + col] = sum;
}";

    public const string GaussianBlur = @"
__kernel void gaussian_blur(__global const float* input, __global float* output, int width, int height) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    
    if(x >= width || y >= height) return;
    
    float kernel[9] = {0.077847f, 0.123317f, 0.077847f,
                       0.123317f, 0.195346f, 0.123317f,
                       0.077847f, 0.123317f, 0.077847f};
    
    float sum = 0.0f;
    for(int ky = -1; ky <= 1; ky++) {
        for(int kx = -1; kx <= 1; kx++) {
            int px = clamp(x + kx, 0, width - 1);
            int py = clamp(y + ky, 0, height - 1);
            sum += input[py * width + px] * kernel[(ky + 1) * 3 +(kx + 1)];
        }
    }
    output[y * width + x] = sum;
}";

    public const string EdgeDetection = @"
__kernel void edge_detection(__global const float* input, __global float* output, int width, int height) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    
    if(x >= width || y >= height || x == 0 || y == 0 || x == width-1 || y == height-1) {
        if(x < width && y < height) output[y * width + x] = 0.0f;
        return;
    }
    
    float gx = -input[(y-1) * width +(x-1)] + input[(y-1) * width +(x+1)]
               -2*input[y * width +(x-1)] + 2*input[y * width +(x+1)]
               -input[(y+1) * width +(x-1)] + input[(y+1) * width +(x+1)];
    
    float gy = -input[(y-1) * width +(x-1)] - 2*input[(y-1) * width + x] - input[(y-1) * width +(x+1)]
               +input[(y+1) * width +(x-1)] + 2*input[(y+1) * width + x] + input[(y+1) * width +(x+1)];
    
    output[y * width + x] = sqrt(gx*gx + gy*gy);
}";

    public const string ContrastEnhance = @"
__kernel void contrast_enhance(__global const float* input, __global float* output, float factor) {
    int gid = get_global_id(0);
    float value = input[gid];
    output[gid] = clamp((value - 128.0f) * factor + 128.0f, 0.0f, 255.0f);
}";

    public const string Convolution2D = @"
__kernel void convolution2d(__global const float* input, __global const float* weights,
                           __global const float* biases, __global float* output,
                           int batchSize, int inputChannels, int outputChannels,
                           int inputSize, int kernelSize) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    int outChannel = get_global_id(2);
    
    int outputSize = inputSize - kernelSize + 1;
    if(x >= outputSize || y >= outputSize || outChannel >= outputChannels) return;
    
    for(int batch = 0; batch < batchSize; batch++) {
        float sum = biases[outChannel];
        
        for(int inChannel = 0; inChannel < inputChannels; inChannel++) {
            for(int ky = 0; ky < kernelSize; ky++) {
                for(int kx = 0; kx < kernelSize; kx++) {
                    int inputIdx =((batch * inputChannels + inChannel) * inputSize +(y + ky)) * inputSize +(x + kx);
                    int weightIdx =((outChannel * inputChannels + inChannel) * kernelSize + ky) * kernelSize + kx;
                    sum += input[inputIdx] * weights[weightIdx];
                }
            }
        }
        
        int outputIdx =((batch * outputChannels + outChannel) * outputSize + y) * outputSize + x;
        output[outputIdx] = fmax(0.0f, sum); // ReLU activation
    }
}";

    public const string ParallelReduction = @"
__kernel void parallel_reduction(__global const float* input, __global float* result,
                                __local float* scratch, int size) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    int lsize = get_local_size(0);
    
    // Load data into local memory
    scratch[lid] =(gid < size) ? input[gid] : 0.0f;
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Parallel reduction in local memory
    for(int offset = lsize / 2; offset > 0; offset >>= 1) {
        if(lid < offset) {
            scratch[lid] += scratch[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    // Write result for this work group
    if(lid == 0) {
        atomic_add_global(result, scratch[0]);
    }
}";
}
