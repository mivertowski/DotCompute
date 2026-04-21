// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.ErrorHandling;

/// <summary>
/// Severity / recovery classification of a <see cref="CudaError"/>.
/// </summary>
/// <remarks>
/// Inspired by RustCompute's <c>RingKernelError</c> classification model.
/// The classification drives retry policy, circuit-breaker decisions, and
/// caller-side recovery (e.g. release cached buffers and retry on Resource,
/// surface to user on Fatal, blind retry on Transient).
/// </remarks>
public enum CudaErrorClass
{
    /// <summary>The operation succeeded.</summary>
    Success,

    /// <summary>
    /// Transient: retrying the same operation may succeed without any change of state.
    /// Examples: NotReady, Timeout, SystemNotReady.
    /// </summary>
    Transient,

    /// <summary>
    /// Resource exhaustion: the operation may succeed after freeing resources or waiting
    /// for the device to drain in-flight work. Examples: OOM, LaunchOutOfResources, DevicesUnavailable.
    /// </summary>
    Resource,

    /// <summary>
    /// Programmer / configuration error: the call site is wrong; retrying with the same
    /// arguments cannot help. Examples: InvalidValue, InvalidConfiguration, NotSupported.
    /// </summary>
    Programmer,

    /// <summary>
    /// Fatal: the device, driver, or context is in an irrecoverable state. The handler
    /// should fail fast and surface to the caller; circuit breaker should open.
    /// Examples: IllegalAddress, EccUncorrectable, NoDevice, InitializationError.
    /// </summary>
    Fatal,
}

/// <summary>
/// Classification helpers for <see cref="CudaError"/>.
/// </summary>
public static class CudaErrorClassification
{
    /// <summary>
    /// Returns the recovery classification for a CUDA error code.
    /// </summary>
    public static CudaErrorClass Classify(this CudaError error) => error switch
    {
        CudaError.Success => CudaErrorClass.Success,

        // Transient — retry without state change
        CudaError.NotReady
            or CudaError.Timeout
            or CudaError.LaunchTimeout
            or CudaError.SystemNotReady
            or CudaError.PeerAccessNotEnabled
            or CudaError.StreamCaptureWrongThread => CudaErrorClass.Transient,

        // Resource — caller should free / wait then retry
        CudaError.MemoryAllocation
            or CudaError.LaunchOutOfResources
            or CudaError.DevicesUnavailable
            or CudaError.DeviceAlreadyInUse
            or CudaError.TooManyPeers
            or CudaError.HostMemoryAlreadyRegistered
            or CudaError.MemoryValueTooLarge => CudaErrorClass.Resource,

        // Fatal — irrecoverable hardware / driver / context state
        CudaError.IllegalAddress
            or CudaError.IllegalInstruction
            or CudaError.EccUncorrectable
            or CudaError.NvlinkUncorrectable
            or CudaError.HardwareStackError
            or CudaError.NoDevice
            or CudaError.InitializationError
            or CudaError.Deinitialized
            or CudaError.InvalidPtx
            or CudaError.InvalidKernelImage
            or CudaError.NoKernelImageForDevice
            or CudaError.IncompatibleDriverContext
            or CudaError.InsufficientDriver
            or CudaError.SystemDriverMismatch
            or CudaError.ContextIsDestroyed
            or CudaError.LaunchFailure
            or CudaError.JitCompilerNotFound
            or CudaError.SharedObjectInitFailed
            or CudaError.SharedObjectSymbolNotFound => CudaErrorClass.Fatal,

        // Everything else is treated as a programmer/config error.
        _ => CudaErrorClass.Programmer,
    };

    /// <summary>
    /// True if the error may succeed when retried after waiting briefly,
    /// without releasing any resources. Equivalent to <see cref="CudaErrorClass.Transient"/>.
    /// </summary>
    public static bool IsRecoverable(this CudaError error) => error.Classify() == CudaErrorClass.Transient;

    /// <summary>
    /// True if the error stems from resource exhaustion (memory, device slots, etc.).
    /// Caller should free what it can and retry.
    /// </summary>
    public static bool IsResource(this CudaError error) => error.Classify() == CudaErrorClass.Resource;

    /// <summary>
    /// True if the error indicates an irrecoverable device or driver state. The circuit
    /// breaker should open and the operation should fail fast.
    /// </summary>
    public static bool IsFatal(this CudaError error) => error.Classify() == CudaErrorClass.Fatal;

    /// <summary>
    /// True if the error indicates a caller / configuration mistake (no point retrying with
    /// the same arguments).
    /// </summary>
    public static bool IsProgrammerError(this CudaError error) => error.Classify() == CudaErrorClass.Programmer;

    /// <summary>
    /// True if the operation is safe to retry — either transient or resource-bounded.
    /// Resource errors are retryable when paired with a cleanup strategy.
    /// </summary>
    public static bool IsRetryable(this CudaError error)
    {
        var c = error.Classify();
        return c == CudaErrorClass.Transient || c == CudaErrorClass.Resource;
    }
}
