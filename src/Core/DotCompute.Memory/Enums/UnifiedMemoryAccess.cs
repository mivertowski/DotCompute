// <copyright file="UnifiedMemoryAccess.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;

namespace DotCompute.Memory.Enums;

/// <summary>
/// Specifies memory access patterns for unified memory.
/// </summary>
[Flags]
public enum UnifiedMemoryAccess
{
    /// <summary>
    /// No specific access pattern.
    /// </summary>
    None = 0,

    /// <summary>
    /// Memory will be accessed by the host.
    /// </summary>
    HostAccess = 1,

    /// <summary>
    /// Memory will be accessed by the device.
    /// </summary>
    DeviceAccess = 2,

    /// <summary>
    /// Memory will be accessed by both host and device.
    /// </summary>
    BothAccess = HostAccess | DeviceAccess,

    /// <summary>
    /// Memory will be frequently accessed.
    /// </summary>
    FrequentAccess = 4,

    /// <summary>
    /// Memory will be rarely accessed.
    /// </summary>
    RareAccess = 8,

    /// <summary>
    /// Memory will be accessed in a streaming pattern.
    /// </summary>
    StreamingAccess = 16,

    /// <summary>
    /// Memory will be accessed randomly.
    /// </summary>
    RandomAccess = 32
}