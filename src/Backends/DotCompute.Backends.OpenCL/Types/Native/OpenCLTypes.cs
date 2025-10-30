// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// Native OpenCL types and enumerations.
/// </summary>
/// <remarks>
/// Type aliases for backward compatibility. Use the individual types directly for new code.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Do not nest type", Justification = "Backward compatibility wrapper")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2225:Provide alternate methods for operators", Justification = "Backward compatibility wrapper - operators provided on non-nested types")]
public static class OpenCLTypes
{
    /// <summary>
    /// Type alias for <see cref="OpenCLPlatformId"/>.
    /// </summary>
    public readonly struct PlatformId
    {
        private readonly OpenCLPlatformId _inner;
        public nint Handle => _inner.Handle;
        public PlatformId(nint handle) => _inner = new OpenCLPlatformId(handle);
        public static implicit operator nint(PlatformId platformId) => platformId._inner.Handle;
        public static implicit operator PlatformId(nint handle) => new(handle);
        public static implicit operator OpenCLPlatformId(PlatformId platformId) => platformId._inner;
        public static implicit operator PlatformId(OpenCLPlatformId platformId) => new(platformId.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLDeviceId"/>.
    /// </summary>
    public readonly struct DeviceId
    {
        private readonly OpenCLDeviceId _inner;
        public nint Handle => _inner.Handle;
        public DeviceId(nint handle) => _inner = new OpenCLDeviceId(handle);
        public static implicit operator nint(DeviceId deviceId) => deviceId._inner.Handle;
        public static implicit operator DeviceId(nint handle) => new(handle);
        public static implicit operator OpenCLDeviceId(DeviceId deviceId) => deviceId._inner;
        public static implicit operator DeviceId(OpenCLDeviceId deviceId) => new(deviceId.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLContextHandle"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Backward compatibility wrapper")]
    public readonly struct Context
    {
        private readonly OpenCLContextHandle _inner;
        public nint Handle => _inner.Handle;
        public Context(nint handle) => _inner = new OpenCLContextHandle(handle);
        public static implicit operator nint(Context context) => context._inner.Handle;
        public static implicit operator Context(nint handle) => new(handle);
        public static implicit operator OpenCLContextHandle(Context context) => context._inner;
        public static implicit operator Context(OpenCLContextHandle context) => new(context.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLCommandQueue"/>.
    /// </summary>
    public readonly struct CommandQueue
    {
        private readonly OpenCLCommandQueue _inner;
        public nint Handle => _inner.Handle;
        public CommandQueue(nint handle) => _inner = new OpenCLCommandQueue(handle);
        public static implicit operator nint(CommandQueue queue) => queue._inner.Handle;
        public static implicit operator CommandQueue(nint handle) => new(handle);
        public static implicit operator OpenCLCommandQueue(CommandQueue queue) => queue._inner;
        public static implicit operator CommandQueue(OpenCLCommandQueue queue) => new(queue.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLProgram"/>.
    /// </summary>
    public readonly struct Program
    {
        private readonly OpenCLProgram _inner;
        public nint Handle => _inner.Handle;
        public Program(nint handle) => _inner = new OpenCLProgram(handle);
        public static implicit operator nint(Program program) => program._inner.Handle;
        public static implicit operator Program(nint handle) => new(handle);
        public static implicit operator OpenCLProgram(Program program) => program._inner;
        public static implicit operator Program(OpenCLProgram program) => new(program.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLKernel"/>.
    /// </summary>
    public readonly struct Kernel
    {
        private readonly OpenCLKernel _inner;
        public nint Handle => _inner.Handle;
        public Kernel(nint handle) => _inner = new OpenCLKernel(handle);
        public static implicit operator nint(Kernel kernel) => kernel._inner.Handle;
        public static implicit operator Kernel(nint handle) => new(handle);
        public static implicit operator OpenCLKernel(Kernel kernel) => kernel._inner;
        public static implicit operator Kernel(OpenCLKernel kernel) => new(kernel.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLMemObject"/>.
    /// </summary>
    public readonly struct MemObject
    {
        private readonly OpenCLMemObject _inner;
        public nint Handle => _inner.Handle;
        public MemObject(nint handle) => _inner = new OpenCLMemObject(handle);
        public static implicit operator nint(MemObject memObject) => memObject._inner.Handle;
        public static implicit operator MemObject(nint handle) => new(handle);
        public static implicit operator OpenCLMemObject(MemObject memObject) => memObject._inner;
        public static implicit operator MemObject(OpenCLMemObject memObject) => new(memObject.Handle);
        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Type alias for <see cref="OpenCLEventHandle"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Event", Justification = "Backward compatibility wrapper")]
    public readonly struct Event : IEquatable<Event>
    {
        private readonly OpenCLEventHandle _inner;
        public nint Handle => _inner.Handle;
        public Event(nint handle) => _inner = new OpenCLEventHandle(handle);
        public static implicit operator nint(Event evt) => evt._inner.Handle;
        public static implicit operator Event(nint handle) => new(handle);
        public static implicit operator OpenCLEventHandle(Event evt) => evt._inner;
        public static implicit operator Event(OpenCLEventHandle evt) => new(evt.Handle);
        public override string ToString() => _inner.ToString();
        public bool Equals(Event other) => _inner.Equals(other._inner);
        public override bool Equals(object? obj) => obj is Event other && Equals(other);
        public override int GetHashCode() => _inner.GetHashCode();
        public static bool operator ==(Event left, Event right) => left.Equals(right);
        public static bool operator !=(Event left, Event right) => !left.Equals(right);
    }
}

/// <summary>
/// OpenCL error codes.
/// </summary>
public enum OpenCLError : int
{
    Success = 0,
    DeviceNotFound = -1,
    DeviceNotAvailable = -2,
    CompilerNotAvailable = -3,
    MemObjectAllocationFailure = -4,
    OutOfResources = -5,
    OutOfHostMemory = -6,
    ProfilingInfoNotAvailable = -7,
    MemCopyOverlap = -8,
    ImageFormatMismatch = -9,
    ImageFormatNotSupported = -10,
    BuildProgramFailure = -11,
    MapFailure = -12,
    MisalignedSubBufferOffset = -13,
    ExecStatusErrorForEventsInWaitList = -14,
    CompileProgramFailure = -15,
    LinkerNotAvailable = -16,
    LinkProgramFailure = -17,
    DevicePartitionFailed = -18,
    KernelArgInfoNotAvailable = -19,
    InvalidValue = -30,
    InvalidDeviceType = -31,
    InvalidPlatform = -32,
    InvalidDevice = -33,
    InvalidContext = -34,
    InvalidQueueProperties = -35,
    InvalidCommandQueue = -36,
    InvalidHostPtr = -37,
    InvalidMemObject = -38,
    InvalidImageFormatDescriptor = -39,
    InvalidImageSize = -40,
    InvalidSampler = -41,
    InvalidBinary = -42,
    InvalidBuildOptions = -43,
    InvalidProgram = -44,
    InvalidProgramExecutable = -45,
    InvalidKernelName = -46,
    InvalidKernelDefinition = -47,
    InvalidKernel = -48,
    InvalidArgIndex = -49,
    InvalidArgValue = -50,
    InvalidArgSize = -51,
    InvalidKernelArgs = -52,
    InvalidWorkDimension = -53,
    InvalidWorkGroupSize = -54,
    InvalidWorkItemSize = -55,
    InvalidGlobalOffset = -56,
    InvalidEventWaitList = -57,
    InvalidEvent = -58,
    InvalidOperation = -59,
    InvalidGLObject = -60,
    InvalidBufferSize = -61,
    InvalidMipLevel = -62,
    InvalidGlobalWorkSize = -63,
    InvalidProperty = -64,
    InvalidImageDescriptor = -65,
    InvalidCompilerOptions = -66,
    InvalidLinkerOptions = -67,
    InvalidDevicePartitionCount = -68
}

/// <summary>
/// OpenCL device types.
/// </summary>
[Flags]
public enum DeviceType : ulong
{
    Default = 1 << 0,
    CPU = 1 << 1,
    GPU = 1 << 2,
    Accelerator = 1 << 3,
    Custom = 1 << 4,
    All = Default | CPU | GPU | Accelerator | Custom
}

/// <summary>
/// OpenCL memory flags.
/// </summary>
[Flags]
public enum MemoryFlags : ulong
{
    ReadWrite = 1 << 0,
    WriteOnly = 1 << 1,
    ReadOnly = 1 << 2,
    UseHostPtr = 1 << 3,
    AllocHostPtr = 1 << 4,
    CopyHostPtr = 1 << 5,
    HostWriteOnly = 1 << 7,
    HostReadOnly = 1 << 8,
    HostNoAccess = 1 << 9
}

/// <summary>
/// OpenCL platform information.
/// </summary>
public enum PlatformInfo : uint
{
    Profile = 0x0900,
    Version = 0x0901,
    Name = 0x0902,
    Vendor = 0x0903,
    Extensions = 0x0904
}

/// <summary>
/// OpenCL device information.
/// </summary>
public enum DeviceInfo : uint
{
    Type = 0x1000,
    VendorId = 0x1001,
    MaxComputeUnits = 0x1002,
    MaxWorkItemDimensions = 0x1003,
    MaxWorkGroupSize = 0x1004,
    MaxWorkItemSizes = 0x1005,
    PreferredVectorWidthChar = 0x1006,
    PreferredVectorWidthShort = 0x1007,
    PreferredVectorWidthInt = 0x1008,
    PreferredVectorWidthLong = 0x1009,
    PreferredVectorWidthFloat = 0x100A,
    PreferredVectorWidthDouble = 0x100B,
    MaxClockFrequency = 0x100C,
    AddressBits = 0x100D,
    MaxReadImageArgs = 0x100E,
    MaxWriteImageArgs = 0x100F,
    MaxMemAllocSize = 0x1010,
    Image2DMaxWidth = 0x1011,
    Image2DMaxHeight = 0x1012,
    Image3DMaxWidth = 0x1013,
    Image3DMaxHeight = 0x1014,
    Image3DMaxDepth = 0x1015,
    ImageSupport = 0x1016,
    MaxParameterSize = 0x1017,
    MaxSamplers = 0x1018,
    MemBaseAddrAlign = 0x1019,
    MinDataTypeAlignSize = 0x101A,
    SingleFpConfig = 0x101B,
    GlobalMemCacheType = 0x101C,
    GlobalMemCachelineSize = 0x101D,
    GlobalMemCacheSize = 0x101E,
    GlobalMemSize = 0x101F,
    MaxConstantBufferSize = 0x1020,
    MaxConstantArgs = 0x1021,
    LocalMemType = 0x1022,
    LocalMemSize = 0x1023,
    ErrorCorrectionSupport = 0x1024,
    ProfilingTimerResolution = 0x1025,
    EndianLittle = 0x1026,
    Available = 0x1027,
    CompilerAvailable = 0x1028,
    ExecutionCapabilities = 0x1029,
    QueueProperties = 0x102A,
    Name = 0x102B,
    Vendor = 0x102C,
    DriverVersion = 0x102D,
    Profile = 0x102E,
    Version = 0x102F,
    Extensions = 0x1030
}
