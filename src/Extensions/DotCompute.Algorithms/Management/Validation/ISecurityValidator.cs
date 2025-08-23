// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Validation;

/// <summary>
/// Interface for assembly security validation.
/// </summary>
public interface ISecurityValidator
{
    /// <summary>
    /// Validates assembly security using comprehensive security policies and scanning.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to validate.</param>
    /// <returns>True if the assembly passes security validation; otherwise, false.</returns>
    Task<bool> ValidateAssemblySecurityAsync(string assemblyPath);

    /// <summary>
    /// Validates strong name signature of an assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to validate.</param>
    /// <returns>True if the assembly has a valid strong name; otherwise, false.</returns>
    Task<bool> ValidateStrongNameAsync(string assemblyPath);

    /// <summary>
    /// Checks if the required framework version is compatible.
    /// </summary>
    /// <param name="requiredVersion">The required framework version.</param>
    /// <returns>True if the version is compatible; otherwise, false.</returns>
    bool IsVersionCompatible(string? requiredVersion);
}