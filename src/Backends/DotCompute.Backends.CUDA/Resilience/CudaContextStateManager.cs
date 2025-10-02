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
    public sealed class CudaContextStateManager(ILogger logger) : IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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


                _logger.LogDebugMessage($"Registered memory allocation: {ptr.ToInt64()} ({size} bytes, {type})");
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


                _logger.LogDebugMessage($"Unregistered memory allocation: {ptr.ToInt64()} ({info.Size} bytes)");
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
                _logger.LogDebugMessage($"Registered CUDA stream: {stream.ToInt64()} (Priority: {priority})");
            }
        }

        /// <summary>
        /// Unregisters a CUDA stream.
        /// </summary>
        public void UnregisterStream(IntPtr stream)
        {
            if (_activeStreams.TryRemove(stream, out _))
            {
                _logger.LogDebugMessage($"Unregistered CUDA stream: {stream.ToInt64()}");
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
            _logger.LogDebugMessage($"Registered kernel: {name} (PTX: {ptxCode.Length} bytes, CUBIN: {cubinCode?.Length ?? 0} bytes)");
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


                _logger.LogInfoMessage($"Created context snapshot with {snapshot.ActiveStreams.Count} streams, {snapshot.CompiledKernels.Count} kernels, {snapshot.MemoryAllocations.Count} allocations");

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
            _logger.LogWarningMessage("Preparing context for recovery - cleaning up resources");

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
                        _logger.LogWarningMessage($"Failed to synchronize stream {stream.Stream.ToInt64()}: {result}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error synchronizing stream during recovery preparation");
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
                    _logger.LogWarning(ex, "Error freeing memory {Ptr:X} during recovery preparation",

                        allocation.Pointer.ToInt64());
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
                        _logger.LogWarningMessage($"Failed to destroy stream {stream.ToInt64()}: {result}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error destroying stream during recovery preparation");
                }
            }

            // Clear tracking collections
            _allocatedMemory.Clear();
            _activeStreams.Clear();
            _activeEvents.Clear();


            _logger.LogInfoMessage("Context prepared for recovery - resources cleaned up");
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

            _logger.LogInfoMessage("Restoring context from snapshot {snapshot.SnapshotId}");

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
                result.Success = false;
                result.Message = $"Restoration completed with {errors.Count} errors";
                result.Errors = errors;


                _logger.LogWarningMessage($"Context restoration completed with errors: {string.Join("; ", errors)}");
            }
            else
            {
                result.Message = "Context successfully restored";
                _logger.LogInfoMessage("Context successfully restored from snapshot");
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
            _logger.LogWarningMessage($"Performing progressive recovery for error {error}, attempt {attemptNumber}");

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
            _logger.LogInfoMessage("Attempting recovery with stream synchronization");


            foreach (var stream in _activeStreams.Values)
            {
                var result = CudaRuntime.cudaStreamSynchronize(stream.Stream);
                if (result != CudaError.Success)
                {
                    _logger.LogWarningMessage("Stream sync failed: {result}");
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
            _logger.LogInfoMessage("Attempting recovery with memory cleanup");

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
                    _logger.LogWarning(ex, "Error freeing memory during recovery");
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
            _logger.LogInfoMessage("Attempting recovery with context reset");


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
            _logger.LogInfoMessage("Attempting recovery with device reset");


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
                    _logger.LogWarning(ex, "Error capturing device state for snapshot");
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _stateLock?.Dispose();
            _disposed = true;

            _logger.LogInfoMessage("Context state manager disposed. Recovery count: {_recoveryCount}");
        }
    }

    // Supporting types
    public enum MemoryType
    {
        Device,
        Host,
        Unified,
        Pinned
    }

    public enum StreamPriority
    {
        Default,
        High,
        Low
    }

    public enum RecoveryStrategy
    {
        None,
        StreamSync,
        MemoryCleanup,
        ContextReset,
        DeviceReset
    }

    public sealed class ResourceInfo
    {
        public IntPtr Pointer { get; init; }
        public ulong Size { get; init; }
        public MemoryType Type { get; init; }
        public string? Tag { get; init; }
        public DateTime AllocationTime { get; init; }
        public DateTime? LastAccessTime { get; set; }
        public int ThreadId { get; init; }
    }

    public sealed class StreamInfo
    {
        public IntPtr Stream { get; init; }
        public StreamPriority Priority { get; init; }
        public DateTime CreationTime { get; init; }
        public DateTime LastUsedTime { get; set; }
    }

    public sealed class EventInfo
    {
        public IntPtr Event { get; init; }
        public DateTime CreationTime { get; init; }
        public bool IsTimingEnabled { get; init; }
    }

    public sealed class ModuleInfo
    {
        public string Name { get; init; } = string.Empty;
        public IntPtr Module { get; init; }
        public DateTime LoadTime { get; init; }
    }

    public sealed class KernelInfo
    {
        public string Name { get; init; } = string.Empty;
        public byte[] PtxCode { get; init; } = [];
        public byte[]? CubinCode { get; init; }
        public DateTime CompilationTime { get; init; }
    }

    public sealed class ContextSnapshot
    {
        public Guid SnapshotId { get; init; }
        public DateTime SnapshotTime { get; init; }
        public Dictionary<IntPtr, ResourceInfo> MemoryAllocations { get; init; } = [];
        public Dictionary<IntPtr, StreamInfo> ActiveStreams { get; init; } = [];
        public Dictionary<IntPtr, EventInfo> ActiveEvents { get; init; } = [];
        public Dictionary<string, ModuleInfo> LoadedModules { get; init; } = [];
        public Dictionary<string, KernelInfo> CompiledKernels { get; init; } = [];
        public ResourceStatistics Statistics { get; init; } = new();
        public int DeviceId { get; set; }
        public string ComputeCapability { get; set; } = string.Empty;
        public nuint FreeMemory { get; set; }
        public nuint TotalMemory { get; set; }
    }

    public sealed class ResourceStatistics
    {
        public long TotalMemoryAllocated { get; init; }
        public long TotalMemoryFreed { get; init; }
        public long CurrentMemoryUsage { get; init; }
        public int ActiveAllocations { get; init; }
        public int ActiveStreams { get; init; }
        public int ActiveEvents { get; init; }
        public int LoadedModules { get; init; }
        public int CompiledKernels { get; init; }
        public int RecoveryCount { get; init; }
    }

    public sealed class RestoreResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RestoredStreams { get; set; }
        public int RestoredKernels { get; set; }
        public int MemoryAllocationsLost { get; set; }
        public List<string> Errors { get; set; } = [];
    }

    public sealed class RecoveryResult
    {
        public bool Success { get; init; }
        public RecoveryStrategy Strategy { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}