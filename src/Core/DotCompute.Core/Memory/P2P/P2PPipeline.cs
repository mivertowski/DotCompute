// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// High-performance P2P pipeline that implements overlapped transfers
    /// with asynchronous staging and optimal bandwidth utilization.
    /// </summary>
    internal sealed class P2PPipeline<T> : IAsyncDisposable where T : unmanaged
    {
        private readonly IBuffer<T> _sourceBuffer;
        private readonly IBuffer<T> _destinationBuffer;
        private readonly int _chunkSize;
        private readonly int _pipelineDepth;
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<P2PPipelineStage> _availableStages;
        private readonly SemaphoreSlim _pipelineSemaphore;
        private readonly CancellationTokenSource _pipelineCts;
        private bool _disposed;

        // Pipeline configuration
        private const int DefaultStageBufferSize = 4 * 1024 * 1024; // 4MB per stage
        private const int MaxPipelineDepth = 8;

        public P2PPipeline(
            IBuffer<T> sourceBuffer,
            IBuffer<T> destinationBuffer,
            int chunkSize,
            int pipelineDepth,
            ILogger logger)
        {
            _sourceBuffer = sourceBuffer ?? throw new ArgumentNullException(nameof(sourceBuffer));
            _destinationBuffer = destinationBuffer ?? throw new ArgumentNullException(nameof(destinationBuffer));
            _chunkSize = Math.Max(chunkSize, 1024); // Minimum 1KB chunks
            _pipelineDepth = Math.Min(Math.Max(pipelineDepth, 1), MaxPipelineDepth);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _availableStages = new ConcurrentQueue<P2PPipelineStage>();
            _pipelineSemaphore = new SemaphoreSlim(_pipelineDepth, _pipelineDepth);
            _pipelineCts = new CancellationTokenSource();

            InitializePipelineStages();
        }

        /// <summary>
        /// Executes the pipelined P2P transfer with overlapped operations.
        /// </summary>
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pipelineCts.Token);
            var token = combinedCts.Token;

            _logger.LogDebug("Starting pipelined P2P transfer: {TotalSize} bytes, {ChunkSize} byte chunks, {PipelineDepth} depth",
                _sourceBuffer.SizeInBytes, _chunkSize, _pipelineDepth);

            var elementSize = Unsafe.SizeOf<T>();
            var elementsPerChunk = _chunkSize / elementSize;
            var totalElements = _sourceBuffer.Length;
            var totalChunks = (totalElements + elementsPerChunk - 1) / elementsPerChunk;

            var transferTasks = new List<Task>();
            var completedChunks = 0;
            var startTime = DateTimeOffset.UtcNow;

            try
            {
                // Process all chunks with pipeline overlap
                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    token.ThrowIfCancellationRequested();

                    var startElement = chunkIndex * elementsPerChunk;
                    var chunkElements = Math.Min(elementsPerChunk, totalElements - startElement);

                    // Wait for available pipeline stage
                    await _pipelineSemaphore.WaitAsync(token);

                    // Start asynchronous chunk transfer
                    var transferTask = TransferChunkAsync(startElement, chunkElements, chunkIndex, token)
                        .ContinueWith(t =>
                        {
                            _pipelineSemaphore.Release();
                            var completed = Interlocked.Increment(ref completedChunks);
                            
                            if (completed % Math.Max(1, totalChunks / 10) == 0)
                            {
                                var progress = (double)completed / totalChunks * 100.0;
                                _logger.LogTrace("Pipeline progress: {Progress:F1}% ({Completed}/{Total} chunks)",
                                    progress, completed, totalChunks);
                            }

                            if (t.IsFaulted && t.Exception != null)
                            {
                                _logger.LogError(t.Exception, "Pipeline chunk transfer failed: chunk {ChunkIndex}", chunkIndex);
                            }

                            return t;
                        }, TaskScheduler.Default).Unwrap();

                    transferTasks.Add(transferTask);

                    // Limit concurrent tasks to prevent memory pressure
                    if (transferTasks.Count >= _pipelineDepth * 2)
                    {
                        var completedTask = await Task.WhenAny(transferTasks);
                        transferTasks.Remove(completedTask);
                        await completedTask; // Propagate any exceptions
                    }
                }

                // Wait for all remaining transfers to complete
                if (transferTasks.Count > 0)
                {
                    await Task.WhenAll(transferTasks);
                }

                var totalDuration = DateTimeOffset.UtcNow - startTime;
                var throughputGBps = (_sourceBuffer.SizeInBytes / (1024.0 * 1024.0 * 1024.0)) / totalDuration.TotalSeconds;

                _logger.LogDebug("Pipelined P2P transfer completed: {Duration:F1}ms, {ThroughputGBps:F2} GB/s, {ChunkCount} chunks",
                    totalDuration.TotalMilliseconds, throughputGBps, totalChunks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Pipelined P2P transfer cancelled by user");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Pipelined P2P transfer cancelled internally");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipelined P2P transfer failed");
                throw;
            }
        }

        /// <summary>
        /// Transfers a single chunk through the pipeline with staging.
        /// </summary>
        private async Task TransferChunkAsync(
            int startElement,
            int elementCount,
            int chunkIndex,
            CancellationToken cancellationToken)
        {
            P2PPipelineStage? stage = null;
            try
            {
                // Get available pipeline stage
                if (!_availableStages.TryDequeue(out stage))
                {
                    // Create temporary stage if none available (shouldn't happen with proper semaphore)
                    stage = new P2PPipelineStage
                    {
                        StageId = chunkIndex,
                        StagingBuffer = new T[elementCount],
                        IsInUse = true
                    };
                }

                stage.IsInUse = true;
                stage.ChunkIndex = chunkIndex;

                // Stage 1: Copy from source to staging buffer (overlapped read)
                await CopyToStagingAsync(stage, startElement, elementCount, cancellationToken);

                // Stage 2: Copy from staging buffer to destination (overlapped write)
                await CopyFromStagingAsync(stage, startElement, elementCount, cancellationToken);
            }
            finally
            {
                // Return stage to available pool
                if (stage != null)
                {
                    stage.IsInUse = false;
                    stage.ChunkIndex = -1;
                    _availableStages.Enqueue(stage);
                }
            }
        }

        /// <summary>
        /// Copies data from source buffer to pipeline staging buffer.
        /// </summary>
        private async Task CopyToStagingAsync(
            P2PPipelineStage stage,
            int startElement,
            int elementCount,
            CancellationToken cancellationToken)
        {
            try
            {
                // Create source slice for the chunk
                var sourceSlice = _sourceBuffer.Slice(startElement, elementCount);
                
                // Copy to host staging buffer
                await sourceSlice.CopyToHostAsync(stage.StagingBuffer.AsMemory(0, elementCount), 0, cancellationToken);
                
                stage.StagingCompleted = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy chunk {ChunkIndex} to staging buffer", stage.ChunkIndex);
                throw;
            }
        }

        /// <summary>
        /// Copies data from pipeline staging buffer to destination buffer.
        /// </summary>
        private async Task CopyFromStagingAsync(
            P2PPipelineStage stage,
            int startElement,
            int elementCount,
            CancellationToken cancellationToken)
        {
            try
            {
                // Create destination slice for the chunk
                var destinationSlice = _destinationBuffer.Slice(startElement, elementCount);
                
                // Copy from host staging buffer to destination
                await destinationSlice.CopyFromHostAsync<T>(stage.StagingBuffer.AsMemory(0, elementCount), 0, cancellationToken);
                
                stage.TransferCompleted = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy chunk {ChunkIndex} from staging buffer", stage.ChunkIndex);
                throw;
            }
        }

        /// <summary>
        /// Initializes the pipeline stages with staging buffers.
        /// </summary>
        private void InitializePipelineStages()
        {
            var elementSize = Unsafe.SizeOf<T>();
            var elementsPerChunk = _chunkSize / elementSize;

            for (int i = 0; i < _pipelineDepth; i++)
            {
                var stage = new P2PPipelineStage
                {
                    StageId = i,
                    StagingBuffer = new T[elementsPerChunk],
                    IsInUse = false,
                    ChunkIndex = -1
                };

                _availableStages.Enqueue(stage);
            }

            _logger.LogTrace("Initialized {StageCount} pipeline stages with {ElementsPerChunk} elements each",
                _pipelineDepth, elementsPerChunk);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            // Cancel any ongoing operations
            await _pipelineCts.CancelAsync();

            // Wait a short time for cleanup
            try
            {
                await Task.Delay(100);
            }
            catch
            {
                // Ignore cleanup delays
            }

            _pipelineSemaphore.Dispose();
            _pipelineCts.Dispose();

            // Clear staging buffers
            while (_availableStages.TryDequeue(out var stage))
            {
                // Staging buffers will be garbage collected
            }

            _logger.LogTrace("P2P Pipeline disposed");
        }

        /// <summary>
        /// Pipeline stage with staging buffer and state tracking.
        /// </summary>
        internal sealed class P2PPipelineStage
        {
            public int StageId { get; init; }
            public required T[] StagingBuffer { get; init; }
            public bool IsInUse { get; set; }
            public int ChunkIndex { get; set; }
            public DateTimeOffset? StagingCompleted { get; set; }
            public DateTimeOffset? TransferCompleted { get; set; }
        }
    }
}