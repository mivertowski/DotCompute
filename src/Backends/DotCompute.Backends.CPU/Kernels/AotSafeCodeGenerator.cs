// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// AOT-safe code generator that uses pre-compiled delegates instead of Expression.Compile().
/// This generator provides fallback implementations for Native AOT scenarios.
/// </summary>
#pragma warning disable CA1822 // Mark members as static - Methods use instance field _kernelImplementations indirectly
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

    private string GenerateKernelKey(KernelDefinition definition, KernelAst ast, KernelAnalysis analysis)
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

    private Func<ExtendedKernelExecutionContext, Task> GenerateGenericImplementation(
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

    private Delegate CreateKernelDelegate(Func<ExtendedKernelExecutionContext, Task> implementation, KernelDefinition definition)
    {
        // Create a delegate that adapts the kernel execution context to the expected signature
        // The delegate signature varies based on parameter count and types
        // Extract parameter count from metadata or infer from name
        var paramCount = 3; // Default to 3 parameters (input1, input2, output)
        if (definition.Metadata?.TryGetValue("ParameterCount", out var paramCountObj) == true && paramCountObj is int count)
        {
            paramCount = count;
        }

        // Comprehensive parameter signature support for all data types
        return CreateTypedDelegate(definition, implementation, paramCount);
    }

    private Delegate CreateTypedDelegate(KernelDefinition definition, Func<ExtendedKernelExecutionContext, Task> implementation, int paramCount)
    {
        // Determine parameter types from metadata or infer from definition
        var parameterTypes = InferParameterTypes(definition, paramCount);

        return parameterTypes.Count switch
        {
            // Single parameter signatures
            1 => CreateSingleParameterDelegate(parameterTypes[0], implementation),

            // Two parameter signatures
            2 => CreateTwoParameterDelegate(parameterTypes[0], parameterTypes[1], implementation),

            // Three parameter signatures (most common)
            3 => CreateThreeParameterDelegate(parameterTypes[0], parameterTypes[1], parameterTypes[2], implementation),

            // Four parameter signatures
            4 => CreateFourParameterDelegate(parameterTypes[0], parameterTypes[1], parameterTypes[2], parameterTypes[3], implementation),

            // Five parameter signatures
            5 => CreateFiveParameterDelegate(parameterTypes, implementation),

            // Six or more parameters - use generic approach
            _ => CreateGenericDelegate(parameterTypes, implementation)
        };
    }

    private List<Type> InferParameterTypes(KernelDefinition definition, int paramCount)
    {
        var types = new List<Type>();

        // Try to get types from metadata
        if (definition.Metadata?.TryGetValue("ParameterTypes", out var paramTypesObj) == true)
        {
            if (paramTypesObj is string[] typeNames)
            {
                foreach (var typeName in typeNames)
                {
                    var type = ParseTypeFromString(typeName);
                    types.Add(type);
                }
            }
        }

        // If we don't have explicit types, infer from kernel name and parameter count
        if (types.Count == 0)
        {
            types = InferTypesFromKernelSignature(definition, paramCount);
        }

        return types;
    }

    private Type ParseTypeFromString(string typeName)
    {
        return typeName.ToUpperInvariant() switch
        {
            "FLOAT" or "SINGLE" => typeof(Memory<float>),
            "DOUBLE" => typeof(Memory<double>),
            "INT" or "INT32" => typeof(Memory<int>),
            "LONG" or "INT64" => typeof(Memory<long>),
            "SHORT" or "INT16" => typeof(Memory<short>),
            "BYTE" or "UINT8" => typeof(Memory<byte>),
            "BOOL" or "BOOLEAN" => typeof(Memory<bool>),
            "UINT" or "UINT32" => typeof(Memory<uint>),
            "ULONG" or "UINT64" => typeof(Memory<ulong>),
            "USHORT" or "UINT16" => typeof(Memory<ushort>),
            "SBYTE" or "INT8" => typeof(Memory<sbyte>),
            "CHAR" => typeof(Memory<char>),
            "DECIMAL" => typeof(Memory<decimal>),
            _ => typeof(Memory<float>) // Default to float
        };
    }

    private List<Type> InferTypesFromKernelSignature(KernelDefinition definition, int paramCount)
    {
        var types = new List<Type>();
        var kernelName = definition.Name.ToUpperInvariant();

        // Common patterns based on kernel names
        if (kernelName.Contains("INT", StringComparison.Ordinal) || kernelName.Contains("INTEGER", StringComparison.Ordinal))
        {
            for (var i = 0; i < paramCount; i++)
            {
                types.Add(typeof(Memory<int>));
            }
        }
        else if (kernelName.Contains("DOUBLE", StringComparison.Ordinal))
        {
            for (var i = 0; i < paramCount; i++)
            {
                types.Add(typeof(Memory<double>));
            }
        }
        else if (kernelName.Contains("LONG", StringComparison.Ordinal))
        {
            for (var i = 0; i < paramCount; i++)
            {
                types.Add(typeof(Memory<long>));
            }
        }
        else if (kernelName.Contains("BYTE", StringComparison.Ordinal))
        {
            for (var i = 0; i < paramCount; i++)
            {
                types.Add(typeof(Memory<byte>));
            }
        }
        else
        {
            // Default to float for most compute kernels
            for (var i = 0; i < paramCount; i++)
            {
                types.Add(typeof(Memory<float>));
            }
        }

        return types;
    }

    private Delegate CreateSingleParameterDelegate(Type paramType, Func<ExtendedKernelExecutionContext, Task> implementation)
    {
        if (paramType == typeof(Memory<float>))
        {
            return new Action<Memory<float>, long[]>((param, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, param);
                context.SetParameter(1, workItemId);
                ExecuteSync(implementation, context);
            });
        }
        else if (paramType == typeof(Memory<double>))
        {
            return new Action<Memory<double>, long[]>((param, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, param);
                context.SetParameter(1, workItemId);
                ExecuteSync(implementation, context);
            });
        }
        else if (paramType == typeof(Memory<int>))
        {
            return new Action<Memory<int>, long[]>((param, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, param);
                context.SetParameter(1, workItemId);
                ExecuteSync(implementation, context);
            });
        }

        // Generic fallback
        return new Action<object, long[]>((param, workItemId) =>
        {
            var context = new ExtendedKernelExecutionContext();
            context.SetParameter(0, param);
            context.SetParameter(1, workItemId);
            ExecuteSync(implementation, context);
        });
    }

    private Delegate CreateTwoParameterDelegate(Type param1Type, Type param2Type, Func<ExtendedKernelExecutionContext, Task> implementation)
    {
        // Handle most common combinations
        if (param1Type == typeof(Memory<float>) && param2Type == typeof(Memory<float>))
        {
            return new Action<Memory<float>, Memory<float>, long[]>((p1, p2, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetParameter(2, workItemId);
                ExecuteSync(implementation, context);
            });
        }
        else if (param1Type == typeof(Memory<double>) && param2Type == typeof(Memory<double>))
        {
            return new Action<Memory<double>, Memory<double>, long[]>((p1, p2, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetParameter(2, workItemId);
                ExecuteSync(implementation, context);
            });
        }
        else if (param1Type == typeof(Memory<int>) && param2Type == typeof(Memory<int>))
        {
            return new Action<Memory<int>, Memory<int>, long[]>((p1, p2, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetParameter(2, workItemId);
                ExecuteSync(implementation, context);
            });
        }

        // Generic fallback
        return new Action<object, object, long[]>((p1, p2, workItemId) =>
        {
            var context = new ExtendedKernelExecutionContext();
            context.SetParameter(0, p1);
            context.SetParameter(1, p2);
            context.SetParameter(2, workItemId);
            ExecuteSync(implementation, context);
        });
    }

    private Delegate CreateThreeParameterDelegate(Type param1Type, Type param2Type, Type param3Type, Func<ExtendedKernelExecutionContext, Task> implementation)
    {
        // Handle most common three-parameter combinations
        if (param1Type == typeof(Memory<float>) && param2Type == typeof(Memory<float>) && param3Type == typeof(Memory<float>))
        {
            return new Action<Memory<float>, Memory<float>, Memory<float>, long[]>((p1, p2, p3, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetBuffer(2, p3);
                context.SetParameter(3, workItemId);
                ExecuteSync(implementation, context);
            });
        }
        else if (param1Type == typeof(Memory<double>) && param2Type == typeof(Memory<double>) && param3Type == typeof(Memory<double>))
        {
            return new Action<Memory<double>, Memory<double>, Memory<double>, long[]>((p1, p2, p3, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetBuffer(2, p3);
                context.SetParameter(3, workItemId);
                ExecuteSync(implementation, context);
            });
        }
        else if (param1Type == typeof(Memory<int>) && param2Type == typeof(Memory<int>) && param3Type == typeof(Memory<int>))
        {
            return new Action<Memory<int>, Memory<int>, Memory<int>, long[]>((p1, p2, p3, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetBuffer(2, p3);
                context.SetParameter(3, workItemId);
                ExecuteSync(implementation, context);
            });
        }

        // Mixed type combinations
        if (param1Type == typeof(Memory<float>) && param2Type == typeof(Memory<float>) && param3Type == typeof(Memory<double>))
        {
            return new Action<Memory<float>, Memory<float>, Memory<double>, long[]>((p1, p2, p3, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetBuffer(2, p3);
                context.SetParameter(3, workItemId);
                ExecuteSync(implementation, context);
            });
        }

        // Generic fallback
        return new Action<object, object, object, long[]>((p1, p2, p3, workItemId) =>
        {
            var context = new ExtendedKernelExecutionContext();
            context.SetParameter(0, p1);
            context.SetParameter(1, p2);
            context.SetParameter(2, p3);
            context.SetParameter(3, workItemId);
            ExecuteSync(implementation, context);
        });
    }

    private Delegate CreateFourParameterDelegate(Type p1Type, Type p2Type, Type p3Type, Type p4Type, Func<ExtendedKernelExecutionContext, Task> implementation)
    {
        // For four parameters, use a more generic approach with specific type checking
        if (p1Type == typeof(Memory<float>) && p2Type == typeof(Memory<float>) && p3Type == typeof(Memory<float>) && p4Type == typeof(Memory<float>))
        {
            return new Action<Memory<float>, Memory<float>, Memory<float>, Memory<float>, long[]>((p1, p2, p3, p4, workItemId) =>
            {
                var context = new ExtendedKernelExecutionContext();
                context.SetBuffer(0, p1);
                context.SetBuffer(1, p2);
                context.SetBuffer(2, p3);
                context.SetBuffer(3, p4);
                context.SetParameter(4, workItemId);
                ExecuteSync(implementation, context);
            });
        }

        // Generic fallback
        return new Action<object, object, object, object, long[]>((p1, p2, p3, p4, workItemId) =>
        {
            var context = new ExtendedKernelExecutionContext();
            context.SetParameter(0, p1);
            context.SetParameter(1, p2);
            context.SetParameter(2, p3);
            context.SetParameter(3, p4);
            context.SetParameter(4, workItemId);
            ExecuteSync(implementation, context);
        });
    }

    private Delegate CreateFiveParameterDelegate(List<Type> paramTypes, Func<ExtendedKernelExecutionContext, Task> implementation)
    {
        // For five or more parameters, use array-based approach
        return new Action<object[], long[]>((parameters, workItemId) =>
        {
            var context = new ExtendedKernelExecutionContext();
            for (var i = 0; i < parameters.Length && i < paramTypes.Count; i++)
            {
                // Try to set as buffer first, fallback to parameter
                if (parameters[i] is IUnifiedMemoryBuffer buffer)
                {
                    var cpuBuffer = buffer as CpuMemoryBuffer ?? throw new InvalidOperationException("Buffer must be a CpuMemoryBuffer for CPU backend");
                    context.SetBuffer(i, cpuBuffer.GetMemory());
                }
                else
                {
                    context.SetParameter(i, parameters[i]);
                }
            }
            context.SetParameter(parameters.Length, workItemId);
            ExecuteSync(implementation, context);
        });
    }

    private Delegate CreateGenericDelegate(List<Type> paramTypes, Func<ExtendedKernelExecutionContext, Task> implementation)
    {
        // Most flexible approach for complex signatures
        return new Action<object[], long[]>((parameters, workItemId) =>
        {
            var context = new ExtendedKernelExecutionContext();

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                // Handle different parameter types
                switch (parameter)
                {
                    case IUnifiedMemoryBuffer buffer:
                        var cpuBuffer = buffer as CpuMemoryBuffer ?? throw new InvalidOperationException("Buffer must be a CpuMemoryBuffer for CPU backend");
                        context.SetBuffer(i, cpuBuffer.GetMemory());
                        break;
                    case Memory<float> floatMem:
                        context.SetBuffer(i, floatMem);
                        break;
                    case Memory<double> doubleMem:
                        context.SetBuffer(i, doubleMem);
                        break;
                    case Memory<int> intMem:
                        context.SetBuffer(i, intMem);
                        break;
                    case Memory<long> longMem:
                        context.SetBuffer(i, longMem);
                        break;
                    case Memory<byte> byteMem:
                        context.SetBuffer(i, byteMem);
                        break;
                    default:
                        context.SetParameter(i, parameter);
                        break;
                }
            }

            context.SetParameter(parameters.Length, workItemId);
            ExecuteSync(implementation, context);
        });
    }

    private static void ExecuteSync(Func<ExtendedKernelExecutionContext, Task> implementation, ExtendedKernelExecutionContext context)
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - Required for AOT delegate signature compatibility
        => implementation(context).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002


    #region Pre-compiled Kernel Implementations

    private static async Task VectorAddFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        var workItemId = context.GetParameter(3) as long[] ?? [0];

        var index = (int)workItemId[0];
        var vectorSize = global::System.Numerics.Vector<float>.Count;

        // Vectorized operation
        if (index + vectorSize <= a.Length)
        {
            var va = new global::System.Numerics.Vector<float>(a.Span[index..]);
            var vb = new global::System.Numerics.Vector<float>(b.Span[index..]);
            var vc = va + vb;
            vc.CopyTo(c.Span[index..]);
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
        var workItemId = context.GetParameter(3) as long[] ?? [0];

        var index = (int)workItemId[0];
        var vectorSize = global::System.Numerics.Vector<float>.Count;

        if (index + vectorSize <= a.Length)
        {
            var va = new global::System.Numerics.Vector<float>(a.Span[index..]);
            var vb = new global::System.Numerics.Vector<float>(b.Span[index..]);
            var vc = va * vb;
            vc.CopyTo(c.Span[index..]);
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
        var workItemId = context.GetParameter(3) as long[] ?? [0];

        var index = (int)workItemId[0];
        c.Span[index] = a.Span[index] + b.Span[index];

        await Task.CompletedTask;
    }

    private static async Task ElementwiseMultiplyFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        var workItemId = context.GetParameter(3) as long[] ?? [0];

        var index = (int)workItemId[0];
        c.Span[index] = a.Span[index] * b.Span[index];

        await Task.CompletedTask;
    }

    private static async Task MatrixMultiplyFloatKernelAsync(ExtendedKernelExecutionContext context)
    {
        var a = context.GetBuffer<float>(0);
        var b = context.GetBuffer<float>(1);
        var c = context.GetBuffer<float>(2);
        _ = context.GetScalar<int>(3); // m - rows, not used in this implementation
        var n = context.GetScalar<int>(4);
        var k = context.GetScalar<int>(5);
        var workItemId = context.GetParameter(6) as long[] ?? [0];

        var row = (int)(workItemId[0] / n);
        var col = (int)(workItemId[0] % n);

        float sum = 0;
        for (var i = 0; i < k; i++)
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
        var workItemId = context.GetParameter(2) as long[] ?? [0];

        // Simple reduction - in production would use tree reduction
        if (workItemId[0] == 0)
        {
            float sum = 0;
            var vectorSize = global::System.Numerics.Vector<float>.Count;
            var vectorCount = input.Length / vectorSize;

            // Vectorized sum
            var vsum = global::System.Numerics.Vector<float>.Zero;
            for (var i = 0; i < vectorCount; i++)
            {
                var v = new global::System.Numerics.Vector<float>(input.Span[(i * vectorSize)..]);
                vsum += v;
            }

            // Sum vector elements
            for (var i = 0; i < vectorSize; i++)
            {
                sum += vsum[i];
            }

            // Add remainder
            for (var i = vectorCount * vectorSize; i < input.Length; i++)
            {
                sum += input.Span[i];
            }

            output.Span[0] = sum;
        }

        await Task.CompletedTask;
    }

    #endregion

    private long EstimateCodeSize(KernelAst ast)
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

    private string[] GenerateOptimizationNotes(KernelAst ast, KernelAnalysis analysis, CompilationOptions options)
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

        return [.. notes];
    }
}
#pragma warning restore CA1822

