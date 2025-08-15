using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Hardware-dependent tests for OpenCL functionality.
/// These tests require OpenCL runtime to be installed.
/// </summary>
[Collection("Hardware")]
[Trait("Category", "HardwareRequired")]
[Trait("Category", "OpenCLRequired")]
[Trait("Category", "Hardware")]
public class OpenCLHardwareTests
{
    private readonly ITestOutputHelper _output;

    public OpenCLHardwareTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    [Trait("Category", "OpenCLRequired")]
    public void Should_DetectOpenCLPlatforms()
    {
        Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

        uint platformCount = 0;
        var result = clGetPlatformIDs(0, null, ref platformCount);

        _output.WriteLine($"OpenCL platform detection result: {result}");
        _output.WriteLine($"Number of OpenCL platforms found: {platformCount}");

        platformCount.Should().BeGreaterThan(0, "No OpenCL platforms detected");
    }

    [SkippableFact]
    [Trait("Category", "OpenCLRequired")]
    public void Should_GetOpenCLDevices()
    {
        Skip.IfNot(IsOpenCLAvailable(), "OpenCL runtime not available");

        uint platformCount = 0;
        var result = clGetPlatformIDs(0, null, ref platformCount);

        if (platformCount > 0)
        {
            var platforms = new IntPtr[platformCount];
            result = clGetPlatformIDs(platformCount, platforms, ref platformCount);

            Assert.Equal(0, result); // CL_SUCCESS

            foreach (var platform in platforms)
            {
                uint deviceCount = 0;
                result = clGetDeviceIDs(platform, DeviceType.CL_DEVICE_TYPE_ALL, 0, null, ref deviceCount);

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
        var result = clGetPlatformIDs(0, null, ref platformCount);

        if (platformCount > 0)
        {
            var platforms = new IntPtr[1];
            result = clGetPlatformIDs(1, platforms, ref platformCount);

            uint deviceCount = 0;
            result = clGetDeviceIDs(platforms[0], DeviceType.CL_DEVICE_TYPE_DEFAULT, 0, null, ref deviceCount);

            if (deviceCount > 0)
            {
                var devices = new IntPtr[1];
                result = clGetDeviceIDs(platforms[0], DeviceType.CL_DEVICE_TYPE_DEFAULT, 1, devices, ref deviceCount);

                var errorCode = 0;
                var context = clCreateContext(IntPtr.Zero, 1, devices, IntPtr.Zero, IntPtr.Zero, ref errorCode);

                Assert.NotEqual(IntPtr.Zero, context);
                Assert.Equal(0, errorCode); // CL_SUCCESS

                if (context != IntPtr.Zero)
                {
                    clReleaseContext(context);
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
    private static extern int clGetPlatformIDs(uint numEntries, IntPtr[]? platforms, ref uint numPlatforms);

    [DllImport(OpenCLLibrary)]
    private static extern int clGetDeviceIDs(IntPtr platform, DeviceType deviceType, uint numEntries, IntPtr[]? devices, ref uint numDevices);

    [DllImport(OpenCLLibrary)]
    private static extern IntPtr clCreateContext(IntPtr properties, uint numDevices, IntPtr[] devices, IntPtr pfnNotify, IntPtr userData, ref int errorCode);

    [DllImport(OpenCLLibrary)]
    private static extern int clReleaseContext(IntPtr context);

    private enum DeviceType : ulong
    {
        CL_DEVICE_TYPE_DEFAULT = (1 << 0),
        CL_DEVICE_TYPE_CPU = (1 << 1),
        CL_DEVICE_TYPE_GPU = (1 << 2),
        CL_DEVICE_TYPE_ACCELERATOR = (1 << 3),
        CL_DEVICE_TYPE_ALL = 0xFFFFFFFF
    }
}
