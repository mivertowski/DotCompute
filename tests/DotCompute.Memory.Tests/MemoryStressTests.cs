// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using DotCompute.Memory;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Long-running stress tests for the memory system.
/// These tests can be run for extended periods to validate stability.
/// </summary>
public class MemoryStressTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CpuMemoryManager _memoryManager;
    private readonly UnifiedMemoryManager _unifiedManager;
    
    public MemoryStressTests(ITestOutputHelper output)
    {
        _output = output;
        _memoryManager = new CpuMemoryManager();
        _unifiedManager = new UnifiedMemoryManager(_memoryManager);
    }
    
    [Fact(Skip = "Long-running stress test - enable manually")]
    public async Task StressTest_24Hour_MemoryStability()
    {
        await RunStressTest(TimeSpan.FromHours(24));
    }
    
    [Fact]
    public async Task StressTest_5Minute_MemoryStability()
    {
        await RunStressTest(TimeSpan.FromMinutes(5));
    }
    
    private async Task RunStressTest(TimeSpan duration)
    {
        var cts = new CancellationTokenSource(duration);
        var startTime = DateTime.UtcNow;
        var stats = new StressTestStats();
        
        _output.WriteLine($"Starting stress test for {duration.TotalMinutes} minutes...");
        
        // Start multiple worker tasks
        var tasks = new Task[]
        {
            // Buffer allocation/deallocation workers
            RunBufferWorker(1, BufferSize.Small, cts.Token, stats),
            RunBufferWorker(2, BufferSize.Medium, cts.Token, stats),
            RunBufferWorker(3, BufferSize.Large, cts.Token, stats),
            
            // Pool stress workers
            RunPoolWorker(4, cts.Token, stats),
            RunPoolWorker(5, cts.Token, stats),
            
            // Memory transfer workers
            RunTransferWorker(6, cts.Token, stats),
            RunTransferWorker(7, cts.Token, stats),
            
            // Concurrent access worker
            RunConcurrentAccessWorker(8, cts.Token, stats),
            
            // Memory pressure worker
            RunMemoryPressureWorker(9, cts.Token, stats),
            
            // Monitoring worker
            RunMonitoringWorker(cts.Token, stats)
        };
        
        await Task.WhenAll(tasks);
        
        var endTime = DateTime.UtcNow;
        var actualDuration = endTime - startTime;
        
        // Print final statistics
        _output.WriteLine($"\nStress test completed after {actualDuration.TotalMinutes:F2} minutes");
        _output.WriteLine($"Total operations: {stats.TotalOperations}");
        _output.WriteLine($"Operations per second: {stats.TotalOperations / actualDuration.TotalSeconds:F2}");
        _output.WriteLine($"Total errors: {stats.TotalErrors}");
        _output.WriteLine($"Peak memory: {stats.PeakMemoryMB:F2} MB");
        _output.WriteLine($"Average memory: {stats.AverageMemoryMB:F2} MB");
        
        // Validate no memory leaks
        await _unifiedManager.CompactAsync();
        var finalStats = _unifiedManager.GetStats();
        
        Assert.Equal(0, stats.TotalErrors);
        Assert.Equal(0, finalStats.ActiveUnifiedBuffers);
        Assert.True(finalStats.TotalAllocatedBytes < 10 * 1024 * 1024, // Less than 10MB retained
            $"Memory leak detected: {finalStats.TotalAllocatedBytes} bytes still allocated");
    }
    
    private async Task RunBufferWorker(int workerId, BufferSize size, CancellationToken ct, StressTestStats stats)
    {
        var random = new Random(workerId);
        var activeBuffers = new ConcurrentBag<UnifiedBuffer<byte>>();
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Allocate new buffer
                var bufferSize = GetBufferSize(size, random);
                var buffer = await _unifiedManager.CreateUnifiedBufferAsync<byte>(bufferSize);
                
                // Perform operations
                await PerformBufferOperations(buffer, random);
                
                activeBuffers.Add(buffer);
                Interlocked.Increment(ref stats.TotalOperations);
                
                // Randomly dispose some buffers
                if (activeBuffers.Count > 10 && random.Next(100) < 30)
                {
                    if (activeBuffers.TryTake(out var oldBuffer))
                    {
                        oldBuffer.Dispose();
                    }
                }
                
                // Prevent runaway allocation
                if (activeBuffers.Count > 100)
                {
                    while (activeBuffers.TryTake(out var oldBuffer))
                    {
                        oldBuffer.Dispose();
                        if (activeBuffers.Count < 50) break;
                    }
                }
                
                await Task.Delay(random.Next(1, 10), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref stats.TotalErrors);
                _output.WriteLine($"Worker {workerId} error: {ex.Message}");
            }
        }
        
        // Cleanup
        while (activeBuffers.TryTake(out var buffer))
        {
            buffer.Dispose();
        }
    }
    
    private async Task RunPoolWorker(int workerId, CancellationToken ct, StressTestStats stats)
    {
        var random = new Random(workerId * 100);
        var pools = new[]
        {
            _unifiedManager.GetPool<byte>(),
            _unifiedManager.GetPool<int>(),
            _unifiedManager.GetPool<float>(),
            _unifiedManager.GetPool<double>()
        };
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var poolIndex = random.Next(pools.Length);
                
                if (poolIndex == 0)
                {
                    await TestPool<byte>(pools[0], random);
                }
                else if (poolIndex == 1)
                {
                    await TestPool<int>((MemoryPool<int>)pools[1], random);
                }
                else if (poolIndex == 2)
                {
                    await TestPool<float>((MemoryPool<float>)pools[2], random);
                }
                else
                {
                    await TestPool<double>((MemoryPool<double>)pools[3], random);
                }
                
                Interlocked.Increment(ref stats.TotalOperations);
                await Task.Delay(random.Next(1, 5), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref stats.TotalErrors);
                _output.WriteLine($"Pool worker {workerId} error: {ex.Message}");
            }
        }
    }
    
    private async Task TestPool<T>(MemoryPool<T> pool, Random random) where T : unmanaged
    {
        var size = random.Next(64, 2048);
        var buffer = pool.Rent(size);
        
        try
        {
            // Write to buffer
            var span = buffer.AsSpan();
            for (int i = 0; i < Math.Min(10, span.Length); i++)
            {
                span[i] = default(T);
            }
            
            // Simulate work
            await Task.Delay(random.Next(1, 10));
        }
        finally
        {
            buffer.Dispose();
        }
    }
    
    private async Task RunTransferWorker(int workerId, CancellationToken ct, StressTestStats stats)
    {
        var random = new Random(workerId * 200);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var size = random.Next(1024, 65536);
                var buffer = await _unifiedManager.CreateUnifiedBufferAsync<int>(size);
                
                try
                {
                    // Host to device transfer
                    var hostSpan = buffer.AsSpan();
                    for (int i = 0; i < Math.Min(100, hostSpan.Length); i++)
                    {
                        hostSpan[i] = random.Next();
                    }
                    
                    buffer.EnsureOnDevice();
                    
                    // Device to host transfer
                    buffer.MarkDeviceDirty();
                    buffer.EnsureOnHost();
                    
                    // Verify data
                    var verifySpan = buffer.AsReadOnlySpan();
                    for (int i = 0; i < Math.Min(100, verifySpan.Length); i++)
                    {
                        if (verifySpan[i] != hostSpan[i])
                        {
                            throw new InvalidOperationException("Data corruption detected");
                        }
                    }
                    
                    Interlocked.Increment(ref stats.TotalOperations);
                }
                finally
                {
                    buffer.Dispose();
                }
                
                await Task.Delay(random.Next(5, 20), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref stats.TotalErrors);
                _output.WriteLine($"Transfer worker {workerId} error: {ex.Message}");
            }
        }
    }
    
    private async Task RunConcurrentAccessWorker(int workerId, CancellationToken ct, StressTestStats stats)
    {
        var random = new Random(workerId * 300);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var buffer = await _unifiedManager.CreateUnifiedBufferAsync<long>(4096);
                var tasks = new Task[4];
                
                // Multiple threads accessing the same buffer
                for (int i = 0; i < tasks.Length; i++)
                {
                    var threadId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        var span = buffer.AsSpan();
                        for (int j = threadId; j < span.Length; j += tasks.Length)
                        {
                            span[j] = Thread.CurrentThread.ManagedThreadId;
                        }
                    });
                }
                
                await Task.WhenAll(tasks);
                buffer.Dispose();
                
                Interlocked.Increment(ref stats.TotalOperations);
                await Task.Delay(random.Next(10, 50), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref stats.TotalErrors);
                _output.WriteLine($"Concurrent worker {workerId} error: {ex.Message}");
            }
        }
    }
    
    private async Task RunMemoryPressureWorker(int workerId, CancellationToken ct, StressTestStats stats)
    {
        var random = new Random(workerId * 400);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pressure = random.NextDouble();
                await _unifiedManager.HandleMemoryPressureAsync(pressure);
                
                if (pressure > 0.8)
                {
                    await _unifiedManager.CompactAsync();
                }
                
                Interlocked.Increment(ref stats.TotalOperations);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref stats.TotalErrors);
                _output.WriteLine($"Pressure worker {workerId} error: {ex.Message}");
            }
        }
    }
    
    private async Task RunMonitoringWorker(CancellationToken ct, StressTestStats stats)
    {
        var samples = new List<long>();
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var memStats = _unifiedManager.GetStats();
                var currentMemoryMB = memStats.TotalAllocatedBytes / (1024.0 * 1024.0);
                
                samples.Add(memStats.TotalAllocatedBytes);
                stats.PeakMemoryMB = Math.Max(stats.PeakMemoryMB, currentMemoryMB);
                
                if (samples.Count > 100)
                {
                    samples.RemoveAt(0);
                }
                
                if (samples.Count > 0)
                {
                    var avg = samples.Average(x => x);
                    stats.AverageMemoryMB = avg / (1024.0 * 1024.0);
                }
                
                if (DateTime.UtcNow.Second % 30 == 0)
                {
                    _output.WriteLine($"[Monitor] Memory: {currentMemoryMB:F2} MB, " +
                                    $"Buffers: {memStats.ActiveUnifiedBuffers}, " +
                                    $"Operations: {stats.TotalOperations}, " +
                                    $"Errors: {stats.TotalErrors}");
                }
                
                await Task.Delay(1000, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _output.WriteLine($"Monitor error: {ex.Message}");
            }
        }
    }
    
    private async Task PerformBufferOperations(UnifiedBuffer<byte> buffer, Random random)
    {
        var operation = random.Next(4);
        
        switch (operation)
        {
            case 0: // Write to host
                var hostSpan = buffer.AsSpan();
                random.NextBytes(hostSpan.Slice(0, Math.Min(100, hostSpan.Length)));
                break;
                
            case 1: // Transfer to device
                buffer.EnsureOnDevice();
                break;
                
            case 2: // Synchronize
                buffer.Synchronize();
                break;
                
            case 3: // Async operations
                var data = new byte[Math.Min(100, buffer.Length)];
                random.NextBytes(data);
                await buffer.CopyFromAsync(data);
                break;
        }
    }
    
    private int GetBufferSize(BufferSize size, Random random)
    {
        return size switch
        {
            BufferSize.Small => random.Next(64, 1024),
            BufferSize.Medium => random.Next(1024, 65536),
            BufferSize.Large => random.Next(65536, 1048576),
            _ => 1024
        };
    }
    
    public void Dispose()
    {
        _unifiedManager?.Dispose();
        _memoryManager?.Dispose();
    }
    
    private enum BufferSize
    {
        Small,
        Medium,
        Large
    }
    
    private class StressTestStats
    {
        public long TotalOperations;
        public long TotalErrors;
        public double PeakMemoryMB;
        public double AverageMemoryMB;
    }
}