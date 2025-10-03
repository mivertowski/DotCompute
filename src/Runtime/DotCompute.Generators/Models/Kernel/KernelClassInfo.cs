// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;

namespace DotCompute.Generators.Models.Kernel;

/// <summary>
/// Represents metadata about a class containing kernel methods.
/// </summary>
public sealed class KernelClassInfo
{
    private readonly Collection<string> _kernelMethodNames = [];

    /// <summary>
    /// Gets or sets the class name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace containing the class.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of kernel method names in this class.
    /// </summary>
    public Collection<string> KernelMethodNames => _kernelMethodNames;
}