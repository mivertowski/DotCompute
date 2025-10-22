// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Operations;

/// <summary>
/// Descriptor for memory operations
/// </summary>
public sealed class MetalMemoryOperationDescriptor : MetalOperationDescriptor
{
    /// <summary>
    /// Type of memory operation
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Copy from host to device
        /// </summary>
        HostToDevice,

        /// <summary>
        /// Copy from device to host
        /// </summary>
        DeviceToHost,

        /// <summary>
        /// Copy from device to device
        /// </summary>
        DeviceToDevice,

        /// <summary>
        /// Fill buffer with pattern
        /// </summary>
        Fill,

        /// <summary>
        /// Clear buffer
        /// </summary>
        Clear
    }

    /// <summary>
    /// Type of memory operation
    /// </summary>
    public OperationType Operation { get; set; }

    /// <summary>
    /// Source buffer or data
    /// </summary>
    public IntPtr Source { get; set; }

    /// <summary>
    /// Destination buffer
    /// </summary>
    public IntPtr Destination { get; set; }

    /// <summary>
    /// Number of bytes to copy
    /// </summary>
    public long BytesToCopy { get; set; }

    /// <summary>
    /// Source offset
    /// </summary>
    public long SourceOffset { get; set; }

    /// <summary>
    /// Destination offset
    /// </summary>
    public long DestinationOffset { get; set; }
}