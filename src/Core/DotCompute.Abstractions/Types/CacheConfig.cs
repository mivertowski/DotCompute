// <copyright file="CacheConfig.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines cache configuration preferences for CUDA kernels.
/// Controls the allocation of on-chip memory between L1 cache and shared memory.
/// </summary>
public enum CacheConfig
{
    /// <summary>
    /// No preference for cache configuration.
    /// Uses the default or previously set configuration.
    /// </summary>
    PreferNone,

    /// <summary>
    /// Prefer shared memory over L1 cache.
    /// Allocates more on-chip memory to shared memory.
    /// Best for kernels with heavy shared memory usage.
    /// </summary>
    PreferShared,

    /// <summary>
    /// Prefer L1 cache over shared memory.
    /// Allocates more on-chip memory to L1 cache.
    /// Best for kernels with good data locality but minimal shared memory needs.
    /// </summary>
    PreferL1,

    /// <summary>
    /// Equal preference for L1 cache and shared memory.
    /// Balances on-chip memory allocation between both.
    /// Good default for kernels with moderate usage of both resources.
    /// </summary>
    PreferEqual
}
