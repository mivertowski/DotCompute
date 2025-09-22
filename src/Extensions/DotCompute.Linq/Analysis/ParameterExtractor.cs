// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;

namespace DotCompute.Linq.Analysis;
{

/// <summary>
/// Visitor for extracting parameters and preparing kernel arguments.
/// </summary>
/// <remarks>
/// This visitor analyzes expression trees to extract constants and parameters
/// that need to be passed to GPU kernels, handling proper memory management
/// and type conversion for GPU execution.
/// </remarks>
internal class ParameterExtractor : ExpressionVisitor
{
    private readonly Dictionary<string, object> _parameters;
    private readonly IAccelerator _accelerator;
    private int _parameterIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterExtractor"/> class.
    /// </summary>
    /// <param name="parameters">Dictionary to store extracted parameters.</param>
    /// <param name="accelerator">The accelerator instance for memory management.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="parameters"/> or <paramref name="accelerator"/> is null.
    /// </exception>
    public ParameterExtractor(Dictionary<string, object> parameters, IAccelerator accelerator)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <summary>
    /// Visits constant expressions to extract parameter values.
    /// </summary>
    /// <param name="node">The constant expression to process.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method extracts constant values and prepares them for GPU kernel execution,
    /// including special handling for arrays and device memory allocation.
    /// </remarks>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value != null)
        {
            var paramName = $"param_{_parameterIndex++}";
            _parameters[paramName] = node.Value;

            // If it's an array, we might need to copy to device memory
            if (node.Value is Array array)
            {
                // This would be implemented to properly handle device memory allocation
                _parameters[$"{paramName}_size"] = array.Length;
            }
        }
        return base.VisitConstant(node);
    }

    /// <summary>
    /// Visits parameter expressions to handle parameter references.
    /// </summary>
    /// <param name="node">The parameter expression to process.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method ensures all parameter references are properly tracked and
    /// initialized with appropriate default values for kernel execution.
    /// </remarks>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        // Handle parameter references
        var paramName = node.Name ?? $"param_{_parameterIndex++}";
        if (!_parameters.ContainsKey(paramName))
        {
            _parameters[paramName] = CreateDefaultValueAotCompatible(node.Type) ?? new object();
        }
        return base.VisitParameter(node);
    }

    /// <summary>
    /// Creates AOT-compatible default values for primitive types.
    /// </summary>
    /// <param name="type">The type for which to create a default value.</param>
    /// <returns>
    /// A default value for the specified type, or <c>null</c> if no specific default is available.
    /// </returns>
    /// <remarks>
    /// This method provides explicit default values for common primitive types
    /// to ensure compatibility with ahead-of-time compilation scenarios.
    /// </remarks>
    private static object? CreateDefaultValueAotCompatible(Type type)
    {
        if (type == typeof(int))
        {
            return 0;
        }

        if (type == typeof(long))
        {
            return 0L;
        }

        if (type == typeof(float))
        {
            return 0.0f;
        }

        if (type == typeof(double))
        {
            return 0.0;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(byte))
        {
            return (byte)0;
        }

        if (type == typeof(short))
        {
            return (short)0;
        }

        if (type == typeof(uint))
        {
            return 0U;
        }

        if (type == typeof(ulong))
        {
            return 0UL;
        }

        if (type == typeof(ushort))
        {
            return (ushort)0;
        }

        if (type == typeof(char))
        {
            return '\0';
        }

        if (type == typeof(decimal))
        {
            return 0m;
        }

        return null;
    }
}