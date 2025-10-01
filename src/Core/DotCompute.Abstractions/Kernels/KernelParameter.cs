// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents kernel parameter information with direction.
/// </summary>
public class KernelParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelParameter"/> class with direction.
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
        Index = -1; // Default value when not specified
        IsInput = direction == ParameterDirection.In || direction == ParameterDirection.InOut;
        IsOutput = direction == ParameterDirection.Out || direction == ParameterDirection.InOut;
        IsConstant = false;
        MemorySpace = MemorySpace.Global;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelParameter"/> class with full details.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="index">The parameter index.</param>
    /// <param name="isInput">Whether this is an input parameter.</param>
    /// <param name="isOutput">Whether this is an output parameter.</param>
    /// <param name="isConstant">Whether this is a constant parameter.</param>
    public KernelParameter(string name, Type type, int index, bool isInput = true, bool isOutput = false, bool isConstant = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Index = index;
        IsInput = isInput;
        IsOutput = isOutput;
        IsConstant = isConstant;

        // Determine direction from input/output flags

        if (isInput && isOutput)
        {
            Direction = ParameterDirection.InOut;
        }

        else if (isOutput)
        {
            Direction = ParameterDirection.Out;
        }
        else
        {
            Direction = ParameterDirection.In;
        }


        MemorySpace = MemorySpace.Global;
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
    /// Gets whether this is an input parameter.
    /// </summary>
    public bool IsInput { get; }

    /// <summary>
    /// Gets whether this is an output parameter.
    /// </summary>
    public bool IsOutput { get; }

    /// <summary>
    /// Gets the parameter index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets whether this is a constant parameter that should never be null.
    /// </summary>
    public bool IsConstant { get; }

    /// <summary>
    /// Gets whether this parameter is read-only.
    /// </summary>
    public bool IsReadOnly => IsInput && !IsOutput;

    /// <summary>
    /// Gets or inits the memory space for this parameter.
    /// </summary>
    public MemorySpace MemorySpace { get; init; }

    /// <summary>
    /// Gets or sets the minimum value for numeric parameter validation.
    /// </summary>
    public object? MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value for numeric parameter validation.
    /// </summary>
    public object? MaxValue { get; set; }
}