// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Backends.CPU.Accelerators;
using static DotCompute.Core.Tests.Memory.MemoryTestUtilities;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Comprehensive memory system tests using test utilities and performance monitoring.
/// </summary>
public class MemorySystemTests : IDisposable
{
    private readonly CpuMemoryManager _memoryManager;

    public MemorySystemTests()
    {
        _memoryManager = new CpuMemoryManager();
    }

    [Fact]
    public async Task MemorySystem_CompleteWorkflow_WithMonitoring()
    {
        // Arrange
        using var monitor = new MemoryMonitor("CompleteWorkflow");
        const int dataSize = 1024 * 1024; // 1MB
        
        var sourceData = TestDataGenerator.GenerateRandomFloats(dataSize / sizeof(float));
        monitor.TakeSnapshot("DataGenerated");

        // Act
        var buffer = await _memoryManager.AllocateAsync(dataSize);
        monitor.TakeSnapshot("BufferAllocated");

        await buffer.CopyFromHostAsync<float>(sourceData);
        monitor.TakeSnapshot("DataCopiedToDevice");

        var resultData = new float[sourceData.Length];
        await buffer.CopyToHostAsync<float>(resultData);
        monitor.TakeSnapshot("DataCopiedToHost");

        await buffer.DisposeAsync();
        monitor.TakeSnapshot("BufferDisposed");

        // Assert
        resultData.Should().BeEquivalentTo(sourceData);
        
        var report = monitor.GenerateReport();
        report.MemoryLeakDetected.Should().BeFalse();
        report.PeakWorkingSetMB.Should().BeGreaterThan(0);
        
        // Save report for analysis
        await TestReporter.SaveMemoryReport(report);
    }

    [Fact]
    public async Task MemoryPerformance_BenchmarkAllOperations()
    {
        // Arrange
        var benchmark = new MemoryPerformanceBenchmark();
        var bufferSizes = new long[] { 1024, 4096, 16384, 65536, 262144, 1048576 };
        const int iterations = 100;

        // Act & Assert
        foreach (var bufferSize in bufferSizes)
        {
            // Benchmark allocation
            var allocResult = await benchmark.BenchmarkAllocation(_memoryManager, bufferSize, iterations);
            allocResult.OperationsPerSecond.Should().BeGreaterThan(0);
            allocResult.ThroughputMBps.Should().BeGreaterThan(0);

            // Benchmark copy operations
            var copyToHostResult = await benchmark.BenchmarkCopyToHost(_memoryManager, bufferSize, iterations);
            copyToHostResult.ThroughputMBps.Should().BeGreaterThan(0);

            var copyFromHostResult = await benchmark.BenchmarkCopyFromHost(_memoryManager, bufferSize, iterations);
            copyFromHostResult.ThroughputMBps.Should().BeGreaterThan(0);
        }

        var summary = benchmark.GenerateSummary();
        summary.TotalOperations.Should().BeGreaterThan(0);
        summary.AverageOperationsPerSecond.Should().BeGreaterThan(0);
        
        // Save benchmark report
        await TestReporter.SaveBenchmarkReport(summary, "AllOperations");
    }

    [Fact]
    public async Task MemoryRegression_DetectPerformanceChanges()
    {
        // Arrange
        var benchmark = new MemoryPerformanceBenchmark();
        const long bufferSize = 1024 * 1024;
        const int iterations = 50;

        // Act
        var currentResult = await benchmark.BenchmarkAllocation(_memoryManager, bufferSize, iterations);
        
        // Create a baseline (in real tests, this would be loaded from historical data)
        var baselineResult = new BenchmarkResult
        {
            OperationType = "Allocation",
            BufferSize = bufferSize,
            Iterations = iterations,
            TotalTimeMs = currentResult.TotalTimeMs + 100, // Simulate slower baseline
            OperationsPerSecond = currentResult.OperationsPerSecond * 0.8, // 20% slower
            ThroughputMBps = currentResult.ThroughputMBps * 0.8
        };

        // Assert
        var hasRegression = RegressionDetector.DetectPerformanceRegression(currentResult, baselineResult, 10.0);
        hasRegression.Should().BeFalse("Current performance should be better than baseline");
        
        // Test regression detection
        var slowerResult = new BenchmarkResult
        {
            OperationType = "Allocation",
            BufferSize = bufferSize,
            Iterations = iterations,
            TotalTimeMs = currentResult.TotalTimeMs * 2,
            OperationsPerSecond = currentResult.OperationsPerSecond * 0.5, // 50% slower
            ThroughputMBps = currentResult.ThroughputMBps * 0.5
        };

        var hasSlowRegression = RegressionDetector.DetectPerformanceRegression(slowerResult, currentResult, 10.0);
        hasSlowRegression.Should().BeTrue("50% slower performance should be detected as regression");
    }

    [Fact]
    public async Task MemoryLeak_DetectionWithUtilities()
    {
        // Arrange
        using var monitor = new MemoryMonitor("MemoryLeakDetection");
        const int iterations = 1000;
        const long bufferSize = 1024 * 1024;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(bufferSize);
            await buffer.DisposeAsync();
            
            if (i % 100 == 0)
            {
                monitor.TakeSnapshot($"Iteration{i}");
            }
        }

        // Assert
        var report = monitor.GenerateReport();
        report.MemoryLeakDetected.Should().BeFalse();
        
        // Peak memory should be reasonable
        report.PeakWorkingSetMB.Should().BeLessThan(100); // Shouldn't use more than 100MB
    }

    [Fact]
    public async Task DataIntegrity_LargeDataSets_MaintainsAccuracy()
    {
        // Arrange
        const int arraySize = 1024 * 1024; // 1M elements
        var sourceInts = TestDataGenerator.GenerateRandomInts(arraySize, -1000000, 1000000);
        var sourceFloats = TestDataGenerator.GenerateRandomFloats(arraySize, -1000.0f, 1000.0f);
        var sourceBytes = TestDataGenerator.GenerateRandomBytes(arraySize);

        // Test with different data types
        await TestDataType(sourceInts, sizeof(int));
        await TestDataType(sourceFloats, sizeof(float));
        await TestDataType(sourceBytes, sizeof(byte));
    }

    private async Task TestDataType<T>(T[] sourceData, int elementSize) where T : unmanaged
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(sourceData.Length * elementSize);

        try
        {
            // Act
            await buffer.CopyFromHostAsync<T>(sourceData);
            var resultData = new T[sourceData.Length];
            await buffer.CopyToHostAsync<T>(resultData);

            // Assert
            resultData.Should().BeEquivalentTo(sourceData);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryAlignment_DifferentSizes_WorksCorrectly()
    {
        // Arrange
        var testSizes = new long[] { 1, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047 };
        var buffers = new List<IMemoryBuffer>();

        try
        {
            // Act
            foreach (var size in testSizes)
            {
                var buffer = await _memoryManager.AllocateAsync(size);
                buffers.Add(buffer);
                
                // Test that we can access the full buffer
                var testData = TestDataGenerator.GenerateRandomBytes((int)size);
                await buffer.CopyFromHostAsync<byte>(testData);
                
                var resultData = new byte[size];
                await buffer.CopyToHostAsync<byte>(resultData);
                
                // Assert
                resultData.Should().BeEquivalentTo(testData);
            }
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task MemoryViews_ComplexOperations_MaintainConsistency()
    {
        // Arrange
        const int totalSize = 1024 * 1024;
        const int viewSize = 1024;
        const int numViews = 100;
        
        var buffer = await _memoryManager.AllocateAsync(totalSize);
        var views = new List<IMemoryBuffer>();

        try
        {
            // Create multiple views
            for (int i = 0; i < numViews; i++)
            {
                var offset = i * viewSize;
                if (offset + viewSize <= totalSize)
                {
                    var view = _memoryManager.CreateView(buffer, offset, viewSize);
                    views.Add(view);
                }
            }

            // Act - Write unique data to each view
            for (int i = 0; i < views.Count; i++)
            {
                var viewData = TestDataGenerator.GenerateSequentialArray(viewSize, (byte)i, b => (byte)(b + 1));
                await views[i].CopyFromHostAsync<byte>(viewData);
            }

            // Assert - Verify each view contains its unique data
            for (int i = 0; i < views.Count; i++)
            {
                var expectedData = TestDataGenerator.GenerateSequentialArray(viewSize, (byte)i, b => (byte)(b + 1));
                var actualData = new byte[viewSize];
                await views[i].CopyToHostAsync<byte>(actualData);
                
                actualData.Should().BeEquivalentTo(expectedData, 
                    $"View {i} should contain its unique data pattern");
            }
        }
        finally
        {
            // Cleanup
            foreach (var view in views)
            {
                await view.DisposeAsync();
            }
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryReporting_HistoricalData_TracksProgress()
    {
        // Arrange
        const string testName = "HistoricalTracking";
        using var monitor = new MemoryMonitor(testName);
        
        // Simulate some work
        var buffer = await _memoryManager.AllocateAsync(1024 * 1024);
        var data = TestDataGenerator.GenerateRandomBytes(1024 * 1024);
        await buffer.CopyFromHostAsync<byte>(data);
        monitor.TakeSnapshot("WorkCompleted");
        await buffer.DisposeAsync();

        // Act
        var currentReport = monitor.GenerateReport();
        await TestReporter.SaveMemoryReport(currentReport);

        // Load historical reports
        var historicalReports = await TestReporter.LoadHistoricalReports(testName);

        // Assert
        historicalReports.Should().NotBeEmpty();
        var latestReport = historicalReports.OrderBy(r => r.Snapshots.FirstOrDefault()?.ElapsedMs ?? 0).LastOrDefault();
        latestReport.Should().NotBeNull();
        latestReport.TestName.Should().Be(testName);
    }

    [Fact]
    public async Task MemoryUtilities_EdgeCases_HandleGracefully()
    {
        // Test with zero-length arrays
        var emptyBytes = TestDataGenerator.GenerateRandomBytes(0);
        emptyBytes.Should().BeEmpty();

        var emptyFloats = TestDataGenerator.GenerateRandomFloats(0);
        emptyFloats.Should().BeEmpty();

        // Test with large arrays
        var largeArray = TestDataGenerator.GenerateSequentialArray(1000000, 0, i => i + 1);
        largeArray.Should().HaveCount(1000000);
        largeArray[0].Should().Be(0);
        largeArray[999999].Should().Be(999999);

        // Test boundary conditions
        var buffer = await _memoryManager.AllocateAsync(1);
        var singleByte = new byte[] { 42 };
        await buffer.CopyFromHostAsync<byte>(singleByte);
        
        var result = new byte[1];
        await buffer.CopyToHostAsync<byte>(result);
        result[0].Should().Be(42);
        
        await buffer.DisposeAsync();
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
        
        // Cleanup old test reports
        TestReporter.CleanupOldReports(7); // Keep reports for 7 days
    }
}