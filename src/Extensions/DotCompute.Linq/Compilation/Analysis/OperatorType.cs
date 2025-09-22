// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Analysis;
namespace DotCompute.Linq.Compilation.Analysis;
{
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
    /// Arithmetic operations (Add, Subtract, Multiply, Divide, etc.).
    Arithmetic,
    /// Logical operations (And, Or, Not, etc.).
    Logical,
    /// Comparison operations (Equal, NotEqual, LessThan, GreaterThan, etc.).
    Comparison,
    /// Conditional operations (conditional expressions, ternary operators).
    Conditional,
    /// Assignment operations.
    Assignment,
    /// Filtering operation (Where).
    Filter,
    /// Projection operation (Select).
    Projection,
    /// Aggregation operation (Sum, Count, etc.).
    Aggregation,
    /// Sorting operation (OrderBy).
    Sort,
    /// Grouping operation (GroupBy).
    Group,
    /// Join operation.
    Join,
    /// Mathematical operation.
    Mathematical,
    /// Conversion operation.
    Conversion,
    /// Memory operation.
    Memory,
    /// Reduction operation.
    Reduction,
    /// Transformation operation.
    Transformation,
    /// Method call operation.
    MethodCall,
    /// Custom operation.
    Custom
}
