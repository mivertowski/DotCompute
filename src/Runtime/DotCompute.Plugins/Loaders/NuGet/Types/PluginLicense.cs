// <copyright file="PluginLicense.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// License information for a plugin.
/// Contains licensing terms and requirements for plugin usage.
/// </summary>
public class PluginLicense
{
    /// <summary>
    /// Gets or sets the license type.
    /// Standard license identifier (e.g., "MIT", "Apache-2.0").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the license URL.
    /// Link to the full license text.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the license expression.
    /// SPDX license expression for complex licensing.
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// Gets or sets whether the license requires acceptance.
    /// Indicates if users must explicitly accept the license terms.
    /// </summary>
    public bool RequiresAcceptance { get; set; }
}
