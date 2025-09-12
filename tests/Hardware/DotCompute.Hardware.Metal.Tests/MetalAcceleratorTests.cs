// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Accelerators;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Hardware tests for Metal accelerator functionality
    /// </summary>
    [Trait("Category", "RequiresMetal")]
    public class MetalAcceleratorTests : MetalTestBase
    {
        public MetalAcceleratorTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task Device_Initialization_Should_Succeed()
        {
            // Skip if Metal hardware is not available
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            
            accelerator.Should().NotBeNull();
            accelerator!.Info.Should().NotBeNull();
            
            Output.WriteLine($"Device Name: {accelerator.Info.Name}");
            Output.WriteLine($"Device Type: {accelerator.Info.DeviceType}");
            Output.WriteLine($"Total Memory: {accelerator.Info.TotalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB");
            Output.WriteLine($"Compute Units: {accelerator.Info.ComputeUnits}");
            
            // Proper async disposal
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Apple_Silicon_Should_Have_Unified_Memory()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");
            Skip.IfNot(IsAppleSilicon(), "Not running on Apple Silicon");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            var capabilities = accelerator!.Info.Capabilities;
            capabilities.Should().NotBeNull();
            
            if (capabilities!.TryGetValue("UnifiedMemory", out var unified))
            {
                unified.Should().BeOfType<bool>();
                ((bool)unified).Should().BeTrue("Apple Silicon should have unified memory");
            }
            
            Output.WriteLine($"Running on Apple Silicon with unified memory architecture");
            
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Memory_Allocation_Should_Work()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            const int bufferSize = 1024 * 1024; // 1 MB
            var buffer = await accelerator!.Memory.AllocateAsync<float>(bufferSize / sizeof(float));
            
            buffer.Should().NotBeNull();
            buffer.SizeInBytes.Should().Be(bufferSize);
            buffer.Length.Should().Be(bufferSize / sizeof(float));
            
            Output.WriteLine($"Allocated {bufferSize} bytes on Metal device");
            Output.WriteLine($"Buffer element count: {buffer.Length}");
            
            await buffer.DisposeAsync();
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Memory_Transfer_Host_To_Device_Should_Work()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            const int elementCount = 1024;
            var hostData = new float[elementCount];
            
            // Initialize test data
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = i * 2.5f;
            }
            
            // Allocate device buffer
            var deviceBuffer = await accelerator!.Memory.AllocateAsync<float>(elementCount);
            
            // Copy to device
            await deviceBuffer.CopyFromAsync(hostData.AsMemory());
            
            // Copy back to verify
            var resultData = new float[elementCount];
            await deviceBuffer.CopyToAsync(resultData.AsMemory());
            
            // Verify data
            for (var i = 0; i < elementCount; i++)
            {
                resultData[i].Should().BeApproximately(hostData[i], 0.001f);
            }
            
            Output.WriteLine($"Successfully transferred {elementCount} floats to and from Metal device");
            
            await deviceBuffer.DisposeAsync();
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public async Task Multiple_Device_Buffers_Should_Work()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            const int bufferCount = 10;
            const int elementsPerBuffer = 1024;
            
            var buffers = new List<DotCompute.Abstractions.IUnifiedMemoryBuffer<float>>();
            
            // Allocate multiple buffers
            for (var i = 0; i < bufferCount; i++)
            {
                var buffer = await accelerator!.Memory.AllocateAsync<float>(elementsPerBuffer);
                buffers.Add(buffer);
            }
            
            buffers.Should().HaveCount(bufferCount);
            
            // Verify all buffers are valid
            foreach (var buffer in buffers)
            {
                buffer.Should().NotBeNull();
                buffer.Length.Should().Be(elementsPerBuffer);
            }
            
            Output.WriteLine($"Successfully allocated {bufferCount} buffers with {elementsPerBuffer} elements each");
            
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
            
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public void Device_Discovery_Should_Find_At_Least_One_Device()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            var devices = Factory.GetAvailableDevices();
            
            devices.Should().NotBeNull();
            devices.Should().NotBeEmpty("At least one Metal device should be available");
            
            foreach (var device in devices)
            {
                Output.WriteLine($"Device {device.Index}: {device.Name}");
                Output.WriteLine($"  Type: {device.DeviceType}");
                Output.WriteLine($"  Memory: {device.TotalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB");
                Output.WriteLine($"  Compute Units: {device.ComputeUnits}");
                Output.WriteLine($"  Available: {device.IsAvailable}");
            }
        }

        [SkippableFact]
        public async Task Device_Should_Support_Basic_Compute()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            await using var accelerator = Factory.CreateProductionAccelerator();
            accelerator.Should().NotBeNull();

            // Check compute capability
            var info = accelerator!.Info;
            info.ComputeCapability.Should().NotBeNull();
            
            Output.WriteLine($"Compute Capability: {info.ComputeCapability}");
            
            // Check for basic compute support
            var capabilities = info.Capabilities;
            if (capabilities != null)
            {
                foreach (var cap in capabilities)
                {
                    Output.WriteLine($"  {cap.Key}: {cap.Value}");
                }
            }
            
            // accelerator will be disposed automatically via await using
        }

        [SkippableFact]
        public void MacOS_Version_Should_Be_Sufficient_For_Metal()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

            var version = GetMacOSVersion();
            
            // Metal requires macOS 10.11, compute shaders require 10.13
            version.Major.Should().BeGreaterThanOrEqualTo(10);
            if (version.Major == 10)
            {
                version.Minor.Should().BeGreaterThanOrEqualTo(13, 
                    "macOS 10.13 (High Sierra) or later is required for Metal compute shaders");
            }
            
            Output.WriteLine($"macOS Version: {version}");
            Output.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
            Output.WriteLine($"Is Apple Silicon: {IsAppleSilicon()}");
        }
    }
}