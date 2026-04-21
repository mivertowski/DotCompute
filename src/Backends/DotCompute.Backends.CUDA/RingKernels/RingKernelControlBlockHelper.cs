// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
        if (!usePinnedMemory)
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
    /// <param name="controlStream">Non-blocking stream for the copy operation.</param>
    /// <returns>The control block state.</returns>
    public static RingKernelControlBlock Read(IntPtr context, IntPtr devicePtr, IntPtr controlStream = default)
    {
        // Use Runtime API for context setup to ensure compatibility with Runtime API allocated memory
        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA device: {setDeviceResult}");
        }

        var controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var hostPtr = Marshal.AllocHGlobal(controlBlockSize);

        try
        {
            // Use Runtime API (cudaMemcpy) for copies from Runtime API allocated memory
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
        // Use Runtime API for context setup to ensure compatibility with Runtime API allocated memory
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

            // Use Runtime API (cudaMemcpy) for copies to Runtime API allocated memory
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
        // Use Runtime API for context setup
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
            // Use Runtime API (cudaMemcpy) for copies to Runtime API allocated memory
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
        // Use Runtime API for context setup
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
            // Use Runtime API (cudaMemcpy) for copies to Runtime API allocated memory
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
}
