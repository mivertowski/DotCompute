// <copyright file="ServiceLifetime.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Services;

/// <summary>
/// Represents the lifetime of a service in the dependency injection container.
/// Determines how and when service instances are created and disposed.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// A new instance is created every time the service is requested.
    /// Each consumer gets a unique instance.
    /// </summary>
    Transient,

    /// <summary>
    /// A single instance is created per scope (e.g., per request in web applications).
    /// All consumers within the same scope share the same instance.
    /// </summary>
    Scoped,

    /// <summary>
    /// A single instance is created and shared for the entire application lifetime.
    /// All consumers share the same instance.
    /// </summary>
    Singleton
}