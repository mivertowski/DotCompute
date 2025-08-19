// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotCompute.Backends.Metal
{

public class MetalMemoryTests : IClassFixture<MetalTestFixture>
{
    private readonly MetalTestFixture _fixture;
    private readonly IAccelerator _accelerator;

    public MetalMemoryTests(MetalTestFixture fixture)
    {
        _fixture = fixture;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && MetalUtilities.IsMetalAvailable())
        {
            _accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        }
    }

    [SkippableFact]
    public void CanAllocateAndFreeMemory()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        const int size = 1024 * 1024; // 1MB
        
        var initialAllocated = _accelerator.Memory.GetAllocatedMemory();
        
        var memory = _accelerator.Memory.Allocate(size);
        
        memory.Should().NotBeNull();
        memory.SizeInBytes.Should().Be(size);
        _accelerator.Memory.GetAllocatedMemory().Should().Be(initialAllocated + size);
        
        _accelerator.Memory.Free(memory);
        
        _accelerator.Memory.GetAllocatedMemory().Should().Be(initialAllocated);
    }

    [SkippableFact]
    public void CanCopyDataToAndFromDevice()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        const int count = 256;
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = i * 3.14f;
        }

        var memory = _accelerator.Memory.Allocate(count * sizeof(float));
        
        try
        {
            // Copy to device
            _accelerator.Memory.CopyToDevice(memory, MemoryMarshal.AsBytes(data.AsSpan();
            
            // Copy back from device
            var result = new float[count];
            _accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), memory);
            
            result.Should().BeEquivalentTo(data);
        }
        finally
        {
            _accelerator.Memory.Free(memory);
        }
    }

    [SkippableFact]
    public void CanCopyBetweenDeviceBuffers()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        const int count = 128;
        var data = new int[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = i * i;
        }

        var source = _accelerator.Memory.Allocate(count * sizeof(int));
        var destination = _accelerator.Memory.Allocate(count * sizeof(int));
        
        try
        {
            // Copy data to source buffer
            _accelerator.Memory.CopyToDevice(source, MemoryMarshal.AsBytes(data.AsSpan();
            
            // Copy between device buffers
            _accelerator.Memory.Copy(destination, source);
            
            // Verify destination has the data
            var result = new int[count];
            _accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), destination);
            
            result.Should().BeEquivalentTo(data);
        }
        finally
        {
            _accelerator.Memory.Free(source);
            _accelerator.Memory.Free(destination);
        }
    }

    [SkippableFact]
    public void CanUseMemoryHints()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        const int size = 4096;
        
        // Test different memory hints
        var sharedMemory = _accelerator.Memory.Allocate(size, MemoryHints.HostVisible | MemoryHints.HostCoherent);
        var privateMemory = _accelerator.Memory.Allocate(size, MemoryHints.DeviceLocal);
        var managedMemory = _accelerator.Memory.Allocate(size, MemoryHints.HostCached);
        
        try
        {
            sharedMemory.Should().NotBeNull();
            privateMemory.Should().NotBeNull();
            managedMemory.Should().NotBeNull();
            
            // All should have the requested size
            sharedMemory.SizeInBytes.Should().Be(size);
            privateMemory.SizeInBytes.Should().Be(size);
            managedMemory.SizeInBytes.Should().Be(size);
        }
        finally
        {
            _accelerator.Memory.Free(sharedMemory);
            _accelerator.Memory.Free(privateMemory);
            _accelerator.Memory.Free(managedMemory);
        }
    }

    [SkippableFact]
    public async Task CanAllocateMemoryAsync()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        const int size = 2048 * sizeof(double);
        
        var memory = await _accelerator.Memory.AllocateAsync(size, MemoryHints.None);
        
        try
        {
            memory.Should().NotBeNull();
            memory.SizeInBytes.Should().Be(size);
        }
        finally
        {
            _accelerator.Memory.Free(memory);
        }
    }

    [SkippableFact]
    public void ShouldThrowOnInvalidAllocation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        // Negative size
        Action negativeAlloc = () => _accelerator.Memory.Allocate(-1);
        negativeAlloc.Should().Throw<ArgumentOutOfRangeException>();
        
        // Zero size
        Action zeroAlloc = () => _accelerator.Memory.Allocate(0);
        zeroAlloc.Should().Throw<ArgumentOutOfRangeException>();
        
        // Exceeding max allocation
        Action hugeAlloc = () => _accelerator.Memory.Allocate(long.MaxValue);
        hugeAlloc.Should().Throw<ArgumentOutOfRangeException>();
    }

    [SkippableFact]
    public void CanHandlePartialCopies()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        const int totalSize = 1024;
        const int partialSize = 256;
        const int offset = 128;
        
        var source = new byte[totalSize];
        for (int i = 0; i < totalSize; i++)
        {
            source[i] = (byte)(i % 256);
        }
        
        var memory = _accelerator.Memory.Allocate(totalSize);
        
        try
        {
            // Copy full data to device
            _accelerator.Memory.CopyToDevice(memory, source);
            
            // Read back partial data
            var partial = new byte[partialSize];
            _accelerator.Memory.CopyFromDevice(partial, memory, offset, partialSize);
            
            // Verify partial data
            for (int i = 0; i < partialSize; i++)
            {
                partial[i].Should().Be(source[offset + i]);
            }
        }
        finally
        {
            _accelerator.Memory.Free(memory);
        }
    }
}}
