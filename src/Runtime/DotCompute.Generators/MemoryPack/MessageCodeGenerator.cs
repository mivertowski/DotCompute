// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.MemoryPack;

/// <summary>
/// Orchestrates batch CUDA code generation for multiple message types with dependency resolution.
/// </summary>
/// <remarks>
/// <para>
/// This generator coordinates the complete code generation pipeline:
/// 1. Discovers all [MemoryPackable] message types via <see cref="MessageTypeDiscovery"/>
/// 2. Builds dependency graph to handle nested types correctly
/// 3. Generates CUDA code in dependency order (dependencies first)
/// 4. Adds header guards and include dependencies
/// 5. Manages output file generation
/// </para>
/// <para>
/// <b>Key Features:</b>
/// - Batch processing for multiple message types in one pass
/// - Automatic dependency resolution (nested types generated first)
/// - Header guard generation to prevent multiple inclusion
/// - Include directive generation for dependencies
/// - Topological sort ensures correct compilation order
/// </para>
/// <para>
/// <b>Usage Example:</b>
/// <code>
/// var compilation = CSharpCompilation.Create("MyAssembly", ...);
/// var generator = new MessageCodeGenerator();
/// var results = generator.GenerateBatch(compilation);
/// foreach (var result in results)
/// {
///     File.WriteAllText($"{result.FileName}.cu", result.SourceCode);
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class MessageCodeGenerator
{
    /// <summary>
    /// Generates CUDA code for all message types in a compilation.
    /// </summary>
    /// <param name="compilation">The compilation to analyze for message types.</param>
    /// <returns>Collection of code generation results, one per message type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when compilation is null.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822:Mark members as static", Justification = "Instance method allows for future configuration and state management")]
    public ImmutableArray<CodeGenerationResult> GenerateBatch(Compilation compilation)
    {
        if (compilation == null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        // Step 1: Discover all message types
        var messageTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);
        if (messageTypes.IsEmpty)
        {
            return ImmutableArray<CodeGenerationResult>.Empty;
        }

        // Step 2: Build dependency graph
        var dependencyGraph = MessageTypeDiscovery.BuildDependencyGraph(messageTypes);

        // Step 3: Generate code for each type in dependency order
        var results = ImmutableArray.CreateBuilder<CodeGenerationResult>();
        var generatedTypes = new HashSet<string>();

        foreach (var node in dependencyGraph)
        {
            var typeSymbol = node.Type;
            var typeName = typeSymbol.ToDisplayString();

            if (generatedTypes.Contains(typeName))
            {
                continue; // Already generated (circular dependency handling)
            }

            // Get semantic model for this type's syntax tree
            var syntaxTree = typeSymbol.Locations.FirstOrDefault()?.SourceTree;
            if (syntaxTree == null)
            {
                continue; // Type not in source (metadata reference)
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Generate code for this type
            var result = GenerateSingle(typeSymbol, semanticModel, node.Dependencies);
            results.Add(result);
            generatedTypes.Add(typeName);
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Generates CUDA code for a single message type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to generate code for.</param>
    /// <param name="semanticModel">The semantic model for type analysis.</param>
    /// <param name="dependencies">List of types this type depends on.</param>
    /// <returns>Code generation result with source code and metadata.</returns>
    private static CodeGenerationResult GenerateSingle(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel,
        IReadOnlyList<INamedTypeSymbol> dependencies)
    {
        var typeName = typeSymbol.ToDisplayString();
        var simpleName = typeSymbol.Name;

        // Analyze binary format
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Generate CUDA code
        var cudaGenerator = new MemoryPackCudaGenerator();
        var cudaCode = cudaGenerator.GenerateCode(spec);

        // Add header guards and includes
        var sourceCode = WrapWithHeaderGuards(simpleName, cudaCode, dependencies);

        // Determine output file name
        var fileName = $"{simpleName}Serialization";

        return new CodeGenerationResult(
            fileName: fileName,
            sourceCode: sourceCode,
            typeName: typeName,
            dependencies: dependencies.Select(d => d.Name).ToImmutableArray(),
            totalSize: spec.TotalSize,
            isFixedSize: spec.IsFixedSize);
    }

    /// <summary>
    /// Wraps CUDA code with header guards and include directives.
    /// </summary>
    /// <param name="typeName">The simple type name (e.g., "VectorAddRequest").</param>
    /// <param name="cudaCode">The generated CUDA code to wrap.</param>
    /// <param name="dependencies">List of types this type depends on (for include directives).</param>
    /// <returns>Complete CUDA source code with header guards and includes.</returns>
    private static string WrapWithHeaderGuards(
        string typeName,
        string cudaCode,
        IReadOnlyList<INamedTypeSymbol> dependencies)
    {
        var sb = new StringBuilder();

        // Header guard - use SCREAMING_SNAKE_CASE
        var guardName = $"DOTCOMPUTE_{ToScreamingSnakeCase(typeName)}_SERIALIZATION_H";

        sb.AppendLine($"#ifndef {guardName}");
        sb.AppendLine($"#define {guardName}");
        sb.AppendLine();

        // Copyright notice
        sb.AppendLine("// Auto-generated MemoryPack CUDA serialization code");
        sb.AppendLine("// Copyright (c) 2025 Michael Ivertowski");
        sb.AppendLine("// Licensed under the MIT License. See LICENSE file in the project root for license information.");
        sb.AppendLine();

        // Standard includes
        sb.AppendLine("// Standard CUDA includes");
        sb.AppendLine("#include <cstdint>");
        sb.AppendLine();

        // Include dependencies (if any)
        if (dependencies != null && dependencies.Count > 0)
        {
            sb.AppendLine("// Message type dependencies");
            foreach (var dep in dependencies)
            {
                sb.AppendLine($"#include \"{dep.Name}Serialization.cu\"");
            }
            sb.AppendLine();
        }

        // Generated code
        sb.Append(cudaCode);

        // Close header guard
        sb.AppendLine();
        sb.AppendLine($"#endif // {guardName}");

        return sb.ToString();
    }

    /// <summary>
    /// Converts PascalCase to SCREAMING_SNAKE_CASE.
    /// </summary>
    /// <param name="input">Input string in PascalCase.</param>
    /// <returns>Output string in SCREAMING_SNAKE_CASE.</returns>
    private static string ToScreamingSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(input[i]));
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents the result of code generation for a single message type.
/// </summary>
public sealed class CodeGenerationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGenerationResult"/> class.
    /// </summary>
    public CodeGenerationResult(
        string fileName,
        string sourceCode,
        string typeName,
        ImmutableArray<string> dependencies,
        int totalSize,
        bool isFixedSize)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        SourceCode = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Dependencies = dependencies;
        TotalSize = totalSize;
        IsFixedSize = isFixedSize;
    }

    /// <summary>
    /// Gets the output file name (without extension, e.g., "VectorAddRequestSerialization").
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the complete generated CUDA source code with header guards.
    /// </summary>
    public string SourceCode { get; }

    /// <summary>
    /// Gets the fully qualified C# type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the list of type names this type depends on (simple names).
    /// </summary>
    public ImmutableArray<string> Dependencies { get; }

    /// <summary>
    /// Gets the total serialized size in bytes.
    /// </summary>
    public int TotalSize { get; }

    /// <summary>
    /// Gets a value indicating whether this message has fixed size.
    /// </summary>
    public bool IsFixedSize { get; }
}
