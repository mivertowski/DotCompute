// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotCompute.Generators.Models.Kernel;
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Analyzes kernel code to determine optimal memory layouts, shared memory usage,
/// and other performance optimizations.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via reflection or dependency injection")]
internal sealed class KernelOptimizationAnalyzer
{
    private readonly SemanticModel _semanticModel;
    private readonly KernelMethodInfo _kernelInfo;
    private readonly KernelAttribute? _kernelAttribute;


    public SharedMemoryConfiguration SharedMemory { get; private set; }
    public ConstantMemoryConfiguration ConstantMemory { get; private set; }
    public TextureMemoryConfiguration TextureMemory { get; private set; }
    public MemoryCoalescingStrategy CoalescingStrategy { get; private set; }
    public CacheConfiguration CacheConfig { get; private set; }
    public OptimizationHints Optimizations { get; private set; }
    public WarpOptimizationStrategy WarpStrategy { get; private set; }

    public KernelOptimizationAnalyzer(
        SemanticModel semanticModel,

        KernelMethodInfo kernelInfo,
        KernelAttribute? kernelAttribute = null)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));
        _kernelAttribute = kernelAttribute;


        SharedMemory = new SharedMemoryConfiguration();
        ConstantMemory = new ConstantMemoryConfiguration();
        TextureMemory = new TextureMemoryConfiguration();
        CoalescingStrategy = new MemoryCoalescingStrategy();
        CacheConfig = new CacheConfiguration();
        WarpStrategy = new WarpOptimizationStrategy();


        Analyze();
    }

    private void Analyze()
    {
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return;
        }

        // Extract optimization hints from attribute

        if (_kernelAttribute != null)
        {
            Optimizations = _kernelAttribute.Optimizations;
            AnalyzeMemoryPattern(_kernelAttribute.MemoryPattern);
        }

        // Analyze the method body

        var dataFlow = _semanticModel.AnalyzeDataFlow(_kernelInfo.MethodDeclaration.Body);


        if (dataFlow != null)
        {
            AnalyzeSharedMemoryUsage(dataFlow);
            AnalyzeConstantMemoryUsage(dataFlow);
        }
        AnalyzeMemoryAccessPatterns(_kernelInfo.MethodDeclaration.Body);
        AnalyzeLoopPatterns(_kernelInfo.MethodDeclaration.Body);
        AnalyzeWarpDivergence(_kernelInfo.MethodDeclaration.Body);
        DetermineCacheConfiguration();
    }

    private void AnalyzeMemoryPattern(MemoryAccessPattern pattern)
    {
        switch (pattern)
        {
            case MemoryAccessPattern.Sequential:
                CoalescingStrategy.EnableCoalescing = true;
                CoalescingStrategy.PrefetchDistance = 2;
                CacheConfig.PreferL1 = false;
                CacheConfig.PreferShared = true;
                break;


            case MemoryAccessPattern.Strided:
                CoalescingStrategy.EnableCoalescing = false;
                CoalescingStrategy.UseTextureMemory = true;
                CacheConfig.PreferL1 = true;
                break;


            case MemoryAccessPattern.Random:
                CoalescingStrategy.EnableCoalescing = false;
                CacheConfig.PreferL1 = true;
                CacheConfig.UseReadOnlyCache = true;
                break;


            case MemoryAccessPattern.Tiled:
                SharedMemory.UseTiling = true;
                SharedMemory.TileSize = DetermineOptimalTileSize();
                break;

            // Additional patterns

            default:
                break;
        }
    }

    private void AnalyzeSharedMemoryUsage(DataFlowAnalysis dataFlow)
    {
        // Find arrays and buffers that are frequently accessed
        var frequentlyAccessedArrays = new Dictionary<string, int>();


        if (_kernelInfo.MethodDeclaration?.Body != null)
        {
            var arrayAccesses = _kernelInfo.MethodDeclaration.Body
                .DescendantNodes()
                .OfType<ElementAccessExpressionSyntax>();


            foreach (var access in arrayAccesses)
            {
                if (access.Expression is IdentifierNameSyntax identifier)
                {
                    var name = identifier.Identifier.Text;
                    if (!frequentlyAccessedArrays.TryGetValue(name, out var count))
                    {
                        count = 0;
                    }

                    frequentlyAccessedArrays[name] = count + 1;
                }
            }
        }

        // Determine which arrays should use shared memory

        foreach (var kvp in frequentlyAccessedArrays)
        {
            var arrayName = kvp.Key;
            var accessCount = kvp.Value;
            if (accessCount > 3) // Threshold for shared memory
            {
                var variable = dataFlow.VariablesDeclared.FirstOrDefault(v => v.Name == arrayName);
                if (variable != null)
                {
                    var size = EstimateArraySize(variable);
                    if (size > 0 && size <= 48 * 1024) // 48KB shared memory limit
                    {
                        SharedMemory.Variables.Add(new SharedMemoryVariable
                        {
                            Name = arrayName,
                            Type = variable.GetTypeDisplayString(),
                            SizeInBytes = size,
                            AccessPattern = DetermineAccessPattern(arrayName)
                        });
                    }
                }
            }
        }

        // Check for reduction patterns

        if (HasReductionPattern())
        {
            SharedMemory.UseForReduction = true;
            SharedMemory.ReductionBufferSize = 256 * sizeof(float); // Default size
        }

        // Check for matrix tiling

        if (HasMatrixOperations())
        {
            SharedMemory.UseTiling = true;
            SharedMemory.TileSize = DetermineOptimalTileSize();
        }
    }

    private void AnalyzeConstantMemoryUsage(DataFlowAnalysis dataFlow)
    {
        // Find read-only parameters and constants
        foreach (var param in _kernelInfo.Parameters)
        {
            if (param.IsReadOnly && !param.IsBuffer)
            {
                // Scalar constants are good candidates
                ConstantMemory.Variables.Add(new ConstantMemoryVariable
                {
                    Name = param.Name,
                    Type = param.Type,
                    IsParameter = true
                });
            }
            else if (param.IsReadOnly && param.IsBuffer)
            {
                // Small read-only buffers can use constant memory (64KB limit)
                var estimatedSize = EstimateParameterSize(param);
                if (estimatedSize > 0 && estimatedSize <= 64 * 1024)
                {
                    ConstantMemory.Variables.Add(new ConstantMemoryVariable
                    {
                        Name = param.Name,
                        Type = param.Type,
                        IsParameter = true,
                        SizeInBytes = estimatedSize
                    });
                }
            }
        }


        ConstantMemory.Enabled = ConstantMemory.Variables.Count > 0;
    }

    private void AnalyzeMemoryAccessPatterns(BlockSyntax body)
    {
        var elementAccesses = body.DescendantNodes().OfType<ElementAccessExpressionSyntax>();


        foreach (var access in elementAccesses)
        {
            // Analyze index expressions
            foreach (var arg in access.ArgumentList.Arguments)
            {
                var indexExpr = arg.Expression;

                // Check for coalesced access pattern (consecutive threads access consecutive memory)

                if (IsCoalescedAccess(indexExpr))
                {
                    CoalescingStrategy.EnableCoalescing = true;
                    CoalescingStrategy.CoalescedLoads++;
                }

                // Check for strided access

                if (IsStridedAccess(indexExpr))
                {
                    CoalescingStrategy.StrideSize = DetectStrideSize(indexExpr);
                    if (CoalescingStrategy.StrideSize > 1)
                    {
                        CoalescingStrategy.UseTextureMemory = true;
                    }
                }

                // Check for 2D/3D access patterns

                if (Is2DAccessPattern(indexExpr))
                {
                    TextureMemory.Use2DTexture = true;
                }
            }
        }
    }

    private void AnalyzeLoopPatterns(BlockSyntax body)
    {
        var loops = body.DescendantNodes().OfType<ForStatementSyntax>();


        foreach (var loop in loops)
        {
            // Check for unrollable loops
            if (IsUnrollableLoop(loop))
            {
                // Loop unrolling optimization enabled

                // Determine unroll factor

                var unrollFactor = DetermineUnrollFactor(loop);
                if (unrollFactor > 1)
                {
                    // Store unroll factor for code generation
                    SharedMemory.LoopUnrollFactors[GetLoopIdentifier(loop)] = unrollFactor;
                }
            }

            // Check for vectorizable loops

            if (IsVectorizableLoop(loop))
            {
                // Vectorization optimization enabled
            }
        }
    }

    private void AnalyzeWarpDivergence(BlockSyntax body)
    {
        var ifStatements = body.DescendantNodes().OfType<IfStatementSyntax>();


        foreach (var ifStmt in ifStatements)
        {
            // Check if condition depends on thread ID
            if (ContainsThreadDependentCondition(ifStmt.Condition))
            {
                WarpStrategy.HasDivergence = true;

                // Analyze branch complexity

                var thenComplexity = EstimateBranchComplexity(ifStmt.Statement);
                var elseComplexity = ifStmt.Else != null ?

                    EstimateBranchComplexity(ifStmt.Else.Statement) : 0;


                if (Math.Abs(thenComplexity - elseComplexity) > 10)
                {
                    WarpStrategy.RequiresPredication = true;
                }
            }
        }

        // Determine warp-level optimizations

        if (!WarpStrategy.HasDivergence)
        {
            WarpStrategy.UseWarpShuffle = true;
            WarpStrategy.UseWarpVote = true;
        }
    }

    private void DetermineCacheConfiguration()
    {
        // Based on memory access patterns, determine optimal cache configuration


        if (SharedMemory.Variables.Count > 0)
        {
            // If using significant shared memory, prefer shared over L1
            var sharedMemUsage = SharedMemory.Variables.Sum(v => v.SizeInBytes);
            if (sharedMemUsage > 16 * 1024) // More than 16KB
            {
                CacheConfig.PreferShared = true;
                CacheConfig.PreferL1 = false;
            }
        }


        if (CoalescingStrategy.EnableCoalescing && CoalescingStrategy.CoalescedLoads > 10)
        {
            // Good memory coalescing, L1 cache helps
            CacheConfig.PreferL1 = true;
        }


        if (TextureMemory.Use2DTexture || TextureMemory.Use3DTexture)
        {
            // Texture memory has its own cache
            CacheConfig.UseTextureCache = true;
        }

        // Read-only data can use read-only cache

        if (ConstantMemory.Variables.Count > 0 ||

            _kernelInfo.Parameters.Any(p => p.IsReadOnly))
        {
            CacheConfig.UseReadOnlyCache = true;
        }
    }

    private bool HasReductionPattern()
    {
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return false;
        }

        // Look for accumulation patterns

        var assignments = _kernelInfo.MethodDeclaration.Body
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>();


        return assignments.Any(a =>

            a.Kind() == SyntaxKind.AddAssignmentExpression ||
            a.Kind() == SyntaxKind.MultiplyAssignmentExpression);
    }

    private bool HasMatrixOperations()
    {
        return _kernelInfo.Name.IndexOf("Matrix", StringComparison.OrdinalIgnoreCase) >= 0 ||
               _kernelInfo.Name.IndexOf("Transpose", StringComparison.OrdinalIgnoreCase) >= 0 ||
               _kernelInfo.Name.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int DetermineOptimalTileSize()
    {
        // Heuristic for tile size based on shared memory availability
        // and warp size considerations
        if (HasMatrixOperations())
        {
            return 16; // Good for tensor cores and warp-level operations
        }
        return 32; // Default tile size
    }

    private static bool IsCoalescedAccess(ExpressionSyntax indexExpr)
    {
        // Check if index contains threadIdx.x or similar patterns
        var text = indexExpr.ToString();
        return text.Contains("threadIdx.x") ||

               text.Contains("idx") ||

               text.Contains("i");
    }

    private static bool IsStridedAccess(ExpressionSyntax indexExpr)
    {
        // Check for multiplication in index
        return indexExpr is BinaryExpressionSyntax binary &&
               binary.Kind() == SyntaxKind.MultiplyExpression;
    }

    private static bool Is2DAccessPattern(ExpressionSyntax indexExpr)
    {
        // Check for row*width+col pattern
        var text = indexExpr.ToString();
        return text.Contains("*") && text.Contains("+");
    }

    private static int DetectStrideSize(ExpressionSyntax indexExpr)
    {
        // Simple heuristic - look for constant multipliers
        if (indexExpr is BinaryExpressionSyntax binary &&
            binary.Kind() == SyntaxKind.MultiplyExpression)
        {
            if (binary.Right is LiteralExpressionSyntax literal &&
                literal.Token.Value is int stride)
            {
                return stride;
            }
        }
        return 1;
    }

    private static bool IsUnrollableLoop(ForStatementSyntax loop)
    {
        // Check if loop has constant bounds and simple body
        if (loop.Condition is BinaryExpressionSyntax condition)
        {
            // Check for constant upper bound
            if (condition.Right is LiteralExpressionSyntax)
            {
                // Check if loop body is simple enough
                var statements = loop.Statement switch
                {
                    BlockSyntax block => block.Statements.Count,
                    _ => 1
                };


                return statements <= 8; // Don't unroll large loops
            }
        }
        return false;
    }

    private static int DetermineUnrollFactor(ForStatementSyntax loop)
    {
        // Determine optimal unroll factor based on loop characteristics
        if (loop.Condition is BinaryExpressionSyntax condition &&
            condition.Right is LiteralExpressionSyntax literal &&
            literal.Token.Value is int bound)
        {
            if (bound <= 4)
            {
                return bound; // Fully unroll small loops
            }

            if (bound <= 16)
            {
                return 4;    // Partial unroll
            }


            if (bound <= 64)
            {
                return 8;    // Larger partial unroll
            }
        }
        return 1; // No unrolling
    }

    private static bool IsVectorizableLoop(ForStatementSyntax loop)
    {
        // Check if loop body contains vectorizable operations
        if (loop.Statement is BlockSyntax block)
        {
            var expressions = block.DescendantNodes().OfType<BinaryExpressionSyntax>();
            return expressions.Any(e =>

                e.Kind() == SyntaxKind.AddExpression ||
                e.Kind() == SyntaxKind.MultiplyExpression ||
                e.Kind() == SyntaxKind.SubtractExpression);
        }
        return false;
    }

    private static string GetLoopIdentifier(ForStatementSyntax loop)
    {
        // Generate unique identifier for loop
        var span = loop.GetLocation().GetLineSpan();
        return $"loop_{span.StartLinePosition.Line}";
    }

    private static bool ContainsThreadDependentCondition(ExpressionSyntax condition)
    {
        var text = condition.ToString();
        return text.Contains("threadIdx") ||

               text.Contains("blockIdx") ||
               text.Contains("idx") ||
               text.Contains("%"); // Modulo often indicates thread-dependent branching
    }

    private static int EstimateBranchComplexity(StatementSyntax statement)
        // Simple complexity metric based on node count
        => statement.DescendantNodes().Count();

    private static int EstimateArraySize(ISymbol variable)
    {
        var type = variable.GetTypeDisplayString();

        // Try to extract array size from type

        if (type.Contains("[") && type.Contains("]"))
        {
            var sizeStr = type.Substring(type.IndexOf('[') + 1, type.IndexOf(']') - type.IndexOf('[') - 1);
            if (int.TryParse(sizeStr, out var size))
            {
                return size * GetElementSize(type);
            }
        }


        return 0; // Unknown size
    }

    private static int EstimateParameterSize(ParameterInfo param)
    {
        // Conservative estimate based on type
        if (param.Type.Contains("float"))
        {
            return 1024 * sizeof(float);
        }


        if (param.Type.Contains("double"))
        {
            return 512 * sizeof(double);
        }


        if (param.Type.Contains("int"))
        {
            return 1024 * sizeof(int);
        }


        return 0;
    }

    private static int GetElementSize(string type)
    {
        if (type.Contains("float"))
        {
            return sizeof(float);
        }


        if (type.Contains("double"))
        {
            return sizeof(double);
        }


        if (type.Contains("int"))
        {
            return sizeof(int);
        }


        if (type.Contains("byte"))
        {
            return sizeof(byte);
        }


        return sizeof(float); // Default
    }

    private string DetermineAccessPattern(string arrayName)
    {
        // Analyze how the array is accessed in the kernel
        // This is a simplified heuristic TODO
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return "unknown";
        }


        var accesses = _kernelInfo.MethodDeclaration.Body
            .DescendantNodes()
            .OfType<ElementAccessExpressionSyntax>()
            .Where(e => e.Expression.ToString() == arrayName);


        foreach (var access in accesses)
        {
            var indexExpr = access.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (indexExpr != null && IsCoalescedAccess(indexExpr))
            {
                return "coalesced";
            }
        }


        return "random";
    }
}

// Configuration classes for different memory types

internal sealed class SharedMemoryConfiguration
{
    public List<SharedMemoryVariable> Variables { get; } = [];
    public bool UseTiling { get; set; }
    public int TileSize { get; set; }
    public bool UseForReduction { get; set; }
    public int ReductionBufferSize { get; set; }
    public Dictionary<string, int> LoopUnrollFactors { get; } = [];


    public int TotalSizeInBytes => Variables.Sum(v => v.SizeInBytes);
}

internal sealed class SharedMemoryVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int SizeInBytes { get; set; }
    public string AccessPattern { get; set; } = "unknown";
}

internal sealed class ConstantMemoryConfiguration
{
    public bool Enabled { get; set; }
    public List<ConstantMemoryVariable> Variables { get; } = [];


    public int TotalSizeInBytes => Variables.Sum(v => v.SizeInBytes);
}

internal sealed class ConstantMemoryVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsParameter { get; set; }
    public int SizeInBytes { get; set; }
}

internal sealed class TextureMemoryConfiguration
{
    public bool Use1DTexture { get; set; }
    public bool Use2DTexture { get; set; }
    public bool Use3DTexture { get; set; }
    public List<string> TextureArrays { get; } = [];
}

internal sealed class MemoryCoalescingStrategy
{
    public bool EnableCoalescing { get; set; }
    public int CoalescedLoads { get; set; }
    public int StrideSize { get; set; } = 1;
    public bool UseTextureMemory { get; set; }
    public int PrefetchDistance { get; set; }
}

internal sealed class CacheConfiguration
{
    public bool PreferL1 { get; set; }
    public bool PreferShared { get; set; }
    public bool UseReadOnlyCache { get; set; }
    public bool UseTextureCache { get; set; }


    public string GetCudaCacheConfig()
    {
        if (PreferShared)
        {
            return "cudaFuncCachePreferShared";
        }


        if (PreferL1)
        {
            return "cudaFuncCachePreferL1";
        }


        return "cudaFuncCachePreferNone";
    }
}

internal sealed class WarpOptimizationStrategy
{
    public bool HasDivergence { get; set; }
    public bool RequiresPredication { get; set; }
    public bool UseWarpShuffle { get; set; }
    public bool UseWarpVote { get; set; }
    public bool UseCooperativeGroups { get; set; }
}