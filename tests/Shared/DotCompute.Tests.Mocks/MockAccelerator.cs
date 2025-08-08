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

    public ValueTask<IAcceleratorContext> CreateContextAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var context = new MockAcceleratorContext(this);
        return ValueTask.FromResult<IAcceleratorContext>(context);
    }

    public ValueTask<AcceleratorInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var info = new AcceleratorInfo
        {
            Name = Name,
            DeviceId = DeviceId,
            Type = Type,
            TotalMemory = TotalMemory,
            AvailableMemory = AvailableMemory,
            MaxWorkGroupSize = MaxWorkGroupSize,
            MaxComputeUnits = MaxComputeUnits,
            ClockRate = ClockRate,
            Features = Features,
            DriverVersion = "Mock Driver 1.0",
            ComputeCapability = "8.0",
            Properties = new Dictionary<string, object>(_properties)
        };
        return ValueTask.FromResult(info);
    }

    public ValueTask<bool> IsCompatibleAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(IsAvailable);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AvailableMemory = TotalMemory;
        return ValueTask.CompletedTask;
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsAvailable = false;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockAccelerator));
    }
}

/// <summary>
/// Mock accelerator context for testing.
/// </summary>
public class MockAcceleratorContext : IAcceleratorContext
{
    private bool _isDisposed;
    private readonly List<IMemoryBuffer> _allocatedBuffers = new();

    public MockAcceleratorContext(IAccelerator accelerator)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    public IAccelerator Accelerator { get; }
    public bool IsDisposed => _isDisposed;

    public async ValueTask<IMemoryBuffer<T>> AllocateBufferAsync<T>(
        int elementCount, 
        MemoryType memoryType = MemoryType.DeviceLocal,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        await Task.Yield(); // Simulate async operation
        
        var buffer = new MockMemoryBuffer<T>(elementCount, memoryType);
        _allocatedBuffers.Add(buffer);
        return buffer;
    }

    public async ValueTask<IKernel> CompileKernelAsync(
        string name,
        string source,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await Task.Delay(10, cancellationToken); // Simulate compilation time
        
        return new MockKernel(name, source);
    }

    public async ValueTask ExecuteKernelAsync(
        IKernel kernel,
        Dim3 globalWorkSize,
        Dim3 localWorkSize,
        params object[] arguments)
    {
        ThrowIfDisposed();
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        
        await Task.Delay(5); // Simulate execution time
        
        // For testing, we can manipulate buffer contents if needed
        // This allows us to verify kernel execution in tests
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask FinishAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        foreach (var buffer in _allocatedBuffers)
        {
            buffer.Dispose();
        }
        _allocatedBuffers.Clear();
        
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockAcceleratorContext));
    }
}

/// <summary>
/// Mock kernel implementation for testing.
/// </summary>
public class MockKernel : IKernel
{
    public MockKernel(string name, string source)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Handle = IntPtr.Zero;
        BinarySize = source.Length * 2; // Simulate binary size
    }

    public string Name { get; }
    public string Source { get; }
    public IntPtr Handle { get; set; }
    public int BinarySize { get; }
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Mock memory buffer implementation for testing.
/// </summary>
public class MockMemoryBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    private readonly T[] _data;
    private bool _isDisposed;

    public MockMemoryBuffer(int elementCount, MemoryType memoryType)
    {
        if (elementCount <= 0)
            throw new ArgumentException("Element count must be positive", nameof(elementCount));
        
        ElementCount = elementCount;
        MemoryType = memoryType;
        _data = new T[elementCount];
        SizeInBytes = elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
    }

    public int ElementCount { get; }
    public long SizeInBytes { get; }
    public MemoryType MemoryType { get; }
    public bool IsDisposed => _isDisposed;

    public ValueTask<T[]> ReadAsync(int offset = 0, int? count = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var actualCount = count ?? (ElementCount - offset);
        if (offset < 0 || offset >= ElementCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (actualCount < 0 || offset + actualCount > ElementCount)
            throw new ArgumentOutOfRangeException(nameof(count));
        
        var result = new T[actualCount];
        Array.Copy(_data, offset, result, 0, actualCount);
        return ValueTask.FromResult(result);
    }

    public ValueTask ReadAsync(T[] destination, int sourceOffset, int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (sourceOffset < 0 || sourceOffset >= ElementCount)
            throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        if (count < 0 || sourceOffset + count > ElementCount)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count > destination.Length)
            throw new ArgumentException("Destination array too small", nameof(destination));
        
        Array.Copy(_data, sourceOffset, destination, 0, count);
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteAsync(T[] source, int offset, int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (offset < 0 || offset >= ElementCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > ElementCount)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count > source.Length)
            throw new ArgumentException("Source array too small", nameof(source));
        
        Array.Copy(source, 0, _data, offset, count);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(IMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (destination.ElementCount < ElementCount)
            throw new ArgumentException("Destination buffer too small", nameof(destination));
        
        return destination.WriteAsync(_data, 0, ElementCount, cancellationToken);
    }

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        for (int i = 0; i < _data.Length; i++)
        {
            _data[i] = value;
        }
        
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockMemoryBuffer<T>));
    }

    // For testing purposes - direct access to data
    public T[] GetData() => _data;
    public void SetData(T[] newData)
    {
        if (newData.Length != _data.Length)
            throw new ArgumentException("Data length mismatch");
        Array.Copy(newData, _data, _data.Length);
    }
}