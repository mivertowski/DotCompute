// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Text;

namespace DotCompute.Generators.MemoryPack;

/// <summary>
/// Generates CUDA C code for MemoryPack serialization/deserialization.
/// </summary>
/// <remarks>
/// <para>
/// This generator produces optimized CUDA code that matches MemoryPack's binary format:
/// - Struct definitions with proper alignment
/// - Deserialize functions with bounds checking
/// - Serialize functions for GPU-to-CPU communication
/// - Guid handling with byte array representation
/// - Nullable type support with presence bytes
/// </para>
/// <para>
/// <b>Generated Code Pattern:</b>
/// <code>
/// // Struct definition
/// struct VectorAddRequest {
///     unsigned char message_id[16];
///     unsigned char priority;
///     struct { bool has_value; unsigned char value[16]; } correlation_id;
///     float a;
///     float b;
/// };
///
/// // Deserialize function
/// __device__ bool deserialize_vector_add_request(
///     const unsigned char* buffer,
///     int buffer_size,
///     VectorAddRequest* out)
/// {
///     if (buffer_size &lt; 42) return false;
///     // Field deserialization with byte offsets...
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class MemoryPackCudaGenerator
{
    /// <summary>
    /// Generates complete CUDA code for a MemoryPack type.
    /// </summary>
    /// <param name="spec">The binary format specification to generate code for.</param>
    /// <returns>Complete CUDA C code with struct and serialization functions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when spec is null.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822:Mark members as static", Justification = "Instance method allows for future configuration and state management")]
    public string GenerateCode(BinaryFormatSpecification spec)
    {
        if (spec == null)
        {
            throw new ArgumentNullException(nameof(spec));
        }

        var sb = new StringBuilder();

        // Header comment
        sb.AppendLine("// Auto-generated MemoryPack CUDA serialization code");
        sb.AppendLine($"// Type: {spec.TypeName}");
        sb.AppendLine($"// Total Size: {spec.TotalSize} bytes");
        sb.AppendLine($"// Fixed Size: {(spec.IsFixedSize ? "Yes" : "No")}");
        sb.AppendLine();

        // Generate struct definition
        GenerateStructDefinition(sb, spec);
        sb.AppendLine();

        // Generate deserialize function
        GenerateDeserializeFunction(sb, spec);
        sb.AppendLine();

        // Generate serialize function
        GenerateSerializeFunction(sb, spec);

        return sb.ToString();
    }

    /// <summary>
    /// Generates CUDA struct definition matching MemoryPack binary layout.
    /// </summary>
    private static void GenerateStructDefinition(StringBuilder sb, BinaryFormatSpecification spec)
    {
        var structName = GetStructName(spec.TypeName);

        sb.AppendLine($"struct {structName}");
        sb.AppendLine("{");

        foreach (var field in spec.Fields)
        {
            sb.Append("    ");

            // Handle array types: "unsigned char[16]" → "unsigned char" + "[16]"
            // BUT: Don't touch complex struct types like "struct { ... }"
            var cudaType = field.CudaTypeName;
            var arrayPart = string.Empty;

            // Only apply bracket transformation to simple types, not structs
            if (!cudaType.StartsWith("struct ", StringComparison.Ordinal))
            {
                var bracketIndex = cudaType.IndexOf('[');
                if (bracketIndex >= 0)
                {
                    arrayPart = cudaType.Substring(bracketIndex); // Extract "[16]"
                    cudaType = cudaType.Substring(0, bracketIndex).TrimEnd(); // Extract "unsigned char"
                }
            }

            sb.Append(cudaType);
            sb.Append(' ');
            sb.Append(GetFieldName(field.Name));
            sb.Append(arrayPart); // Add array brackets after field name
            sb.AppendLine(";");
        }

        sb.AppendLine("};");
    }

    /// <summary>
    /// Generates CUDA deserialize function with bounds checking.
    /// </summary>
    private static void GenerateDeserializeFunction(StringBuilder sb, BinaryFormatSpecification spec)
    {
        var structName = GetStructName(spec.TypeName);
        var functionName = GetDeserializeFunctionName(spec.TypeName);

        sb.AppendLine($"__device__ bool {functionName}(");
        sb.AppendLine("    const unsigned char* buffer,");
        sb.AppendLine("    int buffer_size,");
        sb.AppendLine($"    {structName}* out)");
        sb.AppendLine("{");

        // Bounds check
        sb.AppendLine($"    // Bounds check: message must be at least {spec.TotalSize} bytes");
        sb.AppendLine($"    if (buffer_size < {spec.TotalSize})");
        sb.AppendLine("    {");
        sb.AppendLine("        return false; // Buffer too small");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Field deserialization
        sb.AppendLine("    // Deserialize fields in MemoryPack order");
        foreach (var field in spec.Fields)
        {
            GenerateFieldDeserialization(sb, field);
        }

        sb.AppendLine();
        sb.AppendLine("    return true; // Deserialization successful");
        sb.AppendLine("}");
    }

    /// <summary>
    /// Generates CUDA serialize function for GPU-to-CPU communication.
    /// </summary>
    private static void GenerateSerializeFunction(StringBuilder sb, BinaryFormatSpecification spec)
    {
        var structName = GetStructName(spec.TypeName);
        var functionName = GetSerializeFunctionName(spec.TypeName);

        sb.AppendLine($"__device__ void {functionName}(");
        sb.AppendLine($"    const {structName}* input,");
        sb.AppendLine("    unsigned char* buffer)");
        sb.AppendLine("{");

        // Field serialization
        sb.AppendLine("    // Serialize fields in MemoryPack order");
        foreach (var field in spec.Fields)
        {
            GenerateFieldSerialization(sb, field);
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Generates deserialization code for a single field.
    /// </summary>
    private static void GenerateFieldDeserialization(StringBuilder sb, FieldSpecification field)
    {
        var fieldName = GetFieldName(field.Name);

        sb.AppendLine($"    // {field.Name}: {field.CSharpTypeName} at offset {field.Offset} ({field.Size} bytes)");

        if (field.FieldType == FieldType.Guid && !field.IsNullable)
        {
            // Guid: copy 16 bytes directly
            sb.AppendLine($"    #pragma unroll");
            sb.AppendLine($"    for (int i = 0; i < 16; i++)");
            sb.AppendLine("    {");
            sb.AppendLine($"        out->{fieldName}[i] = buffer[{field.Offset} + i];");
            sb.AppendLine("    }");
        }
        else if (field.FieldType == FieldType.Guid && field.IsNullable)
        {
            // Nullable Guid: presence byte + 16 bytes
            sb.AppendLine($"    out->{fieldName}.has_value = buffer[{field.Offset}] != 0;");
            sb.AppendLine($"    if (out->{fieldName}.has_value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        #pragma unroll");
            sb.AppendLine($"        for (int i = 0; i < 16; i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            out->{fieldName}.value[i] = buffer[{field.Offset + 1} + i];");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        else if (field.IsNullable)
        {
            // Nullable primitive: presence byte + value
            var typeSize = field.Size - 1; // Subtract presence byte
            sb.AppendLine($"    out->{fieldName}.has_value = buffer[{field.Offset}] != 0;");
            sb.AppendLine($"    if (out->{fieldName}.has_value)");
            sb.AppendLine("    {");
            GeneratePrimitiveRead(sb, field.FieldType, $"out->{fieldName}.value", field.Offset + 1, typeSize, "        ");
            sb.AppendLine("    }");
        }
        else
        {
            // Non-nullable primitive
            GeneratePrimitiveRead(sb, field.FieldType, $"out->{fieldName}", field.Offset, field.Size, "    ");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Generates serialization code for a single field.
    /// </summary>
    private static void GenerateFieldSerialization(StringBuilder sb, FieldSpecification field)
    {
        var fieldName = GetFieldName(field.Name);

        sb.AppendLine($"    // {field.Name}: {field.CSharpTypeName} at offset {field.Offset}");

        if (field.FieldType == FieldType.Guid && !field.IsNullable)
        {
            // Guid: copy 16 bytes directly
            sb.AppendLine($"    #pragma unroll");
            sb.AppendLine($"    for (int i = 0; i < 16; i++)");
            sb.AppendLine("    {");
            sb.AppendLine($"        buffer[{field.Offset} + i] = input->{fieldName}[i];");
            sb.AppendLine("    }");
        }
        else if (field.FieldType == FieldType.Guid && field.IsNullable)
        {
            // Nullable Guid: presence byte + 16 bytes
            sb.AppendLine($"    buffer[{field.Offset}] = input->{fieldName}.has_value ? 1 : 0;");
            sb.AppendLine($"    if (input->{fieldName}.has_value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        #pragma unroll");
            sb.AppendLine($"        for (int i = 0; i < 16; i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            buffer[{field.Offset + 1} + i] = input->{fieldName}.value[i];");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        else if (field.IsNullable)
        {
            // Nullable primitive: presence byte + value
            sb.AppendLine($"    buffer[{field.Offset}] = input->{fieldName}.has_value ? 1 : 0;");
            sb.AppendLine($"    if (input->{fieldName}.has_value)");
            sb.AppendLine("    {");
            GeneratePrimitiveWrite(sb, field.FieldType, $"input->{fieldName}.value", field.Offset + 1, "        ");
            sb.AppendLine("    }");
        }
        else
        {
            // Non-nullable primitive
            GeneratePrimitiveWrite(sb, field.FieldType, $"input->{fieldName}", field.Offset, "    ");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Generates code to read a primitive value from buffer.
    /// </summary>
    private static void GeneratePrimitiveRead(StringBuilder sb, FieldType fieldType, string targetVar, int offset, int size, string indent)
    {
        switch (fieldType)
        {
            case FieldType.Byte:
            case FieldType.SByte:
            case FieldType.Boolean:
                sb.AppendLine($"{indent}{targetVar} = buffer[{offset}];");
                break;

            case FieldType.Int16:
            case FieldType.UInt16:
                sb.AppendLine($"{indent}{targetVar} = *reinterpret_cast<const {GetCudaTypeName(fieldType)}*>(&buffer[{offset}]);");
                break;

            case FieldType.Int32:
            case FieldType.UInt32:
            case FieldType.Float:
                sb.AppendLine($"{indent}{targetVar} = *reinterpret_cast<const {GetCudaTypeName(fieldType)}*>(&buffer[{offset}]);");
                break;

            case FieldType.Int64:
            case FieldType.UInt64:
            case FieldType.Double:
                sb.AppendLine($"{indent}{targetVar} = *reinterpret_cast<const {GetCudaTypeName(fieldType)}*>(&buffer[{offset}]);");
                break;

            default:
                sb.AppendLine($"{indent}// Unsupported type: {fieldType}");
                break;
        }
    }

    /// <summary>
    /// Generates code to write a primitive value to buffer.
    /// </summary>
    private static void GeneratePrimitiveWrite(StringBuilder sb, FieldType fieldType, string sourceVar, int offset, string indent)
    {
        switch (fieldType)
        {
            case FieldType.Byte:
            case FieldType.SByte:
            case FieldType.Boolean:
                sb.AppendLine($"{indent}buffer[{offset}] = {sourceVar};");
                break;

            case FieldType.Int16:
            case FieldType.UInt16:
            case FieldType.Int32:
            case FieldType.UInt32:
            case FieldType.Float:
            case FieldType.Int64:
            case FieldType.UInt64:
            case FieldType.Double:
                sb.AppendLine($"{indent}*reinterpret_cast<{GetCudaTypeName(fieldType)}*>(&buffer[{offset}]) = {sourceVar};");
                break;

            default:
                sb.AppendLine($"{indent}// Unsupported type: {fieldType}");
                break;
        }
    }

    /// <summary>
    /// Converts FieldType to CUDA type name.
    /// </summary>
    private static string GetCudaTypeName(FieldType fieldType)
    {
        return fieldType switch
        {
            FieldType.Byte => "uint8_t",
            FieldType.SByte => "int8_t",
            FieldType.Int16 => "int16_t",
            FieldType.UInt16 => "uint16_t",
            FieldType.Int32 => "int32_t",
            FieldType.UInt32 => "uint32_t",
            FieldType.Int64 => "int64_t",
            FieldType.UInt64 => "uint64_t",
            FieldType.Float => "float",
            FieldType.Double => "double",
            FieldType.Boolean => "bool",
            _ => "void*"
        };
    }

    /// <summary>
    /// Converts C# type name to CUDA struct name (snake_case).
    /// </summary>
    private static string GetStructName(string typeName)
    {
        // Extract simple name from fully qualified name
        var simpleName = typeName.Split('.').Last();

        // Convert PascalCase to snake_case
        var sb = new StringBuilder();
        for (int i = 0; i < simpleName.Length; i++)
        {
            if (i > 0 && char.IsUpper(simpleName[i]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(simpleName[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts C# property name to CUDA field name (snake_case).
    /// </summary>
    private static string GetFieldName(string propertyName)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < propertyName.Length; i++)
        {
            if (i > 0 && char.IsUpper(propertyName[i]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(propertyName[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets deserialize function name for a type.
    /// </summary>
    private static string GetDeserializeFunctionName(string typeName)
    {
        return $"deserialize_{GetStructName(typeName)}";
    }

    /// <summary>
    /// Gets serialize function name for a type.
    /// </summary>
    private static string GetSerializeFunctionName(string typeName)
    {
        return $"serialize_{GetStructName(typeName)}";
    }
}
