// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.Native;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Comprehensive hardware detection tests for Metal backend
    /// </summary>
    [Trait("Category", "Hardware")]
    [Trait("Category", "Metal")]
    public class MetalHardwareDetectionTests : MetalTestBase
    {
        public MetalHardwareDetectionTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public void Metal_ShouldBeAvailable_OnMacOS()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test requires macOS");
            Skip.IfNot(GetMacOSVersion().Major >= 10 && GetMacOSVersion().Minor >= 13, "Test requires macOS 10.13+");

            // Act & Assert
            IsMetalAvailable().Should().BeTrue("Metal should be available on supported macOS versions");

            Output.WriteLine($"✓ Metal is available on macOS {GetMacOSVersion()}");
        }

        [SkippableFact]
        public void AppleSilicon_ShouldBeDetected_OnM1M2M3()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test requires macOS");
            Skip.IfNot(IsMetalAvailable(), "Test requires Metal support");

            // Act
            var isAppleSilicon = IsAppleSilicon();
            var deviceInfo = GetMetalDeviceInfoString();

            // Assert
            Output.WriteLine($"Device: {deviceInfo}");
            Output.WriteLine($"Apple Silicon: {isAppleSilicon}");
            Output.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");

            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                isAppleSilicon.Should().BeTrue("ARM64 architecture should indicate Apple Silicon");
            }
        }

        [SkippableFact]
        public void MetalDevice_ShouldCreateSuccessfully()
        {
            Skip.IfNot(IsMetalAvailable(), "Test requires Metal support");

            // Act
            var device = MetalNative.CreateSystemDefaultDevice();

            // Assert
            try
            {
                device.Should().NotBe(IntPtr.Zero, "Metal device creation should succeed");
                Output.WriteLine("✓ Metal device created successfully");
            }
            finally
            {
                if (device != IntPtr.Zero)
                {
                    MetalNative.ReleaseDevice(device);
                    Output.WriteLine("✓ Metal device released successfully");
                }
            }
        }

        [SkippableFact]
        public void MetalDevice_ShouldHaveExpectedCapabilities()
        {
            Skip.IfNot(IsMetalAvailable(), "Test requires Metal support");

            // Arrange
            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                // Act
                var deviceInfo = MetalNative.GetDeviceInfo(device);
                var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "";

                // Assert
                deviceInfo.MaxBufferLength.Should().BeGreaterThan(0, "Max buffer length should be positive");
                deviceName.Should().NotBeNullOrEmpty("Device name should not be empty");

                // Log capabilities
                LogMetalDeviceCapabilities();

                // Additional validation for Apple Silicon
                if (IsAppleSilicon())
                {
                    deviceInfo.HasUnifiedMemory.Should().BeTrue("Apple Silicon should have unified memory");
                    deviceName.Should().Contain("Apple", "Apple Silicon devices should have 'Apple' in the name");
                    deviceInfo.MaxBufferLength.Should().BeGreaterThan(1024L * 1024 * 1024, 
                        "Apple Silicon should have >1GB max buffer length");
                }

                Output.WriteLine($"✓ Device capabilities validated for {deviceName}");
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void MetalCommandQueue_ShouldCreateSuccessfully()
        {
            Skip.IfNot(IsMetalAvailable(), "Test requires Metal support");

            // Arrange
            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                // Act
                var commandQueue = MetalNative.CreateCommandQueue(device);

                // Assert
                try
                {
                    commandQueue.Should().NotBe(IntPtr.Zero, "Command queue creation should succeed");
                    Output.WriteLine("✓ Metal command queue created successfully");
                }
                finally
                {
                    if (commandQueue != IntPtr.Zero)
                    {
                        MetalNative.ReleaseCommandQueue(commandQueue);
                        Output.WriteLine("✓ Metal command queue released successfully");
                    }
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void MetalLibrary_ShouldCompileBasicShader()
        {
            Skip.IfNot(IsMetalAvailable(), "Test requires Metal support");

            // Arrange
            const string simpleShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void simple_test(device float* data [[ buffer(0) ]],
                       uint gid [[ thread_position_in_grid ]])
{
    data[gid] = data[gid] * 2.0f;
}";

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                // Act
                var library = MetalNative.CreateLibraryWithSource(device, simpleShader);

                // Assert
                try
                {
                    library.Should().NotBe(IntPtr.Zero, "Shader compilation should succeed");
                    Output.WriteLine("✓ Basic Metal shader compiled successfully");

                    // Try to get the function
                    var function = MetalNative.GetFunction(library, "simple_test");
                    try
                    {
                        function.Should().NotBe(IntPtr.Zero, "Function retrieval should succeed");
                        Output.WriteLine("✓ Metal function retrieved successfully");
                    }
                    finally
                    {
                        if (function != IntPtr.Zero)
                        {
                            MetalNative.ReleaseFunction(function);
                        }
                    }
                }
                finally
                {
                    if (library != IntPtr.Zero)
                    {
                        MetalNative.ReleaseLibrary(library);
                    }
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void MetalPipelineState_ShouldCreateFromFunction()
        {
            Skip.IfNot(IsMetalAvailable(), "Test requires Metal support");

            // Arrange
            const string simpleShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void pipeline_test(device float* data [[ buffer(0) ]],
                         uint gid [[ thread_position_in_grid ]])
{
    data[gid] = data[gid] + 1.0f;
}";

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var library = MetalNative.CreateLibraryWithSource(device, simpleShader);
                Skip.If(library == IntPtr.Zero, "Shader compilation failed");

                try
                {
                    var function = MetalNative.GetFunction(library, "pipeline_test");
                    Skip.If(function == IntPtr.Zero, "Function retrieval failed");

                    try
                    {
                        // Act
                        var error = IntPtr.Zero;
                        var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);

                        // Assert
                        try
                        {
                            if (error != IntPtr.Zero)
                            {
                                var errorDesc = MetalNative.GetErrorLocalizedDescription(error);
                                var errorMessage = Marshal.PtrToStringAnsi(errorDesc) ?? "Unknown error";
                                Output.WriteLine($"Pipeline creation error: {errorMessage}");
                                MetalNative.ReleaseError(error);
                            }

                            pipelineState.Should().NotBe(IntPtr.Zero, "Pipeline state creation should succeed");
                            Output.WriteLine("✓ Metal compute pipeline state created successfully");

                            // Test pipeline properties
                            var maxThreads = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
                            maxThreads.Should().BeGreaterThan(0, "Max threads per threadgroup should be positive");
                            Output.WriteLine($"✓ Max threads per threadgroup: {maxThreads}");
                        }
                        finally
                        {
                            if (pipelineState != IntPtr.Zero)
                            {
                                MetalNative.ReleaseComputePipelineState(pipelineState);
                            }
                        }
                    }
                    finally
                    {
                        MetalNative.ReleaseFunction(function);
                    }
                }
                finally
                {
                    MetalNative.ReleaseLibrary(library);
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void MetalSupport_ShouldBeConsistentAcrossChecks()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test requires macOS");

            // Act
            var isAvailable1 = IsMetalAvailable();
            var isAvailable2 = IsMetalAvailable();
            var nativeSupported = MetalNative.IsMetalSupported();

            // Assert
            isAvailable1.Should().Be(isAvailable2, "Metal availability should be consistent");
            isAvailable1.Should().Be(nativeSupported, "Native and managed Metal checks should agree");

            Output.WriteLine($"✓ Metal support consistency verified: {isAvailable1}");
        }
    }
}