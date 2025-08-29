// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Represents the current state of a memory buffer.
/// </summary>
public enum BufferState
{
    /// <summary>
    /// Buffer is uninitialized.
    /// </summary>
    Uninitialized,
    
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
    Disposed,
    
    /// <summary>
    /// Buffer is accessible from both host and device.
    /// </summary>
    Synchronized,
    
    /// <summary>
    /// Buffer is only accessible from host.
    /// </summary>
    HostOnly,
    
    /// <summary>
    /// Buffer is only accessible from device.
    /// </summary>
    DeviceOnly,
    
    /// <summary>
    /// Host has more recent data than device.
    /// </summary>
    HostDirty,
    
    /// <summary>
    /// Device has more recent data than host.
    /// </summary>
    DeviceDirty,
    
    /// <summary>
    /// Buffer data is ready on the host.
    /// </summary>
    HostReady,
    
    /// <summary>
    /// Buffer data is ready on the device.
    /// </summary>
    DeviceReady,
    
    /// <summary>
    /// Buffer has been released and its memory is available for reuse.
    /// </summary>
    Released,
    
    /// <summary>
    /// Buffer data is valid and available on the device.
    /// </summary>
    DeviceValid
}