// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Memory;
using FluentAssertions;

namespace DotCompute.Tests.Integration
{

/// <summary>
/// Utility class for advanced memory transfer operations in tests.
/// </summary>
public sealed class MemoryTransferUtility : IAsyncDisposable
{
    private readonly AdvancedMemoryTransferEngine _transferEngine;
    private readonly IMemoryManager _memoryManager;
    private bool _disposed;

    public MemoryTransferUtility(IMemoryManager memoryManager)
    {
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _transferEngine = new AdvancedMemoryTransferEngine(memoryManager);
    }

    /// <summary>
    /// Performs a large dataset transfer using advanced streaming techniques.
    /// </summary>
    public async Task<(bool Success, string? Error, IMemoryBuffer? Buffer, double ThroughputMBps)> PerformLargeDataTransferAsync<T>(
        T[] data,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        try
        {
            var options = new TransferOptions
            {
                EnableMemoryMapping = data.Length * Unsafe.SizeOf<T>() > 64 * 1024 * 1024, // 64MB threshold
                VerifyIntegrity = true,
                IntegritySampleSize = Math.Min(1000, data.Length / 100), // 1% sample or max 1000 elements
                ChunkSize = 4 * 1024 * 1024 // 4MB chunks
            };

            var result = await _transferEngine.TransferLargeDatasetAsync(data, accelerator, options, cancellationToken);

            return (result.Success, result.Error, result.TransferredBuffer, result.ThroughputMBps);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null, 0.0);
        }
    }

    /// <summary>
    /// Performs concurrent async transfers with optimal parallelism.
    /// </summary>
    public async Task<(bool Success, double ConcurrentTimeMs, double SequentialTimeMs, double SpeedupRatio)> PerformConcurrentTransfersAsync<T>(
        T[][] dataSets,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Perform concurrent transfers
            var concurrentOptions = new ConcurrentTransferOptions
            {
                MaxConcurrency = Math.Max(2, Environment.ProcessorCount / 2), // Use moderate concurrency
                EnableMemoryMapping = false, // Disable for smaller test datasets
                VerifyIntegrity = false, // Skip integrity checking for performance
                ChunkSize = 1024 * 1024 // 1MB chunks for better parallelism
            };

            var concurrentResult = await _transferEngine.ExecuteConcurrentTransfersAsync(dataSets, accelerator, concurrentOptions, cancellationToken);
            stopwatch.Stop();
            var concurrentTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            // Perform sequential transfers for comparison
            stopwatch.Restart();
            var sequentialOptions = new ConcurrentTransferOptions
            {
                MaxConcurrency = 1, // Force sequential execution
                EnableMemoryMapping = false,
                VerifyIntegrity = false,
                ChunkSize = 1024 * 1024
            };

            var sequentialResult = await _transferEngine.ExecuteConcurrentTransfersAsync(dataSets, accelerator, sequentialOptions, cancellationToken);
            stopwatch.Stop();
            var sequentialTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            var speedupRatio = sequentialTimeMs > 0 ? sequentialTimeMs / concurrentTimeMs : 1.0;

            // Clean up buffers
            if (concurrentResult.Success)
            {
                foreach (var result in concurrentResult.Results)
                {
                    if (result.TransferredBuffer != null)
                    {
                        await result.TransferredBuffer.DisposeAsync();
                    }
                }
            }

            if (sequentialResult.Success)
            {
                foreach (var result in sequentialResult.Results)
                {
                    if (result.TransferredBuffer != null)
                    {
                        await result.TransferredBuffer.DisposeAsync();
                    }
                }
            }

            return (concurrentResult.Success, concurrentTimeMs, sequentialTimeMs, speedupRatio);
        }
        catch (Exception)
        {
            return (false, 0.0, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Creates test data with proper size distribution for transfer testing.
    /// </summary>
    public static T[] CreateLargeTestData<T>(int elementCount) where T : unmanaged
    {
        var data = new T[elementCount];
        var random = new Random(42); // Deterministic for consistent testing

        if (typeof(T) == typeof(float))
        {
            var floatData = data as float[];
            for (var i = 0; i < elementCount; i++)
            {
                floatData![i] = random.NextSingle();
            }
        }
        else if (typeof(T) == typeof(int))
        {
            var intData = data as int[];
            for (var i = 0; i < elementCount; i++)
            {
                intData![i] = random.Next();
            }
        }
        else if (typeof(T) == typeof(byte))
        {
            var byteData = data as byte[];
            random.NextBytes(byteData!);
        }
        else
        {
            // Fill with pattern data for other types
            var bytes = MemoryMarshal.AsBytes(data.AsSpan());
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 256);
            }
        }

        return data;
    }

    /// <summary>
    /// Creates multiple datasets for concurrent transfer testing.
    /// </summary>
    public static T[][] CreateMultipleTestDatasets<T>(int datasetCount, int elementsPerDataset) where T : unmanaged
    {
        var datasets = new T[datasetCount][];
        for (var i = 0; i < datasetCount; i++)
        {
            datasets[i] = CreateLargeTestData<T>(elementsPerDataset);
        }
        return datasets;
    }

    /// <summary>
    /// Verifies data integrity with proper bounds checking.
    /// </summary>
    public static bool VerifyDataIntegrityWithSafeIndexing<T>(T[] original, T[] transferred, int[]? sampleIndices = null) where T : unmanaged
    {
        if (original.Length != transferred.Length)
            return false;

        if (sampleIndices != null)
        {
            // Ensure all sample indices are within valid bounds
            var maxValidIndex = Math.Min(original.Length, transferred.Length) - 1;
            var validIndices = sampleIndices.Where(i => i >= 0 && i <= maxValidIndex).ToArray();

            if (validIndices.Length == 0)
                return true; // No valid indices to check

            foreach (var index in validIndices)
            {
                if (!original[index].Equals(transferred[index]))
                    return false;
            }
            return true;
        }
        else
        {
            // Full comparison for smaller datasets
            return original.AsSpan().SequenceEqual(transferred.AsSpan());
        }
    }

    /// <summary>
    /// Gets transfer engine statistics.
    /// </summary>
    public TransferStatistics Statistics => _transferEngine.Statistics;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await _transferEngine.DisposeAsync();
    }
}
}
