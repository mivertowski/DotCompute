// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Analysis;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Legacy OperatorType enum - use UnifiedOperatorType instead.
/// </summary>
[Obsolete("Use DotCompute.Core.Analysis.UnifiedOperatorType instead. This enum is maintained for backward compatibility.", false)]
public enum OperatorType
{
    /// <summary>
    /// Unknown or unspecified operator type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Arithmetic operations (Add, Subtract, Multiply, Divide, etc.).
    /// </summary>
    Arithmetic,

    /// <summary>
    /// Logical operations (And, Or, Not, etc.).
    /// </summary>
    Logical,

    /// <summary>
    /// Comparison operations (Equal, NotEqual, LessThan, GreaterThan, etc.).
    /// </summary>
    Comparison,

    /// <summary>
    /// Conditional operations (conditional expressions, ternary operators).
    /// </summary>
    Conditional,

    /// <summary>
    /// Assignment operations.
    /// </summary>
    Assignment,

    /// <summary>
    /// Filtering operation (Where).
    /// </summary>
    Filter,

    /// <summary>
    /// Projection operation (Select).
    /// </summary>
    Projection,

    /// <summary>
    /// Aggregation operation (Sum, Count, etc.).
    /// </summary>
    Aggregation,

    /// <summary>
    /// Sorting operation (OrderBy).
    /// </summary>
    Sort,

    /// <summary>
    /// Grouping operation (GroupBy).
    /// </summary>
    Group,

    /// <summary>
    /// Join operation.
    /// </summary>
    Join,

    /// <summary>
    /// Mathematical operation.
    /// </summary>
    Mathematical,

    /// <summary>
    /// Conversion operation.
    /// </summary>
    Conversion,

    /// <summary>
    /// Memory operation.
    /// </summary>
    Memory,

    /// <summary>
    /// Reduction operation.
    /// </summary>
    Reduction,

    /// <summary>
    /// Transformation operation.
    /// </summary>
    Transformation,

    /// <summary>
    /// Method call operation.
    /// </summary>
    MethodCall,

    /// <summary>
    /// Custom operation.
    /// </summary>
    Custom
}