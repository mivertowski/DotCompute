// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.CommandLine;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotCompute.Generators.Cli.Commands;

/// <summary>
/// Command to generate CUDA serialization code for message types.
/// </summary>
internal static class GenerateCudaSerializationCommand
{
    /// <summary>
    /// Exit codes for the command.
    /// </summary>
    private static class ExitCodes
    {
        public const int Success = 0;
        public const int Error = 1;
        public const int NoMessagesFound = 2;
    }

    /// <summary>
    /// Creates the generate-cuda-serialization command.
    /// </summary>
    public static Command Create()
    {
        var sourceFilesOption = new Option<FileInfo[]>(
            aliases: ["--source-files", "-s"],
            description: "C# source files to analyze for message types")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };

        var outputDirectoryOption = new Option<DirectoryInfo>(
            aliases: ["--output", "-o"],
            description: "Output directory for generated CUDA files")
        {
            IsRequired = true
        };

        var referencesOption = new Option<FileInfo[]>(
            aliases: ["--references", "-r"],
            description: "Reference assemblies needed for compilation")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Enable verbose logging",
            getDefaultValue: () => false);

        var command = new Command("generate-cuda-serialization", "Generate CUDA serialization code for message types")
        {
            sourceFilesOption,
            outputDirectoryOption,
            referencesOption,
            verboseOption
        };

        command.SetHandler(ExecuteAsync, sourceFilesOption, outputDirectoryOption, referencesOption, verboseOption);

        return command;
    }

    private static async Task<int> ExecuteAsync(
        FileInfo[] sourceFiles,
        DirectoryInfo outputDirectory,
        FileInfo[]? references,
        bool verbose)
    {
        var result = new GenerationResult();

        try
        {
            if (sourceFiles == null || sourceFiles.Length == 0)
            {
                result.Success = true;
                result.Message = "No source files provided";
                result.Files = [];
                Console.WriteLine(JsonSerializer.Serialize(result));
                return ExitCodes.Success;
            }

            // Create output directory if it doesn't exist
            outputDirectory.Create();

            // Parse source files into syntax trees
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var sourceFile in sourceFiles)
            {
                if (!sourceFile.Exists)
                {
                    if (verbose)
                    {
                        Console.Error.WriteLine($"Warning: Source file not found: {sourceFile.FullName}");
                    }
                    continue;
                }

                var sourceText = await File.ReadAllTextAsync(sourceFile.FullName);
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: sourceFile.FullName);
                syntaxTrees.Add(syntaxTree);
            }

            if (syntaxTrees.Count == 0)
            {
                result.Success = true;
                result.Message = "No valid source files to process";
                result.Files = [];
                Console.WriteLine(JsonSerializer.Serialize(result));
                return ExitCodes.Success;
            }

            // Create compilation with references
            var metadataReferences = GetMetadataReferences(references);
            var compilation = CSharpCompilation.Create(
                "MessageTypeAnalysis",
                syntaxTrees,
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Generate CUDA code using MessageCodeGenerator logic
            var generatedFiles = await GenerateCudaCodeAsync(compilation, outputDirectory, verbose);

            if (generatedFiles.Count == 0)
            {
                result.Success = true;
                result.Message = "No message types found";
                result.Files = [];
                Console.WriteLine(JsonSerializer.Serialize(result));
                return ExitCodes.NoMessagesFound;
            }

            result.Success = true;
            result.Message = $"Generated {generatedFiles.Count} files";
            result.Files = generatedFiles.ToArray();

            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false }));
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.Error = ex.ToString();
            result.Files = [];

            Console.WriteLine(JsonSerializer.Serialize(result));
            return ExitCodes.Error;
        }
    }

    private static List<MetadataReference> GetMetadataReferences(FileInfo[]? references)
    {
        var metadataReferences = new List<MetadataReference>
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
            metadataReferences.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
        }

        // Add System.Memory for ReadOnlySpan<T>
        var systemMemory = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Memory");
        if (systemMemory != null && !string.IsNullOrEmpty(systemMemory.Location))
        {
            metadataReferences.Add(MetadataReference.CreateFromFile(systemMemory.Location));
        }

        // Add user-provided references
        if (references != null)
        {
            foreach (var reference in references)
            {
                if (reference.Exists)
                {
                    metadataReferences.Add(MetadataReference.CreateFromFile(reference.FullName));
                }
            }
        }

        return metadataReferences;
    }

    /// <summary>
    /// Generates CUDA serialization code from the compilation.
    /// This is a simplified version that scans for IRingKernelMessage types.
    /// </summary>
    private static Task<List<string>> GenerateCudaCodeAsync(
        Compilation compilation,
        DirectoryInfo outputDirectory,
        bool verbose)
    {
        var generatedFiles = new List<string>();

        // Find all types implementing IRingKernelMessage with [MemoryPackable]
        var messageTypes = FindMessageTypes(compilation);

        if (verbose)
        {
            Console.Error.WriteLine($"Found {messageTypes.Count} message types");
        }

        foreach (var messageType in messageTypes)
        {
            var typeName = messageType.Name;
            var cudaCode = GenerateCudaSerializationCode(messageType);

            if (string.IsNullOrEmpty(cudaCode))
            {
                continue;
            }

            var outputPath = Path.Combine(outputDirectory.FullName, $"{typeName}_serialization.cu");

            // Check if regeneration is needed (incremental build)
            if (File.Exists(outputPath))
            {
                var existingContent = File.ReadAllText(outputPath);
                if (existingContent == cudaCode)
                {
                    if (verbose)
                    {
                        Console.Error.WriteLine($"Skipping unchanged: {typeName}_serialization.cu");
                    }
                    generatedFiles.Add(outputPath);
                    continue;
                }
            }

            File.WriteAllText(outputPath, cudaCode);

            if (verbose)
            {
                Console.Error.WriteLine($"Generated: {typeName}_serialization.cu");
            }

            generatedFiles.Add(outputPath);
        }

        return Task.FromResult(generatedFiles);
    }

    private static List<INamedTypeSymbol> FindMessageTypes(Compilation compilation)
    {
        var messageTypes = new List<INamedTypeSymbol>();

        // Look for IRingKernelMessage interface
        var ringKernelMessageInterface = compilation.GetTypeByMetadataName(
            "DotCompute.Abstractions.Messaging.IRingKernelMessage");

        if (ringKernelMessageInterface == null)
        {
            return messageTypes;
        }

        // Find all types that implement IRingKernelMessage
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            var typeDeclarations = root.DescendantNodes()
                .Where(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax);

            foreach (var typeDecl in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null)
                {
                    continue;
                }

                // Check if implements IRingKernelMessage
                if (!symbol.AllInterfaces.Any(i =>
                    i.ToDisplayString() == "DotCompute.Abstractions.Messaging.IRingKernelMessage"))
                {
                    continue;
                }

                // Check for [MemoryPackable] attribute
                var hasMemoryPackable = symbol.GetAttributes().Any(a =>
                    a.AttributeClass?.Name is "MemoryPackableAttribute" or "MemoryPackable");

                if (hasMemoryPackable)
                {
                    messageTypes.Add(symbol);
                }
            }
        }

        return messageTypes;
    }

    private static string GenerateCudaSerializationCode(INamedTypeSymbol messageType)
    {
        var typeName = messageType.Name;
        var members = messageType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToList();

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("// Auto-generated CUDA serialization code");
        sb.AppendLine($"// Type: {messageType.ToDisplayString()}");
        sb.AppendLine("// Generated by DotCompute.Generators.Cli");
        sb.AppendLine();
        sb.AppendLine("#pragma once");
        sb.AppendLine("#include <cstdint>");
        sb.AppendLine("#include <cstring>");
        sb.AppendLine();

        // Generate struct
        sb.AppendLine($"struct {typeName} {{");
        foreach (var member in members)
        {
            var cudaType = GetCudaType(member.Type);
            sb.AppendLine($"    {cudaType} {member.Name};");
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // Generate deserialize function
        sb.AppendLine($"__device__ inline bool deserialize_{typeName}(const uint8_t* buffer, size_t length, {typeName}* result) {{");
        sb.AppendLine($"    if (length < sizeof({typeName})) return false;");
        sb.AppendLine($"    memcpy(result, buffer, sizeof({typeName}));");
        sb.AppendLine("    return true;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate serialize function
        sb.AppendLine($"__device__ inline size_t serialize_{typeName}(const {typeName}* value, uint8_t* buffer, size_t maxLength) {{");
        sb.AppendLine($"    if (maxLength < sizeof({typeName})) return 0;");
        sb.AppendLine($"    memcpy(buffer, value, sizeof({typeName}));");
        sb.AppendLine($"    return sizeof({typeName});");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetCudaType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName switch
        {
            "int" or "System.Int32" => "int32_t",
            "uint" or "System.UInt32" => "uint32_t",
            "long" or "System.Int64" => "int64_t",
            "ulong" or "System.UInt64" => "uint64_t",
            "short" or "System.Int16" => "int16_t",
            "ushort" or "System.UInt16" => "uint16_t",
            "byte" or "System.Byte" => "uint8_t",
            "sbyte" or "System.SByte" => "int8_t",
            "float" or "System.Single" => "float",
            "double" or "System.Double" => "double",
            "bool" or "System.Boolean" => "bool",
            _ => "uint8_t*" // Default to pointer for complex types
        };
    }
}

/// <summary>
/// Result of CUDA code generation, serialized to JSON for MSBuild consumption.
/// </summary>
internal sealed class GenerationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string[] Files { get; set; } = [];
}
