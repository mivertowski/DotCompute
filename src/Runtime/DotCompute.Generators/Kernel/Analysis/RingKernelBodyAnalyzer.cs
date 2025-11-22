// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Analyzes ring kernel method bodies to extract feature requirements and prepare for CUDA translation.
/// </summary>
/// <remarks>
/// This analyzer walks the C# syntax tree of a ring kernel method and identifies:
/// <list type="bullet">
/// <item><description>RingKernelContext API calls (barriers, temporal, K2K messaging)</description></item>
/// <item><description>Local variable declarations and types</description></item>
/// <item><description>Control flow constructs (if/for/while)</description></item>
/// <item><description>Return statements and output serialization needs</description></item>
/// <item><description>Atomic operations and memory ordering requirements</description></item>
/// </list>
/// </remarks>
public sealed class RingKernelBodyAnalyzer : CSharpSyntaxWalker
{
    private readonly SemanticModel? _semanticModel;
    private readonly List<string> _contextApiCalls = new();
    private readonly List<LocalVariableInfo> _localVariables = new();
    private readonly HashSet<string> _targetKernels = new();
    private readonly HashSet<string> _targetTopics = new();
    private readonly HashSet<string> _namedBarriers = new();
    private bool _usesBarriers;
    private bool _usesTimestamps;
    private bool _usesK2KMessaging;
    private bool _usesPubSub;
    private bool _usesAtomics;
    private bool _usesWarpPrimitives;
    private bool _usesMemoryFences;
    private int _maxLoopDepth;
    private int _currentLoopDepth;

    /// <summary>
    /// Initializes a new instance of the analyzer.
    /// </summary>
    /// <param name="semanticModel">Optional semantic model for type resolution.</param>
    public RingKernelBodyAnalyzer(SemanticModel? semanticModel = null)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Analyzes a ring kernel method and returns the analysis result.
    /// </summary>
    /// <param name="methodDeclaration">The method syntax to analyze.</param>
    /// <param name="semanticModel">Optional semantic model for type resolution.</param>
    /// <returns>The analysis result containing feature requirements.</returns>
    public static RingKernelBodyAnalysisResult Analyze(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel? semanticModel = null)
    {
        var analyzer = new RingKernelBodyAnalyzer(semanticModel);
        analyzer.Visit(methodDeclaration);
        return analyzer.BuildResult(methodDeclaration);
    }

    /// <inheritdoc/>
    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Detect RingKernelContext API calls
        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            var targetName = GetTargetName(memberAccess.Expression);

            // Check if calling on 'ctx' or similar context parameter
            if (IsContextParameter(targetName))
            {
                _contextApiCalls.Add(methodName);
                ClassifyContextCall(methodName, node);
            }
        }

        base.VisitInvocationExpression(node);
    }

    /// <inheritdoc/>
    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            var typeName = node.Declaration.Type.ToString();
            var varName = variable.Identifier.Text;
            var hasInitializer = variable.Initializer != null;

            _localVariables.Add(new LocalVariableInfo(varName, typeName, hasInitializer));
        }

        base.VisitLocalDeclarationStatement(node);
    }

    /// <inheritdoc/>
    public override void VisitForStatement(ForStatementSyntax node)
    {
        _currentLoopDepth++;
        _maxLoopDepth = Math.Max(_maxLoopDepth, _currentLoopDepth);

        base.VisitForStatement(node);

        _currentLoopDepth--;
    }

    /// <inheritdoc/>
    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        _currentLoopDepth++;
        _maxLoopDepth = Math.Max(_maxLoopDepth, _currentLoopDepth);

        base.VisitWhileStatement(node);

        _currentLoopDepth--;
    }

    /// <inheritdoc/>
    public override void VisitDoStatement(DoStatementSyntax node)
    {
        _currentLoopDepth++;
        _maxLoopDepth = Math.Max(_maxLoopDepth, _currentLoopDepth);

        base.VisitDoStatement(node);

        _currentLoopDepth--;
    }

    private void ClassifyContextCall(string methodName, InvocationExpressionSyntax node)
    {
        switch (methodName)
        {
            // Barriers
            case "SyncThreads":
            case "SyncGrid":
            case "SyncWarp":
                _usesBarriers = true;
                break;

            case "NamedBarrier":
                _usesBarriers = true;
                var barrierArg = GetFirstStringArgument(node);
                if (barrierArg != null)
                {
                    _namedBarriers.Add(barrierArg);
                }
                break;

            // Temporal
            case "Now":
            case "Tick":
            case "UpdateClock":
                _usesTimestamps = true;
                break;

            // Memory fences
            case "ThreadFence":
            case "ThreadFenceBlock":
            case "ThreadFenceSystem":
                _usesMemoryFences = true;
                break;

            // K2K Messaging
            case "SendToKernel":
                _usesK2KMessaging = true;
                var targetKernel = GetFirstStringArgument(node);
                if (targetKernel != null)
                {
                    _targetKernels.Add(targetKernel);
                }
                break;

            case "TryReceiveFromKernel":
            case "GetPendingMessageCount":
                _usesK2KMessaging = true;
                var sourceKernel = GetFirstStringArgument(node);
                if (sourceKernel != null)
                {
                    _targetKernels.Add(sourceKernel);
                }
                break;

            // Pub/Sub
            case "PublishToTopic":
                _usesPubSub = true;
                var pubTopic = GetFirstStringArgument(node);
                if (pubTopic != null)
                {
                    _targetTopics.Add(pubTopic);
                }
                break;

            case "TryReceiveFromTopic":
                _usesPubSub = true;
                var subTopic = GetFirstStringArgument(node);
                if (subTopic != null)
                {
                    _targetTopics.Add(subTopic);
                }
                break;

            // Atomics
            case "AtomicAdd":
            case "AtomicCAS":
            case "AtomicExch":
            case "AtomicMin":
            case "AtomicMax":
                _usesAtomics = true;
                break;

            // Warp primitives
            case "WarpShuffle":
            case "WarpShuffleDown":
            case "WarpReduce":
            case "WarpBallot":
            case "WarpAll":
            case "WarpAny":
                _usesWarpPrimitives = true;
                break;
        }
    }

    private static string? GetTargetName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }

    private static bool IsContextParameter(string? name)
    {
        if (name == null)
        {
            return false;
        }

        // Common context parameter names
        return name is "ctx" or "context" or "kernelContext" or "ringContext";
    }

    private static string? GetFirstStringArgument(InvocationExpressionSyntax node)
    {
        var firstArg = node.ArgumentList.Arguments.FirstOrDefault();
        if (firstArg?.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }
        return null;
    }

    private RingKernelBodyAnalysisResult BuildResult(MethodDeclarationSyntax methodDeclaration)
    {
        // Extract input/output types from method signature
        var inputType = ExtractInputType(methodDeclaration);
        var outputType = ExtractOutputType(methodDeclaration);
        var methodBody = methodDeclaration.Body?.ToFullString() ??
                        methodDeclaration.ExpressionBody?.Expression.ToFullString() ?? "";

        return new RingKernelBodyAnalysisResult
        {
            MethodName = methodDeclaration.Identifier.Text,
            InputTypeName = inputType,
            OutputTypeName = outputType,
            MethodBodySource = methodBody,
            ContextApiCalls = _contextApiCalls.ToImmutableArray(),
            LocalVariables = _localVariables.ToImmutableArray(),
            UsesBarriers = _usesBarriers,
            UsesTimestamps = _usesTimestamps,
            UsesK2KMessaging = _usesK2KMessaging,
            UsesPubSub = _usesPubSub,
            UsesAtomics = _usesAtomics,
            UsesWarpPrimitives = _usesWarpPrimitives,
            UsesMemoryFences = _usesMemoryFences,
            TargetKernels = _targetKernels.ToImmutableHashSet(),
            TargetTopics = _targetTopics.ToImmutableHashSet(),
            NamedBarriers = _namedBarriers.ToImmutableHashSet(),
            MaxLoopDepth = _maxLoopDepth
        };
    }

    private static string? ExtractInputType(MethodDeclarationSyntax method)
    {
        // Find first parameter that isn't RingKernelContext
        foreach (var param in method.ParameterList.Parameters)
        {
            var typeName = param.Type?.ToString();
            if (typeName != null && !typeName.Contains("RingKernelContext"))
            {
                return typeName;
            }
        }
        return null;
    }

    private static string? ExtractOutputType(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType.ToString();
        return returnType is "void" or "Void" ? null : returnType;
    }
}

/// <summary>
/// Result of analyzing a ring kernel method body.
/// </summary>
public sealed class RingKernelBodyAnalysisResult
{
    /// <summary>
    /// The name of the analyzed method.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// The input message type name (first non-context parameter), or null if void/none.
    /// </summary>
    public string? InputTypeName { get; set; }

    /// <summary>
    /// The output message type name (return type), or null if void.
    /// </summary>
    public string? OutputTypeName { get; set; }

    /// <summary>
    /// The source code of the method body for CUDA translation.
    /// </summary>
    public string MethodBodySource { get; set; } = string.Empty;

    /// <summary>
    /// List of RingKernelContext API methods called.
    /// </summary>
    public ImmutableArray<string> ContextApiCalls { get; set; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Local variables declared in the method.
    /// </summary>
    public ImmutableArray<LocalVariableInfo> LocalVariables { get; set; } = ImmutableArray<LocalVariableInfo>.Empty;

    /// <summary>
    /// Whether the method uses barrier synchronization.
    /// </summary>
    public bool UsesBarriers { get; set; }

    /// <summary>
    /// Whether the method uses HLC timestamps.
    /// </summary>
    public bool UsesTimestamps { get; set; }

    /// <summary>
    /// Whether the method uses kernel-to-kernel messaging.
    /// </summary>
    public bool UsesK2KMessaging { get; set; }

    /// <summary>
    /// Whether the method uses pub/sub messaging.
    /// </summary>
    public bool UsesPubSub { get; set; }

    /// <summary>
    /// Whether the method uses atomic operations.
    /// </summary>
    public bool UsesAtomics { get; set; }

    /// <summary>
    /// Whether the method uses warp-level primitives.
    /// </summary>
    public bool UsesWarpPrimitives { get; set; }

    /// <summary>
    /// Whether the method uses memory fences.
    /// </summary>
    public bool UsesMemoryFences { get; set; }

    /// <summary>
    /// Kernel IDs referenced in K2K messaging calls.
    /// </summary>
    public ImmutableHashSet<string> TargetKernels { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Topic names referenced in pub/sub calls.
    /// </summary>
    public ImmutableHashSet<string> TargetTopics { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Named barriers referenced in the method.
    /// </summary>
    public ImmutableHashSet<string> NamedBarriers { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Maximum nesting depth of loops in the method.
    /// </summary>
    public int MaxLoopDepth { get; set; }

    /// <summary>
    /// Whether the method requires any advanced GPU features.
    /// </summary>
    public bool RequiresAdvancedFeatures => UsesBarriers || UsesK2KMessaging || UsesPubSub || UsesAtomics || UsesWarpPrimitives;
}

/// <summary>
/// Information about a local variable in a ring kernel method.
/// </summary>
public sealed class LocalVariableInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalVariableInfo"/> class.
    /// </summary>
    public LocalVariableInfo(string name, string typeName, bool hasInitializer)
    {
        Name = name;
        TypeName = typeName;
        HasInitializer = hasInitializer;
    }

    /// <summary>The variable name.</summary>
    public string Name { get; }

    /// <summary>The variable type name.</summary>
    public string TypeName { get; }

    /// <summary>Whether the variable has an initializer.</summary>
    public bool HasInitializer { get; }
}
