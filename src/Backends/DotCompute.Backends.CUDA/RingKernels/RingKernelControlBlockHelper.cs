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
        IntPtr devicePtr = IntPtr.Zero;
        int controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        var allocResult = CudaApi.cuMemAlloc(ref devicePtr, (nuint)controlBlockSize);
        if (allocResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to allocate control block: {allocResult}");
        }

        // Initialize control block on host
        var controlBlock = RingKernelControlBlock.CreateInactive();

        // Copy to device
        IntPtr hostPtr = Marshal.AllocHGlobal(controlBlockSize);
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
    /// Reads the current state of a control block from device memory.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="devicePtr">Device pointer to the control block.</param>
    /// <returns>The control block state.</returns>
    public static RingKernelControlBlock Read(IntPtr context, IntPtr devicePtr)
    {
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        int controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        IntPtr hostPtr = Marshal.AllocHGlobal(controlBlockSize);

        try
        {
            var copyResult = CudaApi.cuMemcpyDtoH(hostPtr, devicePtr, (nuint)controlBlockSize);
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to read control block: {copyResult}");
            }

            // Ensure copy completes
            CudaRuntimeCore.cuCtxSynchronize();

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
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        int controlBlockSize = Unsafe.SizeOf<RingKernelControlBlock>();
        IntPtr hostPtr = Marshal.AllocHGlobal(controlBlockSize);

        try
        {
            unsafe
            {
                Unsafe.Write(hostPtr.ToPointer(), controlBlock);
            }

            var copyResult = CudaApi.cuMemcpyHtoD(devicePtr, hostPtr, (nuint)controlBlockSize);
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to write control block: {copyResult}");
            }

            // Ensure write completes
            CudaRuntimeCore.cuCtxSynchronize();
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
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        int value = active ? 1 : 0;
        IntPtr hostPtr = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            Marshal.WriteInt32(hostPtr, value);

            // Write just the IsActive field (offset 0, size 4)
            var copyResult = CudaApi.cuMemcpyHtoD(devicePtr, hostPtr, sizeof(int));
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set active flag: {copyResult}");
            }

            CudaRuntimeCore.cuCtxSynchronize();
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
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        IntPtr hostPtr = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            Marshal.WriteInt32(hostPtr, 1);

            // Write the ShouldTerminate field (offset 4, size 4)
            IntPtr terminateFlagPtr = devicePtr + 4;
            var copyResult = CudaApi.cuMemcpyHtoD(terminateFlagPtr, hostPtr, sizeof(int));
            if (copyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set terminate flag: {copyResult}");
            }

            CudaRuntimeCore.cuCtxSynchronize();
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
