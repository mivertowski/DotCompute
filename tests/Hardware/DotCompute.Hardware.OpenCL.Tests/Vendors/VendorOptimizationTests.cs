// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Vendor;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Vendors;

/// <summary>
/// Tests for vendor-specific OpenCL optimizations on Intel Arc and NVIDIA RTX hardware.
/// Validates that vendor adapters properly detect and configure for each vendor.
/// </summary>
[Collection("Hardware")]
[Trait("Category", "RequiresOpenCL")]
[Trait("Category", "VendorOptimization")]
public sealed class VendorOptimizationTests : OpenCLTestBase
{
    public VendorOptimizationTests(ITestOutputHelper output) : base(output)
    {
    }

    [SkippableFact(DisplayName = "001: Vendor Detection - Detects Intel Arc GPU")]
    public void VendorDetection_DetectsIntelArcGpu()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();

        // Act
        var intelDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU);

        // Assert or Skip
        if (intelDevice == null)
        {
            Output.WriteLine("Intel Arc GPU not found on this system - test skipped");
            Skip.If(true, "Intel Arc GPU not available");
            return;
        }

        Output.WriteLine($"✓ Intel GPU detected: {intelDevice.Name}");
        Output.WriteLine($"  Vendor: {intelDevice.Vendor}");
        Output.WriteLine($"  Type: {intelDevice.Type}");
        Output.WriteLine($"  Compute Units: {intelDevice.MaxComputeUnits}");
        Output.WriteLine($"  Global Memory: {intelDevice.GlobalMemorySize / (1024.0 * 1024.0 * 1024.0):F2} GB");

        // Verify vendor detection
        var devicePlatform = GetPlatformForDevice(deviceManager, intelDevice);
        Assert.NotNull(devicePlatform);

        var adapter = VendorAdapterFactory.GetAdapter(devicePlatform);
        Assert.Equal(OpenCLVendor.Intel, adapter.Vendor);
        Assert.Equal("Intel Corporation", adapter.VendorName);
    }

    [SkippableFact(DisplayName = "002: Vendor Detection - Detects NVIDIA RTX GPU")]
    public void VendorDetection_DetectsNvidiaRtxGpu()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();

        // Act
        var nvidiaDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
            d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU);

        // Assert or Skip
        if (nvidiaDevice == null)
        {
            Output.WriteLine("NVIDIA RTX GPU not found on this system - test skipped");
            Skip.If(true, "NVIDIA RTX GPU not available");
            return;
        }

        Output.WriteLine($"✓ NVIDIA GPU detected: {nvidiaDevice.Name}");
        Output.WriteLine($"  Vendor: {nvidiaDevice.Vendor}");
        Output.WriteLine($"  Type: {nvidiaDevice.Type}");
        Output.WriteLine($"  Compute Units: {nvidiaDevice.MaxComputeUnits}");
        Output.WriteLine($"  Global Memory: {nvidiaDevice.GlobalMemorySize / (1024.0 * 1024.0 * 1024.0):F2} GB");

        // Verify vendor detection
        var devicePlatform = GetPlatformForDevice(deviceManager, nvidiaDevice);
        Assert.NotNull(devicePlatform);

        var adapter = VendorAdapterFactory.GetAdapter(devicePlatform);
        Assert.Equal(OpenCLVendor.NVIDIA, adapter.Vendor);
        Assert.Equal("NVIDIA Corporation", adapter.VendorName);
    }

    [SkippableFact(DisplayName = "003: NVIDIA Optimizations - Warp-aligned work groups")]
    public void NvidiaOptimizations_WarpAlignedWorkGroups()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var nvidiaDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));

        Skip.If(nvidiaDevice == null, "NVIDIA GPU not available");

        var platform = GetPlatformForDevice(deviceManager, nvidiaDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var workGroupSize = adapter.GetOptimalWorkGroupSize(nvidiaDevice!, 128);

        // Assert
        Output.WriteLine($"NVIDIA optimal work group size: {workGroupSize}");

        // NVIDIA prefers warp-aligned sizes (multiples of 32)
        Assert.True(workGroupSize % 32 == 0, $"Work group size {workGroupSize} is not warp-aligned (multiple of 32)");
        Assert.True(workGroupSize >= 128, "Work group size should be at least 128 (4 warps)");
        Assert.True(workGroupSize <= (int)nvidiaDevice!.MaxWorkGroupSize, "Work group size exceeds device maximum");

        Output.WriteLine($"✓ Work group size {workGroupSize} is warp-aligned (divisible by 32)");
    }

    [SkippableFact(DisplayName = "004: Intel Optimizations - SIMD-aligned work groups")]
    public void IntelOptimizations_SimdAlignedWorkGroups()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var intelDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU);

        Skip.If(intelDevice == null, "Intel GPU not available");

        var platform = GetPlatformForDevice(deviceManager, intelDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var workGroupSize = adapter.GetOptimalWorkGroupSize(intelDevice!, 128);

        // Assert
        Output.WriteLine($"Intel optimal work group size: {workGroupSize}");

        // Intel prefers SIMD-aligned sizes (multiples of 16)
        Assert.True(workGroupSize % 16 == 0, $"Work group size {workGroupSize} is not SIMD-aligned (multiple of 16)");
        Assert.True(workGroupSize >= 64, "Work group size should be at least 64");
        Assert.True(workGroupSize <= (int)intelDevice!.MaxWorkGroupSize, "Work group size exceeds device maximum");

        Output.WriteLine($"✓ Work group size {workGroupSize} is SIMD-aligned (divisible by 16)");
    }

    [SkippableFact(DisplayName = "005: NVIDIA Optimizations - 128-byte buffer alignment")]
    public void NvidiaOptimizations_BufferAlignment()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var nvidiaDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));

        Skip.If(nvidiaDevice == null, "NVIDIA GPU not available");

        var platform = GetPlatformForDevice(deviceManager, nvidiaDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var alignment = adapter.GetRecommendedBufferAlignment(nvidiaDevice!);

        // Assert
        Output.WriteLine($"NVIDIA buffer alignment: {alignment} bytes");
        Assert.Equal(128, alignment); // NVIDIA prefers 128-byte alignment for coalesced access

        Output.WriteLine("✓ NVIDIA buffer alignment is 128 bytes (optimal for coalesced memory access)");
    }

    [SkippableFact(DisplayName = "006: Intel Optimizations - 64-byte buffer alignment")]
    public void IntelOptimizations_BufferAlignment()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var intelDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU);

        Skip.If(intelDevice == null, "Intel GPU not available");

        var platform = GetPlatformForDevice(deviceManager, intelDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var alignment = adapter.GetRecommendedBufferAlignment(intelDevice!);

        // Assert
        Output.WriteLine($"Intel buffer alignment: {alignment} bytes");
        Assert.Equal(64, alignment); // Intel prefers 64-byte alignment (cache line size)

        Output.WriteLine("✓ Intel buffer alignment is 64 bytes (matches CPU cache line size)");
    }

    [SkippableFact(DisplayName = "007: NVIDIA Optimizations - Aggressive compiler flags")]
    public void NvidiaOptimizations_CompilerFlags()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var nvidiaDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));

        Skip.If(nvidiaDevice == null, "NVIDIA GPU not available");

        var platform = GetPlatformForDevice(deviceManager, nvidiaDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var compilerOptions = adapter.GetCompilerOptions(enableOptimizations: true);

        // Assert
        Output.WriteLine($"NVIDIA compiler options: {compilerOptions}");

        Assert.Contains("-cl-mad-enable", compilerOptions);
        Assert.Contains("-cl-fast-relaxed-math", compilerOptions);
        Assert.Contains("-cl-denorms-are-zero", compilerOptions); // NVIDIA-specific

        Output.WriteLine("✓ NVIDIA compiler flags include aggressive optimizations");
    }

    [SkippableFact(DisplayName = "008: Intel Optimizations - Conservative compiler flags")]
    public void IntelOptimizations_CompilerFlags()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var intelDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU);

        Skip.If(intelDevice == null, "Intel GPU not available");

        var platform = GetPlatformForDevice(deviceManager, intelDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var compilerOptions = adapter.GetCompilerOptions(enableOptimizations: true);

        // Assert
        Output.WriteLine($"Intel compiler options: {compilerOptions}");

        Assert.Contains("-cl-mad-enable", compilerOptions);
        Assert.Contains("-cl-fast-relaxed-math", compilerOptions);
        // Intel uses more conservative flags (no -cl-denorms-are-zero)
        Assert.DoesNotContain("-cl-denorms-are-zero", compilerOptions);

        Output.WriteLine("✓ Intel compiler flags are more conservative for compatibility");
    }

    [SkippableFact(DisplayName = "009: NVIDIA Optimizations - Out-of-order execution enabled")]
    public void NvidiaOptimizations_OutOfOrderExecution()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var nvidiaDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));

        Skip.If(nvidiaDevice == null, "NVIDIA GPU not available");

        var platform = GetPlatformForDevice(deviceManager, nvidiaDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var baseProperties = new Backends.OpenCL.Execution.QueueProperties
        {
            InOrderExecution = true // Start with in-order
        };

        var optimizedProperties = adapter.ApplyVendorOptimizations(baseProperties, nvidiaDevice!);

        // Assert
        Output.WriteLine($"NVIDIA queue properties:");
        Output.WriteLine($"  In-Order Execution: {optimizedProperties.InOrderExecution}");

        Assert.False(optimizedProperties.InOrderExecution); // NVIDIA prefers out-of-order
        Output.WriteLine("✓ NVIDIA enables out-of-order execution for better GPU utilization");
    }

    [SkippableFact(DisplayName = "010: Intel Arc Optimizations - Discrete GPU detection")]
    public void IntelOptimizations_DiscreteGpuDetection()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();
        var intelDevice = devices.FirstOrDefault(d =>
            d.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU);

        Skip.If(intelDevice == null, "Intel GPU not available");

        var platform = GetPlatformForDevice(deviceManager, intelDevice!);
        var adapter = VendorAdapterFactory.GetAdapter(platform!);

        // Act
        var supportsPersistentKernels = adapter.SupportsPersistentKernels(intelDevice!);
        var workGroupSize = adapter.GetOptimalWorkGroupSize(intelDevice!, 128);

        // Assert
        Output.WriteLine($"Intel GPU: {intelDevice!.Name}");
        Output.WriteLine($"  Compute Units: {intelDevice.MaxComputeUnits}");
        Output.WriteLine($"  Supports Persistent Kernels: {supportsPersistentKernels}");
        Output.WriteLine($"  Optimal Work Group Size: {workGroupSize}");

        // Arc series has 96+ EUs
        var isDiscrete = intelDevice.MaxComputeUnits >= 96;
        Output.WriteLine($"  Detected as: {(isDiscrete ? "Discrete GPU (Arc series)" : "Integrated GPU")}");

        if (isDiscrete)
        {
            Assert.True(supportsPersistentKernels, "Arc discrete GPUs should support persistent kernels");
            Assert.True(workGroupSize >= 256 || workGroupSize == (int)intelDevice.MaxWorkGroupSize,
                "Arc GPUs should prefer 256 work group size");
            Output.WriteLine("✓ Discrete Intel Arc GPU optimizations applied");
        }
        else
        {
            Output.WriteLine("✓ Integrated Intel GPU optimizations applied");
        }
    }

    [SkippableFact(DisplayName = "011: Extension Reliability - FP64 support")]
    public void ExtensionReliability_FP64Support()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.AllDevices.ToList();

        foreach (var device in devices.Where(d => d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU))
        {
            var platform = GetPlatformForDevice(deviceManager, device);
            var adapter = VendorAdapterFactory.GetAdapter(platform!);

            var hasFP64Extension = device.SupportsDoublePrecision;
            var isReliable = adapter.IsExtensionReliable("cl_khr_fp64", device);

            Output.WriteLine($"\n{device.Vendor} - {device.Name}:");
            Output.WriteLine($"  Has FP64 Extension: {hasFP64Extension}");
            Output.WriteLine($"  FP64 Reliable: {isReliable}");

            if (hasFP64Extension)
            {
                var reliabilityStatus = isReliable ? "is" : "is NOT";
                Output.WriteLine($"  ✓ FP64 extension {reliabilityStatus} considered reliable by {adapter.VendorName}");
            }
        }
    }

    [SkippableFact(DisplayName = "012: Vendor Adapter Factory - Detects all available vendors")]
    public void VendorAdapterFactory_DetectsAllVendors()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL not available");

        // Arrange
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var platforms = deviceManager.Platforms;

        Output.WriteLine($"Detected {platforms.Count} OpenCL platform(s):");

        // Act & Assert
        foreach (var platform in platforms)
        {
            var adapter = VendorAdapterFactory.GetAdapter(platform);
            var vendor = VendorAdapterFactory.DetectVendor(platform);

            Output.WriteLine($"\nPlatform: {platform.Name}");
            Output.WriteLine($"  Vendor String: {platform.Vendor}");
            Output.WriteLine($"  Detected Vendor: {vendor}");
            Output.WriteLine($"  Adapter: {adapter.VendorName}");
            Output.WriteLine($"  Devices: {platform.AvailableDevices.Count}");

            Assert.NotNull(adapter);
            Assert.True(adapter.CanHandle(platform));

            foreach (var device in platform.AvailableDevices.Where(d => d.Type == Backends.OpenCL.Types.Native.DeviceType.GPU))
            {
                var workGroupSize = adapter.GetOptimalWorkGroupSize(device, 128);
                var alignment = adapter.GetRecommendedBufferAlignment(device);

                Output.WriteLine($"    GPU: {device.Name}");
                Output.WriteLine($"      Optimal Work Group Size: {workGroupSize}");
                Output.WriteLine($"      Buffer Alignment: {alignment} bytes");
            }
        }
    }

    // Helper methods

    private OpenCLPlatformInfo? GetPlatformForDevice(OpenCLDeviceManager deviceManager, OpenCLDeviceInfo device)
    {
        var platforms = deviceManager.Platforms;
        return platforms.FirstOrDefault(p => p.AvailableDevices.Any(d => d.DeviceId.Handle == device.DeviceId.Handle));
    }
}
