using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Shared.TestInfrastructure;

/// <summary>
/// Simulates hardware accelerators for testing purposes
/// </summary>
[ExcludeFromCodeCoverage]
public class HardwareSimulator : IDisposable
{
    private readonly ILogger<HardwareSimulator> _logger;
    private readonly Dictionary<AcceleratorType, List<SimulatedAccelerator>> _accelerators = new();
    private bool _disposed;

    public HardwareSimulator(ILogger<HardwareSimulator>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareSimulator>.Instance;
    }

    /// <summary>
    /// Add a simulated accelerator
    /// </summary>
    public SimulatedAccelerator AddAccelerator(AcceleratorType type, string name, long memorySize = 1024 * 1024 * 1024)
    {
        if (!_accelerators.ContainsKey(type))
        {
            _accelerators[type] = new List<SimulatedAccelerator>();
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
    public IEnumerable<SimulatedAccelerator> GetAllAccelerators()
    {
        return _accelerators.Values.SelectMany(list => list);
    }

    /// <summary>
    /// Create a typical GPU setup
    /// </summary>
    public void CreateStandardGpuSetup()
    {
        AddAccelerator(AcceleratorType.CUDA, "NVIDIA RTX 4090", 24L * 1024 * 1024 * 1024);
        AddAccelerator(AcceleratorType.CUDA, "NVIDIA GTX 1080", 8L * 1024 * 1024 * 1024);
        AddAccelerator(AcceleratorType.OpenCL, "Intel UHD Graphics", 2L * 1024 * 1024 * 1024);
    }

    /// <summary>
    /// Create a CPU-only setup
    /// </summary>
    public void CreateCpuOnlySetup()
    {
        AddAccelerator(AcceleratorType.CPU, "Intel Core i9-13900K", 32L * 1024 * 1024 * 1024);
    }

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
    }
}

/// <summary>
/// Simulated accelerator for testing
/// </summary>
[ExcludeFromCodeCoverage]
public class SimulatedAccelerator : IAccelerator
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, byte[]> _memory = new();
    private bool _disposed;
    private string? _failureMessage;

    public AcceleratorType Type { get; }
    public string Name { get; }
    public long TotalMemory { get; }
    public long AvailableMemory => TotalMemory - _memory.Values.Sum(m => m.Length);
    public bool IsAvailable => _failureMessage == null && !_disposed;
    public AcceleratorContext Context { get; }

    public SimulatedAccelerator(AcceleratorType type, string name, long totalMemory, ILogger logger)
    {
        Type = type;
        Name = name;
        TotalMemory = totalMemory;
        _logger = logger;
        Context = new SimulatedAcceleratorContext(this);
    }

    /// <summary>
    /// Simulate a failure condition
    /// </summary>
    public void SimulateFailure(string errorMessage)
    {
        _failureMessage = errorMessage;
        _logger.LogWarning("Simulated failure on {Name}: {Error}", Name, errorMessage);
    }

    /// <summary>
    /// Reset failure simulation
    /// </summary>
    public void ResetFailure()
    {
        _failureMessage = null;
        _logger.LogInformation("Reset failure simulation on {Name}", Name);
    }

    /// <summary>
    /// Allocate simulated memory
    /// </summary>
    public string AllocateMemory(int size)
    {
        ThrowIfFailed();
        
        if (size > AvailableMemory)
        {
            throw new OutOfMemoryException($"Not enough memory on {Name}. Requested: {size}, Available: {AvailableMemory}");
        }

        var handle = Guid.NewGuid().ToString();
        _memory[handle] = new byte[size];
        
        _logger.LogDebug("Allocated {Size} bytes on {Name}, handle: {Handle}", size, Name, handle);
        return handle;
    }

    /// <summary>
    /// Free simulated memory
    /// </summary>
    public void FreeMemory(string handle)
    {
        if (_memory.Remove(handle))
        {
            _logger.LogDebug("Freed memory on {Name}, handle: {Handle}", Name, handle);
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
        
        _logger.LogDebug("Executed kernel on {Name} in {Time}ms", Name, executionTime.TotalMilliseconds);
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
        _logger.LogDebug("Copied {Size} bytes to {Name} in {Time}ms", source.Length, Name, copyTime.TotalMilliseconds);
    }

    private void ThrowIfFailed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SimulatedAccelerator));
        
        if (_failureMessage != null)
            throw new InvalidOperationException(_failureMessage);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _memory.Clear();
        _disposed = true;
        _logger.LogDebug("Disposed simulated accelerator {Name}", Name);
    }
}

/// <summary>
/// Simulated accelerator context
/// </summary>
[ExcludeFromCodeCoverage]
public class SimulatedAcceleratorContext : AcceleratorContext
{
    private readonly SimulatedAccelerator _accelerator;

    public SimulatedAcceleratorContext(SimulatedAccelerator accelerator)
    {
        _accelerator = accelerator;
    }

    public override void Synchronize()
    {
        // Simulate synchronization
        Thread.Sleep(1);
    }

    public override Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Delay(1, cancellationToken);
    }

    protected override void DisposeCore()
    {
        // Nothing to dispose for simulation
    }
}