// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Comprehensive tests for Metal memory operations including transfers, allocation, and unified memory.
/// Matches CUDA memory test patterns for cross-platform validation.
/// </summary>
[Trait("Category", "RequiresMetal")]
public sealed class MetalMemoryTransferTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly MetalAccelerator? _accelerator;

    public MetalMemoryTransferTests(ITestOutputHelper output)
    {
        _output = output;

        if (IsMetalAvailable())
        {
            var options = Options.Create(new MetalAcceleratorOptions());
            _accelerator = new MetalAccelerator(options, NullLogger<MetalAccelerator>.Instance);
        }
    }

    #region Memory Allocation Tests

    [SkippableFact]
    public async Task Memory_Allocation_Should_Work_On_Device()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int bufferSize = 1024 * 1024; // 1 MB
        await using var buffer = await _accelerator!.Memory.AllocateAsync<float>((int)(bufferSize / sizeof(float)));

        Assert.NotNull(buffer);
        Assert.Equal(bufferSize, buffer.SizeInBytes);

        _output.WriteLine($"✓ Allocated {bufferSize} bytes on Metal device");
    }

    [SkippableFact]
    public async Task Memory_Transfer_HostToDevice_Should_Work()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 1024;
        var hostData = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            hostData[i] = i * 2.5f;
        }

        await using var deviceBuffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await deviceBuffer.CopyFromAsync(hostData.AsMemory());
        stopwatch.Stop();

        var transferRate = (elementCount * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);

        _output.WriteLine($"Host to Device transfer: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Transfer rate: {transferRate:F2} MB/s");

        Assert.True(stopwatch.Elapsed.TotalSeconds < 1.0);
    }

    [SkippableFact]
    public async Task Memory_Transfer_DeviceToHost_Should_Work()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 1024;
        var originalData = new float[elementCount];
        var resultData = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            originalData[i] = (float)Math.Sin(i * 0.1);
        }

        await using var deviceBuffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        await deviceBuffer.CopyFromAsync(originalData.AsMemory());

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await deviceBuffer.CopyToAsync(resultData.AsMemory());
        stopwatch.Stop();

        for (var i = 0; i < elementCount; i++)
        {
            Assert.True(Math.Abs(resultData[i] - originalData[i]) < 0.0001f);
        }

        _output.WriteLine($"✓ Device to Host transfer: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
    }

    #endregion

    #region Unified Memory Tests

    [SkippableFact]
    public async Task Unified_Memory_Should_Work_If_Supported()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        if (!_accelerator!.Info.IsUnifiedMemory)
        {
            _output.WriteLine("⚠ Unified memory not supported - skipping");
            return;
        }

        const int elementCount = 1024;
        var testData = new float[elementCount];

        for (var i = 0; i < elementCount; i++)
        {
            testData[i] = i * 0.5f;
        }

        // For Metal with unified memory, regular allocation is unified by default
        await using var unifiedBuffer = await _accelerator.Memory.AllocateAsync<float>(elementCount);
        await unifiedBuffer.CopyFromAsync(testData.AsMemory());

        var resultData = new float[elementCount];
        await unifiedBuffer.CopyToAsync(resultData.AsMemory());

        for (var i = 0; i < elementCount; i++)
        {
            Assert.True(Math.Abs(resultData[i] - testData[i]) < 0.0001f);
        }

        _output.WriteLine("✓ Unified memory test completed successfully");
    }

    #endregion

    #region Large Transfer Tests

    [SkippableFact]
    public async Task Memory_Transfer_LargeBuffer_Should_Complete()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        const int elementCount = 16 * 1024 * 1024; // 64 MB
        var hostData = new float[elementCount];

        await using var deviceBuffer = await _accelerator!.Memory.AllocateAsync<float>(elementCount);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await deviceBuffer.CopyFromAsync(hostData.AsMemory());
        stopwatch.Stop();

        var sizeInMB = (elementCount * sizeof(float)) / (1024.0 * 1024.0);
        var transferRate = sizeInMB / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"✓ Large buffer transfer: {sizeInMB:F2} MB in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Transfer rate: {transferRate:F2} MB/s");

        Assert.True(stopwatch.Elapsed.TotalSeconds < 5.0, "Large transfer took too long");
    }

    #endregion

    private static bool IsMetalAvailable()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            var options = Options.Create(new MetalAcceleratorOptions());
            var accelerator = new MetalAccelerator(options, NullLogger<MetalAccelerator>.Instance);
            accelerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_accelerator != null)
        {
            await _accelerator.DisposeAsync();
        }
    }
}
