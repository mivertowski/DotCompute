// <copyright file="KernelParameter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;

namespace DotCompute.Linq.Operators.Parameters;

/// <summary>
/// Represents kernel parameter information with direction.
/// </summary>
public class KernelParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelParameter"/> class.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="direction">The parameter direction.</param>
    /// <exception cref="ArgumentNullException">Thrown when name or type is null.</exception>
    public KernelParameter(string name, Type type, ParameterDirection direction)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Direction = direction;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the parameter direction.
    /// </summary>
    public ParameterDirection Direction { get; }

    /// <summary>
    /// Gets or sets the parameter size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets additional parameter metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}