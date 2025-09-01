// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotCompute.Abstractions;
using DotCompute.Tests.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Performance benchmarks for memory operations including allocation speed,
/// transfer bandwidth, memory pool efficiency, and concurrent allocation performance.
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("Category", TestCategories.MemoryIntensive)]
public class MemoryPerformanceTests : GpuTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly MockMemoryManager _memoryManager;
    private readonly CancellationTokenSource _cts;
    
    // Test data sizes (in bytes)
    private static readonly int[] TestDataSizes = 
    {
        1024,           // 1 KB
        64 * 1024,      // 64 KB  
        1024 * 1024,    // 1 MB
        16 * 1024 * 1024, // 16 MB
        64 * 1024 * 1024, // 64 MB
        256 * 1024 * 1024 // 256 MB
    };
    
    public MemoryPerformanceTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _memoryManager = new MockMemoryManager();
        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
    }

    #region Memory Allocation Performance

    [Theory]
    [MemberData(nameof(GetAllocationTestSizes))]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateMemory_MeasuresAllocationSpeed_ScalesWithSize(int sizeBytes, string description)
    {
        // Arrange
        const int iterations = 100;
        var allocationTimes = new List<double>();
        var allocatedBuffers = new List<IMemoryBuffer>();
        
        using var perfContext = CreatePerformanceContext($"Memory Allocation - {description}");

        try
        {
            // Act - Measure allocation performance
            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var buffer = await _memoryManager.AllocateAsync(sizeBytes, _cts.Token);
                sw.Stop();
                
                allocationTimes.Add(sw.Elapsed.TotalMicroseconds);
                allocatedBuffers.Add(buffer);
                
                if (i % 20 == 0)
                {
                    perfContext.Checkpoint($"Allocated {i + 1}/{iterations} buffers");
                }
            }

            // Analyze results
            var avgTime = allocationTimes.Average();
            var minTime = allocationTimes.Min();
            var maxTime = allocationTimes.Max();
            var stdDev = CalculateStandardDeviation(allocationTimes);
            var throughput = (sizeBytes * iterations / (1024.0 * 1024.0)) / (allocationTimes.Sum() / 1_000_000.0); // MB/s
            
            _output.WriteLine($"Allocation Performance - {description} ({sizeBytes:N0} bytes):");
            _output.WriteLine($"  Average time: {avgTime:F2}μs");
            _output.WriteLine($"  Min time: {minTime:F2}μs");
            _output.WriteLine($"  Max time: {maxTime:F2}μs");
            _output.WriteLine($"  Std deviation: {stdDev:F2}μs");
            _output.WriteLine($"  Throughput: {throughput:F1} MB/s");
            _output.WriteLine($"  Consistency (CV): {(stdDev / avgTime) * 100:F1}%");
            
            // Performance assertions
            avgTime.Should().BeLessThan(10000, "Average allocation time should be under 10ms");
            (stdDev / avgTime).Should().BeLessThan(1.0, "Allocation times should be reasonably consistent");
        }
        finally
        {
            // Cleanup
            foreach (var buffer in allocatedBuffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateMemory_MeasuresPoolEfficiency_ComparesPooledVsUnpooled()
    {
        // Arrange
        const int iterations = 200;
        const int bufferSize = 1024 * 1024; // 1 MB
        
        var pooledTimes = new List<double>();
        var unpooledTimes = new List<double>();
        
        using var perfContext = CreatePerformanceContext("Memory Pool Efficiency");
        
        var pooledManager = new MockMemoryManager(usePool: true);
        var unpooledManager = new MockMemoryManager(usePool: false);

        try
        {
            // Warmup
            for (var i = 0; i < 10; i++)
            {
                var warmupBuffer1 = await pooledManager.AllocateAsync(bufferSize, _cts.Token);
                var warmupBuffer2 = await unpooledManager.AllocateAsync(bufferSize, _cts.Token);
                await warmupBuffer1.DisposeAsync();
                await warmupBuffer2.DisposeAsync();
            }
            
            perfContext.Checkpoint("Warmup completed");

            // Test pooled allocations
            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var buffer = await pooledManager.AllocateAsync(bufferSize, _cts.Token);
                sw.Stop();
                
                pooledTimes.Add(sw.Elapsed.TotalMicroseconds);
                await buffer.DisposeAsync();
            }
            
            perfContext.Checkpoint("Pooled allocations completed");

            // Test unpooled allocations
            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var buffer = await unpooledManager.AllocateAsync(bufferSize, _cts.Token);
                sw.Stop();
                
                unpooledTimes.Add(sw.Elapsed.TotalMicroseconds);
                await buffer.DisposeAsync();
            }
            
            perfContext.Checkpoint("Unpooled allocations completed");

            // Analyze and compare results
            var pooledAvg = pooledTimes.Average();
            var unpooledAvg = unpooledTimes.Average();
            var pooledStdDev = CalculateStandardDeviation(pooledTimes);
            var unpooledStdDev = CalculateStandardDeviation(unpooledTimes);
            var speedupRatio = unpooledAvg / pooledAvg;
            
            _output.WriteLine($"Memory Pool Efficiency Comparison:");
            _output.WriteLine($"  Pooled allocations:");
            _output.WriteLine($"    Average: {pooledAvg:F2}μs");
            _output.WriteLine($"    Std Dev: {pooledStdDev:F2}μs");
            _output.WriteLine($"    CV: {(pooledStdDev / pooledAvg) * 100:F1}%");
            _output.WriteLine($"  Unpooled allocations:");
            _output.WriteLine($"    Average: {unpooledAvg:F2}μs");
            _output.WriteLine($"    Std Dev: {unpooledStdDev:F2}μs");
            _output.WriteLine($"    CV: {(unpooledStdDev / unpooledAvg) * 100:F1}%");
            _output.WriteLine($"  Pool speedup: {speedupRatio:F2}x");
            
            // Performance expectations
            pooledAvg.Should().BeLessThan(unpooledAvg, "Pooled allocations should be faster than unpooled");
            speedupRatio.Should().BeGreaterThan(1.1, "Memory pool should provide at least 10% speedup");
        }
        finally
        {
            await pooledManager.DisposeAsync();
            await unpooledManager.DisposeAsync();
        }
    }

    #endregion

    #region Memory Transfer Performance

    [Theory]
    [MemberData(nameof(GetTransferTestSizes))]
    [Trait("Category", TestCategories.Performance)]
    public async Task TransferMemory_MeasuresHostToDeviceBandwidth_ScalesWithSize(int sizeBytes, string description)
    {
        // Arrange
        const int iterations = 20;
        var transferTimes = new List<double>();
        var hostData = GenerateRandomFloats(sizeBytes / sizeof(float));
        
        using var perfContext = CreatePerformanceContext($"H2D Transfer - {description}");
        using var deviceBuffer = await _memoryManager.AllocateAsync(sizeBytes, _cts.Token);

        // Warmup
        for (var i = 0; i < 3; i++)
        {
            await deviceBuffer.CopyFromAsync(hostData, _cts.Token);
        }
        
        perfContext.Checkpoint("Warmup completed");

        // Act - Measure transfer performance
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await deviceBuffer.CopyFromAsync(hostData, _cts.Token);
            await _memoryManager.SynchronizeAsync(_cts.Token); // Ensure transfer completion
            sw.Stop();
            
            transferTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Analyze bandwidth results
        var avgTimeUs = transferTimes.Average();
        var minTimeUs = transferTimes.Min();
        var maxTimeUs = transferTimes.Max();
        var bandwidthGBps = CalculateBandwidth(sizeBytes, avgTimeUs / 1000.0); // Convert μs to ms
        var peakBandwidthGBps = CalculateBandwidth(sizeBytes, minTimeUs / 1000.0);
        
        _output.WriteLine($"Host-to-Device Transfer - {description} ({sizeBytes:N0} bytes):");
        _output.WriteLine($"  Average time: {avgTimeUs:F2}μs");
        _output.WriteLine($"  Min time: {minTimeUs:F2}μs");
        _output.WriteLine($"  Max time: {maxTimeUs:F2}μs");
        _output.WriteLine($"  Average bandwidth: {bandwidthGBps:F2} GB/s");
        _output.WriteLine($"  Peak bandwidth: {peakBandwidthGBps:F2} GB/s");
        _output.WriteLine($"  Efficiency: {(bandwidthGBps / GetTheoreticalBandwidth()) * 100:F1}% of theoretical");
        
        // Performance expectations
        bandwidthGBps.Should().BeGreaterThan(0.1, "Should achieve measurable bandwidth");
        (peakBandwidthGBps / bandwidthGBps).Should().BeLessThan(2.0, "Performance should be reasonably consistent");
    }

    [Theory]
    [MemberData(nameof(GetTransferTestSizes))]
    [Trait("Category", TestCategories.Performance)]
    public async Task TransferMemory_MeasuresDeviceToHostBandwidth_ScalesWithSize(int sizeBytes, string description)
    {
        // Arrange
        const int iterations = 20;
        var transferTimes = new List<double>();
        var hostData = new float[sizeBytes / sizeof(float)];
        
        using var perfContext = CreatePerformanceContext($"D2H Transfer - {description}");
        using var deviceBuffer = await _memoryManager.AllocateAsync(sizeBytes, _cts.Token);
        
        // Initialize device buffer with test data
        var initData = GenerateRandomFloats(sizeBytes / sizeof(float));
        await deviceBuffer.CopyFromAsync(initData, _cts.Token);

        // Warmup
        for (var i = 0; i < 3; i++)
        {
            await deviceBuffer.CopyToAsync(hostData, _cts.Token);
        }
        
        perfContext.Checkpoint("Warmup completed");

        // Act - Measure transfer performance
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await deviceBuffer.CopyToAsync(hostData, _cts.Token);
            await _memoryManager.SynchronizeAsync(_cts.Token);
            sw.Stop();
            
            transferTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Analyze bandwidth results
        var avgTimeUs = transferTimes.Average();
        var bandwidthGBps = CalculateBandwidth(sizeBytes, avgTimeUs / 1000.0);
        var consistency = CalculateStandardDeviation(transferTimes) / avgTimeUs;
        
        _output.WriteLine($"Device-to-Host Transfer - {description} ({sizeBytes:N0} bytes):");
        _output.WriteLine($"  Average time: {avgTimeUs:F2}μs");
        _output.WriteLine($"  Bandwidth: {bandwidthGBps:F2} GB/s");
        _output.WriteLine($"  Consistency: {consistency * 100:F1}% variation");
        
        // Performance assertions
        bandwidthGBps.Should().BeGreaterThan(0.1, "Should achieve measurable D2H bandwidth");
        consistency.Should().BeLessThan(0.3, "Transfer times should be reasonably consistent");
    }

    [Theory]
    [MemberData(nameof(GetTransferTestSizes))]
    [Trait("Category", TestCategories.Performance)]
    public async Task TransferMemory_MeasuresDeviceToDeviceBandwidth_OptimalPerformance(int sizeBytes, string description)
    {
        // Arrange
        const int iterations = 30;
        var transferTimes = new List<double>();
        
        using var perfContext = CreatePerformanceContext($"D2D Transfer - {description}");
        using var sourceBuffer = await _memoryManager.AllocateAsync(sizeBytes, _cts.Token);
        using var destBuffer = await _memoryManager.AllocateAsync(sizeBytes, _cts.Token);
        
        // Initialize source buffer
        var initData = GenerateRandomFloats(sizeBytes / sizeof(float));
        await sourceBuffer.CopyFromAsync(initData, _cts.Token);

        // Warmup
        for (var i = 0; i < 5; i++)
        {
            await sourceBuffer.CopyToAsync(destBuffer, _cts.Token);
        }
        
        perfContext.Checkpoint("Warmup completed");

        // Act - Measure D2D transfer performance
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await sourceBuffer.CopyToAsync(destBuffer, _cts.Token);
            await _memoryManager.SynchronizeAsync(_cts.Token);
            sw.Stop();
            
            transferTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Analyze D2D performance
        var avgTimeUs = transferTimes.Average();
        var bandwidthGBps = CalculateBandwidth(sizeBytes, avgTimeUs / 1000.0);
        var minTimeUs = transferTimes.Min();
        var peakBandwidthGBps = CalculateBandwidth(sizeBytes, minTimeUs / 1000.0);
        
        _output.WriteLine($"Device-to-Device Transfer - {description} ({sizeBytes:N0} bytes):");
        _output.WriteLine($"  Average time: {avgTimeUs:F2}μs");
        _output.WriteLine($"  Average bandwidth: {bandwidthGBps:F2} GB/s");
        _output.WriteLine($"  Peak bandwidth: {peakBandwidthGBps:F2} GB/s");
        _output.WriteLine($"  Theoretical efficiency: {(bandwidthGBps / GetTheoreticalInternalBandwidth()) * 100:F1}%");
        
        // D2D should be faster than H2D/D2H transfers
        var theoreticalBandwidth = GetTheoreticalInternalBandwidth();
        bandwidthGBps.Should().BeGreaterThan(GetTheoreticalBandwidth() * 0.8, 
            "D2D bandwidth should be better than PCIe bandwidth");
    }

    #endregion

    #region Concurrent Memory Operations

    [Theory]
    [InlineData(4, "Low concurrency")]
    [InlineData(16, "Medium concurrency")]
    [InlineData(64, "High concurrency")]
    [Trait("Category", TestCategories.Concurrency)]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateMemory_ConcurrentAllocations_MaintainsPerformance(int concurrencyLevel, string description)
    {
        // Arrange
        const int bufferSize = 1024 * 1024; // 1 MB per allocation
        const int allocationsPerThread = 20;
        var allocationResults = new ConcurrentQueue<AllocationResult>();
        
        using var perfContext = CreatePerformanceContext($"Concurrent Allocations - {description}");
        using var semaphore = new SemaphoreSlim(concurrencyLevel, concurrencyLevel);
        
        var overallSw = Stopwatch.StartNew();

        // Act - Concurrent allocations
        var tasks = Enumerable.Range(0, concurrencyLevel)
            .Select(async threadId =>
            {
                var threadResults = new List<AllocationResult>();
                
                for (var i = 0; i < allocationsPerThread; i++)
                {
                    await semaphore.WaitAsync(_cts.Token);
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var buffer = await _memoryManager.AllocateAsync(bufferSize, _cts.Token);
                        sw.Stop();
                        
                        var result = new AllocationResult
                        {
                            ThreadId = threadId,
                            AllocationId = i,
                            TimeMicroseconds = sw.Elapsed.TotalMicroseconds,
                            Buffer = buffer,
                            Timestamp = DateTime.UtcNow
                        };
                        
                        threadResults.Add(result);
                        allocationResults.Enqueue(result);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
                
                return threadResults;
            });

        var allResults = await Task.WhenAll(tasks);
        overallSw.Stop();
        
        perfContext.Checkpoint("All allocations completed");

        try
        {
            // Analyze concurrent performance
            var flatResults = allResults.SelectMany(r => r).ToList();
            var avgTime = flatResults.Average(r => r.TimeMicroseconds);
            var minTime = flatResults.Min(r => r.TimeMicroseconds);
            var maxTime = flatResults.Max(r => r.TimeMicroseconds);
            var stdDev = CalculateStandardDeviation(flatResults.Select(r => r.TimeMicroseconds));
            var totalAllocations = flatResults.Count;
            var overallThroughput = totalAllocations / overallSw.Elapsed.TotalSeconds;
            var totalMemoryMB = (totalAllocations * bufferSize) / (1024.0 * 1024.0);
            
            _output.WriteLine($"Concurrent Memory Allocation - {description}:");
            _output.WriteLine($"  Threads: {concurrencyLevel}");
            _output.WriteLine($"  Total allocations: {totalAllocations:N0}");
            _output.WriteLine($"  Total memory allocated: {totalMemoryMB:F1} MB");
            _output.WriteLine($"  Overall duration: {overallSw.Elapsed.TotalMilliseconds:F0}ms");
            _output.WriteLine($"  Overall throughput: {overallThroughput:F1} allocs/sec");
            _output.WriteLine($"  Per-allocation times:");
            _output.WriteLine($"    Average: {avgTime:F2}μs");
            _output.WriteLine($"    Min: {minTime:F2}μs");
            _output.WriteLine($"    Max: {maxTime:F2}μs");
            _output.WriteLine($"    Std Dev: {stdDev:F2}μs");
            _output.WriteLine($"    CV: {(stdDev / avgTime) * 100:F1}%");
            
            // Analyze thread fairness
            var threadAverages = flatResults
                .GroupBy(r => r.ThreadId)
                .Select(g => new { ThreadId = g.Key, AvgTime = g.Average(r => r.TimeMicroseconds) })
                .ToList();
                
            var threadAvgStdDev = CalculateStandardDeviation(threadAverages.Select(t => t.AvgTime));
            var threadFairness = threadAvgStdDev / threadAverages.Average(t => t.AvgTime);
            
            _output.WriteLine($"  Thread fairness (CV): {threadFairness * 100:F1}%");
            
            // Performance assertions
            overallThroughput.Should().BeGreaterThan(concurrencyLevel * 5, 
                "Should maintain reasonable throughput under concurrent load");
            threadFairness.Should().BeLessThan(0.5, "Thread allocation times should be reasonably fair");
            (stdDev / avgTime).Should().BeLessThan(1.0, "Allocation times should remain consistent under concurrency");
        }
        finally
        {
            // Cleanup all allocated buffers
            var cleanupTasks = allResults.SelectMany(r => r)
                .Select(result => result.Buffer.DisposeAsync().AsTask());
            await Task.WhenAll(cleanupTasks);
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Concurrency)]
    [Trait("Category", TestCategories.MemoryIntensive)]
    public async Task MemoryOperations_ConcurrentTransfers_MaintainsBandwidth()
    {
        // Arrange
        const int bufferSize = 4 * 1024 * 1024; // 4 MB
        const int concurrentTransfers = 8;
        const int transfersPerThread = 10;
        
        using var perfContext = CreatePerformanceContext("Concurrent Memory Transfers");
        var transferResults = new ConcurrentQueue<TransferResult>();
        
        // Pre-allocate buffers
        var buffers = new List<IMemoryBuffer>();
        var hostBuffers = new List<float[]>();
        
        for (var i = 0; i < concurrentTransfers; i++)
        {
            buffers.Add(await _memoryManager.AllocateAsync(bufferSize, _cts.Token));
            hostBuffers.Add(GenerateRandomFloats(bufferSize / sizeof(float)));
        }
        
        perfContext.Checkpoint("Buffers pre-allocated");

        try
        {
            var overallSw = Stopwatch.StartNew();
            
            // Act - Concurrent transfers
            var transferTasks = Enumerable.Range(0, concurrentTransfers)
                .Select(async threadId =>
                {
                    var deviceBuffer = buffers[threadId];
                    var hostBuffer = hostBuffers[threadId];
                    
                    for (var i = 0; i < transfersPerThread; i++)
                    {
                        // H2D Transfer
                        var h2dSw = Stopwatch.StartNew();
                        await deviceBuffer.CopyFromAsync(hostBuffer, _cts.Token);
                        await _memoryManager.SynchronizeAsync(_cts.Token);
                        h2dSw.Stop();
                        
                        transferResults.Enqueue(new TransferResult
                        {
                            ThreadId = threadId,
                            TransferType = "H2D",
                            TimeMicroseconds = h2dSw.Elapsed.TotalMicroseconds,
                            DataSizeBytes = bufferSize
                        });
                        
                        // D2H Transfer
                        var d2hSw = Stopwatch.StartNew();
                        await deviceBuffer.CopyToAsync(hostBuffer, _cts.Token);
                        await _memoryManager.SynchronizeAsync(_cts.Token);
                        d2hSw.Stop();
                        
                        transferResults.Enqueue(new TransferResult
                        {
                            ThreadId = threadId,
                            TransferType = "D2H",
                            TimeMicroseconds = d2hSw.Elapsed.TotalMicroseconds,
                            DataSizeBytes = bufferSize
                        });
                    }
                });

            await Task.WhenAll(transferTasks);
            overallSw.Stop();
            
            perfContext.Checkpoint("All transfers completed");

            // Analyze concurrent transfer performance
            var results = transferResults.ToList();
            var h2dResults = results.Where(r => r.TransferType == "H2D").ToList();
            var d2hResults = results.Where(r => r.TransferType == "D2H").ToList();
            
            var h2dAvgBandwidth = CalculateAverageBandwidth(h2dResults);
            var d2hAvgBandwidth = CalculateAverageBandwidth(d2hResults);
            
            var totalDataTransferred = results.Sum(r => r.DataSizeBytes * 2L); // Each operation transfers data twice
            var overallBandwidth = (totalDataTransferred / (1024.0 * 1024.0 * 1024.0)) / overallSw.Elapsed.TotalSeconds;
            
            _output.WriteLine($"Concurrent Memory Transfer Performance:");
            _output.WriteLine($"  Concurrent transfers: {concurrentTransfers}");
            _output.WriteLine($"  Transfers per thread: {transfersPerThread}");
            _output.WriteLine($"  Total duration: {overallSw.Elapsed.TotalMilliseconds:F0}ms");
            _output.WriteLine($"  Total data transferred: {totalDataTransferred / (1024.0 * 1024.0 * 1024.0):F2} GB");
            _output.WriteLine($"  Overall bandwidth: {overallBandwidth:F2} GB/s");
            _output.WriteLine($"  H2D average bandwidth: {h2dAvgBandwidth:F2} GB/s");
            _output.WriteLine($"  D2H average bandwidth: {d2hAvgBandwidth:F2} GB/s");
            
            // Performance assertions
            overallBandwidth.Should().BeGreaterThan(1.0, "Should achieve reasonable overall bandwidth");
            h2dAvgBandwidth.Should().BeGreaterThan(0.5, "H2D transfers should maintain reasonable bandwidth");
            d2hAvgBandwidth.Should().BeGreaterThan(0.5, "D2H transfers should maintain reasonable bandwidth");
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

    #endregion

    #region Helper Methods and Data

    public static IEnumerable<object[]> GetAllocationTestSizes()
    {
        yield return new object[] { 1024, "1 KB" };
        yield return new object[] { 64 * 1024, "64 KB" };
        yield return new object[] { 1024 * 1024, "1 MB" };
        yield return new object[] { 16 * 1024 * 1024, "16 MB" };
        yield return new object[] { 64 * 1024 * 1024, "64 MB" };
    }
    
    public static IEnumerable<object[]> GetTransferTestSizes()
    {
        yield return new object[] { 64 * 1024, "64 KB" };
        yield return new object[] { 1024 * 1024, "1 MB" };
        yield return new object[] { 4 * 1024 * 1024, "4 MB" };
        yield return new object[] { 16 * 1024 * 1024, "16 MB" };
        yield return new object[] { 64 * 1024 * 1024, "64 MB" };
    }

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        var mean = valuesList.Average();
        var sumSquaredDiffs = valuesList.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / valuesList.Count);
    }

    private static double GetTheoreticalBandwidth() => 16.0; // GB/s - PCIe 4.0 x16 theoretical
    private static double GetTheoreticalInternalBandwidth() => 900.0; // GB/s - High-end GPU memory bandwidth

    private double CalculateAverageBandwidth(List<TransferResult> results)
    {
        if (!results.Any()) return 0.0;
        
        return results.Average(r => CalculateBandwidth(r.DataSizeBytes, r.TimeMicroseconds / 1000.0));
    }

    #endregion
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
            _memoryManager?.DisposeAsync().AsTask().Wait(1000);
        }
        base.Dispose(disposing);
    }
}

#region Support Classes

/// <summary>
/// Result of a memory allocation operation for performance analysis
/// </summary>
internal class AllocationResult
{
    public int ThreadId { get; set; }
    public int AllocationId { get; set; }
    public double TimeMicroseconds { get; set; }
    public IMemoryBuffer Buffer { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Result of a memory transfer operation for performance analysis
/// </summary>
internal class TransferResult
{
    public int ThreadId { get; set; }
    public string TransferType { get; set; } = string.Empty; // "H2D", "D2H", "D2D"
    public double TimeMicroseconds { get; set; }
    public long DataSizeBytes { get; set; }
}

// Removed duplicate MockMemoryManager - using the one from KernelPerformanceTests.cs

/// <summary>
/// Interface for memory buffers used in performance testing
/// </summary>
internal interface IMemoryBuffer : IAsyncDisposable
{
    long Size { get; }
    ValueTask CopyFromAsync<T>(T[] data, CancellationToken cancellationToken = default) where T : unmanaged;
    ValueTask CopyToAsync<T>(T[] data, CancellationToken cancellationToken = default) where T : unmanaged;
    ValueTask CopyToAsync(IMemoryBuffer destination, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mock memory buffer for performance testing
/// </summary>
internal class MockMemoryBuffer : IMemoryBuffer
{
    private readonly MockMemoryManager _manager;
    private readonly Random _random = new(42);
    
    public long Size { get; }
    
    public MockMemoryBuffer(long size, MockMemoryManager manager)
    {
        Size = size;
        _manager = manager;
    }

    public async ValueTask CopyFromAsync<T>(T[] data, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Simulate transfer time based on size
        var transferTime = Math.Max(1, (int)(Size / (100 * 1024 * 1024))); // 1ms per 100MB
        await Task.Delay(transferTime, cancellationToken);
    }

    public async ValueTask CopyToAsync<T>(T[] data, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Simulate transfer time
        var transferTime = Math.Max(1, (int)(Size / (80 * 1024 * 1024))); // Slightly slower for D2H
        await Task.Delay(transferTime, cancellationToken);
    }
    
    public async ValueTask CopyToAsync(IMemoryBuffer destination, CancellationToken cancellationToken = default)
    {
        // Simulate D2D transfer (faster)
        var transferTime = Math.Max(1, (int)(Size / (500 * 1024 * 1024))); // 1ms per 500MB
        await Task.Delay(transferTime, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _manager.ReturnToPool(this);
        return ValueTask.CompletedTask;
    }
}

#endregion
