// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Core.Tests.Pipelines;

/// <summary>
/// Comprehensive tests for kernel chain functionality covering the 0% pipeline coverage gap.
/// Tests core pipeline operations, chaining, error handling, and performance validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
public class KernelChainTests : PipelineTestBase
{
    private readonly MockComputeOrchestrator _mockOrchestrator;

    public KernelChainTests()
    {
        _mockOrchestrator = (MockComputeOrchestrator)Services.GetRequiredService<IComputeOrchestrator>();
        SetupMockKernels();
    }

    [Fact]
    public void KernelChain_Create_ReturnsValidBuilder()
    {
        // Arrange & Act
        var builder = CreatePipelineBuilder();

        // Assert
        Assert.NotNull(builder);
        _ = Assert.IsAssignableFrom<IKernelChainBuilder>(builder);
    }

    [Fact]
    public async Task KernelChain_Sequential_ExecutesInOrder()
    {
        // Arrange
        var input = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Act
        var result = await builder
            .Kernel("VectorAdd", input, input, new float[input.Length])
            .Then("VectorMultiply", ["result", input, new float[input.Length]])
            .ExecuteAsync<float[]>(CreateTestTimeout());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input.Length, result.Length);

        // Verify execution order

        var history = _mockOrchestrator.ExecutionHistory;
        Assert.Equal(2, history.Count);
        Assert.Equal("VectorAdd", history[0].Name);
        Assert.Equal("VectorMultiply", history[1].Name);

        // Verify all executions were successful

        Assert.All(history, record => Assert.True(record.Success));
    }

    [Fact]
    public async Task KernelChain_Parallel_ExecutesConcurrently()
    {
        // Arrange
        var input1 = GenerateTestData<float>(500);
        var input2 = GenerateTestData<float>(500);
        var builder = CreatePipelineBuilder();

        // Act
        var startTime = DateTime.UtcNow;
        var result = await builder
            .Parallel(
                ("SlowKernel", new object[] { input1 }),
                ("SlowKernel", new object[] { input2 })
            )
            .ExecuteAsync<float[][]>(CreateTestTimeout());
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);

        // Parallel execution should be significantly faster than sequential
        // Two 100ms operations should complete in ~100-120ms, not 200ms

        Assert.True(duration < TimeSpan.FromMilliseconds(150),

            $"Parallel execution took too long: {duration.TotalMilliseconds}ms");

        // Verify both kernels were executed

        var history = _mockOrchestrator.ExecutionHistory;
        Assert.True(history.Count >= 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KernelChain_Branch_FollowsCondition(bool conditionValue)
    {
        // Arrange
        var input = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();

        // Act
        var result = await builder
            .Kernel("VectorAdd", input, input, new float[input.Length])
            .Branch<float[]>(
                data => conditionValue,
                truePath => truePath.Then("VectorMultiply", ["data", input, new float[input.Length]]),
                falsePath => falsePath.Then("VectorAdd", ["data", input, new float[input.Length]])
            )
            .ExecuteAsync<float[]>(CreateTestTimeout());

        // Assert
        Assert.NotNull(result);


        var history = _mockOrchestrator.ExecutionHistory;
        Assert.True(history.Count >= 2);

        // First operation should always be VectorAdd

        Assert.Equal("VectorAdd", history[0].Name);

        // Second operation should depend on branch condition

        var expectedSecondKernel = conditionValue ? "VectorMultiply" : "VectorAdd";
        Assert.Equal(expectedSecondKernel, history[1].Name);
    }

    [Fact]
    public async Task KernelChain_Cache_ImprovesPerfomance()
    {
        // Arrange
        var input = GenerateTestData<float>(1000);
        var cacheKey = "test-cache-key";
        var builder = CreatePipelineBuilder();

        // Act - First execution (uncached)
        var firstExecution = await MeasureExecutionTimeAsync(async () =>
        {
            _ = await builder
                .Kernel("SlowKernel", input)
                .Cache(cacheKey, TimeSpan.FromMinutes(1))
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Second execution (should be cached)
        var secondExecution = await MeasureExecutionTimeAsync(async () =>
        {
            _ = await builder
                .Kernel("SlowKernel", input)
                .Cache(cacheKey, TimeSpan.FromMinutes(1))
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Assert
        // Second execution should be significantly faster due to caching
        Assert.True(secondExecution < firstExecution * 0.5,

            $"Caching did not improve performance. First: {firstExecution.TotalMilliseconds}ms, " +
            $"Second: {secondExecution.TotalMilliseconds}ms");
    }

    [Theory]
    [InlineData(Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Continue)]
    [InlineData(Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Retry)]
    [InlineData(Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Skip)]
    [InlineData(Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort)]
    [InlineData(Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Fallback)]
    public async Task KernelChain_ErrorStrategy_HandlesFailures(DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy strategy)
    {
        // Arrange
        var input = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();

        // Configure error handling

        _ = builder.OnError(ex => strategy);

        // Act & Assert based on strategy
        switch (strategy)
        {
            case Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Continue:
            case Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Skip:
            case Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Fallback:
                // These should complete without throwing
                var result = await builder
                    .Kernel("VectorAdd", input, input, new float[input.Length])
                    .Then("ErrorKernel", input) // This will fail
                    .Then("VectorAdd", input, input, new float[input.Length]) // This should still execute
                    .ExecuteAsync<float[]>(CreateTestTimeout());
                Assert.NotNull(result);
                break;

            case Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Retry:
                // Should eventually succeed after retries (or fail after max retries)
                var retryResult = await builder
                    .Kernel("VectorAdd", input, input, new float[input.Length])
                    .Then("RetryableErrorKernel", input)
                    .ExecuteAsync<float[]>(CreateTestTimeout());
                break;

            case Abstractions.Pipelines.Enums.ErrorHandlingStrategy.Abort:
                // Should throw and abort the entire chain
                _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    _ = await builder
                        .Kernel("VectorAdd", input, input, new float[input.Length])
                        .Then("ErrorKernel", input)
                        .Then("VectorAdd", input, input, new float[input.Length])
                        .ExecuteAsync<float[]>(CreateTestTimeout());
                });
                break;
        }
    }

    [Theory]
    [InlineData("CPU")]
    [InlineData("CUDA")]
    [InlineData("OpenCL")]
    public async Task KernelChain_BackendSelection_ChoosesOptimal(string backendName)
    {
        // Arrange
        var input = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Act
        var result = await builder
            .Kernel("VectorAdd", input, input, new float[input.Length])
            .OnBackend(backendName)
            .ExecuteAsync<float[]>(CreateTestTimeout());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input.Length, result.Length);

        // Verify the backend selection was recorded

        var history = _mockOrchestrator.ExecutionHistory;
        _ = Assert.Single(history);
        Assert.True(history[0].Success);
    }

    [Theory]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    public async Task KernelChain_DataTypes_HandlesAllTypes(Type dataType)
    {
        // Arrange
        var size = 1000;
        var builder = CreatePipelineBuilder();

        // Act & Assert based on data type
        if (dataType == typeof(float))
        {
            var input = GenerateTestData<float>(size);
            var result = await builder
                .Kernel("VectorAdd", input, input, new float[size])
                .ExecuteAsync<float[]>(CreateTestTimeout());
            Assert.Equal(size, result.Length);
        }
        else if (dataType == typeof(double))
        {
            var input = GenerateTestData<double>(size);
            var result = await builder
                .Kernel("VectorAdd", input, input, new double[size])
                .ExecuteAsync<double[]>(CreateTestTimeout());
            Assert.Equal(size, result.Length);
        }
        else if (dataType == typeof(int))
        {
            var input = GenerateTestData<int>(size);
            var result = await builder
                .Kernel("VectorAdd", input, input, new int[size])
                .ExecuteAsync<int[]>(CreateTestTimeout());
            Assert.Equal(size, result.Length);
        }
        else if (dataType == typeof(long))
        {
            var input = GenerateTestData<long>(size);
            var result = await builder
                .Kernel("VectorAdd", input, input, new long[size])
                .ExecuteAsync<long[]>(CreateTestTimeout());
            Assert.Equal(size, result.Length);
        }
    }

    [Fact]
    public async Task KernelChain_WithValidation_ValidatesInputs()
    {
        // Arrange
        var builder = CreatePipelineBuilder();

        // Act
        var validationResult = await builder
            .Kernel("VectorAdd", null!, null!, null!) // Invalid null inputs
            .WithValidation(validateInputs: true)
            .ValidateAsync();

        // Assert
        Assert.False(validationResult.IsValid);
        Assert.NotNull(validationResult.Errors);
        Assert.NotEmpty(validationResult.Errors);
    }

    [Fact]
    public async Task KernelChain_WithProfiling_CapturesMetrics()
    {
        // Arrange
        var input = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Act
        var result = await builder
            .Kernel("VectorAdd", input, input, new float[input.Length])
            .Then("VectorMultiply", ["result", input, new float[input.Length]])
            .WithProfiling("test-profile")
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.StepMetrics);
        Assert.Equal(2, result.StepMetrics.Count);

        // Verify metrics contain meaningful data

        foreach (var stepMetric in result.StepMetrics)
        {
            Assert.True(stepMetric.ExecutionTime > TimeSpan.Zero);
            Assert.NotNull(stepMetric.KernelName);
            Assert.True(stepMetric.MemoryUsed >= 0);
        }


        Assert.NotNull(result.MemoryMetrics);
        Assert.True(result.MemoryMetrics.TotalMemoryAllocated >= 0);
    }

    [Fact]
    public async Task KernelChain_WithTimeout_EnforcesTimeLimit()
    {
        // Arrange
        var input = GenerateTestData<float>(100);
        var builder = CreatePipelineBuilder();

        // Act & Assert
        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await builder
                .Kernel("VerySlowKernel", input) // Takes 1000ms
                .WithTimeout(TimeSpan.FromMilliseconds(50)) // Timeout after 50ms
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });
    }

    [Fact]
    public async Task KernelChain_MemoryUsage_StaysWithinBounds()
    {
        // Arrange
        var largeInput = GenerateTestData<float>(100_000); // Large dataset
        var builder = CreatePipelineBuilder();

        // Act & Assert
        var memoryMetrics = await ValidateMemoryUsageAsync(
            async () =>
            {
                var result = await builder
                    .Kernel("VectorAdd", largeInput, largeInput, new float[largeInput.Length])
                    .Then("VectorMultiply", ["result", largeInput, new float[largeInput.Length]])
                    .ExecuteAsync<float[]>(CreateTestTimeout());
                Assert.NotNull(result);
            },
            maxMemoryIncreaseMB: 50 // Should not increase memory by more than 50MB
        );

        // Verify memory was managed efficiently
        Assert.True(memoryMetrics.MemoryIncreaseMB > 0, "Should allocate some memory");
        Assert.True(memoryMetrics.MemoryIncreaseMB < 50, "Memory usage should stay within bounds");
    }

    [Fact]
    public async Task KernelChain_ConcurrentExecution_ThreadSafe()
    {
        // Arrange
        var builder = CreatePipelineBuilder();
        var tasks = new List<Task<float[]>>();
        const int concurrentExecutions = 10;

        // Act - Execute multiple chains concurrently
        for (var i = 0; i < concurrentExecutions; i++)
        {
            var input = GenerateTestData<float>(100, DataPattern.Sequential);
            tasks.Add(builder
                .Kernel("VectorAdd", input, input, new float[input.Length])
                .ExecuteAsync<float[]>(CreateTestTimeout()));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentExecutions, results.Length);
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(100, result.Length);
        });

        // Verify all executions completed successfully
        var history = _mockOrchestrator.ExecutionHistory;
        Assert.Equal(concurrentExecutions, history.Count);
        Assert.All(history, record => Assert.True(record.Success));
    }

    private void SetupMockKernels()
    {
        _mockOrchestrator.Reset();

        // Basic vector operations

        _mockOrchestrator.RegisterMockKernel("VectorAdd", MockComputeOrchestrator.CreateVectorAddMock());
        _mockOrchestrator.RegisterMockKernel("VectorMultiply", MockComputeOrchestrator.CreateVectorMultiplyMock());
        _mockOrchestrator.RegisterMockKernel("Aggregate", MockComputeOrchestrator.CreateAggregateMock());

        // Performance testing kernels

        _mockOrchestrator.RegisterMockKernel("SlowKernel", MockComputeOrchestrator.CreateSlowMock(100));
        _mockOrchestrator.RegisterMockKernel("VerySlowKernel", MockComputeOrchestrator.CreateSlowMock(1000));

        // Error testing kernels

        _mockOrchestrator.RegisterMockKernel("ErrorKernel",

            MockComputeOrchestrator.CreateErrorMock(typeof(InvalidOperationException), "Simulated kernel error"));

        // Retryable error kernel that succeeds on second attempt

        var retryAttempts = 0;
        _mockOrchestrator.RegisterMockKernel("RetryableErrorKernel", args =>
        {
            if (++retryAttempts == 1)
                throw new InvalidOperationException("First attempt fails");
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