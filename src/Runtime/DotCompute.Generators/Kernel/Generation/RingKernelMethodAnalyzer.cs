// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Analyzes Ring Kernel methods to extract metadata and configuration for code generation.
/// Provides semantic analysis of Ring Kernel methods including attribute processing,
/// parameter analysis, and message-passing configuration extraction.
/// </summary>
/// <remarks>
/// Ring Kernels are persistent kernels with message-passing capabilities designed for:
/// - Real-time data processing pipelines
/// - Distributed computing with inter-kernel communication
/// - Stream processing with backpressure handling
/// - Event-driven compute workflows
///
/// This analyzer extracts configuration including:
/// - Kernel lifecycle properties (ID, capacity, queue sizes)
/// - Execution mode (Persistent vs EventDriven)
/// - Messaging strategy (SharedMemory, AtomicQueue, P2P, NCCL)
/// - Domain-specific optimizations
/// - Backend support and memory configuration
/// </remarks>
public sealed class RingKernelMethodAnalyzer
{
    /// <summary>
    /// Analyzes a Ring Kernel method and extracts comprehensive metadata.
    /// </summary>
    /// <param name="context">The generator syntax context containing the method.</param>
    /// <returns>A <see cref="RingKernelMethodInfo"/> object containing method metadata, or null if analysis fails.</returns>
    /// <remarks>
    /// This method performs comprehensive analysis of a Ring Kernel method including:
    /// - Semantic symbol extraction
    /// - Attribute processing and configuration extraction
    /// - Parameter analysis and type information
    /// - Backend compatibility determination
    /// - Message queue configuration validation
    /// </remarks>
#pragma warning disable CA1822 // Mark members as static - instance method for design consistency
    public RingKernelMethodInfo? AnalyzeRingKernelMethod(GeneratorSyntaxContext context)
#pragma warning restore CA1822
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        // Extract Ring Kernel attribute data
        var ringKernelAttribute = GetRingKernelAttribute(methodSymbol);
        if (ringKernelAttribute is null)
        {
            return null;
        }

        // Analyze Ring Kernel configuration from attributes
        var info = RingKernelAttributeAnalyzer.AnalyzeRingKernelConfiguration(ringKernelAttribute);

        // Set basic method information
        info.Name = methodSymbol.Name;
        info.ContainingType = methodSymbol.ContainingType.ToDisplayString();
        info.Namespace = methodSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : methodSymbol.ContainingNamespace.ToDisplayString();
        info.ReturnType = methodSymbol.ReturnType.ToDisplayString();
        info.MethodDeclaration = methodDeclaration;

        // Analyze method parameters
        var parameters = KernelParameterAnalyzer.AnalyzeParameters(methodSymbol);

        // Auto-generate KernelId if not provided (MUST be done before validation!)
        if (string.IsNullOrWhiteSpace(info.KernelId))
        {
            info.KernelId = GenerateDefaultKernelId(methodSymbol);
        }

        // Validate method compatibility for Ring Kernel
        if (!ValidateRingKernelCompatibility(methodSymbol, parameters, info))
        {
            return null;
        }

        // Populate parameters collection
        foreach (var param in parameters)
        {
            info.Parameters.Add(param);
        }

        // Check for EnableTelemetry attribute on the containing class
        AnalyzeEnableTelemetryAttribute(methodSymbol.ContainingType, info);

        return info;
    }

    /// <summary>
    /// Extracts the Ring Kernel attribute from a method symbol.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to examine.</param>
    /// <returns>The Ring Kernel attribute data, or null if not found.</returns>
    private static AttributeData? GetRingKernelAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes()
            .FirstOrDefault(a => IsRingKernelAttribute(a.AttributeClass));
    }

    /// <summary>
    /// Determines if an attribute class represents a Ring Kernel attribute.
    /// </summary>
    /// <param name="attributeClass">The attribute class to check.</param>
    /// <returns>True if the attribute is a Ring Kernel attribute; otherwise, false.</returns>
    private static bool IsRingKernelAttribute(INamedTypeSymbol? attributeClass)
    {
        return attributeClass?.Name is "RingKernelAttribute" or "RingKernel";
    }

    /// <summary>
    /// Validates that a method is compatible with Ring Kernel generation.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to validate.</param>
    /// <param name="parameters">The analyzed parameter information.</param>
    /// <param name="info">The Ring Kernel configuration to validate.</param>
    /// <returns>True if the method is compatible; otherwise, false.</returns>
    /// <remarks>
    /// This method performs semantic validation to ensure the method can be
    /// successfully generated for Ring Kernel execution. It checks:
    /// - Method is static (required for kernels)
    /// - Return type is void or compatible
    /// - Parameter types are supported
    /// - Method accessibility is appropriate
    /// - Ring Kernel configuration is valid
    /// </remarks>
    private static bool ValidateRingKernelCompatibility(
        IMethodSymbol methodSymbol,
        IReadOnlyList<ParameterInfo> parameters,
        RingKernelMethodInfo info)
    {
        // Must be static
        if (!methodSymbol.IsStatic)
        {
            return false;
        }

        // Must return void (Ring Kernels process messages, don't return values)
        if (methodSymbol.ReturnType.SpecialType != SpecialType.System_Void)
        {
            return false;
        }

        // All parameters must be compatible
        if (parameters.Any(p => !IsCompatibleParameterType(p)))
        {
            return false;
        }

        // Must be publicly accessible
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        // Validate Ring Kernel configuration
        var validationErrors = RingKernelAttributeAnalyzer.ValidateConfiguration(info);
        if (validationErrors.Count > 0)
        {
            // Configuration errors will be reported separately
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if a parameter type is compatible with Ring Kernel generation.
    /// </summary>
    /// <param name="parameter">The parameter information to check.</param>
    /// <returns>True if the parameter type is compatible; otherwise, false.</returns>
    private static bool IsCompatibleParameterType(ParameterInfo parameter)
    {
        // Check for RingKernelContext (unified API)
        if (IsRingKernelContextType(parameter.Type))
        {
            return true;
        }

        // Check for supported buffer types
        if (parameter.IsBuffer)
        {
            return IsSupportedBufferType(parameter.Type);
        }

        // Check for supported scalar types
        if (IsSupportedScalarType(parameter.Type))
        {
            return true;
        }

        // Allow struct message types (for unified ring kernel API)
        // Any non-primitive struct type is potentially a message type
        return IsMessageType(parameter.Type);
    }

    /// <summary>
    /// Determines if a type is RingKernelContext.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the type is RingKernelContext; otherwise, false.</returns>
    private static bool IsRingKernelContextType(string typeName)
    {
        return typeName.EndsWith("RingKernelContext", StringComparison.Ordinal) ||
               typeName == "RingKernelContext";
    }

    /// <summary>
    /// Determines if a type is likely a message type for ring kernel processing.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the type is likely a message type; otherwise, false.</returns>
    private static bool IsMessageType(string typeName)
    {
        // Exclude known non-message types
        if (typeName.Contains("System.") && !typeName.Contains("System.ValueTuple"))
        {
            return false;
        }

        // Common message type suffixes
        if (typeName.EndsWith("Message", StringComparison.Ordinal) ||
            typeName.EndsWith("Request", StringComparison.Ordinal) ||
            typeName.EndsWith("Response", StringComparison.Ordinal) ||
            typeName.EndsWith("Event", StringComparison.Ordinal) ||
            typeName.EndsWith("Command", StringComparison.Ordinal) ||
            typeName.EndsWith("Dto", StringComparison.Ordinal) ||
            typeName.EndsWith("Data", StringComparison.Ordinal))
        {
            return true;
        }

        // Allow any struct type that's not a known primitive
        // This enables custom message types without requiring specific naming conventions
        return !IsSupportedScalarType(typeName) && !typeName.Contains("Span<");
    }

    /// <summary>
    /// Determines if a buffer type is supported for Ring Kernel generation.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the buffer type is supported; otherwise, false.</returns>
    private static bool IsSupportedBufferType(string typeName)
    {
        return typeName.Contains("Span<") ||
               typeName.Contains("ReadOnlySpan<") ||
               typeName.Contains("UnifiedBuffer<") ||
               typeName.Contains("Buffer<") ||
               typeName.Contains("MessageQueue<") ||
               typeName.Contains("IMessageQueue<") ||
               typeName.EndsWith("[]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if a scalar type is supported for Ring Kernel generation.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the scalar type is supported; otherwise, false.</returns>
    private static bool IsSupportedScalarType(string typeName)
    {
        return typeName switch
        {
            "int" or "uint" or "long" or "ulong" => true,
            "float" or "double" => true,
            "byte" or "sbyte" or "short" or "ushort" => true,
            "bool" or "char" => true,
            _ => false
        };
    }

    /// <summary>
    /// Generates a default kernel ID based on the method name and containing type.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to generate an ID for.</param>
    /// <returns>A generated kernel ID.</returns>
    /// <remarks>
    /// The generated ID follows the format: "ContainingType_MethodName".
    /// This ensures uniqueness within the assembly while remaining readable.
    /// </remarks>
    private static string GenerateDefaultKernelId(IMethodSymbol methodSymbol)
    {
        var typeName = methodSymbol.ContainingType.Name;
        var methodName = methodSymbol.Name;
        return $"{typeName}_{methodName}";
    }

    /// <summary>
    /// Analyzes method body to determine if it's suitable for persistent kernel execution.
    /// </summary>
    /// <param name="methodDeclaration">The method declaration to analyze.</param>
    /// <returns>Information about Ring Kernel execution characteristics.</returns>
    public static RingKernelExecutionInfo AnalyzeExecutionCharacteristics(MethodDeclarationSyntax methodDeclaration)
    {
        var methodBody = methodDeclaration.Body?.ToString() ??
                        methodDeclaration.ExpressionBody?.ToString() ?? string.Empty;

        return new RingKernelExecutionInfo
        {
            HasMessageLoops = ContainsMessageLoops(methodDeclaration),
            HasStateManagement = ContainsStateManagement(methodBody),
            HasSynchronization = ContainsSynchronization(methodBody),
            RequiresAtomicOperations = RequiresAtomicOperations(methodBody),
            ComplexityScore = CalculateComplexityScore(methodDeclaration)
        };
    }

    /// <summary>
    /// Determines if method contains message processing loops.
    /// </summary>
    private static bool ContainsMessageLoops(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.DescendantNodes()
            .Any(n => n is WhileStatementSyntax or ForStatementSyntax);
    }

    /// <summary>
    /// Determines if method contains state management code.
    /// </summary>
    private static bool ContainsStateManagement(string methodBody)
    {
        return methodBody.Contains("static ") ||
               methodBody.Contains("ThreadLocal") ||
               methodBody.Contains("volatile");
    }

    /// <summary>
    /// Determines if method contains synchronization primitives.
    /// </summary>
    private static bool ContainsSynchronization(string methodBody)
    {
        return methodBody.Contains("lock(") ||
               methodBody.Contains("Monitor") ||
               methodBody.Contains("Semaphore") ||
               methodBody.Contains("Barrier") ||
               methodBody.Contains("ManualResetEvent");
    }

    /// <summary>
    /// Determines if method requires atomic operations.
    /// </summary>
    private static bool RequiresAtomicOperations(string methodBody)
    {
        return methodBody.Contains("Interlocked") ||
               methodBody.Contains("Atomic");
    }

    /// <summary>
    /// Calculates a complexity score for the Ring Kernel method.
    /// </summary>
    private static int CalculateComplexityScore(MethodDeclarationSyntax methodDeclaration)
    {
        var score = 1; // Base complexity

        // Count control flow statements (weighted higher for persistent kernels)
        score += methodDeclaration.DescendantNodes().Count(n =>
            n is IfStatementSyntax or SwitchStatementSyntax) * 2;

        // Count loops (critical for persistent kernels)
        score += methodDeclaration.DescendantNodes().Count(n =>
            n is ForStatementSyntax or WhileStatementSyntax) * 3;

        // Count method calls
        score += methodDeclaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count();

        return score;
    }

    /// <summary>
    /// Analyzes the EnableTelemetry attribute on the containing class and populates telemetry configuration.
    /// </summary>
    /// <param name="containingType">The containing type symbol.</param>
    /// <param name="info">The Ring Kernel method info to populate.</param>
    private static void AnalyzeEnableTelemetryAttribute(INamedTypeSymbol containingType, RingKernelMethodInfo info)
    {
        var telemetryAttribute = containingType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "EnableTelemetryAttribute" or "EnableTelemetry");

        if (telemetryAttribute is null)
        {
            info.HasEnableTelemetry = false;
            return;
        }

        info.HasEnableTelemetry = true;

        // Extract EnableTelemetry attribute properties
        foreach (var namedArg in telemetryAttribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "CollectDetailedMetrics":
                    info.TelemetryCollectDetailedMetrics = namedArg.Value.Value is true;
                    break;
                case "SamplingRate":
                    if (namedArg.Value.Value is double samplingRate)
                    {
                        info.TelemetrySamplingRate = samplingRate;
                    }
                    break;
                case "TrackMemory":
                    info.TelemetryTrackMemory = namedArg.Value.Value is true;
                    break;
                case "CustomProviderType":
                    if (namedArg.Value.Value is string customProviderType)
                    {
                        info.TelemetryCustomProviderType = customProviderType;
                    }
                    break;
            }
        }
    }
}

/// <summary>
/// Contains execution analysis results for a Ring Kernel method.
/// </summary>
public sealed class RingKernelExecutionInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether the method contains message processing loops.
    /// </summary>
    public bool HasMessageLoops { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains state management code.
    /// </summary>
    public bool HasStateManagement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains synchronization primitives.
    /// </summary>
    public bool HasSynchronization { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method requires atomic operations.
    /// </summary>
    public bool RequiresAtomicOperations { get; set; }

    /// <summary>
    /// Gets or sets the complexity score of the method.
    /// </summary>
    public int ComplexityScore { get; set; }
}
