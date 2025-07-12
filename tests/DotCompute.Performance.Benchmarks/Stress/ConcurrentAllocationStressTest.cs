using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Performance.Benchmarks.Stress;

/// <summary>
/// Concurrent allocation stress test for multi-threaded scenarios
/// </summary>
public class ConcurrentAllocationStressTest
{
    private readonly ILogger<ConcurrentAllocationStressTest> _logger;
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private readonly ConcurrentBag<AllocationMetrics> _metrics = new();
    private readonly ConcurrentQueue<byte[]> _allocations = new();
    
    private long _totalAllocations;
    private long _totalFailures;
    private long _totalMemoryAllocated;
    private volatile bool _stopRequested;
    
    public ConcurrentAllocationStressTest(ILogger<ConcurrentAllocationStressTest> logger)
    {
        _logger = logger;
    }

    public async Task RunStressTest(int threadCount, TimeSpan duration)
    {
        _logger.LogInformation("Starting concurrent allocation stress test with {ThreadCount} threads for {Duration}", 
            threadCount, duration);

        var cancellationTokenSource = new CancellationTokenSource(duration);
        var tasks = new List<Task>();
        
        // Start allocation threads
        for (int i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() => AllocationWorker(threadId, cancellationTokenSource.Token)));
        }
        
        // Start monitoring thread
        tasks.Add(Task.Run(() => MonitoringWorker(cancellationTokenSource.Token)));
        
        // Start cleanup thread
        tasks.Add(Task.Run(() => CleanupWorker(cancellationTokenSource.Token)));
        
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when duration expires
        }
        
        _stopRequested = true;
        await GenerateStressTestReport(threadCount, duration);
    }

    private async Task AllocationWorker(int threadId, CancellationToken cancellationToken)
    {
        var random = new Random(threadId);
        var threadMetrics = new AllocationMetrics { ThreadId = threadId };
        var stopwatch = Stopwatch.StartNew();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var allocationSize = random.Next(1024, 1024 * 1024); // 1KB to 1MB
                var allocationType = random.Next(0, 5);
                
                var allocationStart = stopwatch.ElapsedTicks;
                
                switch (allocationType)
                {
                    case 0: // Regular array allocation
                        var array = new byte[allocationSize];
                        _allocations.Enqueue(array);
                        threadMetrics.ManagedAllocations++;
                        break;
                        
                    case 1: // Array pool allocation
                        var pooledArray = _arrayPool.Rent(allocationSize);
                        _allocations.Enqueue(pooledArray);
                        threadMetrics.PooledAllocations++;
                        break;
                        
                    case 2: // Native memory allocation
                        unsafe
                        {
                            var ptr = System.Runtime.InteropServices.NativeMemory.Alloc((nuint)allocationSize);
                            var unmanagedArray = new UnmanagedMemoryAccessor(ptr, allocationSize);
                            threadMetrics.NativeAllocations++;
                        }
                        break;
                        
                    case 3: // Large object allocation (>85KB)
                        var largeSize = Math.Max(allocationSize, 100 * 1024);
                        var largeArray = new byte[largeSize];
                        _allocations.Enqueue(largeArray);
                        threadMetrics.LargeObjectAllocations++;
                        break;
                        
                    case 4: // Pinned memory allocation
                        var pinnedArray = GC.AllocateUninitializedArray<byte>(allocationSize, pinned: true);
                        _allocations.Enqueue(pinnedArray);
                        threadMetrics.PinnedAllocations++;
                        break;
                }
                
                var allocationEnd = stopwatch.ElapsedTicks;
                var allocationTime = allocationEnd - allocationStart;
                
                threadMetrics.TotalAllocations++;
                threadMetrics.TotalMemoryAllocated += allocationSize;
                threadMetrics.TotalAllocationTime += allocationTime;
                
                if (allocationTime > threadMetrics.MaxAllocationTime)
                {
                    threadMetrics.MaxAllocationTime = allocationTime;
                }
                
                if (allocationTime < threadMetrics.MinAllocationTime || threadMetrics.MinAllocationTime == 0)
                {
                    threadMetrics.MinAllocationTime = allocationTime;
                }
                
                Interlocked.Increment(ref _totalAllocations);
                Interlocked.Add(ref _totalMemoryAllocated, allocationSize);
                
                // Simulate some work with the allocated memory
                await Task.Delay(random.Next(1, 10), cancellationToken);
            }
            catch (OutOfMemoryException)
            {
                threadMetrics.OutOfMemoryErrors++;
                Interlocked.Increment(ref _totalFailures);
                
                // Force garbage collection and wait a bit
                GC.Collect();
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                threadMetrics.OtherErrors++;
                Interlocked.Increment(ref _totalFailures);
                _logger.LogWarning("Allocation error in thread {ThreadId}: {Error}", threadId, ex.Message);
                await Task.Delay(100, cancellationToken);
            }
        }
        
        threadMetrics.Duration = stopwatch.Elapsed;
        _metrics.Add(threadMetrics);
    }

    private async Task MonitoringWorker(CancellationToken cancellationToken)
    {
        var lastReport = DateTime.UtcNow;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now - lastReport >= TimeSpan.FromSeconds(30))
                {
                    var process = Process.GetCurrentProcess();
                    var workingSet = process.WorkingSet64;
                    var gcMemory = GC.GetTotalMemory(false);
                    
                    _logger.LogInformation(
                        "Stress Test Status - " +
                        "Allocations: {Allocations:N0}, " +
                        "Failures: {Failures:N0}, " +
                        "Memory Allocated: {MemoryMB:N1} MB, " +
                        "Working Set: {WorkingSetMB:N1} MB, " +
                        "GC Memory: {GcMemoryMB:N1} MB, " +
                        "Queue Size: {QueueSize:N0}",
                        _totalAllocations,
                        _totalFailures,
                        _totalMemoryAllocated / (1024.0 * 1024.0),
                        workingSet / (1024.0 * 1024.0),
                        gcMemory / (1024.0 * 1024.0),
                        _allocations.Count);
                    
                    lastReport = now;
                }
                
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring worker");
            }
        }
    }

    private async Task CleanupWorker(CancellationToken cancellationToken)
    {
        var random = new Random();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Randomly deallocate some memory to simulate realistic usage
                var deallocateCount = Math.Min(random.Next(1, 50), _allocations.Count);
                
                for (int i = 0; i < deallocateCount; i++)
                {
                    if (_allocations.TryDequeue(out var allocation))
                    {
                        // For array pool allocations, we should return them
                        // This is a simplified example - in practice, you'd need to track allocation types
                        if (allocation.Length > 0)
                        {
                            try
                            {
                                _arrayPool.Return(allocation);
                            }
                            catch
                            {
                                // Not from array pool, let GC handle it
                            }
                        }
                    }
                }
                
                await Task.Delay(TimeSpan.FromSeconds(random.Next(5, 15)), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup worker");
            }
        }
    }

    private async Task GenerateStressTestReport(int threadCount, TimeSpan duration)
    {
        var report = new
        {
            TestConfiguration = new
            {
                ThreadCount = threadCount,
                Duration = duration,
                TestCompleted = DateTime.UtcNow
            },
            OverallResults = new
            {
                TotalAllocations = _totalAllocations,
                TotalFailures = _totalFailures,
                TotalMemoryAllocatedMB = _totalMemoryAllocated / (1024.0 * 1024.0),
                AllocationRate = _totalAllocations / duration.TotalSeconds,
                FailureRate = _totalFailures / (double)_totalAllocations * 100,
                AverageMemoryPerAllocation = _totalMemoryAllocated / (double)_totalAllocations
            },
            ThreadMetrics = _metrics.Select(m => new
            {
                m.ThreadId,
                m.Duration,
                m.TotalAllocations,
                m.ManagedAllocations,
                m.PooledAllocations,
                m.NativeAllocations,
                m.LargeObjectAllocations,
                m.PinnedAllocations,
                m.OutOfMemoryErrors,
                m.OtherErrors,
                TotalMemoryAllocatedMB = m.TotalMemoryAllocated / (1024.0 * 1024.0),
                AverageAllocationTime = m.TotalAllocationTime / (double)m.TotalAllocations,
                MaxAllocationTime = m.MaxAllocationTime,
                MinAllocationTime = m.MinAllocationTime,
                AllocationRate = m.TotalAllocations / m.Duration.TotalSeconds
            }).ToArray(),
            SystemMetrics = new
            {
                FinalWorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0),
                FinalGCMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                ProcessorCount = Environment.ProcessorCount,
                RemainingAllocations = _allocations.Count
            }
        };
        
        _logger.LogInformation("Concurrent Allocation Stress Test Report: {@Report}", report);
        
        // Save detailed report to file
        var reportPath = Path.Combine("Analysis", $"concurrent-allocation-stress-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        
        await File.WriteAllTextAsync(reportPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
        
        _logger.LogInformation("Detailed stress test report saved to: {ReportPath}", reportPath);
    }

    private class AllocationMetrics
    {
        public int ThreadId { get; set; }
        public TimeSpan Duration { get; set; }
        public long TotalAllocations { get; set; }
        public long ManagedAllocations { get; set; }
        public long PooledAllocations { get; set; }
        public long NativeAllocations { get; set; }
        public long LargeObjectAllocations { get; set; }
        public long PinnedAllocations { get; set; }
        public long OutOfMemoryErrors { get; set; }
        public long OtherErrors { get; set; }
        public long TotalMemoryAllocated { get; set; }
        public long TotalAllocationTime { get; set; }
        public long MaxAllocationTime { get; set; }
        public long MinAllocationTime { get; set; }
    }

    private unsafe class UnmanagedMemoryAccessor : IDisposable
    {
        private readonly void* _pointer;
        private readonly int _size;
        
        public UnmanagedMemoryAccessor(void* pointer, int size)
        {
            _pointer = pointer;
            _size = size;
        }
        
        public void Dispose()
        {
            System.Runtime.InteropServices.NativeMemory.Free(_pointer);
        }
    }
}