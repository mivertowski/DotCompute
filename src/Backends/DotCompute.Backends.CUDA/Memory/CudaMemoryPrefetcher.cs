// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using CudaMemoryAdvice = DotCompute.Backends.CUDA.Types.Native.CudaMemoryAdvise;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Manages memory prefetching for unified memory to optimize data movement.
    /// Uses cudaMemPrefetchAsync to proactively move data between host and device.
    /// </summary>
    public sealed partial class CudaMemoryPrefetcher : IDisposable
    {
        #region LoggerMessage Delegates (Event IDs 5600-5649)

        // LoggerMessage delegates moved to CudaMemoryPrefetcher.LoggerMessages.cs

        [LoggerMessage(EventId = 5605, Level = LogLevel.Debug, Message = "Batch prefetch: {SuccessCount}/{TotalCount} successful")]
        private static partial void LogBatchPrefetchResult(ILogger logger, int successCount, int totalCount);

        [LoggerMessage(EventId = 5606, Level = LogLevel.Warning, Message = "Failed to synchronize prefetch stream")]
        private static partial void LogFailedToSynchronizePrefetchStream(ILogger logger);

        [LoggerMessage(EventId = 5607, Level = LogLevel.Warning, Message = "Failed to destroy prefetch stream")]
        private static partial void LogFailedToDestroyPrefetchStream(ILogger logger);

        [LoggerMessage(EventId = 5608, Level = LogLevel.Information, Message = "Disposed memory prefetcher. Total prefetched: {TotalBytes} bytes in {OperationCount} operations")]
        private static partial void LogDisposedMemoryPrefetcher(ILogger logger, long totalBytes, long operationCount);

        #endregion

        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, PrefetchInfo> _activePrefetches;
        private readonly SemaphoreSlim _prefetchSemaphore;
        private IntPtr _prefetchStream;
        private bool _supportsPrefetch;
        private bool _disposed;

        // Performance counters
        private long _totalPrefetchedBytes;
        private long _prefetchCount;
        private long _prefetchHits;
        private long _prefetchMisses;
        /// <summary>
        /// Initializes a new instance of the CudaMemoryPrefetcher class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="device">The device.</param>
        /// <param name="logger">The logger.</param>

        public CudaMemoryPrefetcher(CudaContext context, CudaDevice device, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activePrefetches = new ConcurrentDictionary<IntPtr, PrefetchInfo>();
            _prefetchSemaphore = new SemaphoreSlim(1, 1);


            Initialize();
        }

        /// <summary>
        /// Gets whether the device supports memory prefetching.
        /// </summary>
        public bool SupportsPrefetch => _supportsPrefetch;

        /// <summary>
        /// Gets the total number of bytes prefetched.
        /// </summary>
        public long TotalPrefetchedBytes => _totalPrefetchedBytes;

        /// <summary>
        /// Gets the total number of prefetch operations.
        /// </summary>
        public long PrefetchCount => _prefetchCount;

        /// <summary>
        /// Gets the prefetch hit rate.
        /// </summary>
        public double PrefetchHitRate => _prefetchCount > 0 ? (double)_prefetchHits / _prefetchCount : 0;

        private void Initialize()
        {
            // Check if device supports unified memory and prefetching
            _supportsPrefetch = CheckPrefetchSupport();


            if (_supportsPrefetch)
            {
                // Create a dedicated stream for prefetch operations
                var result = CudaRuntime.cudaStreamCreate(ref _prefetchStream);
                if (result != CudaError.Success)
                {
                    LogCreatePrefetchStreamFailed(new InvalidOperationException($"Failed with error: {result}"));
                    _supportsPrefetch = false;
                }
                else
                {
                    LogPrefetchEnabled();
                }
            }
            else
            {
                LogPrefetchNotSupported();
            }
        }

        private bool CheckPrefetchSupport()
        {
            try
            {
                // Check for unified memory support
                var supportsManaged = 0;
                var result = CudaRuntime.cudaDeviceGetAttribute(
                    ref supportsManaged,

                    CudaDeviceAttribute.ManagedMemory,

                    _device.DeviceId);


                if (result != CudaError.Success || supportsManaged == 0)
                {
                    return false;
                }

                // Check for concurrent managed access
                var supportsConcurrent = 0;
                result = CudaRuntime.cudaDeviceGetAttribute(
                    ref supportsConcurrent,
                    CudaDeviceAttribute.ConcurrentManagedAccess,
                    _device.DeviceId);


                return result == CudaError.Success && supportsConcurrent != 0;
            }
            catch (Exception ex)
            {
                LogPrefetchSupportCheckError(ex);
                return false;
            }
        }

        /// <summary>
        /// Prefetches memory to the specified device asynchronously.
        /// </summary>
        public async Task<bool> PrefetchToDeviceAsync(
            IntPtr ptr,
            long sizeInBytes,
            int deviceId = -1,
            IntPtr stream = default,
            CancellationToken cancellationToken = default)
        {
            if (!_supportsPrefetch)
            {
                LogPrefetchSkipped();
                return false;
            }

            ObjectDisposedException.ThrowIf(_disposed, this);

            if (deviceId < 0)
            {
                deviceId = _device.DeviceId;
            }


            if (stream == IntPtr.Zero)
            {
                stream = _prefetchStream;
            }


            await _prefetchSemaphore.WaitAsync(cancellationToken);
            try
            {
                var result = CudaRuntime.cudaMemPrefetch(ptr, (nuint)sizeInBytes, deviceId, stream);


                if (result == CudaError.Success)
                {
                    var info = new PrefetchInfo(ptr, sizeInBytes, deviceId, PrefetchTarget.Device);
                    _activePrefetches[ptr] = info;

                    _ = Interlocked.Add(ref _totalPrefetchedBytes, sizeInBytes);
                    _ = Interlocked.Increment(ref _prefetchCount);

                    LogPrefetchedToDevice(sizeInBytes, deviceId);
                    return true;
                }
                else if (result == CudaError.InvalidValue)
                {
                    // Memory not managed, prefetch not applicable
                    LogNotManagedMemory(ptr);
                    return false;
                }
                else
                {
                    LogPrefetchMemoryFailed(new InvalidOperationException($"Prefetch failed with error: {result}"));
                    return false;
                }
            }
            finally
            {
                _ = _prefetchSemaphore.Release();
            }
        }

        /// <summary>
        /// Prefetches memory to the host CPU asynchronously.
        /// </summary>
        public async Task<bool> PrefetchToHostAsync(
            IntPtr ptr,
            long sizeInBytes,
            IntPtr stream = default,
            CancellationToken cancellationToken = default)
        {
            if (!_supportsPrefetch)
            {
                LogPrefetchSkipped();
                return false;
            }

            ObjectDisposedException.ThrowIf(_disposed, this);

            if (stream == IntPtr.Zero)
            {
                stream = _prefetchStream;
            }


            await _prefetchSemaphore.WaitAsync(cancellationToken);
            try
            {
                // CPU is specified as device -1 in CUDA
                const int cpuDevice = -1;
                var result = CudaRuntime.cudaMemPrefetch(ptr, (nuint)sizeInBytes, cpuDevice, stream);


                if (result == CudaError.Success)
                {
                    var info = new PrefetchInfo(ptr, sizeInBytes, cpuDevice, PrefetchTarget.Host);
                    _activePrefetches[ptr] = info;

                    _ = Interlocked.Add(ref _totalPrefetchedBytes, sizeInBytes);
                    _ = Interlocked.Increment(ref _prefetchCount);

                    LogPrefetchedToHost(sizeInBytes);
                    return true;
                }
                else
                {
                    LogPrefetchMemoryFailed(new InvalidOperationException($"Prefetch failed with error: {result}"));
                    return false;
                }
            }
            finally
            {
                _ = _prefetchSemaphore.Release();
            }
        }

        /// <summary>
        /// Advises CUDA about the expected access pattern for memory.
        /// </summary>
        public async Task<bool> AdviseMemoryAsync(
            IntPtr devicePointer,
            long sizeInBytes,
            CudaMemoryAdvice advice,
            int deviceId = -1,
            CancellationToken cancellationToken = default)
        {
            if (!_supportsPrefetch)
            {
                LogMemoryAdviceSkipped();
                return false;
            }

            ObjectDisposedException.ThrowIf(_disposed, this);

            if (deviceId < 0)
            {
                deviceId = _device.DeviceId;
            }


            return await Task.Run(() =>
            {
                var result = CudaRuntime.cudaMemAdvise(devicePointer, (nuint)sizeInBytes, (CudaMemoryAdvice)advice, deviceId);


                if (result == CudaError.Success)
                {
                    LogMemoryAdviceSet(advice, sizeInBytes, devicePointer);
                    return true;
                }
                else
                {
                    LogAdviseMemoryFailed(new InvalidOperationException($"Advise failed with error: {result}"));
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Prefetches multiple memory regions in batch.
        /// </summary>
        public async Task<int> BatchPrefetchAsync(
            PrefetchRequest[] requests,
            CancellationToken cancellationToken = default)
        {
            if (!_supportsPrefetch || requests == null || requests.Length == 0)
            {

                return 0;
            }


            ObjectDisposedException.ThrowIf(_disposed, this);

            var successCount = 0;
            var tasks = new Task<bool>[requests.Length];

            for (var i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                tasks[i] = request.Target == PrefetchTarget.Device
                    ? PrefetchToDeviceAsync(request.Pointer, request.Size, request.DeviceId, IntPtr.Zero, cancellationToken)
                    : PrefetchToHostAsync(request.Pointer, request.Size, IntPtr.Zero, cancellationToken);
            }

            var results = await Task.WhenAll(tasks);


            foreach (var success in results)
            {
                if (success)
                {
                    successCount++;
                }
            }

            LogBatchPrefetchResult(_logger, successCount, requests.Length);
            return successCount;
        }

        /// <summary>
        /// Waits for all pending prefetch operations to complete.
        /// </summary>
        public async Task WaitForPrefetchesAsync(CancellationToken cancellationToken = default)
        {
            if (!_supportsPrefetch || _prefetchStream == IntPtr.Zero)
            {
                return;
            }


            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaStreamSynchronize(_prefetchStream);
                if (result != CudaError.Success)
                {
                    LogFailedToSynchronizePrefetchStream(_logger);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Records a prefetch hit (data was used as expected).
        /// </summary>
        public void RecordPrefetchHit(IntPtr devicePointer)
        {
            if (_activePrefetches.ContainsKey(devicePointer))
            {
                _ = Interlocked.Increment(ref _prefetchHits);
                _ = _activePrefetches.TryRemove(devicePointer, out _);
            }
        }

        /// <summary>
        /// Records a prefetch miss (data was not where expected).
        /// </summary>
        public void RecordPrefetchMiss(IntPtr devicePointer) => _ = Interlocked.Increment(ref _prefetchMisses);

        /// <summary>
        /// Gets prefetch statistics.
        /// </summary>
        public PrefetchStatistics GetStatistics()
        {
            return new PrefetchStatistics
            {
                TotalPrefetchedBytes = _totalPrefetchedBytes,
                PrefetchCount = _prefetchCount,
                PrefetchHits = _prefetchHits,
                PrefetchMisses = _prefetchMisses,
                HitRate = PrefetchHitRate,
                ActivePrefetches = _activePrefetches.Count
            };
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            if (_prefetchStream != IntPtr.Zero)
            {
                var result = CudaRuntime.cudaStreamDestroy(_prefetchStream);
                if (result != CudaError.Success)
                {
                    LogFailedToDestroyPrefetchStream(_logger);
                }
            }

            _activePrefetches.Clear();
            _prefetchSemaphore?.Dispose();
            _disposed = true;

            LogDisposedMemoryPrefetcher(_logger, _totalPrefetchedBytes, _prefetchCount);
        }
        /// <summary>
        /// A class that represents prefetch info.
        /// </summary>

        private sealed class PrefetchInfo(IntPtr pointer, long size, int deviceId, PrefetchTarget target)
        {
            /// <summary>
            /// Gets or sets the pointer.
            /// </summary>
            /// <value>The pointer.</value>
            public IntPtr Pointer { get; } = pointer;
            /// <summary>
            /// Gets or sets the size.
            /// </summary>
            /// <value>The size.</value>
            public long Size { get; } = size;
            /// <summary>
            /// Gets or sets the device identifier.
            /// </summary>
            /// <value>The device id.</value>
            public int DeviceId { get; } = deviceId;
            /// <summary>
            /// Gets or sets the target.
            /// </summary>
            /// <value>The target.</value>
            public PrefetchTarget Target { get; } = target;
            /// <summary>
            /// Gets or sets the timestamp.
            /// </summary>
            /// <value>The timestamp.</value>
            public DateTime Timestamp { get; } = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Target location for prefetch operation.
    /// </summary>
    public enum PrefetchTarget
    {
        /// <summary>
        /// Prefetch to host CPU.
        /// </summary>
        Host,

        /// <summary>
        /// Prefetch to device GPU.
        /// </summary>
        Device
    }


    /// <summary>
    /// Request for batch prefetch operation.
    /// </summary>
    public sealed class PrefetchRequest
    {
        /// <summary>
        /// Gets or sets the pointer.
        /// </summary>
        /// <value>The pointer.</value>
        public IntPtr Pointer { get; init; }
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public long Size { get; init; }
        /// <summary>
        /// Gets or sets the target.
        /// </summary>
        /// <value>The target.</value>
        public PrefetchTarget Target { get; init; }
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>
        public int DeviceId { get; init; } = -1;
    }

    /// <summary>
    /// Statistics for prefetch operations.
    /// </summary>
    public sealed class PrefetchStatistics
    {
        /// <summary>
        /// Gets or sets the total prefetched bytes.
        /// </summary>
        /// <value>The total prefetched bytes.</value>
        public long TotalPrefetchedBytes { get; init; }
        /// <summary>
        /// Gets or sets the prefetch count.
        /// </summary>
        /// <value>The prefetch count.</value>
        public long PrefetchCount { get; init; }
        /// <summary>
        /// Gets or sets the prefetch hits.
        /// </summary>
        /// <value>The prefetch hits.</value>
        public long PrefetchHits { get; init; }
        /// <summary>
        /// Gets or sets the prefetch misses.
        /// </summary>
        /// <value>The prefetch misses.</value>
        public long PrefetchMisses { get; init; }
        /// <summary>
        /// Gets or sets the hit rate.
        /// </summary>
        /// <value>The hit rate.</value>
        public double HitRate { get; init; }
        /// <summary>
        /// Gets or sets the active prefetches.
        /// </summary>
        /// <value>The active prefetches.</value>
        public int ActivePrefetches { get; init; }
    }
}