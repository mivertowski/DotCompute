// <copyright file="BufferState.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Memory.Enums;

/// <summary>
/// Represents the current state of a memory buffer.
/// </summary>
public enum BufferState
{
    /// <summary>
    /// Buffer is allocated and ready for use.
    /// </summary>
    Allocated,

    /// <summary>
    /// Buffer is currently being accessed by the host.
    /// </summary>
    HostAccess,

    /// <summary>
    /// Buffer is currently being accessed by the device.
    /// </summary>
    DeviceAccess,

    /// <summary>
    /// Buffer is being transferred between host and device.
    /// </summary>
    Transferring,

    /// <summary>
    /// Buffer has been disposed.
    /// </summary>
    Disposed
}