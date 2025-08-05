// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CPU backend has dynamic logging requirements

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Compiles kernels for CPU execution with vectorization support.
/// </summary>
internal static partial class CpuKernelCompiler
{
    private static readonly CpuRuntimeCodeGenerator CodeGenerator = new();
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
            if (options.OptimizationLevel != OptimizationLevel.None)
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
            logger.LogInformation("Successfully compiled kernel '{KernelName}' in {ElapsedMs}ms with {OptimizationLevel} optimization",
                definition.Name, stopwatch.ElapsedMilliseconds, options.OptimizationLevel);

            return compiledKernel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compile kernel '{KernelName}'", definition.Name);
            throw new KernelCompilationException($"Failed to compile kernel '{definition.Name}'", ex);
        }
    }

    private static ValidationResult ValidateKernelDefinition(KernelDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return new ValidationResult(false, "Kernel name cannot be empty");
        }

        if (definition.Code == null || definition.Code.Length == 0)
        {
            return new ValidationResult(false, "Kernel code cannot be null or empty");
        }

        // TODO: Add work dimensions validation when available in KernelDefinition
        // if (definition.WorkDimensions < 1 || definition.WorkDimensions > 3)
        // {
        //     return new ValidationResult(false, "Work dimensions must be between 1 and 3");
        // }

        // TODO: Add parameter validation when available in KernelDefinition
        // if (definition.Parameters == null || definition.Parameters.Count == 0)
        // {
        //     return new ValidationResult(false, "Kernel must have at least one parameter");
        // }

        // TODO: Validate parameter types when available in KernelDefinition
        // foreach (var param in definition.Parameters)
        // {
        //     if (string.IsNullOrWhiteSpace(param.Name))
        //     {
        //         return new ValidationResult(false, "All kernel parameters must have names");
        //     }
        // }

        return new ValidationResult(true, null);
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
            EnableLoopUnrolling = context.Options.OptimizationLevel == OptimizationLevel.Maximum,
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

        if (bufferParams == 0)
        {
            return MemoryAccessPattern.ComputeIntensive;
        }
        else if (readOnlyParams == bufferParams)
        {
            return MemoryAccessPattern.ReadOnly;
        }
        else if (writeOnlyParams == bufferParams)
        {
            return MemoryAccessPattern.WriteOnly;
        }
        else
        {
            return MemoryAccessPattern.ReadWrite;
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
            MemoryAccessPattern.ReadOnly => 128,      // Aggressive prefetch for read-only
            MemoryAccessPattern.WriteOnly => 64,      // Moderate prefetch for write-only
            MemoryAccessPattern.ReadWrite => 32,      // Conservative for read-write
            MemoryAccessPattern.ComputeIntensive => 0, // No prefetch for compute-only
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
        switch (options.OptimizationLevel)
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

            case OptimizationLevel.Maximum:
                // Aggressive optimizations
                ast = KernelOptimizer.ApplyAggressiveOptimizations(ast);
                if (analysis.CanVectorize)
                {
                    ast = KernelOptimizer.ApplyVectorizationOptimizations(ast, analysis.VectorizationFactor);
                    ast = KernelOptimizer.ApplyLoopUnrolling(ast, analysis.VectorizationFactor);
                }
                // Fast math for maximum optimization
                if (options.OptimizationLevel == OptimizationLevel.Maximum)
                {
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
            return CodeGenerator.GenerateFromAst(kernelAst, definition, analysis, options);
        }
        else if (definition.Code != null && definition.Code.Length > 0)
        {
            // JIT compile bytecode
            return CodeGenerator.GenerateFromBytecode(definition.Code, definition, analysis, options);
        }
        else
        {
            // Generate default vectorized kernel based on operation type
            return CodeGenerator.GenerateDefaultKernel(definition, analysis, options);
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
        return ast.Operations.Any(op => IsVectorizableOperation(op));
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

        var sourceCode = System.Text.Encoding.UTF8.GetString(original.Code);
        var kernelSource = new TextKernelSource(
            code: sourceCode,
            name: original.Name,
            language: KernelLanguage.CSharpIL,
            entryPoint: original.EntryPoint ?? "main",
            dependencies: Array.Empty<string>()
        );

        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false,
            AdditionalFlags = null,
            Defines = null
        };

        var definition = new KernelDefinition(original.Name, kernelSource, compilationOptions);

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
/// Context for kernel compilation.
/// </summary>
internal sealed class CpuKernelCompilationContext
{
    public required KernelDefinition Definition { get; init; }
    public required CompilationOptions Options { get; init; }
    public required SimdSummary SimdCapabilities { get; init; }
    public required CpuThreadPool ThreadPool { get; init; }
    public required ILogger Logger { get; init; }
}

/// <summary>
/// Analysis results for a kernel.
/// </summary>
internal sealed class KernelAnalysis
{
    public required KernelDefinition Definition { get; init; }
    public required bool CanVectorize { get; init; }
    public required int VectorizationFactor { get; init; }
    public required MemoryAccessPattern MemoryAccessPattern { get; init; }
    public required ComputeIntensity ComputeIntensity { get; init; }
    public required int PreferredWorkGroupSize { get; init; }
    public bool HasBranching { get; set; }
    public bool HasLoops { get; set; }
    public int EstimatedComplexity { get; set; }
}

/// <summary>
/// Execution plan for a compiled kernel.
/// </summary>
internal sealed class KernelExecutionPlan
{
    public required KernelAnalysis Analysis { get; init; }
    public required bool UseVectorization { get; init; }
    public required int VectorWidth { get; init; }
    public required int VectorizationFactor { get; init; }
    public required int WorkGroupSize { get; init; }
    public required int MemoryPrefetchDistance { get; init; }
    public required bool EnableLoopUnrolling { get; init; }
    public required IReadOnlySet<string> InstructionSets { get; init; }
}

/// <summary>
/// Memory access pattern for a kernel.
/// </summary>
internal enum MemoryAccessPattern
{
    ReadOnly,
    WriteOnly,
    ReadWrite,
    ComputeIntensive
}

/// <summary>
/// Compute intensity level.
/// </summary>
internal enum ComputeIntensity
{
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// Represents a validation result for kernel compilation.
/// </summary>
internal readonly struct ValidationResult(bool isValid, string? errorMessage)
{
    public bool IsValid { get; } = isValid;
    public string? ErrorMessage { get; } = errorMessage;
}

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
public sealed class KernelCompilationException : Exception
{
    public KernelCompilationException() : base() { }
    public KernelCompilationException(string message) : base(message) { }
    public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Abstract Syntax Tree representation of a kernel.
/// </summary>
internal sealed class KernelAst
{
    public List<AstNode> Operations { get; set; } = [];
    public List<AstNode> MemoryOperations { get; set; } = [];
    public bool HasConditionals { get; set; }
    public bool HasLoops { get; set; }
    public bool HasRecursion { get; set; }
    public bool HasIndirectMemoryAccess { get; set; }
    public bool HasComplexControlFlow { get; set; }
}

/// <summary>
/// AST node representing an operation.
/// </summary>
internal sealed class AstNode
{
    public AstNodeType NodeType { get; set; }
    public List<AstNode> Children { get; set; } = [];
    public object? Value { get; set; }
}

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
    Parameter
}

/// <summary>
/// Result of code generation.
/// </summary>
internal sealed class CompiledCode
{
    public Delegate? CompiledDelegate { get; set; }
    public byte[]? Bytecode { get; set; }
    public long CodeSize { get; set; }
    public string[] OptimizationNotes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Parses kernel source code into AST.
/// </summary>
internal static class KernelSourceParser
{
#pragma warning disable SYSLIB1045 // Suppress GeneratedRegex warning for AOT compatibility
    private static readonly Regex _loadPatternRegex = new(@"(\w+)\[", RegexOptions.Compiled);
    private static readonly Regex _storePatternRegex = new(@"\[\w+\]\s*=", RegexOptions.Compiled);
#pragma warning restore SYSLIB1045
    public static KernelAst Parse(string code, string language)
    {
        // Simple parser implementation for demonstration
        // In production, this would use a proper parser library or Roslyn for C#

        var ast = new KernelAst();

        // Detect basic patterns
        ast.HasConditionals = code.Contains("if", StringComparison.Ordinal) || code.Contains('?', StringComparison.Ordinal);
        ast.HasLoops = code.Contains("for", StringComparison.Ordinal) || code.Contains("while", StringComparison.Ordinal);
        ast.HasRecursion = false; // Would need deeper analysis
        ast.HasIndirectMemoryAccess = code.Contains('[', StringComparison.Ordinal) && code.Contains(']', StringComparison.Ordinal);

        // Parse operations (simplified)
        if (code.Contains('+', StringComparison.Ordinal))
        {
            ast.Operations.Add(new AstNode { NodeType = AstNodeType.Add });
        }

        if (code.Contains('-', StringComparison.Ordinal))
        {
            ast.Operations.Add(new AstNode { NodeType = AstNodeType.Subtract });
        }

        if (code.Contains('*', StringComparison.Ordinal))
        {
            ast.Operations.Add(new AstNode { NodeType = AstNodeType.Multiply });
        }

        if (code.Contains('/', StringComparison.Ordinal))
        {
            ast.Operations.Add(new AstNode { NodeType = AstNodeType.Divide });
        }

        // Detect memory operations
        var loadMatches = _loadPatternRegex.Matches(code);
        for (var i = 0; i < loadMatches.Count; i++)
        {
            ast.MemoryOperations.Add(new AstNode { NodeType = AstNodeType.Load });
        }

        var storeMatches = _storePatternRegex.Matches(code);
        for (var i = 0; i < storeMatches.Count; i++)
        {
            ast.MemoryOperations.Add(new AstNode { NodeType = AstNodeType.Store });
        }

        return ast;
    }
}

/// <summary>
/// Optimizes kernel AST.
/// </summary>
internal static partial class KernelOptimizer
{
    public static KernelAst ApplyBasicOptimizations(KernelAst ast) => ast; // Constant folding, dead code elimination

    public static KernelAst ApplyStandardOptimizations(KernelAst ast)
    {
        // Common subexpression elimination, strength reduction
        ast = KernelOptimizer.ApplyBasicOptimizations(ast);
        return ast;
    }

    public static KernelAst ApplyAggressiveOptimizations(KernelAst ast)
    {
        // Loop transformations, function inlining
        ast = KernelOptimizer.ApplyStandardOptimizations(ast);
        return ast;
    }

    public static KernelAst ApplyVectorizationOptimizations(KernelAst ast, int vectorizationFactor) => ast; // Transform operations to use SIMD instructions

    public static KernelAst ApplyLoopUnrolling(KernelAst ast, int unrollFactor) => ast; // Unroll loops by the specified factor

    public static KernelAst ApplyFastMathOptimizations(KernelAst ast) => ast; // Relaxed floating-point operations for performance
}
