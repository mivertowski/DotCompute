// <copyright file="CpuKernelGenerator.cs" company="DotCompute">
// Copyright (c) DotCompute. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Generates SIMD-accelerated CPU kernels from LINQ operation graphs.
/// </summary>
/// <remarks>
/// This generator produces optimized C# code using System.Runtime.Intrinsics for hardware acceleration.
/// Supports AVX2, AVX512, and ARM NEON instruction sets with automatic fallback to scalar code.
/// Generated kernels use Vector&lt;T&gt; for portability and explicit intrinsics for maximum performance.
/// </remarks>
public class CpuKernelGenerator
{
    private readonly SimdCapabilities _capabilities;
    private readonly StringBuilder _builder;
    private const int IndentSize = 4;
    private int _indentLevel;
    private int _tempVarCounter;
    private bool _isVectorContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuKernelGenerator"/> class.
    /// </summary>
    public CpuKernelGenerator()
    {
        _capabilities = DetectSimdCapabilities();
        _builder = new StringBuilder(4096);
        _indentLevel = 0;
        _tempVarCounter = 0;
    }

    /// <summary>
    /// Gets the detected SIMD capabilities for the current platform.
    /// </summary>
    internal SimdCapabilities Capabilities => _capabilities;

    /// <inheritdoc/>
    public string GenerateKernel(OperationGraph graph, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateGraph(graph);

        // Thread-safety: Lock the entire generation to prevent concurrent modification
        // of instance state (_builder, _indentLevel, _tempVarCounter)
        lock (_builder)
        {
            _builder.Clear();
            _indentLevel = 0;
            _tempVarCounter = 0;

            // Generate using statements
            EmitUsingStatements();
            _builder.AppendLine();

            // Start namespace and class
            _builder.AppendLine("namespace DotCompute.Linq.Generated;");
            _builder.AppendLine();
            _builder.AppendLine("/// <summary>");
            _builder.AppendLine("/// Generated CPU kernel with SIMD acceleration.");
            _builder.AppendLine("/// </summary>");
            _builder.AppendLine("public static class GeneratedKernel");
            _builder.AppendLine("{");
            _indentLevel++;

            // Generate method signature
            var signature = BuildMethodSignature(metadata);
            foreach (var line in signature.Split('\n'))
            {
                var trimmedLine = line.TrimEnd('\r');
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    AppendLine(trimmedLine);
                }
            }
            AppendLine("{");
            _indentLevel++;

            // Generate kernel body
            GenerateKernelBody(graph, metadata);

            _indentLevel--;
            AppendLine("}");

            // Generate array-based overload for delegate compatibility
            _builder.AppendLine();
            GenerateArrayBasedOverload(metadata);

            _indentLevel--;
            _builder.AppendLine("}");

            return _builder.ToString();
        }
    }

    /// <summary>
    /// Generates an array-based Execute overload that wraps the Span-based Execute method.
    /// This allows direct delegate creation without needing a separate wrapper class.
    /// </summary>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateArrayBasedOverload(TypeMetadata metadata)
    {
        var inputTypeName = GetTypeName(metadata.InputType);
        var outputTypeName = GetTypeName(metadata.ResultType ?? metadata.InputType);

        AppendLine("/// <summary>");
        AppendLine("/// Array-based kernel execution overload for delegate compatibility.");
        AppendLine("/// </summary>");
        AppendLine("/// <param name=\"input\">Input array.</param>");
        AppendLine("/// <returns>Output array.</returns>");
        AppendLine($"public static {outputTypeName}[] Execute({inputTypeName}[] input)");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var output = new {outputTypeName}[input.Length];");
        AppendLine("Execute(input.AsSpan(), output.AsSpan());");
        AppendLine("return output;");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Detects the SIMD capabilities available on the current platform.
    /// </summary>
    /// <returns>The highest level of SIMD support available.</returns>
    private SimdCapabilities DetectSimdCapabilities()
    {
        // Check ARM NEON first (for Apple Silicon and ARM64)
        if (AdvSimd.IsSupported)
        {
            return SimdCapabilities.NEON;
        }

        // Check x86/x64 capabilities
        if (Avx512F.IsSupported && Avx512BW.IsSupported)
        {
            return SimdCapabilities.AVX512;
        }

        if (Avx2.IsSupported)
        {
            return SimdCapabilities.AVX2;
        }

        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
        {
            return SimdCapabilities.SSE2;
        }

        // Fallback to scalar operations
        return SimdCapabilities.None;
    }

    /// <summary>
    /// Generates the complete kernel body from the operation graph.
    /// </summary>
    private void GenerateKernelBody(OperationGraph graph, TypeMetadata metadata)
    {
        // Check if we can fuse multiple operations
        var fusableOps = IdentifyFusableOperations(new List<Operation>(graph.Operations));

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
    private void GenerateOperation(Operation operation, TypeMetadata metadata)
    {
        switch (operation.Type)
        {
            case Optimization.OperationType.Map:
                GenerateMapOperation(operation, metadata);
                break;

            case Optimization.OperationType.Filter:
                GenerateFilterOperation(operation, metadata);
                break;

            case Optimization.OperationType.Aggregate:
            case Optimization.OperationType.Reduce:
                GenerateReduceOperation(operation, metadata);
                break;

            case Optimization.OperationType.Scan:
                GenerateScanOperation(operation, metadata);
                break;

            case Optimization.OperationType.Join:
            case Optimization.OperationType.GroupBy:
            case Optimization.OperationType.OrderBy:
                // These operations are complex and would require significant scaffolding
                // For now, generate a simple pass-through with a comment
                AppendLine($"// {operation.Type} operation not yet fully implemented - passing through");
                AppendLine("for (int i = 0; i < input.Length; i++)");
                AppendLine("{");
                _indentLevel++;
                AppendLine("output[i] = input[i];");
                _indentLevel--;
                AppendLine("}");
                break;

            default:
                throw new InvalidOperationException($"Unsupported operation type: {operation.Type}");
        }
    }

    /// <summary>
    /// Generates a map (Select/transformation) operation with SIMD acceleration.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateMapOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        var elementType = metadata.InputType;
        var isSimdSupported = IsSimdSupported(elementType);

        AppendLine($"// Map operation: {op.Type}");

        if (!isSimdSupported || _capabilities == SimdCapabilities.None)
        {
            GenerateScalarMap(op, metadata);
            return;
        }

        // Generate SIMD-accelerated map
        AppendLine("int i = 0;");
        AppendLine($"var vectorSize = Vector<{GetTypeName(elementType)}>.Count;");
        AppendLine();

        // Main vectorized loop
        AppendLine("// Vectorized main loop");
        AppendLine("for (; i <= input.Length - vectorSize; i += vectorSize)");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var vec = new Vector<{GetTypeName(elementType)}>(input.Slice(i));");

        // Generate the operation inline
        var lambda = GetLambda(op);
        var operationCode = EmitLambdaInline(lambda, "vec");
        AppendLine($"var result = {operationCode};");

        AppendLine("result.CopyTo(output.Slice(i));");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Remainder loop
        AppendLine("// Scalar remainder loop");
        AppendLine("for (; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        var scalarOp = EmitLambdaInline(lambda, "input[i]");
        AppendLine($"output[i] = {scalarOp};");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates a filter (Where) operation with SIMD acceleration.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateFilterOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        AppendLine($"// Filter operation: {op.Type}");

        // Filter operations with Vector<T> comparisons are complex
        // (need Vector.LessThan, Vector.GreaterThan, etc. instead of operators)
        // For now, use scalar implementation for all filter operations
        GenerateScalarFilter(op, metadata);
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

        AppendLine($"// Scan operation: {op.Type}");
        AppendLine("// Note: Scan operations are sequential by nature");

        var elementType = metadata.InputType;
        var typeName = GetTypeName(elementType);

        // Initialize accumulator
        AppendLine($"{typeName} accumulator = default;");
        AppendLine();

        // Scan loop - each output is cumulative
        AppendLine("for (int i = 0; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        var lambda = TryGetLambda(op);
        if (lambda != null)
        {
            var operation = EmitLambdaInline(lambda, "input[i]");
            AppendLine($"accumulator += {operation};");
        }
        else
        {
            AppendLine("accumulator += input[i];");
        }

        AppendLine("output[i] = accumulator;");

        _indentLevel--;
        AppendLine("}");
    }

    /// <summary>
    /// Generates a reduce (Sum/Aggregate) operation with SIMD acceleration.
    /// </summary>
    /// <param name="op">The operation to generate.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    private void GenerateReduceOperation(Operation op, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(metadata);

        var elementType = metadata.InputType;
        var isSimdSupported = IsSimdSupported(elementType);

        AppendLine($"// Reduce operation: {op.Type}");

        if (!isSimdSupported || _capabilities == SimdCapabilities.None)
        {
            GenerateScalarReduce(op, metadata);
            return;
        }

        var typeName = GetTypeName(elementType);

        // Initialize accumulator
        AppendLine($"var accumulator = Vector<{typeName}>.Zero;");
        AppendLine("int i = 0;");
        AppendLine($"var vectorSize = Vector<{typeName}>.Count;");
        AppendLine();

        // Determine if we should parallelize
        var estimatedWorkload = EstimateWorkload(op);
        var useParallel = estimatedWorkload > 10000 && metadata.InputType.IsPrimitive;

        if (useParallel)
        {
            AppendLine("// Parallel reduction for large dataset");
            GenerateParallelReduction(op, metadata);
        }
        else
        {
            // Vectorized reduction loop
            AppendLine("// Vectorized reduction loop");
            AppendLine("for (; i <= input.Length - vectorSize; i += vectorSize)");
            AppendLine("{");
            _indentLevel++;

            AppendLine($"var vec = new Vector<{typeName}>(input.Slice(i));");

            // Apply operation if needed (e.g., Select before Sum)
            var lambda = TryGetLambda(op);
            if (lambda != null)
            {
                var operationCode = EmitLambdaInline(lambda, "vec");
                AppendLine($"vec = {operationCode};");
            }

            AppendLine("accumulator += vec;");

            _indentLevel--;
            AppendLine("}");
            AppendLine();

            // Horizontal sum
            AppendLine("// Horizontal sum of vector lanes");
            AppendLine($"{typeName} result = default;");
            AppendLine("for (int lane = 0; lane < vectorSize; lane++)");
            AppendLine("{");
            _indentLevel++;
            AppendLine("result += accumulator[lane];");
            _indentLevel--;
            AppendLine("}");
            AppendLine();

            // Scalar remainder
            AppendLine("// Scalar remainder");
            AppendLine("for (; i < input.Length; i++)");
            AppendLine("{");
            _indentLevel++;

            if (lambda != null)
            {
                var scalarOp = EmitLambdaInline(lambda, "input[i]");
                AppendLine($"result += {scalarOp};");
            }
            else
            {
                AppendLine("result += input[i];");
            }

            _indentLevel--;
            AppendLine("}");
            AppendLine();

            AppendLine("// return result; // Value written to output[0]");
            // Store result in first element of output span for reduce operations
            AppendLine("output[0] = result;");
        }
    }

    /// <summary>
    /// Generates a fused operation combining multiple operations into a single kernel.
    /// </summary>
    /// <param name="ops">The list of operations to fuse.</param>
    /// <param name="metadata">Type metadata for the operations.</param>
    private void GenerateFusedOperation(List<Operation> ops, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(ops);
        ArgumentNullException.ThrowIfNull(metadata);

        if (ops.Count == 0)
        {
            throw new ArgumentException("Operation list cannot be empty.", nameof(ops));
        }

        var elementType = metadata.InputType;
        var isSimdSupported = IsSimdSupported(elementType);

        AppendLine($"// Fused operations: {string.Join(" -> ", ops.Select(o => o.Type))}");

        if (!isSimdSupported || _capabilities == SimdCapabilities.None)
        {
            GenerateScalarFused(ops, metadata);
            return;
        }

        // Check common fusion patterns
        if (IsFusableSelectWhere(ops))
        {
            GenerateFusedSelectWhere(ops, metadata);
        }
        else if (IsFusableWhereSelect(ops))
        {
            GenerateFusedWhereSelect(ops, metadata);
        }
        else
        {
            // General fusion
            GenerateGeneralFusion(ops, metadata);
        }
    }

    /// <summary>
    /// Generates a parallel loop for multi-threaded execution.
    /// </summary>
    /// <param name="body">The loop body code.</param>
    /// <param name="estimatedWorkload">Estimated number of elements to process.</param>
    private void GenerateParallelLoop(string body, int estimatedWorkload)
    {
        var optimalChunkSize = CalculateOptimalChunkSize(estimatedWorkload);

        AppendLine($"var chunkSize = Math.Max(1, input.Length / Environment.ProcessorCount);");
        AppendLine("var partitioner = Partitioner.Create(0, input.Length, chunkSize);");
        AppendLine();
        AppendLine("Parallel.ForEach(partitioner, range =>");
        AppendLine("{");
        _indentLevel++;

        // Append the body with range adjustments
        var adjustedBody = body.Replace("i < input.Length", "i < range.Item2")
                               .Replace("i = 0", "int i = range.Item1");
        _builder.Append(adjustedBody);

        _indentLevel--;
        AppendLine("});");


    }

    /// <summary>
    /// Generates the main vectorized loop structure.
    /// </summary>
    private void GenerateVectorLoop(string operation, Type elementType)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(elementType);

        var typeName = GetTypeName(elementType);

        AppendLine("int i = 0;");
        AppendLine($"var vectorSize = Vector<{typeName}>.Count;");
        AppendLine();

        AppendLine("// Main vectorized loop");
        AppendLine("for (; i <= input.Length - vectorSize; i += vectorSize)");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var vec = new Vector<{typeName}>(input.Slice(i));");
        AppendLine($"var result = {operation};");
        AppendLine("result.CopyTo(output.Slice(i));");

        _indentLevel--;
        AppendLine("}");


    }

    /// <summary>
    /// Generates scalar fallback code for types that don't support SIMD.
    /// </summary>
    private void GenerateScalarFallback(OperationGraph graph, TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(graph);

        AppendLine("// Scalar fallback (SIMD not supported for this type)");

        foreach (var op in graph.Operations)
        {
            switch (op.Type)
            {
                case Optimization.OperationType.Map:
                    GenerateScalarMap(op, metadata);
                    break;

                case Optimization.OperationType.Filter:
                    GenerateScalarFilter(op, metadata);
                    break;

                case Optimization.OperationType.Aggregate:
                case Optimization.OperationType.Reduce:
                    GenerateScalarReduce(op, metadata);
                    break;

                case Optimization.OperationType.Scan:
                    GenerateScanOperation(op, metadata);
                    break;
            }
        }


    }

    /// <summary>
    /// Builds the method signature for the generated kernel.
    /// </summary>
    private string BuildMethodSignature(TypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var inputTypeName = GetTypeName(metadata.InputType);
        var outputTypeName = GetTypeName(metadata.ResultType ?? metadata.InputType);

        var sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// SIMD-accelerated kernel execution method.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <param name=\"input\">Input data span.</param>");
        sb.AppendLine("/// <param name=\"output\">Output data span.</param>");

        // Always use concrete types for now - generic handling would need type constraints
        sb.Append($"public static void Execute(ReadOnlySpan<{inputTypeName}> input, Span<{outputTypeName}> output)");

        return sb.ToString();
    }

    /// <summary>
    /// Emits the required using statements for the generated code.
    /// </summary>
    private void EmitUsingStatements()
    {
        var usings = new[]
        {
            "using System;",
            "using System.Linq;",
            "using System.Numerics;",
            "using System.Runtime.Intrinsics;",
            "using System.Runtime.Intrinsics.X86;",
            "using System.Runtime.Intrinsics.Arm;",
            "using System.Threading.Tasks;",
            "using System.Collections.Concurrent;"
        };

        foreach (var u in usings)
        {
            _builder.AppendLine(u);
        }
    }

    /// <summary>
    /// Emits a lambda expression inline as C# code.
    /// </summary>
    /// <param name="lambda">The lambda expression to inline.</param>
    /// <param name="inputVar">The variable name to substitute for the parameter.</param>
    /// <returns>The inlined C# code.</returns>
    private string EmitLambdaInline(LambdaExpression lambda, string inputVar)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        ArgumentNullException.ThrowIfNull(inputVar);

        var body = lambda.Body;
        var parameter = lambda.Parameters[0];

        // Set vector context if the input variable looks like a vector
        var wasVectorContext = _isVectorContext;
        if (inputVar == "vec" || inputVar == "transformed" || inputVar == "mask" || inputVar.Contains("vec"))
        {
            _isVectorContext = true;
        }

        // Replace parameter references with input variable
        var visitor = new ParameterReplacementVisitor(parameter, inputVar);
        var replaced = visitor.Visit(body);

        var result = ExpressionToCode(replaced);

        // Restore context
        _isVectorContext = wasVectorContext;

        return result;
    }

    /// <summary>
    /// Optimizes code generation for specific types.
    /// </summary>
    private string OptimizeForType(Type type, string operation)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(operation);

        // Use explicit intrinsics for better performance on integer types
        if ((type == typeof(int) || type == typeof(long)) && _capabilities >= SimdCapabilities.AVX2)
        {
            return $"Avx2.{operation}";
        }

        // Use Vector<T> for portable SIMD
        if (type == typeof(float) || type == typeof(double))
        {
            return $"Vector.{operation}";
        }

        // Handle nullable types
        if (Nullable.GetUnderlyingType(type) != null)
        {
            return $"/* Nullable handling */ {operation}";
        }

        return operation;
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
            // Validate operation type is recognized (all enum values are valid)
            if (!Enum.IsDefined(typeof(Optimization.OperationType), op.Type))
            {
                throw new InvalidOperationException($"Operation graph contains invalid operation type: {op.Type}");
            }
        }

        // Verify type consistency
        for (var i = 0; i < graph.Operations.Count - 1; i++)
        {
            var current = graph.Operations[i];
            var next = graph.Operations[i + 1];

            // Output type of current should match input type of next (simplified check)
            // This would need more sophisticated type tracking in production
        }
    }

    // Helper Methods

    /// <summary>
    /// Extracts the lambda expression from an Operation.
    /// Works with both OperationNode (has Lambda property) and Operation (stores in Metadata).
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
    /// Tries to extract the lambda expression from an Operation, returning null if not found.
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

        // Fall back to extracting from Metadata (Operation)
        if (operation.Metadata.TryGetValue("Lambda", out var lambdaObj) && lambdaObj is LambdaExpression metadataLambda)
        {
            return metadataLambda;
        }

        return null;
    }

    private void AppendLine(string line = "")
    {
        if (!string.IsNullOrEmpty(line))
        {
            // Guard against negative indent level (can happen in parallel scenarios)
            var indentSpaces = Math.Max(0, _indentLevel * IndentSize);
            _builder.Append(' ', indentSpaces);
        }

        _builder.AppendLine(line);
    }

    private bool IsSimdSupported(Type type)
    {
        // SIMD is supported for primitive numeric types
        return type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(uint) ||
               type == typeof(ulong) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(byte) ||
               type == typeof(sbyte);
    }

    private string GetTypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type switch
        {
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(long) => "long",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(uint) => "uint",
            Type t when t == typeof(ulong) => "ulong",
            Type t when t == typeof(short) => "short",
            Type t when t == typeof(ushort) => "ushort",
            Type t when t == typeof(byte) => "byte",
            Type t when t == typeof(sbyte) => "sbyte",
            Type t when t == typeof(bool) => "bool",
            Type t when t == typeof(string) => "string",
            _ => type.Name
        };
    }

    private void GenerateScalarMap(Operation op, TypeMetadata metadata)
    {
        AppendLine("for (int i = 0; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        var lambda = TryGetLambda(op);
        var operation = lambda != null ? EmitLambdaInline(lambda, "input[i]") : "input[i]";
        AppendLine($"output[i] = {operation};");

        _indentLevel--;
        AppendLine("}");


    }

    private void GenerateScalarFilter(Operation op, TypeMetadata metadata)
    {
        AppendLine("int outputIndex = 0;");
        AppendLine("for (int i = 0; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        var lambda = GetLambda(op);
        var predicate = EmitLambdaInline(lambda, "input[i]");
        AppendLine($"if ({predicate})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("output[outputIndex++] = input[i];");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();
        AppendLine("// return outputIndex; // Actual count written to output");
    }

    private void GenerateScalarReduce(Operation op, TypeMetadata metadata)
    {
        var elementType = metadata.InputType;
        var typeName = GetTypeName(elementType);

        AppendLine($"{typeName} result = default;");
        AppendLine("for (int i = 0; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        var lambda = TryGetLambda(op);
        if (lambda != null)
        {
            var operation = EmitLambdaInline(lambda, "input[i]");
            AppendLine($"result += {operation};");
        }
        else
        {
            AppendLine("result += input[i];");
        }

        _indentLevel--;
        AppendLine("}");
        AppendLine();
        AppendLine("// return result; // Value written to output[0]");
        AppendLine("output[0] = result;");
    }

    private List<Operation> IdentifyFusableOperations(List<Operation> operations)
    {
        var fusable = new List<Operation>();

        for (var i = 0; i < operations.Count; i++)
        {
            fusable.Add(operations[i]);

            // Check if next operation can be fused
            if (i + 1 < operations.Count)
            {
                var current = operations[i];
                var next = operations[i + 1];

                // Can fuse Map-Filter, Filter-Map, Map-Map
                if ((current.Type == Optimization.OperationType.Map && next.Type == Optimization.OperationType.Filter) ||
                    (current.Type == Optimization.OperationType.Filter && next.Type == Optimization.OperationType.Map) ||
                    (current.Type == Optimization.OperationType.Map && next.Type == Optimization.OperationType.Map))
                {
                    continue; // Add next to fusable
                }
                else
                {
                    break; // Stop fusion
                }
            }
        }

        return fusable;
    }

    private bool IsFusableSelectWhere(List<Operation> ops)
    {
        return ops.Count == 2 && ops[0].Type == Optimization.OperationType.Map && ops[1].Type == Optimization.OperationType.Filter;
    }

    private bool IsFusableWhereSelect(List<Operation> ops)
    {
        return ops.Count == 2 && ops[0].Type == Optimization.OperationType.Filter && ops[1].Type == Optimization.OperationType.Map;
    }

    private void GenerateFusedSelectWhere(List<Operation> ops, TypeMetadata metadata)
    {
        AppendLine("int outputIndex = 0;");
        AppendLine("int i = 0;");
        AppendLine($"var vectorSize = Vector<{GetTypeName(metadata.InputType)}>.Count;");
        AppendLine();

        AppendLine("// Fused Select + Where");
        AppendLine("for (; i <= input.Length - vectorSize; i += vectorSize)");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var vec = new Vector<{GetTypeName(metadata.InputType)}>(input.Slice(i));");

        // Apply Map
        var lambda0 = GetLambda(ops[0]);
        var selectCode = EmitLambdaInline(lambda0, "vec");
        AppendLine($"var transformed = {selectCode};");

        // Apply Filter - returns Vector<T> mask (not bool)
        var lambda1 = GetLambda(ops[1]);
        var whereCode = EmitLambdaInline(lambda1, "transformed");
        AppendLine($"var mask = {whereCode};");

        // Compact results - mask contains -1 for true, 0 for false
        AppendLine("for (int j = 0; j < vectorSize; j++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("if (mask[j] != 0)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("output[outputIndex++] = transformed[j];");
        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Scalar remainder for fused Select+Where
        AppendLine("// Scalar remainder for fused Select + Where");
        AppendLine("for (; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        // Apply Map - use "input[i]" to avoid vector context detection
        var scalarSelectCode = EmitLambdaInline(lambda0, "input[i]");
        AppendLine($"var scalarValue = {scalarSelectCode};");

        // Apply Filter - use "scalarValue" (not "transformed") to avoid vector context
        var scalarWhereCode = EmitLambdaInline(lambda1, "scalarValue");
        AppendLine($"if ({scalarWhereCode})");
        AppendLine("{");
        _indentLevel++;
        AppendLine("output[outputIndex++] = scalarValue;");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
    }

    private void GenerateFusedWhereSelect(List<Operation> ops, TypeMetadata metadata)
    {
        // Filter first, then Map
        AppendLine("int outputIndex = 0;");
        AppendLine("int i = 0;");
        AppendLine($"var vectorSize = Vector<{GetTypeName(metadata.InputType)}>.Count;");
        AppendLine();

        AppendLine("// Fused Where + Select");
        AppendLine("for (; i <= input.Length - vectorSize; i += vectorSize)");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var vec = new Vector<{GetTypeName(metadata.InputType)}>(input.Slice(i));");

        // Apply Filter - returns Vector<T> mask (not bool)
        var lambda0 = GetLambda(ops[0]);
        var whereCode = EmitLambdaInline(lambda0, "vec");
        AppendLine($"var mask = {whereCode};");

        // Apply Map to filtered elements
        var lambda1 = GetLambda(ops[1]);

        // Compact results - mask contains -1 for true, 0 for false
        AppendLine("for (int j = 0; j < vectorSize; j++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("if (mask[j] != 0)");
        AppendLine("{");
        _indentLevel++;

        // Apply transformation to the individual element
        var elementSelectCode = EmitLambdaInline(lambda1, "vec[j]");
        AppendLine($"output[outputIndex++] = {elementSelectCode};");

        _indentLevel--;
        AppendLine("}");
        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
        AppendLine();

        // Scalar remainder for fused Where+Select
        AppendLine("// Scalar remainder for fused Where + Select");
        AppendLine("for (; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        // Apply Filter - "input[i]" won't trigger vector context
        var scalarWhereCode = EmitLambdaInline(lambda0, "input[i]");
        AppendLine($"if ({scalarWhereCode})");
        AppendLine("{");
        _indentLevel++;

        // Apply Map - "input[i]" won't trigger vector context
        var scalarSelectCode = EmitLambdaInline(lambda1, "input[i]");
        AppendLine($"output[outputIndex++] = {scalarSelectCode};");

        _indentLevel--;
        AppendLine("}");

        _indentLevel--;
        AppendLine("}");
    }

    private void GenerateGeneralFusion(List<Operation> ops, TypeMetadata metadata)
    {
        // General fusion strategy: apply all operations sequentially on vectorized data
        AppendLine("int i = 0;");
        AppendLine($"var vectorSize = Vector<{GetTypeName(metadata.InputType)}>.Count;");
        AppendLine();

        AppendLine("for (; i <= input.Length - vectorSize; i += vectorSize)");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var vec = new Vector<{GetTypeName(metadata.InputType)}>(input.Slice(i));");

        foreach (var op in ops)
        {
            var lambda = GetLambda(op);
            var code = EmitLambdaInline(lambda, "vec");
            AppendLine($"vec = {code};");
        }

        AppendLine("vec.CopyTo(output.Slice(i));");

        _indentLevel--;
        AppendLine("}");

        // Scalar remainder
        GenerateScalarFused(ops, metadata);
    }

    private void GenerateScalarFused(List<Operation> ops, TypeMetadata metadata)
    {
        AppendLine("// Scalar remainder for fused operations");
        AppendLine("for (; i < input.Length; i++)");
        AppendLine("{");
        _indentLevel++;

        AppendLine("var value = input[i];");

        foreach (var op in ops)
        {
            var lambda = GetLambda(op);
            var code = EmitLambdaInline(lambda, "value");
            AppendLine($"value = {code};");
        }

        AppendLine("output[i] = value;");

        _indentLevel--;
        AppendLine("}");


    }

    private void GenerateParallelReduction(Operation op, TypeMetadata metadata)
    {
        var typeName = GetTypeName(metadata.InputType);

        AppendLine($"var localSums = new ConcurrentBag<{typeName}>();");
        AppendLine();
        AppendLine("Parallel.ForEach(");
        _indentLevel++;
        AppendLine("Partitioner.Create(0, input.Length),");
        AppendLine("range =>");
        AppendLine("{");
        _indentLevel++;

        AppendLine($"var localAccumulator = Vector<{typeName}>.Zero;");
        AppendLine("int i = range.Item1;");
        AppendLine($"var vectorSize = Vector<{typeName}>.Count;");
        AppendLine();

        AppendLine("for (; i <= range.Item2 - vectorSize; i += vectorSize)");
        AppendLine("{");
        _indentLevel++;
        AppendLine($"var vec = new Vector<{typeName}>(input.Slice(i));");
        AppendLine("localAccumulator += vec;");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine($"{typeName} localSum = default;");
        AppendLine("for (int lane = 0; lane < vectorSize; lane++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("localSum += localAccumulator[lane];");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("for (; i < range.Item2; i++)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("localSum += input[i];");
        _indentLevel--;
        AppendLine("}");
        AppendLine();

        AppendLine("localSums.Add(localSum);");

        _indentLevel--;
        AppendLine("});");
        _indentLevel--;
        AppendLine();

        AppendLine($"{typeName} result = default;");
        AppendLine("foreach (var sum in localSums)");
        AppendLine("{");
        _indentLevel++;
        AppendLine("result += sum;");
        _indentLevel--;
        AppendLine("}");
        AppendLine();
        AppendLine("return result;");
    }

    private int EstimateWorkload(Operation op)
    {
        // Simple heuristic: assume metadata contains size hint
        return 10000; // Default to triggering parallelization
    }

    private int CalculateOptimalChunkSize(int totalWork)
    {
        var processorCount = Environment.ProcessorCount;
        return Math.Max(1024, totalWork / (processorCount * 4)); // 4 chunks per core
    }

    private string ExpressionToCode(Expression expr)
    {
        return expr switch
        {
            BinaryExpression binary => GenerateBinaryExpression(binary),
            UnaryExpression unary when unary.NodeType == ExpressionType.Convert => GenerateConvertExpression(unary),
            UnaryExpression unary => $"{GetOperator(unary.NodeType)}({ExpressionToCode(unary.Operand)})",
            ConstantExpression constant => FormatConstant(constant),
            MemberExpression member => $"{ExpressionToCode(member.Expression!)}.{member.Member.Name}",
            MethodCallExpression method => GenerateMethodCall(method),
            ParameterExpression parameter => parameter.Name ?? $"param{_tempVarCounter++}",
            _ => $"/* Unsupported: {expr.NodeType} */"
        };
    }

    /// <summary>
    /// Generates code for a Convert/Cast expression.
    /// </summary>
    /// <param name="unary">The unary convert expression.</param>
    /// <returns>The generated code with appropriate cast.</returns>
    private string GenerateConvertExpression(UnaryExpression unary)
    {
        var operandCode = ExpressionToCode(unary.Operand);
        var targetType = GetTypeName(unary.Type);

        // In vector context, we can't cast Vector<T> directly
        // The cast should be applied before vectorization
        if (_isVectorContext)
        {
            // For vector operations, the cast is implicit in the Vector<T> type
            // Just return the operand code
            return operandCode;
        }

        // In scalar context, emit explicit cast for small integer types
        // to avoid issues with integer promotion
        if (unary.Type == typeof(byte) ||
            unary.Type == typeof(sbyte) ||
            unary.Type == typeof(short) ||
            unary.Type == typeof(ushort))
        {
            return $"({targetType})({operandCode})";
        }

        // For other types, cast only if necessary
        return operandCode;
    }

    private string GenerateBinaryExpression(BinaryExpression binary)
    {
        // Check if we're in vector context BEFORE generating code
        var isVectorOp = false;
        Type? elementType = null;
        string? right = null;

        // Check for parameter on left side, potentially wrapped in a Convert expression
        // The C# compiler may add implicit conversions for byte/short operations
        ParameterExpression? param = binary.Left as ParameterExpression;
        if (param == null && binary.Left is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            param = unary.Operand as ParameterExpression;
        }

        // For vector operations with constants, wrap constants in Vector<T>
        // If we're in vector context OR if the left parameter name suggests vectorization
        if (param != null && binary.Right is ConstantExpression constant)
        {
            // Check if parameter name suggests it's a vector variable
            var isVectorVar = param.Name == "vec" || param.Name == "transformed" || param.Name == "mask" ||
                             (param.Name?.Contains("vec") == true);

            if (_isVectorContext || isVectorVar)
            {
                isVectorOp = true;
                // In vector context, the parameter represents a vector even if its type is the element type
                // Get the element type from the parameter type (which should be the scalar type)
                elementType = param.Type;
                var typeName = GetTypeName(elementType);

                // Format constant with appropriate cast for the target element type
                var constantValue = constant.Value?.ToString() ?? "0";

                // For byte/sbyte operations, ensure proper casting to avoid int promotion issues
                if (elementType == typeof(byte))
                {
                    constantValue = $"(byte){constantValue}";
                }
                else if (elementType == typeof(sbyte))
                {
                    constantValue = $"(sbyte){constantValue}";
                }
                else if (elementType == typeof(short))
                {
                    constantValue = $"(short){constantValue}";
                }
                else if (elementType == typeof(ushort))
                {
                    constantValue = $"(ushort){constantValue}";
                }
                else
                {
                    constantValue = FormatConstant(constant);
                }

                right = $"new Vector<{typeName}>({constantValue})";
            }
        }

        // Generate left side and operator
        var left = ExpressionToCode(binary.Left);
        var op = GetOperator(binary.NodeType);

        // Generate right side if not already handled
        if (right == null)
        {
            right = ExpressionToCode(binary.Right);
        }

        // For vector comparison operations, use Vector.GreaterThan, Vector.LessThan, etc.
        if (isVectorOp && IsComparisonOperator(binary.NodeType))
        {
            var typeName = GetTypeName(elementType!);
            return binary.NodeType switch
            {
                ExpressionType.GreaterThan => $"Vector.GreaterThan({left}, {right})",
                ExpressionType.GreaterThanOrEqual => $"Vector.GreaterThanOrEqual({left}, {right})",
                ExpressionType.LessThan => $"Vector.LessThan({left}, {right})",
                ExpressionType.LessThanOrEqual => $"Vector.LessThanOrEqual({left}, {right})",
                ExpressionType.Equal => $"Vector.Equals({left}, {right})",
                ExpressionType.NotEqual => $"~Vector.Equals({left}, {right})",
                _ => $"({left} {op} {right})"
            };
        }

        return $"({left} {op} {right})";
    }

    private bool IsComparisonOperator(ExpressionType type)
    {
        return type == ExpressionType.GreaterThan ||
               type == ExpressionType.GreaterThanOrEqual ||
               type == ExpressionType.LessThan ||
               type == ExpressionType.LessThanOrEqual ||
               type == ExpressionType.Equal ||
               type == ExpressionType.NotEqual;
    }

    private string FormatConstant(ConstantExpression constant)
    {
        if (constant.Value == null)
        {
            return "null";
        }

        // Add type suffixes for proper type inference in generated code
        return constant.Type switch
        {
            Type t when t == typeof(float) => $"{constant.Value}f",
            Type t when t == typeof(double) => $"{constant.Value}d",
            Type t when t == typeof(long) => $"{constant.Value}L",
            Type t when t == typeof(ulong) => $"{constant.Value}UL",
            Type t when t == typeof(uint) => $"{constant.Value}u",
            Type t when t == typeof(byte) => $"(byte){constant.Value}",
            Type t when t == typeof(sbyte) => $"(sbyte){constant.Value}",
            Type t when t == typeof(short) => $"(short){constant.Value}",
            Type t when t == typeof(ushort) => $"(ushort){constant.Value}",
            _ => constant.Value.ToString() ?? "null"
        };
    }

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
            _ => $"/* Op: {type} */"
        };
    }

    private string GenerateMethodCall(MethodCallExpression method)
    {
        var args = string.Join(", ", method.Arguments.Select(ExpressionToCode));

        if (method.Object != null)
        {
            return $"{ExpressionToCode(method.Object)}.{method.Method.Name}({args})";
        }

        return $"{method.Method.DeclaringType?.Name}.{method.Method.Name}({args})";
    }

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
                // Return a new parameter with the replacement name
                return Expression.Parameter(node.Type, _replacement);
            }

            return base.VisitParameter(node);
        }
    }
}

/// <summary>
/// Represents the SIMD capabilities available on the platform.
/// </summary>
internal enum SimdCapabilities
{
    /// <summary>No SIMD support (scalar operations only).</summary>
    None = 0,

    /// <summary>SSE2 support (128-bit vectors).</summary>
    SSE2 = 1,

    /// <summary>AVX2 support (256-bit vectors).</summary>
    AVX2 = 2,

    /// <summary>AVX-512 support (512-bit vectors).</summary>
    AVX512 = 3,

    /// <summary>ARM NEON support (128-bit vectors).</summary>
    NEON = 4
}
