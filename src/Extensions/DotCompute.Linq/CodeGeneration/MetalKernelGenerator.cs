// <copyright file="MetalKernelGenerator.cs" company="DotCompute">
// Copyright (c) DotCompute. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Generates Metal Shading Language (MSL) GPU kernels from LINQ operation graphs.
/// </summary>
/// <remarks>
/// <para>
/// This generator produces optimized Metal kernels for Apple Silicon (M1/M2/M3) and AMD GPUs.
/// Supports Metal 2.0+ (iOS 12+, macOS 10.13+) with threadgroup memory optimization.
/// </para>
/// <para>
/// Generated kernels use:
/// - `device` memory qualifiers for global memory
/// - `[[buffer(N)]]` attributes for parameter binding
/// - `[[thread_position_in_grid]]` for thread indexing
/// - Bounds checking for safe memory access
/// </para>
/// <para>
/// Phase 5 Task 4: GPU Kernel Generation Implementation.
/// </para>
/// </remarks>
public class MetalKernelGenerator : IGpuKernelGenerator
{
    private readonly StringBuilder _builder;
    private const int IndentSize = 4;
    private int _indentLevel;
    private int _tempVarCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalKernelGenerator"/> class.
    /// </summary>
    public MetalKernelGenerator()
    {
        _builder = new StringBuilder(4096);
        _indentLevel = 0;
        _tempVarCounter = 0;
    }

    /// <inheritdoc/>
    public string GenerateCudaKernel(OperationGraph graph, TypeMetadata metadata)
    {
        throw new NotSupportedException("CUDA kernel generation is handled by CudaKernelGenerator. Use GenerateMetalKernel for Metal backend.");
    }

    /// <inheritdoc/>
    public string GenerateOpenCLKernel(OperationGraph graph, TypeMetadata metadata)
    {
        throw new NotSupportedException("OpenCL kernel generation is handled by OpenCLKernelGenerator. Use GenerateMetalKernel for Metal backend.");
    }

    /// <inheritdoc/>
    public string GenerateMetalKernel(OperationGraph graph, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateGraph(graph);

        // Thread-safety: Lock the entire generation to prevent concurrent modification
        lock (_builder)
        {
            _builder.Clear();
            _indentLevel = 0;
            _tempVarCounter = 0;

            // Check if this kernel contains filter operations (requires atomic counter)
            var hasFilterOperation = graph.Operations.Any(op =>
                op.Type == OperationType.Filter);

            // Generate Metal header
            EmitMetalHeader();
            _builder.AppendLine();

            // Generate kernel function
            GenerateKernelFunction(graph, metadata, hasFilterOperation);

            return _builder.ToString();
        }
    }

    /// <inheritdoc/>
    public GpuCompilationOptions GetCompilationOptions(ComputeBackend backend)
    {
        if (backend != ComputeBackend.Metal)
        {
            throw new ArgumentException($"MetalKernelGenerator only supports Metal backend, got {backend}", nameof(backend));
        }

        return new GpuCompilationOptions
        {
            ThreadBlockSize = 256, // Optimal for Apple Silicon
            MaxRegisters = 32,     // Metal compiler manages registers automatically
            UseSharedMemory = true,
            TargetArchitecture = "metal2.0", // Metal 2.0+ for broad compatibility
            EnableFastMath = true,
            MaxThreadsPerBlock = 1024 // Metal maximum
        };
    }

    /// <summary>
    /// Emits the Metal header with required includes.
    /// </summary>
    private void EmitMetalHeader()
    {
        _builder.AppendLine("#include <metal_stdlib>");
        _builder.AppendLine("using namespace metal;");
    }

    /// <summary>
    /// Generates the complete Metal kernel function.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for input/output types.</param>
    /// <param name="includeOutputCount">Whether to include atomic output counter parameter for filter operations.</param>
    private void GenerateKernelFunction(OperationGraph graph, TypeMetadata metadata, bool includeOutputCount = false)
    {
        var inputTypeName = MapTypeToMetal(metadata.InputType);
        var outputTypeName = MapTypeToMetal(metadata.ResultType ?? metadata.InputType);

        // Generate kernel signature
        _builder.AppendLine();
        _builder.AppendLine("/// Generated Metal kernel for LINQ query");
        AppendLine("kernel void ComputeKernel(");
        _indentLevel++;

        // Parameters with buffer bindings
        AppendLine($"device const {inputTypeName}* input [[buffer(0)]],");
        AppendLine($"device {outputTypeName}* output [[buffer(1)]],");

        if (includeOutputCount)
        {
            AppendLine($"device atomic_int* outputCount [[buffer(2)]],");
            AppendLine($"constant int& length [[buffer(3)]],");
        }
        else
        {
            AppendLine($"constant int& length [[buffer(2)]],");
        }

        AppendLine($"uint idx [[thread_position_in_grid]])");

        _indentLevel--;
        AppendLine("{");
        _indentLevel++;

        // Bounds check
        AppendLine("// Bounds checking for safe memory access");
        AppendLine("if (idx >= length) {");
        _indentLevel++;
        AppendLine("return;");
        _indentLevel--;
        AppendLine("}");
        _builder.AppendLine();

        // Generate kernel body
        GenerateKernelBody(graph, metadata);

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates the kernel body from the operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to compile.</param>
    /// <param name="metadata">Type metadata for input/output types.</param>
    private void GenerateKernelBody(OperationGraph graph, TypeMetadata metadata)
    {
        // Check if we can fuse multiple operations
        var fusableOps = IdentifyFusableOperations(graph.Operations);

        if (fusableOps.Count > 1)
        {
            AppendLine("// Fused operations for optimal performance");
            GenerateFusedOperation(fusableOps, metadata);
        }
        else
        {
            // Generate individual operations
            foreach (var operation in graph.Operations)
            {
                GenerateOperation(operation, metadata);
            }
        }
    }

    /// <summary>
    /// Generates code for a single operation.
    /// </summary>
    /// <param name="operation">The operation to generate code for.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateOperation(Operation operation, TypeMetadata metadata)
    {
        switch (operation.Type)
        {
            case OperationType.Map:
                GenerateMapOperation(operation, metadata);
                break;

            case OperationType.Filter:
                GenerateFilterOperation(operation, metadata);
                break;

            case OperationType.Aggregate:
            case OperationType.Reduce:
                GenerateReduceOperation(operation, metadata);
                break;

            case OperationType.Scan:
                GenerateScanOperation(operation, metadata);
                break;

            case OperationType.Join:
            case OperationType.GroupBy:
            case OperationType.OrderBy:
                // Complex operations not yet implemented
                AppendLine($"// {operation.Type} operation not yet implemented - passing through");
                AppendLine("output[idx] = input[idx];");
                break;

            default:
                throw new InvalidOperationException($"Unsupported operation type: {operation.Type}");
        }
    }

    /// <summary>
    /// Generates a map (Select/transformation) operation.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateMapOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        AppendLine($"// Map operation: element-wise transformation");

        var lambda = TryGetLambda(op);
        if (lambda != null)
        {
            var elementCode = EmitLambdaInline(lambda, "input[idx]");
            AppendLine($"output[idx] = {elementCode};");
        }
        else
        {
            // Default: pass-through
            AppendLine("output[idx] = input[idx];");
        }
    }

    /// <summary>
    /// Generates a filter (Where) operation with atomic stream compaction.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    /// <remarks>
    /// <para>
    /// <b>Task 10: GPU-Accelerated Filter Compaction</b>
    /// </para>
    /// <para>
    /// Uses Metal's atomic operations for thread-safe output position allocation.
    /// Each thread that passes the predicate atomically increments the output
    /// counter and writes to the allocated position.
    /// </para>
    /// <para>
    /// <b>Metal Implementation Details:</b>
    /// <list type="bullet">
    /// <item>Uses <c>atomic_fetch_add_explicit()</c> for thread-safe position allocation</item>
    /// <item>Requires <c>device atomic_int* outputCount</c> parameter in kernel signature</item>
    /// <item>Uses <c>memory_order_relaxed</c> for optimal performance</item>
    /// <item>Final count accessible via <c>atomic_load(outputCount)</c> after kernel execution</item>
    /// </list>
    /// </para>
    /// </remarks>
    private void GenerateFilterOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        var typeName = MapTypeToMetal(metadata.InputType);

        AppendLine($"// Filter operation: {op.Type}");
        AppendLine("// Stream compaction using atomic counter for thread-safe output allocation");
        AppendLine();

        var lambda = GetLambda(op);
        var predicate = EmitLambdaInline(lambda, "input[idx]");

        AppendLine("// Evaluate predicate");
        AppendLine($"if ({predicate})");
        AppendLine("{");
        _indentLevel++;

        AppendLine("// Atomically allocate output position (Metal 2.0+)");
        AppendLine("int outIdx = atomic_fetch_add_explicit(outputCount, 1, memory_order_relaxed);");
        AppendLine();

        AppendLine("// Write passing element to compacted output");
        AppendLine("output[outIdx] = input[idx];");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates a reduce (Sum/Aggregate) operation.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateReduceOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        AppendLine($"// Reduce operation: parallel reduction");
        AppendLine("// Note: Requires two-phase reduction (local + global)");
        AppendLine("// For MVP: simplified atomic accumulation");

        var lambda = TryGetLambda(op);
        if (lambda != null)
        {
            var operation = EmitLambdaInline(lambda, "input[idx]");
            AppendLine($"auto value = {operation};");
            AppendLine("// atomic_fetch_add_explicit(&output[0], value, memory_order_relaxed);");
            AppendLine("output[0] += value; // Simplified (not thread-safe)");
        }
        else
        {
            AppendLine("// atomic_fetch_add_explicit(&output[0], input[idx], memory_order_relaxed);");
            AppendLine("output[0] += input[idx]; // Simplified (not thread-safe)");
        }
    }

    /// <summary>
    /// Generates a scan (cumulative/prefix sum) operation.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateScanOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        AppendLine($"// Scan operation: cumulative computation");
        AppendLine("// Note: Scan requires parallel prefix sum algorithm");
        AppendLine("// For MVP: not implemented (requires complex parallel scan)");
        AppendLine("output[idx] = input[idx];");
    }

    /// <summary>
    /// Generates a fused operation combining multiple operations.
    /// </summary>
    /// <param name="ops">The list of operations to fuse.</param>
    /// <param name="metadata">Type metadata for the operations.</param>
    /// <remarks>
    /// <para>
    /// <b>Task 10: Kernel Fusion Optimization</b>
    /// </para>
    /// <para>
    /// Combines multiple LINQ operations into a single kernel to reduce memory bandwidth.
    /// Supports conditional execution for filter operations using <c>bool passesFilter</c> flag.
    /// </para>
    /// <para>
    /// <b>Supported Fusion Patterns:</b>
    /// <list type="bullet">
    /// <item><b>Map+Map</b>: Sequential transformations in registers</item>
    /// <item><b>Map+Filter</b>: Transform then conditionally filter</item>
    /// <item><b>Filter+Map</b>: Filter then transform passing elements</item>
    /// <item><b>Filter+Filter</b>: Combined predicates with AND logic</item>
    /// </list>
    /// </para>
    /// </remarks>
    private void GenerateFusedOperation(System.Collections.Generic.List<Operation> ops, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(ops);
        ArgumentNullException.ThrowIfNull(metadata);

        if (ops.Count == 0)
        {
            throw new ArgumentException("Operation list cannot be empty.", nameof(ops));
        }

        AppendLine($"// Fused operations: {string.Join(" -> ", ops.Select(o => o.Type))}");
        AppendLine("// Performance: Eliminates intermediate memory transfers");

        // Check if fusion contains filter operations (requires conditional execution)
        var hasFilter = ops.Any(op => op.Type == OperationType.Filter);

        // Start with input value
        var typeName = MapTypeToMetal(metadata.InputType);
        AppendLine($"{typeName} value = input[idx];");
        AppendLine();

        if (hasFilter)
        {
            // For filter-containing fusions, use conditional execution
            AppendLine("// Conditional execution for filter operations");
            AppendLine("bool passesFilter = true;");
            AppendLine();
        }

        // Apply each operation sequentially to the value
        foreach (var op in ops)
        {
            if (op.Type == OperationType.Map)
            {
                var lambda = TryGetLambda(op);
                if (lambda != null)
                {
                    var operationCode = EmitLambdaInline(lambda, "value");

                    if (hasFilter)
                    {
                        // Conditional transformation (only if passed filter)
                        AppendLine("if (passesFilter)");
                        AppendLine("{");
                        _indentLevel++;
                        AppendLine($"value = {operationCode};");
                        _indentLevel--;
                        AppendLine("}");
                    }
                    else
                    {
                        // Direct transformation
                        AppendLine($"value = {operationCode};");
                    }
                }
            }
            else if (op.Type == OperationType.Filter)
            {
                var lambda = GetLambda(op);
                var predicate = EmitLambdaInline(lambda, "value");

                AppendLine($"// Filter: Check if element passes predicate");
                AppendLine($"passesFilter = passesFilter && ({predicate});");
            }
        }

        AppendLine();

        if (hasFilter)
        {
            // Only write if passed all filters
            AppendLine("// Write result only if passed all filter predicates");
            AppendLine("if (passesFilter)");
            AppendLine("{");
            _indentLevel++;
            AppendLine("output[idx] = value;");
            _indentLevel--;
            AppendLine("}");
        }
        else
        {
            // Direct write for map-only fusions
            AppendLine("// Write fused transformation result");
            AppendLine("output[idx] = value;");
        }
    }

    /// <summary>
    /// Identifies fusable operations in a sequence.
    /// </summary>
    /// <param name="operations">The list of operations to analyze.</param>
    /// <returns>A list of fusable operations.</returns>
    /// <remarks>
    /// <para>
    /// <b>Task 10: Enhanced Fusion Detection</b>
    /// </para>
    /// <para>
    /// Supports fusion patterns: Map+Map, Map+Filter, Filter+Map, Filter+Filter.
    /// Operations requiring global synchronization (Reduce, Aggregate, Scan, GroupBy, Join, OrderBy)
    /// cannot be fused and will break the fusion chain.
    /// </para>
    /// </remarks>
    private System.Collections.Generic.List<Operation> IdentifyFusableOperations(System.Collections.Generic.IReadOnlyList<Operation> operations)
    {
        var fusable = new System.Collections.Generic.List<Operation>();

        for (var i = 0; i < operations.Count; i++)
        {
            var current = operations[i];

            // Check if current operation can be fused
            if (!CanBeFused(current))
            {
                // Stop fusion at non-fusable operations
                if (fusable.Count == 0)
                {
                    fusable.Add(current); // Include first non-fusable operation
                }
                break;
            }

            fusable.Add(current);

            // Check if we can continue fusing with next operation
            if (i + 1 < operations.Count)
            {
                var next = operations[i + 1];

                // Can fuse Map-Filter, Filter-Map, Map-Map, Filter-Filter
                if ((current.Type == OperationType.Map && next.Type == OperationType.Filter) ||
                    (current.Type == OperationType.Filter && next.Type == OperationType.Map) ||
                    (current.Type == OperationType.Map && next.Type == OperationType.Map) ||
                    (current.Type == OperationType.Filter && next.Type == OperationType.Filter))
                {
                    continue; // Continue fusion with next operation
                }
                else
                {
                    break; // Stop fusion at incompatible operation
                }
            }
        }

        return fusable;
    }

    /// <summary>
    /// Determines if an operation can be fused with adjacent operations.
    /// </summary>
    /// <param name="op">The operation to check.</param>
    /// <returns>True if the operation can be fused; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// <b>Fusable Operations:</b>
    /// <list type="bullet">
    /// <item><b>Map</b>: Element-wise transformation, can be fused</item>
    /// <item><b>Filter</b>: Element-wise predicate, can be fused</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Non-Fusable Operations:</b>
    /// <list type="bullet">
    /// <item><b>Reduce/Aggregate</b>: Requires global synchronization</item>
    /// <item><b>Scan</b>: Sequential dependencies across threads</item>
    /// <item><b>GroupBy/Join/OrderBy</b>: Require global coordination</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static bool CanBeFused(Operation op)
    {
        return op.Type switch
        {
            OperationType.Map => true,      // Element-wise transformation
            OperationType.Filter => true,   // Element-wise predicate
            OperationType.Reduce => false,  // Requires synchronization
            OperationType.Aggregate => false, // Requires synchronization
            OperationType.Scan => false,    // Sequential dependencies
            OperationType.GroupBy => false, // Global coordination needed
            OperationType.Join => false,    // Global coordination needed
            OperationType.OrderBy => false, // Global coordination needed
            _ => false // Unknown operations cannot be fused
        };
    }

    /// <summary>
    /// Maps a C# type to its Metal Shading Language equivalent.
    /// </summary>
    /// <param name="type">The C# type to map.</param>
    /// <returns>The Metal type name.</returns>
    private string MapTypeToMetal(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type switch
        {
            Type t when t == typeof(byte) => "uchar",
            Type t when t == typeof(sbyte) => "char",
            Type t when t == typeof(short) => "short",
            Type t when t == typeof(ushort) => "ushort",
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(uint) => "uint",
            Type t when t == typeof(long) => "long",
            Type t when t == typeof(ulong) => "ulong",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(bool) => "bool",
            _ => throw new NotSupportedException($"Type {type.Name} is not supported in Metal kernels")
        };
    }

    /// <summary>
    /// Emits a lambda expression inline as Metal code.
    /// </summary>
    /// <param name="lambda">The lambda expression to inline.</param>
    /// <param name="inputVar">The variable name to substitute for the parameter.</param>
    /// <returns>The inlined Metal code.</returns>
    private string EmitLambdaInline(LambdaExpression lambda, string inputVar)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        ArgumentNullException.ThrowIfNull(inputVar);

        var body = lambda.Body;
        var parameter = lambda.Parameters[0];

        // Replace parameter references with input variable
        var visitor = new ParameterReplacementVisitor(parameter, inputVar);
        var replaced = visitor.Visit(body);

        return ExpressionToMetalCode(replaced);
    }

    /// <summary>
    /// Converts an expression tree to Metal code.
    /// </summary>
    /// <param name="expr">The expression to convert.</param>
    /// <returns>The Metal code representation.</returns>
    private string ExpressionToMetalCode(Expression expr)
    {
        return expr switch
        {
            BinaryExpression binary => GenerateBinaryExpression(binary),
            UnaryExpression unary when unary.NodeType == ExpressionType.Convert => ExpressionToMetalCode(unary.Operand),
            UnaryExpression unary => $"{GetOperator(unary.NodeType)}({ExpressionToMetalCode(unary.Operand)})",
            ConstantExpression constant => FormatConstant(constant),
            MemberExpression member => $"{ExpressionToMetalCode(member.Expression!)}.{member.Member.Name}",
            MethodCallExpression method => GenerateMethodCall(method),
            ParameterExpression parameter => parameter.Name ?? $"temp{_tempVarCounter++}",
            _ => throw new NotSupportedException($"Expression type {expr.NodeType} is not supported in Metal kernel generation")
        };
    }

    /// <summary>
    /// Generates a binary expression in Metal syntax.
    /// </summary>
    private string GenerateBinaryExpression(BinaryExpression binary)
    {
        var left = ExpressionToMetalCode(binary.Left);
        var right = ExpressionToMetalCode(binary.Right);
        var op = GetOperator(binary.NodeType);

        return $"({left} {op} {right})";
    }

    /// <summary>
    /// Formats a constant value for Metal code.
    /// </summary>
    /// <remarks>
    /// ToLowerInvariant() is intentional for Metal boolean constants (true/false).
    /// Metal Shading Language boolean literals are lowercase and case-sensitive.
    /// </remarks>
#pragma warning disable CA1308 // ToLowerInvariant is correct for Metal boolean literals
    private string FormatConstant(ConstantExpression constant)
    {
        if (constant.Value == null)
        {
            return "0";
        }

        return constant.Type switch
        {
            Type t when t == typeof(float) => $"{constant.Value}f",
            Type t when t == typeof(double) => $"{constant.Value}",
            Type t when t == typeof(long) => $"{constant.Value}LL",
            Type t when t == typeof(ulong) => $"{constant.Value}ULL",
            Type t when t == typeof(uint) => $"{constant.Value}u",
            Type t when t == typeof(bool) => constant.Value.ToString()?.ToLowerInvariant() ?? "false",
            _ => constant.Value.ToString() ?? "0"
        };
    }
#pragma warning restore CA1308

    /// <summary>
    /// Gets the Metal operator for an expression type.
    /// </summary>
    private string GetOperator(ExpressionType type)
    {
        return type switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            ExpressionType.Equal => "==",
            ExpressionType.NotEqual => "!=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.AndAlso => "&&",
            ExpressionType.OrElse => "||",
            ExpressionType.Not => "!",
            ExpressionType.Negate => "-",
            _ => throw new NotSupportedException($"Operator {type} is not supported in Metal kernels")
        };
    }

    /// <summary>
    /// Generates a method call in Metal syntax.
    /// </summary>
    /// <remarks>
    /// ToLowerInvariant() is intentional for Metal MSL function names (sin, cos, sqrt, etc.).
    /// Metal Shading Language function names are lowercase by specification.
    /// </remarks>
#pragma warning disable CA1308 // ToLowerInvariant is correct for Metal function names
    private string GenerateMethodCall(MethodCallExpression method)
    {
        var args = string.Join(", ", method.Arguments.Select(ExpressionToMetalCode));

        // Map common Math methods to Metal equivalents
        if (method.Method.DeclaringType == typeof(Math))
        {
            return method.Method.Name.ToLowerInvariant() switch
            {
                "abs" => $"abs({args})",
                "sqrt" => $"sqrt({args})",
                "pow" => $"pow({args})",
                "sin" => $"sin({args})",
                "cos" => $"cos({args})",
                "tan" => $"tan({args})",
                "floor" => $"floor({args})",
                "ceil" => $"ceil({args})",
                "round" => $"round({args})",
                "min" => $"min({args})",
                "max" => $"max({args})",
                _ => throw new NotSupportedException($"Math method {method.Method.Name} is not supported in Metal kernels")
            };
        }

        if (method.Object != null)
        {
            return $"{ExpressionToMetalCode(method.Object)}.{method.Method.Name}({args})";
        }

        return $"{method.Method.Name}({args})";
    }
#pragma warning restore CA1308

    /// <summary>
    /// Extracts the lambda expression from an Operation.
    /// </summary>
    private LambdaExpression GetLambda(Operation operation)
    {
        var lambda = TryGetLambda(operation);
        if (lambda != null)
        {
            return lambda;
        }

        throw new InvalidOperationException($"Operation {operation.Type} does not have a Lambda expression");
    }

    /// <summary>
    /// Tries to extract the lambda expression from an Operation.
    /// </summary>
    private LambdaExpression? TryGetLambda(Operation operation)
    {
        // Try to get Lambda property directly (OperationNode)
        var lambdaProperty = operation.GetType().GetProperty("Lambda");
        if (lambdaProperty != null)
        {
            var lambda = lambdaProperty.GetValue(operation) as LambdaExpression;
            if (lambda != null)
            {
                return lambda;
            }
        }

        // Fall back to extracting from Metadata
        if (operation.Metadata.TryGetValue("Lambda", out var lambdaObj) && lambdaObj is LambdaExpression metadataLambda)
        {
            return metadataLambda;
        }

        return null;
    }

    /// <summary>
    /// Validates the operation graph before code generation.
    /// </summary>
    private void ValidateGraph(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Operations.Count == 0)
        {
            throw new InvalidOperationException("Operation graph contains no operations.");
        }

        foreach (var op in graph.Operations)
        {
            if (!Enum.IsDefined(typeof(OperationType), op.Type))
            {
                throw new InvalidOperationException($"Operation graph contains invalid operation type: {op.Type}");
            }
        }
    }

    /// <summary>
    /// Appends a line with proper indentation.
    /// </summary>
    private void AppendLine(string line = "")
    {
        if (!string.IsNullOrEmpty(line))
        {
            var indentSpaces = Math.Max(0, _indentLevel * IndentSize);
            _builder.Append(' ', indentSpaces);
        }

        _builder.AppendLine(line);
    }

    /// <summary>
    /// Visitor for replacing parameter references in expression trees.
    /// </summary>
    private class ParameterReplacementVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly string _replacement;

        public ParameterReplacementVisitor(ParameterExpression parameter, string replacement)
        {
            _parameter = parameter;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _parameter)
            {
                return Expression.Parameter(node.Type, _replacement);
            }

            return base.VisitParameter(node);
        }
    }
}
