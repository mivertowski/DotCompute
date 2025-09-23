// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory.P2P
{
    #region Supporting Types - Validation

    /// <summary>
    /// Comprehensive P2P validation result.
    /// </summary>
    public sealed class P2PValidationResult
    {
        public required string ValidationId { get; init; }
        public required DateTimeOffset ValidationTime { get; init; }
        public required bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public required List<P2PValidationDetail> ValidationDetails { get; init; }
    }

    /// <summary>
    /// Individual validation detail result.
    /// </summary>
    public sealed class P2PValidationDetail
    {
        public required string ValidationType { get; init; }
        public required bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>
    /// P2P validation statistics.
    /// </summary>
    public sealed class P2PValidationStatistics
    {
        public long TotalValidations { get; set; }
        public long SuccessfulValidations { get; set; }
        public long FailedValidations { get; set; }
        public long IntegrityChecks { get; set; }
        public long PerformanceValidations { get; set; }
        public long BenchmarksExecuted { get; set; }
        public TimeSpan TotalValidationTime { get; set; }
        public TimeSpan AverageValidationTime { get; set; }
        public long CachedBenchmarkHits { get; set; }
        public double ValidationSuccessRate { get; set; }
    }

    #endregion

    #region Supporting Types - Benchmarking

    /// <summary>
    /// P2P benchmark options for customizing benchmark execution.
    /// </summary>
    public sealed class P2PBenchmarkOptions
    {
        public static P2PBenchmarkOptions Default => new();

        public int MinTransferSizeMB { get; init; } = 1;
        public int MaxTransferSizeMB { get; init; } = 64;
        public bool UseCachedResults { get; init; } = true;
        public int WarmupIterations { get; init; } = 3;
        public int MeasurementIterations { get; init; } = 10;
    }

    /// <summary>
    /// P2P benchmark result for a specific device pair.
    /// </summary>
    public sealed class P2PBenchmarkResult
    {
        public required string BenchmarkId { get; init; }
        public required string SourceDevice { get; init; }
        public required string TargetDevice { get; init; }
        public required DateTimeOffset BenchmarkTime { get; init; }
        public required P2PBenchmarkOptions BenchmarkOptions { get; init; }
        public required List<P2PTransferBenchmark> TransferSizes { get; init; }
        public double PeakThroughputGBps { get; set; }
        public double AverageThroughputGBps { get; set; }
        public double MinimumLatencyMs { get; set; }
        public double AverageLatencyMs { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Individual transfer size benchmark result.
    /// </summary>
    public sealed class P2PTransferBenchmark
    {
        public required long TransferSizeBytes { get; init; }
        public required double ThroughputGBps { get; init; }
        public required double PeakThroughputGBps { get; init; }
        public required double MinThroughputGBps { get; init; }
        public required double LatencyMs { get; init; }
        public required List<double> Measurements { get; init; }
        public required List<double> LatencyMeasurements { get; init; }
    }

    /// <summary>
    /// Multi-GPU benchmark options.
    /// </summary>
    public sealed class P2PMultiGpuBenchmarkOptions
    {
        public static P2PMultiGpuBenchmarkOptions Default => new();

        public bool EnablePairwiseBenchmarks { get; init; } = true;
        public bool EnableScatterBenchmarks { get; init; } = true;
        public bool EnableGatherBenchmarks { get; init; } = true;
        public bool EnableAllToAllBenchmarks { get; init; } = true;
        public int PairwiseTransferSizeMB { get; init; } = 64;
        public int ScatterTransferSizeMB { get; init; } = 32;
        public int GatherTransferSizeMB { get; init; } = 32;
        public int AllToAllTransferSizeMB { get; init; } = 16;
        public bool UseCachedResults { get; init; } = true;
    }

    /// <summary>
    /// Multi-GPU benchmark result.
    /// </summary>
    public sealed class P2PMultiGpuBenchmarkResult
    {
        public required string BenchmarkId { get; init; }
        public required DateTimeOffset BenchmarkTime { get; init; }
        public required int DeviceCount { get; init; }
        public required List<P2PBenchmarkResult> PairwiseBenchmarks { get; init; }
        public required List<P2PScatterBenchmarkResult> ScatterBenchmarks { get; init; }
        public required List<P2PGatherBenchmarkResult> GatherBenchmarks { get; init; }
        public required List<P2PAllToAllBenchmarkResult> AllToAllBenchmarks { get; init; }
        public double PeakPairwiseThroughputGBps { get; set; }
        public double AveragePairwiseThroughputGBps { get; set; }
        public double TotalBenchmarkTimeMs { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// P2P scatter benchmark result.
    /// </summary>
    public sealed class P2PScatterBenchmarkResult
    {
        public required string ScatterPattern { get; init; }
        public required double TotalThroughputGBps { get; init; }
        public required double AverageLatencyMs { get; init; }
    }

    /// <summary>
    /// P2P gather benchmark result.
    /// </summary>
    public sealed class P2PGatherBenchmarkResult
    {
        public required string GatherPattern { get; init; }
        public required double TotalThroughputGBps { get; init; }
        public required double AverageLatencyMs { get; init; }
    }

    /// <summary>
    /// P2P all-to-all benchmark result.
    /// </summary>
    public sealed class P2PAllToAllBenchmarkResult
    {
        public required string CommunicationPattern { get; init; }
        public required double AggregateThroughputGBps { get; init; }
        public required double MaxLatencyMs { get; init; }
    }

    #endregion

    #region Mock Buffer Implementation

    /// <summary>
    /// Mock buffer implementation for testing purposes.
    /// </summary>
    internal sealed class MockBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
        public MockBuffer(IAccelerator accelerator, int length)
        {
            Accelerator = accelerator;
            Length = length;
            SizeInBytes = length * Unsafe.SizeOf<T>();
        }

        public int Length { get; }
        public long SizeInBytes { get; }
        public IAccelerator Accelerator { get; }
        public MemoryOptions Options => MemoryOptions.None;
        public bool IsDisposed => false;
        public BufferState State { get; set; } = BufferState.HostReady;
        public bool IsOnHost => State == BufferState.HostReady || State == BufferState.HostDirty;
        public bool IsOnDevice => State == BufferState.DeviceReady || State == BufferState.DeviceDirty;
        public bool IsDirty => State == BufferState.HostDirty || State == BufferState.DeviceDirty;

        public static ValueTask CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;

        public static ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;

        public static Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => Task.CompletedTask;

        public static ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public static Task ClearAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IUnifiedMemoryBuffer<T> Slice(int offset, int count)
            => new MockBuffer<T>(Accelerator, count);

        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
            => new MockBuffer<TNew>(Accelerator, (int)(SizeInBytes / Unsafe.SizeOf<TNew>()));

        // Memory access methods
        public Span<T> AsSpan() => [];
        public ReadOnlySpan<T> AsReadOnlySpan() => [];
        public Memory<T> AsMemory() => Memory<T>.Empty;
        public ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;

        // Synchronization methods
        public void EnsureOnHost() => State = BufferState.HostReady;
        public void EnsureOnDevice() => State = BufferState.DeviceReady;
        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnHost();
            return ValueTask.CompletedTask;
        }
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnDevice();
            return ValueTask.CompletedTask;
        }
        public void Synchronize() { }
        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        public void MarkHostDirty() => State = BufferState.HostDirty;
        public void MarkDeviceDirty() => State = BufferState.DeviceDirty;

        // Copy methods
        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public MappedMemory<T> Map(MapMode mode) => MappedMemory<T>.Invalid;
        public MappedMemory<T> MapRange(int offset, int count, MapMode mode) => MappedMemory<T>.Invalid;
        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(MappedMemory<T>.Invalid);

        /// <summary>
        /// Copies data from source memory to this mock buffer with offset support.
        /// </summary>
        /// <typeparam name="TSource">Type of elements to copy.</typeparam>
        /// <param name="source">Source data to copy from.</param>
        /// <param name="destinationOffset">Offset into this buffer in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        public ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long destinationOffset, CancellationToken cancellationToken = default) where TSource : unmanaged
            => ValueTask.CompletedTask;

        /// <summary>
        /// Copies data from this mock buffer to destination memory with offset support.
        /// </summary>
        /// <typeparam name="TDest">Type of elements to copy.</typeparam>
        /// <param name="destination">Destination memory to copy to.</param>
        /// <param name="sourceOffset">Offset into this buffer in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        public ValueTask CopyToAsync<TDest>(Memory<TDest> destination, long sourceOffset, CancellationToken cancellationToken = default) where TDest : unmanaged
            => ValueTask.CompletedTask;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion
}