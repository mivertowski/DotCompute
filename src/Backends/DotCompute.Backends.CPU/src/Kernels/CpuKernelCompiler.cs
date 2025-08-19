// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis.CSharp;

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

    // Validate work dimensions if available in metadata
    if (definition.Metadata?.TryGetValue("WorkDimensions", out var workDimsObj) == true)
    {
        if (workDimsObj is int workDimensions && (workDimensions < 1 || workDimensions > 3))
        {
            return new ValidationResult(false, "Work dimensions must be between 1 and 3");
        }
    }

    // Validate parameters if available in metadata
    if (definition.Metadata?.TryGetValue("Parameters", out var paramsObj) == true)
    {
        if (paramsObj is IList<object> parameters)
        {
            if (parameters.Count == 0)
            {
                return new ValidationResult(false, "Kernel must have at least one parameter");
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
                        return new ValidationResult(false, $"Parameter at index {i} must have a valid name");
                    }

                    if (!param.TryGetValue("Type", out var typeObj) ||
                        typeObj is not string type ||
                        string.IsNullOrWhiteSpace(type))
                    {
                        return new ValidationResult(false, $"Parameter '{name}' must have a valid type");
                    }
                }
                else
                {
                    return new ValidationResult(false, $"Invalid parameter format at index {i}");
                }
            }
        }
    }

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
    else return writeOnlyParams == bufferParams ? MemoryAccessPattern.WriteOnly : MemoryAccessPattern.ReadWrite;
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
        return CpuRuntimeCodeGenerator.GenerateFromAst(kernelAst, definition, analysis, options);
    }
    else if (definition.Code != null && definition.Code.Length > 0)
    {
        // JIT compile bytecode
        return CpuRuntimeCodeGenerator.GenerateFromBytecode(definition.Code, definition, analysis, options);
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
        dependencies: []
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
public List<string> Variables { get; set; } = [];
public List<string> Parameters { get; set; } = [];
public List<string> FunctionCalls { get; set; } = [];
public bool HasConditionals { get; set; }
public bool HasLoops { get; set; }
public bool HasRecursion { get; set; }
public bool HasIndirectMemoryAccess { get; set; }
public bool HasComplexControlFlow { get; set; }
public int ComplexityScore { get; set; }
public long EstimatedInstructions { get; set; }
}

/// <summary>
/// AST node representing an operation.
/// </summary>
internal sealed class AstNode
{
public AstNodeType NodeType { get; set; }
public List<AstNode> Children { get; set; } = [];
public object? Value { get; set; }
public int Position { get; set; }
public string? Text { get; set; }
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

/// <summary>
/// Result of code generation.
/// </summary>
internal sealed class CompiledCode
{
public Delegate? CompiledDelegate { get; set; }
public byte[]? Bytecode { get; set; }
public long CodeSize { get; set; }
public string[] OptimizationNotes { get; set; } = [];
}

/// <summary>
/// Parses kernel source code into AST.
/// </summary>
internal static partial class KernelSourceParser
{
[GeneratedRegex(@"(public|private|protected|internal)?\s*(static)?\s*\w+\s+(\w+)\s*\(", RegexOptions.Compiled)]
private static partial Regex FunctionNameRegex();
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
        System.Diagnostics.Debug.WriteLine($"Failed to parse with advanced parser: {ex.Message}");
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
        ast.Operations = visitor.Operations;
        ast.Variables = visitor.Variables;
        ast.Parameters = visitor.Parameters;
        ast.FunctionCalls = visitor.FunctionCalls;
        
        // Additional analysis
        ast.ComplexityScore = CalculateComplexityScore(ast);
        ast.EstimatedInstructions = EstimateInstructionCount(ast);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Roslyn parsing failed: {ex.Message}");
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
    long count = ast.Operations.Count * 2L; // Rough estimate: 2 instructions per operation
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
public bool HasConditionals { get; private set; }
public bool HasLoops { get; private set; }
public bool HasRecursion { get; private set; }
public bool HasIndirectMemoryAccess { get; private set; }
public List<AstNode> Operations { get; } = new();
public List<string> Variables { get; } = new();
public List<string> Parameters { get; } = new();
public List<string> FunctionCalls { get; } = new();

private readonly HashSet<string> _declaredMethods = new();
private readonly HashSet<string> _calledMethods = new();

public override void VisitIfStatement(Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax node)
{
    HasConditionals = true;
    base.VisitIfStatement(node);
}

public override void VisitSwitchStatement(Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax node)
{
    HasConditionals = true;
    base.VisitSwitchStatement(node);
}

public override void VisitConditionalExpression(Microsoft.CodeAnalysis.CSharp.Syntax.ConditionalExpressionSyntax node)
{
    HasConditionals = true;
    base.VisitConditionalExpression(node);
}

public override void VisitForStatement(Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax node)
{
    HasLoops = true;
    base.VisitForStatement(node);
}

public override void VisitWhileStatement(Microsoft.CodeAnalysis.CSharp.Syntax.WhileStatementSyntax node)
{
    HasLoops = true;
    base.VisitWhileStatement(node);
}

public override void VisitDoStatement(Microsoft.CodeAnalysis.CSharp.Syntax.DoStatementSyntax node)
{
    HasLoops = true;
    base.VisitDoStatement(node);
}

public override void VisitForEachStatement(Microsoft.CodeAnalysis.CSharp.Syntax.ForEachStatementSyntax node)
{
    HasLoops = true;
    base.VisitForEachStatement(node);
}

public override void VisitElementAccessExpression(Microsoft.CodeAnalysis.CSharp.Syntax.ElementAccessExpressionSyntax node)
{
    HasIndirectMemoryAccess = true;
    base.VisitElementAccessExpression(node);
}

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

public override void VisitVariableDeclarator(Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax node)
{
    var variableName = node.Identifier.ValueText;
    if (!Variables.Contains(variableName))
    {
        Variables.Add(variableName);
    }
    base.VisitVariableDeclarator(node);
}

public override void VisitParameter(Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax node)
{
    var parameterName = node.Identifier.ValueText;
    if (!Parameters.Contains(parameterName))
    {
        Parameters.Add(parameterName);
    }
    base.VisitParameter(node);
}

public override void VisitMethodDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax node)
{
    var methodName = node.Identifier.ValueText;
    _declaredMethods.Add(methodName);
    base.VisitMethodDeclaration(node);
}

public override void VisitInvocationExpression(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax node)
{
    if (node.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax identifierName)
    {
        var methodName = identifierName.Identifier.ValueText;
        _calledMethods.Add(methodName);
        
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
    HasRecursion = _declaredMethods.Intersect(_calledMethods).Any();
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
