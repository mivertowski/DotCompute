// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Platform-specific optimizations
/// </summary>
public enum MetalPlatformOptimization
{
    /// <summary>
    /// Generic Metal optimizations
    /// </summary>
    Generic,

    /// <summary>
    /// macOS-specific optimizations
    /// </summary>
    MacOS,

    /// <summary>
    /// iOS-specific optimizations
    /// </summary>
    iOS,

    /// <summary>
    /// iPadOS-specific optimizations
    /// </summary>
    iPadOS,

    /// <summary>
    /// tvOS-specific optimizations
    /// </summary>
    tvOS,

    /// <summary>
    /// watchOS-specific optimizations
    /// </summary>
    watchOS
}