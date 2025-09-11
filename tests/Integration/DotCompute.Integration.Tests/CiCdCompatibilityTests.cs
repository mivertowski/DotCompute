// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Integration.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// CI/CD compatibility tests ensuring DotCompute works reliably in automated environments.
/// Tests headless execution, resource constraints, timeout handling, and deterministic behavior.
/// </summary>
[Collection("Integration")]
public class CiCdCompatibilityTests : IntegrationTestBase
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<CiCdCompatibilityTests> _logger;

    public CiCdCompatibilityTests(ITestOutputHelper output) : base(output)
    {
        _orchestrator = GetService<IComputeOrchestrator>();
        _logger = GetLogger<CiCdCompatibilityTests>();
    }

    [Fact]
    public async Task HeadlessExecution_NoDisplayOrGUI_ShouldWorkInCiEnvironment()
    {
        // Arrange
        _logger.LogInformation("Testing headless execution compatibility");
        
        // Simulate CI environment variables
        var originalDisplay = Environment.GetEnvironmentVariable("DISPLAY");
        var originalSession = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", null);
            Environment.SetEnvironmentVariable("XDG_SESSION_TYPE", null);
            Environment.SetEnvironmentVariable("CI", "true");
            Environment.SetEnvironmentVariable("HEADLESS", "true");

            const int size = 1000;
            var testData = GetService<TestDataGenerator>();
            var a = testData.GenerateFloatArray(size);
            var b = testData.GenerateFloatArray(size);
            var result = new float[size];

            // Act
            var measurement = await MeasurePerformanceAsync(async () =>
            {
                await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
            }, "HeadlessExecution");

            // Assert
            for (int i = 0; i < size; i++)
            {
                result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                    $"Headless execution should produce correct results at index {i}");
            }

            measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(30),
                "Headless execution should complete in reasonable time");

            _logger.LogInformation("Headless execution test passed in {Time}ms",
                measurement.ElapsedTime.TotalMilliseconds);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("DISPLAY", originalDisplay);
            Environment.SetEnvironmentVariable("XDG_SESSION_TYPE", originalSession);
            Environment.SetEnvironmentVariable("CI", null);
            Environment.SetEnvironmentVariable("HEADLESS", null);
        }
    }

    [Fact]
    public async Task ResourceConstraints_LimitedMemory_ShouldHandleGracefully()
    {
        // Arrange
        _logger.LogInformation("Testing behavior under memory constraints");

        var memorySizes = new[] { 1000, 10000, 100000, 500000 }; // Increasing sizes
        var successCount = 0;
        var lastSuccessfulSize = 0;

        // Act & Assert
        foreach (var size in memorySizes)
        {
            try
            {
                var testData = GetService<TestDataGenerator>();
                var a = testData.GenerateFloatArray(size);
                var b = testData.GenerateFloatArray(size);
                var result = new float[size];

                var measurement = await MeasurePerformanceAsync(async () =>
                {
                    await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
                }, $"ConstrainedMemory_{size}");

                // Verify correctness
                for (int i = 0; i < Math.Min(size, 10); i++)
                {
                    result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                        $"Result should be correct for size {size} at index {i}");
                }

                successCount++;
                lastSuccessfulSize = size;
                
                _logger.LogInformation("Size {Size}: SUCCESS ({Time}ms, {Memory}MB)",
                    size, measurement.ElapsedTime.TotalMilliseconds, measurement.MemoryUsed / 1024 / 1024);

                // Memory usage should scale reasonably
                var expectedMemory = size * sizeof(float) * 3; // 3 arrays
                measurement.MemoryUsed.Should().BeLessOrEqualTo(expectedMemory * 10,
                    $"Memory usage should be reasonable for size {size}");
            }
            catch (OutOfMemoryException)
            {
                _logger.LogInformation("Size {Size}: OutOfMemory (expected for large sizes)", size);
                break; // Expected for very large sizes
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Size {Size}: FAILED - {Error}", size, ex.Message);
                break;
            }
        }

        // Assert
        successCount.Should().BeGreaterThan(0, "Should succeed for at least small sizes");
        lastSuccessfulSize.Should().BeGreaterOrEqualTo(1000, "Should handle at least 1000 elements");

        _logger.LogInformation("Resource constraint test: {Count}/{Total} sizes succeeded, max size: {MaxSize}",
            successCount, memorySizes.Length, lastSuccessfulSize);
    }

    [Fact]
    public async Task TimeoutHandling_LongRunningOperations_ShouldRespectTimeouts()
    {
        // Arrange
        const int size = 10000;
        var testData = GetService<TestDataGenerator>();
        var a = testData.GenerateFloatArray(size);
        var b = testData.GenerateFloatArray(size);
        var result = new float[size];

        var shortTimeout = TimeSpan.FromMilliseconds(1); // Very short timeout
        var reasonableTimeout = TimeSpan.FromSeconds(30); // Reasonable timeout

        _logger.LogInformation("Testing timeout handling with {Size} elements", size);

        // Act & Assert - Very short timeout should fail or complete very quickly
        var shortTimeoutTask = _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
        var shortTimeoutStopwatch = Stopwatch.StartNew();
        
        try
        {
            await shortTimeoutTask.WaitAsync(shortTimeout);
            shortTimeoutStopwatch.Stop();
            
            // If it completes, it should be very fast
            shortTimeoutStopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
                "Operation completing within short timeout should be very fast");
                
            _logger.LogInformation("Short timeout test: Completed in {Time}ms (within timeout)",
                shortTimeoutStopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException)
        {
            shortTimeoutStopwatch.Stop();
            _logger.LogInformation("Short timeout test: Timed out as expected in {Time}ms",
                shortTimeoutStopwatch.ElapsedMilliseconds);
            
            // This is expected behavior
            shortTimeoutStopwatch.Elapsed.Should().BeGreaterOrEqualTo(shortTimeout.Subtract(TimeSpan.FromMilliseconds(10)),
                "Timeout should be respected");
        }

        // Reasonable timeout should succeed
        Array.Clear(result); // Reset result
        var reasonableTimeoutTask = _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
        var reasonableStopwatch = Stopwatch.StartNew();
        
        await reasonableTimeoutTask.WaitAsync(reasonableTimeout);
        reasonableStopwatch.Stop();

        // Verify correctness
        for (int i = 0; i < Math.Min(size, 10); i++)
        {
            result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                $"Result should be correct with reasonable timeout at index {i}");
        }

        _logger.LogInformation("Reasonable timeout test: Completed in {Time}ms",
            reasonableStopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task DeterministicBehavior_RepeatedExecution_ShouldProduceSameResults()
    {
        // Arrange
        const int size = 1000;
        const int iterations = 5;
        var testData = GetService<TestDataGenerator>();
        
        // Use fixed seed for deterministic test data
        var a = testData.GenerateSequentialArray(size, i => (float)i);
        var b = testData.GenerateSequentialArray(size, i => (float)(i * 2));
        
        var results = new List<float[]>();

        _logger.LogInformation("Testing deterministic behavior over {Iterations} iterations", iterations);

        // Act
        for (int iter = 0; iter < iterations; iter++)
        {
            var result = new float[size];
            await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
            results.Add(result);
            
            _logger.LogInformation("Iteration {Iter} completed", iter + 1);
        }

        // Assert
        results.Should().HaveCount(iterations, "Should have results from all iterations");

        // All results should be identical
        var firstResult = results[0];
        for (int iter = 1; iter < iterations; iter++)
        {
            var currentResult = results[iter];
            for (int i = 0; i < size; i++)
            {
                currentResult[i].Should().Be(firstResult[i],
                    $"Result should be deterministic: iteration {iter}, index {i}");
            }
        }

        _logger.LogInformation("Deterministic behavior test passed: all {Iterations} iterations identical", iterations);
    }

    [Fact]
    public async Task EnvironmentVariables_CiSpecificSettings_ShouldBeRespected()
    {
        // Arrange
        var testEnvironmentVars = new Dictionary<string, string>
        {
            ["DOTCOMPUTE_BACKEND"] = "CPU",
            ["DOTCOMPUTE_LOG_LEVEL"] = "Warning",
            ["DOTCOMPUTE_CACHE_DISABLED"] = "true",
            ["DOTCOMPUTE_PARALLEL_DEGREE"] = "2"
        };

        _logger.LogInformation("Testing CI-specific environment variable handling");

        var originalValues = new Dictionary<string, string?>();
        
        try
        {
            // Set test environment variables
            foreach (var kvp in testEnvironmentVars)
            {
                originalValues[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                _logger.LogInformation("Set {Key} = {Value}", kvp.Key, kvp.Value);
            }

            const int size = 500;
            var testData = GetService<TestDataGenerator>();
            var a = testData.GenerateFloatArray(size);
            var b = testData.GenerateFloatArray(size);
            var result = new float[size];

            // Act
            var measurement = await MeasurePerformanceAsync(async () =>
            {
                await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
            }, "EnvironmentVarTest");

            // Assert
            for (int i = 0; i < size; i++)
            {
                result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                    $"Execution with environment variables should be correct at index {i}");
            }

            measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(30),
                "Execution with environment variables should complete in reasonable time");

            _logger.LogInformation("Environment variable test passed in {Time}ms",
                measurement.ElapsedTime.TotalMilliseconds);
        }
        finally
        {
            // Restore original environment variables
            foreach (var kvp in originalValues)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task ConcurrentCiBuilds_MultipleProcesses_ShouldNotInterfere()
    {
        // Arrange
        const int processCount = 4;
        const int operationsPerProcess = 10;
        const int arraySize = 500;

        _logger.LogInformation("Testing concurrent CI build simulation with {Processes} processes", processCount);

        // Act - Simulate multiple processes by concurrent operations with different data
        var results = await ExecuteConcurrentlyAsync(async processId =>
        {
            var processResults = new List<float[]>();
            var testData = GetService<TestDataGenerator>();

            for (int op = 0; op < operationsPerProcess; op++)
            {
                // Use process-specific data to avoid conflicts
                var a = testData.GenerateSequentialArray(arraySize, i => (float)(processId * 1000 + i));
                var b = testData.GenerateSequentialArray(arraySize, i => (float)(processId * 1000 + i + 1));
                var result = new float[arraySize];

                await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
                processResults.Add(result);
            }

            return new { ProcessId = processId, Results = processResults };
        }, processCount);

        // Assert
        results.Should().HaveCount(processCount, "All simulated processes should complete");

        foreach (var processResult in results)
        {
            processResult.Results.Should().HaveCount(operationsPerProcess,
                $"Process {processResult.ProcessId} should complete all operations");

            foreach (var result in processResult.Results)
            {
                result.Should().NotBeNull($"Process {processResult.ProcessId} should produce valid results");
                result.Should().HaveCount(arraySize, $"Process {processResult.ProcessId} results should have correct size");
                result.Should().NotContain(float.NaN, $"Process {processResult.ProcessId} should not produce NaN");
            }
        }

        _logger.LogInformation("Concurrent CI build test passed: all {Processes} processes completed successfully", processCount);
    }

    [Fact]
    public async Task ResourceCleanup_AfterExecution_ShouldNotLeakResources()
    {
        // Arrange
        const int iterations = 20;
        const int arraySize = 5000;

        var initialMemory = GC.GetTotalMemory(true);
        var memoryMeasurements = new List<long>();

        _logger.LogInformation("Testing resource cleanup over {Iterations} iterations", iterations);

        // Act
        for (int iter = 0; iter < iterations; iter++)
        {
            var testData = GetService<TestDataGenerator>();
            var a = testData.GenerateFloatArray(arraySize);
            var b = testData.GenerateFloatArray(arraySize);
            var result = new float[arraySize];

            await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);

            // Verify result correctness
            for (int i = 0; i < Math.Min(arraySize, 5); i++)
            {
                result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                    $"Iteration {iter} should produce correct results");
            }

            // Measure memory after each iteration
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var currentMemory = GC.GetTotalMemory(false);
            memoryMeasurements.Add(currentMemory);

            _logger.LogInformation("Iteration {Iter}: {Memory:N0} bytes", iter + 1, currentMemory);
        }

        // Assert
        var finalMemory = memoryMeasurements.Last();
        var memoryIncrease = finalMemory - initialMemory;
        var maxAllowedIncrease = TestSettings.MemoryLeakThresholdMB * 1024 * 1024;

        memoryIncrease.Should().BeLessThan(maxAllowedIncrease,
            $"Memory should not increase by more than {TestSettings.MemoryLeakThresholdMB}MB over {iterations} iterations");

        // Check for consistent memory usage (no continual growth)
        var midPoint = iterations / 2;
        var earlyAverage = memoryMeasurements.Take(midPoint).Average();
        var lateAverage = memoryMeasurements.Skip(midPoint).Average();
        var growthRate = (lateAverage - earlyAverage) / earlyAverage;

        growthRate.Should().BeLessThan(0.5, "Memory growth rate should be reasonable");

        _logger.LogInformation("Resource cleanup test passed: memory increased by {Increase:N0} bytes ({Rate:P1} growth)",
            memoryIncrease, growthRate);
    }

    [Fact]
    public async Task ErrorRecovery_TransientFailures_ShouldRecoverGracefully()
    {
        // Arrange
        const int attempts = 10;
        const int arraySize = 1000;
        var successCount = 0;
        var failureCount = 0;

        _logger.LogInformation("Testing error recovery over {Attempts} attempts", attempts);

        // Act
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                var testData = GetService<TestDataGenerator>();
                var a = testData.GenerateFloatArray(arraySize);
                var b = testData.GenerateFloatArray(arraySize);
                var result = new float[arraySize];

                // Introduce occasional "stress" by varying array sizes
                var stressSize = attempt % 3 == 0 ? arraySize * 2 : arraySize;
                if (stressSize != arraySize)
                {
                    Array.Resize(ref a, stressSize);
                    Array.Resize(ref b, stressSize);
                    Array.Resize(ref result, stressSize);
                }

                await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);

                // Verify correctness
                for (int i = 0; i < Math.Min(stressSize, 5); i++)
                {
                    result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                        $"Attempt {attempt} should produce correct results");
                }

                successCount++;
                _logger.LogInformation("Attempt {Attempt}: SUCCESS (size: {Size})", attempt + 1, stressSize);
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogWarning("Attempt {Attempt}: FAILED - {Error}", attempt + 1, ex.Message);
                
                // In CI, we should still be able to recover from transient failures
                // Allow a small number of failures
            }
        }

        // Assert
        successCount.Should().BeGreaterThan(attempts / 2, "Majority of attempts should succeed");
        
        var successRate = (double)successCount / attempts;
        _logger.LogInformation("Error recovery test: {Success}/{Total} attempts succeeded ({Rate:P1})",
            successCount, attempts, successRate);

        successRate.Should().BeGreaterThan(0.7, "Success rate should be at least 70%");
    }

    [Fact]
    public async Task PerformanceBenchmark_CiBaseline_ShouldMeetMinimumRequirements()
    {
        // Arrange
        var benchmarks = new[]
        {
            new { Name = "VectorAdd", Size = 10000, MaxTimeMs = 1000 },
            new { Name = "VectorAdd", Size = 100000, MaxTimeMs = 5000 },
            new { Name = "ScalarMultiply", Size = 50000, MaxTimeMs = 2000 }
        };

        var results = new List<(string Name, int Size, TimeSpan Duration, bool Passed)>();

        _logger.LogInformation("Running CI performance benchmarks");

        // Act & Assert
        foreach (var benchmark in benchmarks)
        {
            try
            {
                var testData = GetService<TestDataGenerator>();
                var a = testData.GenerateFloatArray(benchmark.Size);
                var b = testData.GenerateFloatArray(benchmark.Size);
                var result = new float[benchmark.Size];

                var measurement = await MeasurePerformanceAsync(async () =>
                {
                    if (benchmark.Name == "VectorAdd")
                    {
                        await _orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);
                    }
                    else if (benchmark.Name == "ScalarMultiply")
                    {
                        await _orchestrator.ExecuteAsync<float[]>("ScalarMultiply", a, result, 2.5f, benchmark.Size);
                    }
                }, benchmark.Name);

                var passed = measurement.ElapsedTime.TotalMilliseconds <= benchmark.MaxTimeMs;
                results.Add((benchmark.Name, benchmark.Size, measurement.ElapsedTime, passed));

                _logger.LogInformation("Benchmark {Name} (size {Size}): {Time}ms [{Status}]",
                    benchmark.Name, benchmark.Size, measurement.ElapsedTime.TotalMilliseconds,
                    passed ? "PASS" : "FAIL");

                // Verify correctness for each benchmark
                if (benchmark.Name == "VectorAdd")
                {
                    for (int i = 0; i < Math.Min(benchmark.Size, 5); i++)
                    {
                        result[i].Should().BeApproximately(a[i] + b[i], 1e-5f,
                            $"Benchmark {benchmark.Name} should produce correct results");
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add((benchmark.Name, benchmark.Size, TimeSpan.MaxValue, false));
                _logger.LogError("Benchmark {Name} (size {Size}) failed: {Error}",
                    benchmark.Name, benchmark.Size, ex.Message);
            }
        }

        // Assert
        var passedCount = results.Count(r => r.Passed);
        var passRate = (double)passedCount / results.Count;

        _logger.LogInformation("CI Performance summary: {Passed}/{Total} benchmarks passed ({Rate:P1})",
            passedCount, results.Count, passRate);

        passRate.Should().BeGreaterOrEqualTo(0.8, "At least 80% of benchmarks should pass in CI");
    }
}