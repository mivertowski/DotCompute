using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using Microsoft.Extensions.Logging;

namespace DotCompute.Performance.Benchmarks.Stress;

/// <summary>
/// 24-hour memory leak stress test
/// </summary>
public class MemoryLeakStressTest
{
    private readonly ILogger<MemoryLeakStressTest> _logger;
    private readonly ConcurrentQueue<WeakReference> _allocations = new();
    private readonly Timer _gcTimer;
    private readonly Timer _reportTimer;
    private readonly Stopwatch _testDuration = new();
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _peakMemoryUsage;
    private long _currentMemoryUsage;
    
    public MemoryLeakStressTest(ILogger<MemoryLeakStressTest> logger)
    {
        _logger = logger;
        _gcTimer = new Timer(ForceGarbageCollection, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        _reportTimer = new Timer(ReportMemoryUsage, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task RunStressTest(TimeSpan duration)
    {
        _logger.LogInformation("Starting 24-hour memory leak stress test for {Duration}", duration);
        _testDuration.Start();
        
        var cancellationTokenSource = new CancellationTokenSource(duration);
        var tasks = new List<Task>();
        
        // Start multiple allocation patterns
        tasks.Add(Task.Run(() => ContinuousAllocationPattern(cancellationTokenSource.Token)));
        tasks.Add(Task.Run(() => BurstAllocationPattern(cancellationTokenSource.Token)));
        tasks.Add(Task.Run(() => LargeObjectAllocationPattern(cancellationTokenSource.Token)));
        tasks.Add(Task.Run(() => ArrayPoolPattern(cancellationTokenSource.Token)));
        tasks.Add(Task.Run(() => NativeMemoryPattern(cancellationTokenSource.Token)));
        tasks.Add(Task.Run(() => WeakReferenceCleanup(cancellationTokenSource.Token)));
        
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when duration expires
        }
        
        _testDuration.Stop();
        await GenerateFinalReport();
    }

    private async Task ContinuousAllocationPattern(CancellationToken cancellationToken)
    {
        var random = new Random();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var size = random.Next(1024, 16384);
                var buffer = new byte[size];
                
                // Simulate some work
                for (int i = 0; i < Math.Min(size, 1000); i++)
                {
                    buffer[i] = (byte)random.Next(256);
                }
                
                _allocations.Enqueue(new WeakReference(buffer));
                Interlocked.Increment(ref _totalAllocations);
                
                await Task.Delay(10, cancellationToken);
            }
            catch (OutOfMemoryException ex)
            {
                _logger.LogWarning("Out of memory in continuous allocation: {Message}", ex.Message);
                GC.Collect();
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task BurstAllocationPattern(CancellationToken cancellationToken)
    {
        var random = new Random();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var burstSize = random.Next(100, 1000);
                var allocations = new List<byte[]>();
                
                // Allocate burst
                for (int i = 0; i < burstSize; i++)
                {
                    var size = random.Next(4096, 65536);
                    var buffer = new byte[size];
                    allocations.Add(buffer);
                    
                    _allocations.Enqueue(new WeakReference(buffer));
                    Interlocked.Increment(ref _totalAllocations);
                }
                
                // Hold for a while
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                
                // Release burst
                allocations.Clear();
                
                // Wait before next burst
                await Task.Delay(TimeSpan.FromSeconds(random.Next(10, 60)), cancellationToken);
            }
            catch (OutOfMemoryException ex)
            {
                _logger.LogWarning("Out of memory in burst allocation: {Message}", ex.Message);
                GC.Collect();
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task LargeObjectAllocationPattern(CancellationToken cancellationToken)
    {
        var random = new Random();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Large Object Heap allocations (>85KB)
                var size = random.Next(100 * 1024, 1024 * 1024); // 100KB to 1MB
                var buffer = new byte[size];
                
                // Fill with pattern
                for (int i = 0; i < size; i += 4096)
                {
                    buffer[i] = (byte)(i % 256);
                }
                
                _allocations.Enqueue(new WeakReference(buffer));
                Interlocked.Increment(ref _totalAllocations);
                
                await Task.Delay(TimeSpan.FromSeconds(random.Next(30, 120)), cancellationToken);
            }
            catch (OutOfMemoryException ex)
            {
                _logger.LogWarning("Out of memory in large object allocation: {Message}", ex.Message);
                GC.Collect();
                await Task.Delay(10000, cancellationToken);
            }
        }
    }

    private async Task ArrayPoolPattern(CancellationToken cancellationToken)
    {
        var random = new Random();
        var rentedArrays = new ConcurrentQueue<byte[]>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Rent arrays
                for (int i = 0; i < 10; i++)
                {
                    var size = random.Next(1024, 32768);
                    var buffer = _arrayPool.Rent(size);
                    rentedArrays.Enqueue(buffer);
                    
                    // Simulate work
                    for (int j = 0; j < Math.Min(size, 100); j++)
                    {
                        buffer[j] = (byte)random.Next(256);
                    }
                }
                
                await Task.Delay(100, cancellationToken);
                
                // Return some arrays
                for (int i = 0; i < 5; i++)
                {
                    if (rentedArrays.TryDequeue(out var buffer))
                    {
                        _arrayPool.Return(buffer);
                    }
                }
                
                await Task.Delay(50, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error in array pool pattern: {Message}", ex.Message);
                await Task.Delay(1000, cancellationToken);
            }
        }
        
        // Return remaining arrays
        while (rentedArrays.TryDequeue(out var buffer))
        {
            _arrayPool.Return(buffer);
        }
    }

    private async Task NativeMemoryPattern(CancellationToken cancellationToken)
    {
        var random = new Random();
        var nativeAllocations = new ConcurrentQueue<IntPtr>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                unsafe
                {
                    var size = random.Next(1024, 65536);
                    var ptr = System.Runtime.InteropServices.NativeMemory.Alloc((nuint)size);
                    nativeAllocations.Enqueue((IntPtr)ptr);
                    
                    // Write pattern
                    var bytePtr = (byte*)ptr;
                    for (int i = 0; i < size; i += 256)
                    {
                        bytePtr[i] = (byte)(i % 256);
                    }
                    
                    Interlocked.Increment(ref _totalAllocations);
                }
                
                await Task.Delay(100, cancellationToken);
                
                // Free some allocations
                if (nativeAllocations.Count > 100)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        if (nativeAllocations.TryDequeue(out var ptr))
                        {
                            unsafe
                            {
                                System.Runtime.InteropServices.NativeMemory.Free(ptr.ToPointer());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error in native memory pattern: {Message}", ex.Message);
                await Task.Delay(1000, cancellationToken);
            }
        }
        
        // Free remaining allocations
        while (nativeAllocations.TryDequeue(out var ptr))
        {
            unsafe
            {
                System.Runtime.InteropServices.NativeMemory.Free(ptr.ToPointer());
            }
        }
    }

    private async Task WeakReferenceCleanup(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var aliveCount = 0;
            var totalCount = 0;
            
            // Count alive and dead weak references
            var tempQueue = new ConcurrentQueue<WeakReference>();
            
            while (_allocations.TryDequeue(out var weakRef))
            {
                totalCount++;
                if (weakRef.IsAlive)
                {
                    aliveCount++;
                    tempQueue.Enqueue(weakRef);
                }
                else
                {
                    Interlocked.Increment(ref _totalDeallocations);
                }
            }
            
            // Put back alive references
            while (tempQueue.TryDequeue(out var weakRef))
            {
                _allocations.Enqueue(weakRef);
            }
            
            _logger.LogDebug("Weak references: {TotalCount} total, {AliveCount} alive", totalCount, aliveCount);
            
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private void ForceGarbageCollection(object? state)
    {
        try
        {
            var beforeGC = GC.GetTotalMemory(false);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var afterGC = GC.GetTotalMemory(false);
            var collected = beforeGC - afterGC;
            
            _logger.LogInformation("Forced GC: Collected {CollectedBytes:N0} bytes", collected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced garbage collection");
        }
    }

    private void ReportMemoryUsage(object? state)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var gcMemory = GC.GetTotalMemory(false);
            
            _currentMemoryUsage = workingSet;
            if (workingSet > _peakMemoryUsage)
            {
                _peakMemoryUsage = workingSet;
            }
            
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);
            
            _logger.LogInformation(
                "Memory Usage - Working Set: {WorkingSetMB:N1} MB, " +
                "GC Memory: {GcMemoryMB:N1} MB, " +
                "Peak: {PeakMB:N1} MB, " +
                "GC Collections: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}, " +
                "Allocations: {Allocations:N0}, " +
                "Deallocations: {Deallocations:N0}",
                workingSet / (1024.0 * 1024.0),
                gcMemory / (1024.0 * 1024.0),
                _peakMemoryUsage / (1024.0 * 1024.0),
                gen0, gen1, gen2,
                _totalAllocations,
                _totalDeallocations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory usage reporting");
        }
    }

    private async Task GenerateFinalReport()
    {
        var report = new
        {
            TestDuration = _testDuration.Elapsed,
            TotalAllocations = _totalAllocations,
            TotalDeallocations = _totalDeallocations,
            PeakMemoryUsageMB = _peakMemoryUsage / (1024.0 * 1024.0),
            CurrentMemoryUsageMB = _currentMemoryUsage / (1024.0 * 1024.0),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            FinalGCMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
            LeakIndicators = new
            {
                UnreleasedAllocations = _totalAllocations - _totalDeallocations,
                WeakReferencesStillAlive = _allocations.Count(wr => wr.IsAlive),
                MemoryGrowthRate = _peakMemoryUsage / _testDuration.Elapsed.TotalHours
            }
        };
        
        _logger.LogInformation("Final Memory Leak Test Report: {@Report}", report);
        
        // Save detailed report to file
        var reportPath = Path.Combine("Analysis", $"memory-leak-report-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        
        await File.WriteAllTextAsync(reportPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
        
        _logger.LogInformation("Detailed report saved to: {ReportPath}", reportPath);
    }

    public void Dispose()
    {
        _gcTimer?.Dispose();
        _reportTimer?.Dispose();
    }
}