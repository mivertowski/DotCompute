// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Helper utilities for managing ring kernel control blocks on the GPU.
/// </summary>
internal static class RingKernelControlBlockHelper
{
    /// <summary>
    /// Represents a pinned control block with both host and device pointers.
    /// </summary>
    public readonly struct PinnedControlBlock
    {
        /// <summary>Host pointer for CPU access (zero-copy reads). Same as DevicePointer for unified memory.</summary>
        public IntPtr HostPointer { get; init; }
        /// <summary>Device pointer for GPU access.</summary>
        public IntPtr DevicePointer { get; init; }
        /// <summary>True if unified memory was used (cudaMallocManaged).</summary>
        public bool IsUnifiedMemory { get; init; }
    }

    /// <summary>
    /// Represents an async control block for WSL2 with pinned staging buffer and CUDA events.
    /// This enables non-blocking control block communication when unified/mapped memory isn't available.
    /// </summary>
    public sealed class AsyncControlBlock : IDisposable
    {
        /// <summary>CUDA context for this control block.</summary>
        public IntPtr Context { get; init; }
        /// <summary>Device pointer for GPU access (the actual control block).</summary>
        public IntPtr DevicePointer { get; init; }
        /// <summary>Pinned host staging buffer for async copies.</summary>
        public IntPtr StagingBuffer { get; init; }
        /// <summary>CUDA event for tracking async read completion.</summary>
        public IntPtr ReadEvent { get; init; }
        /// <summary>CUDA event for tracking async write completion.</summary>
        public IntPtr WriteEvent { get; init; }
        /// <summary>Dedicated stream for control block operations.</summary>
        public IntPtr ControlStream { get; init; }
        /// <summary>Size of the control block in bytes.</summary>
        public int Size { get; init; }
        /// <summary>True if this is a WSL2 async control block (vs pinned/unified).</summary>
        public bool IsAsyncMode { get; init; }
        /// <summary>Cached last read value for non-blocking reads.</summary>
        public RingKernelControlBlock LastReadValue { get; set; }
        /// <summary>True if an async read is in progress.</summary>
        public bool ReadPending { get; set; }
        /// <summary>True if an async write is in progress.</summary>
        public bool WritePending { get; set; }
        /// <summary>True if staging buffer is pinned (cudaHostAlloc succeeded). If false, staging uses regular malloc.</summary>
        public bool IsStagingPinned { get; init; }

        private bool _disposed;

        /// <summary>
        /// Disposes of the async control block resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Free CUDA events
            if (ReadEvent != IntPtr.Zero)
            {
                CudaRuntime.cudaEventDestroy(ReadEvent);
            }
            if (WriteEvent != IntPtr.Zero)
            {
                CudaRuntime.cudaEventDestroy(WriteEvent);
            }

            // Free staging buffer (pinned or regular malloc)
            if (StagingBuffer != IntPtr.Zero)
            {
                if (IsStagingPinned)
                {
                    CudaRuntime.cudaFreeHost(StagingBuffer);
                }
                else
                {
                    Marshal.FreeHGlobal(StagingBuffer);
                }
            }

            // Free device memory (allocated via Runtime API cudaMalloc)
            if (DevicePointer != IntPtr.Zero)
            {
                CudaRuntime.cudaFree(DevicePointer);
            }

            // Note: ControlStream is managed by the runtime, not freed here
        }
    }

    /// <summary>
    /// Allocates a control block using pinned host memory for zero-copy CPU access.
    /// This allows reading control block state without blocking on cooperative kernels.
    /// </summary>
    /// <param name="context">CUDA context to use for allocation.</param>
    /// <returns>Pinned control block with host and device pointers.</returns>
    public static PinnedControlBlock AllocateAndInitializePinned(IntPtr context)
    {
        // Set context as current for this thread (Driver API)
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        // Initialize Runtime API context as well (required for cudaHostAlloc/cudaMallocManaged)
        // The Runtime API needs explicit device selection to work with Driver API contexts
        _ = CudaRuntime.cudaSetDevice(0);
        // Ignore errors - will fall back to device memory if Runtime API doesn't work

        var controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var hostPtr = IntPtr.Zero;
        var devicePtr = IntPtr.Zero;
        var usePinnedMemory = false;
        var useUnifiedMemory = false;

        // Strategy 1: Try to allocate pinned host memory with device mapping (zero-copy)
        // This may fail in some environments (e.g., WSL2 with certain configurations)
        // Mapped = map to device address space, Portable = accessible from all contexts
        const CudaHostAllocFlags flags = CudaHostAllocFlags.Mapped | CudaHostAllocFlags.Portable;
        var allocResult = CudaRuntime.cudaHostAlloc(ref hostPtr, (ulong)controlBlockSize, (uint)flags);
        if (allocResult == CudaError.Success)
        {
            // Get device pointer for the same physical memory
            var devicePtrResult = CudaRuntime.cudaHostGetDevicePointer(ref devicePtr, hostPtr, 0);
            if (devicePtrResult == CudaError.Success)
            {
                usePinnedMemory = true;
            }
            else
            {
                // Failed to get device pointer, free and fall back
                CudaRuntime.cudaFreeHost(hostPtr);
                hostPtr = IntPtr.Zero;
            }
        }

        // Strategy 2: Try unified memory (cudaMallocManaged)
        // NOTE: In WSL2, unified memory allocation succeeds but concurrent CPU/GPU access
        // during cooperative kernel execution causes AccessViolationException.
        // We skip unified memory in WSL2 and fall back to device memory.
        var isWsl2 = IsRunningInWsl2();
        if (!usePinnedMemory && !isWsl2)
        {
            var unifiedPtr = IntPtr.Zero;
            // Flag 1 = cudaMemAttachGlobal - accessible from any stream on any device
            var unifiedResult = CudaRuntime.cudaMallocManaged(ref unifiedPtr, (ulong)controlBlockSize, 1);
            if (unifiedResult == CudaError.Success)
            {
                // With unified memory, same pointer works for both CPU and GPU
                hostPtr = unifiedPtr;
                devicePtr = unifiedPtr;
                useUnifiedMemory = true;
            }
        }
        else if (isWsl2 && !usePinnedMemory)
        {
        }

        // Strategy 3: Fallback to regular device memory if both pinned and unified failed
        // Note: This will require explicit memory copies and may block on cooperative kernels
        if (!usePinnedMemory && !useUnifiedMemory)
        {
            // Use Runtime API cudaMalloc instead of Driver API cuMemAlloc
            // The primary context works correctly with Runtime API but NOT with Driver API's cuMemAlloc
            // This is because the primary context must be activated via cudaSetDevice first,
            // and after that, only Runtime API memory operations work correctly.
            var deviceAllocResult = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)controlBlockSize);
            if (deviceAllocResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to allocate control block in device memory: {deviceAllocResult}");
            }
            // Note: hostPtr remains IntPtr.Zero - will use device memory copy for reads
        }

        // Initialize control block
        var controlBlock = RingKernelControlBlock.CreateInactive();

        if (usePinnedMemory || useUnifiedMemory)
        {
            // Write directly to pinned/unified host memory (visible to GPU)
            unsafe
            {
                Unsafe.Write(hostPtr.ToPointer(), controlBlock);
            }
        }
        else
        {
            // Copy to device memory (fallback mode)
            // Use Runtime API cudaMemcpy since we allocated with cudaMalloc
            var tempHostPtr = Marshal.AllocHGlobal(controlBlockSize);
            try
            {
                unsafe
                {
                    Unsafe.Write(tempHostPtr.ToPointer(), controlBlock);
                }
                var copyResult = CudaRuntime.cudaMemcpy(
                    devicePtr,
                    tempHostPtr,
                    (nuint)controlBlockSize,
                    CudaMemcpyKind.HostToDevice);
                if (copyResult != CudaError.Success)
                {
                    // Use Runtime API cudaFree since we allocated with cudaMalloc
                    CudaRuntime.cudaFree(devicePtr);
                    throw new InvalidOperationException($"Failed to initialize control block: {copyResult}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tempHostPtr);
            }
        }

        // CRITICAL: Restore the Driver API context after using Runtime API functions
        // The cudaSetDevice(0) call may have switched to the Runtime API's primary context
        // We must restore the original context for subsequent Driver API operations
        var restoreResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (restoreResult != CudaError.Success)
        {
            // Try to clean up if context restoration fails
            if (useUnifiedMemory)
            {
                CudaRuntime.cudaFree(devicePtr);
            }
            else if (usePinnedMemory)
            {
                CudaRuntime.cudaFreeHost(hostPtr);
            }
            else if (devicePtr != IntPtr.Zero)
            {
                // Use Runtime API cudaFree since we allocated with cudaMalloc
                CudaRuntime.cudaFree(devicePtr);
            }
            throw new InvalidOperationException($"Failed to restore CUDA context after allocation: {restoreResult}");
        }

        return new PinnedControlBlock { HostPointer = hostPtr, DevicePointer = devicePtr, IsUnifiedMemory = useUnifiedMemory };
    }

    /// <summary>
    /// Reads control block directly from pinned host memory (zero-copy, non-blocking).
    /// </summary>
    /// <param name="hostPtr">Host pointer to the pinned control block.</param>
    /// <returns>The control block state.</returns>
    public static RingKernelControlBlock ReadPinned(IntPtr hostPtr)
    {
        if (hostPtr == IntPtr.Zero)
        {
            throw new ArgumentException("Host pointer cannot be null", nameof(hostPtr));
        }

        // Ensure CPU sees latest GPU writes for unified/pinned memory
        Thread.MemoryBarrier();
        unsafe
        {
            return Unsafe.Read<RingKernelControlBlock>(hostPtr.ToPointer());
        }
    }

    /// <summary>
    /// Writes control block directly to pinned/unified host memory (zero-copy, non-blocking).
    /// GPU will see the write immediately for unified memory, or on next access for pinned mapped memory.
    /// </summary>
    /// <param name="hostPtr">Host pointer to the pinned/unified control block.</param>
    /// <param name="controlBlock">The control block state to write.</param>
    public static void WritePinned(IntPtr hostPtr, RingKernelControlBlock controlBlock)
    {
        unsafe
        {
            Unsafe.Write(hostPtr.ToPointer(), controlBlock);
        }
        // Memory fence to ensure visibility to GPU
        Thread.MemoryBarrier();
    }

    /// <summary>
    /// Sets the IsActive flag directly in pinned/unified memory (zero-copy, non-blocking).
    /// </summary>
    /// <param name="hostPtr">Host pointer to the pinned/unified control block.</param>
    /// <param name="active">True to activate, false to deactivate.</param>
    public static void SetActivePinned(IntPtr hostPtr, bool active)
    {
        // IsActive is at offset 0 in the control block (first int field)
        Marshal.WriteInt32(hostPtr, active ? 1 : 0);
        // Memory fence to ensure visibility to GPU
        Thread.MemoryBarrier();
    }

    /// <summary>
    /// Sets the ShouldTerminate flag directly in pinned/unified memory (zero-copy, non-blocking).
    /// </summary>
    /// <param name="hostPtr">Host pointer to the pinned/unified control block.</param>
    public static void SetTerminatePinned(IntPtr hostPtr)
    {
        // ShouldTerminate is at offset 4 in the control block (second int field)
        Marshal.WriteInt32(hostPtr + 4, 1);
        // Memory fence to ensure visibility to GPU
        Thread.MemoryBarrier();
    }

    /// <summary>
    /// Frees a pinned control block allocated with AllocateAndInitializePinned.
    /// </summary>
    /// <param name="controlBlock">The pinned control block to free.</param>
    public static void FreePinned(PinnedControlBlock controlBlock)
    {
        FreePinned(controlBlock.HostPointer, controlBlock.DevicePointer, controlBlock.IsUnifiedMemory);
    }

    /// <summary>
    /// Frees a pinned control block allocated with AllocateAndInitializePinned.
    /// </summary>
    /// <param name="hostPtr">Host pointer to free (can be IntPtr.Zero if fallback was used).</param>
    /// <param name="devicePtr">Device pointer (only freed if hostPtr is zero - fallback mode).</param>
    /// <param name="isUnifiedMemory">True if unified memory was used (requires cudaFree instead of cudaFreeHost).</param>
    public static void FreePinned(IntPtr hostPtr, IntPtr devicePtr = default, bool isUnifiedMemory = false)
    {
        if (isUnifiedMemory && hostPtr != IntPtr.Zero)
        {
            // Unified memory - use cudaFree (same pointer for host and device)
            CudaRuntime.cudaFree(hostPtr);
        }
        else if (hostPtr != IntPtr.Zero)
        {
            // Pinned memory - free via host pointer (device pointer is mapped to same physical memory)
            CudaRuntime.cudaFreeHost(hostPtr);
        }
        else if (devicePtr != IntPtr.Zero)
        {
            // Fallback mode - free device memory allocated with cudaMalloc (Runtime API)
            CudaRuntime.cudaFree(devicePtr);
        }
    }

    /// <summary>
    /// Allocates a control block in device memory and initializes it to inactive state.
    /// </summary>
    /// <param name="context">CUDA context to use for allocation.</param>
    /// <returns>Device pointer to the allocated control block.</returns>
    public static IntPtr AllocateAndInitialize(IntPtr context)
    {
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        // Allocate device memory for control block
        var devicePtr = IntPtr.Zero;
        var controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var allocResult = CudaApi.cuMemAlloc(ref devicePtr, (nuint)controlBlockSize);
        if (allocResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to allocate control block: {allocResult}");
        }

        // Initialize control block on host
        var controlBlock = RingKernelControlBlock.CreateInactive();

        // Copy to device
        var hostPtr = Marshal.AllocHGlobal(controlBlockSize);
        try
        {
            unsafe
            {
                Unsafe.Write(hostPtr.ToPointer(), controlBlock);
            }

            var copyResult = CudaApi.cuMemcpyHtoD(devicePtr, hostPtr, (nuint)controlBlockSize);
            if (copyResult != CudaError.Success)
            {
                CudaApi.cuMemFree(devicePtr);
                throw new InvalidOperationException($"Failed to initialize control block: {copyResult}");
            }

            // Ensure copy completes
            CudaRuntimeCore.cuCtxSynchronize();
        }
        finally
        {
            Marshal.FreeHGlobal(hostPtr);
        }

        return devicePtr;
    }

    /// <summary>
    /// Reads the current state of a control block from device memory using Runtime API.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to the control block.</param>
    /// <param name="controlStream">Non-blocking stream for the copy operation (ignored in WSL2 mode).</param>
    /// <returns>The control block state.</returns>
    public static RingKernelControlBlock Read(IntPtr context, IntPtr devicePtr, IntPtr controlStream = default)
    {
        // WSL2 fix: Use Runtime API for context setup to ensure compatibility with Runtime API allocated memory
        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA device: {setDeviceResult}");
        }

        var controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var hostPtr = Marshal.AllocHGlobal(controlBlockSize);

        try
        {
            // WSL2 fix: Use Runtime API (cudaMemcpy) for copies from Runtime API allocated memory
            // This ensures consistency when device memory is allocated via cudaMalloc
            var copyResult = CudaRuntime.cudaMemcpy(
                hostPtr,
                devicePtr,
                (nuint)controlBlockSize,
                CudaMemcpyKind.DeviceToHost);

            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to read control block: {copyResult}");
            }

            unsafe
            {
                return Unsafe.Read<RingKernelControlBlock>(hostPtr.ToPointer());
            }
        }
        finally
        {
            Marshal.FreeHGlobal(hostPtr);
        }
    }

    /// <summary>
    /// Writes an updated control block state to device memory.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to the control block.</param>
    /// <param name="controlBlock">The control block state to write.</param>
    public static void Write(IntPtr context, IntPtr devicePtr, RingKernelControlBlock controlBlock)
    {
        // WSL2 fix: Use Runtime API for context setup to ensure compatibility with Runtime API allocated memory
        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA device: {setDeviceResult}");
        }

        var controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var hostPtr = Marshal.AllocHGlobal(controlBlockSize);

        try
        {
            unsafe
            {
                Unsafe.Write(hostPtr.ToPointer(), controlBlock);
            }

            // WSL2 fix: Use Runtime API (cudaMemcpy) for copies to Runtime API allocated memory
            // This ensures consistency when device memory is allocated via cudaMalloc
            var copyResult = CudaRuntime.cudaMemcpy(
                devicePtr,
                hostPtr,
                (nuint)controlBlockSize,
                CudaMemcpyKind.HostToDevice);
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to write control block: {copyResult}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(hostPtr);
        }
    }

    /// <summary>
    /// Sets the IsActive flag in the control block atomically.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to the control block.</param>
    /// <param name="active">True to activate, false to deactivate.</param>
    public static void SetActive(IntPtr context, IntPtr devicePtr, bool active)
    {
        // WSL2 fix: Use Runtime API for context setup
        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA device: {setDeviceResult}");
        }

        var value = active ? 1 : 0;
        var hostPtr = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            Marshal.WriteInt32(hostPtr, value);

            // Write just the IsActive field (offset 0, size 4)
            // WSL2 fix: Use Runtime API (cudaMemcpy) for copies to Runtime API allocated memory
            var copyResult = CudaRuntime.cudaMemcpy(
                devicePtr,
                hostPtr,
                (nuint)sizeof(int),
                CudaMemcpyKind.HostToDevice);
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set active flag: {copyResult}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(hostPtr);
        }
    }

    /// <summary>
    /// Sets the ShouldTerminate flag in the control block atomically.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to the control block.</param>
    public static void SetTerminate(IntPtr context, IntPtr devicePtr)
    {
        // WSL2 fix: Use Runtime API for context setup
        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA device: {setDeviceResult}");
        }

        var hostPtr = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            Marshal.WriteInt32(hostPtr, 1);

            // Write the ShouldTerminate field (offset 4, size 4)
            // WSL2 fix: Use Runtime API (cudaMemcpy) for copies to Runtime API allocated memory
            var terminateFlagPtr = devicePtr + 4;
            var copyResult = CudaRuntime.cudaMemcpy(
                terminateFlagPtr,
                hostPtr,
                (nuint)sizeof(int),
                CudaMemcpyKind.HostToDevice);
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set terminate flag: {copyResult}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(hostPtr);
        }
    }

    /// <summary>
    /// Waits for the kernel to set the HasTerminated flag, indicating graceful shutdown.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to the control block.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if kernel terminated gracefully, false if timeout or cancelled.</returns>
    public static async Task<bool> WaitForTerminationAsync(
        IntPtr context,
        IntPtr devicePtr,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        try
        {
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                var controlBlock = Read(context, devicePtr);
                if (controlBlock.HasTerminated != 0)
                {
                    return true;
                }

                // Poll every 10ms
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            // Cancellation requested - return false
            return false;
        }

        return false;
    }

    /// <summary>
    /// Frees a control block allocated in device memory.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to free.</param>
    public static void Free(IntPtr context, IntPtr devicePtr)
    {
        if (devicePtr == IntPtr.Zero)
        {
            return;
        }

        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            // Log but don't throw during cleanup
            return;
        }

        CudaApi.cuMemFree(devicePtr);
    }

    /// <summary>
    /// Detects if running in WSL2 environment.
    /// WSL2 has limited support for unified memory concurrent access during cooperative kernel execution.
    /// </summary>
    /// <returns>True if running in WSL2; otherwise, false.</returns>
    internal static bool IsRunningInWsl2()
    {
        // Check for WSL2-specific indicators
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            // WSL2 kernel has "microsoft" in the version string
            var kernelVersion = File.ReadAllText("/proc/version");
            if (kernelVersion.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ||
                kernelVersion.Contains("WSL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Also check for WSL interop file
            if (File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop"))
            {
                return true;
            }
        }
        catch
        {
            // If we can't determine, assume not WSL2
        }

        return false;
    }

    #region WSL2 Async Control Block Methods

    /// <summary>
    /// Allocates an async control block for WSL2 with pinned staging buffer and CUDA events.
    /// This enables non-blocking control block communication when unified/mapped memory isn't available.
    /// </summary>
    /// <param name="context">CUDA context to use for allocation.</param>
    /// <param name="controlStream">Dedicated stream for control block operations.</param>
    /// <returns>Async control block with all resources allocated.</returns>
    public static AsyncControlBlock AllocateAsyncControlBlock(IntPtr context, IntPtr controlStream)
    {
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        var controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var devicePtr = IntPtr.Zero;
        var stagingBuffer = IntPtr.Zero;
        var readEvent = IntPtr.Zero;
        var writeEvent = IntPtr.Zero;
        var isStagingPinned = false;

        try
        {
            // Initialize Runtime API for cudaHostAlloc (staging buffer doesn't need device mapping)
            var setDeviceResult = CudaRuntime.cudaSetDevice(0);

            // Allocate pinned host memory for staging buffer (portable, not mapped)
            // cudaHostAllocPortable ensures the memory is portable across contexts
            const uint pinnedFlags = (uint)CudaHostAllocFlags.Portable;
            var stagingResult = CudaRuntime.cudaHostAlloc(ref stagingBuffer, (ulong)controlBlockSize, pinnedFlags);
            if (stagingResult == CudaError.Success)
            {
                isStagingPinned = true;
            }
            else
            {
                // Fall back to regular malloc if pinned fails
                // NOTE: When using regular malloc, async copies won't work - will use sync copies
                stagingBuffer = Marshal.AllocHGlobal(controlBlockSize);
                isStagingPinned = false;
            }

            // Create CUDA events with blocking sync disabled for non-blocking queries
            // cudaEventDisableTiming (0x2) - don't record timing (faster)
            const uint eventFlags = 0x2;
            var readEventResult = CudaRuntime.cudaEventCreateWithFlags(ref readEvent, eventFlags);
            if (readEventResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to create read event: {readEventResult}");
            }

            var writeEventResult = CudaRuntime.cudaEventCreateWithFlags(ref writeEvent, eventFlags);
            if (writeEventResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to create write event: {writeEventResult}");
            }

            // WSL2 fix: Use Runtime API for device memory allocation and copies
            // This ensures consistency when using cudaMemcpy later (Runtime API throughout)
            var deviceAllocResult = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)controlBlockSize);
            if (deviceAllocResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to allocate device memory: {deviceAllocResult}");
            }

            // Initialize control block with is_active=1 for WSL2 mode
            // This is critical because cross-CPU/GPU memory visibility for the is_active flag
            // is unreliable in WSL2. By starting active, we avoid the need for mid-execution signaling.
            var controlBlock = RingKernelControlBlock.CreateActive();
            unsafe
            {
                Unsafe.Write(stagingBuffer.ToPointer(), controlBlock);
            }

            // Copy initial state to device (synchronous for initialization) using Runtime API
            var initCopyResult = CudaRuntime.cudaMemcpy(
                devicePtr,
                stagingBuffer,
                (nuint)controlBlockSize,
                CudaMemcpyKind.HostToDevice);
            if (initCopyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to initialize control block: {initCopyResult}");
            }


            return new AsyncControlBlock
            {
                Context = context,
                DevicePointer = devicePtr,
                StagingBuffer = stagingBuffer,
                ReadEvent = readEvent,
                WriteEvent = writeEvent,
                ControlStream = controlStream,
                Size = controlBlockSize,
                IsAsyncMode = true,
                IsStagingPinned = isStagingPinned,
                LastReadValue = controlBlock,
                ReadPending = false,
                WritePending = false
            };
        }
        catch
        {
            // Cleanup on failure
            if (readEvent != IntPtr.Zero)
            {
                CudaRuntime.cudaEventDestroy(readEvent);
            }

            if (writeEvent != IntPtr.Zero)
            {
                CudaRuntime.cudaEventDestroy(writeEvent);
            }

            // Free staging buffer using the appropriate method based on allocation type
            if (stagingBuffer != IntPtr.Zero)
            {
                if (isStagingPinned)
                {
                    CudaRuntime.cudaFreeHost(stagingBuffer);
                }
                else
                {
                    Marshal.FreeHGlobal(stagingBuffer);
                }
            }

            if (devicePtr != IntPtr.Zero)
            {
                CudaRuntime.cudaFree(devicePtr);
            }

            throw;
        }
    }

    /// <summary>
    /// Initiates an async read of the control block from device to staging buffer.
    /// Use <see cref="TryCompleteAsyncRead"/> to check completion and get the result.
    /// If staging buffer is not pinned (WSL2 fallback mode), uses synchronous copy instead.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <returns>True if read was initiated/completed; false if a read is already pending or on error.</returns>
    public static bool StartAsyncRead(AsyncControlBlock asyncBlock)
    {
        if (asyncBlock.ReadPending)
        {
            return false; // Already have a read in progress
        }

        // WSL2 fix: Use Runtime API instead of Driver API for better compatibility
        // The Runtime API context management is more reliable in WSL2 where Driver API
        // context handles can become stale across different code paths
        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResult != CudaError.Success)
        {
            Trace.WriteLine($"[StartAsyncRead] FAILED: cudaSetDevice returned {setDeviceResult}");
            return false;
        }

        // Check if staging buffer is pinned - cudaMemcpyAsync requires pinned host memory
        if (!asyncBlock.IsStagingPinned)
        {
            // WSL2 fallback: Staging buffer is regular malloc - must use synchronous copy
            // cudaMemcpy (sync) works with non-pinned memory, cudaMemcpyAsync does not
            var syncCopyResult = CudaRuntime.cudaMemcpy(
                asyncBlock.StagingBuffer,
                asyncBlock.DevicePointer,
                (nuint)asyncBlock.Size,
                CudaMemcpyKind.DeviceToHost);

            if (syncCopyResult != CudaError.Success)
            {
                Trace.WriteLine($"[StartAsyncRead] FAILED: cudaMemcpy (sync) returned {syncCopyResult}, DevicePtr=0x{asyncBlock.DevicePointer:X}, Size={asyncBlock.Size}");
                return false;
            }

            // Copy completed synchronously - update cached value immediately
            unsafe
            {
                asyncBlock.LastReadValue = Unsafe.Read<RingKernelControlBlock>(asyncBlock.StagingBuffer.ToPointer());
            }
            var cb = asyncBlock.LastReadValue;
            Trace.WriteLine($"[StartAsyncRead] SUCCESS (sync): IsActive={cb.IsActive}, MessagesProcessed={cb.MessagesProcessed}, HasTerminated={cb.HasTerminated}, ShouldTerminate={cb.ShouldTerminate}, Errors={cb.ErrorsEncountered}, InputQueuePtr=0x{cb.InputQueueBufferPtr:X}");
            // Don't set ReadPending - copy is already complete
            return true;
        }

        // Pinned staging buffer - use async copy for non-blocking behavior
        var copyResult = CudaRuntime.cudaMemcpyAsync(
            asyncBlock.StagingBuffer,
            asyncBlock.DevicePointer,
            (ulong)asyncBlock.Size,
            CudaMemcpyKind.DeviceToHost,
            asyncBlock.ControlStream);

        if (copyResult != CudaError.Success)
        {
            Trace.WriteLine($"[StartAsyncRead] FAILED: cudaMemcpyAsync returned {copyResult}");
            return false;
        }

        // Record event to track completion
        var eventResult = CudaRuntime.cudaEventRecord(asyncBlock.ReadEvent, asyncBlock.ControlStream);
        if (eventResult != CudaError.Success)
        {
            Trace.WriteLine($"[StartAsyncRead] FAILED: cudaEventRecord returned {eventResult}");
            return false;
        }

        Trace.WriteLine("[StartAsyncRead] Async read initiated (pinned memory path)");
        asyncBlock.ReadPending = true;
        return true;
    }

    /// <summary>
    /// Tries to complete a pending async read. Non-blocking.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <param name="controlBlock">The read control block if completed.</param>
    /// <returns>True if read completed; false if still pending or no read was started.</returns>
    public static bool TryCompleteAsyncRead(AsyncControlBlock asyncBlock, out RingKernelControlBlock controlBlock)
    {
        controlBlock = asyncBlock.LastReadValue;

        if (!asyncBlock.ReadPending)
        {
            return false; // No read pending
        }

        // Query event status (non-blocking)
        var queryResult = CudaRuntime.cudaEventQuery(asyncBlock.ReadEvent);

        if (queryResult == CudaError.Success)
        {
            // Read completed - extract value from staging buffer
            unsafe
            {
                controlBlock = Unsafe.Read<RingKernelControlBlock>(asyncBlock.StagingBuffer.ToPointer());
            }
            asyncBlock.LastReadValue = controlBlock;
            asyncBlock.ReadPending = false;
            Trace.WriteLine($"[TryCompleteAsyncRead] Completed: IsActive={controlBlock.IsActive}, MessagesProcessed={controlBlock.MessagesProcessed}, HasTerminated={controlBlock.HasTerminated}, Errors={controlBlock.ErrorsEncountered}");
            return true;
        }
        else if (queryResult == CudaError.NotReady)
        {
            // Still pending
            return false;
        }
        else
        {
            // Error - clear pending flag and return cached value
            Trace.WriteLine($"[TryCompleteAsyncRead] FAILED: cudaEventQuery returned {queryResult}");
            asyncBlock.ReadPending = false;
            return false;
        }
    }

    /// <summary>
    /// Reads the control block with non-blocking async behavior.
    /// If an async read is pending, returns cached value and starts new read.
    /// If no async read is pending, starts one and returns cached value.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <returns>The most recent control block value (may be slightly stale).</returns>
    public static RingKernelControlBlock ReadNonBlocking(AsyncControlBlock asyncBlock)
    {
        // Try to complete any pending read first
        if (asyncBlock.ReadPending)
        {
            TryCompleteAsyncRead(asyncBlock, out _);
        }

        // Start a new async read if none pending
        if (!asyncBlock.ReadPending)
        {
            StartAsyncRead(asyncBlock);
        }

        // Return the cached value (may be from previous read or initial state)
        return asyncBlock.LastReadValue;
    }

    /// <summary>
    /// Blocking read that waits for async copy to complete.
    /// Use this for operations that require the latest value immediately.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>The current control block value.</returns>
    public static RingKernelControlBlock ReadAsyncBlocking(AsyncControlBlock asyncBlock, TimeSpan timeout)
    {
        // Complete any pending read
        if (asyncBlock.ReadPending)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (asyncBlock.ReadPending && DateTime.UtcNow < deadline)
            {
                if (TryCompleteAsyncRead(asyncBlock, out var result))
                {
                    return result;
                }
                Thread.SpinWait(100);
            }
        }

        // Start new read and wait for it
        if (StartAsyncRead(asyncBlock))
        {
            var deadline = DateTime.UtcNow + timeout;
            while (asyncBlock.ReadPending && DateTime.UtcNow < deadline)
            {
                if (TryCompleteAsyncRead(asyncBlock, out var result))
                {
                    return result;
                }
                Thread.SpinWait(100);
            }
        }

        return asyncBlock.LastReadValue;
    }

    /// <summary>
    /// Writes the control block non-blocking to device memory via async CUDA copy.
    /// If staging buffer is not pinned (WSL2 fallback mode), uses synchronous copy instead.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <param name="controlBlock">The control block state to write.</param>
    /// <returns>True if write was initiated/completed; false on error.</returns>
    public static bool WriteNonBlocking(AsyncControlBlock asyncBlock, RingKernelControlBlock controlBlock)
    {
        Trace.WriteLine($"[WriteNonBlocking] Writing: IsActive={controlBlock.IsActive}, ShouldTerminate={controlBlock.ShouldTerminate}, HasTerminated={controlBlock.HasTerminated}, InputQueuePtr=0x{controlBlock.InputQueueBufferPtr:X}");
        if (controlBlock.ShouldTerminate == 1)
        {
            Trace.WriteLine($"[WriteNonBlocking] ALERT: ShouldTerminate=1 being written! Stack trace:");
            Trace.WriteLine(Environment.StackTrace);
        }

        // Wait for any pending write to complete first (to avoid overwriting staging buffer)
        if (asyncBlock.WritePending)
        {
            var queryResult = CudaRuntime.cudaEventQuery(asyncBlock.WriteEvent);
            if (queryResult == CudaError.NotReady)
            {
                // Previous write still pending - sync on it
                CudaRuntime.cudaEventSynchronize(asyncBlock.WriteEvent);
            }

            asyncBlock.WritePending = false;
        }

        // Write to staging buffer
        unsafe
        {
            Unsafe.Write(asyncBlock.StagingBuffer.ToPointer(), controlBlock);
        }

        // Check if staging buffer is pinned - cudaMemcpyAsync requires pinned host memory
        if (!asyncBlock.IsStagingPinned)
        {
            // WSL2 fallback: Staging buffer is regular malloc - must use synchronous copy
            // cudaMemcpy (sync) works with non-pinned memory, cudaMemcpyAsync does not
            // CRITICAL: In WSL2 with infinite-loop kernels, cudaSetDevice blocks!
            // Skip cudaSetDevice for sync mode - use Driver API cudaMemcpyHtoD instead
            // which doesn't require device context switching

            // Use Driver API cuMemcpyHtoD - it doesn't require cudaSetDevice
            // The Driver API operates on contexts, not the "current device" like Runtime API
            var cuResult = CudaApi.cuMemcpyHtoD(
                asyncBlock.DevicePointer,
                asyncBlock.StagingBuffer,
                (nuint)asyncBlock.Size);

            if (cuResult != CudaError.Success)
            {
                // Fall back to synchronous cudaMemcpy with cudaSetDevice
                var setDeviceResult = CudaRuntime.cudaSetDevice(0);
                if (setDeviceResult != CudaError.Success)
                {
                    return false;
                }
                var syncCopyResult = CudaRuntime.cudaMemcpy(
                    asyncBlock.DevicePointer,
                    asyncBlock.StagingBuffer,
                    (nuint)asyncBlock.Size,
                    CudaMemcpyKind.HostToDevice);
                if (syncCopyResult != CudaError.Success)
                {
                    return false;
                }
            }

            // Copy completed - update cached value
            asyncBlock.LastReadValue = controlBlock;
            return true;
        }

        // WSL2 fix: Use Runtime API instead of Driver API for better compatibility
        // The Runtime API context management is more reliable in WSL2 where Driver API
        // context handles can become stale across different code paths
        var setDeviceResultPinned = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResultPinned != CudaError.Success)
        {
            return false;
        }

        // Pinned staging buffer - use async copy for non-blocking behavior
        var copyResult = CudaRuntime.cudaMemcpyAsync(
            asyncBlock.DevicePointer,
            asyncBlock.StagingBuffer,
            (ulong)asyncBlock.Size,
            CudaMemcpyKind.HostToDevice,
            asyncBlock.ControlStream);

        if (copyResult != CudaError.Success)
        {
            return false;
        }

        // Record event to track completion
        var eventResult = CudaRuntime.cudaEventRecord(asyncBlock.WriteEvent, asyncBlock.ControlStream);
        if (eventResult != CudaError.Success)
        {
            return false;
        }

        asyncBlock.WritePending = true;
        asyncBlock.LastReadValue = controlBlock; // Update cached value
        return true;
    }

    /// <summary>
    /// Sets the IsActive flag non-blocking using async CUDA copy.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <param name="active">True to activate, false to deactivate.</param>
    public static void SetActiveNonBlocking(AsyncControlBlock asyncBlock, bool active)
    {
        // Update cached value
        var controlBlock = asyncBlock.LastReadValue;
        controlBlock.IsActive = active ? 1 : 0;
        asyncBlock.LastReadValue = controlBlock;

        // Write the entire control block (simplest approach for WSL2)
        WriteNonBlocking(asyncBlock, controlBlock);
    }

    /// <summary>
    /// Sets the ShouldTerminate flag non-blocking via async CUDA copy.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    public static void SetTerminateNonBlocking(AsyncControlBlock asyncBlock)
    {
        // Update cached value
        var controlBlock = asyncBlock.LastReadValue;
        controlBlock.ShouldTerminate = 1;
        asyncBlock.LastReadValue = controlBlock;

        // Write the entire control block
        WriteNonBlocking(asyncBlock, controlBlock);
    }

    /// <summary>
    /// Waits for the kernel to set the HasTerminated flag using async reads.
    /// </summary>
    /// <param name="asyncBlock">The async control block.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if kernel terminated; false if timeout or cancelled.</returns>
    public static async Task<bool> WaitForTerminationAsync(
        AsyncControlBlock asyncBlock,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            // Try to get latest value
            var controlBlock = ReadNonBlocking(asyncBlock);

            if (controlBlock.HasTerminated != 0)
            {
                return true;
            }

            // Short delay between polls
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);

            // Try to complete any pending read
            TryCompleteAsyncRead(asyncBlock, out var updated);
            if (updated.HasTerminated != 0)
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
