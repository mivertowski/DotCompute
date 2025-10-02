// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Pipelines.Exceptions;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Core.Tests.Pipelines;

/// <summary>
/// Comprehensive tests for pipeline error handling and failure recovery.
/// Tests all error scenarios, recovery strategies, and fault tolerance mechanisms.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "PipelineError")]
public class PipelineErrorTests : PipelineTestBase
{
    private readonly MockComputeOrchestrator _mockOrchestrator;

    public PipelineErrorTests()
    {
        _mockOrchestrator = (MockComputeOrchestrator)Services.GetRequiredService<IComputeOrchestrator>();
        SetupErrorTestKernels();
    }

    [Fact]
    public async Task Pipeline_StageFailure_RecoversGracefully()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Configure graceful error recovery

        _ = builder.OnError(ex => DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Skip);

        // Act
        var result = await builder
            .Kernel("VectorAdd", data, data, new float[data.Length])
            .Then("FailingKernel", ["result"]) // This will fail
            .Then("VectorMultiply", ["result", data, new float[data.Length]]) // This should still run
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success); // Overall success despite individual failure
        Assert.NotNull(result.Errors);
        _ = Assert.Single(result.Errors); // One error from FailingKernel

        // Should have 3 steps: Add (success), Failing (error), Multiply (success)

        Assert.Equal(3, result.StepMetrics.Count);
        Assert.True(result.StepMetrics[0].ExecutionTime > TimeSpan.Zero); // VectorAdd succeeded
        Assert.True(result.StepMetrics[2].ExecutionTime > TimeSpan.Zero); // VectorMultiply succeeded
    }

    [Fact]
    public async Task Pipeline_MemoryExhaustion_FallsBackToCPU()
    {
        // Arrange
        var largeData = GenerateTestData<float>(10_000_000); // Very large dataset
        var builder = CreatePipelineBuilder();

        // Configure fallback on memory issues

        _ = builder.OnError(ex => ex is OutOfMemoryException ? DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Fallback : DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort);

        // Act
        var result = await builder
            .Kernel("MemoryExhaustionKernel", largeData) // Will fail with OOM on GPU
            .OnBackend("CUDA") // Initially try GPU
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success); // Should succeed after fallback
        Assert.NotNull(result.Errors);
        _ = Assert.Single(result.Errors);
        _ = Assert.IsType<OutOfMemoryException>(result.Errors[0]);

        // Should have fallen back to CPU

        Assert.Equal("CPU", result.Backend);
    }

    [Fact]
    public async Task Pipeline_InvalidExpression_ProvidesHelpfulError()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PipelineValidationException>(async () =>
        {
            _ = await builder
                .Kernel("InvalidKernel", null!, null!) // Invalid null arguments
                .WithValidation(validateInputs: true)
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Assert error message is helpful
        Assert.Contains("Invalid arguments", exception.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidKernel", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.Errors);
        Assert.NotEmpty(exception.Errors);
    }

    [Fact]
    public async Task Pipeline_BackendFailure_SwitchesBackend()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Configure backend switching on failure

        _ = builder.OnError(ex => DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Fallback);

        // Act
        var result = await builder
            .Kernel("BackendSpecificFailure", data) // Fails only on specific backend
            .OnBackend("CUDA") // Try CUDA first
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success); // Should succeed after backend switch
        Assert.NotNull(result.Errors);
        _ = Assert.Single(result.Errors); // One error from CUDA failure

        // Should have switched to different backend

        Assert.NotEqual("CUDA", result.Backend);
    }

    [Fact]
    public async Task Pipeline_Cancellation_CleansUpResources()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();
        using var cts = new CancellationTokenSource();

        // Act - Cancel after short delay
        var task = builder
            .Kernel("LongRunningKernel", data) // Takes 2 seconds
            .ExecuteAsync<float[]>(cts.Token);

        // Cancel after 100ms
        await Task.Delay(100);
        await cts.CancelAsync();

        // Assert
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => task);

        // Verify resources were cleaned up (mock orchestrator should record cleanup)

        var history = _mockOrchestrator.ExecutionHistory;
        Assert.True(history.Any()); // Some execution should have started
        Assert.True(history[^1].EndTime > history[^1].StartTime); // Execution was terminated
    }

    [Theory]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Continue, true, 3)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Skip, true, 3)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Retry, true, 3)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort, false, 1)]
    [InlineData(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Fallback, true, 3)]
    public async Task Pipeline_ErrorStrategy_HandlesCorrectly(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy strategy, bool shouldSucceed, int expectedSteps)
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();


        _ = builder.OnError(ex => strategy);

        // Act
        Exception? caughtException = null;
        KernelChainExecutionResult? result = null;


        try
        {
            result = await builder
                .Kernel("VectorAdd", data, data, new float[data.Length])
                .Then("ReliableFailingKernel", ["result"]) // Always fails
                .Then("VectorMultiply", ["result", data, new float[data.Length]])
                .ExecuteWithMetricsAsync(CreateTestTimeout());
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        if (shouldSucceed)
        {
            Assert.Null(caughtException);
            Assert.NotNull(result);
            Assert.True(result.Success);


            if (strategy != DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Retry)
            {
                Assert.Equal(expectedSteps, result.StepMetrics.Count);
            }
        }
        else
        {
            Assert.NotNull(caughtException);
            _ = Assert.IsType<InvalidOperationException>(caughtException);
        }
    }

    [Fact]
    public async Task Pipeline_CircuitBreaker_PreventsCascadingFailures()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();

        // Configure circuit breaker (fails after 3 consecutive failures)

        var failureCount = 0;
        _ = builder.OnError(ex =>
        {
            failureCount++;
            return failureCount >= 3 ? DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort : DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Retry;
        });

        // Act & Assert - Should abort after 3 failures
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await builder
                .Kernel("AlwaysFailingKernel", data) // Always fails
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Should have tried exactly 3 times before circuit breaker triggered

        var history = _mockOrchestrator.ExecutionHistory;
        var failures = history.Where(h => h.Name == "AlwaysFailingKernel" && !h.Success).Count();
        Assert.Equal(3, failures);
    }

    [Fact]
    public async Task Pipeline_TimeoutError_HandlesGracefully()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();


        _ = builder.OnError(ex => ex is TimeoutException ? DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Fallback : DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort);

        // Act
        var result = await builder
            .Kernel("TimeoutKernel", data) // Times out after 2 seconds
            .WithTimeout(TimeSpan.FromMilliseconds(100)) // Very short timeout
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success); // Should succeed with fallback
        Assert.NotNull(result.Errors);
        _ = Assert.Single(result.Errors);
        _ = Assert.IsType<TimeoutException>(result.Errors[0]);
    }

    [Fact]
    public async Task Pipeline_ValidationError_StopsExecution()
    {
        // Arrange
        var builder = CreatePipelineBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PipelineValidationException>(async () =>
        {
            _ = await builder
                .Kernel("VectorAdd", null!, null!, null!) // Invalid inputs
                .WithValidation(validateInputs: true)
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Validation should catch errors before execution
        Assert.Empty(_mockOrchestrator.ExecutionHistory); // No kernels should have executed
        Assert.NotEmpty(exception.Errors);
    }

    [Fact]
    public async Task Pipeline_PartialFailure_ReturnsPartialResults()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Configure to continue on failure

        _ = builder.OnError(ex => DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Continue);

        // Act
        var result = await builder
            .Kernel("VectorAdd", data, data, new float[data.Length]) // Succeeds
            .Then("PartialFailingKernel", ["result"]) // Partially fails
            .Then("VectorMultiply", ["result", data, new float[data.Length]]) // Continues
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success); // Overall success
        Assert.NotNull(result.Result);
        Assert.NotNull(result.Errors);
        _ = Assert.Single(result.Errors); // One partial failure recorded

        // All steps should have been attempted

        Assert.Equal(3, result.StepMetrics.Count);
    }

    [Fact]
    public async Task Pipeline_ErrorAggregation_CollectsAllErrors()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();

        // Continue on all errors to collect them

        _ = builder.OnError(ex => DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Continue);

        // Act
        var result = await builder
            .Kernel("ErrorKernel1", data) // Error 1
            .Then("ErrorKernel2", [data]) // Error 2
            .Then("ErrorKernel3", [data]) // Error 3
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success); // Succeeds with error handling
        Assert.NotNull(result.Errors);
        Assert.Equal(3, result.Errors.Count); // All errors collected

        // Verify different error types

        Assert.Contains(result.Errors, e => e.Message.Contains("Error 1", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Message.Contains("Error 2", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Message.Contains("Error 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pipeline_RetryWithBackoff_RetriesCorrectly()
    {
        // Arrange
        var data = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();
        var retryCount = 0;

        // Retry with exponential backoff

        _ = builder.OnError(ex =>
        {
            retryCount++;
            return retryCount < 3 ? DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Retry : DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort;
        });

        // Act & Assert - Should eventually abort after retries
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await builder
                .Kernel("SometimesFailingKernel", data) // Fails first 3 times
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Should have retried 3 times
        Assert.Equal(3, retryCount);
        var history = _mockOrchestrator.ExecutionHistory;
        var attempts = history.Where(h => h.Name == "SometimesFailingKernel").Count();
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Pipeline_DependencyFailure_HandlesProperly()
    {
        // Arrange
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();


        _ = builder.OnError(ex => DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Skip);

        // Act
        var result = await builder
            .Kernel("DependencyKernel", data) // Fails, affects dependent operations
            .Then("DependentKernel", ["dependency_result"]) // Depends on first
            .Then("IndependentKernel", data) // Independent operation
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Errors);
        Assert.True(result.Errors.Count >= 1); // At least dependency failure

        // Independent kernel should still execute

        var independentExecution = _mockOrchestrator.ExecutionHistory
            .FirstOrDefault(h => h.Name == "IndependentKernel");
        Assert.NotNull(independentExecution);
        Assert.True(independentExecution.Success);
    }

    private void SetupErrorTestKernels()
    {
        _mockOrchestrator.Reset();

        // Basic working kernels

        _mockOrchestrator.RegisterMockKernel("VectorAdd", MockComputeOrchestrator.CreateVectorAddMock());
        _mockOrchestrator.RegisterMockKernel("VectorMultiply", MockComputeOrchestrator.CreateVectorMultiplyMock());

        // Error testing kernels

        _mockOrchestrator.RegisterMockKernel("FailingKernel",

            MockComputeOrchestrator.CreateErrorMock(typeof(InvalidOperationException), "Simulated kernel failure"));


        _mockOrchestrator.RegisterMockKernel("AlwaysFailingKernel",
            MockComputeOrchestrator.CreateErrorMock(typeof(InvalidOperationException), "Always fails"));


        _mockOrchestrator.RegisterMockKernel("ReliableFailingKernel",
            MockComputeOrchestrator.CreateErrorMock(typeof(InvalidOperationException), "Reliable failure for testing"));

        // Memory exhaustion kernel

        _mockOrchestrator.RegisterMockKernel("MemoryExhaustionKernel",

            MockComputeOrchestrator.CreateErrorMock(typeof(OutOfMemoryException), "Out of GPU memory"));

        // Backend-specific failure

        _mockOrchestrator.RegisterMockKernel("BackendSpecificFailure", args =>
            // Simulate CUDA-specific failure
            throw new InvalidOperationException("CUDA kernel compilation failed"));

        // Long running kernel

        _mockOrchestrator.RegisterMockKernel("LongRunningKernel", MockComputeOrchestrator.CreateSlowMock(2000));

        // Timeout kernel

        _mockOrchestrator.RegisterMockKernel("TimeoutKernel", async args =>
        {
            await Task.Delay(2000); // Takes 2 seconds
            return args[0];
        });

        // Partial failure kernel

        _mockOrchestrator.RegisterMockKernel("PartialFailingKernel", args =>
            // Simulate partial processing failure
            throw new InvalidOperationException("Partial processing error"));

        // Multiple error kernels

        _mockOrchestrator.RegisterMockKernel("ErrorKernel1",

            MockComputeOrchestrator.CreateErrorMock(typeof(ArgumentException), "Error 1"));
        _mockOrchestrator.RegisterMockKernel("ErrorKernel2",

            MockComputeOrchestrator.CreateErrorMock(typeof(InvalidOperationException), "Error 2"));
        _mockOrchestrator.RegisterMockKernel("ErrorKernel3",

            MockComputeOrchestrator.CreateErrorMock(typeof(NotSupportedException), "Error 3"));

        // Sometimes failing kernel (for retry testing)

        var failCount = 0;
        _mockOrchestrator.RegisterMockKernel("SometimesFailingKernel", args =>
        {
            if (++failCount <= 3) // Fail first 3 attempts
                throw new InvalidOperationException($"Failure attempt {failCount}");
            return args[0]; // Succeed on 4th attempt
        });

        // Dependency kernels

        _mockOrchestrator.RegisterMockKernel("DependencyKernel",

            MockComputeOrchestrator.CreateErrorMock(typeof(InvalidOperationException), "Dependency failure"));
        _mockOrchestrator.RegisterMockKernel("DependentKernel", args => args[0]);
        _mockOrchestrator.RegisterMockKernel("IndependentKernel", args => args[0]);

        // Invalid kernel for validation testing

        _mockOrchestrator.RegisterMockKernel("InvalidKernel", args =>
        {
            if (args[0] == null || args[1] == null)
                throw new ArgumentNullException("Invalid null arguments");
            return args[0];
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mockOrchestrator?.Reset();
        }
        base.Dispose(disposing);
    }
}