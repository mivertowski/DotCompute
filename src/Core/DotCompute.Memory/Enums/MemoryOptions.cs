// <copyright file="MemoryOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;

namespace DotCompute.Memory.Enums;

/// <summary>
/// Specifies memory allocation and management options.
/// </summary>
[Flags]
public enum MemoryOptions
{
    /// <summary>
    /// No specific options.
    /// </summary>
    None = 0,

    /// <summary>
    /// Pin memory to prevent garbage collection movement.
    /// </summary>
    Pinned = 1,

    /// <summary>
    /// Align memory for optimal performance.
    /// </summary>
    Aligned = 2,

    /// <summary>
    /// Use write-combined memory for better streaming performance.
    /// </summary>
    WriteCombined = 4,

    /// <summary>
    /// Pre-allocate memory to avoid allocation overhead.
    /// </summary>
    PreAllocated = 8,

    /// <summary>
    /// Use persistent memory that survives context switches.
    /// </summary>
    Persistent = 16,

    /// <summary>
    /// Enable memory compression if supported.
    /// </summary>
    Compressed = 32,

    /// <summary>
    /// Use high-bandwidth memory if available.
    /// </summary>
    HighBandwidth = 64,

    /// <summary>
    /// Enable automatic memory migration between host and device.
    /// </summary>
    AutoMigrate = 128,

    /// <summary>
    /// Memory is read-only.
    /// </summary>
    ReadOnly = 256,

    /// <summary>
    /// Memory is write-only.
    /// </summary>
    WriteOnly = 512,

    /// <summary>
    /// Memory should be allocated in host-visible memory if possible.
    /// </summary>
    HostVisible = 1024,

    /// <summary>
    /// Memory should be cached if possible.
    /// </summary>
    Cached = 2048,

    /// <summary>
    /// Memory will be used for atomic operations.
    /// </summary>
    Atomic = 4096,

    /// <summary>
    /// Use lazy synchronization between host and device.
    /// </summary>
    LazySync = 8192,

    /// <summary>
    /// Prefer pooled allocation for better performance.
    /// </summary>
    PreferPooled = 16384,

    /// <summary>
    /// Clear memory securely when disposed.
    /// </summary>
    SecureClear = 32768
}