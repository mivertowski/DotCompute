// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Resilience
{
    /// <summary>
    /// Manages and preserves CUDA context state for recovery operations.
    /// Tracks all allocated resources and provides state snapshots for recovery.
    /// </summary>
    public sealed partial class CudaContextStateManager(ILogger logger) : IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        #region LoggerMessage Delegates (Event IDs 5550-5599)

        [LoggerMessage(EventId = 5550, Level = LogLevel.Debug, Message = "Registered memory allocation: {Pointer} ({Size} bytes, {Type})")]
        private static partial void LogRegisteredMemoryAllocation(ILogger logger, long pointer, ulong size, MemoryType type);

        [LoggerMessage(EventId = 5551, Level = LogLevel.Debug, Message = "Unregistered memory allocation: {Pointer} ({Size} bytes)")]
        private static partial void LogUnregisteredMemoryAllocation(ILogger logger, long pointer, ulong size);

        [LoggerMessage(EventId = 5552, Level = LogLevel.Debug, Message = "Registered CUDA stream: {StreamPointer} (Priority: {Priority})")]
        private static partial void LogRegisteredStream(ILogger logger, long streamPointer, StreamPriority priority);

        [LoggerMessage(EventId = 5553, Level = LogLevel.Debug, Message = "Unregistered CUDA stream: {StreamPointer}")]
        private static partial void LogUnregisteredStream(ILogger logger, long streamPointer);

        [LoggerMessage(EventId = 5554, Level = LogLevel.Debug, Message = "Registered kernel: {KernelName} (PTX: {PtxSize} bytes, CUBIN: {CubinSize} bytes)")]
        private static partial void LogRegisteredKernel(ILogger logger, string kernelName, int ptxSize, int cubinSize);

        [LoggerMessage(EventId = 5555, Level = LogLevel.Information, Message = "Created context snapshot with {StreamCount} streams, {KernelCount} kernels, {AllocationCount} allocations")]
        private static partial void LogCreatedContextSnapshot(ILogger logger, int streamCount, int kernelCount, int allocationCount);

        [LoggerMessage(EventId = 5556, Level = LogLevel.Warning, Message = "Preparing context for recovery - cleaning up resources")]
        private static partial void LogPreparingForRecovery(ILogger logger);

        [LoggerMessage(EventId = 5557, Level = LogLevel.Warning, Message = "Failed to synchronize stream {StreamPointer}: {Result}")]
        private static partial void LogFailedToSynchronizeStream(ILogger logger, long streamPointer, CudaError result);

        [LoggerMessage(EventId = 5558, Level = LogLevel.Warning, Message = "Failed to destroy stream {StreamPointer}: {Result}")]
        private static partial void LogFailedToDestroyStream(ILogger logger, long streamPointer, CudaError result);

        [LoggerMessage(EventId = 5559, Level = LogLevel.Information, Message = "Context prepared for recovery - resources cleaned up")]
        private static partial void LogContextPreparedForRecovery(ILogger logger);

        [LoggerMessage(EventId = 5560, Level = LogLevel.Information, Message = "Restoring context from snapshot {SnapshotId}")]
        private static partial void LogRestoringContextFromSnapshot(ILogger logger, Guid snapshotId);

        [LoggerMessage(EventId = 5561, Level = LogLevel.Warning, Message = "Context restoration completed with errors: {Errors}")]
        private static partial void LogContextRestorationErrors(ILogger logger, string errors);

        [LoggerMessage(EventId = 5562, Level = LogLevel.Information, Message = "Context successfully restored from snapshot")]
        private static partial void LogContextSuccessfullyRestored(ILogger logger);

        [LoggerMessage(EventId = 5563, Level = LogLevel.Warning, Message = "Performing progressive recovery for error {Error}, attempt {AttemptNumber}")]
        private static partial void LogPerformingProgressiveRecovery(ILogger logger, CudaError error, int attemptNumber);

        [LoggerMessage(EventId = 5564, Level = LogLevel.Information, Message = "Attempting recovery with stream synchronization")]
        private static partial void LogAttemptingStreamSyncRecovery(ILogger logger);

        [LoggerMessage(EventId = 5565, Level = LogLevel.Warning, Message = "Stream sync failed: {Result}")]
        private static partial void LogStreamSyncFailed(ILogger logger, CudaError result);

        [LoggerMessage(EventId = 5566, Level = LogLevel.Information, Message = "Attempting recovery with memory cleanup")]
        private static partial void LogAttemptingMemoryCleanupRecovery(ILogger logger);

        [LoggerMessage(EventId = 5567, Level = LogLevel.Information, Message = "Attempting recovery with context reset")]
        private static partial void LogAttemptingContextResetRecovery(ILogger logger);

        [LoggerMessage(EventId = 5568, Level = LogLevel.Information, Message = "Attempting recovery with device reset")]
        private static partial void LogAttemptingDeviceResetRecovery(ILogger logger);

        [LoggerMessage(EventId = 5569, Level = LogLevel.Information, Message = "Context state manager disposed. Recovery count: {RecoveryCount}")]
        private static partial void LogContextStateManagerDisposed(ILogger logger, int recoveryCount);

        [LoggerMessage(EventId = 6150, Level = LogLevel.Warning, Message = "Error synchronizing stream during recovery preparation")]
        private partial void LogErrorSynchronizingStream(Exception ex);

        [LoggerMessage(EventId = 6151, Level = LogLevel.Warning, Message = "Error freeing memory {Ptr:X} during recovery preparation")]
        private partial void LogErrorFreeingMemory(Exception ex, long ptr);

        [LoggerMessage(EventId = 6152, Level = LogLevel.Warning, Message = "Error destroying stream during recovery preparation")]
        private partial void LogErrorDestroyingStream(Exception ex);

        [LoggerMessage(EventId = 6153, Level = LogLevel.Warning, Message = "Error freeing memory during recovery")]
        private partial void LogErrorFreeingMemoryDuringRecovery(Exception ex);

        [LoggerMessage(EventId = 6154, Level = LogLevel.Warning, Message = "Error capturing device state for snapshot")]
        private partial void LogErrorCapturingDeviceState(Exception ex);

        #endregion
        private readonly ConcurrentDictionary<IntPtr, ResourceInfo> _allocatedMemory = new();
        private readonly ConcurrentDictionary<IntPtr, StreamInfo> _activeStreams = new();
        private readonly ConcurrentDictionary<IntPtr, EventInfo> _activeEvents = new();
        private readonly ConcurrentDictionary<string, ModuleInfo> _loadedModules = new();
        private readonly ConcurrentDictionary<string, KernelInfo> _compiledKernels = new();
        private readonly ReaderWriterLockSlim _stateLock = new();
        private ContextSnapshot? _lastSnapshot;
        private bool _disposed;

        // Resource tracking
        private long _totalMemoryAllocated;
        private long _totalMemoryFreed;
        private int _activeAllocations;
        private int _recoveryCount;

        /// <summary>
        /// Gets current resource statistics.
        /// </summary>
        public ResourceStatistics GetStatistics()
        {
            _stateLock.EnterReadLock();
            try
            {
                return new ResourceStatistics
                {
                    TotalMemoryAllocated = _totalMemoryAllocated,
                    TotalMemoryFreed = _totalMemoryFreed,
                    CurrentMemoryUsage = _totalMemoryAllocated - _totalMemoryFreed,
                    ActiveAllocations = _activeAllocations,
                    ActiveStreams = _activeStreams.Count,
                    ActiveEvents = _activeEvents.Count,
                    LoadedModules = _loadedModules.Count,
                    CompiledKernels = _compiledKernels.Count,
                    RecoveryCount = _recoveryCount
                };
            }
            finally
            {
                _stateLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Registers a memory allocation for tracking.
        /// </summary>
        public void RegisterMemoryAllocation(IntPtr ptr, ulong size, MemoryType type, string? tag = null)
        {
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            var info = new ResourceInfo
            {
                Pointer = ptr,
                Size = size,
                Type = type,
                Tag = tag,
                AllocationTime = DateTime.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId
            };

            if (_allocatedMemory.TryAdd(ptr, info))
            {
                _ = Interlocked.Add(ref _totalMemoryAllocated, (long)size);
                _ = Interlocked.Increment(ref _activeAllocations);

                LogRegisteredMemoryAllocation(_logger, ptr.ToInt64(), size, type);
            }
        }

        /// <summary>
        /// Unregisters a memory deallocation.
        /// </summary>
        public void UnregisterMemoryAllocation(IntPtr ptr)
        {
            if (_allocatedMemory.TryRemove(ptr, out var info))
            {
                _ = Interlocked.Add(ref _totalMemoryFreed, (long)info.Size);
                _ = Interlocked.Decrement(ref _activeAllocations);

                LogUnregisteredMemoryAllocation(_logger, ptr.ToInt64(), info.Size);
            }
        }

        /// <summary>
        /// Registers a CUDA stream for tracking.
        /// </summary>
        public void RegisterStream(IntPtr stream, StreamPriority priority = StreamPriority.Default)
        {
            if (stream == IntPtr.Zero)
            {
                return;
            }


            var info = new StreamInfo
            {
                Stream = stream,
                Priority = priority,
                CreationTime = DateTime.UtcNow,
                LastUsedTime = DateTime.UtcNow
            };

            if (_activeStreams.TryAdd(stream, info))
            {
                LogRegisteredStream(_logger, stream.ToInt64(), priority);
            }
        }

        /// <summary>
        /// Unregisters a CUDA stream.
        /// </summary>
        public void UnregisterStream(IntPtr stream)
        {
            if (_activeStreams.TryRemove(stream, out _))
            {
                LogUnregisteredStream(_logger, stream.ToInt64());
            }
        }

        /// <summary>
        /// Registers a compiled kernel for recovery.
        /// </summary>
        public void RegisterKernel(string name, byte[] ptxCode, byte[]? cubinCode = null)
        {
            var info = new KernelInfo
            {
                Name = name,
                PtxCode = ptxCode,
                CubinCode = cubinCode,
                CompilationTime = DateTime.UtcNow
            };

            _compiledKernels[name] = info;
            LogRegisteredKernel(_logger, name, ptxCode.Length, cubinCode?.Length ?? 0);
        }

        /// <summary>
        /// Creates a snapshot of the current context state.
        /// </summary>
        public async Task<ContextSnapshot> CreateSnapshotAsync(CancellationToken cancellationToken = default)
        {
            _stateLock.EnterReadLock();
            try
            {
                var snapshot = new ContextSnapshot
                {
                    SnapshotTime = DateTime.UtcNow,
                    SnapshotId = Guid.NewGuid(),
                    MemoryAllocations = new Dictionary<IntPtr, ResourceInfo>(_allocatedMemory),
                    ActiveStreams = new Dictionary<IntPtr, StreamInfo>(_activeStreams),
                    ActiveEvents = new Dictionary<IntPtr, EventInfo>(_activeEvents),
                    LoadedModules = new Dictionary<string, ModuleInfo>(_loadedModules),
                    CompiledKernels = new Dictionary<string, KernelInfo>(_compiledKernels),
                    Statistics = GetStatistics()
                };

                // Get device state
                await CaptureDeviceStateAsync(snapshot, cancellationToken);

                _lastSnapshot = snapshot;

                LogCreatedContextSnapshot(_logger, snapshot.ActiveStreams.Count, snapshot.CompiledKernels.Count, snapshot.MemoryAllocations.Count);

                return snapshot;
            }
            finally
            {
                _stateLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Prepares context for recovery by cleaning up resources.
        /// </summary>
        public async Task PrepareForRecoveryAsync(CancellationToken cancellationToken = default)
        {
            LogPreparingForRecovery(_logger);

            // Create snapshot before cleanup
            _ = await CreateSnapshotAsync(cancellationToken);

            // Synchronize all streams
            foreach (var stream in _activeStreams.Values)
            {
                try
                {
                    var result = CudaRuntime.cudaStreamSynchronize(stream.Stream);
                    if (result != CudaError.Success)
                    {
                        LogFailedToSynchronizeStream(_logger, stream.Stream.ToInt64(), result);
                    }
                }
                catch (Exception ex)
                {
                    LogErrorSynchronizingStream(ex);
                }
            }

            // Free all tracked memory
            foreach (var allocation in _allocatedMemory.Values)
            {
                try
                {
                    FreeMemoryAllocation(allocation);
                }
                catch (Exception ex)
                {
                    LogErrorFreeingMemory(ex, allocation.Pointer.ToInt64());
                }
            }

            // Destroy streams
            foreach (var stream in _activeStreams.Keys)
            {
                try
                {
                    var result = CudaRuntime.cudaStreamDestroy(stream);
                    if (result != CudaError.Success)
                    {
                        LogFailedToDestroyStream(_logger, stream.ToInt64(), result);
                    }
                }
                catch (Exception ex)
                {
                    LogErrorDestroyingStream(ex);
                }
            }

            // Clear tracking collections
            _allocatedMemory.Clear();
            _activeStreams.Clear();
            _activeEvents.Clear();

            LogContextPreparedForRecovery(_logger);
        }

        /// <summary>
        /// Restores context state after recovery.
        /// </summary>
        public async Task<RestoreResult> RestoreFromSnapshotAsync(
            ContextSnapshot? snapshot = null,

            CancellationToken cancellationToken = default)
        {
            snapshot ??= _lastSnapshot;
            if (snapshot == null)
            {
                return new RestoreResult
                {

                    Success = false,

                    Message = "No snapshot available for restoration"

                };
            }

            LogRestoringContextFromSnapshot(_logger, snapshot.SnapshotId);

            var result = new RestoreResult { Success = true };
            var errors = new List<string>();

            // Restore streams asynchronously
            await Task.Run(() =>
            {
                // Restore streams
                foreach (var streamInfo in snapshot.ActiveStreams.Values)
                {
                    try
                    {
                        var newStream = IntPtr.Zero;
                        var createResult = streamInfo.Priority == StreamPriority.High
                            ? CudaRuntime.cudaStreamCreateWithPriority(ref newStream, 0, -1)
                            : CudaRuntime.cudaStreamCreate(ref newStream);

                        if (createResult == CudaError.Success)
                        {
                            RegisterStream(newStream, streamInfo.Priority);
                            result.RestoredStreams++;
                        }
                        else
                        {
                            errors.Add($"Failed to restore stream: {createResult}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error restoring stream: {ex.Message}");
                    }
                }

                // Restore kernel compilations
                foreach (var kernel in snapshot.CompiledKernels.Values)
                {
                    try
                    {
                        RegisterKernel(kernel.Name, kernel.PtxCode, kernel.CubinCode);
                        result.RestoredKernels++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error restoring kernel {kernel.Name}: {ex.Message}");
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            // Note: Memory allocations are not restored automatically
            // as they would need to be re-created with new data
            result.MemoryAllocationsLost = snapshot.MemoryAllocations.Count;

            if (errors.Count > 0)
            {
                // Recreate result with init-only Errors property
                result = new RestoreResult
                {
                    Success = false,
                    Message = $"Restoration completed with {errors.Count} errors",
                    RestoredStreams = result.RestoredStreams,
                    RestoredKernels = result.RestoredKernels,
                    MemoryAllocationsLost = result.MemoryAllocationsLost,
                    Errors = errors
                };

                LogContextRestorationErrors(_logger, string.Join("; ", errors));
            }
            else
            {
                result.Message = "Context successfully restored";
                LogContextSuccessfullyRestored(_logger);
            }

            _ = Interlocked.Increment(ref _recoveryCount);
            return result;
        }

        /// <summary>
        /// Performs progressive recovery with multiple strategies.
        /// </summary>
        public async Task<RecoveryResult> PerformProgressiveRecoveryAsync(
            CudaError error,
            int attemptNumber = 1,
            CancellationToken cancellationToken = default)
        {
            LogPerformingProgressiveRecovery(_logger, error, attemptNumber);

            var strategy = DetermineRecoveryStrategy(error, attemptNumber);


            switch (strategy)
            {
                case RecoveryStrategy.StreamSync:
                    return await RecoverWithStreamSyncAsync(cancellationToken);


                case RecoveryStrategy.MemoryCleanup:
                    return await RecoverWithMemoryCleanupAsync(cancellationToken);


                case RecoveryStrategy.ContextReset:
                    return await RecoverWithContextResetAsync(cancellationToken);


                case RecoveryStrategy.DeviceReset:
                    return await RecoverWithDeviceResetAsync(cancellationToken);


                default:
                    return new RecoveryResult
                    {

                        Success = false,

                        Strategy = strategy,
                        Message = "No recovery strategy available"

                    };
            }
        }

        private static RecoveryStrategy DetermineRecoveryStrategy(CudaError error, int attemptNumber)
        {
            // Progressive strategy based on error type and attempt number
            if (attemptNumber == 1)
            {
                return error switch
                {
                    CudaError.NotReady => RecoveryStrategy.StreamSync,
                    CudaError.MemoryAllocation => RecoveryStrategy.MemoryCleanup,
                    CudaError.LaunchTimeout => RecoveryStrategy.StreamSync,
                    _ => RecoveryStrategy.ContextReset
                };
            }
            else if (attemptNumber == 2)
            {
                return RecoveryStrategy.ContextReset;
            }
            else
            {
                return RecoveryStrategy.DeviceReset;
            }
        }

        private async Task<RecoveryResult> RecoverWithStreamSyncAsync(CancellationToken cancellationToken)
        {
            LogAttemptingStreamSyncRecovery(_logger);

            foreach (var stream in _activeStreams.Values)
            {
                var result = CudaRuntime.cudaStreamSynchronize(stream.Stream);
                if (result != CudaError.Success)
                {
                    LogStreamSyncFailed(_logger, result);
                    return new RecoveryResult
                    {

                        Success = false,

                        Strategy = RecoveryStrategy.StreamSync,
                        Message = $"Stream sync failed: {result}"

                    };
                }
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Brief pause


            return new RecoveryResult
            {

                Success = true,

                Strategy = RecoveryStrategy.StreamSync,
                Message = "Recovery successful with stream synchronization"

            };
        }

        private async Task<RecoveryResult> RecoverWithMemoryCleanupAsync(CancellationToken cancellationToken)
        {
            LogAttemptingMemoryCleanupRecovery(_logger);

            // Free least recently used allocations

            var allocationsToFree = _allocatedMemory.Values
                .OrderBy(a => a.LastAccessTime ?? a.AllocationTime)
                .Take(_allocatedMemory.Count / 3) // Free 1/3 of allocations
                .ToList();

            foreach (var allocation in allocationsToFree)
            {
                try
                {
                    FreeMemoryAllocation(allocation);
                    UnregisterMemoryAllocation(allocation.Pointer);
                }
                catch (Exception ex)
                {
                    LogErrorFreeingMemoryDuringRecovery(ex);
                }
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);


            return new RecoveryResult
            {

                Success = true,

                Strategy = RecoveryStrategy.MemoryCleanup,
                Message = $"Recovery successful after freeing {allocationsToFree.Count} allocations"

            };
        }

        private async Task<RecoveryResult> RecoverWithContextResetAsync(CancellationToken cancellationToken)
        {
            LogAttemptingContextResetRecovery(_logger);

            await PrepareForRecoveryAsync(cancellationToken);

            // Context reset is handled by CudaErrorRecoveryManager


            return new RecoveryResult
            {

                Success = true,

                Strategy = RecoveryStrategy.ContextReset,
                Message = "Context prepared for reset"

            };
        }

        private async Task<RecoveryResult> RecoverWithDeviceResetAsync(CancellationToken cancellationToken)
        {
            LogAttemptingDeviceResetRecovery(_logger);

            await PrepareForRecoveryAsync(cancellationToken);


            var result = CudaRuntime.cudaDeviceReset();
            if (result != CudaError.Success)
            {
                return new RecoveryResult
                {

                    Success = false,

                    Strategy = RecoveryStrategy.DeviceReset,
                    Message = $"Device reset failed: {result}"

                };
            }


            return new RecoveryResult
            {

                Success = true,

                Strategy = RecoveryStrategy.DeviceReset,
                Message = "Recovery successful with device reset"

            };
        }

        private static void FreeMemoryAllocation(ResourceInfo allocation)
        {
            switch (allocation.Type)
            {
                case MemoryType.Device:
                    _ = CudaRuntime.cudaFree(allocation.Pointer);
                    break;
                case MemoryType.Host:
                    _ = CudaRuntime.cudaFreeHost(allocation.Pointer);
                    break;
                case MemoryType.Unified:
                    _ = CudaRuntime.cudaFree(allocation.Pointer);
                    break;
                case MemoryType.Pinned:
                    _ = CudaRuntime.cudaHostUnregister(allocation.Pointer);
                    break;
            }
        }

        private async Task CaptureDeviceStateAsync(ContextSnapshot snapshot, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Get current device
                    _ = CudaRuntime.cudaGetDevice(out var device);
                    snapshot.DeviceId = device;

                    // Get device properties
                    int major = 0, minor = 0;
                    _ = CudaRuntime.cudaDeviceGetAttribute(ref major, CudaDeviceAttribute.ComputeCapabilityMajor, device);
                    _ = CudaRuntime.cudaDeviceGetAttribute(ref minor, CudaDeviceAttribute.ComputeCapabilityMinor, device);
                    snapshot.ComputeCapability = $"{major}.{minor}";

                    // Get memory info
                    _ = CudaRuntime.cudaMemGetInfo(out var free, out var total);
                    snapshot.FreeMemory = free;
                    snapshot.TotalMemory = total;
                }
                catch (Exception ex)
                {
                    LogErrorCapturingDeviceState(ex);
                }
            }, cancellationToken).ConfigureAwait(false);
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


            _stateLock?.Dispose();
            _disposed = true;

            LogContextStateManagerDisposed(_logger, _recoveryCount);
        }
    }
    /// <summary>
    /// An memory type enumeration.
    /// </summary>

    // Supporting types
    public enum MemoryType
    {
        Device,
        Host,
        Unified,
        Pinned
    }
    /// <summary>
    /// An stream priority enumeration.
    /// </summary>

    public enum StreamPriority
    {
        Default,
        High,
        Low
    }
    /// <summary>
    /// An recovery strategy enumeration.
    /// </summary>

    public enum RecoveryStrategy
    {
        None,
        StreamSync,
        MemoryCleanup,
        ContextReset,
        DeviceReset
    }
    /// <summary>
    /// A class that represents resource info.
    /// </summary>

    public sealed class ResourceInfo
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
        public ulong Size { get; init; }
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public MemoryType Type { get; init; }
        /// <summary>
        /// Gets or sets the tag.
        /// </summary>
        /// <value>The tag.</value>
        public string? Tag { get; init; }
        /// <summary>
        /// Gets or sets the allocation time.
        /// </summary>
        /// <value>The allocation time.</value>
        public DateTime AllocationTime { get; init; }
        /// <summary>
        /// Gets or sets the last access time.
        /// </summary>
        /// <value>The last access time.</value>
        public DateTime? LastAccessTime { get; set; }
        /// <summary>
        /// Gets or sets the thread identifier.
        /// </summary>
        /// <value>The thread id.</value>
        public int ThreadId { get; init; }
    }
    /// <summary>
    /// A class that represents stream info.
    /// </summary>

    public sealed class StreamInfo
    {
        /// <summary>
        /// Gets or sets the stream.
        /// </summary>
        /// <value>The stream.</value>
        public IntPtr Stream { get; init; }
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public StreamPriority Priority { get; init; }
        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        /// <value>The creation time.</value>
        public DateTime CreationTime { get; init; }
        /// <summary>
        /// Gets or sets the last used time.
        /// </summary>
        /// <value>The last used time.</value>
        public DateTime LastUsedTime { get; set; }
    }
    /// <summary>
    /// A class that represents event info.
    /// </summary>

    public sealed class EventInfo
    {
        /// <summary>
        /// Gets or sets the event.
        /// </summary>
        /// <value>The event.</value>
        public IntPtr Event { get; init; }
        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        /// <value>The creation time.</value>
        public DateTime CreationTime { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether timing enabled.
        /// </summary>
        /// <value>The is timing enabled.</value>
        public bool IsTimingEnabled { get; init; }
    }
    /// <summary>
    /// A class that represents module info.
    /// </summary>

    public sealed class ModuleInfo
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the module.
        /// </summary>
        /// <value>The module.</value>
        public IntPtr Module { get; init; }
        /// <summary>
        /// Gets or sets the load time.
        /// </summary>
        /// <value>The load time.</value>
        public DateTime LoadTime { get; init; }
    }
    /// <summary>
    /// A class that represents kernel info.
    /// </summary>

    public sealed class KernelInfo
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the ptx code.
        /// </summary>
        /// <value>The ptx code.</value>
        public byte[] PtxCode { get; init; } = [];
        /// <summary>
        /// Gets or sets the cubin code.
        /// </summary>
        /// <value>The cubin code.</value>
        public byte[]? CubinCode { get; init; }
        /// <summary>
        /// Gets or sets the compilation time.
        /// </summary>
        /// <value>The compilation time.</value>
        public DateTime CompilationTime { get; init; }
    }
    /// <summary>
    /// A class that represents context snapshot.
    /// </summary>

    public sealed class ContextSnapshot
    {
        /// <summary>
        /// Gets or sets the snapshot identifier.
        /// </summary>
        /// <value>The snapshot id.</value>
        public Guid SnapshotId { get; init; }
        /// <summary>
        /// Gets or sets the snapshot time.
        /// </summary>
        /// <value>The snapshot time.</value>
        public DateTime SnapshotTime { get; init; }
        /// <summary>
        /// Gets or sets the memory allocations.
        /// </summary>
        /// <value>The memory allocations.</value>
        public Dictionary<IntPtr, ResourceInfo> MemoryAllocations { get; init; } = [];
        /// <summary>
        /// Gets or sets the active streams.
        /// </summary>
        /// <value>The active streams.</value>
        public Dictionary<IntPtr, StreamInfo> ActiveStreams { get; init; } = [];
        /// <summary>
        /// Gets or sets the active events.
        /// </summary>
        /// <value>The active events.</value>
        public Dictionary<IntPtr, EventInfo> ActiveEvents { get; init; } = [];
        /// <summary>
        /// Gets or sets the loaded modules.
        /// </summary>
        /// <value>The loaded modules.</value>
        public Dictionary<string, ModuleInfo> LoadedModules { get; init; } = [];
        /// <summary>
        /// Gets or sets the compiled kernels.
        /// </summary>
        /// <value>The compiled kernels.</value>
        public Dictionary<string, KernelInfo> CompiledKernels { get; init; } = [];
        /// <summary>
        /// Gets or sets the statistics.
        /// </summary>
        /// <value>The statistics.</value>
        public ResourceStatistics Statistics { get; init; } = new();
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>
        public int DeviceId { get; set; }
        /// <summary>
        /// Gets or sets the compute capability.
        /// </summary>
        /// <value>The compute capability.</value>
        public string ComputeCapability { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the free memory.
        /// </summary>
        /// <value>The free memory.</value>
        public nuint FreeMemory { get; set; }
        /// <summary>
        /// Gets or sets the total memory.
        /// </summary>
        /// <value>The total memory.</value>
        public nuint TotalMemory { get; set; }
    }
    /// <summary>
    /// A class that represents resource statistics.
    /// </summary>

    public sealed class ResourceStatistics
    {
        /// <summary>
        /// Gets or sets the total memory allocated.
        /// </summary>
        /// <value>The total memory allocated.</value>
        public long TotalMemoryAllocated { get; init; }
        /// <summary>
        /// Gets or sets the total memory freed.
        /// </summary>
        /// <value>The total memory freed.</value>
        public long TotalMemoryFreed { get; init; }
        /// <summary>
        /// Gets or sets the current memory usage.
        /// </summary>
        /// <value>The current memory usage.</value>
        public long CurrentMemoryUsage { get; init; }
        /// <summary>
        /// Gets or sets the active allocations.
        /// </summary>
        /// <value>The active allocations.</value>
        public int ActiveAllocations { get; init; }
        /// <summary>
        /// Gets or sets the active streams.
        /// </summary>
        /// <value>The active streams.</value>
        public int ActiveStreams { get; init; }
        /// <summary>
        /// Gets or sets the active events.
        /// </summary>
        /// <value>The active events.</value>
        public int ActiveEvents { get; init; }
        /// <summary>
        /// Gets or sets the loaded modules.
        /// </summary>
        /// <value>The loaded modules.</value>
        public int LoadedModules { get; init; }
        /// <summary>
        /// Gets or sets the compiled kernels.
        /// </summary>
        /// <value>The compiled kernels.</value>
        public int CompiledKernels { get; init; }
        /// <summary>
        /// Gets or sets the recovery count.
        /// </summary>
        /// <value>The recovery count.</value>
        public int RecoveryCount { get; init; }
    }
    /// <summary>
    /// A class that represents restore result.
    /// </summary>

    public sealed class RestoreResult
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <value>The success.</value>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>The message.</value>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the restored streams.
        /// </summary>
        /// <value>The restored streams.</value>
        public int RestoredStreams { get; set; }
        /// <summary>
        /// Gets or sets the restored kernels.
        /// </summary>
        /// <value>The restored kernels.</value>
        public int RestoredKernels { get; set; }
        /// <summary>
        /// Gets or sets the memory allocations lost.
        /// </summary>
        /// <value>The memory allocations lost.</value>
        public int MemoryAllocationsLost { get; set; }
        /// <summary>
        /// Gets or initializes the errors.
        /// </summary>
        /// <value>The errors.</value>
        public IList<string> Errors { get; init; } = [];
    }
    /// <summary>
    /// A class that represents recovery result.
    /// </summary>

    public sealed class RecoveryResult
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <value>The success.</value>
        public bool Success { get; init; }
        /// <summary>
        /// Gets or sets the strategy.
        /// </summary>
        /// <value>The strategy.</value>
        public RecoveryStrategy Strategy { get; init; }
        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>The message.</value>
        public string Message { get; init; } = string.Empty;
    }
}