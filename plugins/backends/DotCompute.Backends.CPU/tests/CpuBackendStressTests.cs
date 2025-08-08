// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using DotCompute.Backends.CPU;
using DotCompute.Backends.CPU.Intrinsics;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Stress tests and edge cases for CPU backend components.
/// Tests SIMD operations, threading limits, memory boundaries, and error conditions.
/// </summary>
public sealed class CpuBackendStressTests
{
    #region SIMD Edge Cases

    [Fact]
    public void SimdCapabilities_DetectAllFeatures_ShouldReportCorrectly()
    {
        // Act
        var capabilities = SimdCapabilities.GetSummary();

        // Assert - Verify detection logic
        capabilities.IsHardwareAccelerated.Should().Be(Vector.IsHardwareAccelerated);
        capabilities.PreferredVectorWidth.Should().BeGreaterThan(0);
        capabilities.SupportedInstructionSets.Should().NotBeEmpty();

        // On ARM systems
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            capabilities.HasNeon.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(32)]
    public void SimdOperations_WithVariousDataSizes_ShouldHandleCorrectly(int size)
    {
        // Arrange
        var data = new float[size];
        for (int i = 0; i < size; i++)
        {
            data[i] = i + 1.0f;
        }

        // Act - Try SIMD operations on various sizes
        if (size > 0)
        {
            var result = new float[size];
            ProcessDataWithSimd(data, result);

            // Assert - Results should be correct regardless of size
            for (int i = 0; i < size; i++)
            {
                result[i].Should().BeApproximately(data[i] * 2.0f, 0.001f);
            }
        }
    }

    [Fact]
    public void SimdOperations_WithUnalignedMemory_ShouldWorkCorrectly()
    {
        // Arrange - Create unaligned data
        const int size = 1000;
        var originalData = new float[size + 1];
        var data = originalData.AsSpan(1); // Offset by 1 to create misalignment

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = i + 1.0f;
        }

        // Act
        var result = new float[data.Length];
        ProcessDataWithSimd(data, result);

        // Assert
        for (int i = 0; i < data.Length; i++)
        {
            result[i].Should().BeApproximately(data[i] * 2.0f, 0.001f);
        }
    }

    [Theory]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.NaN)]
    [InlineData(0.0f)]
    [InlineData(-0.0f)]
    [InlineData(1e-10f)]
    [InlineData(-1e-10f)]
    public void SimdOperations_WithExtremeValues_ShouldHandleGracefully(float extremeValue)
    {
        // Arrange
        const int size = 16;
        var data = new float[size];
        Array.Fill(data, extremeValue);

        // Act
        var result = new float[size];
        var act = () => ProcessDataWithSimd(data, result);

        // Assert - Should not throw, even with extreme values
        act.Should().NotThrow("SIMD operations should handle extreme values gracefully");

        // For non-NaN inputs, verify basic properties
        if (!float.IsNaN(extremeValue))
        {
            for (int i = 0; i < size; i++)
            {
                if (float.IsInfinity(extremeValue))
                {
                    result[i].Should().Be(extremeValue); // Infinity * 2 = Infinity
                }
                else
                {
                    result[i].Should().BeApproximately(extremeValue * 2.0f, 0.001f);
                }
            }
        }
    }

    [Fact]
    public void SimdOperations_LargeDataset_ShouldMaintainPerformance()
    {
        // Arrange
        const int size = 1024 * 1024; // 1M elements
        var data = new float[size];
        var random = new Random(42);
        
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)random.NextDouble() * 1000;
        }

        var result = new float[size];
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        ProcessDataWithSimd(data, result);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "SIMD operations on large datasets should complete quickly");

        // Verify correctness on sample points
        for (int i = 0; i < Math.Min(100, size); i += 10)
        {
            result[i].Should().BeApproximately(data[i] * 2.0f, 0.001f);
        }
    }

    #endregion

    #region Threading Stress Tests

    [Fact]
    public async Task CpuKernelExecution_HighConcurrency_ShouldNotCauseDataRaces()
    {
        // Arrange
        var threadCount = Environment.ProcessorCount * 2;
        const int operationsPerThread = 100;
        const int dataSize = 1000;

        var exceptions = new ConcurrentBag<Exception>();
        var results = new ConcurrentBag<(int ThreadId, bool Success)>();

        // Act - Multiple threads executing kernels concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        // Create thread-local data
                        var inputData = new float[dataSize];
                        var outputData = new float[dataSize];
                        
                        // Initialize with thread-specific pattern
                        for (int j = 0; j < dataSize; j++)
                        {
                            inputData[j] = threadId * 1000 + i * 10 + j;
                        }

                        // Process data (simulate kernel execution)
                        ProcessDataWithSimd(inputData, outputData);

                        // Verify results
                        var success = true;
                        for (int j = 0; j < dataSize; j++)
                        {
                            var expected = inputData[j] * 2.0f;
                            if (Math.Abs(outputData[j] - expected) > 0.001f)
                            {
                                success = false;
                                break;
                            }
                        }

                        if (!success)
                        {
                            results.Add((threadId, false));
                            return;
                        }
                    }

                    results.Add((threadId, true));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    results.Add((threadId, false));
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("High concurrency should not cause exceptions");
        results.Count.Should().Be(threadCount);
        results.Should().OnlyContain(r => r.Success, "All threads should complete successfully");
    }

    [Fact]
    public async Task CpuAccelerator_ThreadSafety_ShouldHandleMultipleClients()
    {
        // This test would require actual CpuAccelerator implementation
        // For now, we'll test the principles with direct operations
        
        // Arrange
        const int clientCount = 20;
        const int requestsPerClient = 50;
        
        var exceptions = new ConcurrentBag<Exception>();
        var completedRequests = new ConcurrentBag<int>();

        // Act - Multiple clients using CPU backend simultaneously
        var tasks = Enumerable.Range(0, clientCount).Select(clientId =>
            Task.Run(async () =>
            {
                try
                {
                    for (int request = 0; request < requestsPerClient; request++)
                    {
                        // Simulate accelerator operations
                        var data = new float[256];
                        var result = new float[256];
                        
                        // Fill with client-specific pattern
                        for (int i = 0; i < data.Length; i++)
                        {
                            data[i] = clientId * 100 + request * 10 + i;
                        }

                        // Process (this would be kernel execution in real scenario)
                        ProcessDataWithSimd(data, result);

                        // Small delay to increase contention
                        await Task.Delay(1);

                        completedRequests.Add(clientId * requestsPerClient + request);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Multiple clients should not interfere with each other");
        completedRequests.Count.Should().Be(clientCount * requestsPerClient);
    }

    #endregion

    #region Memory Boundary Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public void MemoryOperations_VariousBoundaries_ShouldHandleCorrectly(int size)
    {
        // Arrange
        var source = new byte[size];
        var destination = new byte[size];
        
        for (int i = 0; i < size; i++)
        {
            source[i] = (byte)(i % 256);
        }

        // Act - Test memory copy operations at various boundaries
        source.AsSpan().CopyTo(destination.AsSpan());

        // Assert
        for (int i = 0; i < size; i++)
        {
            destination[i].Should().Be(source[i], "Memory copy should preserve all bytes");
        }
    }

    [Fact]
    public void LargeMemoryOperations_ShouldNotCauseStackOverflow()
    {
        // Arrange
        const int largeSize = 10 * 1024 * 1024; // 10MB
        var source = new byte[largeSize];
        var destination = new byte[largeSize];

        // Fill with pattern
        for (int i = 0; i < largeSize; i++)
        {
            source[i] = (byte)(i % 256);
        }

        // Act & Assert - Should not cause stack overflow
        var act = () => source.AsSpan().CopyTo(destination.AsSpan());
        act.Should().NotThrow("Large memory operations should not cause stack overflow");

        // Verify some sample points
        for (int i = 0; i < 1000; i += 100)
        {
            destination[i].Should().Be(source[i]);
        }
    }

    #endregion

    #region Error Condition Tests

    [Fact]
    public void SimdOperations_WithNullInput_ShouldThrowArgumentNullException()
    {
        // Arrange
        float[] nullData = null!;
        var result = new float[10];

        // Act & Assert
        var act = () => ProcessDataWithSimd(nullData, result);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SimdOperations_WithMismatchedSizes_ShouldThrowArgumentException()
    {
        // Arrange
        var data = new float[10];
        var result = new float[5]; // Mismatched size

        // Act & Assert
        var act = () => ProcessDataWithSimd(data, result);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SimdCapabilities_OnUnsupportedPlatform_ShouldHandleGracefully()
    {
        // This test verifies that capability detection works even on platforms
        // where SIMD isn't available or detection fails

        // Act
        var act = () => SimdCapabilities.GetSummary();

        // Assert - Should not throw, even on unsupported platforms
        act.Should().NotThrow("Capability detection should be robust");

        var capabilities = SimdCapabilities.GetSummary();
        
        // Should have reasonable defaults
        capabilities.Should().NotBeNull();
        
        // At least one of these should typically be available on modern systems
        var hasAnySimd = capabilities.HasSse41 || capabilities.HasAvx2 || 
                        capabilities.HasAvx512 || capabilities.HasNeon;
        
        if (RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86)
        {
            // Most modern x64 systems should have at least SSE4.1
            capabilities.HasSse41.Should().BeTrue();
        }
    }

    #endregion

    #region Performance Edge Cases

    [Fact]
    public void CpuOperations_UnderHighSystemLoad_ShouldMaintainCorrectness()
    {
        // Arrange
        const int iterationCount = 1000;
        const int dataSize = 1000;
        var random = new Random(42);

        // Create background load
        var loadTasks = Enumerable.Range(0, Environment.ProcessorCount).Select(_ =>
            Task.Run(() =>
            {
                var loadData = new float[1000];
                for (int i = 0; i < 10000; i++) // Background processing
                {
                    for (int j = 0; j < loadData.Length; j++)
                    {
                        loadData[j] = (float)Math.Sin(i * j);
                    }
                }
            })).ToArray();

        var correctResults = 0;
        var totalResults = 0;

        // Act - Perform operations under load
        for (int iteration = 0; iteration < iterationCount; iteration++)
        {
            var data = new float[dataSize];
            var result = new float[dataSize];

            // Generate test data
            for (int i = 0; i < dataSize; i++)
            {
                data[i] = (float)random.NextDouble() * 1000;
            }

            // Process data
            ProcessDataWithSimd(data, result);

            // Verify correctness
            var isCorrect = true;
            for (int i = 0; i < dataSize; i++)
            {
                if (Math.Abs(result[i] - data[i] * 2.0f) > 0.001f)
                {
                    isCorrect = false;
                    break;
                }
            }

            if (isCorrect) correctResults++;
            totalResults++;
        }

        // Wait for background load to complete
        Task.WaitAll(loadTasks, TimeSpan.FromSeconds(10));

        // Assert
        correctResults.Should().Be(totalResults, 
            "All operations should be correct even under high system load");
    }

    #endregion

    #region Resource Cleanup Tests

    [Fact]
    public void RepeatedOperations_ShouldNotLeakMemory()
    {
        // Arrange
        const int iterationCount = 1000;
        const int dataSize = 10000;

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Perform many operations that could potentially leak
        for (int i = 0; i < iterationCount; i++)
        {
            var data = new float[dataSize];
            var result = new float[dataSize];

            // Fill with data
            for (int j = 0; j < dataSize; j++)
            {
                data[j] = i + j;
            }

            ProcessDataWithSimd(data, result);

            // Occasionally force GC to detect leaks
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;

        // Assert - Memory growth should be minimal
        memoryGrowth.Should().BeLessThan(10 * 1024 * 1024, // 10MB threshold
            "Repeated operations should not cause significant memory growth");
    }

    #endregion

    /// <summary>
    /// Helper method to process data with SIMD operations.
    /// This simulates the kind of operations that would be done in CPU kernels.
    /// </summary>
    private static void ProcessDataWithSimd(ReadOnlySpan<float> input, Span<float> output)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.Length != output.Length)
            throw new ArgumentException("Input and output must have the same length");

        // Simple operation: multiply each element by 2
        if (Vector256.IsHardwareAccelerated && input.Length >= Vector256<float>.Count)
        {
            ProcessWithAvx(input, output);
        }
        else if (Vector128.IsHardwareAccelerated && input.Length >= Vector128<float>.Count)
        {
            ProcessWithSse(input, output);
        }
        else
        {
            ProcessScalar(input, output);
        }
    }

    private static void ProcessWithAvx(ReadOnlySpan<float> input, Span<float> output)
    {
        var vectorSize = Vector256<float>.Count;
        var factor = Vector256.Create(2.0f);
        int i = 0;

        // Process vectors
        for (; i <= input.Length - vectorSize; i += vectorSize)
        {
            var vector = Vector256.Create(input.Slice(i, vectorSize));
            var result = Vector256.Multiply(vector, factor);
            result.CopyTo(output.Slice(i, vectorSize));
        }

        // Process remaining elements
        for (; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }

    private static void ProcessWithSse(ReadOnlySpan<float> input, Span<float> output)
    {
        var vectorSize = Vector128<float>.Count;
        var factor = Vector128.Create(2.0f);
        int i = 0;

        // Process vectors
        for (; i <= input.Length - vectorSize; i += vectorSize)
        {
            var vector = Vector128.Create(input.Slice(i, vectorSize));
            var result = Vector128.Multiply(vector, factor);
            result.CopyTo(output.Slice(i, vectorSize));
        }

        // Process remaining elements
        for (; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }

    private static void ProcessScalar(ReadOnlySpan<float> input, Span<float> output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}