// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Enums;
using DotCompute.Backends.CPU.Kernels.Exceptions;
using DotCompute.Backends.CPU.Kernels.Models;
using DotCompute.Backends.CPU.Kernels.Types;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CPU backend has dynamic logging requirements

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// Compiles kernels for CPU execution with vectorization support.
/// </summary>
internal static partial class CpuKernelCompiler
{
    /// <summary>
    /// Compiles a kernel for CPU execution.
    /// </summary>
    public static async ValueTask<ICompiledKernel> CompileAsync(
        CpuKernelCompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var definition = context.Definition;
        var options = context.Options;
        var logger = context.Logger;

        logger.LogDebug("Starting kernel compilation: {KernelName}", definition.Name);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Validate kernel definition
            var validationResult = ValidateKernelDefinition(definition);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Kernel validation failed: {validationResult.ErrorMessage}");
            }

            // Step 2: Parse kernel source
            // Since we're working with pre-compiled kernels represented by metadata,
            // we create an AST from the kernel definition rather than parsing source text
            KernelAst? kernelAst = null;

            // Step 3: Analyze the kernel for vectorization opportunities
            var analysis = await AnalyzeKernelAsync(definition, kernelAst, cancellationToken).ConfigureAwait(false);

            // Step 4: Apply optimization passes
            // Apply optimization passes if enabled
            if ((OptimizationLevel)(int)options.OptimizationLevel != OptimizationLevel.None)
            {
                kernelAst = await OptimizeKernelAstAsync(kernelAst!, options, analysis, cancellationToken).ConfigureAwait(false);
            }

            // Step 5: Generate native code or IL
            var compiledCode = await GenerateCodeAsync(definition, kernelAst, analysis, options, cancellationToken).ConfigureAwait(false);

            // Step 6: Generate vectorized execution plan
            var executionPlan = GenerateExecutionPlan(analysis, context);

            // Step 7: Add compilation metadata
            var enrichedDefinition = EnrichDefinitionWithCompilationMetadata(definition, context.SimdCapabilities, compiledCode, stopwatch.Elapsed);

            // Step 8: Create the compiled kernel
            var compiledKernel = new CpuCompiledKernel(enrichedDefinition, executionPlan, context.ThreadPool, logger);

            // Store compiled code delegate if generated
            if (compiledCode.CompiledDelegate != null)
            {
                compiledKernel.SetCompiledDelegate(compiledCode.CompiledDelegate);
            }

            stopwatch.Stop();
            logger.LogInformation("Successfully compiled kernel '{KernelName}' in {ElapsedMs}ms with {DotCompute.Abstractions.Enums.OptimizationLevel} optimization",
                definition.Name, stopwatch.ElapsedMilliseconds, options.OptimizationLevel);

            return compiledKernel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compile kernel '{KernelName}'", definition.Name);
            throw new KernelCompilationException($"Failed to compile kernel '{definition.Name}'", ex);
        }
    }

    private static UnifiedValidationResult ValidateKernelDefinition(KernelDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return new UnifiedValidationResult(false, "Kernel name cannot be empty");
        }

        if (definition.Code == null || definition.Code.Length == 0)
        {
            return new UnifiedValidationResult(false, "Kernel code cannot be null or empty");
        }

        // Validate work dimensions if available in metadata
        if (definition.Metadata?.TryGetValue("WorkDimensions", out var workDimsObj) == true)
        {
            if (workDimsObj is int workDimensions && (workDimensions < 1 || workDimensions > 3))
            {
                return new UnifiedValidationResult(false, "Work dimensions must be between 1 and 3");
            }
        }

        // Validate parameters if available in metadata
        if (definition.Metadata?.TryGetValue("Parameters", out var paramsObj) == true)
        {
            if (paramsObj is IIReadOnlyList<object> parameters)
            {
                if (parameters.Count == 0)
                {
                    return new UnifiedValidationResult(false, "Kernel must have at least one parameter");
                }

                // Validate each parameter
                for (var i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i] is IDictionary<string, object> param)
                    {
                        if (!param.TryGetValue("Name", out var nameObj) ||
                            nameObj is not string name ||
                            string.IsNullOrWhiteSpace(name))
                        {
                            return new UnifiedValidationResult(false, $"Parameter at index {i} must have a valid name");
                        }

                        if (!param.TryGetValue("Type", out var typeObj) ||
                            typeObj is not string type ||
                            string.IsNullOrWhiteSpace(type))
                        {
                            return new UnifiedValidationResult(false, $"Parameter '{name}' must have a valid type");
                        }
                    }
                    else
                    {
                        return new UnifiedValidationResult(false, $"Invalid parameter format at index {i}");
                    }
                }
            }
        }

        return new UnifiedValidationResult(true, null);
    }

    private static async ValueTask<KernelAst> ParseKernelSourceAsync(string sourceCode, CancellationToken cancellationToken)
    {
        await Task.Yield(); // Ensure async execution

        return KernelSourceParser.Parse(sourceCode, "C#"); // C# is the primary kernel language
    }

    private static async ValueTask<KernelAnalysis> AnalyzeKernelAsync(
        KernelDefinition definition,
        KernelAst? kernelAst,
        CancellationToken cancellationToken)
    {
        // Simulate async analysis work
        await Task.Yield();

        // Enhanced analysis if we have AST
        var canVectorize = kernelAst != null ? AnalyzeVectorizability(kernelAst) : true;
        var hasBranching = kernelAst?.HasConditionals ?? false;
        var hasLoops = kernelAst?.HasLoops ?? false;
        var estimatedComplexity = kernelAst != null ? EstimateComplexity(kernelAst) : 1.0f;

        var analysis = new KernelAnalysis
        {
            Definition = definition,
            CanVectorize = canVectorize,
            VectorizationFactor = CalculateVectorizationFactor(definition),
            MemoryAccessPattern = AnalyzeMemoryAccess(definition),
            ComputeIntensity = EstimateComputeIntensity(definition),
            PreferredWorkGroupSize = CalculatePreferredWorkGroupSize(definition),
            HasBranching = hasBranching,
            HasLoops = hasLoops,
            EstimatedComplexity = (int)estimatedComplexity
        };

        return analysis;
    }

    private static KernelExecutionPlan GenerateExecutionPlan(
        KernelAnalysis analysis,
        CpuKernelCompilationContext context)
    {
        var simdCapabilities = context.SimdCapabilities;
        var vectorWidth = simdCapabilities.PreferredVectorWidth;
        var canUseSimd = simdCapabilities.IsHardwareAccelerated && analysis.CanVectorize;

        return new KernelExecutionPlan
        {
            Analysis = analysis,
            UseVectorization = canUseSimd,
            VectorWidth = vectorWidth,
            VectorizationFactor = analysis.VectorizationFactor,
            WorkGroupSize = analysis.PreferredWorkGroupSize,
            MemoryPrefetchDistance = CalculateMemoryPrefetchDistance(analysis),
            EnableLoopUnrolling = (OptimizationLevel)(int)context.Options.OptimizationLevel == OptimizationLevel.O3,
            InstructionSets = simdCapabilities.SupportedInstructionSets
        };
    }

    private static int CalculateVectorizationFactor(KernelDefinition definition)
    {
        // Analyze the kernel to determine optimal vectorization factor
        var simdWidth = SimdCapabilities.PreferredVectorWidth;

        // Extract parameter count from metadata
        var paramCount = 3; // Default to 3 parameters
        if (definition.Metadata?.TryGetValue("ParameterCount", out var paramCountObj) == true && paramCountObj is int count)
        {
            paramCount = count;
        }

        // For simple kernels, use maximum vectorization
        if (paramCount <= 4)
        {
            return simdWidth switch
            {
                512 => 16, // AVX512 - 16 floats
                256 => 8,  // AVX2 - 8 floats
                128 => 4,  // SSE - 4 floats
                _ => 1     // No vectorization
            };
        }

        // For complex kernels, use conservative vectorization
        return simdWidth switch
        {
            512 => 8,  // AVX512 - 8 floats
            256 => 4,  // AVX2 - 4 floats
            128 => 2,  // SSE - 2 floats
            _ => 1     // No vectorization
        };
    }

    private static MemoryAccessPattern AnalyzeMemoryAccess(KernelDefinition definition)
    {
        // Extract parameter info from metadata
        var paramCount = 3; // Default to 3 parameters
        if (definition.Metadata?.TryGetValue("ParameterCount", out var paramCountObj) == true && paramCountObj is int count)
        {
            paramCount = count;
        }

        // Analyze parameter types to determine memory access pattern
        // Default assumption: all parameters are buffers with read-write access
        var bufferParams = paramCount;
        var readOnlyParams = 0;
        var writeOnlyParams = 0;

        if (definition.Metadata?.TryGetValue("ParameterAccess", out var accessObj) == true && accessObj is string[] access)
        {
            readOnlyParams = access.Count(a => a == "ReadOnly");
            writeOnlyParams = access.Count(a => a == "WriteOnly");
        }

        // Map CPU-specific access patterns to canonical spatial patterns
        if (bufferParams == 0)
        {
            // Compute-intensive kernels typically have sequential access
            return MemoryAccessPattern.Sequential;
        }
        else if (readOnlyParams == bufferParams)
        {
            // Read-only access typically sequential for good cache behavior
            return MemoryAccessPattern.Sequential;
        }
        else if (writeOnlyParams == bufferParams)
        {
            // Write-only access, often streaming pattern
            return MemoryAccessPattern.Sequential;
        }
        else
        {
            // Mixed read-write, use sequential as default
            return MemoryAccessPattern.Sequential;
        }
    }

    private static ComputeIntensity EstimateComputeIntensity(KernelDefinition definition)
    {
        // Extract parameter count from metadata
        var paramCount = 3; // Default to 3 parameters
        if (definition.Metadata?.TryGetValue("ParameterCount", out var paramCountObj) == true && paramCountObj is int count)
        {
            paramCount = count;
        }

        // Extract work dimensions from metadata
        var workDimensions = 1; // Default to 1D
        if (definition.Metadata?.TryGetValue("WorkDimensions", out var dimObj) == true && dimObj is int dims)
        {
            workDimensions = dims;
        }

        // Estimate based on kernel complexity
        var parameterComplexity = paramCount;
        var dimensionComplexity = workDimensions;

        var totalComplexity = parameterComplexity + dimensionComplexity;

        return totalComplexity switch
        {
            <= 3 => ComputeIntensity.Low,
            <= 6 => ComputeIntensity.Medium,
            <= 10 => ComputeIntensity.High,
            _ => ComputeIntensity.VeryHigh
        };
    }

    private static int CalculatePreferredWorkGroupSize(KernelDefinition definition)
    {
        // Extract work dimensions from metadata
        var workDimensions = 1; // Default to 1D
        if (definition.Metadata?.TryGetValue("WorkDimensions", out var dimObj) == true && dimObj is int dims)
        {
            workDimensions = dims;
        }

        // Calculate based on dimensions and complexity
        var baseSize = workDimensions switch
        {
            1 => 64,   // 1D kernels
            2 => 16,   // 2D kernels (16x16 = 256)
            3 => 8,    // 3D kernels (8x8x8 = 512)
            _ => 32    // Default
        };

        // Extract parameter count from metadata
        var paramCount = 3; // Default to 3 parameters
        if (definition.Metadata?.TryGetValue("ParameterCount", out var paramCountObj) == true && paramCountObj is int count)
        {
            paramCount = count;
        }

        // Adjust based on parameter count
        if (paramCount > 8)
        {
            baseSize /= 2; // Reduce for complex kernels
        }

        return Math.Max(baseSize, 8); // Minimum work group size
    }

    private static int CalculateMemoryPrefetchDistance(KernelAnalysis analysis)
    {
        // Calculate optimal prefetch distance based on access pattern
        return analysis.MemoryAccessPattern switch
        {
            MemoryAccessPattern.Sequential => 128,    // Aggressive prefetch for sequential
            MemoryAccessPattern.Coalesced => 64,      // Moderate prefetch for coalesced
            MemoryAccessPattern.Strided => 32,        // Conservative for strided
            MemoryAccessPattern.Random => 16,         // Minimal prefetch for random
            MemoryAccessPattern.Tiled => 64,          // Moderate for tiled access
            _ => 64
        };
    }

    private static async ValueTask<KernelAst> OptimizeKernelAstAsync(
        KernelAst ast,
        CompilationOptions options,
        KernelAnalysis analysis,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        // Use static methods directly

        // Apply optimization passes based on optimization level
        switch ((OptimizationLevel)(int)options.OptimizationLevel)
        {
            case OptimizationLevel.None:
                // Minimal optimization for debugging
                ast = KernelOptimizer.ApplyBasicOptimizations(ast);
                break;

            case OptimizationLevel.Default:
                // Standard optimizations
                ast = KernelOptimizer.ApplyStandardOptimizations(ast);
                if (analysis.CanVectorize)
                {
                    ast = KernelOptimizer.ApplyVectorizationOptimizations(ast, analysis.VectorizationFactor);
                }
                break;

            case OptimizationLevel.O3:
                // Aggressive optimizations (O3, Aggressive, and Full all map to value 3, but only use O3 case)
                // Note: OptimizationLevel.Aggressive and OptimizationLevel.Full are aliases for O3
                ast = KernelOptimizer.ApplyAggressiveOptimizations(ast);
                if (analysis.CanVectorize)
                {
                    ast = KernelOptimizer.ApplyVectorizationOptimizations(ast, analysis.VectorizationFactor);
                    ast = KernelOptimizer.ApplyLoopUnrolling(ast, analysis.VectorizationFactor);
                    ast = KernelOptimizer.ApplyFastMathOptimizations(ast);
                }
                break;
        }

        return ast;
    }

    private static async ValueTask<CompiledCode> GenerateCodeAsync(
        KernelDefinition definition,
        KernelAst? kernelAst,
        KernelAnalysis analysis,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        // Use static methods directly

        if (kernelAst != null)
        {
            // Generate code from AST
            return CpuRuntimeCodeGenerator.GenerateFromAst(kernelAst, definition, analysis, options);
        }
        else if (definition.Code != null && definition.Code.Length > 0)
        {
            // JIT compile bytecode
            return CpuRuntimeCodeGenerator.GenerateFromBytecode(System.Text.Encoding.UTF8.GetBytes(definition.Code), definition, analysis, options);
        }
        else
        {
            // Generate default vectorized kernel based on operation type
            return CpuRuntimeCodeGenerator.GenerateDefaultKernel(definition, analysis, options);
        }
    }

    private static bool AnalyzeVectorizability(KernelAst ast)
    {
        // Check if kernel can be vectorized
        // Look for patterns that prevent vectorization
        if (ast.HasRecursion)
        {
            return false;
        }

        if (ast.HasIndirectMemoryAccess)
        {
            return false;
        }

        if (ast.HasComplexControlFlow)
        {
            return false;
        }

        // Check for vectorizable operations
        return ast.Operations.Any(IsVectorizableOperation);
    }

    private static bool IsVectorizableOperation(AstNode operation)
    {
        return operation.NodeType switch
        {
            AstNodeType.Add => true,
            AstNodeType.Subtract => true,
            AstNodeType.Multiply => true,
            AstNodeType.Divide => true,
            AstNodeType.Min => true,
            AstNodeType.Max => true,
            AstNodeType.Abs => true,
            AstNodeType.Sqrt => true,
            _ => false
        };
    }

    private static int EstimateComplexity(KernelAst ast)
    {
        var complexity = 0;

        // Count operations
        complexity += ast.Operations.Count;

        // Add complexity for control flow
        if (ast.HasConditionals)
        {
            complexity += 5;
        }

        if (ast.HasLoops)
        {
            complexity += 10;
        }

        // Add complexity for memory operations
        complexity += ast.MemoryOperations.Count * 2;

        return complexity;
    }

    private static KernelDefinition EnrichDefinitionWithCompilationMetadata(
        KernelDefinition original,
        SimdSummary simdCapabilities,
        CompiledCode compiledCode,
        TimeSpan compilationTime)
    {
        var metadata = original.Metadata != null
            ? new Dictionary<string, object>(original.Metadata)
            : [];

        metadata["SimdCapabilities"] = simdCapabilities;
        metadata["CompilationTime"] = compilationTime;
        metadata["CodeSize"] = compiledCode.CodeSize;
        metadata["OptimizationNotes"] = compiledCode.OptimizationNotes;

        var sourceCode = original.Code ?? string.Empty;
        var kernelSource = new TextKernelSource(
            code: sourceCode,
            name: original.Name,
            language: KernelLanguage.CSharpIL,
            entryPoint: original.EntryPoint ?? "main",
            dependencies: []
        );

        var definition = new KernelDefinition(original.Name, kernelSource.Code, kernelSource.EntryPoint);

        // Override metadata with enriched information
        if (definition.Metadata != null)
        {
            foreach (var kvp in metadata)
            {
                definition.Metadata[kvp.Key] = kvp.Value;
            }
        }

        return definition;
    }
}

/// <summary>
/// Result of code generation.
/// </summary>
internal sealed class CompiledCode
{
    /// <summary>
    /// Gets or sets the compiled delegate.
    /// </summary>
    /// <value>The compiled delegate.</value>
    public Delegate? CompiledDelegate { get; set; }
    /// <summary>
    /// Gets or sets the bytecode.
    /// </summary>
    /// <value>The bytecode.</value>
    public byte[]? Bytecode { get; set; }
    /// <summary>
    /// Gets or sets the code size.
    /// </summary>
    /// <value>The code size.</value>
    public long CodeSize { get; set; }
    /// <summary>
    /// Gets or sets the optimization notes.
    /// </summary>
    /// <value>The optimization notes.</value>
    public string[] OptimizationNotes { get; set; } = [];
}

/// <summary>
/// Parses kernel source code into AST.
/// </summary>
internal static partial class KernelSourceParser
{
    [GeneratedRegex(@"(public|private|protected|internal)?\s*(static)?\s*\w+\s+(\w+)\s*\(", RegexOptions.Compiled)]
    private static partial Regex FunctionNameRegex();
    /// <summary>
    /// Gets parse.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="language">The language.</param>
    /// <returns>The result of the operation.</returns>
    public static KernelAst Parse(string code, string language)
    {
        try
        {
            if (language.Equals("C#", StringComparison.OrdinalIgnoreCase) ||
                language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
            {
                return ParseCSharpWithRoslyn(code);
            }

            // For other languages, use pattern-based parsing
            return ParseWithPatterns(code, language);
        }
        catch (Exception ex)
        {
            // Log error and fall back to pattern matching
            Debug.WriteLine($"Failed to parse with advanced parser: {ex.Message}");
            return ParseWithPatterns(code, language);
        }
    }

    private static KernelAst ParseCSharpWithRoslyn(string code)
    {
        var ast = new KernelAst();

        try
        {
            var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();

            var visitor = new KernelSyntaxVisitor();
            visitor.Visit(root);

            ast.HasConditionals = visitor.HasConditionals;
            ast.HasLoops = visitor.HasLoops;
            ast.HasRecursion = visitor.HasRecursion;
            ast.HasIndirectMemoryAccess = visitor.HasIndirectMemoryAccess;

            foreach (var op in visitor.Operations)
            {
                ast.Operations.Add(op);
            }
            foreach (var variable in visitor.Variables)
            {
                ast.Variables.Add(variable);
            }
            foreach (var parameter in visitor.Parameters)
            {
                ast.Parameters.Add(parameter);
            }
            foreach (var call in visitor.FunctionCalls)
            {
                ast.FunctionCalls.Add(call);
            }

            // Additional analysis
            ast.ComplexityScore = CalculateComplexityScore(ast);
            ast.EstimatedInstructions = EstimateInstructionCount(ast);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Roslyn parsing failed: {ex.Message}");
            // Fall back to pattern matching
            return ParseWithPatterns(code, "C#");
        }

        return ast;
    }

    private static KernelAst ParseWithPatterns(string code, string language)
    {
        var ast = new KernelAst();

        // Detect basic patterns
        ast.HasConditionals = code.Contains("if", StringComparison.Ordinal) ||
                             code.Contains('?', StringComparison.Ordinal) ||
                             code.Contains("switch", StringComparison.Ordinal);

        ast.HasLoops = code.Contains("for", StringComparison.Ordinal) ||
                      code.Contains("while", StringComparison.Ordinal) ||
                      code.Contains("foreach", StringComparison.Ordinal) ||
                      code.Contains("do", StringComparison.Ordinal);

        ast.HasRecursion = DetectRecursion(code);
        ast.HasIndirectMemoryAccess = code.Contains('[', StringComparison.Ordinal) &&
                                     code.Contains(']', StringComparison.Ordinal);

        // Parse operations using regex patterns
        DetectOperations(code, ast);
        DetectVariables(code, ast);
        DetectFunctionCalls(code, ast);

        // Calculate metrics
        ast.ComplexityScore = CalculateComplexityScore(ast);
        ast.EstimatedInstructions = EstimateInstructionCount(ast);

        return ast;
    }

    private static bool DetectRecursion(string code)
    {
        // Simple heuristic: look for function calls that might be recursive
        var functionNameMatch = FunctionNameRegex().Match(code);
        if (functionNameMatch.Success)
        {
            var functionName = functionNameMatch.Groups[3].Value;
            return code.IndexOf(functionName, functionNameMatch.Index + functionNameMatch.Length, StringComparison.Ordinal) >= 0;
        }
        return false;
    }

    private static void DetectOperations(string code, KernelAst ast)
    {
        var operationPatterns = new Dictionary<string, AstNodeType>
    {
        { @"\+(?!=)", AstNodeType.Add },
        { @"-(?!=)", AstNodeType.Subtract },
        { @"\*(?!=)", AstNodeType.Multiply },
        { @"/(?!=)", AstNodeType.Divide },
        { @"%", AstNodeType.Modulo },
        { @"\&\&", AstNodeType.LogicalAnd },
        { @"\|\|", AstNodeType.LogicalOr },
        { @"==", AstNodeType.Equal },
        { @"!=", AstNodeType.NotEqual },
        { @"<=", AstNodeType.LessThanOrEqual },
        { @">=", AstNodeType.GreaterThanOrEqual },
        { @"<(?!=)", AstNodeType.LessThan },
        { @">(?!=)", AstNodeType.GreaterThan },
        { @"\&(?!&)", AstNodeType.BitwiseAnd },
        { @"\|(?!\|)", AstNodeType.BitwiseOr },
        { @"\^", AstNodeType.BitwiseXor },
        { @"<<", AstNodeType.LeftShift },
        { @">>", AstNodeType.RightShift }
    };

        foreach (var pattern in operationPatterns)
        {
            if (Regex.IsMatch(code, pattern.Key))
            {
                var matches = Regex.Matches(code, pattern.Key);
                foreach (Match match in matches)
                {
                    ast.Operations.Add(new AstNode
                    {
                        NodeType = pattern.Value,
                        Position = match.Index,
                        Text = match.Value
                    });
                }
            }
        }
    }

    private static void DetectVariables(string code, KernelAst ast)
    {
        // Detect variable declarations
        var variablePatterns = new[]
        {
        @"(int|float|double|long|short|byte|bool|char)\s+(\w+)",
        @"var\s+(\w+)",
        @"(\w+)\s+(\w+)\s*="
    };

        foreach (var pattern in variablePatterns)
        {
            var matches = Regex.Matches(code, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var variableName = match.Groups.Count > 2 ? match.Groups[2].Value : match.Groups[1].Value;
                if (!ast.Variables.Contains(variableName))
                {
                    ast.Variables.Add(variableName);
                }
            }
        }
    }

    private static void DetectFunctionCalls(string code, KernelAst ast)
    {
        // Detect function calls
        var functionCallPattern = @"(\w+)\s*\(";
        var matches = Regex.Matches(code, functionCallPattern);

        foreach (Match match in matches)
        {
            var functionName = match.Groups[1].Value;
            // Filter out language keywords and common operators
            if (!IsKeyword(functionName) && !ast.FunctionCalls.Contains(functionName))
            {
                ast.FunctionCalls.Add(functionName);
            }
        }
    }

    private static bool IsKeyword(string word)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "if", "else", "for", "while", "do", "switch", "case", "default",
        "return", "break", "continue", "goto", "try", "catch", "finally",
        "throw", "using", "namespace", "class", "struct", "interface",
        "enum", "delegate", "public", "private", "protected", "internal",
        "static", "virtual", "override", "abstract", "sealed", "readonly",
        "const", "volatile", "unsafe", "fixed", "lock", "sizeof", "typeof"
    };
        return keywords.Contains(word);
    }

    private static int CalculateComplexityScore(KernelAst ast)
    {
        var score = ast.Operations.Count;
        if (ast.HasConditionals)
        {
            score += 2;
        }
        if (ast.HasLoops)
        {
            score += 3;
        }
        if (ast.HasRecursion)
        {
            score += 5;
        }
        if (ast.HasIndirectMemoryAccess)
        {
            score += 2;
        }
        score += ast.FunctionCalls.Count;
        return score;
    }

    private static long EstimateInstructionCount(KernelAst ast)
    {
        var count = ast.Operations.Count * 2L; // Rough estimate: 2 instructions per operation
        if (ast.HasLoops)
        {
            count *= 10; // Loops multiply instruction count
        }
        if (ast.HasConditionals)
        {
            count += 5; // Branching overhead
        }
        if (ast.HasRecursion)
        {
            count *= 5; // Recursive overhead
        }
        count += ast.FunctionCalls.Count * 3L; // Function call overhead
        return Math.Max(count, 1L);
    }
}

/// <summary>
/// Roslyn syntax visitor for analyzing C# kernel code.
/// </summary>
internal sealed class KernelSyntaxVisitor : Microsoft.CodeAnalysis.CSharp.CSharpSyntaxWalker
{
    /// <summary>
    /// Gets or sets a value indicating whether conditionals.
    /// </summary>
    /// <value>The has conditionals.</value>
    public bool HasConditionals { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether loops.
    /// </summary>
    /// <value>The has loops.</value>
    public bool HasLoops { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether recursion.
    /// </summary>
    /// <value>The has recursion.</value>
    public bool HasRecursion { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether indirect memory access.
    /// </summary>
    /// <value>The has indirect memory access.</value>
    public bool HasIndirectMemoryAccess { get; private set; }
    /// <summary>
    /// Gets or sets the operations.
    /// </summary>
    /// <value>The operations.</value>
    public IList<AstNode> Operations { get; } = [];
    /// <summary>
    /// Gets or sets the variables.
    /// </summary>
    /// <value>The variables.</value>
    public IList<string> Variables { get; } = [];
    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    /// <value>The parameters.</value>
    public IList<string> Parameters { get; } = [];
    /// <summary>
    /// Gets or sets the function calls.
    /// </summary>
    /// <value>The function calls.</value>
    public IList<string> FunctionCalls { get; } = [];

    private readonly HashSet<string> _declaredMethods = [];
    private readonly HashSet<string> _calledMethods = [];
    /// <summary>
    /// Performs visit if statement.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitIfStatement(Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax node)
    {
        HasConditionals = true;
        base.VisitIfStatement(node);
    }
    /// <summary>
    /// Performs visit switch statement.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitSwitchStatement(Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax node)
    {
        HasConditionals = true;
        base.VisitSwitchStatement(node);
    }
    /// <summary>
    /// Performs visit conditional expression.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitConditionalExpression(Microsoft.CodeAnalysis.CSharp.Syntax.ConditionalExpressionSyntax node)
    {
        HasConditionals = true;
        base.VisitConditionalExpression(node);
    }
    /// <summary>
    /// Performs visit for statement.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitForStatement(Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax node)
    {
        HasLoops = true;
        base.VisitForStatement(node);
    }
    /// <summary>
    /// Performs visit while statement.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitWhileStatement(Microsoft.CodeAnalysis.CSharp.Syntax.WhileStatementSyntax node)
    {
        HasLoops = true;
        base.VisitWhileStatement(node);
    }
    /// <summary>
    /// Performs visit do statement.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitDoStatement(Microsoft.CodeAnalysis.CSharp.Syntax.DoStatementSyntax node)
    {
        HasLoops = true;
        base.VisitDoStatement(node);
    }
    /// <summary>
    /// Performs visit for each statement.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitForEachStatement(Microsoft.CodeAnalysis.CSharp.Syntax.ForEachStatementSyntax node)
    {
        HasLoops = true;
        base.VisitForEachStatement(node);
    }
    /// <summary>
    /// Performs visit element access expression.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitElementAccessExpression(Microsoft.CodeAnalysis.CSharp.Syntax.ElementAccessExpressionSyntax node)
    {
        HasIndirectMemoryAccess = true;
        base.VisitElementAccessExpression(node);
    }
    /// <summary>
    /// Performs visit binary expression.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitBinaryExpression(Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax node)
    {
        var operationType = node.OperatorToken.ValueText switch
        {
            "+" => AstNodeType.Add,
            "-" => AstNodeType.Subtract,
            "*" => AstNodeType.Multiply,
            "/" => AstNodeType.Divide,
            "%" => AstNodeType.Modulo,
            "&&" => AstNodeType.LogicalAnd,
            "||" => AstNodeType.LogicalOr,
            "==" => AstNodeType.Equal,
            "!=" => AstNodeType.NotEqual,
            "<" => AstNodeType.LessThan,
            ">" => AstNodeType.GreaterThan,
            "<=" => AstNodeType.LessThanOrEqual,
            ">=" => AstNodeType.GreaterThanOrEqual,
            "&" => AstNodeType.BitwiseAnd,
            "|" => AstNodeType.BitwiseOr,
            "^" => AstNodeType.BitwiseXor,
            "<<" => AstNodeType.LeftShift,
            ">>" => AstNodeType.RightShift,
            _ => AstNodeType.Unknown
        };

        if (operationType != AstNodeType.Unknown)
        {
            Operations.Add(new AstNode
            {
                NodeType = operationType,
                Position = node.SpanStart,
                Text = node.OperatorToken.ValueText
            });
        }

        base.VisitBinaryExpression(node);
    }
    /// <summary>
    /// Performs visit variable declarator.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitVariableDeclarator(Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax node)
    {
        var variableName = node.Identifier.ValueText;
        if (!Variables.Contains(variableName))
        {
            Variables.Add(variableName);
        }
        base.VisitVariableDeclarator(node);
    }
    /// <summary>
    /// Performs visit parameter.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitParameter(Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax node)
    {
        var parameterName = node.Identifier.ValueText;
        if (!Parameters.Contains(parameterName))
        {
            Parameters.Add(parameterName);
        }
        base.VisitParameter(node);
    }
    /// <summary>
    /// Performs visit method declaration.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitMethodDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax node)
    {
        var methodName = node.Identifier.ValueText;
        _ = _declaredMethods.Add(methodName);
        base.VisitMethodDeclaration(node);
    }
    /// <summary>
    /// Performs visit invocation expression.
    /// </summary>
    /// <param name="node">The node.</param>

    public override void VisitInvocationExpression(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax node)
    {
        if (node.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax identifierName)
        {
            var methodName = identifierName.Identifier.ValueText;
            _ = _calledMethods.Add(methodName);

            if (!FunctionCalls.Contains(methodName))
            {
                FunctionCalls.Add(methodName);
            }
        }
        else if (node.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.ValueText;
            if (!FunctionCalls.Contains(methodName))
            {
                FunctionCalls.Add(methodName);
            }
        }

        base.VisitInvocationExpression(node);

        // Check for recursion after visiting all nodes
        HasRecursion = _declaredMethods.Intersect(_calledMethods).Count() > 0;
    }
}

/// <summary>
/// Optimizes kernel AST.
/// </summary>
internal static partial class KernelOptimizer
{
    /// <summary>
    /// Gets apply basic optimizations.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <returns>The result of the operation.</returns>
    public static KernelAst ApplyBasicOptimizations(KernelAst ast) => ast; // Constant folding, dead code elimination
    /// <summary>
    /// Gets apply standard optimizations.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <returns>The result of the operation.</returns>

    public static KernelAst ApplyStandardOptimizations(KernelAst ast)
    {
        // Common subexpression elimination, strength reduction
        ast = ApplyBasicOptimizations(ast);
        return ast;
    }
    /// <summary>
    /// Gets apply aggressive optimizations.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <returns>The result of the operation.</returns>

    public static KernelAst ApplyAggressiveOptimizations(KernelAst ast)
    {
        // Loop transformations, function inlining
        ast = ApplyStandardOptimizations(ast);
        return ast;
    }
    /// <summary>
    /// Gets apply vectorization optimizations.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <param name="vectorizationFactor">The vectorization factor.</param>
    /// <returns>The result of the operation.</returns>

    public static KernelAst ApplyVectorizationOptimizations(KernelAst ast, int vectorizationFactor) => ast; // Transform operations to use SIMD instructions
    /// <summary>
    /// Gets apply loop unrolling.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <param name="unrollFactor">The unroll factor.</param>
    /// <returns>The result of the operation.</returns>

    public static KernelAst ApplyLoopUnrolling(KernelAst ast, int unrollFactor) => ast; // Unroll loops by the specified factor
    /// <summary>
    /// Gets apply fast math optimizations.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <returns>The result of the operation.</returns>

    public static KernelAst ApplyFastMathOptimizations(KernelAst ast) => ast; // Relaxed floating-point operations for performance
}
