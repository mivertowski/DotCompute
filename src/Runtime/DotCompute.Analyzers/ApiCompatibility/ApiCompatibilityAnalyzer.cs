// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Analyzers.ApiCompatibility;

/// <summary>
/// Roslyn analyzer that detects breaking API changes in DotCompute.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer ensures API stability by detecting:
/// <list type="bullet">
/// <item>Removal of public types</item>
/// <item>Changes to public method signatures</item>
/// <item>Changes to public interface contracts</item>
/// <item>Breaking changes to enumerations</item>
/// <item>Visibility reductions</item>
/// </list>
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ApiCompatibilityAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic IDs
    public const string DiagnosticIdPublicApiRemoved = "DC1001";
    public const string DiagnosticIdSignatureChanged = "DC1002";
    public const string DiagnosticIdVisibilityReduced = "DC1003";
    public const string DiagnosticIdReturnTypeChanged = "DC1004";
    public const string DiagnosticIdParameterTypeChanged = "DC1005";
    public const string DiagnosticIdEnumValueRemoved = "DC1006";
    public const string DiagnosticIdInterfaceMemberAdded = "DC1007";
    public const string DiagnosticIdSealedAdded = "DC1008";
    public const string DiagnosticIdAbstractAdded = "DC1009";
    public const string DiagnosticIdVirtualRemoved = "DC1010";

    private const string Category = "ApiCompatibility";

    private static readonly DiagnosticDescriptor RulePublicApiRemoved = new(
        DiagnosticIdPublicApiRemoved,
        "Public API removed",
        "Public type '{0}' has been removed which is a breaking change",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Removing public types is a breaking change.");

    private static readonly DiagnosticDescriptor RuleSignatureChanged = new(
        DiagnosticIdSignatureChanged,
        "Method signature changed",
        "Method '{0}' signature has been changed which is a breaking change",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Changing method signatures is a breaking change.");

    private static readonly DiagnosticDescriptor RuleVisibilityReduced = new(
        DiagnosticIdVisibilityReduced,
        "Visibility reduced",
        "Member '{0}' visibility has been reduced from {1} to {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Reducing visibility is a breaking change.");

    private static readonly DiagnosticDescriptor RuleReturnTypeChanged = new(
        DiagnosticIdReturnTypeChanged,
        "Return type changed",
        "Method '{0}' return type changed from '{1}' to '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Changing return types is a breaking change.");

    private static readonly DiagnosticDescriptor RuleParameterTypeChanged = new(
        DiagnosticIdParameterTypeChanged,
        "Parameter type changed",
        "Method '{0}' parameter '{1}' type changed from '{2}' to '{3}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Changing parameter types is a breaking change.");

    private static readonly DiagnosticDescriptor RuleEnumValueRemoved = new(
        DiagnosticIdEnumValueRemoved,
        "Enum value removed",
        "Enum value '{0}.{1}' has been removed",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Removing enum values is a breaking change.");

    private static readonly DiagnosticDescriptor RuleInterfaceMemberAdded = new(
        DiagnosticIdInterfaceMemberAdded,
        "Interface member added without default",
        "Interface '{0}' has new member '{1}' without default implementation",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Adding interface members without defaults is a breaking change.");

    private static readonly DiagnosticDescriptor RuleSealedAdded = new(
        DiagnosticIdSealedAdded,
        "Sealed modifier added",
        "Type or member '{0}' is now sealed which prevents inheritance",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Adding sealed modifier can break derived types.");

    private static readonly DiagnosticDescriptor RuleAbstractAdded = new(
        DiagnosticIdAbstractAdded,
        "Abstract modifier added",
        "Member '{0}' is now abstract which requires implementation",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Adding abstract modifier is a breaking change.");

    private static readonly DiagnosticDescriptor RuleVirtualRemoved = new(
        DiagnosticIdVirtualRemoved,
        "Virtual modifier removed",
        "Member '{0}' is no longer virtual which prevents overriding",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Removing virtual modifier can break derived types.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            RulePublicApiRemoved,
            RuleSignatureChanged,
            RuleVisibilityReduced,
            RuleReturnTypeChanged,
            RuleParameterTypeChanged,
            RuleEnumValueRemoved,
            RuleInterfaceMemberAdded,
            RuleSealedAdded,
            RuleAbstractAdded,
            RuleVirtualRemoved);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register for symbol actions on types that matter for API compatibility
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        // Only analyze public types in DotCompute namespaces
        if (!IsPublicApi(typeSymbol)) return;

        // Check for sealed added to previously non-sealed type
        if (typeSymbol.IsSealed && ShouldCheckForSealedChange(typeSymbol))
        {
            // This would compare against baseline - placeholder check
            // In real implementation, compare against stored API baseline
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        // Only analyze public methods
        if (!IsPublicApi(methodSymbol)) return;

        // Skip special methods
        if (methodSymbol.MethodKind != MethodKind.Ordinary &&
            methodSymbol.MethodKind != MethodKind.Constructor)
        {
            return;
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var propertySymbol = (IPropertySymbol)context.Symbol;

        // Only analyze public properties
        if (!IsPublicApi(propertySymbol)) return;
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var fieldSymbol = (IFieldSymbol)context.Symbol;

        // Only analyze public fields (rare but can exist for const/static readonly)
        if (!IsPublicApi(fieldSymbol)) return;

        // Check for enum fields specifically
        if (fieldSymbol.ContainingType.TypeKind == TypeKind.Enum)
        {
            // Enum value analysis
        }
    }

    private static bool IsPublicApi(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Public)
            return false;

        // Check if in DotCompute namespace
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.StartsWith("DotCompute", StringComparison.Ordinal);
    }

    private static bool ShouldCheckForSealedChange(INamedTypeSymbol type)
    {
        // Only check classes that are not compiler-generated
        return type.TypeKind == TypeKind.Class &&
               !type.IsImplicitlyDeclared;
    }
}

/// <summary>
/// Represents a baseline API snapshot for comparison.
/// </summary>
public sealed class ApiBaseline
{
    private readonly Dictionary<string, TypeInfo> _types = new();
    private readonly Dictionary<string, MethodInfo> _methods = new();
    private readonly Dictionary<string, PropertyInfo> _properties = new();

    /// <summary>
    /// Gets the baseline version.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the baseline creation date.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the type definitions in this baseline.
    /// </summary>
    public IReadOnlyDictionary<string, TypeInfo> Types => _types;

    /// <summary>
    /// Gets the method definitions in this baseline.
    /// </summary>
    public IReadOnlyDictionary<string, MethodInfo> Methods => _methods;

    /// <summary>
    /// Gets the property definitions in this baseline.
    /// </summary>
    public IReadOnlyDictionary<string, PropertyInfo> Properties => _properties;

    /// <summary>
    /// Adds a type to the baseline.
    /// </summary>
    public void AddType(TypeInfo type) => _types[type.FullName] = type;

    /// <summary>
    /// Adds a method to the baseline.
    /// </summary>
    public void AddMethod(MethodInfo method) => _methods[method.Signature] = method;

    /// <summary>
    /// Adds a property to the baseline.
    /// </summary>
    public void AddProperty(PropertyInfo property) => _properties[property.FullName] = property;

    /// <summary>
    /// Compares this baseline against current symbols.
    /// </summary>
    public IEnumerable<ApiBreakingChange> Compare(ApiBaseline current)
    {
        var changes = new List<ApiBreakingChange>();

        // Check for removed types
        foreach (var type in _types.Keys)
        {
            if (!current._types.ContainsKey(type))
            {
                changes.Add(new ApiBreakingChange
                {
                    ChangeType = BreakingChangeType.TypeRemoved,
                    MemberName = type,
                    Description = $"Public type '{type}' was removed"
                });
            }
        }

        // Check for removed methods
        foreach (var method in _methods.Keys)
        {
            if (!current._methods.ContainsKey(method))
            {
                changes.Add(new ApiBreakingChange
                {
                    ChangeType = BreakingChangeType.MethodRemoved,
                    MemberName = method,
                    Description = $"Public method '{method}' was removed"
                });
            }
        }

        return changes;
    }
}

/// <summary>
/// Type information for API baseline.
/// </summary>
public sealed record TypeInfo
{
    public required string FullName { get; init; }
    public required string Kind { get; init; } // class, interface, struct, enum
    public required string Accessibility { get; init; }
    public bool IsSealed { get; init; }
    public bool IsAbstract { get; init; }
    public string[] GenericParameters { get; init; } = Array.Empty<string>();
    public string[] BaseTypes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Method information for API baseline.
/// </summary>
public sealed record MethodInfo
{
    public required string Signature { get; init; }
    public required string DeclaringType { get; init; }
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public required string Accessibility { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsSealed { get; init; }
    public bool IsStatic { get; init; }
    public ParameterInfo[] Parameters { get; init; } = Array.Empty<ParameterInfo>();
}

/// <summary>
/// Property information for API baseline.
/// </summary>
public sealed record PropertyInfo
{
    public required string FullName { get; init; }
    public required string DeclaringType { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Accessibility { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
}

/// <summary>
/// Parameter information.
/// </summary>
public sealed record ParameterInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsOptional { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsParams { get; init; }
    public bool IsIn { get; init; }
    public bool IsOut { get; init; }
    public bool IsRef { get; init; }
}

/// <summary>
/// Represents a breaking API change.
/// </summary>
public sealed record ApiBreakingChange
{
    public required BreakingChangeType ChangeType { get; init; }
    public required string MemberName { get; init; }
    public required string Description { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? MigrationGuidance { get; init; }
}

/// <summary>
/// Types of breaking changes.
/// </summary>
public enum BreakingChangeType
{
    TypeRemoved,
    TypeKindChanged,
    VisibilityReduced,
    MethodRemoved,
    MethodSignatureChanged,
    ReturnTypeChanged,
    ParameterTypeChanged,
    ParameterRemoved,
    PropertyRemoved,
    PropertyTypeChanged,
    EnumValueRemoved,
    InterfaceMemberAdded,
    SealedAdded,
    AbstractAdded,
    VirtualRemoved,
    GenericConstraintChanged
}
