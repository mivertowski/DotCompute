// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Performance.Benchmarks.Stress;

/// <summary>
/// High-frequency transfer stress test for data movement patterns
/// </summary>
public class HighFrequencyTransferStressTest
{
    private readonly ILogger<HighFrequencyTransferStressTest> _logger;
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private readonly ConcurrentBag<TransferMetrics> _metrics = new();
    
    private long _totalTransfers;
    private long _totalBytesTransferred;
    private long _totalTransferTime;
    private long _transferFailures;
    
    public HighFrequencyTransferStressTest(ILogger<HighFrequencyTransferStressTest> logger)
    {
        _logger = logger;
    }

    public async Task RunStressTest(int producerCount, int consumerCount, TimeSpan duration)
    {
        _logger.LogInformation("Starting high-frequency transfer stress test with {ProducerCount} producers, " +
            "{ConsumerCount} consumers for {Duration}", producerCount, consumerCount, duration);

        var cancellationTokenSource = new CancellationTokenSource(duration);
        var tasks = new List<Task>();
        
        // Create multiple transfer patterns
        tasks.AddRange(await CreateMemoryToMemoryTransferPattern(producerCount, consumerCount, cancellationTokenSource.Token));
        tasks.AddRange(await CreateChannelTransferPattern(producerCount, consumerCount, cancellationTokenSource.Token));
        tasks.AddRange(await CreateStreamTransferPattern(producerCount, consumerCount, cancellationTokenSource.Token));
        tasks.AddRange(await CreateArrayPoolTransferPattern(producerCount, consumerCount, cancellationTokenSource.Token));
        tasks.AddRange(await CreateNativeMemoryTransferPattern(producerCount, consumerCount, cancellationTokenSource.Token));
        
        // Start monitoring
        tasks.Add(Task.Run(() => MonitoringWorker(cancellationTokenSource.Token)));
        
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when duration expires
        }
        
        await GenerateTransferStressReport(producerCount, consumerCount, duration);
    }

    private async Task<List<Task>> CreateMemoryToMemoryTransferPattern(int producerCount, int consumerCount, CancellationToken cancellationToken)
    {
        var queue = new ConcurrentQueue<byte[]>();
        var tasks = new List<Task>();
        
        // Producers
        for (int i = 0; i < producerCount; i++)
        {
            var producerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var random = new Random(producerId);
                var metrics = new TransferMetrics { PatternName = "MemoryToMemory", Role = "Producer", Id = producerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var size = random.Next(1024, 32768);
                        var data = new byte[size];
                        random.NextBytes(data);
                        
                        var transferStart = stopwatch.ElapsedTicks;
                        queue.Enqueue(data);
                        var transferEnd = stopwatch.ElapsedTicks;
                        
                        metrics.TransferCount++;
                        metrics.BytesTransferred += size;
                        metrics.TotalTransferTime += transferEnd - transferStart;
                        
                        Interlocked.Increment(ref _totalTransfers);
                        Interlocked.Add(ref _totalBytesTransferred, size);
                        
                        await Task.Delay(random.Next(1, 5), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        Interlocked.Increment(ref _transferFailures);
                        _logger.LogWarning("Memory-to-memory producer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        // Consumers
        for (int i = 0; i < consumerCount; i++)
        {
            var consumerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var metrics = new TransferMetrics { PatternName = "MemoryToMemory", Role = "Consumer", Id = consumerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (queue.TryDequeue(out var data))
                        {
                            var transferStart = stopwatch.ElapsedTicks;
                            
                            // Simulate processing
                            var checksum = data.Sum(b => b);
                            
                            var transferEnd = stopwatch.ElapsedTicks;
                            
                            metrics.TransferCount++;
                            metrics.BytesTransferred += data.Length;
                            metrics.TotalTransferTime += transferEnd - transferStart;
                        }
                        else
                        {
                            await Task.Delay(1, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        _logger.LogWarning("Memory-to-memory consumer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        return tasks;
    }

    private async Task<List<Task>> CreateChannelTransferPattern(int producerCount, int consumerCount, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = consumerCount == 1,
            SingleWriter = producerCount == 1
        });
        
        var tasks = new List<Task>();
        
        // Producers
        for (int i = 0; i < producerCount; i++)
        {
            var producerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var random = new Random(producerId);
                var metrics = new TransferMetrics { PatternName = "Channel", Role = "Producer", Id = producerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var size = random.Next(1024, 32768);
                        var data = new byte[size];
                        random.NextBytes(data);
                        
                        var transferStart = stopwatch.ElapsedTicks;
                        await channel.Writer.WriteAsync(data, cancellationToken);
                        var transferEnd = stopwatch.ElapsedTicks;
                        
                        metrics.TransferCount++;
                        metrics.BytesTransferred += size;
                        metrics.TotalTransferTime += transferEnd - transferStart;
                        
                        Interlocked.Increment(ref _totalTransfers);
                        Interlocked.Add(ref _totalBytesTransferred, size);
                        
                        await Task.Delay(random.Next(1, 5), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        Interlocked.Increment(ref _transferFailures);
                        _logger.LogWarning("Channel producer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        // Consumers
        for (int i = 0; i < consumerCount; i++)
        {
            var consumerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var metrics = new TransferMetrics { PatternName = "Channel", Role = "Consumer", Id = consumerId };
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    await foreach (var data in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        var transferStart = stopwatch.ElapsedTicks;
                        
                        // Simulate processing
                        var checksum = data.Sum(b => b);
                        
                        var transferEnd = stopwatch.ElapsedTicks;
                        
                        metrics.TransferCount++;
                        metrics.BytesTransferred += data.Length;
                        metrics.TotalTransferTime += transferEnd - transferStart;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
                catch (Exception ex)
                {
                    metrics.Errors++;
                    _logger.LogWarning("Channel consumer error: {Error}", ex.Message);
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        return tasks;
    }

    private async Task<List<Task>> CreateStreamTransferPattern(int producerCount, int consumerCount, CancellationToken cancellationToken)
    {
        var streamQueue = new ConcurrentQueue<MemoryStream>();
        var tasks = new List<Task>();
        
        // Producers
        for (int i = 0; i < producerCount; i++)
        {
            var producerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var random = new Random(producerId);
                var metrics = new TransferMetrics { PatternName = "Stream", Role = "Producer", Id = producerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var size = random.Next(1024, 32768);
                        var data = new byte[size];
                        random.NextBytes(data);
                        
                        var transferStart = stopwatch.ElapsedTicks;
                        var stream = new MemoryStream(data);
                        streamQueue.Enqueue(stream);
                        var transferEnd = stopwatch.ElapsedTicks;
                        
                        metrics.TransferCount++;
                        metrics.BytesTransferred += size;
                        metrics.TotalTransferTime += transferEnd - transferStart;
                        
                        Interlocked.Increment(ref _totalTransfers);
                        Interlocked.Add(ref _totalBytesTransferred, size);
                        
                        await Task.Delay(random.Next(1, 5), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        Interlocked.Increment(ref _transferFailures);
                        _logger.LogWarning("Stream producer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        // Consumers
        for (int i = 0; i < consumerCount; i++)
        {
            var consumerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var metrics = new TransferMetrics { PatternName = "Stream", Role = "Consumer", Id = consumerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (streamQueue.TryDequeue(out var stream))
                        {
                            using (stream)
                            {
                                var transferStart = stopwatch.ElapsedTicks;
                                
                                var buffer = new byte[4096];
                                long totalRead = 0;
                                int bytesRead;
                                
                                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    totalRead += bytesRead;
                                    // Simulate processing
                                    var checksum = buffer.Take(bytesRead).Sum(b => b);
                                }
                                
                                var transferEnd = stopwatch.ElapsedTicks;
                                
                                metrics.TransferCount++;
                                metrics.BytesTransferred += totalRead;
                                metrics.TotalTransferTime += transferEnd - transferStart;
                            }
                        }
                        else
                        {
                            await Task.Delay(1, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        _logger.LogWarning("Stream consumer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        return tasks;
    }

    private async Task<List<Task>> CreateArrayPoolTransferPattern(int producerCount, int consumerCount, CancellationToken cancellationToken)
    {
        var poolQueue = new ConcurrentQueue<(byte[] Array, int Length)>();
        var tasks = new List<Task>();
        
        // Producers
        for (int i = 0; i < producerCount; i++)
        {
            var producerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var random = new Random(producerId);
                var metrics = new TransferMetrics { PatternName = "ArrayPool", Role = "Producer", Id = producerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var size = random.Next(1024, 32768);
                        
                        var transferStart = stopwatch.ElapsedTicks;
                        var array = _arrayPool.Rent(size);
                        random.NextBytes(array.AsSpan(0, size));
                        poolQueue.Enqueue((array, size));
                        var transferEnd = stopwatch.ElapsedTicks;
                        
                        metrics.TransferCount++;
                        metrics.BytesTransferred += size;
                        metrics.TotalTransferTime += transferEnd - transferStart;
                        
                        Interlocked.Increment(ref _totalTransfers);
                        Interlocked.Add(ref _totalBytesTransferred, size);
                        
                        await Task.Delay(random.Next(1, 5), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        Interlocked.Increment(ref _transferFailures);
                        _logger.LogWarning("ArrayPool producer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        // Consumers
        for (int i = 0; i < consumerCount; i++)
        {
            var consumerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var metrics = new TransferMetrics { PatternName = "ArrayPool", Role = "Consumer", Id = consumerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (poolQueue.TryDequeue(out var pooledItem))
                        {
                            var transferStart = stopwatch.ElapsedTicks;
                            
                            // Simulate processing
                            var checksum = pooledItem.Array.AsSpan(0, pooledItem.Length).ToArray().Sum(b => b);
                            
                            _arrayPool.Return(pooledItem.Array);
                            
                            var transferEnd = stopwatch.ElapsedTicks;
                            
                            metrics.TransferCount++;
                            metrics.BytesTransferred += pooledItem.Length;
                            metrics.TotalTransferTime += transferEnd - transferStart;
                        }
                        else
                        {
                            await Task.Delay(1, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        _logger.LogWarning("ArrayPool consumer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        return tasks;
    }

    private async Task<List<Task>> CreateNativeMemoryTransferPattern(int producerCount, int consumerCount, CancellationToken cancellationToken)
    {
        var nativeQueue = new ConcurrentQueue<(IntPtr Pointer, int Size)>();
        var tasks = new List<Task>();
        
        // Producers
        for (int i = 0; i < producerCount; i++)
        {
            var producerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var random = new Random(producerId);
                var metrics = new TransferMetrics { PatternName = "NativeMemory", Role = "Producer", Id = producerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var size = random.Next(1024, 32768);
                        
                        var transferStart = stopwatch.ElapsedTicks;
                        
                        unsafe
                        {
                            var ptr = System.Runtime.InteropServices.NativeMemory.Alloc((nuint)size);
                            var span = new Span<byte>(ptr, size);
                            random.NextBytes(span);
                            nativeQueue.Enqueue(((IntPtr)ptr, size));
                        }
                        
                        var transferEnd = stopwatch.ElapsedTicks;
                        
                        metrics.TransferCount++;
                        metrics.BytesTransferred += size;
                        metrics.TotalTransferTime += transferEnd - transferStart;
                        
                        Interlocked.Increment(ref _totalTransfers);
                        Interlocked.Add(ref _totalBytesTransferred, size);
                        
                        await Task.Delay(random.Next(1, 5), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        Interlocked.Increment(ref _transferFailures);
                        _logger.LogWarning("NativeMemory producer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        // Consumers
        for (int i = 0; i < consumerCount; i++)
        {
            var consumerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var metrics = new TransferMetrics { PatternName = "NativeMemory", Role = "Consumer", Id = consumerId };
                var stopwatch = Stopwatch.StartNew();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (nativeQueue.TryDequeue(out var nativeItem))
                        {
                            var transferStart = stopwatch.ElapsedTicks;
                            
                            unsafe
                            {
                                var span = new Span<byte>(nativeItem.Pointer.ToPointer(), nativeItem.Size);
                                var checksum = 0;
                                foreach (var b in span)
                                {
                                    checksum += b;
                                }
                                
                                System.Runtime.InteropServices.NativeMemory.Free(nativeItem.Pointer.ToPointer());
                            }
                            
                            var transferEnd = stopwatch.ElapsedTicks;
                            
                            metrics.TransferCount++;
                            metrics.BytesTransferred += nativeItem.Size;
                            metrics.TotalTransferTime += transferEnd - transferStart;
                        }
                        else
                        {
                            await Task.Delay(1, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        metrics.Errors++;
                        _logger.LogWarning("NativeMemory consumer error: {Error}", ex.Message);
                    }
                }
                
                metrics.Duration = stopwatch.Elapsed;
                _metrics.Add(metrics);
            }));
        }
        
        return tasks;
    }

    private async Task MonitoringWorker(CancellationToken cancellationToken)
    {
        var lastReport = DateTime.UtcNow;
        var lastTransfers = 0L;
        var lastBytes = 0L;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now - lastReport >= TimeSpan.FromSeconds(30))
                {
                    var currentTransfers = _totalTransfers;
                    var currentBytes = _totalBytesTransferred;
                    
                    var transferRate = (currentTransfers - lastTransfers) / 30.0;
                    var throughputMBps = (currentBytes - lastBytes) / (1024.0 * 1024.0) / 30.0;
                    
                    _logger.LogInformation(
                        "Transfer Stress Status - " +
                        "Total Transfers: {Transfers:N0}, " +
                        "Transfer Rate: {Rate:N1}/sec, " +
                        "Total Bytes: {BytesMB:N1} MB, " +
                        "Throughput: {ThroughputMBps:N1} MB/sec, " +
                        "Failures: {Failures:N0}",
                        currentTransfers,
                        transferRate,
                        currentBytes / (1024.0 * 1024.0),
                        throughputMBps,
                        _transferFailures);
                    
                    lastTransfers = currentTransfers;
                    lastBytes = currentBytes;
                    lastReport = now;
                }
                
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in transfer monitoring worker");
            }
        }
    }

    private async Task GenerateTransferStressReport(int producerCount, int consumerCount, TimeSpan duration)
    {
        var patternGroups = _metrics.GroupBy(m => m.PatternName).ToArray();
        
        var report = new
        {
            TestConfiguration = new
            {
                ProducerCount = producerCount,
                ConsumerCount = consumerCount,
                Duration = duration,
                TestCompleted = DateTime.UtcNow
            },
            OverallResults = new
            {
                TotalTransfers = _totalTransfers,
                TotalBytesTransferredMB = _totalBytesTransferred / (1024.0 * 1024.0),
                AverageTransferRate = _totalTransfers / duration.TotalSeconds,
                AverageThroughputMBps = (_totalBytesTransferred / (1024.0 * 1024.0)) / duration.TotalSeconds,
                TransferFailures = _transferFailures,
                FailureRate = _transferFailures / (double)_totalTransfers * 100
            },
            PatternResults = patternGroups.Select(g => new
            {
                PatternName = g.Key,
                TotalTransfers = g.Sum(m => m.TransferCount),
                TotalBytesTransferredMB = g.Sum(m => m.BytesTransferred) / (1024.0 * 1024.0),
                AverageTransferRate = g.Sum(m => m.TransferCount) / g.Average(m => m.Duration.TotalSeconds),
                AverageThroughputMBps = (g.Sum(m => m.BytesTransferred) / (1024.0 * 1024.0)) / g.Average(m => m.Duration.TotalSeconds),
                ProducerMetrics = g.Where(m => m.Role == "Producer").Select(m => new
                {
                    m.Id,
                    m.TransferCount,
                    BytesTransferredMB = m.BytesTransferred / (1024.0 * 1024.0),
                    TransferRate = m.TransferCount / m.Duration.TotalSeconds,
                    AverageTransferTime = m.TotalTransferTime / (double)m.TransferCount,
                    m.Errors
                }).ToArray(),
                ConsumerMetrics = g.Where(m => m.Role == "Consumer").Select(m => new
                {
                    m.Id,
                    m.TransferCount,
                    BytesTransferredMB = m.BytesTransferred / (1024.0 * 1024.0),
                    TransferRate = m.TransferCount / m.Duration.TotalSeconds,
                    AverageTransferTime = m.TotalTransferTime / (double)m.TransferCount,
                    m.Errors
                }).ToArray()
            }).ToArray()
        };
        
        _logger.LogInformation("High-Frequency Transfer Stress Test Report: {@Report}", report);
        
        // Save detailed report to file
        var reportPath = Path.Combine("Analysis", $"transfer-stress-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        
        await File.WriteAllTextAsync(reportPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
        
        _logger.LogInformation("Detailed transfer stress report saved to: {ReportPath}", reportPath);
    }

    private class TransferMetrics
    {
        public string PatternName { get; set; } = "";
        public string Role { get; set; } = "";
        public int Id { get; set; }
        public TimeSpan Duration { get; set; }
        public long TransferCount { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalTransferTime { get; set; }
        public long Errors { get; set; }
    }
}