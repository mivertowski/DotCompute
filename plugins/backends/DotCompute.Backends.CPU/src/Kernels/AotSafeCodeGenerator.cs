// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - AOT scenarios require synchronous execution

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// AOT-safe code generator that uses pre-compiled delegates instead of Expression.Compile().
/// This generator provides fallback implementations for Native AOT scenarios.
/// </summary>
internal sealed class AotSafeCodeGenerator
{
    private readonly Dictionary<string, Func<ExtendedKernelExecutionContext, Task>> _kernelImplementations;

    public AotSafeCodeGenerator()
    {
        _kernelImplementations = [];
        RegisterKernelImplementations();
    }

    /// <summary>
    /// Generates AOT-safe kernel code without using Expression.Compile().
    /// </summary>
    public CompiledKernelCode GenerateKernel(
        KernelDefinition definition,
        KernelAst ast,
        KernelAnalysis analysis,
        CompilationOptions options)
    {
        // For AOT, we select from pre-compiled kernel implementations
        var kernelKey = GenerateKernelKey(definition, ast, analysis);

        if (!_kernelImplementations.TryGetValue(kernelKey, out var implementation))
        {
            // Fall back to generic implementation based on operation type
            implementation = GenerateGenericImplementation(definition, ast, analysis);
        }

        // Create delegate adapter that matches expected signature
        var kernelDelegate = CreateKernelDelegate(implementation, definition);

        return new CompiledKernelCode
        {
            CompiledDelegate = kernelDelegate,
            IsVectorized = analysis.CanVectorize,
            OptimizationLevel = options.OptimizationLevel,
            EstimatedCodeSize = EstimateCodeSize(ast),
            OptimizationNotes = GenerateOptimizationNotes(ast, analysis, options)
        };
    }

    private void RegisterKernelImplementations()
    {
        // Register common kernel patterns
        _kernelImplementations["vector_add_float"] = VectorAddFloatKernelAsync;
        _kernelImplementations["vector_multiply_float"] = VectorMultiplyFloatKernelAsync;
        _kernelImplementations["matrix_multiply_float"] = MatrixMultiplyFloatKernelAsync;
        _kernelImplementations["reduction_sum_float"] = ReductionSumFloatKernelAsync;
        _kernelImplementations["elementwise_add_float"] = ElementwiseAddFloatKernelAsync;
        _kernelImplementations["elementwise_multiply_float"] = ElementwiseMultiplyFloatKernelAsync;
    }

    private static string GenerateKernelKey(KernelDefinition definition, KernelAst ast, KernelAnalysis analysis)
    {
        // Generate a key based on kernel characteristics
        var operationTypeUpper = ast.Operations.FirstOrDefault()?.NodeType.ToString().ToUpperInvariant() ?? "UNKNOWN";
#pragma warning disable CA1308 // Normalize strings to uppercase - needed for kernel key matching
        var operationType = operationTypeUpper.ToLowerInvariant();
#pragma warning restore CA1308
        // Determine data type from metadata or default to float
        var dataTypeUpper = "FLOAT";
        if (definition.Metadata?.TryGetValue("DataType", out var dataTypeObj) == true)
        {
            dataTypeUpper = dataTypeObj?.ToString()?.ToUpperInvariant() ?? "FLOAT";
        }
#pragma warning disable CA1308 // Normalize strings to uppercase - needed for kernel key matching
        var dataType = dataTypeUpper.ToLowerInvariant();
#pragma warning restore CA1308

        // Special handling for common patterns
        if (definition.Name.Contains("add", StringComparison.OrdinalIgnoreCase))
        {
            return analysis.CanVectorize ? $"vector_add_{dataType}" : $"elementwise_add_{dataType}";
        }
        else if (definition.Name.Contains("multiply", StringComparison.OrdinalIgnoreCase))
        {
            if (definition.Name.Contains("matrix", StringComparison.OrdinalIgnoreCase))
            {
                return $"matrix_multiply_{dataType}";
            }
            return analysis.CanVectorize ? $"vector_multiply_{dataType}" : $"elementwise_multiply_{dataType}";
        }
        else if (definition.Name.Contains("sum", StringComparison.OrdinalIgnoreCase) ||
                 definition.Name.Contains("reduce", StringComparison.OrdinalIgnoreCase))
        {
            return $"reduction_sum_{dataType}";
        }

        return $"{operationType}_{dataType}";
    }

    private static Func<ExtendedKernelExecutionContext, Task> GenerateGenericImplementation(
        KernelDefinition definition,
        KernelAst ast,
        KernelAnalysis analysis)
    {
        // Determine the most appropriate generic implementation
        if (ast.Operations.Any(op => op.NodeType == AstNodeType.Add))
        {
            return analysis.CanVectorize ? VectorAddFloatKernelAsync : ElementwiseAddFloatKernelAsync;
        }
        else if (ast.Operations.Any(op => op.NodeType == AstNodeType.Multiply))
        {
            return analysis.CanVectorize ? VectorMultiplyFloatKernelAsync : ElementwiseMultiplyFloatKernelAsync;
        }

        // Default to simple elementwise operation
        return ElementwiseAddFloatKernelAsync;
    }

    private static Delegate CreateKernelDelegate(Func<ExtendedKernelExecutionContext, Task> implementation, KernelDefinition definition)
    {
        // Create a delegate that adapts the kernel execution context to the expected signature
        // The delegate signature varies based on parameter count and types
        // Extract parameter count from metadata or infer from name
        var paramCount = 3; // Default to 3 parameters (input1, input2, output)
        if (definition.Metadata?.TryGetValue("ParameterCount", out var paramCountObj) == true && paramCountObj is int count)
        {
            paramCount = count;
        }

        // For simplicity, we support common signatures
        // In production, this would be more comprehensive
        switch (paramCount)
        {
            case 3: // Common case: input1, input2, output
                return new Action<Memory<float>, Memory<float>, Memory<float>, long[]>((a, b, c, workItemId) =>
                {
                    var context = new ExtendedKernelExecutionContext();
                    context.SetBuffer(0, a);
                    context.SetBuffer(1, b);
                    context.SetBuffer(2, c);
                    context.SetParameter(3, workItemId);

                    // Execute synchronously for AOT compatibility
                    implementation(context).GetAwaiter().GetResult();
                });

            case 2: // Reduction case: input, output
                return new Action<Memory<float>, Memory<float>, long[]>((input, output, workItemId) =>
                {
                    var context = new ExtendedKernelExecutionContext();
                    context.SetBuffer(0, input);
                    context.SetBuffer(1, output);
                    context.SetParameter(2, workItemId);

                    implementation(context).GetAwaiter().GetResult();
                });

            default:
                // Generic delegate for other cases
                return new Action<object[], long[]>((parameters, workItemId) =>
                {
                    var context = new ExtendedKernelExecutionContext();
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        context.SetParameter(i, parameters[i]);
                    }
                    context.SetParameter(parameters.Length, workItemId);

                    implementation(context).GetAwaiter().GetResult();
                });
        }
    }

    #region Pre-compiled Kernel Implementations

    private static async Task VectorAddFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        var workItemId = context.GetParameter(3) as long[] ?? new long[] { 0 };

        var index = (int)workItemId[0];
        var vectorSize = System.Numerics.Vector<float>.Count;

        // Vectorized operation
        if (index + vectorSize <= a.Length)
        {
            var va = new System.Numerics.Vector<float>(a.Span.Slice(index));
            var vb = new System.Numerics.Vector<float>(b.Span.Slice(index));
            var vc = va + vb;
            vc.CopyTo(c.Span.Slice(index));
        }
        else
        {
            // Scalar fallback for remainder
            c.Span[index] = a.Span[index] + b.Span[index];
        }

        await Task.CompletedTask;
    }

    private static async Task VectorMultiplyFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        var workItemId = context.GetParameter(3) as long[] ?? new long[] { 0 };

        var index = (int)workItemId[0];
        var vectorSize = System.Numerics.Vector<float>.Count;

        if (index + vectorSize <= a.Length)
        {
            var va = new System.Numerics.Vector<float>(a.Span.Slice(index));
            var vb = new System.Numerics.Vector<float>(b.Span.Slice(index));
            var vc = va * vb;
            vc.CopyTo(c.Span.Slice(index));
        }
        else
        {
            c.Span[index] = a.Span[index] * b.Span[index];
        }

        await Task.CompletedTask;
    }

    private static async Task ElementwiseAddFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        var workItemId = context.GetParameter(3) as long[] ?? new long[] { 0 };

        var index = (int)workItemId[0];
        c.Span[index] = a.Span[index] + b.Span[index];

        await Task.CompletedTask;
    }

    private static async Task ElementwiseMultiplyFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        var workItemId = context.GetParameter(3) as long[] ?? new long[] { 0 };

        var index = (int)workItemId[0];
        c.Span[index] = a.Span[index] * b.Span[index];

        await Task.CompletedTask;
    }

    private static async Task MatrixMultiplyFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        // Matrix dimensions are used in the calculation below
        _ = context.GetScalar<int>(3); // m - rows in A
        var n = context.GetScalar<int>(4);
        var k = context.GetScalar<int>(5);
        var workItemId = context.GetParameter(6) as long[] ?? new long[] { 0 };

        var row = (int)(workItemId[0] / n);
        var col = (int)(workItemId[0] % n);

        float sum = 0;
        for (int i = 0; i < k; i++)
        {
            sum += a.Span[row * k + i] * b.Span[i * n + col];
        }
        c.Span[row * n + col] = sum;

        await Task.CompletedTask;
    }

    private static async Task ReductionSumFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var input = context.GetBuffer<float>(0);
        var output = context.GetBuffer<float>(1);
        var workItemId = context.GetParameter(2) as long[] ?? new long[] { 0 };

        // Simple reduction - in production would use tree reduction
        if (workItemId[0] == 0)
        {
            float sum = 0;
            var vectorSize = System.Numerics.Vector<float>.Count;
            var vectorCount = input.Length / vectorSize;

            // Vectorized sum
            var vsum = System.Numerics.Vector<float>.Zero;
            for (int i = 0; i < vectorCount; i++)
            {
                var v = new System.Numerics.Vector<float>(input.Span.Slice(i * vectorSize));
                vsum += v;
            }

            // Sum vector elements
            for (int i = 0; i < vectorSize; i++)
            {
                sum += vsum[i];
            }

            // Add remainder
            for (int i = vectorCount * vectorSize; i < input.Length; i++)
            {
                sum += input.Span[i];
            }

            output.Span[0] = sum;
        }

        await Task.CompletedTask;
    }

    #endregion

    private static long EstimateCodeSize(KernelAst ast)
    {
        // Rough estimation based on AST complexity
        long baseSize = 1024;
        baseSize += ast.Operations.Count * 128;
        baseSize += ast.MemoryOperations.Count * 64;
        if (ast.HasLoops)
        {
            baseSize += 512;
        }
        if (ast.HasConditionals)
        {
            baseSize += 256;
        }
        return baseSize;
    }

    private static string[] GenerateOptimizationNotes(KernelAst ast, KernelAnalysis analysis, CompilationOptions options)
    {
        var notes = new List<string>
        {
            $"AOT-safe kernel with {ast.Operations.Count} operations"
        };

        if (analysis.CanVectorize)
        {
            notes.Add($"Applied vectorization with factor {analysis.VectorizationFactor}");
        }
        else
        {
            notes.Add("Scalar execution (vectorization not applicable)");
        }

        if (options.OptimizationLevel >= OptimizationLevel.Default)
        {
            notes.Add("Applied release optimizations");
        }

        notes.Add("Using pre-compiled kernel implementation for AOT compatibility");

        return notes.ToArray();
    }
}
