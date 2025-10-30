// <copyright file="UnifiedOperatorType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Analysis;

/// <summary>
/// Comprehensive unified operator type enumeration that consolidates all operator types
/// from across the DotCompute codebase.
/// </summary>
public enum UnifiedOperatorType
{
    /// <summary>Unknown or unspecified operator type.</summary>
    Unknown,

    // Arithmetic Operations
    /// <summary>Arithmetic operations (Add, Subtract, Multiply, Divide, etc.).</summary>
    Arithmetic,

    /// <summary>Addition operation.</summary>
    Add,

    /// <summary>Subtraction operation.</summary>
    Subtract,

    /// <summary>Multiplication operation.</summary>
    Multiply,

    /// <summary>Division operation.</summary>
    Divide,

    /// <summary>Modulo operation.</summary>
    Modulo,

    /// <summary>Power operation.</summary>
    Power,

    // Logical Operations
    /// <summary>Logical operations (And, Or, Not, etc.).</summary>
    Logical,

    /// <summary>Logical AND operation.</summary>
    LogicalAnd,

    /// <summary>Logical OR operation.</summary>
    LogicalOr,

    /// <summary>Logical NOT operation.</summary>
    LogicalNot,

    /// <summary>Logical XOR operation.</summary>
    LogicalXor,

    // Bitwise Operations
    /// <summary>Bitwise AND operation.</summary>
    BitwiseAnd,

    /// <summary>Bitwise OR operation.</summary>
    BitwiseOr,

    /// <summary>Bitwise XOR operation.</summary>
    BitwiseXor,

    /// <summary>Bitwise NOT operation.</summary>
    BitwiseNot,

    /// <summary>Left shift operation.</summary>
    LeftShift,

    /// <summary>Right shift operation.</summary>
    RightShift,

    // Comparison Operations
    /// <summary>Comparison operations (Equal, NotEqual, LessThan, GreaterThan, etc.).</summary>
    Comparison,

    /// <summary>Equality comparison.</summary>
    Equal,

    /// <summary>Not equal comparison.</summary>
    NotEqual,

    /// <summary>Less than comparison.</summary>
    LessThan,

    /// <summary>Less than or equal comparison.</summary>
    LessThanOrEqual,

    /// <summary>Greater than comparison.</summary>
    GreaterThan,

    /// <summary>Greater than or equal comparison.</summary>
    GreaterThanOrEqual,

    // Control Flow Operations
    /// <summary>Conditional operations (conditional expressions, ternary operators).</summary>
    Conditional,

    /// <summary>If-then-else operation.</summary>
    IfThenElse,

    /// <summary>Switch operation.</summary>
    Switch,

    /// <summary>Loop operation.</summary>
    Loop,

    /// <summary>Branch operation.</summary>
    Branch,

    // Assignment Operations
    /// <summary>Assignment operations.</summary>
    Assignment,

    /// <summary>Simple assignment.</summary>
    Assign,

    /// <summary>Addition assignment.</summary>
    AddAssign,

    /// <summary>Subtraction assignment.</summary>
    SubtractAssign,

    /// <summary>Multiplication assignment.</summary>
    MultiplyAssign,

    /// <summary>Division assignment.</summary>
    DivideAssign,

    // LINQ Operations
    /// <summary>Filtering operation (Where).</summary>
    Filter,

    /// <summary>Where clause operation.</summary>
    Where,

    /// <summary>Projection operation (Select).</summary>
    Projection,

    /// <summary>Select operation.</summary>
    Select,

    /// <summary>SelectMany operation.</summary>
    SelectMany,

    /// <summary>Aggregation operation (Sum, Count, etc.).</summary>
    Aggregation,

    /// <summary>Sum operation.</summary>
    Sum,

    /// <summary>Count operation.</summary>
    Count,

    /// <summary>Average operation.</summary>
    Average,

    /// <summary>Min operation.</summary>
    Min,

    /// <summary>Max operation.</summary>
    Max,

    /// <summary>First operation.</summary>
    First,

    /// <summary>Last operation.</summary>
    Last,

    /// <summary>Single element operation (LINQ Single method).</summary>
    SingleElement,

    /// <summary>Any operation.</summary>
    Any,

    /// <summary>All operation.</summary>
    All,

    /// <summary>Sorting operation (OrderBy).</summary>
    Sort,

    /// <summary>OrderBy operation.</summary>
    OrderBy,

    /// <summary>OrderByDescending operation.</summary>
    OrderByDescending,

    /// <summary>ThenBy operation.</summary>
    ThenBy,

    /// <summary>ThenByDescending operation.</summary>
    ThenByDescending,

    /// <summary>Grouping operation (GroupBy).</summary>
    Group,

    /// <summary>GroupBy operation.</summary>
    GroupBy,

    /// <summary>Join operation.</summary>
    Join,

    /// <summary>GroupJoin operation.</summary>
    GroupJoin,

    /// <summary>LeftJoin operation.</summary>
    LeftJoin,

    /// <summary>RightJoin operation.</summary>
    RightJoin,

    /// <summary>InnerJoin operation.</summary>
    InnerJoin,

    /// <summary>OuterJoin operation.</summary>
    OuterJoin,

    // Set Operations
    /// <summary>Union operation.</summary>
    Union,

    /// <summary>Intersect operation.</summary>
    Intersect,

    /// <summary>Except operation.</summary>
    Except,

    /// <summary>Distinct operation.</summary>
    Distinct,

    /// <summary>Concat operation.</summary>
    Concat,

    // Sequence Operations
    /// <summary>Take operation.</summary>
    Take,

    /// <summary>Skip operation.</summary>
    Skip,

    /// <summary>TakeWhile operation.</summary>
    TakeWhile,

    /// <summary>SkipWhile operation.</summary>
    SkipWhile,

    /// <summary>Reverse operation.</summary>
    Reverse,

    /// <summary>Zip operation.</summary>
    Zip,

    // Mathematical Operations
    /// <summary>Mathematical operation.</summary>
    Mathematical,

    /// <summary>Trigonometric operations.</summary>
    Trigonometric,

    /// <summary>Sine operation.</summary>
    Sin,

    /// <summary>Cosine operation.</summary>
    Cos,

    /// <summary>Tangent operation.</summary>
    Tan,

    /// <summary>Arc sine operation.</summary>
    ASin,

    /// <summary>Arc cosine operation.</summary>
    ACos,

    /// <summary>Arc tangent operation.</summary>
    ATan,

    /// <summary>Hyperbolic sine operation.</summary>
    Sinh,

    /// <summary>Hyperbolic cosine operation.</summary>
    Cosh,

    /// <summary>Hyperbolic tangent operation.</summary>
    Tanh,

    /// <summary>Square root operation.</summary>
    Sqrt,

    /// <summary>Exponential operation.</summary>
    Exp,

    /// <summary>Natural logarithm operation.</summary>
    Log,

    /// <summary>Logarithm base 10 operation.</summary>
    Log10,

    /// <summary>Absolute value operation.</summary>
    Abs,

    /// <summary>Floor operation.</summary>
    Floor,

    /// <summary>Ceiling operation.</summary>
    Ceiling,

    /// <summary>Round operation.</summary>
    Round,

    // Conversion Operations
    /// <summary>Conversion operation.</summary>
    Conversion,

    /// <summary>Cast operation.</summary>
    Cast,

    /// <summary>OfType operation.</summary>
    OfType,

    /// <summary>AsEnumerable operation.</summary>
    AsEnumerable,

    /// <summary>AsQueryable operation.</summary>
    AsQueryable,

    /// <summary>ToArray operation.</summary>
    ToArray,

    /// <summary>ToList operation.</summary>
    ToList,

    /// <summary>ToDictionary operation.</summary>
    ToDictionary,

    /// <summary>ToLookup operation.</summary>
    ToLookup,

    // Memory Operations
    /// <summary>Memory operation.</summary>
    Memory,

    /// <summary>Memory allocation operation.</summary>
    Allocation,

    /// <summary>Memory deallocation operation.</summary>
    Deallocation,

    /// <summary>Memory copy operation.</summary>
    Copy,

    /// <summary>Memory move operation.</summary>
    Move,

    /// <summary>Memory set operation.</summary>
    Set,

    // Reduction Operations
    /// <summary>Reduction operation.</summary>
    Reduction,

    /// <summary>Map-reduce operation.</summary>
    MapReduce,

    /// <summary>Fold operation.</summary>
    Fold,

    /// <summary>Scan operation.</summary>
    Scan,

    // Transformation Operations
    /// <summary>Transformation operation.</summary>
    Transformation,

    /// <summary>Transform operation (alias for Transformation).</summary>
    Transform,

    /// <summary>Map operation.</summary>
    Map,

    /// <summary>FlatMap operation.</summary>
    FlatMap,

    /// <summary>Transpose operation.</summary>
    Transpose,

    /// <summary>Reshape operation.</summary>
    Reshape,

    // Method Call Operations
    /// <summary>Method call operation.</summary>
    MethodCall,

    /// <summary>Static method call.</summary>
    StaticMethodCall,

    /// <summary>Instance method call.</summary>
    InstanceMethodCall,

    /// <summary>Extension method call.</summary>
    ExtensionMethodCall,

    /// <summary>Generic method call.</summary>
    GenericMethodCall,

    // Custom Operations
    /// <summary>Custom operation.</summary>
    Custom,

    /// <summary>User-defined operation.</summary>
    UserDefined,

    /// <summary>Plugin operation.</summary>
    Plugin,

    /// <summary>External operation.</summary>
    External
}

/// <summary>
/// Extension methods for UnifiedOperatorType to provide conversion and utility functions.
/// </summary>
public static class UnifiedOperatorTypeExtensions
{
    private static readonly Dictionary<UnifiedOperatorType, string[]> OperatorCategories = new()
    {
        [UnifiedOperatorType.Arithmetic] = ["Add", "Subtract", "Multiply", "Divide", "Modulo", "Power"],
        [UnifiedOperatorType.Logical] = ["LogicalAnd", "LogicalOr", "LogicalNot", "LogicalXor"],
        [UnifiedOperatorType.Comparison] = ["Equal", "NotEqual", "LessThan", "GreaterThan", "LessThanOrEqual", "GreaterThanOrEqual"],
        [UnifiedOperatorType.Filter] = ["Where"],
        [UnifiedOperatorType.Projection] = ["Select", "SelectMany"],
        [UnifiedOperatorType.Aggregation] = ["Sum", "Count", "Average", "Min", "Max", "First", "Last", "SingleElement", "Any", "All"],
        [UnifiedOperatorType.Sort] = ["OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending"],
        [UnifiedOperatorType.Group] = ["GroupBy"],
        [UnifiedOperatorType.Join] = ["Join", "GroupJoin", "LeftJoin", "RightJoin", "InnerJoin", "OuterJoin"],
        [UnifiedOperatorType.Mathematical] = ["Sin", "Cos", "Tan", "ASin", "ACos", "ATan", "Sinh", "Cosh", "Tanh", "Sqrt", "Exp", "Log", "Log10", "Abs", "Floor", "Ceiling", "Round"],
        [UnifiedOperatorType.Conversion] = ["Cast", "OfType", "AsEnumerable", "AsQueryable", "ToArray", "ToList", "ToDictionary", "ToLookup"],
        [UnifiedOperatorType.Memory] = ["Allocation", "Deallocation", "Copy", "Move", "Set"],
        [UnifiedOperatorType.Reduction] = ["MapReduce", "Fold", "Scan"],
        [UnifiedOperatorType.Transformation] = ["Map", "FlatMap", "Transpose", "Reshape"],
        [UnifiedOperatorType.MethodCall] = ["StaticMethodCall", "InstanceMethodCall", "ExtensionMethodCall", "GenericMethodCall"]
    };

    /// <summary>
    /// Gets the category of the operator.
    /// </summary>
    public static UnifiedOperatorType GetCategory(this UnifiedOperatorType operatorType)
    {
        foreach (var category in OperatorCategories)
        {
            if (category.Value.Contains(operatorType.ToString()))
            {
                return category.Key;
            }
        }
        return UnifiedOperatorType.Unknown;
    }

    /// <summary>
    /// Checks if the operator is a LINQ operation.
    /// </summary>
    public static bool IsLinqOperation(this UnifiedOperatorType operatorType) => operatorType is >= UnifiedOperatorType.Filter and <= UnifiedOperatorType.Zip;

    /// <summary>
    /// Checks if the operator is an arithmetic operation.
    /// </summary>
    public static bool IsArithmetic(this UnifiedOperatorType operatorType) => operatorType is >= UnifiedOperatorType.Arithmetic and <= UnifiedOperatorType.Power;

    /// <summary>
    /// Checks if the operator is a comparison operation.
    /// </summary>
    public static bool IsComparison(this UnifiedOperatorType operatorType) => operatorType is >= UnifiedOperatorType.Comparison and <= UnifiedOperatorType.GreaterThanOrEqual;

    /// <summary>
    /// Checks if the operator supports parallel execution.
    /// </summary>
    public static bool SupportsParallelExecution(this UnifiedOperatorType operatorType)
    {
        return operatorType switch
        {
            UnifiedOperatorType.Map or
            UnifiedOperatorType.Select or
            UnifiedOperatorType.Where or
            UnifiedOperatorType.Filter or
            UnifiedOperatorType.Arithmetic or
            UnifiedOperatorType.Mathematical or
            UnifiedOperatorType.Transformation => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the estimated complexity of the operator.
    /// </summary>
    public static int GetEstimatedComplexity(this UnifiedOperatorType operatorType)
    {
        return operatorType switch
        {
            UnifiedOperatorType.Add or UnifiedOperatorType.Subtract => 1,
            UnifiedOperatorType.Multiply => 2,
            UnifiedOperatorType.Divide => 3,
            UnifiedOperatorType.Power => 5,
            UnifiedOperatorType.Sin or UnifiedOperatorType.Cos or UnifiedOperatorType.Tan => 4,
            UnifiedOperatorType.Exp or UnifiedOperatorType.Log => 6,
            UnifiedOperatorType.Sort => 10,
            UnifiedOperatorType.GroupBy => 8,
            UnifiedOperatorType.Join => 12,
            _ => 1
        };
    }
}
