// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Models.Kernel;

/// <summary>
/// Contains detailed information about a kernel method parameter.
/// </summary>
/// <remarks>
/// This class captures metadata about individual parameters of kernel methods,
/// including type information and memory access characteristics. This information
/// is essential for generating efficient kernel code that properly handles
/// different parameter types and memory access patterns.
/// </remarks>
public sealed class ParameterInfo
{
    /// <summary>
    /// Gets or sets the name of the parameter.
    /// </summary>
    /// <value>
    /// The parameter name as it appears in the method signature.
    /// </value>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified type name of the parameter.
    /// </summary>
    /// <value>
    /// The complete type information including namespace and generic type parameters
    /// if applicable. This is used for accurate type checking and code generation.
    /// </value>
    /// <example>
    /// Examples of type values:
    /// <list type="bullet">
    /// <item><description>"int" - Simple value type</description></item>
    /// <item><description>"float[]" - Array type</description></item>
    /// <item><description>"System.Span&lt;double&gt;" - Generic type</description></item>
    /// <item><description>"DotCompute.Memory.Buffer&lt;float&gt;" - Custom buffer type</description></item>
    /// </list>
    /// </example>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this parameter represents a memory buffer.
    /// </summary>
    /// <value>
    /// <c>true</c> if the parameter is a buffer type (array, span, or custom buffer);
    /// otherwise, <c>false</c> for scalar value parameters.
    /// </value>
    /// <remarks>
    /// Buffer parameters require special handling during kernel execution, including
    /// memory allocation, data transfer between host and compute device, and proper
    /// cleanup after execution.
    /// </remarks>
    public bool IsBuffer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this parameter is read-only.
    /// </summary>
    /// <value>
    /// <c>true</c> if the parameter should not be modified during kernel execution;
    /// <c>false</c> if the parameter may be written to or modified.
    /// </value>
    /// <remarks>
    /// This flag helps optimize memory management and enables certain compiler
    /// optimizations. Read-only parameters can be cached more aggressively and
    /// may not require write-back operations after kernel execution.
    /// </remarks>
    public bool IsReadOnly { get; set; }
}
