using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Tests.Utilities;

/// <summary>
/// Mock accelerator implementation for testing without hardware dependencies.
/// Provides comprehensive IAccelerator implementation with configurable behavior for testing scenarios.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed partial class MockAccelerator : IAccelerator
{
    private bool _isDisposed;
    private readonly Dictionary<string, object> _properties = [];
    private readonly AcceleratorInfo _info;
    private readonly IMemoryManager _memoryManager;
    private readonly ILogger _logger;
    private string? _failureMessage;

    public MockAccelerator(
        string name = "MockGPU",
        AcceleratorType type = AcceleratorType.GPU,
        long totalMemory = 8L * 1024 * 1024 * 1024, // 8GB default
        ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
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

        LogCreatedMockAccelerator(_logger, DeviceId, name, totalMemory);

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

        _memoryManager = new MockMemoryManager(this, _logger);
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
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await Task.Delay(10, cancellationToken); // Simulate compilation time

        return new MockCompiledKernel(definition.Name ?? "MockKernel", _logger);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        IsAvailable = false;
        if (_memoryManager is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

    public void SetProperty(string key, object value) => _properties[key] = value;

    public void SimulateMemoryUsage(long bytesUsed) => AvailableMemory = Math.Max(0, TotalMemory - bytesUsed);

    /// <summary>
    /// Simulates a device failure for testing error handling scenarios.
    /// </summary>
    /// <param name="errorMessage">The error message to simulate.</param>
    public void SimulateDeviceFailure(string errorMessage = "Simulated device failure")
    {
        IsAvailable = false;
        _failureMessage = errorMessage;
        LogSimulatedDeviceFailure(_logger, errorMessage);
    }

    /// <summary>
    /// Resets any simulated failures.
    /// </summary>
    public void ResetFailure()
    {
        IsAvailable = true;
        _failureMessage = null;
        LogResetDeviceFailureSimulation(_logger);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created MockAccelerator {deviceId} ({deviceName}) with {totalMemory} bytes memory")]
    private static partial void LogCreatedMockAccelerator(ILogger logger, string deviceId, string deviceName, long totalMemory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Simulated device failure: {errorMessage}")]
    private static partial void LogSimulatedDeviceFailure(ILogger logger, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reset device failure simulation")]
    private static partial void LogResetDeviceFailureSimulation(ILogger logger);
}

/// <summary>
/// Mock compiled kernel implementation for testing.
/// </summary>
public sealed class MockCompiledKernel : ICompiledKernel
{
    private readonly ILogger _logger;
    private bool _isDisposed;

    public MockCompiledKernel(string name, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        _logger = logger ?? NullLogger.Instance;
    }

    public string Name { get; }

    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // Simulate kernel execution
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock memory manager implementation for testing.
/// </summary>
public sealed class MockMemoryManager : IMemoryManager, IDisposable
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger _logger;
    private bool _isDisposed;

    public MockMemoryManager(IAccelerator accelerator, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _accelerator = accelerator;
        _logger = logger ?? NullLogger.Instance;
    }

    public async ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await Task.Yield(); // Simulate async operation
        return new MockMemoryBuffer(sizeInBytes, options);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        int sizeInBytes = source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        IMemoryBuffer buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
        await buffer.CopyFromHostAsync(source, 0, cancellationToken);
        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || offset >= buffer.SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0 || offset + length > buffer.SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(length));

        // For mock, we create a new buffer that represents the view
        return new MockMemoryBufferView((MockMemoryBuffer)buffer, offset, length);
    }

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        int sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (buffer is MockMemoryBuffer mockBuffer)
        {
            // Simulate copying data to the mock buffer
            ReadOnlySpan<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
            bytes.CopyTo(mockBuffer.GetData().AsSpan());
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        if (buffer is MockMemoryBuffer mockBuffer)
        {
            // Simulate copying data from the mock buffer
            Span<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
            mockBuffer.GetData().AsSpan()[..bytes.Length].CopyTo(bytes);
        }
    }

    public void Free(IMemoryBuffer? buffer) => buffer?.Dispose();

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock memory buffer implementation for testing.
/// </summary>
public sealed class MockMemoryBuffer : IMemoryBuffer, IDisposable
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

        int sizeInBytes = source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
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

        int sizeInBytes = destination.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (offset < 0 || offset + sizeInBytes > SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset));

        // Copy data - for simplicity in mock, we just simulate the operation
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    // For testing purposes - direct access to data
    public byte[] GetData() => _data;
}

/// <summary>
/// Mock memory buffer view implementation for testing.
/// </summary>
public sealed class MockMemoryBufferView : IMemoryBuffer, IDisposable
{
    private readonly MockMemoryBuffer _parentBuffer;
    private readonly long _offset;
    private readonly long _length;
    private bool _isDisposed;

    public MockMemoryBufferView(MockMemoryBuffer parentBuffer, long offset, long length)
    {
        ArgumentNullException.ThrowIfNull(parentBuffer);
        _parentBuffer = parentBuffer;
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public MemoryOptions Options => _parentBuffer.Options;
    public bool IsDisposed => _isDisposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return _parentBuffer.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return _parentBuffer.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return ValueTask.CompletedTask;
        _isDisposed = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}