// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Enums;
/// <summary>
/// An ast node type enumeration.
/// </summary>

/// <summary>
/// Types of AST nodes.
/// </summary>
internal enum AstNodeType
{
    // Arithmetic operations
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,

    // Math functions
    Abs,
    Min,
    Max,
    Sqrt,
    Pow,
    Exp,
    Log,
    Sin,
    Cos,
    Tan,

    // Logical operations
    LogicalAnd,
    LogicalOr,

    // Comparison operations
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,

    // Bitwise operations
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,

    // Memory operations
    Load,
    Store,

    // Control flow
    If,
    For,
    While,
    Return,

    // Literals and identifiers
    Constant,
    Variable,
    Parameter,

    // Unknown/error case
    Unknown
}