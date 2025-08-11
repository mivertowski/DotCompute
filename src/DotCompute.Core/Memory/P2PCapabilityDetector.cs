// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory;

/// <summary>
/// Detects and manages P2P capabilities between accelerator devices.
/// </summary>
public sealed class P2PCapabilityDetector : IAsyncDisposable
{
    private readonly ILogger _logger;
    private bool _disposed;

    public P2PCapabilityDetector(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects P2P capability between two devices.
    /// </summary>
    public async ValueTask<P2PConnectionCapability> DetectP2PCapabilityAsync(
        IAccelerator device1, 
        IAccelerator device2, 
        CancellationToken cancellationToken = default)
    {
        // Mock implementation - in real implementation this would query hardware capabilities
        await Task.Delay(1, cancellationToken);

        return new P2PConnectionCapability
        {
            IsSupported = device1.Info.Id != device2.Info.Id, // Different devices may support P2P
            ConnectionType = P2PConnectionType.PCIe,
            EstimatedBandwidthGBps = 32.0, // Mock 32 GB/s
            LimitationReason = device1.Info.Id == device2.Info.Id ? "Same device" : null
        };
    }

    /// <summary>
    /// Enables P2P access between two devices.
    /// </summary>
    public async ValueTask<P2PEnableResult> EnableP2PAccessAsync(
        IAccelerator device1, 
        IAccelerator device2, 
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);

        var capability = await DetectP2PCapabilityAsync(device1, device2, cancellationToken);
        
        return new P2PEnableResult
        {
            Success = capability.IsSupported,
            Capability = capability,
            ErrorMessage = capability.IsSupported ? null : capability.LimitationReason
        };
    }

    /// <summary>
    /// Gets optimal transfer strategy for the given parameters.
    /// </summary>
    public async ValueTask<TransferStrategy> GetOptimalTransferStrategyAsync(
        IAccelerator sourceDevice,
        IAccelerator targetDevice,
        long transferSize,
        CancellationToken cancellationToken = default)
    {
        var capability = await DetectP2PCapabilityAsync(sourceDevice, targetDevice, cancellationToken);

        if (capability.IsSupported && transferSize > 1024 * 1024) // > 1MB
        {
            return new TransferStrategy
            {
                Type = TransferType.DirectP2P,
                EstimatedBandwidthGBps = capability.EstimatedBandwidthGBps,
                ChunkSize = 4 * 1024 * 1024 // 4MB chunks
            };
        }
        else if (transferSize > 64 * 1024 * 1024) // > 64MB
        {
            return new TransferStrategy
            {
                Type = TransferType.Streaming,
                EstimatedBandwidthGBps = 8.0, // Host bandwidth
                ChunkSize = 8 * 1024 * 1024 // 8MB chunks
            };
        }
        else
        {
            return new TransferStrategy
            {
                Type = TransferType.HostMediated,
                EstimatedBandwidthGBps = 16.0, // Host bandwidth  
                ChunkSize = 1 * 1024 * 1024 // 1MB chunks
            };
        }
    }

    /// <summary>
    /// Gets device capabilities including P2P support.
    /// </summary>
    public async ValueTask<DeviceCapabilities> GetDeviceCapabilitiesAsync(
        IAccelerator device,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);

        return new DeviceCapabilities
        {
            SupportsP2P = true, // Mock support
            MemoryBandwidthGBps = 900.0, // Mock GPU memory bandwidth
            P2PBandwidthGBps = 32.0, // Mock P2P bandwidth
            MaxMemoryBytes = 24L * 1024 * 1024 * 1024 // 24GB
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await Task.CompletedTask;
    }
}

/// <summary>
/// P2P connection capability information.
/// </summary>
public sealed class P2PConnectionCapability
{
    public required bool IsSupported { get; init; }
    public P2PConnectionType ConnectionType { get; init; }
    public double EstimatedBandwidthGBps { get; init; }
    public string? LimitationReason { get; init; }
}

/// <summary>
/// P2P connection types.
/// </summary>
public enum P2PConnectionType
{
    None = 0,
    PCIe = 1,
    NVLink = 2,
    InfiniBand = 3,
    DirectGMA = 4
}

/// <summary>
/// Result of P2P enable operation.
/// </summary>
public sealed class P2PEnableResult
{
    public required bool Success { get; init; }
    public P2PConnectionCapability? Capability { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Transfer strategy information.
/// </summary>
public sealed class TransferStrategy
{
    public required TransferType Type { get; init; }
    public double EstimatedBandwidthGBps { get; init; }
    public int ChunkSize { get; init; }
}

/// <summary>
/// Transfer types.
/// </summary>
public enum TransferType
{
    HostMediated = 0,
    DirectP2P = 1,
    Streaming = 2,
    MemoryMapped = 3
}

/// <summary>
/// Device capabilities for P2P operations.
/// </summary>
public sealed class DeviceCapabilities
{
    public bool SupportsP2P { get; init; }
    public double MemoryBandwidthGBps { get; init; }
    public double P2PBandwidthGBps { get; init; }
    public long MaxMemoryBytes { get; init; }
}