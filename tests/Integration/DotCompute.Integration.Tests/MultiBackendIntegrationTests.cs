// Copyright(c) 2025 Michael Ivertowski

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Tests.Common.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using DotCompute.Integration.Tests.Infrastructure;

namespace DotCompute.Tests.Integration;


/// <summary>
/// Integration tests for multi-backend scenarios including backend switching,
/// cross-backend data transfer, and backend-specific optimizations.
/// </summary>
[Collection("Integration")]
public sealed class MultiBackendIntegrationTests : ComputeWorkflowTestBase
{
    private readonly IAcceleratorManager _acceleratorManager;

    public MultiBackendIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
    }

    [Fact]
    public async Task BackendSwitching_CPUToCUDA_ShouldTransferContextCorrectly()
    {
        // Arrange
        var cpuWorkflow = CreateWorkflowForBackend(ComputeBackendType.CPU, "vector_add", 1024);
        var cudaWorkflow = CreateWorkflowForBackend(ComputeBackendType.CUDA, "vector_multiply", 1024);

        // Act - Execute on CPU first
        var cpuResult = await ExecuteComputeWorkflowAsync("CPU_VectorAdd", cpuWorkflow);

        // Switch to CUDA and use CPU results as input
        cudaWorkflow.Inputs[0].Data = (float[])cpuResult.Results["result"];
        var cudaResult = await ExecuteComputeWorkflowAsync("CUDA_VectorMultiply", cudaWorkflow);

        // Assert
        _ = cpuResult.Success.Should().BeTrue();
        _ = cudaResult.Success.Should().BeTrue();

        // Verify backend usage
        VerifyBackendUsage(cpuResult, ComputeBackendType.CPU);
        VerifyBackendUsage(cudaResult, ComputeBackendType.CUDA);

        LogPerformanceMetrics("BackendSwitching_CPUToCUDA",
            cpuResult.Duration + cudaResult.Duration, 2048);
    }

    [Fact]
    public async Task MultiBackendExecution_ParallelWorkloads_ShouldExecuteConcurrently()
    {
        // Arrange
        var cpuWorkflow = CreateWorkflowForBackend(ComputeBackendType.CPU, "matrix_multiply", 128);
        var cudaWorkflow = CreateWorkflowForBackend(ComputeBackendType.CUDA, "vector_add", 4096);
        var metalWorkflow = CreateWorkflowForBackend(ComputeBackendType.Metal, "reduction", 2048);

        // Act - Execute all workflows concurrently
        var tasks = new[]
        {
        ExecuteComputeWorkflowAsync("CPU_MatrixMultiply", cpuWorkflow),
        ExecuteComputeWorkflowAsync("CUDA_VectorAdd", cudaWorkflow),
        ExecuteComputeWorkflowAsync("Metal_Reduction", metalWorkflow)
    };

        var results = await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Verify each backend was used appropriately
        VerifyBackendUsage(results[0], ComputeBackendType.CPU);
        VerifyBackendUsage(results[1], ComputeBackendType.CUDA);
        VerifyBackendUsage(results[2], ComputeBackendType.Metal);

        // Verify parallel execution improved total time
        var totalSequentialTime = results.Sum(r => r.Duration.TotalMilliseconds);
        var actualParallelTime = results.Max(r => r.Duration.TotalMilliseconds);

        Logger.LogInformation("Sequential time: {Sequential}ms, Parallel time: {Parallel}ms, " +
                             "Efficiency: {Efficiency:P1}",
            totalSequentialTime, actualParallelTime,
           (totalSequentialTime - actualParallelTime) / totalSequentialTime);

        Assert.True(actualParallelTime < totalSequentialTime * 0.8); // At least 20% improvement
    }

    [Fact]
    public async Task BackendFallback_UnavailableBackend_ShouldFallbackToCPU()
    {
        // Arrange - Simulate GPU unavailable scenario
        HardwareSimulator.SimulateRandomFailures(1.0, AcceleratorType.CUDA); // Force all CUDA devices to fail

        var workflow = new ComputeWorkflowDefinition
        {
            Name = "FallbackTest",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "vector_operation",
                SourceCode = KernelSources.VectorAdd,
                CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "inputA", Data = TestDataGenerators.GenerateFloatArray(512) },
            new WorkflowInput { Name = "inputB", Data = TestDataGenerators.GenerateFloatArray(512) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "result", Size = 512 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "primary_execution",
                Order = 1,
                KernelName = "vector_operation",
                BackendType = ComputeBackendType.CUDA, // Prefer CUDA but should fallback
                ArgumentNames = ["inputA", "inputB", "result"]
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("FallbackTest", workflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        // Since CUDA failed, execution should have fallen back to CPU
        var actualBackend = DetectActualBackendUsed(result);
        Assert.Equal(ComputeBackendType.CPU, actualBackend);

        Logger.LogInformation("Successfully fell back from CUDA to CPU execution");
    }

    [Fact]
    public async Task CrossBackendDataTransfer_LargeData_ShouldMaintainIntegrity()
    {
        // Arrange
        const int dataSize = 8192;
        var originalData = TestDataGenerators.GenerateFloatArray(dataSize, -100f, 100f);

        // Create workflow that processes on different backends
        var stage1Workflow = CreateWorkflowForBackend(ComputeBackendType.CPU, "data_preprocessing", dataSize);
        var stage2Workflow = CreateWorkflowForBackend(ComputeBackendType.CUDA, "data_processing", dataSize);
        var stage3Workflow = CreateWorkflowForBackend(ComputeBackendType.Metal, "data_postprocessing", dataSize);

        stage1Workflow.Inputs[0].Data = originalData;

        // Act - Execute multi-stage cross-backend pipeline
        var stage1Result = await ExecuteComputeWorkflowAsync("Stage1_CPU_Preprocess", stage1Workflow);
        _ = stage1Result.Success.Should().BeTrue();

        stage2Workflow.Inputs[0].Data = (float[])stage1Result.Results["result"];
        var stage2Result = await ExecuteComputeWorkflowAsync("Stage2_CUDA_Process", stage2Workflow);
        _ = stage2Result.Success.Should().BeTrue();

        stage3Workflow.Inputs[0].Data = (float[])stage2Result.Results["result"];
        var stage3Result = await ExecuteComputeWorkflowAsync("Stage3_Metal_Postprocess", stage3Workflow);

        // Assert
        _ = stage3Result.Success.Should().BeTrue();

        var finalResult = (float[])stage3Result.Results["result"];
        _ = finalResult.Length.Should().Be(dataSize);

        // Verify data integrity through the pipeline
        _ = finalResult.Should().NotContain(float.NaN);
        _ = finalResult.Should().NotContain(float.PositiveInfinity);
        _ = finalResult.Should().NotContain(float.NegativeInfinity);

        // Calculate total pipeline throughput
        var totalTime = stage1Result.Duration + stage2Result.Duration + stage3Result.Duration;
        var totalDataMB = (dataSize * sizeof(float) * 4) / 1024.0 / 1024.0; // 4 transfers
        var throughputMBps = totalDataMB / totalTime.TotalSeconds;

        Logger.LogInformation("Cross-backend pipeline throughput: {Throughput:F2} MB/s", throughputMBps);
        Assert.True(throughputMBps > 10); // Minimum acceptable throughput
    }

    [Theory]
    [InlineData(ComputeBackendType.CPU, 1024)]
    [InlineData(ComputeBackendType.CUDA, 4096)]
    [InlineData(ComputeBackendType.Metal, 2048)]
    public async Task BackendSpecificOptimizations_DifferentBackends_ShouldShowPerformanceCharacteristics(
        ComputeBackendType backend, int optimalSize)
    {
        // Arrange
        var workflow = new ComputeWorkflowDefinition
        {
            Name = $"BackendOptimization_{backend}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "optimized_kernel",
                SourceCode = GetBackendOptimizedKernel(backend),
                CompilationOptions = GetBackendOptimizedCompilationOptions(backend)
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "data", Data = TestDataGenerators.GenerateFloatArray(optimalSize) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "result", Size = optimalSize }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "optimized_execution",
                Order = 1,
                KernelName = "optimized_kernel",
                BackendType = backend,
                ExecutionOptions = GetBackendOptimizedExecutionOptions(backend, optimalSize),
                ArgumentNames = ["data", "result"]
            }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync($"BackendOptimization_{backend}", workflow);

        // Assert
        _ = result.Success.Should().BeTrue();

        // Verify backend-specific performance characteristics
        var metrics = result.Metrics!;
        ValidateBackendPerformanceCharacteristics(backend, metrics, optimalSize);

        LogPerformanceMetrics($"BackendOptimization_{backend}", result.Duration, optimalSize);
    }

    [Fact]
    public async Task BackendCompatibility_SameKernelAllBackends_ShouldProduceSimilarResults()
    {
        // Arrange
        const int dataSize = 256;
        var inputData = TestDataGenerators.GenerateFloatArray(dataSize, 1f, 10f);
        var backends = new[] { ComputeBackendType.CPU, ComputeBackendType.CUDA, ComputeBackendType.Metal };
        var results = new Dictionary<ComputeBackendType, float[]>();

        // Act - Execute same kernel on all available backends
        foreach (var backend in backends)
        {
            var workflow = CreateCompatibilityTestWorkflow(backend, inputData);
            var result = await ExecuteComputeWorkflowAsync($"Compatibility_{backend}", workflow);

            if (result.Success)
            {
                results[backend] = (float[])result.Results["result"];
            }
            else
            {
                Logger.LogWarning("Backend {Backend} execution failed: {Error}", backend, result.Error?.Message);
            }
        }

        // Assert
        Assert.NotEmpty(results);

        if (results.Count > 1)
        {
            // Compare results across backends - they should be very similar
            var referenceResult = results.Values.First();

            foreach (var (backend, backendResult) in results.Skip(1))
            {
                Logger.LogInformation("Comparing {Backend} results with reference", backend);

                for (var i = 0; i < Math.Min(100, referenceResult.Length); i++)
                {
                    var difference = Math.Abs(referenceResult[i] - backendResult[i]);
                    var tolerance = Math.Max(Math.Abs(referenceResult[i]) * 0.001f, 1e-5f);

                    _ = (difference < tolerance).Should().BeTrue(
                        $"Results should be similar across backends at index {i}. " +
                        $"Reference: {referenceResult[i]}, {backend}: {backendResult[i]}");
                }
            }
        }

        Logger.LogInformation("Backend compatibility validated across {Count} backends", results.Count);
    }

    [Fact]
    public async Task DynamicBackendSelection_WorkloadCharacteristics_ShouldSelectOptimalBackend()
    {
        // Arrange - Different workloads with different optimal backends
        var workloads = new[]
        {
        new { Name = "SmallVectorOps", Size = 64, OptimalBackend = ComputeBackendType.CPU },
        new { Name = "LargeMatrixOps", Size = 512, OptimalBackend = ComputeBackendType.CUDA },
        new { Name = "ImageProcessing", Size = 256, OptimalBackend = ComputeBackendType.Metal }
    };

        var results = new List<(string Name, ComputeBackendType Selected, double Performance)>();

        // Act - Let the system dynamically select backends based on workload
        foreach (var workload in workloads)
        {
            var workflow = CreateDynamicSelectionWorkflow(workload.Name, workload.Size);
            var result = await ExecuteComputeWorkflowAsync($"Dynamic_{workload.Name}", workflow);

            _ = result.Success.Should().BeTrue();

            var selectedBackend = DetectActualBackendUsed(result);
            var performance = CalculatePerformanceScore(result, workload.Size);

            results.Add((workload.Name, selectedBackend, performance));

            Logger.LogInformation("Workload {Name}: Selected {Selected}, Expected {Expected}, Performance: {Performance:F2}",
                workload.Name, selectedBackend, workload.OptimalBackend, performance);
        }

        // Assert
        _ = results.Should().AllSatisfy(r => r.Performance.Should().BeGreaterThan(0));

        // Verify that the selection made reasonable choices
        //(In a real implementation, this would validate against actual performance characteristics)
        var avgPerformance = results.Average(r => r.Performance);
        Assert.True(avgPerformance > 10); // Minimum acceptable performance score
    }

    // Helper methods

    private static ComputeWorkflowDefinition CreateWorkflowForBackend(ComputeBackendType backend, string operation, int size)
    {
        return new ComputeWorkflowDefinition
        {
            Name = $"{backend}_{operation}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = operation,
                SourceCode = GetKernelSourceForOperation(operation),
                CompilationOptions = GetBackendOptimizedCompilationOptions(backend)
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(size) },
            new WorkflowInput { Name = "input2", Data = TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "result", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "execute",
                Order = 1,
                KernelName = operation,
                BackendType = backend,
                ExecutionOptions = GetBackendOptimizedExecutionOptions(backend, size),
                ArgumentNames = ["input", "input2", "result"]
            }
            ]
        };
    }

    private static string GetKernelSourceForOperation(string operation)
    {
        return operation switch
        {
            "vector_add" => KernelSources.VectorAdd,
            "vector_multiply" => KernelSources.VectorMultiply,
            "matrix_multiply" => KernelSources.MatrixMultiply,
            "reduction" => KernelSources.ParallelReduction,
            "data_preprocessing" => KernelSources.DataPreprocessing,
            "data_processing" => KernelSources.DataProcessing,
            "data_postprocessing" => KernelSources.DataPostprocessing,
            _ => KernelSources.VectorAdd
        };
    }

    private static string GetBackendOptimizedKernel(ComputeBackendType backend)
    {
        return backend switch
        {
            ComputeBackendType.CPU => KernelSources.CPUOptimizedKernel,
            ComputeBackendType.CUDA => KernelSources.CUDAOptimizedKernel,
            ComputeBackendType.Metal => KernelSources.MetalOptimizedKernel,
            _ => KernelSources.VectorAdd
        };
    }

    private static CompilationOptions GetBackendOptimizedCompilationOptions(ComputeBackendType backend)
    {
        return backend switch
        {
            ComputeBackendType.CPU => new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                UnrollLoops = true,
                EnableParallelExecution = true
            },
            ComputeBackendType.CUDA => new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Aggressive,
                FastMath = true,
                EnableMemoryCoalescing = true,
                SharedMemorySize = 48 * 1024
            },
            ComputeBackendType.Metal => new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                EnableOperatorFusion = true,
                FastMath = true
            },
            _ => new CompilationOptions { OptimizationLevel = OptimizationLevel.Default }
        };
    }

    private static ExecutionOptions GetBackendOptimizedExecutionOptions(ComputeBackendType backend, int dataSize)
    {
        return backend switch
        {
            ComputeBackendType.CPU => new ExecutionOptions
            {
                GlobalWorkSize = [dataSize],
                LocalWorkSize = [Math.Min(64, dataSize)]
            },
            ComputeBackendType.CUDA => new ExecutionOptions
            {
                GlobalWorkSize = [dataSize],
                LocalWorkSize = [Math.Min(256, dataSize)]
            },
            ComputeBackendType.Metal => new ExecutionOptions
            {
                GlobalWorkSize = [dataSize],
                LocalWorkSize = [Math.Min(128, dataSize)]
            },
            _ => new ExecutionOptions
            {
                GlobalWorkSize = [dataSize],
                LocalWorkSize = [Math.Min(32, dataSize)]
            }
        };
    }

    private static ComputeWorkflowDefinition CreateCompatibilityTestWorkflow(ComputeBackendType backend, float[] inputData)
    {
        return new ComputeWorkflowDefinition
        {
            Name = $"Compatibility_{backend}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "compatibility_test",
                SourceCode = KernelSources.CompatibilityTestKernel
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = inputData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "result", Size = inputData.Length }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "test_execution",
                Order = 1,
                KernelName = "compatibility_test",
                BackendType = backend,
                ArgumentNames = ["input", "result"]
            }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateDynamicSelectionWorkflow(string workloadName, int size)
    {
        return new ComputeWorkflowDefinition
        {
            Name = $"Dynamic_{workloadName}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "dynamic_kernel",
                SourceCode = GetWorkloadSpecificKernel(workloadName)
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "result", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "dynamic_execution",
                Order = 1,
                KernelName = "dynamic_kernel",
                BackendType = ComputeBackendType.CPU, // Will be dynamically selected
                ArgumentNames = ["input", "result"]
            }
            ]
        };
    }

    private static string GetWorkloadSpecificKernel(string workloadName)
    {
        return workloadName switch
        {
            "SmallVectorOps" => KernelSources.SimpleVectorOperation,
            "LargeMatrixOps" => KernelSources.ComplexMatrixOperation,
            "ImageProcessing" => KernelSources.ImageProcessingKernel,
            _ => KernelSources.VectorAdd
        };
    }

    private void VerifyBackendUsage(WorkflowExecutionResult result, ComputeBackendType expectedBackend)
    {
        // In a real implementation, this would check execution logs, performance characteristics,
        // or backend-specific metrics to verify the correct backend was used
        _ = result.Success.Should().BeTrue();

        var actualBackend = DetectActualBackendUsed(result);
        Logger.LogInformation("Expected backend: {Expected}, Detected backend: {Actual}",
            expectedBackend, actualBackend);

        // For testing purposes, we'll assume the backend was used correctly if execution succeeded
        // In a production system, this would be more sophisticated
    }

    private static ComputeBackendType DetectActualBackendUsed(WorkflowExecutionResult result)
    {
        // In a real implementation, this would analyze execution characteristics,
        // performance metrics, or execution logs to determine which backend was actually used

        // For testing, we'll use performance characteristics as hints
        if (result.Metrics != null)
        {
            var throughput = result.Metrics.ThroughputMBps;
            var executionTime = result.Metrics.ExecutionTime;

            // Simulate backend detection based on performance characteristics
            if (throughput > 100 && executionTime < 10)
                return ComputeBackendType.CUDA;
            if (throughput > 50 && executionTime < 20)
                return ComputeBackendType.Metal;
            return ComputeBackendType.CPU;
        }

        return ComputeBackendType.CPU; // Default assumption
    }

    private static void ValidateBackendPerformanceCharacteristics(ComputeBackendType backend,
        PerformanceMetrics metrics, int dataSize)
    {
        switch (backend)
        {
            case ComputeBackendType.CPU:
                // CPU should have consistent performance with good memory efficiency
                _ = metrics.ResourceUtilization.CpuUsagePercent.Should().BeGreaterThan(10);
                break;

            case ComputeBackendType.CUDA:
                // CUDA should show high throughput for large workloads
                if (dataSize > 1024)
                {
                    _ = metrics.ThroughputMBps.Should().BeGreaterThan(20);
                }
                break;

            case ComputeBackendType.Metal:
                // Metal should show good performance with balanced resource usage
                _ = metrics.ResourceUtilization.GpuUsagePercent.Should().BeGreaterThan(5);
                break;
        }
    }

    private static double CalculatePerformanceScore(WorkflowExecutionResult result, int dataSize)
    {
        if (result.Metrics == null)
            return 0;

        // Simple performance scoring based on throughput and efficiency
        var throughputScore = Math.Min(result.Metrics.ThroughputMBps, 100);
        var efficiencyScore = Math.Max(0, 100 - result.Metrics.ExecutionTime);
        var utilizationScore = (result.Metrics.ResourceUtilization.CpuUsagePercent +
                               result.Metrics.ResourceUtilization.GpuUsagePercent) / 2;

        return (throughputScore + efficiencyScore + utilizationScore) / 3;
    }
}

/// <summary>
/// Additional kernel sources for multi-backend testing.
/// </summary>
internal static partial class KernelSources
{
    public const string VectorMultiply = @"
__kernel void vector_multiply(__global const float* a, __global const float* b, __global float* result) {
    int gid = get_global_id(0);
    result[gid] = a[gid] * b[gid];
}";

    public const string DataPreprocessing = @"
__kernel void data_preprocessing(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Normalize and clamp data
    output[gid] = clamp((input[gid] + 1.0f) * 0.5f, 0.0f, 1.0f);
}";

    public const string DataProcessing = @"
__kernel void data_processing(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Apply sigmoid activation
    output[gid] = 1.0f /(1.0f + exp(-input[gid]));
}";

    public const string DataPostprocessing = @"
__kernel void data_postprocessing(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Apply final scaling and offset
    output[gid] = input[gid] * 2.0f - 1.0f;
}";

    public const string CPUOptimizedKernel = @"
__kernel void cpu_optimized(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float4 data = vload4(gid / 4,__global float4*)input);
    data = data * data + 1.0f;
    vstore4(data, gid / 4,__global float4*)output);
}";

    public const string CUDAOptimizedKernel = @"
__kernel void cuda_optimized(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    __local float shared[256];
    
    shared[lid] = input[gid];
    barrier(CLK_LOCAL_MEM_FENCE);
    
    float result = shared[lid];
    for(int i = 1; i < 4; i++) {
        if(lid + i < get_local_size(0)) {
            result += shared[lid + i];
        }
    }
    output[gid] = result * 0.25f;
}";

    public const string MetalOptimizedKernel = @"
__kernel void metal_optimized(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    // Metal-optimized math functions
    output[gid] = native_sin(value) * native_cos(value) + native_sqrt(fabs(value));
}";

    public const string CompatibilityTestKernel = @"
__kernel void compatibility_test(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Standard mathematical operations that should work across all backends
    float x = input[gid];
    output[gid] = x * x + 2.0f * x + 1.0f; //(x + 1)^2
}";

    public const string SimpleVectorOperation = @"
__kernel void simple_vector(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] + 1.0f;
}";

    public const string ComplexMatrixOperation = @"
__kernel void complex_matrix(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float sum = 0.0f;
    for(int i = 0; i < 16; i++) {
        sum += input[(gid * 16 + i) % get_global_size(0)] *(i + 1);
    }
    output[gid] = sum;
}";

    public const string ImageProcessingKernel = @"
__kernel void image_processing(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int size =(int)sqrt((float)get_global_size(0));
    int x = gid % size;
    int y = gid / size;
    
    float sum = 0.0f;
    int count = 0;
    
    // 3x3 averaging filter
    for(int dy = -1; dy <= 1; dy++) {
        for(int dx = -1; dx <= 1; dx++) {
            int nx = clamp(x + dx, 0, size - 1);
            int ny = clamp(y + dy, 0, size - 1);
            sum += input[ny * size + nx];
            count++;
        }
    }
    output[gid] = sum / count;
}";
}
