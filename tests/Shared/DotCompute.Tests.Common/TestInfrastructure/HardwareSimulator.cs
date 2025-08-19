using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Utilities.TestInfrastructure;


/// <summary>
/// Simulates hardware accelerators for testing purposes
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class HardwareSimulator(ILogger<HardwareSimulator>? logger = null) : IDisposable
{
    private readonly ILogger<HardwareSimulator> _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareSimulator>.Instance;
    private readonly Dictionary<AcceleratorType, List<SimulatedAccelerator>> _accelerators = [];
    private bool _disposed;

    /// <summary>
    /// Add a simulated accelerator
    /// </summary>
    public SimulatedAccelerator AddAccelerator(AcceleratorType type, string name, long memorySize = 1024 * 1024 * 1024)
    {
        if (!_accelerators.ContainsKey(type))
        {
            _accelerators[type] = [];
        }

        var accelerator = new SimulatedAccelerator(type, name, memorySize, _logger);
        _accelerators[type].Add(accelerator);

        _logger.LogInformation("Added simulated {Type} accelerator: {Name} with {Memory} bytes memory",
            type, name, memorySize);

        return accelerator;
    }

    /// <summary>
    /// Get all accelerators of a specific type
    /// </summary>
    public IEnumerable<SimulatedAccelerator> GetAccelerators(AcceleratorType type)
    {
        return _accelerators.TryGetValue(type, out var accelerators)
            ? accelerators
            : Enumerable.Empty<SimulatedAccelerator>();
    }

    /// <summary>
    /// Get all accelerators
    /// </summary>
    public IEnumerable<SimulatedAccelerator> GetAllAccelerators() => _accelerators.Values.SelectMany(list => list);

    /// <summary>
    /// Create a typical GPU setup
    /// </summary>
    public void CreateStandardGpuSetup()
    {
        _ = AddAccelerator(AcceleratorType.CUDA, "NVIDIA RTX 4090", 24L * 1024 * 1024 * 1024);
        _ = AddAccelerator(AcceleratorType.CUDA, "NVIDIA GTX 1080", 8L * 1024 * 1024 * 1024);
        _ = AddAccelerator(AcceleratorType.OpenCL, "Intel UHD Graphics", 2L * 1024 * 1024 * 1024);
    }

    /// <summary>
    /// Create a CPU-only setup
    /// </summary>
    public void CreateCpuOnlySetup() => AddAccelerator(AcceleratorType.CPU, "Intel Core i9-13900K", 32L * 1024 * 1024 * 1024);

    /// <summary>
    /// Simulate hardware failures
    /// </summary>
    public void SimulateFailure(AcceleratorType type, string errorMessage = "Simulated hardware failure")
    {
        foreach (var accelerator in GetAccelerators(type))
        {
            accelerator.SimulateFailure(errorMessage);
        }
    }

    /// <summary>
    /// Reset all failure simulations
    /// </summary>
    public void ResetFailures()
    {
        foreach (var accelerator in GetAllAccelerators())
        {
            accelerator.ResetFailure();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var accelerator in GetAllAccelerators())
        {
            accelerator.Dispose();
        }

        _accelerators.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Simulated accelerator for testing
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SimulatedAccelerator : IAccelerator
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, byte[]> _memory = [];
    private bool _disposed;
    private string? _failureMessage;
    private readonly IMemoryManager _memoryManager;

    public AcceleratorType Type { get; }
    public AcceleratorInfo Info { get; }
    public IMemoryManager Memory => _memoryManager;
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);
    public long AvailableMemory => Info.TotalMemory - _memory.Values.Sum(m => m.Length);
    public bool IsAvailable => _failureMessage == null && !_disposed;

    public SimulatedAccelerator(AcceleratorType type, string name, long totalMemory, ILogger logger)
    {
        Type = type;
        _logger = logger;
        _memoryManager = new TestMemoryManager();

        Info = new AcceleratorInfo
        {
            Id = $"sim_{type}_{Guid.NewGuid():N}",
            Name = name,
            DeviceType = type.ToString(),
            Vendor = "Test Vendor",
            DriverVersion = "1.0.0",
            TotalMemory = totalMemory,
            AvailableMemory = totalMemory,
            MaxSharedMemoryPerBlock = 48 * 1024,
            MaxMemoryAllocationSize = totalMemory,
            LocalMemorySize = 64 * 1024,
            IsUnifiedMemory = type == AcceleratorType.CPU,
            ComputeUnits = 8,
            MaxClockFrequency = 1500,
            MaxThreadsPerBlock = 1024
        };
    }

    /// <summary>
    /// Simulate a failure condition
    /// </summary>
    public void SimulateFailure(string errorMessage)
    {
        _failureMessage = errorMessage;
        _logger.LogWarning("Simulated failure on {Name}: {Error}", Info.Name, errorMessage);
    }

    /// <summary>
    /// Reset failure simulation
    /// </summary>
    public void ResetFailure()
    {
        _failureMessage = null;
        _logger.LogInformation("Reset failure simulation on {Name}", Info.Name);
    }

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsAvailable)
            throw new InvalidOperationException(_failureMessage ?? "Accelerator not available");

        var kernel = new TestCompiledKernel(definition.Name, definition.Code, options ?? new CompilationOptions());
        return ValueTask.FromResult<ICompiledKernel>(kernel);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Simulate async synchronization
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Allocate simulated memory
    /// </summary>
    public string AllocateMemory(int size)
    {
        ThrowIfFailed();

        if (size > AvailableMemory)
        {
            throw new OutOfMemoryException($"Not enough memory on {Info.Name}. Requested: {size}, Available: {AvailableMemory}");
        }

        var handle = Guid.NewGuid().ToString();
        _memory[handle] = new byte[size];

        _logger.LogDebug("Allocated {Size} bytes on {Name}, handle: {Handle}", size, Info.Name, handle);
        return handle;
    }

    /// <summary>
    /// Free simulated memory
    /// </summary>
    public void FreeMemory(string handle)
    {
        if (_memory.Remove(handle))
        {
            _logger.LogDebug("Freed memory on {Name}, handle: {Handle}", Info.Name, handle);
        }
    }

    /// <summary>
    /// Simulate kernel execution
    /// </summary>
    public async Task<TimeSpan> ExecuteKernelAsync(string kernelSource, object[] parameters,
        CancellationToken cancellationToken = default)
    {
        ThrowIfFailed();

        var executionTime = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 100));
        await Task.Delay(executionTime, cancellationToken);

        _logger.LogDebug("Executed kernel on {Name} in {Time}ms", Info.Name, executionTime.TotalMilliseconds);
        return executionTime;
    }

    /// <summary>
    /// Simulate memory copy
    /// </summary>
    public async Task CopyMemoryAsync(byte[] source, string destinationHandle,
        CancellationToken cancellationToken = default)
    {
        ThrowIfFailed();

        if (!_memory.TryGetValue(destinationHandle, out var destination))
        {
            throw new ArgumentException($"Invalid memory handle: {destinationHandle}");
        }

        if (source.Length > destination.Length)
        {
            throw new ArgumentException("Source data too large for destination");
        }

        var copyTime = TimeSpan.FromMilliseconds(source.Length / 1000.0); // Simulate bandwidth
        await Task.Delay(copyTime, cancellationToken);

        Array.Copy(source, destination, source.Length);
        _logger.LogDebug("Copied {Size} bytes to {Name} in {Time}ms", source.Length, Info.Name, copyTime.TotalMilliseconds);
    }

    private void ThrowIfFailed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_failureMessage != null)
            throw new InvalidOperationException(_failureMessage);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _memory.Clear();
        _disposed = true;
        _logger.LogDebug("Disposed simulated accelerator {Name}", Info.Name);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simulated accelerator context
/// </summary>
[ExcludeFromCodeCoverage]
#pragma warning disable CS9113 // Parameter is unread
public class SimulatedAcceleratorContext(SimulatedAccelerator accelerator)
#pragma warning restore CS9113
{
    public static void Synchronize()
        // Simulate synchronization
        => Thread.Sleep(1);

    public static Task SynchronizeAsync(CancellationToken cancellationToken = default) => Task.Delay(1, cancellationToken);

    public static void Dispose()
    {
        // Nothing to dispose for simulation
    }
}
