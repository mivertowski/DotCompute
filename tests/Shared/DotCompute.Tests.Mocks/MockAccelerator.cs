using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Mocks;

/// <summary>
/// Mock accelerator implementation for testing without hardware dependencies.
/// </summary>
public class MockAccelerator : IAccelerator
{
    private bool _isDisposed;
    private readonly Dictionary<string, object> _properties = new();
    private readonly AcceleratorInfo _info;
    private readonly IMemoryManager _memoryManager;

    public MockAccelerator(
        string name = "MockGPU", 
        AcceleratorType type = AcceleratorType.GPU,
        long totalMemory = 8L * 1024 * 1024 * 1024) // 8GB default
    {
        Name = name;
        Type = type;
        TotalMemory = totalMemory;
        AvailableMemory = totalMemory;
        DeviceId = Guid.NewGuid().ToString();
        MaxWorkGroupSize = 1024;
        MaxComputeUnits = 64;
        ClockRate = 1500; // MHz
        Features = AcceleratorFeature.DoublePrecision | AcceleratorFeature.UnifiedMemory;
        IsAvailable = true;

        // Initialize AcceleratorInfo with required properties
        _info = new AcceleratorInfo
        {
            Id = DeviceId,
            Name = name,
            DeviceType = type.ToString(),
            Vendor = "Mock Vendor",
            DriverVersion = "Mock Driver 1.0",
            TotalMemory = totalMemory,
            AvailableMemory = totalMemory,
            MaxSharedMemoryPerBlock = 48 * 1024,
            MaxMemoryAllocationSize = totalMemory,
            LocalMemorySize = 64 * 1024,
            IsUnifiedMemory = type == AcceleratorType.CPU,
            ComputeUnits = MaxComputeUnits,
            MaxClockFrequency = ClockRate,
            MaxThreadsPerBlock = MaxWorkGroupSize,
            ComputeCapability = new Version(8, 0),
            Capabilities = new Dictionary<string, object>(_properties)
        };

        _memoryManager = new MockMemoryManager(this);
    }

    public string Name { get; set; }
    public AcceleratorType Type { get; set; }
    public string DeviceId { get; set; }
    public long TotalMemory { get; set; }
    public long AvailableMemory { get; set; }
    public int MaxWorkGroupSize { get; set; }
    public int MaxComputeUnits { get; set; }
    public int ClockRate { get; set; }
    public AcceleratorFeature Features { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsDisposed => _isDisposed;

    // IAccelerator implementation
    public AcceleratorInfo Info => _info;
    public IMemoryManager Memory => _memoryManager;

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await Task.Delay(10, cancellationToken); // Simulate compilation time
        
        return new MockCompiledKernel(definition.Name ?? "MockKernel");
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsAvailable = false;
        if (_memoryManager is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

    public void SetProperty(string key, object value)
    {
        _properties[key] = value;
    }

    public void SimulateMemoryUsage(long bytesUsed)
    {
        AvailableMemory = Math.Max(0, TotalMemory - bytesUsed);
    }

    public void SimulateDeviceFailure()
    {
        IsAvailable = false;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockAccelerator));
    }
}

/// <summary>
/// Mock compiled kernel implementation for testing.
/// </summary>
public class MockCompiledKernel : ICompiledKernel
{
    private bool _isDisposed;

    public MockCompiledKernel(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockCompiledKernel));
        
        // Simulate kernel execution
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed) return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock memory manager implementation for testing.
/// </summary>
public class MockMemoryManager : IMemoryManager
{
    private readonly IAccelerator _accelerator;
    private bool _isDisposed;

    public MockMemoryManager(IAccelerator accelerator)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    public async ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes, 
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryManager));
        
        await Task.Yield(); // Simulate async operation
        return new MockMemoryBuffer(sizeInBytes, options);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryManager));
        
        var sizeInBytes = source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
        await buffer.CopyFromHostAsync(source, 0, cancellationToken);
        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryManager));
        
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset >= buffer.SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0 || offset + length > buffer.SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(length));
        
        // For mock, we create a new buffer that represents the view
        return new MockMemoryBufferView((MockMemoryBuffer)buffer, offset, length);
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed) return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock memory buffer implementation for testing.
/// </summary>
public class MockMemoryBuffer : IMemoryBuffer
{
    private readonly byte[] _data;
    private bool _isDisposed;

    public MockMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        if (sizeInBytes <= 0)
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));
        
        SizeInBytes = sizeInBytes;
        _data = new byte[sizeInBytes];
        Options = options;
    }

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _isDisposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var sizeInBytes = source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (offset < 0 || offset + sizeInBytes > SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
        // Copy data - for simplicity in mock, we just simulate the operation
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var sizeInBytes = destination.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (offset < 0 || offset + sizeInBytes > SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
        // Copy data - for simplicity in mock, we just simulate the operation
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed) return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryBuffer));
    }

    // For testing purposes - direct access to data
    public byte[] GetData() => _data;
}

/// <summary>
/// Mock memory buffer view implementation for testing.
/// </summary>
public class MockMemoryBufferView : IMemoryBuffer
{
    private readonly MockMemoryBuffer _parentBuffer;
    private readonly long _offset;
    private readonly long _length;
    private bool _isDisposed;

    public MockMemoryBufferView(MockMemoryBuffer parentBuffer, long offset, long length)
    {
        _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public MemoryOptions Options => _parentBuffer.Options;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryBufferView));
        
        return _parentBuffer.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryBufferView));
        
        return _parentBuffer.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed) return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}