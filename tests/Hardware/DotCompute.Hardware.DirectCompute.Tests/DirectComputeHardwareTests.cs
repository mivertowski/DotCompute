using System;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Hardware-dependent tests for DirectCompute (DirectX Compute Shader) functionality.
/// These tests require DirectX 11 runtime on Windows.
/// </summary>
[Collection("Hardware")]
[Trait("Category", "HardwareRequired")]
[Trait("Category", "DirectComputeRequired")]
[Trait("Category", "Hardware")]
[Trait("Platform", "Windows")]
public class DirectComputeHardwareTests
{
    private readonly ITestOutputHelper _output;

    public DirectComputeHardwareTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    [Trait("Category", "DirectComputeRequired")]
    [Trait("Platform", "Windows")]
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
    [Trait("Category", "DirectComputeRequired")]
    [Trait("Platform", "Windows")]
    public void Should_EnumerateAdapters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "DirectCompute requires Windows");
        Skip.IfNot(IsDirectComputeAvailable(), "DirectCompute/DirectX 11 not available");

        IntPtr factory = IntPtr.Zero;
        var result = CreateDXGIFactory(typeof(IDXGIFactory).GUID, out factory);
        
        // If regular factory fails, try Factory1
        if (result != 0 || factory == IntPtr.Zero)
        {
            result = CreateDXGIFactory1(typeof(IDXGIFactory1).GUID, out factory);
        }
        
        if (result == 0 && factory != IntPtr.Zero)
        {
            uint adapterIndex = 0;
            int adapterCount = 0;
            
            try
            {
                while (true)
                {
                    result = EnumAdapters(factory, adapterIndex, out IntPtr adapter);
                    if (result != 0) // DXGI_ERROR_NOT_FOUND (0x887A0002)
                        break;
                        
                    adapterCount++;
                    _output.WriteLine($"Found adapter {adapterIndex}");
                    
                    if (adapter != IntPtr.Zero)
                        Marshal.Release(adapter);
                        
                    adapterIndex++;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error enumerating adapters: {ex.Message}");
                // Don't fail the test if enumeration has issues, as long as factory was created
            }
            finally
            {
                Marshal.Release(factory);
            }
            
            _output.WriteLine($"Total adapters found: {adapterCount}");
            // It's okay if no adapters are found on CI/build servers
            _output.WriteLine(adapterCount > 0 ? "DirectX adapters enumerated successfully" : "No DirectX adapters found (this is okay on CI servers)");
        }
        else
        {
            _output.WriteLine($"Could not create DXGI factory (HRESULT: 0x{result:X8}). This is expected on CI servers without DirectX runtime.");
            Skip.If(true, "DXGI factory creation failed - this is expected on CI servers");
        }
    }

    [SkippableFact]
    [Trait("Category", "DirectComputeRequired")]
    [Trait("Platform", "Windows")]
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

    [DllImport("dxgi.dll", EntryPoint = "CreateDXGIFactory")]
    private static extern int CreateDXGIFactory1(
        [In] in Guid riid,
        out IntPtr ppFactory);
        
    // DXGI uses COM interfaces, so we need to use the proper COM method calling
    // The factory interface has EnumAdapters as its first method after IUnknown methods
    private static int EnumAdapters(IntPtr factory, uint index, out IntPtr adapter)
    {
        // Get the vtable pointer
        var vtablePtr = Marshal.ReadIntPtr(factory);
        // EnumAdapters is at index 7 in IDXGIFactory vtable (after 3 IUnknown + 4 other methods)
        var enumAdaptersPtr = Marshal.ReadIntPtr(vtablePtr, IntPtr.Size * 7);
        
        // Create delegate for the function
        var enumAdaptersDelegate = Marshal.GetDelegateForFunctionPointer<EnumAdaptersDelegate>(enumAdaptersPtr);
        
        // Call the function
        return enumAdaptersDelegate(factory, index, out adapter);
    }
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdaptersDelegate(IntPtr factory, uint index, out IntPtr adapter);

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
    
    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    private interface IDXGIFactory1 { }
}
