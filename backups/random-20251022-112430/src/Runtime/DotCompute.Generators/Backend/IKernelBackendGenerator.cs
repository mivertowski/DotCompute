// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Backend;

/// <summary>
/// Defines the contract for kernel backend generators.
/// </summary>
public interface IKernelBackendGenerator
{
    /// <summary>
    /// Gets the name of the backend this generator supports.
    /// </summary>
    public string BackendName { get; }

    /// <summary>
    /// Determines if this generator supports the specified backend.
    /// </summary>
    public bool SupportsBackend(string backend);

    /// <summary>
    /// Generates the backend-specific implementation for a kernel method.
    /// </summary>
    public string? GenerateImplementation(KernelMethodInfo method, Compilation compilation);
}