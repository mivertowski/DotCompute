// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotCompute.Core.Kernels;

/// <summary>
/// DirectCompute interoperability layer providing P/Invoke definitions for Direct3D 11 compute shaders.
/// This implementation targets DirectX 11 on Windows for compute shader execution.
/// </summary>
internal static partial class DirectComputeInterop
{
    // Constants for Direct3D 11
    public const int D3D11_SDK_VERSION = 7;
    public const uint DXGI_ERROR_UNSUPPORTED = 0x887A0001;
    public const uint DXGI_ERROR_NOT_FOUND = 0x887A0002;
    public const uint DXGI_ERROR_DEVICE_REMOVED = 0x887A0005;
    public const uint DXGI_ERROR_DEVICE_RESET = 0x887A0007;
    
    // D3D11 Create Device Flags
    public const uint D3D11_CREATE_DEVICE_DEBUG = 0x2;
    public const uint D3D11_CREATE_DEVICE_SWITCH_TO_REF = 0x4;
    public const uint D3D11_CREATE_DEVICE_PREVENT_INTERNAL_THREADING_OPTIMIZATIONS = 0x8;
    
    // Buffer flags
    public const uint D3D11_BIND_UNORDERED_ACCESS = 0x80;
    public const uint D3D11_BIND_SHADER_RESOURCE = 0x8;
    public const uint D3D11_BIND_CONSTANT_BUFFER = 0x4;
    
    // Resource usage
    public const uint D3D11_USAGE_DEFAULT = 0;
    public const uint D3D11_USAGE_IMMUTABLE = 1;
    public const uint D3D11_USAGE_DYNAMIC = 2;
    public const uint D3D11_USAGE_STAGING = 3;
    
    // CPU access flags
    public const uint D3D11_CPU_ACCESS_WRITE = 0x10000;
    public const uint D3D11_CPU_ACCESS_READ = 0x20000;
    
    // Resource misc flags
    public const uint D3D11_RESOURCE_MISC_BUFFER_STRUCTURED = 0x40;
    public const uint D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS = 0x20;
    
    // Map types
    public const uint D3D11_MAP_READ = 1;
    public const uint D3D11_MAP_WRITE = 2;
    public const uint D3D11_MAP_READ_WRITE = 3;
    public const uint D3D11_MAP_WRITE_DISCARD = 4;
    public const uint D3D11_MAP_WRITE_NO_OVERWRITE = 5;
    
    // Query types
    public const uint D3D11_QUERY_TIMESTAMP = 3;
    public const uint D3D11_QUERY_TIMESTAMP_DISJOINT = 4;
    
    // UAV dimension
    public const uint D3D11_UAV_DIMENSION_BUFFER = 1;
    
    // SRV dimension
    public const uint D3D11_SRV_DIMENSION_BUFFER = 1;

    // Compile flags for D3DCompile
    public const uint D3DCOMPILE_DEBUG = 1 << 0;
    public const uint D3DCOMPILE_SKIP_VALIDATION = 1 << 1;
    public const uint D3DCOMPILE_SKIP_OPTIMIZATION = 1 << 2;
    public const uint D3DCOMPILE_PACK_MATRIX_ROW_MAJOR = 1 << 3;
    public const uint D3DCOMPILE_PACK_MATRIX_COLUMN_MAJOR = 1 << 4;
    public const uint D3DCOMPILE_PARTIAL_PRECISION = 1 << 5;
    public const uint D3DCOMPILE_OPTIMIZATION_LEVEL0 = 1 << 14;
    public const uint D3DCOMPILE_OPTIMIZATION_LEVEL1 = 0;
    public const uint D3DCOMPILE_OPTIMIZATION_LEVEL2 = (1 << 14) | (1 << 15);
    public const uint D3DCOMPILE_OPTIMIZATION_LEVEL3 = 1 << 15;

    // Error checking
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFailed(int hr, string operation)
    {
        if (hr < 0)
        {
            throw new InvalidOperationException($"DirectCompute operation '{operation}' failed with HRESULT 0x{hr:X8}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Succeeded(int hr) => hr >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Failed(int hr) => hr < 0;

    // Feature levels
    public enum D3D_FEATURE_LEVEL : uint
    {
        D3D_FEATURE_LEVEL_11_0 = 0xb000,
        D3D_FEATURE_LEVEL_11_1 = 0xb100,
        D3D_FEATURE_LEVEL_12_0 = 0xc000,
        D3D_FEATURE_LEVEL_12_1 = 0xc100
    }

    // Driver types
    public enum D3D_DRIVER_TYPE : uint
    {
        D3D_DRIVER_TYPE_UNKNOWN = 0,
        D3D_DRIVER_TYPE_HARDWARE = 1,
        D3D_DRIVER_TYPE_REFERENCE = 2,
        D3D_DRIVER_TYPE_NULL = 3,
        D3D_DRIVER_TYPE_SOFTWARE = 4,
        D3D_DRIVER_TYPE_WARP = 5
    }

    // Structures
    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BUFFER_DESC
    {
        public uint ByteWidth;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
        public uint StructureByteStride;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_UNORDERED_ACCESS_VIEW_DESC
    {
        public uint Format; // DXGI_FORMAT
        public uint ViewDimension; // D3D11_UAV_DIMENSION
        public D3D11_BUFFER_UAV Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BUFFER_UAV
    {
        public uint FirstElement;
        public uint NumElements;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_SHADER_RESOURCE_VIEW_DESC
    {
        public uint Format; // DXGI_FORMAT
        public uint ViewDimension; // D3D11_SRV_DIMENSION
        public D3D11_BUFFER_SRV Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BUFFER_SRV
    {
        public uint FirstElement;
        public uint NumElements;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_MAPPED_SUBRESOURCE
    {
        public IntPtr pData;
        public uint RowPitch;
        public uint DepthPitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_QUERY_DESC
    {
        public uint Query; // D3D11_QUERY
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_QUERY_DATA_TIMESTAMP_DISJOINT
    {
        public ulong Frequency;
        public uint Disjoint; // BOOL
    }

    // COM Interface GUIDs
    public static readonly Guid IID_ID3D11Device = new("db6f6ddb-ac77-4e88-8253-819df9bbf140");
    public static readonly Guid IID_ID3D11DeviceContext = new("c0bfa96c-e089-44fb-8eaf-26f8796190da");
    public static readonly Guid IID_ID3D11Buffer = new("48570b85-d1ee-4fcd-a250-eb350722b037");
    public static readonly Guid IID_ID3D11ComputeShader = new("4f5b196e-c2bd-495e-bd01-1fded38e4969");
    public static readonly Guid IID_ID3D11UnorderedAccessView = new("28acf509-7f5c-48f6-8611-f316010a6380");
    public static readonly Guid IID_ID3D11ShaderResourceView = new("b0e06fe0-8192-4e1a-b1ca-36d7414710b2");
    public static readonly Guid IID_ID3D11Query = new("d6c00747-87b7-425e-b84d-44d108560acd");

    // P/Invoke declarations for D3D11.dll
#if NET7_0_OR_GREATER && WINDOWS
    [LibraryImport("d3d11.dll", EntryPoint = "D3D11CreateDevice")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial int D3D11CreateDevice(
        IntPtr pAdapter,
        D3D_DRIVER_TYPE DriverType,
        IntPtr Software,
        uint Flags,
        [In] D3D_FEATURE_LEVEL[] pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        out D3D_FEATURE_LEVEL pFeatureLevel,
        out IntPtr ppImmediateContext);
#else
    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        D3D_DRIVER_TYPE DriverType,
        IntPtr Software,
        uint Flags,
        [In] D3D_FEATURE_LEVEL[] pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        out D3D_FEATURE_LEVEL pFeatureLevel,
        out IntPtr ppImmediateContext);
#endif

    // P/Invoke declarations for D3DCompiler.dll
#if NET7_0_OR_GREATER && WINDOWS
    [LibraryImport("d3dcompiler_47.dll", EntryPoint = "D3DCompile", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial int D3DCompile(
        [MarshalAs(UnmanagedType.LPStr)] string pSrcData,
        nuint SrcDataSize,
        [MarshalAs(UnmanagedType.LPStr)] string? pSourceName,
        IntPtr pDefines,
        IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1,
        uint Flags2,
        out IntPtr ppCode,
        out IntPtr ppErrorMsgs);
#else
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int D3DCompile(
        [MarshalAs(UnmanagedType.LPStr)] string pSrcData,
        nuint SrcDataSize,
        [MarshalAs(UnmanagedType.LPStr)] string? pSourceName,
        IntPtr pDefines,
        IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1,
        uint Flags2,
        out IntPtr ppCode,
        out IntPtr ppErrorMsgs);
#endif

    // COM interface wrappers for type safety
    [ComImport]
    [Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ID3D11Device
    {
        [PreserveSig]
        int CreateBuffer([In] ref D3D11_BUFFER_DESC pDesc, IntPtr pInitialData, out IntPtr ppBuffer);

        [PreserveSig]
        int CreateTexture1D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture1D);

        [PreserveSig]
        int CreateTexture2D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);

        [PreserveSig]
        int CreateTexture3D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture3D);

        [PreserveSig]
        int CreateShaderResourceView(IntPtr pResource, [In] ref D3D11_SHADER_RESOURCE_VIEW_DESC pDesc, out IntPtr ppSRView);

        [PreserveSig]
        int CreateUnorderedAccessView(IntPtr pResource, [In] ref D3D11_UNORDERED_ACCESS_VIEW_DESC pDesc, out IntPtr ppUAView);

        [PreserveSig]
        int CreateRenderTargetView(IntPtr pResource, IntPtr pDesc, out IntPtr ppRTView);

        [PreserveSig]
        int CreateDepthStencilView(IntPtr pResource, IntPtr pDesc, out IntPtr ppDepthStencilView);

        [PreserveSig]
        int CreateInputLayout(IntPtr pInputElementDescs, uint NumElements, IntPtr pShaderBytecodeWithInputSignature, nuint BytecodeLength, out IntPtr ppInputLayout);

        [PreserveSig]
        int CreateVertexShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppVertexShader);

        [PreserveSig]
        int CreateGeometryShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppGeometryShader);

        [PreserveSig]
        int CreateGeometryShaderWithStreamOutput(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pSODeclaration, uint NumEntries, IntPtr pBufferStrides, uint NumStrides, uint RasterizedStream, IntPtr pClassLinkage, out IntPtr ppGeometryShader);

        [PreserveSig]
        int CreatePixelShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppPixelShader);

        [PreserveSig]
        int CreateHullShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppHullShader);

        [PreserveSig]
        int CreateDomainShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppDomainShader);

        [PreserveSig]
        int CreateComputeShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppComputeShader);

        [PreserveSig]
        int CreateClassLinkage(out IntPtr ppLinkage);

        [PreserveSig]
        int CreateBlendState(IntPtr pBlendStateDesc, out IntPtr ppBlendState);

        [PreserveSig]
        int CreateDepthStencilState(IntPtr pDepthStencilDesc, out IntPtr ppDepthStencilState);

        [PreserveSig]
        int CreateRasterizerState(IntPtr pRasterizerDesc, out IntPtr ppRasterizerState);

        [PreserveSig]
        int CreateSamplerState(IntPtr pSamplerDesc, out IntPtr ppSamplerState);

        [PreserveSig]
        int CreateQuery([In] ref D3D11_QUERY_DESC pQueryDesc, out IntPtr ppQuery);

        [PreserveSig]
        int CreatePredicate(IntPtr pPredicateDesc, out IntPtr ppPredicate);

        [PreserveSig]
        int CreateCounter(IntPtr pCounterDesc, out IntPtr ppCounter);

        [PreserveSig]
        int CreateDeferredContext(uint ContextFlags, out IntPtr ppDeferredContext);

        [PreserveSig]
        int OpenSharedResource(IntPtr hResource, [In] ref Guid ReturnedInterface, out IntPtr ppResource);

        [PreserveSig]
        int CheckFormatSupport(uint Format, out uint pFormatSupport);

        [PreserveSig]
        int CheckMultisampleQualityLevels(uint Format, uint SampleCount, out uint pNumQualityLevels);

        [PreserveSig]
        void CheckCounterInfo(out IntPtr pCounterInfo);

        [PreserveSig]
        int CheckCounter(IntPtr pDesc, out uint pType, out uint pActiveCounters, out StringBuilder szName, out uint pNameLength, out StringBuilder szUnits, out uint pUnitsLength, out StringBuilder szDescription, out uint pDescriptionLength);

        [PreserveSig]
        int CheckFeatureSupport(uint Feature, IntPtr pFeatureSupportData, uint FeatureSupportDataSize);

        [PreserveSig]
        int GetPrivateData([In] ref Guid guid, ref uint pDataSize, IntPtr pData);

        [PreserveSig]
        int SetPrivateData([In] ref Guid guid, uint DataSize, IntPtr pData);

        [PreserveSig]
        int SetPrivateDataInterface([In] ref Guid guid, IntPtr pData);

        [PreserveSig]
        D3D_FEATURE_LEVEL GetFeatureLevel();

        [PreserveSig]
        uint GetCreationFlags();

        [PreserveSig]
        int GetDeviceRemovedReason();

        [PreserveSig]
        void GetImmediateContext(out IntPtr ppImmediateContext);

        [PreserveSig]
        int SetExceptionMode(uint RaiseFlags);

        [PreserveSig]
        uint GetExceptionMode();
    }

    [ComImport]
    [Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ID3D11DeviceContext
    {
        [PreserveSig]
        void VSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);

        [PreserveSig]
        void PSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);

        [PreserveSig]
        void PSSetShader(IntPtr pPixelShader, [In] IntPtr[] ppClassInstances, uint NumClassInstances);

        [PreserveSig]
        void PSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);

        [PreserveSig]
        void VSSetShader(IntPtr pVertexShader, [In] IntPtr[] ppClassInstances, uint NumClassInstances);

        [PreserveSig]
        void DrawIndexed(uint IndexCount, uint StartIndexLocation, int BaseVertexLocation);

        [PreserveSig]
        void Draw(uint VertexCount, uint StartVertexLocation);

        [PreserveSig]
        int Map(IntPtr pResource, uint Subresource, uint MapType, uint MapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);

        [PreserveSig]
        void Unmap(IntPtr pResource, uint Subresource);

        [PreserveSig]
        void PSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);

        [PreserveSig]
        void IASetInputLayout(IntPtr pInputLayout);

        [PreserveSig]
        void IASetVertexBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppVertexBuffers, [In] uint[] pStrides, [In] uint[] pOffsets);

        [PreserveSig]
        void IASetIndexBuffer(IntPtr pIndexBuffer, uint Format, uint Offset);

        [PreserveSig]
        void DrawIndexedInstanced(uint IndexCountPerInstance, uint InstanceCount, uint StartIndexLocation, int BaseVertexLocation, uint StartInstanceLocation);

        [PreserveSig]
        void DrawInstanced(uint VertexCountPerInstance, uint InstanceCount, uint StartVertexLocation, uint StartInstanceLocation);

        [PreserveSig]
        void GSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);

        [PreserveSig]
        void GSSetShader(IntPtr pShader, [In] IntPtr[] ppClassInstances, uint NumClassInstances);

        [PreserveSig]
        void IASetPrimitiveTopology(uint Topology);

        [PreserveSig]
        void VSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);

        [PreserveSig]
        void VSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);

        [PreserveSig]
        void Begin(IntPtr pAsync);

        [PreserveSig]
        void End(IntPtr pAsync);

        [PreserveSig]
        int GetData(IntPtr pAsync, IntPtr pData, uint DataSize, uint GetDataFlags);

        [PreserveSig]
        void SetPredication(IntPtr pPredicate, uint PredicateValue);

        [PreserveSig]
        void GSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);

        [PreserveSig]
        void GSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);

        [PreserveSig]
        void OMSetRenderTargets(uint NumViews, [In] IntPtr[] ppRenderTargetViews, IntPtr pDepthStencilView);

        [PreserveSig]
        void OMSetRenderTargetsAndUnorderedAccessViews(uint NumRTVs, [In] IntPtr[] ppRenderTargetViews, IntPtr pDepthStencilView, uint UAVStartSlot, uint NumUAVs, [In] IntPtr[] ppUnorderedAccessViews, [In] uint[] pUAVInitialCounts);

        [PreserveSig]
        void OMSetBlendState(IntPtr pBlendState, [In] float[] BlendFactor, uint SampleMask);

        [PreserveSig]
        void OMSetDepthStencilState(IntPtr pDepthStencilState, uint StencilRef);

        [PreserveSig]
        void SOSetTargets(uint NumBuffers, [In] IntPtr[] ppSOTargets, [In] uint[] pOffsets);

        [PreserveSig]
        void DrawAuto();

        [PreserveSig]
        void DrawIndexedInstancedIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);

        [PreserveSig]
        void DrawInstancedIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);

        [PreserveSig]
        void Dispatch(uint ThreadGroupCountX, uint ThreadGroupCountY, uint ThreadGroupCountZ);

        [PreserveSig]
        void DispatchIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);

        [PreserveSig]
        void RSSetState(IntPtr pRasterizerState);

        [PreserveSig]
        void RSSetViewports(uint NumViewports, IntPtr pViewports);

        [PreserveSig]
        void RSSetScissorRects(uint NumRects, IntPtr pRects);

        [PreserveSig]
        void CopySubresourceRegion(IntPtr pDstResource, uint DstSubresource, uint DstX, uint DstY, uint DstZ, IntPtr pSrcResource, uint SrcSubresource, IntPtr pSrcBox);

        [PreserveSig]
        void CopyResource(IntPtr pDstResource, IntPtr pSrcResource);

        [PreserveSig]
        void UpdateSubresource(IntPtr pDstResource, uint DstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint SrcRowPitch, uint SrcDepthPitch);

        [PreserveSig]
        void CopyStructureCount(IntPtr pDstBuffer, uint DstAlignedByteOffset, IntPtr pSrcView);

        [PreserveSig]
        void ClearRenderTargetView(IntPtr pRenderTargetView, [In] float[] ColorRGBA);

        [PreserveSig]
        void ClearUnorderedAccessViewUint(IntPtr pUnorderedAccessView, [In] uint[] Values);

        [PreserveSig]
        void ClearUnorderedAccessViewFloat(IntPtr pUnorderedAccessView, [In] float[] Values);

        [PreserveSig]
        void ClearDepthStencilView(IntPtr pDepthStencilView, uint ClearFlags, float Depth, byte Stencil);

        [PreserveSig]
        void GenerateMips(IntPtr pShaderResourceView);

        [PreserveSig]
        void SetResourceMinLOD(IntPtr pResource, float MinLOD);

        [PreserveSig]
        float GetResourceMinLOD(IntPtr pResource);

        [PreserveSig]
        void ResolveSubresource(IntPtr pDstResource, uint DstSubresource, IntPtr pSrcResource, uint SrcSubresource, uint Format);

        [PreserveSig]
        void ExecuteCommandList(IntPtr pCommandList, uint RestoreContextState);

        [PreserveSig]
        void HSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);

        [PreserveSig]
        void HSSetShader(IntPtr pHullShader, [In] IntPtr[] ppClassInstances, uint NumClassInstances);

        [PreserveSig]
        void HSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);

        [PreserveSig]
        void HSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);

        [PreserveSig]
        void DSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);

        [PreserveSig]
        void DSSetShader(IntPtr pDomainShader, [In] IntPtr[] ppClassInstances, uint NumClassInstances);

        [PreserveSig]
        void DSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);

        [PreserveSig]
        void DSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);

        [PreserveSig]
        void CSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);

        [PreserveSig]
        void CSSetUnorderedAccessViews(uint StartSlot, uint NumUAVs, [In] IntPtr[] ppUnorderedAccessViews, [In] uint[] pUAVInitialCounts);

        [PreserveSig]
        void CSSetShader(IntPtr pComputeShader, [In] IntPtr[] ppClassInstances, uint NumClassInstances);

        [PreserveSig]
        void CSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);

        [PreserveSig]
        void CSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);

        // Additional methods for getting state would go here...
        [PreserveSig]
        void Flush();

        [PreserveSig]
        uint GetType();

        [PreserveSig]
        uint GetContextFlags();

        [PreserveSig]
        int FinishCommandList(uint RestoreDeferredContextState, out IntPtr ppCommandList);
    }

    // Helper methods for working with D3D blobs
    [ComImport]
    [Guid("8ba5fb08-5195-40e2-ac58-0d989c3a0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ID3DBlob
    {
        [PreserveSig]
        IntPtr GetBufferPointer();

        [PreserveSig]
        nuint GetBufferSize();
    }

    /// <summary>
    /// Helper class for managing DirectCompute resources with proper disposal.
    /// </summary>
    public sealed class DirectComputeResource : IDisposable
    {
        private IntPtr _resource;
        private bool _disposed;

        public DirectComputeResource(IntPtr resource)
        {
            _resource = resource;
        }

        public IntPtr Handle => _disposed ? IntPtr.Zero : _resource;

        public void Dispose()
        {
            if (!_disposed && _resource != IntPtr.Zero)
            {
                Marshal.Release(_resource);
                _resource = IntPtr.Zero;
                _disposed = true;
            }
        }

        ~DirectComputeResource()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Creates a DirectCompute device with the specified feature level.
    /// </summary>
    public static (IntPtr device, IntPtr context, D3D_FEATURE_LEVEL featureLevel) CreateDevice()
    {
        var featureLevels = new[]
        {
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0
        };

        uint flags = 0;
#if DEBUG
        flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

        var hr = D3D11CreateDevice(
            IntPtr.Zero, // Use default adapter
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            flags,
            featureLevels,
            (uint)featureLevels.Length,
            D3D11_SDK_VERSION,
            out var device,
            out var featureLevel,
            out var context);

        ThrowIfFailed(hr, "D3D11CreateDevice");
        return (device, context, featureLevel);
    }

    /// <summary>
    /// Compiles HLSL compute shader source code to bytecode.
    /// </summary>
    public static (byte[] bytecode, string log) CompileComputeShader(string source, string entryPoint, string target, uint flags)
    {
        var sourceBytes = Encoding.UTF8.GetBytes(source);
        
        var hr = D3DCompile(
            source,
            (nuint)sourceBytes.Length,
            null, // Source name
            IntPtr.Zero, // Defines
            IntPtr.Zero, // Include
            entryPoint,
            target,
            flags,
            0, // Effect flags
            out var codeBlob,
            out var errorBlob);

        var log = "";
        if (errorBlob != IntPtr.Zero)
        {
            try
            {
                if (Marshal.GetObjectForIUnknown(errorBlob) is ID3DBlob errorBlobInterface)
                {
                    var errorPtr = errorBlobInterface.GetBufferPointer();
                    var errorSize = (int)errorBlobInterface.GetBufferSize();
                    if (errorPtr != IntPtr.Zero && errorSize > 0)
                    {
                        log = Marshal.PtrToStringAnsi(errorPtr, errorSize) ?? "";
                    }
                }
            }
            finally
            {
                Marshal.Release(errorBlob);
            }
        }

        if (Failed(hr))
        {
            if (codeBlob != IntPtr.Zero)
            {
                Marshal.Release(codeBlob);
            }

            throw new InvalidOperationException($"HLSL compilation failed (HRESULT: 0x{hr:X8}): {log}");
        }

        try
        {
            if (Marshal.GetObjectForIUnknown(codeBlob) is not ID3DBlob codeBlobInterface)
            {
                throw new InvalidOperationException("Failed to get blob interface");
            }

            var bytecodePtr = codeBlobInterface.GetBufferPointer();
            var bytecodeSize = (int)codeBlobInterface.GetBufferSize();
            
            var bytecode = new byte[bytecodeSize];
            Marshal.Copy(bytecodePtr, bytecode, 0, bytecodeSize);
            
            return (bytecode, log);
        }
        finally
        {
            if (codeBlob != IntPtr.Zero)
            {
                Marshal.Release(codeBlob);
            }
        }
    }
}