// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the different types of data transfers that can occur during pipeline execution.
/// Each type represents a different data movement pattern with distinct performance characteristics.
/// </summary>
public enum DataTransferType
{
    /// <summary>
    /// Data transfer from host (CPU) memory to device (GPU/accelerator) memory.
    /// Typically involves copying data over PCIe or similar interconnects.
    /// </summary>
    HostToDevice,

    /// <summary>
    /// Data transfer from device (GPU/accelerator) memory to host (CPU) memory.
    /// Used for retrieving computation results from accelerator devices.
    /// </summary>
    DeviceToHost,

    /// <summary>
    /// Data transfer between different memory regions on the same device.
    /// Usually faster than host-device transfers but still has performance impact.
    /// </summary>
    DeviceToDevice,

    /// <summary>
    /// Data transfer between different devices using peer-to-peer communication.
    /// Enables direct device-to-device communication without involving host memory.
    /// </summary>
    PeerToPeer
}
