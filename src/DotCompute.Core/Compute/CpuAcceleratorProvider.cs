// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Accelerators;

/// <summary>
/// Provides CPU accelerator instances.
/// </summary>
public class CpuAcceleratorProvider : IAcceleratorProvider
{
    private readonly ILogger<CpuAcceleratorProvider> _logger;

    public CpuAcceleratorProvider(ILogger<CpuAcceleratorProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "CPU";

    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.CPU };

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering CPU accelerators");

        var cpuInfo = new AcceleratorInfo
        {
            Id = "cpu-0",
            Name = GetProcessorName(),
            DeviceType = "CPU",
            Vendor = GetProcessorVendor(),
            ComputeCapability = GetProcessorCapability(),
            TotalMemory = GetAvailableMemory(),
            ComputeUnits = Environment.ProcessorCount,
            MaxClockFrequency = 0, // Would require platform-specific code to get
            Capabilities = new Dictionary<string, object>
            {
                ["Architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["IsHardwareAccelerated"] = System.Numerics.Vector.IsHardwareAccelerated,
                ["VectorWidth"] = System.Numerics.Vector<float>.Count * sizeof(float)
            }
        };

        var accelerator = new CpuAccelerator(cpuInfo, _logger);
        return ValueTask.FromResult<IEnumerable<IAccelerator>>(new[] { accelerator });
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        if (info.DeviceType != "CPU")
            throw new ArgumentException("Can only create CPU accelerators", nameof(info));

        var accelerator = new CpuAccelerator(info, _logger);
        return ValueTask.FromResult<IAccelerator>(accelerator);
    }

    private static string GetProcessorName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux CPU"; // Would need to parse /proc/cpuinfo
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS CPU"; // Would need to use sysctl
        else
            return "Unknown CPU";
    }

    private static string GetProcessorVendor()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X64 or Architecture.X86 => "x86",
            Architecture.Arm or Architecture.Arm64 => "ARM",
            _ => "Unknown"
        };
    }

    private static Version GetProcessorCapability()
    {
        // Simple version based on SIMD support
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            return new Version(2, 0);
        else if (System.Runtime.Intrinsics.X86.Sse41.IsSupported)
            return new Version(1, 4);
        else if (System.Numerics.Vector.IsHardwareAccelerated)
            return new Version(1, 0);
        else
            return new Version(0, 1);
    }

    private static long GetAvailableMemory()
    {
        // This is a simplified implementation
        // In production, would use platform-specific APIs
        return GC.GetTotalMemory(false);
    }
}

/// <summary>
/// CPU accelerator implementation.
/// </summary>
internal class CpuAccelerator : IAccelerator
{
    private readonly ILogger _logger;
    private readonly CpuMemoryManager _memoryManager;
    private bool _disposed;

    public CpuAccelerator(AcceleratorInfo info, ILogger logger)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryManager = new CpuMemoryManager(this, logger);
    }

    public AcceleratorInfo Info { get; }

    public IMemoryManager Memory => _memoryManager;

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // For CPU, we would typically use expression trees or delegates
        // This is a placeholder implementation
        await Task.Yield(); // Simulate async work
        
        return new CpuCompiledKernel(definition.Name);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // CPU operations are synchronous by default
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _memoryManager?.Dispose();
            _disposed = true;
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple CPU memory manager implementation.
/// </summary>
internal class CpuMemoryManager : IMemoryManager, IDisposable
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger _logger;
    private readonly List<CpuMemoryBuffer> _allocatedBuffers = new();

    public CpuMemoryManager(IAccelerator accelerator, ILogger logger)
    {
        _accelerator = accelerator;
        _logger = logger;
    }

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        var buffer = new CpuMemoryBuffer(sizeInBytes, options);
        _allocatedBuffers.Add(buffer);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var buffer = new CpuMemoryBuffer(sizeInBytes, options);
        buffer.CopyFromHostAsync(source).AsTask().Wait();
        _allocatedBuffers.Add(buffer);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is not CpuMemoryBuffer cpuBuffer)
            throw new ArgumentException("Buffer must be a CPU buffer", nameof(buffer));

        return new CpuMemoryBufferView(cpuBuffer, offset, length);
    }

    public void Dispose()
    {
        foreach (var buffer in _allocatedBuffers)
        {
            buffer.Dispose();
        }
        _allocatedBuffers.Clear();
    }
}

/// <summary>
/// Simple CPU memory buffer implementation.
/// </summary>
internal class CpuMemoryBuffer : IMemoryBuffer
{
    private readonly byte[] _data;
    private bool _disposed;

    public CpuMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        if (sizeInBytes > int.MaxValue)
            throw new ArgumentException("Size too large for CPU buffer", nameof(sizeInBytes));
            
        _data = new byte[sizeInBytes];
        SizeInBytes = sizeInBytes;
        Options = options;
    }

    public long SizeInBytes { get; }

    public MemoryOptions Options { get; }

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceSpan = source.Span;
        var destSpan = MemoryMarshal.Cast<byte, T>(_data.AsSpan((int)offset));
        sourceSpan.CopyTo(destSpan);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceSpan = MemoryMarshal.Cast<byte, T>(_data.AsSpan((int)offset));
        var destSpan = destination.Span;
        sourceSpan.CopyTo(destSpan);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// View over a CPU memory buffer.
/// </summary>
internal class CpuMemoryBufferView : IMemoryBuffer
{
    private readonly CpuMemoryBuffer _parent;
    private readonly long _offset;

    public CpuMemoryBufferView(CpuMemoryBuffer parent, long offset, long length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        SizeInBytes = length;
        Options = parent.Options;
    }

    public long SizeInBytes { get; }

    public MemoryOptions Options { get; }

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        // Views don't own the memory
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple CPU compiled kernel.
/// </summary>
internal class CpuCompiledKernel : ICompiledKernel
{
    public CpuCompiledKernel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        // Placeholder implementation
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}