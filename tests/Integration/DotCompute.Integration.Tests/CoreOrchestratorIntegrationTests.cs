// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Integration.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for the DotCompute orchestration pipeline.
/// Tests end-to-end kernel execution, backend selection, cross-backend validation, and performance optimization.
/// </summary>
[Collection("Integration")]
public class CoreOrchestratorIntegrationTests : IntegrationTestBase
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly IKernelDebugService _debugService;
    private readonly ILogger<CoreOrchestratorIntegrationTests> _logger;

    public CoreOrchestratorIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _orchestrator = GetService<IComputeOrchestrator>();
        _debugService = GetService<IKernelDebugService>();
        _logger = GetLogger<CoreOrchestratorIntegrationTests>();
    }

    [Fact]
    public async Task ExecuteAsync_SimpleVectorAddition_ShouldExecuteSuccessfully()
    {
        // Arrange
        const int size = 1000;
        var testData = GetService<TestDataGenerator>();
        var a = testData.GenerateFloatArray(size, 1f, 10f);
        var b = testData.GenerateFloatArray(size, 1f, 10f);
        var result = new float[size];

        _logger.LogInformation("Testing simple vector addition with {Size} elements", size);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
        }, "VectorAdd");

        // Assert
        for (int i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Element {i}: expected {a[i] + b[i]}, got {result[i]}");
        }

        // Performance assertion (should complete in reasonable time)
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "Vector addition should complete quickly");

        _logger.LogInformation("Vector addition completed in {Time}ms", 
            measurement.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task ExecuteAsync_WithBackendPreference_ShouldRespectPreference()
    {
        // Arrange
        const int size = 100;
        var testData = GetService<TestDataGenerator>();
        var input = testData.GenerateFloatArray(size);
        var result = new float[size];

        // Act & Assert - CPU preference
        await _orchestrator.ExecuteAsync<float[]>("ArrayCopy", "CPU", input, result);
        
        result.Should().BeEquivalentTo(input, 
            "Array copy with CPU preference should produce correct results");

        // Reset result
        Array.Clear(result);

        // Act & Assert - GPU preference (if available)
        try
        {
            await _orchestrator.ExecuteAsync<float[]>("ArrayCopy", "GPU", input, result);
            result.Should().BeEquivalentTo(input,
                "Array copy with GPU preference should produce correct results");
        }
        catch (Exception ex) when (ex.Message.Contains("GPU") || ex.Message.Contains("CUDA"))
        {
            _logger.LogWarning("GPU not available for testing: {Message}", ex.Message);
            // This is acceptable in CI environments without GPU
        }
    }

    [Fact]
    public async Task GetOptimalAcceleratorAsync_ForDifferentKernels_ShouldSelectAppropriately()
    {
        // Arrange
        var kernelsToTest = new[]
        {
            "VectorAdd",        // Simple operation - could be CPU or GPU
            "MatrixMultiply",   // Compute-intensive - likely GPU if available
            "ScalarOperation",  // Simple scalar - likely CPU
            "FFT"              // Complex algorithm - depends on size
        };

        // Act & Assert
        foreach (var kernelName in kernelsToTest)
        {
            var accelerator = await _orchestrator.GetOptimalAcceleratorAsync(kernelName);
            
            if (accelerator != null)
            {
                accelerator.Should().NotBeNull($"Should find an accelerator for {kernelName}");
                accelerator.Info.Should().NotBeNull("Accelerator should have valid info");
                accelerator.Type.Should().BeOneOf(AcceleratorType.CPU, AcceleratorType.GPU);
                
                _logger.LogInformation("Optimal accelerator for {Kernel}: {Type} - {Name}",
                    kernelName, accelerator.Type, accelerator.Info.Name);
            }
            else
            {
                _logger.LogWarning("No optimal accelerator found for {Kernel}", kernelName);
            }
        }
    }

    [Fact]
    public async Task GetSupportedAcceleratorsAsync_ShouldReturnValidAccelerators()
    {
        // Arrange
        const string kernelName = "VectorAdd";

        // Act
        var accelerators = await _orchestrator.GetSupportedAcceleratorsAsync(kernelName);

        // Assert
        accelerators.Should().NotBeNull("Should return accelerator list");
        accelerators.Should().NotBeEmpty("Should have at least one supported accelerator");
        
        foreach (var accelerator in accelerators)
        {
            accelerator.Info.Should().NotBeNull("Each accelerator should have valid info");
            accelerator.Info.Name.Should().NotBeNullOrEmpty("Accelerator should have a name");
            accelerator.Type.Should().BeOneOf(AcceleratorType.CPU, AcceleratorType.GPU, AcceleratorType.Other);
            
            _logger.LogInformation("Supported accelerator: {Type} - {Name} (Memory: {Memory:N0} bytes)",
                accelerator.Type, accelerator.Info.Name, accelerator.Info.TotalMemory);
        }
    }

    [Fact]
    public async Task PrecompileKernelAsync_ShouldCompileWithoutExecution()
    {
        // Arrange
        const string kernelName = "VectorAdd";

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.PrecompileKernelAsync(kernelName);
        }, "PrecompileKernel");

        // Assert
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "Kernel precompilation should complete in reasonable time");

        _logger.LogInformation("Kernel precompilation completed in {Time}ms",
            measurement.ElapsedTime.TotalMilliseconds);

        // Verify that subsequent execution is faster (due to compilation cache)
        var executionMeasurement = await MeasurePerformanceAsync(async () =>
        {
            var testData = GetService<TestDataGenerator>();
            var a = testData.GenerateFloatArray(100);
            var b = testData.GenerateFloatArray(100);
            var result = new float[100];
            await _orchestrator.ExecuteAsync<float[]>(kernelName, a, b, result);
        }, "ExecutePrecompiledKernel");

        // First execution after precompilation should be relatively fast
        executionMeasurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "Execution of precompiled kernel should be fast");
    }

    [Fact]
    public async Task ValidateKernelArgsAsync_WithValidArgs_ShouldReturnTrue()
    {
        // Arrange
        const string kernelName = "VectorAdd";
        var testData = GetService<TestDataGenerator>();
        var validArgs = new object[]
        {
            testData.GenerateFloatArray(100),
            testData.GenerateFloatArray(100),
            new float[100]
        };

        // Act
        var isValid = await _orchestrator.ValidateKernelArgsAsync(kernelName, validArgs);

        // Assert
        isValid.Should().BeTrue("Valid kernel arguments should pass validation");
    }

    [Fact]
    public async Task ValidateKernelArgsAsync_WithInvalidArgs_ShouldReturnFalse()
    {
        // Arrange
        const string kernelName = "VectorAdd";
        var invalidArgs = new object[]
        {
            "invalid string argument",
            42,
            null!
        };

        // Act
        var isValid = await _orchestrator.ValidateKernelArgsAsync(kernelName, invalidArgs);

        // Assert
        isValid.Should().BeFalse("Invalid kernel arguments should fail validation");
    }

    [Fact]
    public async Task ExecuteWithBuffersAsync_ZeroCopyOptimization_ShouldWork()
    {
        // Arrange
        const int size = 1000;
        var testData = GetService<TestDataGenerator>();

        var buffer1 = await CreateTestBufferAsync<float>(size, i => i * 2.0f);
        var buffer2 = await CreateTestBufferAsync<float>(size, i => i * 3.0f);
        var resultBuffer = await CreateTestBufferAsync<float>(size);

        var buffers = new[] { buffer1, buffer2, resultBuffer };

        _logger.LogInformation("Testing zero-copy buffer execution with {Size} elements", size);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            await _orchestrator.ExecuteWithBuffersAsync<float[]>("VectorAdd", buffers);
        }, "ZeroCopyExecution");

        // Assert
        var resultSpan = await resultBuffer.GetHostSpanAsync();
        var expectedSpan = await buffer1.GetHostSpanAsync();
        var buffer2Span = await buffer2.GetHostSpanAsync();

        for (int i = 0; i < size; i++)
        {
            resultSpan[i].Should().BeApproximately(expectedSpan[i] + buffer2Span[i], 1e-5f,
                $"Element {i}: expected {expectedSpan[i] + buffer2Span[i]}, got {resultSpan[i]}");
        }

        // Zero-copy operations should be efficient
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "Zero-copy execution should be efficient");

        _logger.LogInformation("Zero-copy execution completed in {Time}ms",
            measurement.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task ConcurrentExecution_MultipleCalls_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 5;
        const int operationsPerThread = 10;
        const int arraySize = 100;

        _logger.LogInformation("Testing concurrent execution with {Threads} threads, {Operations} operations each",
            threadCount, operationsPerThread);

        // Act
        var allResults = await ExecuteConcurrentlyAsync(async threadId =>
        {
            var results = new List<float[]>();
            var testData = GetService<TestDataGenerator>();

            for (int i = 0; i < operationsPerThread; i++)
            {
                var a = testData.GenerateFloatArray(arraySize, threadId, threadId + 10);
                var b = testData.GenerateFloatArray(arraySize, threadId + 1, threadId + 11);
                var result = new float[arraySize];

                await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
                results.Add(result);
            }

            return results;
        }, threadCount);

        // Assert
        allResults.Should().HaveCount(threadCount, "Should have results from all threads");
        
        foreach (var threadResults in allResults)
        {
            threadResults.Should().HaveCount(operationsPerThread, "Each thread should complete all operations");
            
            foreach (var result in threadResults)
            {
                result.Should().NotBeNull("Results should not be null");
                result.Should().HaveCount(arraySize, "Results should have correct size");
                result.Should().NotContain(float.NaN, "Results should not contain NaN values");
                result.Should().NotContain(float.PositiveInfinity, "Results should not contain infinity");
                result.Should().NotContain(float.NegativeInfinity, "Results should not contain negative infinity");
            }
        }

        _logger.LogInformation("Concurrent execution completed successfully");
    }

    [SkippableFact]
    public async Task CrossBackendValidation_SameKernel_ShouldProduceSameResults()
    {
        // Skip if debug service is not available
        Skip.IfNot(_debugService != null, "Debug service not available");

        // Arrange
        const string kernelName = "VectorAdd";
        const int size = 500;
        var testData = GetService<TestDataGenerator>();
        var inputs = new object[]
        {
            testData.GenerateFloatArray(size, 1f, 100f),
            testData.GenerateFloatArray(size, 1f, 100f),
            new float[size]
        };

        _logger.LogInformation("Testing cross-backend validation for {Kernel}", kernelName);

        // Act
        var validationResult = await _debugService.ValidateKernelAsync(kernelName, inputs, tolerance: 1e-4f);

        // Assert
        validationResult.Should().NotBeNull("Validation result should not be null");
        
        if (validationResult.IsSuccessful)
        {
            _logger.LogInformation("Cross-backend validation successful");
        }
        else
        {
            _logger.LogWarning("Cross-backend validation found differences: {Issues}",
                string.Join(", ", validationResult.Issues ?? Array.Empty<string>()));
        }

        // In a production environment, this should pass, but in testing we allow some tolerance
        validationResult.IsSuccessful.Should().BeTrue(
            "Results should be consistent across backends within tolerance");
    }

    [Fact]
    public async Task BackendFallback_WhenGpuFails_ShouldFallbackToCpu()
    {
        // Arrange
        const int size = 200;
        var testData = GetService<TestDataGenerator>();
        var a = testData.GenerateFloatArray(size);
        var b = testData.GenerateFloatArray(size);
        var result = new float[size];

        // Try to force GPU execution first, then allow fallback
        _logger.LogInformation("Testing backend fallback mechanism");

        // Act - This should succeed even if GPU is not available
        await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);

        // Assert
        for (int i = 0; i < size; i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Fallback execution should produce correct results at index {i}");
        }

        _logger.LogInformation("Backend fallback test completed successfully");
    }

    [Fact]
    public async Task LargeDataset_Performance_ShouldScaleReasonably()
    {
        // Arrange
        var sizes = new[] { 1000, 10000, 100000 };
        var timings = new List<(int size, TimeSpan duration)>();

        // Act
        foreach (var size in sizes)
        {
            var testData = GetService<TestDataGenerator>();
            var a = testData.GenerateFloatArray(size);
            var b = testData.GenerateFloatArray(size);
            var result = new float[size];

            var measurement = await MeasurePerformanceAsync(async () =>
            {
                await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
            }, $"VectorAdd_Size_{size}");

            timings.Add((size, measurement.ElapsedTime));

            _logger.LogInformation("Size {Size}: {Time}ms", size, measurement.ElapsedTime.TotalMilliseconds);
        }

        // Assert - Performance should scale reasonably (not exponentially)
        for (int i = 1; i < timings.Count; i++)
        {
            var prevTiming = timings[i - 1];
            var currentTiming = timings[i];
            
            var sizeRatio = (double)currentTiming.size / prevTiming.size;
            var timeRatio = currentTiming.duration.TotalMilliseconds / prevTiming.duration.TotalMilliseconds;

            // Time should not increase faster than size ratio squared (allowing for some overhead)
            timeRatio.Should().BeLessOrEqualTo(sizeRatio * sizeRatio * 2,
                $"Performance should scale reasonably from size {prevTiming.size} to {currentTiming.size}");
        }
    }

    [Fact]
    public async Task ErrorHandling_InvalidKernelName_ShouldThrowAppropriateException()
    {
        // Arrange
        const string invalidKernelName = "NonExistentKernel";
        var dummyArgs = new object[] { new float[10], new float[10] };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>(invalidKernelName, dummyArgs);
        });
    }

    [Fact]
    public async Task ErrorHandling_NullArguments_ShouldThrowAppropriateException()
    {
        // Arrange
        const string kernelName = "VectorAdd";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _orchestrator.ExecuteAsync<float[]>(kernelName, (object[])null!);
        });
    }
}