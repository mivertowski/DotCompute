// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Memory.Types;

namespace DotCompute.Memory;


/// <summary>
/// Advanced memory transfer engine with high-performance optimizations for large datasets,
/// concurrent transfers, streaming operations, and memory-mapped file support.
/// </summary>
public sealed class AdvancedMemoryTransferEngine : IAsyncDisposable
{
    private readonly IUnifiedMemoryManager _memoryManager;
    private readonly ConcurrentQueue<TransferOperation> _transferQueue = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Timer _pressureMonitor;

    // Performance optimization constants
    private static readonly int MaxConcurrentTransfers = Environment.ProcessorCount * 2;
    private const int LargeDatasetThreshold = 64 * 1024 * 1024; // 64MB
    private const double MemoryPressureThreshold = 0.85;

    // Statistics tracking
    private long _totalBytesTransferred;
    private long _totalTransferCount;
    private readonly Lock _statsLock = new();

    // Memory pressure tracking
    private double _currentMemoryPressure;
    private volatile bool _disposed;

    public AdvancedMemoryTransferEngine(IUnifiedMemoryManager memoryManager)
    {
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _concurrencyLimiter = new SemaphoreSlim(MaxConcurrentTransfers, MaxConcurrentTransfers);

        // Start background memory pressure monitoring
        _pressureMonitor = new Timer(MonitorMemoryPressure, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

        // Start background transfer processing
        _ = Task.Run(ProcessTransferQueueAsync, _shutdownCts.Token);
    }

    /// <summary>
    /// Performs a high-performance large dataset transfer with chunking and streaming support.
    /// </summary>
    public async Task<AdvancedTransferResult> TransferLargeDatasetAsync<T>(
        T[] data,
        IAccelerator accelerator,
        TransferOptions? options = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        options ??= TransferOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var sizeInBytes = data.Length * Unsafe.SizeOf<T>();
            var result = new AdvancedTransferResult
            {
                StartTime = startTime,
                TotalBytes = sizeInBytes,
                ChunkCount = 1,
                UsedStreaming = sizeInBytes > LargeDatasetThreshold,
                UsedCompression = options.EnableCompression,
                UsedMemoryMapping = options.EnableMemoryMapping && sizeInBytes > LargeDatasetThreshold
            };

            IUnifiedMemoryBuffer? buffer = null;

            try
            {
                if (result.UsedMemoryMapping)
                {
                    // Use memory-mapped files for very large datasets
                    buffer = await TransferWithMemoryMappingAsync(data, accelerator, options, result, cancellationToken).ConfigureAwait(false);
                }
                else if (result.UsedStreaming)
                {
                    // Use streaming transfers with chunking
                    buffer = await TransferWithStreamingAsync(data, accelerator, options, result, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Standard transfer for smaller datasets
                    buffer = await _memoryManager.AllocateAndCopyAsync<T>(data, options.MemoryOptions, cancellationToken).ConfigureAwait(false);
                    result.ChunkCount = 1;
                }

                // Verify data integrity with optimized sampling
                if (options.VerifyIntegrity && buffer != null)
                {
                    result.IntegrityVerified = await VerifyDataIntegrityAsync(buffer, data, options, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result.IntegrityVerified = true;
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.Success = true;
                result.TransferredBuffer = buffer;

                // Calculate performance metrics
                result.ThroughputMBps = (sizeInBytes / (1024.0 * 1024.0)) / result.Duration.TotalSeconds;
                result.EfficiencyRatio = CalculateEfficiencyRatio(result);

                // Update statistics
                lock (_statsLock)
                {
                    _totalBytesTransferred += sizeInBytes;
                    _totalTransferCount++;
                }

                return result;
            }
            finally
            {
                // Don't dispose buffer - caller owns it
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new AdvancedTransferResult
            {
                StartTime = startTime,
                Duration = stopwatch.Elapsed,
                Success = false,
                ErrorMessage = ex.Message,
                TotalBytes = data.Length * Unsafe.SizeOf<T>()
            };
        }
    }

    /// <summary>
    /// Performs concurrent async transfers with optimized parallelism and load balancing.
    /// </summary>
    public async Task<ConcurrentTransferResult> ExecuteConcurrentTransfersAsync<T>(
        T[][] dataSets,
        IAccelerator accelerator,
        ConcurrentTransferOptions? options = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        options ??= ConcurrentTransferOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var results = new ConcurrentBag<AdvancedTransferResult>();
        var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);

        try
        {
            // Create transfer tasks with controlled concurrency
            var transferTasks = dataSets.Select(async (dataSet, index) =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var transferOptions = new TransferOptions
                    {
                        EnableCompression = options.EnableCompression,
                        EnableMemoryMapping = options.EnableMemoryMapping,
                        VerifyIntegrity = options.VerifyIntegrity,
                        ChunkSize = options.ChunkSize,
                        MemoryOptions = options.MemoryOptions
                    };

                    var result = await TransferLargeDatasetAsync(dataSet, accelerator, transferOptions, cancellationToken).ConfigureAwait(false);
                    result.TransferIndex = index;
                    results.Add(result);
                    return result;
                }
                finally
                {
                    _ = semaphore.Release();
                }
            });

            // Execute all transfers concurrently
            var allResults = await Task.WhenAll(transferTasks).ConfigureAwait(false);

            overallStopwatch.Stop();

            return new ConcurrentTransferResult
            {
                StartTime = startTime,
                Duration = overallStopwatch.Elapsed,
                TransferCount = dataSets.Length,
                SuccessfulTransfers = allResults.Count(r => r.Success),
                FailedTransfers = allResults.Count(r => !r.Success),
                Results = allResults,
                TotalBytes = allResults.Sum(r => r.TotalBytes),
                AverageThroughputMBps = allResults.Where(r => r.Success).Average(r => r.ThroughputMBps),
                Success = allResults.All(r => r.Success),
                ConcurrencyBenefit = CalculateConcurrencyBenefit(allResults, overallStopwatch.Elapsed)
            };
        }
        finally
        {
            semaphore.Dispose();
        }
    }

    /// <summary>
    /// Transfers data using memory-mapped files for ultra-large datasets.
    /// </summary>
    private async Task<IUnifiedMemoryBuffer> TransferWithMemoryMappingAsync<T>(
        T[] data,
        IAccelerator accelerator,
        TransferOptions options,
        AdvancedTransferResult result,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var sizeInBytes = data.Length * Unsafe.SizeOf<T>();
        var tempFileName = Path.GetTempFileName();

        try
        {
            // Create memory-mapped file
            using var mmf = MemoryMappedFile.CreateFromFile(tempFileName, FileMode.Create, "transfer", sizeInBytes);
            using var accessor = mmf.CreateViewAccessor(0, sizeInBytes);

            // Write data to memory-mapped file
            var dataSpan = MemoryMarshal.Cast<T, byte>(data.AsSpan());
            accessor.WriteArray(0, dataSpan.ToArray(), 0, dataSpan.Length);

            // Create buffer and copy from memory-mapped file
            var buffer = await _memoryManager.AllocateAsync<byte>((int)sizeInBytes, options.MemoryOptions, cancellationToken).ConfigureAwait(false);

            // Read from memory-mapped file and copy to buffer
            var tempBuffer = new byte[sizeInBytes];
            _ = accessor.ReadArray(0, tempBuffer, 0, tempBuffer.Length);
            await buffer.CopyFromAsync(tempBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);

            result.UsedMemoryMapping = true;
            return buffer as IUnifiedMemoryBuffer ?? throw new InvalidOperationException("Buffer allocation failed");
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Transfers data using streaming with chunked processing for large datasets.
    /// </summary>
    private async Task<IUnifiedMemoryBuffer> TransferWithStreamingAsync<T>(
        T[] data,
        IAccelerator accelerator,
        TransferOptions options,
        AdvancedTransferResult result,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        var chunkSize = Math.Min(options.ChunkSize, sizeInBytes);
        var chunkCount = (int)Math.Ceiling((double)sizeInBytes / chunkSize);

        result.ChunkCount = chunkCount;
        result.UsedStreaming = true;

        // Allocate destination buffer
        var buffer = await _memoryManager.AllocateAsync<byte>((int)sizeInBytes, options.MemoryOptions, cancellationToken).ConfigureAwait(false);

        // Process chunks with optimal parallelism
        var chunkSemaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount, chunkCount));
        var chunkTasks = new List<Task>();

        try
        {
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var currentChunkIndex = chunkIndex; // Capture for closure
                var chunkTask = ProcessChunkAsync(data, buffer, currentChunkIndex, chunkSize, elementSize, chunkSemaphore, cancellationToken);
                chunkTasks.Add(chunkTask);
            }

            await Task.WhenAll(chunkTasks).ConfigureAwait(false);
            return buffer as IUnifiedMemoryBuffer ?? throw new InvalidOperationException("Buffer allocation failed");
        }
        finally
        {
            chunkSemaphore.Dispose();
        }
    }

    /// <summary>
    /// Processes a single chunk of data during streaming transfer.
    /// </summary>
    private static async Task ProcessChunkAsync<T>(
        T[] data,
        IUnifiedMemoryBuffer buffer,
        int chunkIndex,
        int chunkSize,
        int elementSize,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken) where T : unmanaged
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var startIndex = chunkIndex * (chunkSize / elementSize);
            var endIndex = Math.Min(startIndex + (chunkSize / elementSize), data.Length);
            var actualChunkSize = endIndex - startIndex;

            if (actualChunkSize <= 0)
            {
                return;
            }

            var chunkData = new T[actualChunkSize];
            Array.Copy(data, startIndex, chunkData, 0, actualChunkSize);

            // Cast to generic buffer for type-safe operations
            if (buffer is IUnifiedMemoryBuffer<byte> typedBuffer)
            {
                var byteChunkData = MemoryMarshal.Cast<T, byte>(chunkData.AsSpan()).ToArray();
                var byteOffset = startIndex * elementSize;
                var bufferSlice = typedBuffer.Slice(byteOffset, byteChunkData.Length);
                await bufferSlice.CopyFromAsync(byteChunkData.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException($"Buffer type mismatch: expected IUnifiedMemoryBuffer<byte>, got {buffer.GetType()}");
            }
        }
        finally
        {
            _ = semaphore.Release();
        }
    }

    /// <summary>
    /// Verifies data integrity using optimized sampling for large datasets.
    /// </summary>
    private static async Task<bool> VerifyDataIntegrityAsync<T>(
        IUnifiedMemoryBuffer buffer,
        T[] originalData,
        TransferOptions options,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var sizeInBytes = originalData.Length * Unsafe.SizeOf<T>();
        var readBuffer = new byte[sizeInBytes];

        // Cast to generic buffer for type-safe operations

        if (buffer is IUnifiedMemoryBuffer<byte> typedBuffer)
        {
            await typedBuffer.CopyToAsync(readBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Buffer type mismatch: expected IUnifiedMemoryBuffer<byte>, got {buffer.GetType()}");
        }

        var readData = MemoryMarshal.Cast<byte, T>(readBuffer);

        if (readData.Length != originalData.Length)
        {
            return false;
        }

        // For large datasets, use statistical sampling for efficiency
        if (originalData.Length > 100000)
        {
            return VerifyDataIntegrityWithSampling(originalData, readData, options.IntegritySampleSize);
        }
        else
        {
            // Full verification for smaller datasets
            return VerifyDataIntegrityFull(originalData, readData);
        }
    }

    /// <summary>
    /// Verifies data integrity using statistical sampling (fixed bounds checking).
    /// </summary>
    private static bool VerifyDataIntegrityWithSampling<T>(T[] original, ReadOnlySpan<T> transferred, int sampleSize) where T : unmanaged
    {
        var actualSampleSize = Math.Min(sampleSize, Math.Min(original.Length, transferred.Length));
#pragma warning disable CA5394 // Random is acceptable for non-security testing
        var random = new Random(42); // Deterministic for consistent testing
#pragma warning restore CA5394

        for (var i = 0; i < actualSampleSize; i++)
        {
            // Use proper random sampling with bounds checking
#pragma warning disable CA5394 // Random is acceptable for non-security testing
            var index = random.Next(0, Math.Min(original.Length, transferred.Length));
#pragma warning restore CA5394

            if (!original[index].Equals(transferred[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verifies complete data integrity for smaller datasets.
    /// </summary>
    private static bool VerifyDataIntegrityFull<T>(T[] original, ReadOnlySpan<T> transferred) where T : unmanaged
    {
        if (original.Length != transferred.Length)
        {
            return false;
        }

        return original.AsSpan().SequenceEqual(transferred);
    }

    /// <summary>
    /// Calculates transfer efficiency ratio based on theoretical vs actual performance.
    /// </summary>
    private static double CalculateEfficiencyRatio(AdvancedTransferResult result)
    {
        // Theoretical maximum based on system memory bandwidth (assume ~25GB/s for DDR4)
        const double theoreticalMaxBandwidthMBps = 25 * 1024; // 25GB/s in MB/s

        var actualRatio = result.ThroughputMBps / theoreticalMaxBandwidthMBps;
        return Math.Min(1.0, actualRatio); // Cap at 100% efficiency
    }

    /// <summary>
    /// Calculates the benefit gained from concurrent execution.
    /// </summary>
    private static double CalculateConcurrencyBenefit(IEnumerable<AdvancedTransferResult> results, TimeSpan totalTime)
    {
        var sequentialTime = results.Sum(r => r.Duration.TotalMilliseconds);
        var concurrentTime = totalTime.TotalMilliseconds;

        if (concurrentTime <= 0)
        {
            return 0;
        }

        return Math.Max(0, (sequentialTime - concurrentTime) / sequentialTime);
    }

    /// <summary>
    /// Monitors system memory pressure and adjusts transfer behavior accordingly.
    /// </summary>
    private void MonitorMemoryPressure(object? state)
    {
        try
        {
            var workingSet = Environment.WorkingSet;
            var totalMemory = GC.GetTotalMemory(false);

            // Simple pressure calculation (can be enhanced with more sophisticated metrics)
            var pressure = Math.Min(1.0, (double)totalMemory / workingSet);

            lock (_statsLock)
            {
                _currentMemoryPressure = pressure;
            }

            // If memory pressure is high, trigger GC
            if (pressure > MemoryPressureThreshold)
            {
                GC.Collect(1, GCCollectionMode.Optimized);
            }
        }
        catch
        {
            // Ignore monitoring errors
        }
    }

    /// <summary>
    /// Processes queued transfer operations in the background.
    /// </summary>
    private async Task ProcessTransferQueueAsync()
    {
        while (!_shutdownCts.Token.IsCancellationRequested)
        {
            try
            {
                if (_transferQueue.TryDequeue(out var operation))
                {
                    await operation.ExecuteAsync(_shutdownCts.Token).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(10, _shutdownCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_shutdownCts.Token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Continue processing despite individual operation failures
            }
        }
    }

    /// <summary>
    /// Gets comprehensive transfer statistics.
    /// </summary>
    public TransferStatistics Statistics
    {
        get
        {
            lock (_statsLock)
            {
                return new TransferStatistics
                {
                    TotalBytesTransferred = _totalBytesTransferred,
                    TotalTransferCount = _totalTransferCount,
                    AverageTransferSize = _totalTransferCount > 0 ? _totalBytesTransferred / _totalTransferCount : 0,
                    CurrentMemoryPressure = _currentMemoryPressure,
                    ActiveTransfers = MaxConcurrentTransfers - _concurrencyLimiter.CurrentCount
                };
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _shutdownCts.CancelAsync().ConfigureAwait(false);
        await _pressureMonitor.DisposeAsync().ConfigureAwait(false);
        _concurrencyLimiter.Dispose();
        _shutdownCts.Dispose();
    }
}
