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
    /// Gets the parameter name.
    public string Name { get; }
    /// Gets the parameter type.
    public Type Type { get; }
    /// Gets the parameter direction.
    public ParameterDirection Direction { get; }
    /// Gets or sets the parameter size in bytes.
    public long Size { get; set; }
    /// Gets or sets additional parameter metadata.
    public Dictionary<string, object> Metadata { get; set; } = [];
}
