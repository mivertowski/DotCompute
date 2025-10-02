// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Models.Kernel;

/// <summary>
/// Represents metadata about a class containing kernel methods.
/// </summary>
public sealed class KernelClassInfo
{
    /// <summary>
    /// Gets or sets the class name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace containing the class.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of kernel method names in this class.
    /// </summary>
    public List<string> KernelMethodNames { get; } = [];
}