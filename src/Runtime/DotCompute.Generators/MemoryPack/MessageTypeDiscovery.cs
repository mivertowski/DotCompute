// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.MemoryPack;

/// <summary>
/// Discovers message types in a compilation that are suitable for automatic CUDA code generation.
/// </summary>
/// <remarks>
/// <para>
/// Scans assemblies for types decorated with <c>[MemoryPackable]</c> that implement
/// <c>IRingKernelMessage</c>. These types are candidates for automatic CUDA serialization
/// code generation.
/// </para>
/// <para>
/// <b>Discovery Criteria:</b>
/// - Must have <c>[MemoryPackable]</c> attribute
/// - Must implement <c>IRingKernelMessage</c> interface
/// - Must be a class or struct (not interface or abstract)
/// - Must have accessible properties/fields for serialization
/// </para>
/// <para>
/// <b>Usage Example:</b>
/// <code>
/// var compilation = CSharpCompilation.Create("MyAssembly", ...);
/// var messageTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);
/// foreach (var type in messageTypes)
/// {
///     var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
///     var spec = analyzer.AnalyzeType(type);
///     // Generate CUDA code...
/// }
/// </code>
/// </para>
/// </remarks>
public static class MessageTypeDiscovery
{
    /// <summary>
    /// Discovers all message types in a compilation suitable for CUDA code generation.
    /// </summary>
    /// <param name="compilation">The compilation to scan for message types.</param>
    /// <returns>Collection of discovered message type symbols.</returns>
    public static ImmutableArray<INamedTypeSymbol> DiscoverMessageTypes(Compilation compilation)
    {
        if (compilation == null)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var messageTypes = new List<INamedTypeSymbol>();

        // Scan all syntax trees in the compilation
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // Find all type declarations in this syntax tree
            var typeDeclarations = root.DescendantNodes()
                .Where(n => n.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration) ||
                           n.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StructDeclaration))
                .ToList();

            foreach (var typeDecl in typeDeclarations)
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol)
                {
                    continue;
                }

                // Check if this type meets all criteria
                if (IsMessageType(typeSymbol))
                {
                    messageTypes.Add(typeSymbol);
                }
            }
        }

        return messageTypes.ToImmutableArray();
    }

    /// <summary>
    /// Discovers message types from a specific semantic model and syntax tree.
    /// </summary>
    /// <param name="semanticModel">The semantic model to analyze.</param>
    /// <returns>Collection of discovered message type symbols.</returns>
    public static ImmutableArray<INamedTypeSymbol> DiscoverMessageTypes(SemanticModel semanticModel)
    {
        if (semanticModel == null)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var messageTypes = new List<INamedTypeSymbol>();
        var root = semanticModel.SyntaxTree.GetRoot();

        // Find all type declarations
        var typeDeclarations = root.DescendantNodes()
            .Where(n => n.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration) ||
                       n.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StructDeclaration))
            .ToList();

        foreach (var typeDecl in typeDeclarations)
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol typeSymbol && IsMessageType(typeSymbol))
            {
                messageTypes.Add(typeSymbol);
            }
        }

        return messageTypes.ToImmutableArray();
    }

    /// <summary>
    /// Extracts message pairs (Request/Response) from discovered message types.
    /// </summary>
    /// <param name="messageTypes">Collection of message types to analyze.</param>
    /// <returns>Collection of message pairs grouped by operation.</returns>
    /// <remarks>
    /// Pairs are identified by naming convention:
    /// - Request types end with "Request"
    /// - Response types end with "Response"
    /// - Same prefix indicates a pair (e.g., VectorAddRequest / VectorAddResponse)
    /// </remarks>
    public static ImmutableArray<MessagePair> ExtractMessagePairs(ImmutableArray<INamedTypeSymbol> messageTypes)
    {
        var pairs = new List<MessagePair>();
        var requestTypes = new Dictionary<string, INamedTypeSymbol>();
        var responseTypes = new Dictionary<string, INamedTypeSymbol>();

        // Group types by base name (without Request/Response suffix)
        foreach (var type in messageTypes)
        {
            var typeName = type.Name;

            if (typeName.EndsWith("Request", System.StringComparison.Ordinal))
            {
                var baseName = typeName.Substring(0, typeName.Length - "Request".Length);
                requestTypes[baseName] = type;
            }
            else if (typeName.EndsWith("Response", System.StringComparison.Ordinal))
            {
                var baseName = typeName.Substring(0, typeName.Length - "Response".Length);
                responseTypes[baseName] = type;
            }
        }

        // Match requests with responses
        foreach (var kvp in requestTypes)
        {
            var baseName = kvp.Key;
            var requestType = kvp.Value;

            if (responseTypes.TryGetValue(baseName, out var responseType))
            {
                pairs.Add(new MessagePair(baseName, requestType, responseType));
            }
        }

        return pairs.ToImmutableArray();
    }

    /// <summary>
    /// Builds a dependency graph for message types to handle nested types correctly.
    /// </summary>
    /// <param name="messageTypes">Collection of message types to analyze.</param>
    /// <returns>Dependency graph with types ordered for generation (dependencies first).</returns>
    /// <remarks>
    /// The dependency graph ensures that nested types are generated before their containers.
    /// For example, if MessageA contains a field of type MessageB, MessageB must be generated first.
    /// </remarks>
    public static ImmutableArray<TypeDependencyNode> BuildDependencyGraph(ImmutableArray<INamedTypeSymbol> messageTypes)
    {
        var nodes = new Dictionary<string, TypeDependencyNode>();

        // Create nodes for all message types
        foreach (var type in messageTypes)
        {
            var fullName = type.ToDisplayString();
            nodes[fullName] = new TypeDependencyNode(type);
        }

        // Analyze dependencies (properties/fields of each type)
        foreach (var type in messageTypes)
        {
            var fullName = type.ToDisplayString();
            var node = nodes[fullName];

            // Get all properties
            var properties = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && !p.IsIndexer && p.GetMethod != null && p.SetMethod != null)
                .ToList();

            foreach (var property in properties)
            {
                var propertyType = property.Type;

                // Unwrap nullable types
                if (propertyType is INamedTypeSymbol namedType && namedType.IsGenericType &&
                    propertyType.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    propertyType = namedType.TypeArguments[0];
                }

                var propertyTypeName = propertyType.ToDisplayString();

                // If this property type is also a message type, add dependency
                if (nodes.TryGetValue(propertyTypeName, out var dependencyNode))
                {
                    node.AddDependency(dependencyNode.Type);
                }
            }
        }

        // Topological sort to order types by dependencies
        var sorted = TopologicalSort(nodes.Values.ToList());
        return sorted.ToImmutableArray();
    }

    /// <summary>
    /// Checks if a type symbol is a valid message type for CUDA code generation.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is a valid message type; otherwise, false.</returns>
    private static bool IsMessageType(INamedTypeSymbol typeSymbol)
    {
        // Must not be abstract
        if (typeSymbol.IsAbstract)
        {
            return false;
        }

        // Must have [MemoryPackable] attribute
        if (!HasMemoryPackableAttribute(typeSymbol))
        {
            return false;
        }

        // Must implement IRingKernelMessage
        if (!ImplementsIRingKernelMessage(typeSymbol))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a type has the [MemoryPackable] attribute.
    /// </summary>
    private static bool HasMemoryPackableAttribute(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "MemoryPackableAttribute" ||
                        attr.AttributeClass?.ToDisplayString() == "MemoryPack.MemoryPackableAttribute");
    }

    /// <summary>
    /// Checks if a type implements IRingKernelMessage interface.
    /// </summary>
    private static bool ImplementsIRingKernelMessage(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces
            .Any(i => i.Name == "IRingKernelMessage" ||
                     i.ToDisplayString().Contains("IRingKernelMessage"));
    }

    /// <summary>
    /// Performs topological sort on dependency nodes to determine generation order.
    /// </summary>
    /// <param name="nodes">List of dependency nodes to sort.</param>
    /// <returns>Sorted list with dependencies first.</returns>
    private static List<TypeDependencyNode> TopologicalSort(List<TypeDependencyNode> nodes)
    {
        var sorted = new List<TypeDependencyNode>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var visiting = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        void Visit(TypeDependencyNode node)
        {
            if (visited.Contains(node.Type))
            {
                return;
            }

            if (visiting.Contains(node.Type))
            {
                // Circular dependency detected - skip for now
                return;
            }

            visiting.Add(node.Type);

            // Visit all dependencies first
            foreach (var dep in node.Dependencies)
            {
                var depNode = nodes.FirstOrDefault(n => SymbolEqualityComparer.Default.Equals(n.Type, dep));
                if (depNode != null)
                {
                    Visit(depNode);
                }
            }

            visiting.Remove(node.Type);
            visited.Add(node.Type);
            sorted.Add(node);
        }

        foreach (var node in nodes)
        {
            Visit(node);
        }

        return sorted;
    }
}

/// <summary>
/// Represents a pair of request and response message types.
/// </summary>
public readonly struct MessagePair : System.IEquatable<MessagePair>
{
    /// <summary>
    /// Gets the base name of the operation (without Request/Response suffix).
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the request message type symbol.
    /// </summary>
    public INamedTypeSymbol RequestType { get; }

    /// <summary>
    /// Gets the response message type symbol.
    /// </summary>
    public INamedTypeSymbol ResponseType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagePair"/> struct.
    /// </summary>
    /// <param name="operationName">Base name of the operation.</param>
    /// <param name="requestType">The request message type symbol.</param>
    /// <param name="responseType">The response message type symbol.</param>
    public MessagePair(string operationName, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
    {
        OperationName = operationName ?? throw new System.ArgumentNullException(nameof(operationName));
        RequestType = requestType ?? throw new System.ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new System.ArgumentNullException(nameof(responseType));
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is MessagePair other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified MessagePair is equal to the current instance.
    /// </summary>
    public bool Equals(MessagePair other)
    {
        return OperationName == other.OperationName &&
               SymbolEqualityComparer.Default.Equals(RequestType, other.RequestType) &&
               SymbolEqualityComparer.Default.Equals(ResponseType, other.ResponseType);
    }

    /// <summary>
    /// Returns a hash code for the current instance.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (OperationName?.GetHashCode() ?? 0);
            hash = hash * 31 + (RequestType != null ? SymbolEqualityComparer.Default.GetHashCode(RequestType) : 0);
            hash = hash * 31 + (ResponseType != null ? SymbolEqualityComparer.Default.GetHashCode(ResponseType) : 0);
            return hash;
        }
    }

    /// <summary>
    /// Determines whether two MessagePair instances are equal.
    /// </summary>
    public static bool operator ==(MessagePair left, MessagePair right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two MessagePair instances are not equal.
    /// </summary>
    public static bool operator !=(MessagePair left, MessagePair right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Returns a string representation of the message pair.
    /// </summary>
    public override string ToString()
    {
        return $"{OperationName}: {RequestType?.Name} → {ResponseType?.Name}";
    }
}

/// <summary>
/// Represents a node in the type dependency graph.
/// </summary>
public sealed class TypeDependencyNode
{
    private readonly List<INamedTypeSymbol> dependencies;

    /// <summary>
    /// Gets the type symbol for this node.
    /// </summary>
    public INamedTypeSymbol Type { get; }

    /// <summary>
    /// Gets the list of types this type depends on.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<INamedTypeSymbol> Dependencies => dependencies;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDependencyNode"/> class.
    /// </summary>
    /// <param name="type">The type symbol for this node.</param>
    public TypeDependencyNode(INamedTypeSymbol type)
    {
        Type = type ?? throw new System.ArgumentNullException(nameof(type));
        dependencies = new List<INamedTypeSymbol>();
    }

    /// <summary>
    /// Adds a dependency to this node.
    /// </summary>
    /// <param name="dependency">The type this node depends on.</param>
    internal void AddDependency(INamedTypeSymbol dependency)
    {
        if (dependency != null && !dependencies.Contains(dependency, SymbolEqualityComparer.Default))
        {
            dependencies.Add(dependency);
        }
    }
}
