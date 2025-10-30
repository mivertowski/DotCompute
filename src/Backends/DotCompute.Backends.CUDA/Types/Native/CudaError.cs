// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Enumeration of CUDA error codes returned by the CUDA runtime API and driver API.
    /// These error codes provide detailed information about the success or failure of CUDA operations.
    /// </summary>
    /// <remarks>
    /// CUDA error codes are used throughout the CUDA APIs to indicate the status of operations.
    /// A return value of <see cref="Success"/> indicates that the operation completed successfully,
    /// while any other value indicates a specific type of error or failure condition.
    /// </remarks>
    public enum CudaError
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// One or more parameters passed to the API call are not within an acceptable range of values.
        /// </summary>
        InvalidValue = 1,

        /// <summary>
        /// The memory allocation operation failed due to insufficient memory.
        /// </summary>
        MemoryAllocation = 2,

        /// <summary>
        /// The system is out of memory.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1069:Enums values should not be duplicated",
            Justification = "NVIDIA CUDA API defines both MemoryAllocation and OutOfMemory with value 2 as equivalent error codes")]
        OutOfMemory = 2,

        /// <summary>
        /// The CUDA driver and runtime could not be initialized.
        /// </summary>
        InitializationError = 3,

        /// <summary>
        /// An exception occurred on the device while executing a kernel.
        /// </summary>
        LaunchFailure = 4,

        /// <summary>
        /// The kernel launch timed out.
        /// </summary>
        LaunchTimeout = 5,

        /// <summary>
        /// The kernel launch failed due to insufficient resources.
        /// </summary>
        LaunchOutOfResources = 6,

        /// <summary>
        /// The device function being invoked is not defined.
        /// </summary>
        InvalidDeviceFunction = 7,

        /// <summary>
        /// The launch configuration is invalid.
        /// </summary>
        InvalidConfiguration = 8,

        /// <summary>
        /// The device ordinal supplied by the user does not correspond to a valid CUDA device.
        /// </summary>
        InvalidDevice = 9,

        /// <summary>
        /// One or more parameters passed to the API call are not valid memory copy directions.
        /// </summary>
        InvalidMemcpyDirection = 10,

        /// <summary>
        /// The specified host pointer is invalid.
        /// </summary>
        InvalidHostPointer = 11,

        /// <summary>
        /// The specified device pointer is invalid.
        /// </summary>
        InvalidDevicePointer = 12,

        /// <summary>
        /// The texture is not bound.
        /// </summary>
        InvalidTexture = 13,

        /// <summary>
        /// The texture binding is invalid.
        /// </summary>
        InvalidTextureBinding = 14,

        /// <summary>
        /// The channel descriptor is invalid.
        /// </summary>
        InvalidChannelDescriptor = 15,

        /// <summary>
        /// The direction of the memory copy is invalid.
        /// </summary>
        InvalidMemcpyChannelDesc = 16,

        /// <summary>
        /// The filter setting is invalid.
        /// </summary>
        InvalidFilterSetting = 17,

        /// <summary>
        /// The normalization setting is invalid.
        /// </summary>
        InvalidNormSetting = 18,

        /// <summary>
        /// The operation attempted to mix device and non-device execution.
        /// </summary>
        MixedDeviceExecution = 19,

        /// <summary>
        /// The operation is not yet implemented.
        /// </summary>
        NotYetImplemented = 20,

        /// <summary>
        /// The memory value is too large.
        /// </summary>
        MemoryValueTooLarge = 21,

        /// <summary>
        /// The installed CUDA driver is insufficient for the CUDA runtime version.
        /// </summary>
        InsufficientDriver = 22,

        /// <summary>
        /// The surface is not bound.
        /// </summary>
        InvalidSurface = 23,

        /// <summary>
        /// A global with the same name already exists.
        /// </summary>
        DuplicateVariableName = 24,

        /// <summary>
        /// A texture with the same name already exists.
        /// </summary>
        DuplicateTextureName = 25,

        /// <summary>
        /// A surface with the same name already exists.
        /// </summary>
        DuplicateSurfaceName = 26,

        /// <summary>
        /// All CUDA-capable devices are busy or unavailable.
        /// </summary>
        DevicesUnavailable = 27,

        /// <summary>
        /// The device kernel image is invalid.
        /// </summary>
        InvalidKernelImage = 28,

        /// <summary>
        /// There is no kernel image available that is suitable for the device.
        /// </summary>
        NoKernelImageForDevice = 29,

        /// <summary>
        /// The driver context is incompatible with the current context.
        /// </summary>
        IncompatibleDriverContext = 30,

        /// <summary>
        /// Peer access has already been enabled from the current device.
        /// </summary>
        PeerAccessAlreadyEnabled = 31,

        /// <summary>
        /// Peer access has not been enabled from the current device.
        /// </summary>
        PeerAccessNotEnabled = 32,

        /// <summary>
        /// The device is already in use.
        /// </summary>
        DeviceAlreadyInUse = 33,

        /// <summary>
        /// The profiler is disabled.
        /// </summary>
        ProfilerDisabled = 34,

        /// <summary>
        /// The profiler is not initialized.
        /// </summary>
        ProfilerNotInitialized = 35,

        /// <summary>
        /// The profiler has already been started.
        /// </summary>
        ProfilerAlreadyStarted = 36,

        /// <summary>
        /// The profiler has already been stopped.
        /// </summary>
        ProfilerAlreadyStopped = 37,

        /// <summary>
        /// No CUDA-capable device is available.
        /// </summary>
        NoDevice = 38,

        /// <summary>
        /// The resource handle is invalid.
        /// </summary>
        InvalidHandle = 39,

        /// <summary>
        /// The resource was not found.
        /// </summary>
        NotFound = 40,

        /// <summary>
        /// The resource is not ready for the requested operation.
        /// </summary>
        NotReady = 41,

        /// <summary>
        /// An illegal memory access was encountered.
        /// </summary>
        IllegalAddress = 42,

        /// <summary>
        /// A kernel launch is being performed with incompatible texturing mode.
        /// </summary>
        LaunchIncompatibleTexturing = 43,

        /// <summary>
        /// Peer access is not supported across the given devices.
        /// </summary>
        PeerAccessUnsupported = 44,

        /// <summary>
        /// A PTX compilation failed.
        /// </summary>
        InvalidPtx = 45,

        /// <summary>
        /// The device kernel source is invalid.
        /// </summary>
        InvalidGraphicsContext = 46,

        /// <summary>
        /// A hardware error occurred that cannot be corrected.
        /// </summary>
        NvlinkUncorrectable = 47,

        /// <summary>
        /// PTX JIT compiler is not available.
        /// </summary>
        JitCompilerNotFound = 48,

        /// <summary>
        /// The device kernel source is invalid.
        /// </summary>
        InvalidSource = 49,

        /// <summary>
        /// The file is not found.
        /// </summary>
        FileNotFound = 50,

        /// <summary>
        /// A required symbol was not found in the shared object.
        /// </summary>
        SharedObjectSymbolNotFound = 51,

        /// <summary>
        /// Initialization of a shared object failed.
        /// </summary>
        SharedObjectInitFailed = 52,

        /// <summary>
        /// An OS call failed.
        /// </summary>
        OperatingSystem = 53,

        /// <summary>
        /// The resource handle is invalid.
        /// </summary>
        InvalidResourceHandle = 54,

        /// <summary>
        /// The operation is not supported.
        /// </summary>
        NotSupported = 55,

        /// <summary>
        /// The system is not ready.
        /// </summary>
        SystemNotReady = 56,

        /// <summary>
        /// The system driver and CUDA driver are not compatible.
        /// </summary>
        SystemDriverMismatch = 57,

        /// <summary>
        /// The operation is not supported on the current device.
        /// </summary>
        CompatNotSupportedOnDevice = 58,

        /// <summary>
        /// Stream capture is not supported.
        /// </summary>
        StreamCaptureUnsupported = 59,

        /// <summary>
        /// The stream capture was invalidated.
        /// </summary>
        StreamCaptureInvalidated = 60,

        /// <summary>
        /// A stream capture merge operation was attempted.
        /// </summary>
        StreamCaptureMerge = 61,

        /// <summary>
        /// The stream capture operation was unmatched.
        /// </summary>
        StreamCaptureUnmatched = 62,

        /// <summary>
        /// The stream capture operation was unjoined.
        /// </summary>
        StreamCaptureUnjoined = 63,

        /// <summary>
        /// A stream capture isolation error occurred.
        /// </summary>
        StreamCaptureIsolation = 64,

        /// <summary>
        /// An implicit stream capture error occurred.
        /// </summary>
        StreamCaptureImplicit = 65,

        /// <summary>
        /// The event was captured in a stream.
        /// </summary>
        CapturedEvent = 66,

        /// <summary>
        /// Stream capture was called from the wrong thread.
        /// </summary>
        StreamCaptureWrongThread = 67,

        /// <summary>
        /// The operation timed out.
        /// </summary>
        Timeout = 68,

        /// <summary>
        /// The graph execution update failed.
        /// </summary>
        GraphExecUpdateFailure = 69,

        /// <summary>
        /// An uncorrectable ECC error was detected during execution.
        /// This indicates a hardware problem that cannot be automatically corrected.
        /// </summary>
        EccUncorrectable = 214,

        /// <summary>
        /// A hardware stack error occurred.
        /// This indicates a problem with the hardware stack management system.
        /// </summary>
        HardwareStackError = 215,

        /// <summary>
        /// An illegal instruction was encountered during kernel execution.
        /// This indicates a problem with the kernel code or compilation.
        /// </summary>
        IllegalInstruction = 216,

        /// <summary>
        /// An unknown error occurred.
        /// </summary>
        Unknown = 999,

        // Aliases for common alternative names
        /// <summary>
        /// Alias for LaunchFailure - An exception occurred on the device while executing a kernel.
        /// </summary>
        LaunchFailed = LaunchFailure,

        /// <summary>
        /// The CUDA driver and runtime could not be initialized (alias for InitializationError).
        /// </summary>
        NotInitialized = InitializationError,

        /// <summary>
        /// The driver API context has been deinitialized.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1069:Enums values should not be duplicated",
            Justification = "NVIDIA CUDA API defines both LaunchFailure and Deinitialized with value 4 for historical reasons")]
        Deinitialized = 4,

        /// <summary>
        /// The context handle is invalid or has been destroyed.
        /// </summary>
        InvalidContext = 201,

        /// <summary>
        /// The context is being or has been destroyed.
        /// </summary>
        ContextIsDestroyed = 202,

        /// <summary>
        /// The operation is not permitted in this context.
        /// </summary>
        NotPermitted = 800,

        /// <summary>
        /// Too many peer-to-peer connections have been established.
        /// </summary>
        TooManyPeers = 217,

        /// <summary>
        /// The host memory is already registered with CUDA.
        /// </summary>
        HostMemoryAlreadyRegistered = 218,

        /// <summary>
        /// The operation has timed out (alias for Timeout).
        /// </summary>
        TimeoutExpired = Timeout,

        /// <summary>
        /// Missing launch configuration.
        /// </summary>
        MissingConfiguration = InvalidConfiguration
    }
}
