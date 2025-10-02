// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Attributes;

/// <summary>
/// Attribute to mark properties for dependency injection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginInjectAttribute"/> class.
/// </remarks>
/// <param name="required">Whether the dependency is required.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PluginInjectAttribute(bool required = true) : Attribute
{

    /// <summary>
    /// Gets whether the dependency is required.
    /// </summary>
    public bool Required { get; } = required;
}