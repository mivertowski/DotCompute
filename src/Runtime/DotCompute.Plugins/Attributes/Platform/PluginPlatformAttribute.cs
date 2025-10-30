// <copyright file="PluginPlatformAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Platform;

/// <summary>
/// Specifies platform requirements for a plugin.
/// This attribute declares the runtime environment requirements that must be met for the plugin to function correctly.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PluginPlatformAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the supported operating systems.
    /// Array of OS names (e.g., "Windows", "Linux", "macOS").
    /// If null or empty, all operating systems are supported.
    /// </summary>
    public string[]? SupportedOperatingSystems { get; set; }

    /// <summary>
    /// Gets or sets the minimum .NET version required.
    /// Uses version string format (e.g., "6.0", "7.0", "8.0").
    /// </summary>
    public string? MinimumDotNetVersion { get; set; }

    /// <summary>
    /// Gets or sets required CPU features.
    /// Array of CPU feature names (e.g., "AVX2", "SSE4", "NEON").
    /// These features must be available for the plugin to function.
    /// </summary>
    public string[]? RequiredCpuFeatures { get; set; }

    /// <summary>
    /// Gets or sets the minimum memory required in megabytes.
    /// The system must have at least this amount of available memory.
    /// </summary>
    public int MinimumMemoryMB { get; set; }

    /// <summary>
    /// Gets or sets whether 64-bit architecture is required.
    /// When true, the plugin will not load on 32-bit systems.
    /// </summary>
    public bool Requires64Bit { get; set; }
}
