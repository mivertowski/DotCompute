// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using DotCompute.Abstractions;
using DotCompute.Tests.Common.Hardware;
using DotCompute.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for error recovery scenarios including hardware failures,
/// memory exhaustion, timeout handling, and graceful degradation.
/// </summary>
[Collection("Integration")]
public sealed class ErrorRecoveryIntegrationTests : ComputeWorkflowTestBase
{
    public ErrorRecoveryIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task HardwareFailureRecovery_DeviceFailure_ShouldFallbackGracefully()
    {
        // Arrange
        var workflow = CreateTestWorkflow("HardwareFailureTest", 1024);

        // Act - Simulate device failure mid-execution
        var executionTask = Task.Run(async () =>
        {
            // Start execution
            var result = await ExecuteComputeWorkflowAsync("HardwareFailureRecovery", workflow);
            return result;
        });

        // Simulate hardware failure after a short delay
        await Task.Delay(50);
        HardwareSimulator.SimulateRandomFailures(0.8, AcceleratorType.CUDA);

        var result = await executionTask;

        // Assert
        // System should either complete successfully or fail gracefully with proper error handling
        if (!result.Success)
        {
            result.Error.Should().NotBeNull();
            result.Error!.Message.Should().NotBeEmpty();
            LoggerMessages.HardwareFailureHandled(Logger, result.Error.Message);
        }
        else
        {
            LoggerMessages.SystemRecoveredFromFailure(Logger);
        }

        // System should still be operational
        var systemStats = HardwareSimulator.GetStatistics();
        systemStats["IsRunning"].Should().Be(true);

        // Reset hardware state
        HardwareSimulator.ResetAllConditions();
    }

    [Fact]
    public async Task MemoryExhaustionRecovery_OutOfMemory_ShouldDegradeGracefully()
    {
        // Arrange
        var workflow = CreateLargeMemoryWorkflow(8192); // Large memory requirement

        // Simulate memory pressure
        HardwareSimulator.SimulateMemoryPressure(AcceleratorType.CUDA, 0.95); // 95% memory used

        // Act
        var result = await ExecuteComputeWorkflowAsync("MemoryExhaustionRecovery", workflow);

        // Assert
        if (!result.Success)
        {
            // Should fail with memory-related error
            result.Error.Should().NotBeNull();
            var message = result.Error!.Message.ToUpperInvariant();
            Assert.True(message.Contains("memory", StringComparison.OrdinalIgnoreCase) || message.Contains("allocation", StringComparison.OrdinalIgnoreCase) || message.Contains("exhausted", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // If successful, should have used memory management strategies
            (result.Metrics?.ResourceUtilization.MemoryUsagePercent < 100).Should().BeTrue();
        }

        LoggerMessages.MemoryExhaustionHandled(Logger, result.Success, result.Error?.Message);

        // Reset memory pressure
        HardwareSimulator.ResetAllConditions();
    }

    [Fact]
    public async Task TimeoutRecovery_LongRunningOperation_ShouldCancelGracefully()
    {
        // Arrange
        var workflow = CreateLongRunningWorkflow();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // 2 second timeout

        var stopwatch = Stopwatch.StartNew();

        // Act
        Exception? caughtException = null;
        WorkflowExecutionResult? result = null;

        try
        {
            result = await ExecuteComputeWorkflowAsync("TimeoutRecovery", workflow, cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            caughtException = ex;
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Should timeout quickly");

        if (caughtException != null)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(caughtException);
            LoggerMessages.OperationCancelledGracefully(Logger, stopwatch.ElapsedMilliseconds);
        }
        else if (result != null && !result.Success)
        {
            LoggerMessages.OperationFailedGracefully(Logger, result.Error?.Message);
        }

        // System should remain responsive
        var quickWorkflow = CreateTestWorkflow("QuickTest", 64);
        var quickResult = await ExecuteComputeWorkflowAsync("TimeoutRecoveryQuickTest", quickWorkflow);
        quickResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task KernelCompilationFailureRecovery_InvalidKernel_ShouldProvideAlternatives()
    {
        // Arrange
        var workflow = new ComputeWorkflowDefinition
        {
            Name = "CompilationFailureRecovery",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "invalid_kernel",
                    SourceCode = "this is not valid kernel code at all!",
                    CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
                },
                new WorkflowKernel
                {
                    Name = "fallback_kernel",
                    SourceCode = KernelSources.SimpleVectorOperation,
                    CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.None }
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(256) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = 256 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "primary_stage",
                    Order = 1,
                    KernelName = "invalid_kernel",
                    ArgumentNames = ["input", "output"]
                }
            ],
            ContinueOnError = true
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("CompilationFailureRecovery", workflow);

        // Assert
        result.CompilationResults.Should().ContainKey("invalid_kernel");
        result.CompilationResults["invalid_kernel"].Success.Should().BeFalse();
        result.CompilationResults["invalid_kernel"].Error.Should().NotBeNull();

        // System should have detected the compilation failure
        var compilationError = result.CompilationResults["invalid_kernel"].Error!;
        compilationError.Message.Should().NotBeEmpty();

        LoggerMessages.OperationFailedGracefully(Logger, compilationError.Message);
    }

    [Fact]
    public async Task RuntimeErrorRecovery_KernelException_ShouldIsolateAndContinue()
    {
        // Arrange
        var workflow = new ComputeWorkflowDefinition
        {
            Name = "RuntimeErrorRecovery",
            Kernels =
            [
                new WorkflowKernel { Name = "safe_kernel", SourceCode = KernelSources.SafeKernel },
                new WorkflowKernel { Name = "error_kernel", SourceCode = KernelSources.ErrorProneKernel },
                new WorkflowKernel { Name = "recovery_kernel", SourceCode = KernelSources.RecoveryKernel }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(512) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_output", Size = 512 }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "safe_result", SizeInBytes = 512 * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "error_result", SizeInBytes = 512 * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "safe_stage",
                    Order = 1,
                    KernelName = "safe_kernel",
                    ArgumentNames = ["input", "safe_result"]
                },
                new WorkflowExecutionStage
                {
                    Name = "error_stage",
                    Order = 2,
                    KernelName = "error_kernel",
                    ArgumentNames = ["safe_result", "error_result"]
                },
                new WorkflowExecutionStage
                {
                    Name = "recovery_stage",
                    Order = 3,
                    KernelName = "recovery_kernel",
                    ArgumentNames = ["safe_result", "error_result", "final_output"]
                }
            ],
            ContinueOnError = true
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("RuntimeErrorRecovery", workflow);

        // Assert
        result.ExecutionResults.Count.Should().Be(3);

        // Safe stage should succeed
        result.ExecutionResults["safe_stage"].Success.Should().BeTrue();

        // Error stage may fail, but recovery stage should handle it
        var errorStageSuccess = result.ExecutionResults["error_stage"].Success;
        var recoveryStageSuccess = result.ExecutionResults["recovery_stage"].Success;

        if (!errorStageSuccess)
        {
            recoveryStageSuccess.Should().BeTrue();
            LoggerMessages.SystemRecoveredFromFailure(Logger);
        }

        // Should have final output
        result.Results.Should().ContainKey("final_output");
        var finalOutput = (float[])result.Results["final_output"];
        finalOutput.Should().NotContain(float.NaN);
    }

    [Fact]
    public async Task NetworkFailureRecovery_DistributedCompute_ShouldFallbackToLocal()
    {
        // Arrange - Simulate distributed compute scenario
        var workflow = CreateDistributedWorkflow();

        // Simulate network issues
        HardwareSimulator.SimulateRandomFailures(0.5, AcceleratorType.CUDA); // Simulate remote GPU failures

        // Act
        var result = await ExecuteComputeWorkflowAsync("NetworkFailureRecovery", workflow);

        // Assert
        if (result.Success)
        {
            LoggerMessages.SystemRecoveredFromFailure(Logger);
        }
        else
        {
            result.Error.Should().NotBeNull();
            LoggerMessages.HardwareFailureHandled(Logger, result.Error!.Message);
        }

        // Verify system can still perform local computation
        var localWorkflow = CreateTestWorkflow("LocalFallback", 256);
        var localResult = await ExecuteComputeWorkflowAsync("LocalFallbackTest", localWorkflow);
        localResult.Success.Should().BeTrue();

        HardwareSimulator.ResetAllConditions();
    }

    [Fact]
    public async Task DataCorruptionRecovery_InvalidInput_ShouldSanitizeAndContinue()
    {
        // Arrange
        var corruptedData = TestDataGenerators.GenerateFloatArray(1024);

        // Inject various types of corruption
        corruptedData[10] = float.NaN;
        corruptedData[20] = float.PositiveInfinity;
        corruptedData[30] = float.NegativeInfinity;
        corruptedData[40] = -0.0f; // Negative zero

        var workflow = new ComputeWorkflowDefinition
        {
            Name = "DataCorruptionRecovery",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "data_sanitizer",
                    SourceCode = KernelSources.DataSanitizer,
                    CompilationOptions = new CompilationOptions { FastMath = false } // Preserve NaN handling
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "corrupted_input", Data = corruptedData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "sanitized_output", Size = 1024 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "sanitize_stage",
                    Order = 1,
                    KernelName = "data_sanitizer",
                    ArgumentNames = ["corrupted_input", "sanitized_output"]
                }
            ]
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("DataCorruptionRecovery", workflow);

        // Assert
        result.Success.Should().BeTrue();

        var sanitizedOutput = (float[])result.Results["sanitized_output"];
        sanitizedOutput.Should().NotContain(float.NaN);
        sanitizedOutput.Should().NotContain(float.PositiveInfinity);
        sanitizedOutput.Should().NotContain(float.NegativeInfinity);

        // Verify corrupted values were replaced with safe defaults
        sanitizedOutput[10].Should().NotBe(float.NaN);
        sanitizedOutput[20].Should().NotBe(float.PositiveInfinity);
        sanitizedOutput[30].Should().NotBe(float.NegativeInfinity);

        LoggerMessages.SystemRecoveredFromFailure(Logger);
    }

    [Fact]
    public async Task ResourceExhaustionRecovery_ThreadpoolStarvation_ShouldQueueAndProcess()
    {
        // Arrange
        const int concurrentWorkflows = 20;
        var workflows = Enumerable.Range(0, concurrentWorkflows)
            .Select(i => CreateTestWorkflow($"ConcurrentWorkflow_{i}", 256))
            .ToArray();

        // Act - Submit all workflows concurrently
        var tasks = workflows.Select((workflow, index) =>
            ExecuteComputeWorkflowAsync($"ResourceExhaustion_{index}", workflow))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Length - successCount;

        LoggerMessages.ResourceExhaustionTestResult(Logger, successCount, failureCount, results.Length);

        // At least 50% should succeed even under resource pressure
        (successCount >= concurrentWorkflows / 2).Should().BeTrue(
            "System should handle resource exhaustion gracefully");

        // Failed workflows should have meaningful error messages
        var failedResults = results.Where(r => !r.Success).ToArray();
        foreach (var failedResult in failedResults.Take(3)) // Check first 3 failures
        {
            failedResult.Error.Should().NotBeNull();
            failedResult.Error!.Message.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task CascadingFailureRecovery_MultiStageFailure_ShouldContainDamage()
    {
        // Arrange
        var workflow = new ComputeWorkflowDefinition
        {
            Name = "CascadingFailureRecovery",
            Kernels =
            [
                new WorkflowKernel { Name = "stage1", SourceCode = KernelSources.ReliableKernel },
                new WorkflowKernel { Name = "stage2", SourceCode = KernelSources.UnstableKernel },
                new WorkflowKernel { Name = "stage3", SourceCode = KernelSources.FailsafeKernel },
                new WorkflowKernel { Name = "stage4", SourceCode = KernelSources.RecoveryKernel },
                new WorkflowKernel { Name = "stage5", SourceCode = KernelSources.ReliableKernel }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(256) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_output", Size = 256 }
            ],
            IntermediateBuffers = new Collection<WorkflowIntermediateBuffer>(
                Enumerable.Range(1, 4)
                .Select(i => new WorkflowIntermediateBuffer
                {
                    Name = $"buffer_{i}",
                    SizeInBytes = 256 * sizeof(float)
                })
                .ToList()),
            ExecutionStages =
            [
                new WorkflowExecutionStage { Name = "exec_stage1", Order = 1, KernelName = "stage1", ArgumentNames = ["input", "buffer_1"] },
                new WorkflowExecutionStage { Name = "exec_stage2", Order = 2, KernelName = "stage2", ArgumentNames = ["buffer_1", "buffer_2"] },
                new WorkflowExecutionStage { Name = "exec_stage3", Order = 3, KernelName = "stage3", ArgumentNames = ["buffer_2", "buffer_3"] },
                new WorkflowExecutionStage { Name = "exec_stage4", Order = 4, KernelName = "stage4", ArgumentNames = ["buffer_3", "buffer_4"] },
                new WorkflowExecutionStage { Name = "exec_stage5", Order = 5, KernelName = "stage5", ArgumentNames = ["buffer_4", "final_output"] }
            ],
            ContinueOnError = true
        };

        // Act
        var result = await ExecuteComputeWorkflowAsync("CascadingFailureRecovery", workflow);

        // Assert
        result.ExecutionResults.Count.Should().Be(5);

        var successfulStages = result.ExecutionResults.Values.Count(r => r.Success);
        var failedStages = result.ExecutionResults.Values.Count(r => !r.Success);

        LoggerMessages.CascadingFailureTestResult(Logger, successfulStages, failedStages);

        // Should have contained the failure and recovered
        successfulStages.Should().BeGreaterThanOrEqualTo(3, "Most stages should complete successfully");

        if (failedStages > 0)
        {
            // Recovery mechanisms should have activated
            result.ExecutionResults.Values.Where(r => !r.Success).Should().AllSatisfy(r =>
                r.Error.Should().NotBeNull());
        }
    }

    [Theory]
    [InlineData(ErrorType.OutOfMemory)]
    [InlineData(ErrorType.DeviceReset)]
    [InlineData(ErrorType.KernelTimeout)]
    [InlineData(ErrorType.InvalidOperation)]
    public async Task SpecificErrorRecovery_VariousErrorTypes_ShouldRecoverAppropriately(ErrorType errorType)
    {
        // Arrange
        var workflow = CreateErrorTypeSpecificWorkflow(errorType);

        // Simulate the specific error condition
        SimulateErrorCondition(errorType);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await ExecuteComputeWorkflowAsync($"SpecificErrorRecovery_{errorType}", workflow);

        stopwatch.Stop();

        // Assert
        var recoveryTime = stopwatch.Elapsed;

        if (result.Success)
        {
            LoggerMessages.SuccessfullyRecoveredFromError(Logger, errorType.ToString(), (long)recoveryTime.TotalMilliseconds);
        }
        else
        {
            result.Error.Should().NotBeNull();
            LoggerMessages.ErrorHandledGracefully(Logger, errorType.ToString(), result.Error?.Message ?? "Unknown error");
        }

        // Recovery should be reasonably fast
        recoveryTime.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "Error recovery should not take too long");

        // Validate error type specific behavior
        ValidateErrorTypeRecovery(errorType, result, recoveryTime);

        // Reset conditions
        HardwareSimulator.ResetAllConditions();
    }

    // Helper methods

    private static ComputeWorkflowDefinition CreateTestWorkflow(string name, int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "test_kernel",
                    SourceCode = KernelSources.SimpleVectorOperation
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = dataSize }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "test_stage",
                    Order = 1,
                    KernelName = "test_kernel",
                    ArgumentNames = ["input", "output"]
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateLargeMemoryWorkflow(int dataSize)
    {
        return new ComputeWorkflowDefinition
        {
            Name = "LargeMemoryWorkflow",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "memory_intensive",
                    SourceCode = KernelSources.MemoryIntensiveKernel
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "large_input", Data = TestDataGenerators.GenerateFloatArray(dataSize) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "large_output", Size = dataSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer
                {
                    Name = "large_temp",
                    SizeInBytes = dataSize * sizeof(float) * 4 // 4x the input size
                }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "memory_stage",
                    Order = 1,
                    KernelName = "memory_intensive",
                    ArgumentNames = ["large_input", "large_temp", "large_output"]
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateLongRunningWorkflow()
    {
        return new ComputeWorkflowDefinition
        {
            Name = "LongRunningWorkflow",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "long_running",
                    SourceCode = KernelSources.LongRunningKernel
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(1024) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = 1024 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "long_stage",
                    Order = 1,
                    KernelName = "long_running",
                    ArgumentNames = ["input", "output"]
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateDistributedWorkflow()
    {
        return new ComputeWorkflowDefinition
        {
            Name = "DistributedWorkflow",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "distributed_compute",
                    SourceCode = KernelSources.DistributedComputeKernel,
                    CompilationOptions = new CompilationOptions
                    {
                        OptimizationLevel = OptimizationLevel.Maximum
                    }
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "distributed_input", Data = TestDataGenerators.GenerateFloatArray(2048) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "distributed_output", Size = 2048 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "distributed_stage",
                    Order = 1,
                    KernelName = "distributed_compute",
                    BackendType = ComputeBackendType.CUDA, // Prefer remote/GPU execution
                    ArgumentNames = ["distributed_input", "distributed_output"]
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateErrorTypeSpecificWorkflow(ErrorType errorType)
    {
        var kernelSource = errorType switch
        {
            ErrorType.OutOfMemory => KernelSources.MemoryIntensiveKernel,
            ErrorType.DeviceReset => KernelSources.DeviceStressKernel,
            ErrorType.KernelTimeout => KernelSources.LongRunningKernel,
            ErrorType.InvalidOperation => KernelSources.ErrorProneKernel,
            _ => KernelSources.SimpleVectorOperation
        };

        return new ComputeWorkflowDefinition
        {
            Name = $"ErrorRecovery_{errorType}",
            Kernels =
            [
                new WorkflowKernel
                {
                    Name = "error_specific_kernel",
                    SourceCode = kernelSource
                }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = TestDataGenerators.GenerateFloatArray(512) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = 512 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "error_stage",
                    Order = 1,
                    KernelName = "error_specific_kernel",
                    ArgumentNames = ["input", "output"]
                }
            ]
        };
    }

    private void SimulateErrorCondition(ErrorType errorType)
    {
        switch (errorType)
        {
            case ErrorType.OutOfMemory:
                HardwareSimulator.SimulateMemoryPressure(AcceleratorType.CUDA, 0.98);
                break;
            case ErrorType.DeviceReset:
                HardwareSimulator.SimulateRandomFailures(0.8, AcceleratorType.CUDA);
                break;
            case ErrorType.KernelTimeout:
                // Timeout will be handled by the long-running kernel itself
                break;
            case ErrorType.InvalidOperation:
                // Invalid operation will be triggered by the error-prone kernel
                break;
        }
    }

    private static void ValidateErrorTypeRecovery(ErrorType errorType, WorkflowExecutionResult result, TimeSpan recoveryTime)
    {
        switch (errorType)
        {
            case ErrorType.OutOfMemory:
                if (!result.Success)
                {
                    var lowerMessage = result.Error!.Message.ToUpperInvariant();
                    (lowerMessage.Contains("memory", StringComparison.OrdinalIgnoreCase) || lowerMessage.Contains("allocation", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
                }
                break;

            case ErrorType.DeviceReset:
                if (!result.Success)
                {
                    var lowerMessage = result.Error!.Message.ToUpperInvariant();
                    (lowerMessage.Contains("device", StringComparison.OrdinalIgnoreCase) || lowerMessage.Contains("hardware", StringComparison.OrdinalIgnoreCase) || lowerMessage.Contains("failure", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
                }
                break;

            case ErrorType.KernelTimeout:
                recoveryTime.Should().BeLessThan(TimeSpan.FromSeconds(15),
                    "Timeout recovery should be faster than actual timeout");
                break;

            case ErrorType.InvalidOperation:
                if (!result.Success)
                {
                    result.Error!.Message.Should().NotBeEmpty();
                }
                break;
        }
    }
}

public enum ErrorType
{
    OutOfMemory,
    DeviceReset,
    KernelTimeout,
    InvalidOperation
}

/// <summary>
/// Additional kernel sources for error recovery testing.
/// </summary>
internal static partial class KernelSources
{
    public const string SafeKernel = @"
__kernel void safe_kernel(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] * 1.1f;
}";

    public const string ErrorProneKernel = @"
__kernel void error_kernel(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Introduce errors for certain indices
    if(gid % 127 == 0) {
        output[gid] = input[gid] / 0.0f; // Division by zero
    } else if(gid % 131 == 0) {
        output[gid] = sqrt(-1.0f); // Invalid operation
    } else {
        output[gid] = input[gid] * 2.0f;
    }
}";

    public const string RecoveryKernel = @"
__kernel void recovery_kernel(__global const float* safe_input, __global const float* error_input, __global float* output) {
    int gid = get_global_id(0);
    float safe_val = safe_input[gid];
    float error_val = error_input[gid];
    
    // Use safe value if error value is invalid
    if(isnan(error_val) || isinf(error_val)) {
        output[gid] = safe_val * 2.0f; // Fallback computation
    } else {
        output[gid] = error_val;
    }
}";

    public const string DataSanitizer = @"
__kernel void data_sanitizer(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    // Sanitize invalid values
    if(isnan(value)) {
        output[gid] = 0.0f;
    } else if(isinf(value)) {
        output[gid] = copysign(1000.0f, value); // Cap to reasonable value
    } else if(value == -0.0f) {
        output[gid] = 0.0f;
    } else {
        output[gid] = value;
    }
}";

    public const string MemoryIntensiveKernel = @"
__kernel void memory_intensive(__global const float* input, __global float* temp, __global float* output) {
    int gid = get_global_id(0);
    int size = get_global_size(0);
    
    // Use lots of memory by accessing temp buffer extensively
    for(int i = 0; i < 4; i++) {
        int temp_idx =(gid + i * size) %(size * 4);
        if(temp_idx < size * 4) {
            temp[temp_idx] = input[gid] + i;
        }
    }
    
    barrier(CLK_GLOBAL_MEM_FENCE);
    
    // Compute result using temporary data
    float sum = 0.0f;
    for(int i = 0; i < 4; i++) {
        int temp_idx =(gid + i * size) %(size * 4);
        if(temp_idx < size * 4) {
            sum += temp[temp_idx];
        }
    }
    
    output[gid] = sum / 4.0f;
}";

    public const string LongRunningKernel = @"
__kernel void long_running(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    // Perform many iterations to simulate long computation
    for(int i = 0; i < 100000; i++) {
        value = sin(value) * cos(value) + 0.001f;
        if(i % 10000 == 0) {
            // Periodic check - in real implementation this would check for cancellation
            barrier(CLK_GLOBAL_MEM_FENCE);
        }
    }
    
    output[gid] = value;
}";

    public const string DistributedComputeKernel = @"
__kernel void distributed_compute(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int global_size = get_global_size(0);
    
    // Simulate distributed computation by accessing data across work groups
    float local_sum = input[gid];
    
    // Simulate inter-node communication patterns
    for(int step = 1; step < global_size; step *= 2) {
        int partner = gid ^ step;
        if(partner < global_size) {
            local_sum =(local_sum + input[partner]) * 0.5f;
        }
        barrier(CLK_GLOBAL_MEM_FENCE);
    }
    
    output[gid] = local_sum;
}";

    public const string ReliableKernel = @"
__kernel void reliable(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] + 1.0f;
}";

    public const string UnstableKernel = @"
__kernel void unstable(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Occasionally produce unstable results
    if((gid * 7) % 100 < 5) { // 5% chance
        output[gid] = input[gid] /(input[gid] - input[gid]); // NaN
    } else {
        output[gid] = input[gid] * 2.0f;
    }
}";

    public const string FailsafeKernel = @"
__kernel void failsafe(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    // Failsafe: always produce valid output
    if(isnan(value) || isinf(value)) {
        output[gid] = 0.0f;
    } else {
        output[gid] = clamp(value, -1000.0f, 1000.0f);
    }
}";

    public const string DeviceStressKernel = @"
__kernel void device_stress(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    __local float shared[256];
    
    // Stress the device with intensive local memory operations
    shared[lid] = input[gid];
    barrier(CLK_LOCAL_MEM_FENCE);
    
    float result = shared[lid];
    for(int i = 0; i < get_local_size(0); i++) {
        result += shared[i] * 0.001f;
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    output[gid] = result;
}";
}
