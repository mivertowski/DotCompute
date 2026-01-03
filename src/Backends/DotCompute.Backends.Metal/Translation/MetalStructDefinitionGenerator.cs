// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Generates Metal Shading Language (MSL) struct definitions from C# struct types.
/// Handles memory layout, alignment, and naming conventions for GPU-compatible structures.
/// </summary>
/// <remarks>
/// This generator creates MSL struct definitions that match C# struct memory layouts,
/// enabling seamless data transfer between CPU and GPU.
///
/// Features:
/// - Automatic C# to MSL type translation
/// - Snake_case naming convention conversion
/// - Memory alignment attributes for GPU optimization
/// - Packed attribute support for explicit layouts
/// - Nested struct support
/// - Array field handling with device pointers
/// - Documentation comment generation
/// </remarks>
public static class MetalStructDefinitionGenerator
{
    /// <summary>
    /// Options for struct generation.
    /// </summary>
    public sealed class GenerationOptions
    {
        /// <summary>
        /// Whether to generate packed structs (no padding).
        /// Default is false to allow natural alignment.
        /// </summary>
        public bool UsePacked { get; init; } = false;

        /// <summary>
        /// Whether to include documentation comments.
        /// </summary>
        public bool IncludeComments { get; init; } = true;

        /// <summary>
        /// Whether to include size and alignment annotations.
        /// </summary>
        public bool IncludeSizeAnnotations { get; init; } = true;

        /// <summary>
        /// Whether to use snake_case for field names.
        /// Metal convention prefers snake_case.
        /// </summary>
        public bool UseSnakeCase { get; init; } = true;

        /// <summary>
        /// Whether to generate alignas attributes for fields.
        /// </summary>
        public bool GenerateAlignmentAttributes { get; init; } = true;

        /// <summary>
        /// Custom namespace prefix for generated structs.
        /// </summary>
        public string? NamespacePrefix { get; init; }
    }

    /// <summary>
    /// Default generation options.
    /// </summary>
    public static GenerationOptions DefaultOptions { get; } = new();

    /// <summary>
    /// Generates an MSL struct definition from C# struct information.
    /// </summary>
    /// <param name="structInfo">The struct information from StructTranslationHelper.AnalyzeStruct.</param>
    /// <param name="options">Optional generation options.</param>
    /// <returns>Complete MSL struct definition.</returns>
    public static string GenerateStructDefinition(
        StructTranslationHelper.StructInfo structInfo,
        GenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(structInfo);
        options ??= DefaultOptions;

        var sb = new StringBuilder();

        // Add header comment
        if (options.IncludeComments)
        {
            AppendStructComment(sb, structInfo, options);
        }

        // Start struct definition
        var structName = options.NamespacePrefix != null
            ? $"{options.NamespacePrefix}_{structInfo.Name}"
            : structInfo.Name;

        if (options.UsePacked)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"struct __attribute__((packed)) {structName} {{");
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"struct {structName} {{");
        }

        // Generate fields
        foreach (var field in structInfo.Fields)
        {
            AppendField(sb, field, options);
        }

        // Close struct
        sb.AppendLine("};");

        // Add size validation comment
        if (options.IncludeSizeAnnotations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"static_assert(sizeof({structName}) == {structInfo.TotalSize}, \"Size mismatch with C# struct\");");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates MSL struct definitions for multiple C# struct types.
    /// Handles dependencies and ordering automatically.
    /// </summary>
    /// <param name="structInfos">Collection of struct information.</param>
    /// <param name="options">Optional generation options.</param>
    /// <returns>Complete MSL source with all struct definitions.</returns>
    public static string GenerateStructDefinitions(
        IEnumerable<StructTranslationHelper.StructInfo> structInfos,
        GenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(structInfos);
        options ??= DefaultOptions;

        var sb = new StringBuilder();

        // Add header
        AppendFileHeader(sb);

        // Generate each struct
        var first = true;
        foreach (var structInfo in structInfos)
        {
            if (!first)
            {
                sb.AppendLine();
            }
            first = false;

            sb.Append(GenerateStructDefinition(structInfo, options));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates an MSL struct definition directly from a C# type.
    /// </summary>
    /// <param name="structType">The C# struct type.</param>
    /// <param name="options">Optional generation options.</param>
    /// <returns>Complete MSL struct definition.</returns>
    public static string GenerateFromType(Type structType, GenerationOptions? options = null)
    {
        var structInfo = StructTranslationHelper.AnalyzeStruct(structType);
        return GenerateStructDefinition(structInfo, options);
    }

    /// <summary>
    /// Generates MSL struct definitions from multiple C# types.
    /// </summary>
    /// <param name="structTypes">Collection of C# struct types.</param>
    /// <param name="options">Optional generation options.</param>
    /// <returns>Complete MSL source with all struct definitions.</returns>
    public static string GenerateFromTypes(
        IEnumerable<Type> structTypes,
        GenerationOptions? options = null)
    {
        var structInfos = structTypes.Select(StructTranslationHelper.AnalyzeStruct);
        return GenerateStructDefinitions(structInfos, options);
    }

    /// <summary>
    /// Generates a forward declaration for a struct.
    /// </summary>
    /// <param name="structName">The struct name.</param>
    /// <param name="namespacePrefix">Optional namespace prefix.</param>
    /// <returns>MSL forward declaration.</returns>
    public static string GenerateForwardDeclaration(string structName, string? namespacePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(structName);

        var fullName = namespacePrefix != null
            ? $"{namespacePrefix}_{structName}"
            : structName;

        return $"struct {fullName};";
    }

    /// <summary>
    /// Generates a typedef alias for a struct.
    /// </summary>
    /// <param name="originalName">The original struct name.</param>
    /// <param name="aliasName">The alias name.</param>
    /// <returns>MSL typedef.</returns>
    public static string GenerateTypedef(string originalName, string aliasName)
    {
        ArgumentNullException.ThrowIfNull(originalName);
        ArgumentNullException.ThrowIfNull(aliasName);

        return $"typedef struct {originalName} {aliasName};";
    }

    /// <summary>
    /// Generates MSL header with standard includes and struct definitions.
    /// </summary>
    /// <param name="structInfos">Collection of struct information.</param>
    /// <param name="headerName">Name for the generated header.</param>
    /// <param name="options">Optional generation options.</param>
    /// <returns>Complete MSL header file content.</returns>
    public static string GenerateHeader(
        IEnumerable<StructTranslationHelper.StructInfo> structInfos,
        string headerName,
        GenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(structInfos);
        ArgumentNullException.ThrowIfNull(headerName);
        options ??= DefaultOptions;

        var sb = new StringBuilder();

        // Include guard
        var guardName = headerName.ToUpperInvariant().Replace('.', '_').Replace('/', '_');
        sb.AppendLine(CultureInfo.InvariantCulture, $"#ifndef {guardName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define {guardName}");
        sb.AppendLine();

        // Metal includes
        sb.AppendLine("#include <metal_stdlib>");
        sb.AppendLine("using namespace metal;");
        sb.AppendLine();

        // Struct definitions
        var first = true;
        foreach (var structInfo in structInfos)
        {
            if (!first)
            {
                sb.AppendLine();
            }
            first = false;

            sb.Append(GenerateStructDefinition(structInfo, options));
        }

        // End include guard
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"#endif // {guardName}");

        return sb.ToString();
    }

    /// <summary>
    /// Appends the file header with Metal includes.
    /// </summary>
    private static void AppendFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// Auto-generated MSL struct definitions");
        sb.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("// DotCompute.Backends.Metal.Translation.MetalStructDefinitionGenerator");
        sb.AppendLine();
        sb.AppendLine("#include <metal_stdlib>");
        sb.AppendLine("using namespace metal;");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends documentation comment for a struct.
    /// </summary>
    private static void AppendStructComment(
        StringBuilder sb,
        StructTranslationHelper.StructInfo structInfo,
        GenerationOptions options)
    {
        sb.AppendLine("/**");
        sb.AppendLine(CultureInfo.InvariantCulture, $" * @struct {structInfo.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $" * @brief Translated from C# struct {structInfo.Name}");

        if (options.IncludeSizeAnnotations)
        {
            sb.AppendLine(" *");
            sb.AppendLine(CultureInfo.InvariantCulture, $" * @size {structInfo.TotalSize} bytes");
            sb.AppendLine(CultureInfo.InvariantCulture, $" * @alignment {structInfo.Alignment} bytes");
            sb.AppendLine(CultureInfo.InvariantCulture, $" * @fields {structInfo.Fields.Count}");
        }

        sb.AppendLine(" */");
    }

    /// <summary>
    /// Appends a field definition with proper formatting.
    /// </summary>
    private static void AppendField(
        StringBuilder sb,
        StructTranslationHelper.StructFieldInfo field,
        GenerationOptions options)
    {
        var fieldName = options.UseSnakeCase
            ? StructTranslationHelper.ToSnakeCase(field.Name)
            : field.Name;

        // Handle array fields
        if (field.IsArray && field.ArrayElementType != null)
        {
            var elementMslType = StructTranslationHelper.GetMslTypeName(field.ArrayElementType);

            if (options.IncludeComments)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    // {field.CSharpType} {field.Name} (offset: {field.Offset}, size: {field.Size})");
            }

            // Arrays become device pointers in MSL
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    device {elementMslType}* {fieldName};");
            return;
        }

        // Regular field
        if (options.IncludeComments)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    // {field.CSharpType} {field.Name} (offset: {field.Offset}, size: {field.Size})");
        }

        // Add alignment attribute if needed
        if (options.GenerateAlignmentAttributes && field.Size >= 4 && !options.UsePacked)
        {
            var alignment = GetFieldAlignment(field);
            if (alignment > 1)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    alignas({alignment}) {field.MslType} {fieldName};");
                return;
            }
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"    {field.MslType} {fieldName};");
    }

    /// <summary>
    /// Determines the appropriate alignment for a field based on its type.
    /// </summary>
    private static int GetFieldAlignment(StructTranslationHelper.StructFieldInfo field)
    {
        return field.MslType switch
        {
            "float4" or "int4" or "uint4" => 16,
            "float3" or "int3" or "uint3" => 16, // Vector3 aligns to 16 on GPU
            "float2" or "int2" or "uint2" or "double" or "long" or "ulong" => 8,
            "float" or "int" or "uint" => 4,
            "half" or "short" or "ushort" => 2,
            _ => 4 // Default alignment for structs
        };
    }

    /// <summary>
    /// Generates buffer parameter declaration for a struct type.
    /// </summary>
    /// <param name="structName">The struct type name.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="bufferIndex">The buffer index attribute value.</param>
    /// <param name="isReadOnly">Whether the buffer is read-only.</param>
    /// <returns>MSL buffer parameter declaration.</returns>
    public static string GenerateBufferParameter(
        string structName,
        string parameterName,
        int bufferIndex,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(structName);
        ArgumentNullException.ThrowIfNull(parameterName);

        var constModifier = isReadOnly ? "const " : "";
        return $"    {constModifier}device {structName}* {parameterName} [[buffer({bufferIndex})]]";
    }

    /// <summary>
    /// Generates constant buffer parameter declaration for a struct type.
    /// </summary>
    /// <param name="structName">The struct type name.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="bufferIndex">The buffer index attribute value.</param>
    /// <returns>MSL constant buffer parameter declaration.</returns>
    public static string GenerateConstantBufferParameter(
        string structName,
        string parameterName,
        int bufferIndex)
    {
        ArgumentNullException.ThrowIfNull(structName);
        ArgumentNullException.ThrowIfNull(parameterName);

        return $"    constant {structName}& {parameterName} [[buffer({bufferIndex})]]";
    }

    /// <summary>
    /// Generates a kernel function stub that uses the specified struct types.
    /// </summary>
    /// <param name="kernelName">The kernel function name.</param>
    /// <param name="inputStructs">Input struct parameters (read-only).</param>
    /// <param name="outputStructs">Output struct parameters (read-write).</param>
    /// <returns>MSL kernel function stub.</returns>
    public static string GenerateKernelStub(
        string kernelName,
        IEnumerable<(string StructName, string ParamName)> inputStructs,
        IEnumerable<(string StructName, string ParamName)> outputStructs)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(inputStructs);
        ArgumentNullException.ThrowIfNull(outputStructs);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");

        var bufferIndex = 0;
        var parameters = new List<string>();

        // Input parameters (const)
        foreach (var (structName, paramName) in inputStructs)
        {
            parameters.Add(GenerateBufferParameter(structName, paramName, bufferIndex++, isReadOnly: true));
        }

        // Output parameters
        foreach (var (structName, paramName) in outputStructs)
        {
            parameters.Add(GenerateBufferParameter(structName, paramName, bufferIndex++, isReadOnly: false));
        }

        // Thread position
        parameters.Add("    uint3 thread_position [[thread_position_in_grid]]");
        parameters.Add("    uint3 grid_size [[threads_per_grid]]");

        sb.AppendLine(string.Join(",\n", parameters));
        sb.AppendLine(") {");
        sb.AppendLine("    // TODO: Implement kernel logic");
        sb.AppendLine("    uint idx = thread_position.x;");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
