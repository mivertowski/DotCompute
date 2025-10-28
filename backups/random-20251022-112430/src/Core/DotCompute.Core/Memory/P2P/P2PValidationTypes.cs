// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
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
        /// <summary>
        /// Gets or sets the validation identifier.
        /// </summary>
        /// <value>The validation id.</value>
        public required string ValidationId { get; init; }
        /// <summary>
        /// Gets or sets the validation time.
        /// </summary>
        /// <value>The validation time.</value>
        public required DateTimeOffset ValidationTime { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether valid.
        /// </summary>
        /// <value>The is valid.</value>
        public required bool IsValid { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the validation details.
        /// </summary>
        /// <value>The validation details.</value>
        public required IList<P2PValidationDetail> ValidationDetails { get; init; }
    }

    /// <summary>
    /// Individual validation detail result.
    /// </summary>
    public sealed class P2PValidationDetail
    {
        /// <summary>
        /// Gets or sets the validation type.
        /// </summary>
        /// <value>The validation type.</value>
        public required string ValidationType { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether valid.
        /// </summary>
        /// <value>The is valid.</value>
        public required bool IsValid { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the details.
        /// </summary>
        /// <value>The details.</value>
        public string? Details { get; set; }
    }

    /// <summary>
    /// P2P validation statistics.
    /// </summary>
    public sealed class P2PValidationStatistics
    {
        /// <summary>
        /// Gets or sets the total validations.
        /// </summary>
        /// <value>The total validations.</value>
        public long TotalValidations { get; set; }
        /// <summary>
        /// Gets or sets the successful validations.
        /// </summary>
        /// <value>The successful validations.</value>
        public long SuccessfulValidations { get; set; }
        /// <summary>
        /// Gets or sets the failed validations.
        /// </summary>
        /// <value>The failed validations.</value>
        public long FailedValidations { get; set; }
        /// <summary>
        /// Gets or sets the integrity checks.
        /// </summary>
        /// <value>The integrity checks.</value>
        public long IntegrityChecks { get; set; }
        /// <summary>
        /// Gets or sets the performance validations.
        /// </summary>
        /// <value>The performance validations.</value>
        public long PerformanceValidations { get; set; }
        /// <summary>
        /// Gets or sets the benchmarks executed.
        /// </summary>
        /// <value>The benchmarks executed.</value>
        public long BenchmarksExecuted { get; set; }
        /// <summary>
        /// Gets or sets the total validation time.
        /// </summary>
        /// <value>The total validation time.</value>
        public TimeSpan TotalValidationTime { get; set; }
        /// <summary>
        /// Gets or sets the average validation time.
        /// </summary>
        /// <value>The average validation time.</value>
        public TimeSpan AverageValidationTime { get; set; }
        /// <summary>
        /// Gets or sets the cached benchmark hits.
        /// </summary>
        /// <value>The cached benchmark hits.</value>
        public long CachedBenchmarkHits { get; set; }
        /// <summary>
        /// Gets or sets the validation success rate.
        /// </summary>
        /// <value>The validation success rate.</value>
        public double ValidationSuccessRate { get; set; }
    }

    #endregion

    #region Supporting Types - Benchmarking

    /// <summary>
    /// P2P benchmark options for customizing benchmark execution.
    /// </summary>
    public sealed class P2PBenchmarkOptions
    {
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static P2PBenchmarkOptions Default => new();
        /// <summary>
        /// Gets or sets the min transfer size m b.
        /// </summary>
        /// <value>The min transfer size m b.</value>

        public int MinTransferSizeMB { get; init; } = 1;
        /// <summary>
        /// Gets or sets the max transfer size m b.
        /// </summary>
        /// <value>The max transfer size m b.</value>
        public int MaxTransferSizeMB { get; init; } = 64;
        /// <summary>
        /// Gets or sets the use cached results.
        /// </summary>
        /// <value>The use cached results.</value>
        public bool UseCachedResults { get; init; } = true;
        /// <summary>
        /// Gets or sets the warmup iterations.
        /// </summary>
        /// <value>The warmup iterations.</value>
        public int WarmupIterations { get; init; } = 3;
        /// <summary>
        /// Gets or sets the measurement iterations.
        /// </summary>
        /// <value>The measurement iterations.</value>
        public int MeasurementIterations { get; init; } = 10;
    }

    /// <summary>
    /// P2P benchmark result for a specific device pair.
    /// </summary>
    public sealed class P2PBenchmarkResult
    {
        /// <summary>
        /// Gets or sets the benchmark identifier.
        /// </summary>
        /// <value>The benchmark id.</value>
        public required string BenchmarkId { get; init; }
        /// <summary>
        /// Gets or sets the source device.
        /// </summary>
        /// <value>The source device.</value>
        public required string SourceDevice { get; init; }
        /// <summary>
        /// Gets or sets the target device.
        /// </summary>
        /// <value>The target device.</value>
        public required string TargetDevice { get; init; }
        /// <summary>
        /// Gets or sets the benchmark time.
        /// </summary>
        /// <value>The benchmark time.</value>
        public required DateTimeOffset BenchmarkTime { get; init; }
        /// <summary>
        /// Gets or sets the benchmark options.
        /// </summary>
        /// <value>The benchmark options.</value>
        public required P2PBenchmarkOptions BenchmarkOptions { get; init; }
        /// <summary>
        /// Gets or sets the transfer sizes.
        /// </summary>
        /// <value>The transfer sizes.</value>
        public required IList<P2PTransferBenchmark> TransferSizes { get; init; }
        /// <summary>
        /// Gets or sets the peak throughput g bps.
        /// </summary>
        /// <value>The peak throughput g bps.</value>
        public double PeakThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the average throughput g bps.
        /// </summary>
        /// <value>The average throughput g bps.</value>
        public double AverageThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the minimum latency ms.
        /// </summary>
        /// <value>The minimum latency ms.</value>
        public double MinimumLatencyMs { get; set; }
        /// <summary>
        /// Gets or sets the average latency ms.
        /// </summary>
        /// <value>The average latency ms.</value>
        public double AverageLatencyMs { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public required bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Individual transfer size benchmark result.
    /// </summary>
    public sealed class P2PTransferBenchmark
    {
        /// <summary>
        /// Gets or sets the transfer size bytes.
        /// </summary>
        /// <value>The transfer size bytes.</value>
        public required long TransferSizeBytes { get; init; }
        /// <summary>
        /// Gets or sets the throughput g bps.
        /// </summary>
        /// <value>The throughput g bps.</value>
        public required double ThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the peak throughput g bps.
        /// </summary>
        /// <value>The peak throughput g bps.</value>
        public required double PeakThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the min throughput g bps.
        /// </summary>
        /// <value>The min throughput g bps.</value>
        public required double MinThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the latency ms.
        /// </summary>
        /// <value>The latency ms.</value>
        public required double LatencyMs { get; init; }
        /// <summary>
        /// Gets or sets the measurements.
        /// </summary>
        /// <value>The measurements.</value>
        public required IList<double> Measurements { get; init; }
        /// <summary>
        /// Gets or sets the latency measurements.
        /// </summary>
        /// <value>The latency measurements.</value>
        public required IList<double> LatencyMeasurements { get; init; }
    }

    /// <summary>
    /// Multi-GPU benchmark options.
    /// </summary>
    public sealed class P2PMultiGpuBenchmarkOptions
    {
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static P2PMultiGpuBenchmarkOptions Default => new();
        /// <summary>
        /// Gets or sets the enable pairwise benchmarks.
        /// </summary>
        /// <value>The enable pairwise benchmarks.</value>

        public bool EnablePairwiseBenchmarks { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable scatter benchmarks.
        /// </summary>
        /// <value>The enable scatter benchmarks.</value>
        public bool EnableScatterBenchmarks { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable gather benchmarks.
        /// </summary>
        /// <value>The enable gather benchmarks.</value>
        public bool EnableGatherBenchmarks { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable all to all benchmarks.
        /// </summary>
        /// <value>The enable all to all benchmarks.</value>
        public bool EnableAllToAllBenchmarks { get; init; } = true;
        /// <summary>
        /// Gets or sets the pairwise transfer size m b.
        /// </summary>
        /// <value>The pairwise transfer size m b.</value>
        public int PairwiseTransferSizeMB { get; init; } = 64;
        /// <summary>
        /// Gets or sets the scatter transfer size m b.
        /// </summary>
        /// <value>The scatter transfer size m b.</value>
        public int ScatterTransferSizeMB { get; init; } = 32;
        /// <summary>
        /// Gets or sets the gather transfer size m b.
        /// </summary>
        /// <value>The gather transfer size m b.</value>
        public int GatherTransferSizeMB { get; init; } = 32;
        /// <summary>
        /// Gets or sets the all to all transfer size m b.
        /// </summary>
        /// <value>The all to all transfer size m b.</value>
        public int AllToAllTransferSizeMB { get; init; } = 16;
        /// <summary>
        /// Gets or sets the use cached results.
        /// </summary>
        /// <value>The use cached results.</value>
        public bool UseCachedResults { get; init; } = true;
    }

    /// <summary>
    /// Multi-GPU benchmark result.
    /// </summary>
    public sealed class P2PMultiGpuBenchmarkResult
    {
        /// <summary>
        /// Gets or sets the benchmark identifier.
        /// </summary>
        /// <value>The benchmark id.</value>
        public required string BenchmarkId { get; init; }
        /// <summary>
        /// Gets or sets the benchmark time.
        /// </summary>
        /// <value>The benchmark time.</value>
        public required DateTimeOffset BenchmarkTime { get; init; }
        /// <summary>
        /// Gets or sets the device count.
        /// </summary>
        /// <value>The device count.</value>
        public required int DeviceCount { get; init; }
        /// <summary>
        /// Gets or sets the pairwise benchmarks.
        /// </summary>
        /// <value>The pairwise benchmarks.</value>
        public required IList<P2PBenchmarkResult> PairwiseBenchmarks { get; init; }
        /// <summary>
        /// Gets or sets the scatter benchmarks.
        /// </summary>
        /// <value>The scatter benchmarks.</value>
        public required IList<P2PScatterBenchmarkResult> ScatterBenchmarks { get; init; }
        /// <summary>
        /// Gets or sets the gather benchmarks.
        /// </summary>
        /// <value>The gather benchmarks.</value>
        public required IList<P2PGatherBenchmarkResult> GatherBenchmarks { get; init; }
        /// <summary>
        /// Gets or sets the all to all benchmarks.
        /// </summary>
        /// <value>The all to all benchmarks.</value>
        public required IList<P2PAllToAllBenchmarkResult> AllToAllBenchmarks { get; init; }
        /// <summary>
        /// Gets or sets the peak pairwise throughput g bps.
        /// </summary>
        /// <value>The peak pairwise throughput g bps.</value>
        public double PeakPairwiseThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the average pairwise throughput g bps.
        /// </summary>
        /// <value>The average pairwise throughput g bps.</value>
        public double AveragePairwiseThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the total benchmark time ms.
        /// </summary>
        /// <value>The total benchmark time ms.</value>
        public double TotalBenchmarkTimeMs { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public required bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// P2P scatter benchmark result.
    /// </summary>
    public sealed class P2PScatterBenchmarkResult
    {
        /// <summary>
        /// Gets or sets the scatter pattern.
        /// </summary>
        /// <value>The scatter pattern.</value>
        public required string ScatterPattern { get; init; }
        /// <summary>
        /// Gets or sets the total throughput g bps.
        /// </summary>
        /// <value>The total throughput g bps.</value>
        public required double TotalThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the average latency ms.
        /// </summary>
        /// <value>The average latency ms.</value>
        public required double AverageLatencyMs { get; init; }
    }

    /// <summary>
    /// P2P gather benchmark result.
    /// </summary>
    public sealed class P2PGatherBenchmarkResult
    {
        /// <summary>
        /// Gets or sets the gather pattern.
        /// </summary>
        /// <value>The gather pattern.</value>
        public required string GatherPattern { get; init; }
        /// <summary>
        /// Gets or sets the total throughput g bps.
        /// </summary>
        /// <value>The total throughput g bps.</value>
        public required double TotalThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the average latency ms.
        /// </summary>
        /// <value>The average latency ms.</value>
        public required double AverageLatencyMs { get; init; }
    }

    /// <summary>
    /// P2P all-to-all benchmark result.
    /// </summary>
    public sealed class P2PAllToAllBenchmarkResult
    {
        /// <summary>
        /// Gets or sets the communication pattern.
        /// </summary>
        /// <value>The communication pattern.</value>
        public required string CommunicationPattern { get; init; }
        /// <summary>
        /// Gets or sets the aggregate throughput g bps.
        /// </summary>
        /// <value>The aggregate throughput g bps.</value>
        public required double AggregateThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the max latency ms.
        /// </summary>
        /// <value>The max latency ms.</value>
        public required double MaxLatencyMs { get; init; }
    }

    #endregion

    #region Mock Buffer Implementation

    /// <summary>
    /// Mock buffer implementation for testing purposes.
    /// </summary>
    internal sealed class MockBuffer<T>(IAccelerator accelerator, int length) : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the length.
        /// </summary>
        /// <value>The length.</value>
        public int Length { get; } = length;
        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        /// <value>The size in bytes.</value>
        public long SizeInBytes { get; } = length * Unsafe.SizeOf<T>();
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public IAccelerator Accelerator { get; } = accelerator;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public MemoryOptions Options => MemoryOptions.None;
        /// <summary>
        /// Gets or sets a value indicating whether disposed.
        /// </summary>
        /// <value>The is disposed.</value>
        public bool IsDisposed => false;
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public BufferState State { get; set; } = BufferState.HostReady;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public bool IsOnHost => State is BufferState.HostReady or BufferState.HostDirty;
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public bool IsOnDevice => State is BufferState.DeviceReady or BufferState.DeviceDirty;
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public bool IsDirty => State is BufferState.HostDirty or BufferState.DeviceDirty;
        /// <summary>
        /// Gets copy from host asynchronously.
        /// </summary>
        /// <typeparam name="TData">The TData type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public static ValueTask CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from host asynchronously.
        /// </summary>
        /// <typeparam name="TData">The TData type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public static ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to host asynchronously.
        /// </summary>
        /// <typeparam name="TData">The TData type parameter.</typeparam>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public static Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => Task.CompletedTask;
        /// <summary>
        /// Gets copy to host asynchronously.
        /// </summary>
        /// <typeparam name="TData">The TData type parameter.</typeparam>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public static ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets clear asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public static Task ClearAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <returns>The result of the operation.</returns>

        public IUnifiedMemoryBuffer<T> Slice(int offset, int count)
            => new MockBuffer<T>(Accelerator, count);
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>

        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
            => new MockBuffer<TNew>(Accelerator, (int)(SizeInBytes / Unsafe.SizeOf<TNew>()));
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // Memory access methods
        public Span<T> AsSpan() => [];
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public ReadOnlySpan<T> AsReadOnlySpan() => [];
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public Memory<T> AsMemory() => Memory<T>.Empty;
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>
        public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
        /// <summary>
        /// Performs ensure on host.
        /// </summary>

        // Synchronization methods
        public void EnsureOnHost() => State = BufferState.HostReady;
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public void EnsureOnDevice() => State = BufferState.DeviceReady;
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnHost();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnDevice();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public void Synchronize() { }
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public void MarkHostDirty() => State = BufferState.HostDirty;
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public void MarkDeviceDirty() => State = BufferState.DeviceDirty;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        // Copy methods
        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>

        public MappedMemory<T> Map(MapMode mode) => MappedMemory<T>.Invalid;
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public MappedMemory<T> MapRange(int offset, int count, MapMode mode) => MappedMemory<T>.Invalid;
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
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
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose() { }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion
}
