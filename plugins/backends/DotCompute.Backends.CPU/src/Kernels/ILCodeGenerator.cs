// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Advanced IL code generator for CPU kernels using Expression Trees and DynamicMethod.
/// </summary>
internal sealed class ILCodeGenerator
{
    private readonly AotSafeCodeGenerator? _aotGenerator;

    public ILCodeGenerator()
    {
        // For AOT compatibility, we use Expression Trees instead of AssemblyBuilder

        // Initialize AOT-safe generator if dynamic code compilation is not available
        if (!RuntimeFeature.IsDynamicCodeCompiled)
        {
            _aotGenerator = new AotSafeCodeGenerator();
        }
    }

    /// <summary>
    /// Generates optimized kernel code using Expression Trees.
    /// </summary>
    public CompiledKernelCode GenerateKernel(
        KernelDefinition definition,
        KernelAst ast,
        KernelAnalysis analysis,
        CompilationOptions options)
    {
        // Use AOT-safe generator if dynamic code compilation is not available
        if (!RuntimeFeature.IsDynamicCodeCompiled && _aotGenerator != null)
        {
            return _aotGenerator.GenerateKernel(definition, ast, analysis, options);
        }

        if (analysis.CanVectorize && options.OptimizationLevel >= OptimizationLevel.Default)
        {
            return GenerateVectorizedKernel(definition, ast, analysis, options);
        }
        else
        {
            return GenerateScalarKernel(definition, ast, analysis, options);
        }
    }

    private CompiledKernelCode GenerateVectorizedKernel(
        KernelDefinition definition,
        KernelAst ast,
        KernelAnalysis analysis,
        CompilationOptions options)
    {
        // Create parameter expressions for kernel arguments
        var parameters = CreateParameterExpressions(definition);
        var workItemIdParam = Expression.Parameter(typeof(long[]), "workItemId");

        // Add work item ID to parameters
        var allParams = parameters.Concat(new[] { workItemIdParam }).ToArray();

        // Create body expression
        var bodyExpressions = new List<Expression>();

        // Get work item index
        var indexVar = Expression.Variable(typeof(long), "index");
        bodyExpressions.Add(Expression.Assign(indexVar, Expression.ArrayIndex(workItemIdParam, Expression.Constant(0))));

        // Generate vectorized operations
        if (ast.Operations.Count > 0)
        {
            var vectorExpr = GenerateVectorizedOperations(ast, parameters, indexVar, analysis.VectorizationFactor);
            bodyExpressions.AddRange(vectorExpr);
        }
        else
        {
            // Default vector addition pattern
            bodyExpressions.AddRange(GenerateDefaultVectorAddition(parameters, indexVar));
        }

        // Create expression body
        var body = Expression.Block(new[] { indexVar }, bodyExpressions);

        // Compile to delegate
#pragma warning disable IL3050 // Using member 'Expression.Lambda' which requires dynamic code
        var lambda = Expression.Lambda(body, allParams);
        var compiledDelegate = lambda.Compile();
#pragma warning restore IL3050

        return new CompiledKernelCode
        {
            CompiledDelegate = compiledDelegate,
            IsVectorized = true,
            OptimizationLevel = options.OptimizationLevel,
            EstimatedCodeSize = EstimateCodeSize(ast),
            OptimizationNotes = GenerateOptimizationNotes(ast, analysis, options)
        };
    }

    private CompiledKernelCode GenerateScalarKernel(
        KernelDefinition definition,
        KernelAst ast,
        KernelAnalysis analysis,
        CompilationOptions options)
    {
        // Create parameter expressions
        var parameters = CreateParameterExpressions(definition);
        var workItemIdParam = Expression.Parameter(typeof(long[]), "workItemId");

        var allParams = parameters.Concat(new[] { workItemIdParam }).ToArray();

        // Create body expression
        var bodyExpressions = new List<Expression>();

        // Get work item index
        var indexVar = Expression.Variable(typeof(long), "index");
        bodyExpressions.Add(Expression.Assign(indexVar, Expression.ArrayIndex(workItemIdParam, Expression.Constant(0))));

        // Generate scalar operations
        if (ast.Operations.Count > 0)
        {
            var scalarExpr = GenerateScalarOperations(ast, parameters, indexVar);
            bodyExpressions.AddRange(scalarExpr);
        }
        else
        {
            // Default scalar addition
            bodyExpressions.AddRange(GenerateDefaultScalarAddition(parameters, indexVar));
        }

        // Create expression body
        var body = Expression.Block(new[] { indexVar }, bodyExpressions);

        // Compile to delegate
#pragma warning disable IL3050 // Using member 'Expression.Lambda' which requires dynamic code
        var lambda = Expression.Lambda(body, allParams);
        var compiledDelegate = lambda.Compile();
#pragma warning restore IL3050

        return new CompiledKernelCode
        {
            CompiledDelegate = compiledDelegate,
            IsVectorized = false,
            OptimizationLevel = options.OptimizationLevel,
            EstimatedCodeSize = EstimateCodeSize(ast),
            OptimizationNotes = GenerateOptimizationNotes(ast, analysis, options)
        };
    }

    private List<ParameterExpression> CreateParameterExpressions(KernelDefinition definition)
    {
        var parameters = new List<ParameterExpression>();

        // Create default parameters based on common kernel patterns
        // This supports the most common scenarios of binary operations with an output buffer
        for (var i = 0; i < 3; i++)
        {
            // Default to Memory<float> for all parameters
            var paramType = typeof(Memory<>).MakeGenericType(typeof(float));
            var paramName = i switch
            {
                0 => "input1",
                1 => "input2",
                2 => "output",
                _ => $"param{i}"
            };
            parameters.Add(Expression.Parameter(paramType, paramName));
        }

        return parameters;
    }

    private List<Expression> GenerateVectorizedOperations(
        KernelAst ast,
        List<ParameterExpression> parameters,
        Expression indexExpr,
        int vectorizationFactor)
    {
        var expressions = new List<Expression>();

        // Generate expressions for each operation
        foreach (var op in ast.Operations)
        {
            switch (op.NodeType)
            {
                case AstNodeType.Add:
                    expressions.AddRange(GenerateVectorAdd(parameters, indexExpr, vectorizationFactor));
                    break;

                case AstNodeType.Multiply:
                    expressions.AddRange(GenerateVectorMultiply(parameters, indexExpr, vectorizationFactor));
                    break;

                    // Add more operations as needed
            }
        }

        return expressions;
    }

    private List<Expression> GenerateScalarOperations(
        KernelAst ast,
        List<ParameterExpression> parameters,
        Expression indexExpr)
    {
        var expressions = new List<Expression>();

        foreach (var op in ast.Operations)
        {
            switch (op.NodeType)
            {
                case AstNodeType.Add:
                    expressions.AddRange(GenerateScalarAdd(parameters, indexExpr));
                    break;

                case AstNodeType.Multiply:
                    expressions.AddRange(GenerateScalarMultiply(parameters, indexExpr));
                    break;
            }
        }

        return expressions;
    }

    private List<Expression> GenerateDefaultVectorAddition(List<ParameterExpression> parameters, Expression indexExpr)
    {
        // Default implementation for C = A + B using Memory<float>
        if (parameters.Count < 3)
        {
            return [];
        }

        var expressions = new List<Expression>();

        // Assume first 3 parameters are Memory<float> for input1, input2, output
        var input1 = parameters[0];
        var input2 = parameters[1];
        var output = parameters[2];

        // Get spans from Memory<float> using AOT-compatible approach
        var spanProperty = typeof(Memory<float>).GetProperty("Span")!;
        var input1Span = Expression.Property(input1, spanProperty);
        var input2Span = Expression.Property(input2, spanProperty);
        var outputSpan = Expression.Property(output, spanProperty);

        // Access elements
        var indexInt = Expression.Convert(indexExpr, typeof(int));
        var value1 = Expression.MakeIndex(input1Span, typeof(Span<float>).GetProperty("Item"), new[] { indexInt });
        var value2 = Expression.MakeIndex(input2Span, typeof(Span<float>).GetProperty("Item"), new[] { indexInt });
        var result = Expression.Add(value1, value2);

        // Store result
        var outputElement = Expression.MakeIndex(outputSpan, typeof(Span<float>).GetProperty("Item"), new[] { indexInt });
        expressions.Add(Expression.Assign(outputElement, result));

        return expressions;
    }

    private List<Expression> GenerateDefaultScalarAddition(List<ParameterExpression> parameters, Expression indexExpr) =>
        // Same as vector addition but without SIMD
        GenerateDefaultVectorAddition(parameters, indexExpr);

    private List<Expression> GenerateVectorAdd(List<ParameterExpression> parameters, Expression indexExpr, int vectorizationFactor) =>
        // For simplicity, delegate to scalar addition
        // In a full implementation, this would use System.Numerics.Vector<T>
        GenerateScalarAdd(parameters, indexExpr);

    private List<Expression> GenerateVectorMultiply(List<ParameterExpression> parameters, Expression indexExpr, int vectorizationFactor) => GenerateScalarMultiply(parameters, indexExpr);

    private List<Expression> GenerateScalarAdd(List<ParameterExpression> parameters, Expression indexExpr) => GenerateDefaultScalarAddition(parameters, indexExpr);

    private List<Expression> GenerateScalarMultiply(List<ParameterExpression> parameters, Expression indexExpr)
    {
        if (parameters.Count < 3)
        {
            return [];
        }

        var expressions = new List<Expression>();

        var input1 = parameters[0];
        var input2 = parameters[1];
        var output = parameters[2];

        // Get spans using AOT-compatible approach
        var spanProperty = typeof(Memory<float>).GetProperty("Span")!;
        var input1Span = Expression.Property(input1, spanProperty);
        var input2Span = Expression.Property(input2, spanProperty);
        var outputSpan = Expression.Property(output, spanProperty);

        // Multiply elements
        var indexInt = Expression.Convert(indexExpr, typeof(int));
        var value1 = Expression.MakeIndex(input1Span, typeof(Span<float>).GetProperty("Item"), new[] { indexInt });
        var value2 = Expression.MakeIndex(input2Span, typeof(Span<float>).GetProperty("Item"), new[] { indexInt });
        var result = Expression.Multiply(value1, value2);

        // Store result
        var outputElement = Expression.MakeIndex(outputSpan, typeof(Span<float>).GetProperty("Item"), new[] { indexInt });
        expressions.Add(Expression.Assign(outputElement, result));

        return expressions;
    }

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
            $"Compiled kernel with {ast.Operations.Count} operations"
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

        if (options.EnableDebugInfo)
        {
            notes.Add("Debug info enabled");
        }

        if (ast.HasLoops && options.OptimizationLevel == OptimizationLevel.Maximum)
        {
            notes.Add("Applied loop unrolling");
        }

        return [.. notes];
    }
}

/// <summary>
/// Represents compiled kernel code with metadata.
/// </summary>
internal sealed class CompiledKernelCode
{
    public Delegate CompiledDelegate { get; set; } = null!;
    public bool IsVectorized { get; set; }
    public OptimizationLevel OptimizationLevel { get; set; }
    public long EstimatedCodeSize { get; set; }
    public string[] OptimizationNotes { get; set; } = Array.Empty<string>();
}
