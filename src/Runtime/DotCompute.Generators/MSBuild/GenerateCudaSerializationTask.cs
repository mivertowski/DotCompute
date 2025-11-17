// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotCompute.Generators.MemoryPack;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotCompute.Generators.MSBuild;

/// <summary>
/// MSBuild task that generates CUDA serialization code for message types at build time.
/// </summary>
/// <remarks>
/// <para>
/// This task runs before compilation to generate .cu files for all message types
/// implementing IRingKernelMessage with [MemoryPackable] attribute.
/// </para>
/// <para>
/// <b>MSBuild Integration:</b>
/// - Runs during BeforeCompile target
/// - Generates CUDA code using MessageCodeGenerator
/// - Writes .cu files to $(IntermediateOutputPath)/Generated/CUDA
/// - Includes generated files in CUDA compilation inputs
/// </para>
/// <para>
/// <b>Incremental Build Support:</b>
/// - Tracks source file timestamps
/// - Only regenerates if source files changed
/// - Compares output file timestamps
/// </para>
/// </remarks>
public sealed class GenerateCudaSerializationTask : Task
{
    /// <summary>
    /// Gets or sets the C# source files to analyze for message types.
    /// </summary>
    [Required]
    public ITaskItem[]? SourceFiles { get; set; }

    /// <summary>
    /// Gets or sets the output directory for generated CUDA files.
    /// </summary>
    [Required]
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets reference assemblies needed for compilation.
    /// </summary>
    public ITaskItem[]? References { get; set; }

    /// <summary>
    /// Gets the generated CUDA files (output parameter).
    /// </summary>
    [Output]
    public ITaskItem[]? GeneratedFiles { get; private set; }

    /// <summary>
    /// Executes the task to generate CUDA serialization code.
    /// </summary>
    /// <returns>True if successful; otherwise, false.</returns>
    public override bool Execute()
    {
        try
        {
            if (SourceFiles == null || SourceFiles.Length == 0)
            {
                Log.LogMessage(MessageImportance.Low, "No source files provided for CUDA code generation.");
                GeneratedFiles = Array.Empty<ITaskItem>();
                return true;
            }

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                Log.LogError("OutputDirectory parameter is required.");
                return false;
            }

            Log.LogMessage(MessageImportance.High, $"Generating CUDA serialization code for {SourceFiles.Length} source files...");

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(OutputDirectory);

            // Parse source files into syntax trees
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var sourceFile in SourceFiles)
            {
                var path = sourceFile.ItemSpec;
                if (!File.Exists(path))
                {
                    Log.LogWarning($"Source file not found: {path}");
                    continue;
                }

                var sourceText = File.ReadAllText(path);
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: path);
                syntaxTrees.Add(syntaxTree);
            }

            if (syntaxTrees.Count == 0)
            {
                Log.LogMessage(MessageImportance.Low, "No valid source files to process.");
                GeneratedFiles = Array.Empty<ITaskItem>();
                return true;
            }

            // Create compilation with references
            var references = GetMetadataReferences();
            var compilation = CSharpCompilation.Create(
                "MessageTypeAnalysis",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Generate CUDA code
            var generator = new MessageCodeGenerator();
            var results = generator.GenerateBatch(compilation);

            if (results.IsEmpty)
            {
                Log.LogMessage(MessageImportance.Normal, "No message types found for CUDA code generation.");
                GeneratedFiles = Array.Empty<ITaskItem>();
                return true;
            }

            // Write generated files to disk
            var generatedFiles = new List<ITaskItem>();
            foreach (var result in results)
            {
                var outputPath = Path.Combine(OutputDirectory, $"{result.FileName}.cu");

                // Check if regeneration is needed (incremental build)
                if (File.Exists(outputPath))
                {
                    var existingContent = File.ReadAllText(outputPath);
                    if (existingContent == result.SourceCode)
                    {
                        Log.LogMessage(MessageImportance.Low, $"Skipping unchanged file: {result.FileName}.cu");
                        generatedFiles.Add(new TaskItem(outputPath));
                        continue;
                    }
                }

                // Write file
                File.WriteAllText(outputPath, result.SourceCode);
                Log.LogMessage(MessageImportance.Normal, $"Generated: {result.FileName}.cu ({result.TotalSize} bytes, Fixed: {result.IsFixedSize})");

                generatedFiles.Add(new TaskItem(outputPath));
            }

            GeneratedFiles = generatedFiles.ToArray();
            Log.LogMessage(MessageImportance.High, $"Successfully generated {GeneratedFiles.Length} CUDA serialization files.");

            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    /// <summary>
    /// Gets metadata references for compilation.
    /// </summary>
    private List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core system references
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };

        // Add System.Runtime for Span<T>
        var systemRuntime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (systemRuntime != null && !string.IsNullOrEmpty(systemRuntime.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
        }

        // Add System.Memory for ReadOnlySpan<T>
        var systemMemory = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Memory");
        if (systemMemory != null && !string.IsNullOrEmpty(systemMemory.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemMemory.Location));
        }

        // Add user-provided references
        if (References != null)
        {
            foreach (var reference in References)
            {
                var path = reference.ItemSpec;
                if (File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        return references;
    }
}
#endif
