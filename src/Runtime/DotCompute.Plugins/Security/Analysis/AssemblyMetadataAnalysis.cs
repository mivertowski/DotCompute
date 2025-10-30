// <copyright file="AssemblyMetadataAnalysis.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Security.Analysis;

/// <summary>
/// Contains the results of assembly metadata security analysis.
/// Provides detailed information about potential security risks in plugin assemblies.
/// </summary>
public class AssemblyMetadataAnalysis
{
    /// <summary>
    /// Gets or sets the path to the analyzed assembly.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the assembly has a strong name.
    /// Strong-named assemblies provide better security guarantees.
    /// </summary>
    public bool HasStrongName { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly uses unsafe code.
    /// Unsafe code can bypass type safety and memory protection.
    /// </summary>
    public bool UsesUnsafeCode { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly uses reflection.
    /// Reflection can be used to bypass access restrictions.
    /// </summary>
    public bool UsesReflection { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly performs P/Invoke calls.
    /// P/Invoke allows calling native unmanaged code.
    /// </summary>
    public bool UsesPInvoke { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly performs file I/O operations.
    /// File access may pose security risks if not properly controlled.
    /// </summary>
    public bool UsesFileIO { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly performs network operations.
    /// Network access can be used for data exfiltration or remote control.
    /// </summary>
    public bool UsesNetwork { get; set; }

    /// <summary>
    /// Gets or sets the calculated risk level based on analysis.
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;

    /// <summary>
    /// Gets or sets whether an error occurred during analysis.
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    /// Gets or sets the error message if an error occurred.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
