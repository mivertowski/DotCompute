// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Helper utilities for analyzing and translating C# struct types to Metal Shading Language (MSL).
/// </summary>
public static class StructTranslationHelper
{
    /// <summary>
    /// Information about a struct field for MSL translation.
    /// </summary>
    /// <param name="Name">The field name in C# (will be snake_cased for MSL).</param>
    /// <param name="CSharpType">The C# type name.</param>
    /// <param name="MslType">The corresponding MSL type name.</param>
    /// <param name="Size">Size in bytes.</param>
    /// <param name="Offset">Offset in the struct (calculated).</param>
    /// <param name="IsArray">Whether this is an array type.</param>
    /// <param name="ArrayElementType">The element type if this is an array.</param>
    public sealed record StructFieldInfo(
        string Name,
        string CSharpType,
        string MslType,
        int Size,
        int Offset,
        bool IsArray,
        string? ArrayElementType);

    /// <summary>
    /// Information about a struct for MSL translation.
    /// </summary>
    /// <param name="Name">The struct name.</param>
    /// <param name="Fields">The fields in declaration order.</param>
    /// <param name="TotalSize">Total size in bytes.</param>
    /// <param name="Alignment">Required alignment in bytes.</param>
    public sealed record StructInfo(
        string Name,
        IReadOnlyList<StructFieldInfo> Fields,
        int TotalSize,
        int Alignment);

    /// <summary>
    /// Analyzes a C# struct type and extracts field information for MSL translation.
    /// </summary>
    /// <param name="structType">The struct type to analyze.</param>
    /// <returns>Struct information for MSL generation.</returns>
    public static StructInfo AnalyzeStruct(Type structType)
    {
        ArgumentNullException.ThrowIfNull(structType);

        if (!structType.IsValueType || structType.IsPrimitive || structType.IsEnum)
        {
            throw new ArgumentException($"Type {structType.Name} is not a struct", nameof(structType));
        }

        var fields = new List<StructFieldInfo>();
        var currentOffset = 0;
        var maxAlignment = 4; // Minimum alignment for MSL

        foreach (var field in structType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var fieldInfo = AnalyzeField(field, ref currentOffset);
            fields.Add(fieldInfo);

            var fieldAlignment = GetTypeAlignment(field.FieldType);
            maxAlignment = Math.Max(maxAlignment, fieldAlignment);
        }

        // Round up to alignment
        var totalSize = AlignTo(currentOffset, maxAlignment);

        return new StructInfo(
            Name: structType.Name,
            Fields: fields,
            TotalSize: totalSize,
            Alignment: maxAlignment);
    }

    /// <summary>
    /// Analyzes a single field for MSL translation.
    /// </summary>
    private static StructFieldInfo AnalyzeField(FieldInfo field, ref int currentOffset)
    {
        var fieldType = field.FieldType;
        var alignment = GetTypeAlignment(fieldType);

        // Align offset
        currentOffset = AlignTo(currentOffset, alignment);

        var isArray = fieldType.IsArray;
        var elementType = isArray ? fieldType.GetElementType()?.Name : null;
        var mslType = GetMslTypeName(fieldType);
        var size = GetTypeSize(fieldType);

        var fieldInfo = new StructFieldInfo(
            Name: field.Name,
            CSharpType: GetCSharpTypeName(fieldType),
            MslType: mslType,
            Size: size,
            Offset: currentOffset,
            IsArray: isArray,
            ArrayElementType: elementType);

        currentOffset += size;

        return fieldInfo;
    }

    /// <summary>
    /// Gets the MSL type name for a C# type.
    /// </summary>
    /// <param name="type">The C# type.</param>
    /// <returns>The MSL equivalent type name.</returns>
    public static string GetMslTypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Handle arrays
        if (underlyingType.IsArray)
        {
            var elementType = underlyingType.GetElementType();
            return elementType != null
                ? $"device {GetMslTypeName(elementType)}*"
                : "device void*";
        }

        // Primitive type mappings
        return underlyingType.FullName switch
        {
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Half" => "half",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.SByte" => "char",
            "System.Byte" => "uchar",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Boolean" => "bool",
            "System.Numerics.Vector2" => "float2",
            "System.Numerics.Vector3" => "float3",
            "System.Numerics.Vector4" => "float4",
            _ when underlyingType.IsValueType && !underlyingType.IsPrimitive => underlyingType.Name,
            _ => throw new NotSupportedException($"Type {underlyingType.Name} is not supported for MSL translation")
        };
    }

    /// <summary>
    /// Gets the MSL type name from a C# type string.
    /// </summary>
    /// <param name="csharpType">The C# type name as a string.</param>
    /// <returns>The MSL equivalent type name.</returns>
    public static string GetMslTypeName(string csharpType)
    {
        ArgumentNullException.ThrowIfNull(csharpType);

        // Handle array types
        if (csharpType.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = csharpType[..^2];
            return $"device {GetMslTypeName(elementType)}*";
        }

        return csharpType switch
        {
            "float" or "Single" or "System.Single" => "float",
            "double" or "Double" or "System.Double" => "double",
            "Half" or "System.Half" => "half",
            "int" or "Int32" or "System.Int32" => "int",
            "uint" or "UInt32" or "System.UInt32" => "uint",
            "short" or "Int16" or "System.Int16" => "short",
            "ushort" or "UInt16" or "System.UInt16" => "ushort",
            "sbyte" or "SByte" or "System.SByte" => "char",
            "byte" or "Byte" or "System.Byte" => "uchar",
            "long" or "Int64" or "System.Int64" => "long",
            "ulong" or "UInt64" or "System.UInt64" => "ulong",
            "bool" or "Boolean" or "System.Boolean" => "bool",
            "Vector2" or "System.Numerics.Vector2" => "float2",
            "Vector3" or "System.Numerics.Vector3" => "float3",
            "Vector4" or "System.Numerics.Vector4" => "float4",
            _ => csharpType // Assume it's a struct name
        };
    }

    /// <summary>
    /// Converts a C# identifier to MSL snake_case convention.
    /// </summary>
    /// <param name="identifier">The C# identifier (PascalCase or camelCase).</param>
    /// <returns>The snake_case identifier for MSL.</returns>
    public static string ToSnakeCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Gets the size in bytes for a C# type.
    /// </summary>
    private static int GetTypeSize(Type type)
    {
        if (type.IsArray)
        {
            return IntPtr.Size; // Pointer size
        }

        return type.FullName switch
        {
            "System.Single" => 4,
            "System.Double" => 8,
            "System.Half" => 2,
            "System.Int32" => 4,
            "System.UInt32" => 4,
            "System.Int16" => 2,
            "System.UInt16" => 2,
            "System.SByte" => 1,
            "System.Byte" => 1,
            "System.Int64" => 8,
            "System.UInt64" => 8,
            "System.Boolean" => 1,
            "System.Numerics.Vector2" => 8,
            "System.Numerics.Vector3" => 12,
            "System.Numerics.Vector4" => 16,
            _ when type.IsValueType => System.Runtime.InteropServices.Marshal.SizeOf(type),
            _ => IntPtr.Size // Pointer for reference types
        };
    }

    /// <summary>
    /// Gets the alignment in bytes for a C# type.
    /// </summary>
    private static int GetTypeAlignment(Type type)
    {
        if (type.IsArray)
        {
            return IntPtr.Size; // Pointer alignment
        }

        return type.FullName switch
        {
            "System.Single" => 4,
            "System.Double" => 8,
            "System.Half" => 2,
            "System.Int32" => 4,
            "System.UInt32" => 4,
            "System.Int16" => 2,
            "System.UInt16" => 2,
            "System.SByte" => 1,
            "System.Byte" => 1,
            "System.Int64" => 8,
            "System.UInt64" => 8,
            "System.Boolean" => 1,
            "System.Numerics.Vector2" => 8,
            "System.Numerics.Vector3" => 16, // Vector3 aligns to 16 on GPU
            "System.Numerics.Vector4" => 16,
            _ when type.IsValueType => 4, // Default struct alignment
            _ => IntPtr.Size
        };
    }

    /// <summary>
    /// Gets the C# type name for a type.
    /// </summary>
    private static string GetCSharpTypeName(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType != null ? $"{GetCSharpTypeName(elementType)}[]" : "object[]";
        }

        return type.FullName switch
        {
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Half" => "Half",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.SByte" => "sbyte",
            "System.Byte" => "byte",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Boolean" => "bool",
            _ => type.Name
        };
    }

    /// <summary>
    /// Aligns a value to the specified alignment.
    /// </summary>
    private static int AlignTo(int value, int alignment)
    {
        return (value + alignment - 1) / alignment * alignment;
    }

    /// <summary>
    /// Checks if a type requires a struct definition in MSL.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type needs a struct definition.</returns>
    public static bool RequiresStructDefinition(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsValueType &&
               !type.IsPrimitive &&
               !type.IsEnum &&
               type.FullName is not (
                   "System.Numerics.Vector2" or
                   "System.Numerics.Vector3" or
                   "System.Numerics.Vector4" or
                   "System.Half");
    }

    /// <summary>
    /// Checks if a type string represents a primitive MSL type.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the type is a primitive MSL type.</returns>
    public static bool IsPrimitiveMslType(string typeName)
    {
        return typeName is
            "float" or "double" or "half" or
            "int" or "uint" or
            "short" or "ushort" or
            "char" or "uchar" or
            "long" or "ulong" or
            "bool" or
            "float2" or "float3" or "float4" or
            "int2" or "int3" or "int4" or
            "uint2" or "uint3" or "uint4";
    }
}
