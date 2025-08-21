// <copyright file="RiskLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Security.Types;

/// <summary>
/// Defines risk levels for plugin security analysis.
/// Used to categorize the potential security impact of plugins.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk level.
    /// Plugin uses only safe APIs and has minimal security concerns.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium risk level.
    /// Plugin uses some sensitive APIs but within acceptable bounds.
    /// May require additional monitoring or restrictions.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High risk level.
    /// Plugin uses potentially dangerous APIs or patterns.
    /// Should be carefully reviewed and may require sandboxing.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical risk level.
    /// Plugin poses significant security threats.
    /// Should be blocked or require explicit authorization to run.
    /// </summary>
    Critical = 3
}