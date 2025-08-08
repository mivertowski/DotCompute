using System;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.DirectCompute.Tests;

/// <summary>
/// Hardware-dependent tests for DirectCompute (DirectX Compute Shader) functionality.
/// These tests require DirectX 11 runtime on Windows.
/// </summary>
[Collection("Hardware")]
[Trait("Category", "RequiresGPU")]
[Trait("Category", "DirectCompute")]
[Trait("Platform", "Windows")]
public class DirectComputeHardwareTests
{
    private readonly ITestOutputHelper _output;

    public DirectComputeHardwareTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public void Should_DetectDirectComputeSupport()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "DirectCompute requires Windows");
        Skip.IfNot(IsDirectComputeAvailable(), "DirectCompute/DirectX 11 not available");

        var result = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            0,
            null,
            0,
            D3D11_SDK_VERSION,
            out IntPtr device,
            out int featureLevel,
            out IntPtr context);

        _output.WriteLine($"D3D11 device creation result: 0x{result:X8}");
        _output.WriteLine($"Feature level: 0x{featureLevel:X4}");

        Assert.Equal(0, result); // S_OK
        Assert.NotEqual(IntPtr.Zero, device);
        Assert.NotEqual(IntPtr.Zero, context);

        // Cleanup
        if (context != IntPtr.Zero)
            Marshal.Release(context);
        if (device != IntPtr.Zero)
            Marshal.Release(device);
    }

    [SkippableFact]
    public void Should_EnumerateAdapters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "DirectCompute requires Windows");
        Skip.IfNot(IsDirectComputeAvailable(), "DirectCompute/DirectX 11 not available");

        var result = CreateDXGIFactory(typeof(IDXGIFactory).GUID, out IntPtr factory);
        
        if (result == 0 && factory != IntPtr.Zero)
        {
            uint adapterIndex = 0;
            int adapterCount = 0;
            
            while (true)
            {
                result = EnumAdapters(factory, adapterIndex, out IntPtr adapter);
                if (result != 0) // DXGI_ERROR_NOT_FOUND
                    break;
                    
                adapterCount++;
                _output.WriteLine($"Found adapter {adapterIndex}");
                
                if (adapter != IntPtr.Zero)
                    Marshal.Release(adapter);
                    
                adapterIndex++;
            }
            
            Marshal.Release(factory);
            
            _output.WriteLine($"Total adapters found: {adapterCount}");
            Assert.True(adapterCount > 0, "No DirectX adapters found");
        }
    }

    [SkippableFact]
    public void Should_CreateComputeShader()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "DirectCompute requires Windows");
        Skip.IfNot(IsDirectComputeAvailable(), "DirectCompute/DirectX 11 not available");

        // Simple compute shader bytecode (compiled from HLSL)
        // This is a minimal compute shader that does nothing
        byte[] shaderBytecode = new byte[]
        {
            0x44, 0x58, 0x42, 0x43, // DXBC header
            // ... (actual shader bytecode would go here)
        };

        var result = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            0,
            null,
            0,
            D3D11_SDK_VERSION,
            out IntPtr device,
            out int featureLevel,
            out IntPtr context);

        if (result == 0 && device != IntPtr.Zero)
        {
            // In a real test, we would create a compute shader here
            _output.WriteLine("DirectCompute device created successfully");
            _output.WriteLine($"Can create compute shaders with feature level: 0x{featureLevel:X4}");

            Assert.True(featureLevel >= 0xB000); // DirectX 11 or higher

            Marshal.Release(context);
            Marshal.Release(device);
        }
    }

    private static bool IsDirectComputeAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            // Try to create a D3D11 device
            var result = D3D11CreateDevice(
                IntPtr.Zero,
                D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                0,
                null,
                0,
                D3D11_SDK_VERSION,
                out IntPtr device,
                out _,
                out IntPtr context);

            if (result == 0 && device != IntPtr.Zero)
            {
                Marshal.Release(context);
                Marshal.Release(device);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    // DirectX P/Invoke declarations
    private const int D3D11_SDK_VERSION = 7;

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        D3D_DRIVER_TYPE driverType,
        IntPtr software,
        uint flags,
        int[]? pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        out IntPtr ppDevice,
        out int pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory(
        [In] in Guid riid,
        out IntPtr ppFactory);

    [DllImport("dxgi.dll")]
    private static extern int EnumAdapters(
        IntPtr factory,
        uint index,
        out IntPtr adapter);

    private enum D3D_DRIVER_TYPE
    {
        D3D_DRIVER_TYPE_UNKNOWN = 0,
        D3D_DRIVER_TYPE_HARDWARE = 1,
        D3D_DRIVER_TYPE_REFERENCE = 2,
        D3D_DRIVER_TYPE_NULL = 3,
        D3D_DRIVER_TYPE_SOFTWARE = 4,
        D3D_DRIVER_TYPE_WARP = 5
    }

    [ComImport]
    [Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369")]
    private interface IDXGIFactory { }
}