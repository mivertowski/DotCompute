// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using AcceleratorType = DotCompute.Abstractions.AcceleratorType;
using CompilationOptions = DotCompute.Abstractions.CompilationOptions;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using KernelArguments = DotCompute.Abstractions.KernelArguments;
using KernelDefinition = DotCompute.Abstractions.KernelDefinition;

namespace DotCompute.Core.Compute
{

/// <summary>
/// Provides CPU accelerator instances.
/// </summary>
public class CpuAcceleratorProvider(ILogger<CpuAcceleratorProvider> logger) : IAcceleratorProvider
{
    private readonly ILogger<CpuAcceleratorProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string Name => "CPU";

    public AcceleratorType[] SupportedTypes => [AcceleratorType.CPU];
    private static readonly char[] _separator = [' '];

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering CPU accelerators");

        var cpuInfo = new AcceleratorInfo(
            AcceleratorType.CPU,
            GetProcessorName(),
            "N/A", // driver version
            GetAvailableMemory(), // memory size
            Environment.ProcessorCount, // compute units
            0, // max clock frequency
            GetProcessorCapability(), // compute capability
            GetAvailableMemory() / 4, // max shared memory per block
            true // is unified memory
        );

        // Create a simple CPU accelerator implementation
        var accelerator = new SimpleCpuAccelerator(cpuInfo, _logger);
        return ValueTask.FromResult<IEnumerable<IAccelerator>>(new[] { accelerator });
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        if (info.DeviceType != "CPU")
        {
            throw new ArgumentException("Can only create CPU accelerators", nameof(info));
        }

        var accelerator = new SimpleCpuAccelerator(info, _logger);
        return ValueTask.FromResult<IAccelerator>(accelerator);
    }

    private static string GetProcessorName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux CPU"; // Would need to parse /proc/cpuinfo
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS CPU"; // Would need to use sysctl
        }
        else
        {
            return "Unknown CPU";
        }
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
        {
            return new Version(2, 0);
        }
        else if (System.Runtime.Intrinsics.X86.Sse41.IsSupported)
        {
            return new Version(1, 4);
        }
        else
            {
                return System.Numerics.Vector.IsHardwareAccelerated ? new Version(1, 0) : new Version(0, 1);
            }
        }

    private static long GetAvailableMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsAvailableMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxAvailableMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacOSAvailableMemory();
            }
            else
            {
                // Fallback: use conservative estimate
                return GetFallbackMemory();
            }
        }
        catch
        {
            // If platform-specific detection fails, return conservative estimate
            return GetFallbackMemory();
        }
    }

    private static long GetWindowsAvailableMemory()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "wmic";
            process.StartInfo.Arguments = "OS get TotalVisibleMemorySize /value";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("TotalVisibleMemorySize"))
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2 && long.TryParse(parts[1].Trim(), out var kb))
                    {
                        return kb * 1024; // Convert KB to bytes
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }
        return GetFallbackMemory();
    }

    private static long GetLinuxAvailableMemory()
    {
        try
        {
            if (System.IO.File.Exists("/proc/meminfo"))
            {
                var lines = System.IO.File.ReadAllLines("/proc/meminfo");
                var availableLine = lines.FirstOrDefault(l => l.StartsWith("MemAvailable:"));
                if (availableLine != null)
                {
                    var parts = availableLine.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    {
                        return kb * 1024; // Convert KB to bytes
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }
        return GetFallbackMemory();
    }

    private static long GetMacOSAvailableMemory()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "sysctl";
            process.StartInfo.Arguments = "hw.memsize";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var parts = output.Split(':');
            if (parts.Length == 2 && long.TryParse(parts[1].Trim(), out var bytes))
            {
                return bytes;
            }
        }
        catch
        {
            // Fall through to default
        }
        return GetFallbackMemory();
    }

    private static long GetFallbackMemory()
    {
        // Conservative fallback based on available managed memory
        var managedMemory = GC.GetTotalMemory(false);
        // Assume we can use 8x the managed heap size or at least 2GB
        return Math.Max(managedMemory * 8, 2L * 1024 * 1024 * 1024);
    }
}

/// <summary>
/// Simple CPU accelerator implementation for Core.
/// The optimized implementation is available in DotCompute.Backends.CPU.
/// </summary>
internal class SimpleCpuAccelerator : IAccelerator
{
    private readonly ILogger _logger;
    private readonly SimpleCpuMemoryManager _memoryManager;
    private bool _disposed;

    public SimpleCpuAccelerator(AcceleratorInfo info, ILogger logger)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryManager = new SimpleCpuMemoryManager(this, logger);
    }

    public AcceleratorInfo Info { get; }
    
    public AcceleratorType Type => AcceleratorType.CPU;

    public IMemoryManager Memory => _memoryManager;

    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        _logger.LogDebug("Compiling simple CPU kernel: {KernelName}", definition.Name);

        // Create a simple compiled kernel that does basic execution
        var compiledKernel = new SimpleCpuCompiledKernel(definition.Name, definition);
        
        _logger.LogInformation("Successfully compiled simple CPU kernel: {KernelName}", definition.Name);
        return await ValueTask.FromResult<ICompiledKernel>(compiledKernel);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        // CPU operations are synchronous by default
        => ValueTask.CompletedTask;

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
internal class SimpleCpuMemoryManager(IAccelerator accelerator, ILogger logger) : IMemoryManager, IDisposable
{
    private readonly IAccelerator _accelerator = accelerator;
    private readonly ILogger _logger = logger;
    private readonly List<SimpleCpuMemoryBuffer> _allocatedBuffers = [];

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        var buffer = new SimpleCpuMemoryBuffer(sizeInBytes, options);
        _allocatedBuffers.Add(buffer);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var buffer = new SimpleCpuMemoryBuffer(sizeInBytes, options);
        await buffer.CopyFromHostAsync(source, cancellationToken: cancellationToken);
        _allocatedBuffers.Add(buffer);
        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is not SimpleCpuMemoryBuffer cpuBuffer)
        {
            throw new ArgumentException("Buffer must be a CPU buffer", nameof(buffer));
        }

        return new SimpleCpuMemoryBufferView(cpuBuffer, offset, length);
    }

    public async ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return await AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var memory = new ReadOnlyMemory<T>(data.ToArray());
        buffer.CopyFromHostAsync(memory).AsTask().Wait();
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var memory = new Memory<T>(new T[data.Length]);
        buffer.CopyToHostAsync(memory).AsTask().Wait();
        memory.Span.CopyTo(data);
    }

    public void Free(IMemoryBuffer buffer)
    {
        if (buffer is SimpleCpuMemoryBuffer cpuBuffer)
        {
            _allocatedBuffers.Remove(cpuBuffer);
            cpuBuffer.Dispose();
        }
        else
        {
            buffer?.Dispose();
        }
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
internal class SimpleCpuMemoryBuffer : IMemoryBuffer
{
    private readonly byte[] _data;
    internal bool _disposed;

    public SimpleCpuMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        if (sizeInBytes > int.MaxValue)
        {
            throw new ArgumentException("Size too large for CPU buffer", nameof(sizeInBytes));
        }

        _data = new byte[sizeInBytes];
        SizeInBytes = sizeInBytes;
        Options = options;
    }

    public long SizeInBytes { get; }

    public MemoryOptions Options { get; }

    public bool IsDisposed => _disposed;

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
        if (!_disposed)
        {
            // Clear the data array to help GC
            if (_data != null && _data.Length > 0)
            {
                Array.Clear(_data, 0, _data.Length);
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
/// View over a CPU memory buffer.
/// </summary>
internal class SimpleCpuMemoryBufferView(SimpleCpuMemoryBuffer parent, long offset, long length) : IMemoryBuffer
{
    private readonly SimpleCpuMemoryBuffer _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    private readonly long _offset = offset;

    public long SizeInBytes { get; } = length;

    public MemoryOptions Options { get; } = parent.Options;

    public bool IsDisposed => _parent._disposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public void Dispose()
    {
        // Views don't dispose the parent
    }

    public ValueTask DisposeAsync()
        // Views don't own the memory
        => ValueTask.CompletedTask;
}

/// <summary>
/// Simple CPU compiled kernel implementation.
/// </summary>
internal class SimpleCpuCompiledKernel(string name, KernelDefinition definition) : ICompiledKernel
{
    private readonly KernelDefinition _definition = definition;
    private bool _disposed;

    public string Name { get; } = name;

    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimpleCpuCompiledKernel));
        }

        // Simple kernel execution - in a real implementation,
        // this would parse and execute the kernel code
        // For now, this is a no-op placeholder
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
}
