// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// Native OpenCL types and enumerations.
/// </summary>
public static class OpenCLTypes
{
    /// <summary>
    /// OpenCL platform ID handle.
    /// </summary>
    public readonly struct PlatformId
    {
        public readonly nint Handle;
        public PlatformId(nint handle) => Handle = handle;
        public static implicit operator nint(PlatformId platformId) => platformId.Handle;
        public static implicit operator PlatformId(nint handle) => new(handle);
        public override string ToString() => $"Platform[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL device ID handle.
    /// </summary>
    public readonly struct DeviceId
    {
        public readonly nint Handle;
        public DeviceId(nint handle) => Handle = handle;
        public static implicit operator nint(DeviceId deviceId) => deviceId.Handle;
        public static implicit operator DeviceId(nint handle) => new(handle);
        public override string ToString() => $"Device[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL context handle.
    /// </summary>
    public readonly struct Context
    {
        public readonly nint Handle;
        public Context(nint handle) => Handle = handle;
        public static implicit operator nint(Context context) => context.Handle;
        public static implicit operator Context(nint handle) => new(handle);
        public override string ToString() => $"Context[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL command queue handle.
    /// </summary>
    public readonly struct CommandQueue
    {
        public readonly nint Handle;
        public CommandQueue(nint handle) => Handle = handle;
        public static implicit operator nint(CommandQueue queue) => queue.Handle;
        public static implicit operator CommandQueue(nint handle) => new(handle);
        public override string ToString() => $"Queue[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL program handle.
    /// </summary>
    public readonly struct Program
    {
        public readonly nint Handle;
        public Program(nint handle) => Handle = handle;
        public static implicit operator nint(Program program) => program.Handle;
        public static implicit operator Program(nint handle) => new(handle);
        public override string ToString() => $"Program[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL kernel handle.
    /// </summary>
    public readonly struct Kernel
    {
        public readonly nint Handle;
        public Kernel(nint handle) => Handle = handle;
        public static implicit operator nint(Kernel kernel) => kernel.Handle;
        public static implicit operator Kernel(nint handle) => new(handle);
        public override string ToString() => $"Kernel[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL memory object handle.
    /// </summary>
    public readonly struct MemObject
    {
        public readonly nint Handle;
        public MemObject(nint handle) => Handle = handle;
        public static implicit operator nint(MemObject memObject) => memObject.Handle;
        public static implicit operator MemObject(nint handle) => new(handle);
        public override string ToString() => $"Buffer[{Handle:X}]";
    }

    /// <summary>
    /// OpenCL event handle.
    /// </summary>
    public readonly struct Event : IEquatable<Event>
    {
        public readonly nint Handle;
        public Event(nint handle) => Handle = handle;
        public static implicit operator nint(Event evt) => evt.Handle;
        public static implicit operator Event(nint handle) => new(handle);
        public override string ToString() => $"Event[{Handle:X}]";

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(Event other) => Handle == other.Handle;

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object? obj) => obj is Event other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() => Handle.GetHashCode();

        /// <summary>
        /// Indicates whether two instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are equal; otherwise, false.</returns>
        public static bool operator ==(Event left, Event right) => left.Equals(right);

        /// <summary>
        /// Indicates whether two instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are not equal; otherwise, false.</returns>
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
    All = 0xFFFFFFFF
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