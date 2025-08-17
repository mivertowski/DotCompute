// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Base class for mock hardware device implementations.
/// Provides common functionality for simulating hardware devices in tests.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class MockHardwareDevice : IHardwareDevice, IDisposable
{
    private bool _disposed;
    private string? _failureMessage;
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected ILogger? Logger { get; }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public AcceleratorType Type { get; }

    /// <inheritdoc/>
    public string Vendor { get; }

    /// <inheritdoc/>
    public string DriverVersion { get; }

    /// <inheritdoc/>
    public long TotalMemory { get; protected set; }

    /// <inheritdoc/>
    public long AvailableMemory { get; protected set; }

    /// <inheritdoc/>
    public int MaxThreadsPerBlock { get; protected set; }

    /// <inheritdoc/>
    public int ComputeUnits { get; }

    /// <summary>
    /// Gets the maximum clock frequency in MHz.
    /// </summary>
    public int MaxClockFrequency { get; protected set; }

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            lock (_lock)
            {
                return !_disposed && _failureMessage == null;
            }
        }
    }

    /// <inheritdoc/>
    public Dictionary<string, object> Capabilities { get; }

    /// <summary>
    /// Gets whether the device has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets the current failure message, if any.
    /// </summary>
    public string? FailureMessage
    {
        get
        {
            lock (_lock)
            {
                return _failureMessage;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the MockHardwareDevice class.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="name">The device name.</param>
    /// <param name="type">The accelerator type.</param>
    /// <param name="vendor">The device vendor.</param>
    /// <param name="driverVersion">The driver version.</param>
    /// <param name="totalMemory">The total memory in bytes.</param>
    /// <param name="maxThreadsPerBlock">The maximum threads per block.</param>
    /// <param name="computeUnits">The number of compute units.</param>
    /// <param name="logger">Optional logger instance.</param>
    protected MockHardwareDevice(
        string id,
        string name,
        AcceleratorType type,
        string vendor,
        string driverVersion,
        long totalMemory,
        int maxThreadsPerBlock,
        int computeUnits,
        ILogger? logger = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Vendor = vendor ?? throw new ArgumentNullException(nameof(vendor));
        DriverVersion = driverVersion ?? throw new ArgumentNullException(nameof(driverVersion));
        TotalMemory = totalMemory;
        AvailableMemory = totalMemory;
        MaxThreadsPerBlock = maxThreadsPerBlock;
        ComputeUnits = computeUnits;
        MaxClockFrequency = 1500; // Default 1.5 GHz
        Logger = logger ?? NullLogger.Instance;

        Capabilities = new Dictionary<string, object>
        {
            ["Type"] = type.ToString(),
            ["Vendor"] = vendor,
            ["DriverVersion"] = driverVersion,
            ["TotalMemory"] = totalMemory,
            ["MaxThreadsPerBlock"] = maxThreadsPerBlock,
            ["ComputeUnits"] = computeUnits,
            ["IsMockDevice"] = true,
            ["CreationTime"] = DateTime.UtcNow
        };

        Logger?.LogDebug("Created mock {Type} device: {Name} ({Id})", type, name, id);
    }

    /// <inheritdoc/>
    public virtual AcceleratorInfo ToAcceleratorInfo()
    {
        ThrowIfDisposed();

        return new AcceleratorInfo
        {
            Id = Id,
            Name = Name,
            DeviceType = Type.ToString(),
            Vendor = Vendor,
            DriverVersion = DriverVersion,
            TotalMemory = TotalMemory,
            AvailableMemory = AvailableMemory,
            MaxSharedMemoryPerBlock = Math.Min(TotalMemory / 4, 48 * 1024), // Reasonable default
            MaxMemoryAllocationSize = TotalMemory,
            LocalMemorySize = TotalMemory / 8,
            IsUnifiedMemory = Type == AcceleratorType.CPU || Type == AcceleratorType.Metal,
            ComputeUnits = ComputeUnits,
            MaxClockFrequency = MaxClockFrequency,
            MaxThreadsPerBlock = MaxThreadsPerBlock,
            ComputeCapability = new Version(8, 0), // Default modern capability
            Capabilities = new Dictionary<string, object>(Capabilities)
        };
    }

    /// <inheritdoc/>
    public virtual Dictionary<string, object> GetProperties()
    {
        ThrowIfDisposed();

        var properties = new Dictionary<string, object>(Capabilities)
        {
            ["Id"] = Id,
            ["Name"] = Name,
            ["Type"] = Type.ToString(),
            ["Vendor"] = Vendor,
            ["DriverVersion"] = DriverVersion,
            ["TotalMemory"] = TotalMemory,
            ["AvailableMemory"] = AvailableMemory,
            ["MaxThreadsPerBlock"] = MaxThreadsPerBlock,
            ["ComputeUnits"] = ComputeUnits,
            ["MaxClockFrequency"] = MaxClockFrequency,
            ["IsAvailable"] = IsAvailable,
            ["FailureMessage"] = FailureMessage ?? "None",
            ["LastHealthCheck"] = DateTime.UtcNow
        };

        return properties;
    }

    /// <inheritdoc/>
    public virtual bool HealthCheck()
    {
        if (_disposed)
        {
            Logger?.LogWarning("Health check failed: Device {DeviceId} is disposed", Id);
            return false;
        }

        lock (_lock)
        {
            if (_failureMessage != null)
            {
                Logger?.LogWarning("Health check failed: Device {DeviceId} has failure: {Error}",
                    Id, _failureMessage);
                return false;
            }
        }

        // Basic sanity checks
        if (TotalMemory <= 0 || AvailableMemory < 0 || AvailableMemory > TotalMemory)
        {
            Logger?.LogWarning("Health check failed: Device {DeviceId} has invalid memory configuration", Id);
            return false;
        }

        if (ComputeUnits <= 0 || MaxThreadsPerBlock <= 0)
        {
            Logger?.LogWarning("Health check failed: Device {DeviceId} has invalid compute configuration", Id);
            return false;
        }

        Logger?.LogDebug("Health check passed for device {DeviceId}", Id);
        return true;
    }

    /// <summary>
    /// Simulates a device failure with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message to associate with the failure.</param>
    public virtual void SimulateFailure(string errorMessage)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(errorMessage);

        lock (_lock)
        {
            _failureMessage = errorMessage;
        }

        Logger?.LogWarning("Simulated failure on device {DeviceId}: {Error}", Id, errorMessage);
    }

    /// <summary>
    /// Resets any simulated failures, making the device available again.
    /// </summary>
    public virtual void ResetFailure()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _failureMessage = null;
        }

        Logger?.LogInformation("Reset failure simulation for device {DeviceId}", Id);
    }

    /// <summary>
    /// Simulates memory usage changes.
    /// </summary>
    /// <param name="memoryUsed">The amount of memory to mark as used (can be negative to free memory).</param>
    public virtual void SimulateMemoryUsage(long memoryUsed)
    {
        ThrowIfDisposed();

        var newAvailableMemory = Math.Max(0, Math.Min(TotalMemory, AvailableMemory - memoryUsed));
        var previousMemory = AvailableMemory;
        AvailableMemory = newAvailableMemory;

        Logger?.LogDebug("Simulated memory usage change on device {DeviceId}: {Previous} -> {Current} bytes",
            Id, previousMemory, AvailableMemory);
    }

    /// <summary>
    /// Simulates clock frequency changes (e.g., thermal throttling).
    /// </summary>
    /// <param name="newFrequencyMHz">The new clock frequency in MHz.</param>
    public virtual void SimulateClockChange(int newFrequencyMHz)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newFrequencyMHz);

        var previousFrequency = MaxClockFrequency;
        MaxClockFrequency = newFrequencyMHz;

        Logger?.LogDebug("Simulated clock frequency change on device {DeviceId}: {Previous} -> {Current} MHz",
            Id, previousFrequency, MaxClockFrequency);
    }

    /// <summary>
    /// Gets diagnostic information about the device.
    /// </summary>
    /// <returns>A dictionary containing diagnostic information.</returns>
    public virtual Dictionary<string, object> GetDiagnostics()
    {
        var diagnostics = new Dictionary<string, object>
        {
            ["Id"] = Id,
            ["Name"] = Name,
            ["Type"] = Type.ToString(),
            ["IsAvailable"] = IsAvailable,
            ["IsDisposed"] = _disposed,
            ["FailureMessage"] = FailureMessage ?? "None",
            ["MemoryUtilization"] = TotalMemory > 0 ? (TotalMemory - AvailableMemory) / (double)TotalMemory : 0,
            ["MemoryUtilizationBytes"] = TotalMemory - AvailableMemory,
            ["LastHealthCheck"] = HealthCheck(),
            ["DiagnosticsTimestamp"] = DateTime.UtcNow
        };

        return diagnostics;
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the device has been disposed.
    /// </summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>
    /// Disposes the device.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            lock (_lock)
            {
                _disposed = true;
                _failureMessage = null;
            }

            Logger?.LogDebug("Disposed mock device {DeviceId}", Id);
        }

        _disposed = true;
    }
}
