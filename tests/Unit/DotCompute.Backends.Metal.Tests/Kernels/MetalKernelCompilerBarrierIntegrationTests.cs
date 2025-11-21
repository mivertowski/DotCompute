// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.Metal.Barriers;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Kernels;

/// <summary>
/// Integration tests for Metal barrier/fence code injection in kernel compiler.
/// </summary>
public sealed class MetalKernelCompilerBarrierIntegrationTests
{
    [Fact]
    public void InjectBarrierAndFenceCode_BarrierMarker_InjectsCorrectly()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(device int* data [[buffer(0)]]) {
    uint tid = thread_position_in_threadgroup;
    data[tid] = tid;
    // @BARRIER
    int neighbor = data[tid + 1];
}";

        var barrierHandle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.DeviceAndThreadgroup);

        // Act
        var result = InvokePrivateInjectMethod(mslSource, barrierHandle);

        // Assert
        Assert.Contains("threadgroup_barrier(mem_flags::mem_device_and_threadgroup);", result);
        Assert.Contains("// Injected barrier (ID=1)", result);
        Assert.DoesNotContain("// @BARRIER", result); // Marker should be replaced
    }

    [Fact]
    public void InjectBarrierAndFenceCode_FenceDeviceMarker_InjectsCorrectly()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(device int* data [[buffer(0)]]) {
    data[0] = 42;
    // @FENCE:DEVICE
    data[1] = data[0];
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Contains("threadgroup_barrier(mem_flags::mem_device);", result);
        Assert.Contains("// Injected device fence", result);
        Assert.DoesNotContain("// @FENCE:DEVICE", result);
    }

    [Fact]
    public void InjectBarrierAndFenceCode_FenceThreadgroupMarker_InjectsCorrectly()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(
    device int* data [[buffer(0)]],
    threadgroup int* shared [[threadgroup(0)]]) {

    shared[0] = data[0];
    // @FENCE:THREADGROUP
    data[0] = shared[0];
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Contains("threadgroup_barrier(mem_flags::mem_threadgroup);", result);
        Assert.Contains("// Injected threadgroup fence", result);
    }

    [Fact]
    public void InjectBarrierAndFenceCode_FenceTextureMarker_InjectsCorrectly()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(texture2d<float, access::read_write> tex [[texture(0)]]) {
    tex.write(float4(1.0), uint2(0, 0));
    // @FENCE:TEXTURE
    float4 value = tex.read(uint2(0, 0));
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Contains("threadgroup_barrier(mem_flags::mem_texture);", result);
        Assert.Contains("// Injected texture fence", result);
    }

    [Fact]
    public void InjectBarrierAndFenceCode_FenceAllMarker_InjectsCorrectly()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(device int* data [[buffer(0)]]) {
    data[0] = 1;
    // @FENCE:ALL
    data[1] = 2;
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Contains("threadgroup_barrier(mem_flags::mem_device_and_threadgroup);", result);
        Assert.Contains("// Injected full fence", result);
    }

    [Fact]
    public void InjectBarrierAndFenceCode_MultipleMarkers_InjectsAll()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(device int* data [[buffer(0)]]) {
    data[0] = 1;
    // @FENCE:DEVICE

    data[1] = 2;
    // @FENCE:THREADGROUP

    data[2] = 3;
    // @FENCE:ALL

    data[3] = 4;
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Contains("threadgroup_barrier(mem_flags::mem_device);", result);
        Assert.Contains("threadgroup_barrier(mem_flags::mem_threadgroup);", result);
        Assert.Contains("threadgroup_barrier(mem_flags::mem_device_and_threadgroup);", result);
    }

    [Fact]
    public void InjectBarrierAndFenceCode_PreservesIndentation()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(device int* data [[buffer(0)]]) {
    if (data[0] > 0) {
        data[1] = 1;
        // @FENCE:DEVICE
        data[2] = 2;
    }
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Contains("        threadgroup_barrier(mem_flags::mem_device);", result); // 8 spaces
    }

    [Fact]
    public void InjectBarrierAndFenceCode_NoMarkers_ReturnsUnchanged()
    {
        // Arrange
        var mslSource = @"
kernel void test_kernel(device int* data [[buffer(0)]]) {
    data[0] = 42;
}";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Equal(mslSource, result);
    }

    [Fact]
    public void InjectBarrierAndFenceCode_EmptySource_ReturnsEmpty()
    {
        // Arrange
        var mslSource = "";

        // Act
        var result = InvokePrivateInjectMethod(mslSource, null);

        // Assert
        Assert.Equal(mslSource, result);
    }

    [Fact]
    public void GenerateMslBarrierCode_AllFenceFlags_GeneratesCorrectCode()
    {
        // Act & Assert
        Assert.Equal(
            "threadgroup_barrier(mem_flags::mem_none);",
            MetalKernelCompiler.GenerateMslBarrierCode(MetalMemoryFenceFlags.None));

        Assert.Equal(
            "threadgroup_barrier(mem_flags::mem_device);",
            MetalKernelCompiler.GenerateMslBarrierCode(MetalMemoryFenceFlags.Device));

        Assert.Equal(
            "threadgroup_barrier(mem_flags::mem_threadgroup);",
            MetalKernelCompiler.GenerateMslBarrierCode(MetalMemoryFenceFlags.Threadgroup));

        Assert.Equal(
            "threadgroup_barrier(mem_flags::mem_texture);",
            MetalKernelCompiler.GenerateMslBarrierCode(MetalMemoryFenceFlags.Texture));

        Assert.Equal(
            "threadgroup_barrier(mem_flags::mem_device_and_threadgroup);",
            MetalKernelCompiler.GenerateMslBarrierCode(MetalMemoryFenceFlags.DeviceAndThreadgroup));
    }

    [Fact]
    public void ContainsBarrierMarkers_WithBarrierMarker_ReturnsTrue()
    {
        // Arrange
        var mslSource = "kernel void test() { // @BARRIER\n }";

        // Act
        var result = MetalKernelCompiler.ContainsBarrierMarkers(mslSource);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsBarrierMarkers_WithFenceMarker_ReturnsTrue()
    {
        // Arrange
        var mslSource = "kernel void test() { // @FENCE:DEVICE\n }";

        // Act
        var result = MetalKernelCompiler.ContainsBarrierMarkers(mslSource);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsBarrierMarkers_NoMarkers_ReturnsFalse()
    {
        // Arrange
        var mslSource = "kernel void test() { data[0] = 42; }";

        // Act
        var result = MetalKernelCompiler.ContainsBarrierMarkers(mslSource);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsBarrierMarkers_EmptySource_ReturnsFalse()
    {
        // Arrange
        var mslSource = "";

        // Act
        var result = MetalKernelCompiler.ContainsBarrierMarkers(mslSource);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Helper method to invoke the private InjectBarrierAndFenceCode method using reflection.
    /// </summary>
    private static string InvokePrivateInjectMethod(string mslSource, MetalBarrierHandle? barrierHandle)
    {
        // Create a minimal kernel compiler instance for testing
        // We don't actually compile, just test the injection method
        var type = typeof(MetalKernelCompiler);
        var method = type.GetMethod("InjectBarrierAndFenceCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);

        // Create real Metal resources if available, otherwise skip
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            throw new SkipException("Metal is not available on this system");
        }

        var queuePoolLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalCommandQueuePool>.Instance;
        var commandQueuePool = new MetalCommandQueuePool(device, queuePoolLogger, maxConcurrency: null);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var compiler = new MetalKernelCompiler(
            device: device,
            commandQueuePool: commandQueuePool,
            logger: logger);

        try
        {
            var result = method.Invoke(compiler, new object?[] { mslSource, barrierHandle });
            return (string)result!;
        }
        finally
        {
            compiler.Dispose();
            commandQueuePool.Dispose();
            MetalNative.ReleaseDevice(device);
        }
    }
}
