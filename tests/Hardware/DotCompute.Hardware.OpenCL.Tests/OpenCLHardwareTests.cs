using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.OpenCL.Tests;


/// <summary>
/// Hardware-dependent tests for OpenCL functionality.
/// These tests require OpenCL runtime to be installed.
/// </summary>
[Collection("Hardware")]
[Trait("Category", "HardwareRequired")]
[Trait("Category", "OpenCLRequired")]
[Trait("Category", "Hardware")]
public class OpenCLHardwareTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [SkippableFact]
    [Trait("Category", "OpenCLRequired")]
    public void Should_DetectOpenCLPlatforms()
    {
        Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

        uint platformCount = 0;
        var result = clGetPlatformIDs(0, null, ref platformCount);

        _output.WriteLine($"OpenCL platform detection result: {result}");
        _output.WriteLine($"Number of OpenCL platforms found: {platformCount}");

        _ = platformCount.Should().BeGreaterThan(0, "No OpenCL platforms detected");
    }

    [SkippableFact]
    [Trait("Category", "OpenCLRequired")]
    public void Should_GetOpenCLDevices()
    {
        Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

        uint platformCount = 0;
        _ = clGetPlatformIDs(0, null, ref platformCount);

        if (platformCount > 0)
        {
            var platforms = new nint[platformCount];
            var getPlatformsResult = clGetPlatformIDs(platformCount, platforms, ref platformCount);

            Assert.Equal(0, getPlatformsResult); // CL_SUCCESS

            foreach (var platform in platforms)
            {
                uint deviceCount = 0;
                _ = clGetDeviceIDs(platform, DeviceType.CL_DEVICE_TYPE_ALL, 0, null, ref deviceCount);

                _output.WriteLine($"Platform has {deviceCount} devices");
                Assert.True(deviceCount >= 0);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "OpenCLRequired")]
    public void Should_CreateOpenCLContext()
    {
        Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

        uint platformCount = 0;
        _ = clGetPlatformIDs(0, null, ref platformCount);

        if (platformCount > 0)
        {
            var platforms = new nint[1];
            _ = clGetPlatformIDs(1, platforms, ref platformCount);

            uint deviceCount = 0;
            _ = clGetDeviceIDs(platforms[0], DeviceType.CL_DEVICE_TYPE_DEFAULT, 0, null, ref deviceCount);

            if (deviceCount > 0)
            {
                var devices = new nint[1];
                _ = clGetDeviceIDs(platforms[0], DeviceType.CL_DEVICE_TYPE_DEFAULT, 1, devices, ref deviceCount);

                var errorCode = 0;
                var context = clCreateContext(nint.Zero, 1, devices, nint.Zero, nint.Zero, ref errorCode);

                Assert.NotEqual(nint.Zero, context);
                Assert.Equal(0, errorCode); // CL_SUCCESS

                if (context != nint.Zero)
                {
                    var releaseResult = clReleaseContext(context);
                    _output.WriteLine($"Context release result: {releaseResult}");
                }
            }
        }
    }

    private static bool IsOpenCLAvailable()
    {
        try
        {
            uint platformCount = 0;
            var result = clGetPlatformIDs(0, null, ref platformCount);
            return result == 0 && platformCount > 0;
        }
        catch
        {
            return false;
        }
    }

    // OpenCL P/Invoke declarations
    private const string OpenCLLibrary = "OpenCL";

    [DllImport(OpenCLLibrary)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int clGetPlatformIDs(uint numEntries, nint[]? platforms, ref uint numPlatforms);

    [DllImport(OpenCLLibrary)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int clGetDeviceIDs(nint platform, DeviceType deviceType, uint numEntries, nint[]? devices, ref uint numDevices);

    [DllImport(OpenCLLibrary)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern nint clCreateContext(nint properties, uint numDevices, nint[] devices, nint pfnNotify, nint userData, ref int errorCode);

    [DllImport(OpenCLLibrary)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int clReleaseContext(nint context);

    private enum DeviceType : ulong
    {
        CL_DEVICE_TYPE_DEFAULT = 1 << 0,
        CL_DEVICE_TYPE_CPU = 1 << 1,
        CL_DEVICE_TYPE_GPU = 1 << 2,
        CL_DEVICE_TYPE_ACCELERATOR = 1 << 3,
        CL_DEVICE_TYPE_ALL = 0xFFFFFFFF
    }
}
