// <copyright file="MemoryPressureLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Types;

/// <summary>
/// Defines memory pressure levels.
/// Indicates the severity of memory constraints on the system.
/// </summary>
public enum MemoryPressureLevel
{
    /// <summary>
    /// Low memory pressure.
    /// Plenty of memory available for operations.
    /// </summary>
    Low,

    /// <summary>
    /// Medium memory pressure.
    /// Memory usage is moderate but manageable.
    /// </summary>
    Medium,

    /// <summary>
    /// High memory pressure.
    /// Memory is constrained and recovery actions may be needed.
    /// </summary>
    High,

    /// <summary>
    /// Critical memory pressure.
    /// System is at risk of running out of memory.
    /// </summary>
    Critical
}