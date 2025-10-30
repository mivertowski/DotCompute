// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace DotCompute.Backends.Metal.Native;


/// <summary>
/// Native interop for Metal API.
/// </summary>
public static partial class MetalNative
{
    private const string LibraryName = "libDotComputeMetal";

    #region Device Management

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_IsMetalSupported")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsMetalSupported();

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateSystemDefaultDevice")]
    public static partial IntPtr CreateSystemDefaultDevice();

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateDeviceAtIndex")]
    public static partial IntPtr CreateDeviceAtIndex(int index);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetDeviceCount")]
    public static partial int GetDeviceCount();

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseDevice")]
    public static partial void ReleaseDevice(IntPtr device);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetDeviceInfo")]
    public static partial MetalDeviceInfo GetDeviceInfo(IntPtr device);

    #endregion

    #region Command Queue

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateCommandQueue")]
    public static partial IntPtr CreateCommandQueue(IntPtr device);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseCommandQueue")]
    public static partial void ReleaseCommandQueue(IntPtr commandQueue);

    #endregion

    #region Command Buffer

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateCommandBuffer")]
    public static partial IntPtr CreateCommandBuffer(IntPtr commandQueue);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CommitCommandBuffer")]
    public static partial void CommitCommandBuffer(IntPtr commandBuffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_WaitUntilCompleted")]
    public static partial void WaitUntilCompleted(IntPtr commandBuffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseCommandBuffer")]
    public static partial void ReleaseCommandBuffer(IntPtr commandBuffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_SetCommandBufferCompletionHandler")]
    public static partial void SetCommandBufferCompletionHandler(IntPtr commandBuffer, CommandBufferCompletionHandler handler);

    #endregion

    #region Buffers

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateBuffer")]
    public static partial IntPtr CreateBuffer(IntPtr device, nuint length, MetalStorageMode storageMode);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateBufferWithBytes")]
    public static partial IntPtr CreateBufferWithBytes(IntPtr device, IntPtr bytes, nuint length, MetalStorageMode storageMode);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetBufferContents")]
    public static partial IntPtr GetBufferContents(IntPtr buffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetBufferLength")]
    public static partial nuint GetBufferLength(IntPtr buffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseBuffer")]
    public static partial void ReleaseBuffer(IntPtr buffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_DidModifyRange")]
    public static partial void DidModifyRange(IntPtr buffer, long offset, long length);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CopyBuffer")]
    public static partial void CopyBuffer(IntPtr source, long sourceOffset, IntPtr destination, long destOffset, long size);

    #endregion

    #region Compilation

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateCompileOptions")]
    public static partial IntPtr CreateCompileOptions();

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_SetCompileOptionsFastMath")]
    public static partial void SetCompileOptionsFastMath(IntPtr options, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_SetCompileOptionsLanguageVersion")]
    public static partial void SetCompileOptionsLanguageVersion(IntPtr options, MetalLanguageVersion version);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseCompileOptions")]
    public static partial void ReleaseCompileOptions(IntPtr options);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CompileLibrary", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr CompileLibrary(IntPtr device, string source, IntPtr options, ref IntPtr error);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateLibraryWithSource", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr CreateLibraryWithSource(IntPtr device, string source);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseLibrary")]
    public static partial void ReleaseLibrary(IntPtr library);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetFunction", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GetFunction(IntPtr library, string functionName);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseFunction")]
    public static partial void ReleaseFunction(IntPtr function);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetLibraryDataSize")]
    public static partial int GetLibraryDataSize(IntPtr library);


    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetLibraryData")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetLibraryData(IntPtr library, IntPtr buffer, int bufferSize);

    #endregion

    #region Pipeline State

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateComputePipelineState")]
    public static partial IntPtr CreateComputePipelineState(IntPtr device, IntPtr function, ref IntPtr error);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateComputePipelineState")]
    public static partial IntPtr CreateComputePipelineState(IntPtr device, IntPtr function);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseComputePipelineState")]
    public static partial void ReleaseComputePipelineState(IntPtr pipelineState);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleasePipelineState")]
    public static partial void ReleasePipelineState(IntPtr pipelineState);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetMaxTotalThreadsPerThreadgroup")]
    public static partial int GetMaxTotalThreadsPerThreadgroup(IntPtr pipelineState);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetThreadExecutionWidth")]
    public static partial void GetThreadExecutionWidth(IntPtr pipelineState, out int x, out int y, out int z);

    // Convenience method that returns tuple
    public static (int x, int y, int z) GetThreadExecutionWidthTuple(IntPtr pipelineState)
    {
        GetThreadExecutionWidth(pipelineState, out var x, out var y, out var z);
        return (x, y, z);
    }

    #endregion

    #region Compute Command Encoder

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateComputeCommandEncoder")]
    public static partial IntPtr CreateComputeCommandEncoder(IntPtr commandBuffer);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_SetComputePipelineState")]
    public static partial void SetComputePipelineState(IntPtr encoder, IntPtr pipelineState);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_SetBuffer")]
    public static partial void SetBuffer(IntPtr encoder, IntPtr buffer, nuint offset, int index);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_SetBytes")]
    public static partial void SetBytes(IntPtr encoder, IntPtr bytes, nuint length, int index);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_DispatchThreadgroups")]
    public static partial void DispatchThreadgroups(IntPtr encoder, MetalSize gridSize, MetalSize threadgroupSize);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_EndEncoding")]
    public static partial void EndEncoding(IntPtr encoder);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseEncoder")]
    public static partial void ReleaseEncoder(IntPtr encoder);

    #endregion

    #region Error Handling

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_GetErrorLocalizedDescription")]
    public static partial IntPtr GetErrorLocalizedDescription(IntPtr error);

    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseError")]
    public static partial void ReleaseError(IntPtr error);

    #endregion
}

#region Native Structures

[StructLayout(LayoutKind.Sequential)]
public struct MetalDeviceInfo
{
    public IntPtr Name;
    public ulong RegistryID;
    public MetalDeviceLocation Location;
    public ulong LocationNumber;
    public ulong MaxThreadgroupSize;
    public ulong MaxThreadsPerThreadgroup;
    public ulong MaxBufferLength;
    public ulong RecommendedMaxWorkingSetSize;
    [MarshalAs(UnmanagedType.Bool)]
    public bool HasUnifiedMemory;
    [MarshalAs(UnmanagedType.Bool)]
    public bool IsLowPower;
    [MarshalAs(UnmanagedType.Bool)]
    public bool IsRemovable;
    public IntPtr SupportedFamilies;
}

[StructLayout(LayoutKind.Sequential)]
public struct MetalSize
{
    public nuint width;
    public nuint height;
    public nuint depth;
}

#endregion

#region Enums

public enum MetalStorageMode
{
    Shared = 0,
    Private = 1,
    Managed = 2,
    Memoryless = 3
}

// Note: uint is required for Metal API compatibility
#pragma warning disable CA1028 // Enum underlying type must be uint for Metal API interop
public enum MetalDeviceLocation : uint
#pragma warning restore CA1028
{
    BuiltIn = 0,
    Slot = 1,
    External = 2,
    Unspecified = uint.MaxValue
}

public enum MetalCommandBufferStatus
{
    NotEnqueued = 0,
    Enqueued = 1,
    Committed = 2,
    Scheduled = 3,
    Completed = 4,
    Error = 5
}

public enum MetalLanguageVersion
{
    Metal10 = 0x10000,
    Metal11 = 0x10100,
    Metal12 = 0x10200,
    Metal20 = 0x20000,
    Metal21 = 0x20100,
    Metal22 = 0x20200,
    Metal23 = 0x20300,
    Metal24 = 0x20400,
    Metal30 = 0x30000,
    Metal31 = 0x30100
}

#endregion

#region Delegates

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void CommandBufferCompletionHandler(MetalCommandBufferStatus status);

#endregion
