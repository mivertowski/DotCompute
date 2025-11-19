// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Maps C# types to CUDA C++ types for kernel code generation.
/// </summary>
/// <remarks>
/// <para>
/// This mapper handles:
/// - Primitive types (int, float, double, etc.)
/// - Pointer types (Span&lt;T&gt; → T*)
/// - Vector types (Vector2, Vector3, Vector4 → float2, float3, float4)
/// - Custom structs (MemoryPackable message types)
/// </para>
/// <para>
/// Type mapping follows CUDA's type system and ensures ABI compatibility
/// between C# and CUDA C++.
/// </para>
/// </remarks>
public static class CudaTypeMapper
{
    /// <summary>
    /// Maps primitive C# types to CUDA C++ types.
    /// </summary>
    private static readonly Dictionary<Type, string> PrimitiveTypeMap = new()
    {
        // Signed integers
        [typeof(sbyte)] = "char",
        [typeof(short)] = "short",
        [typeof(int)] = "int",
        [typeof(long)] = "long long",

        // Unsigned integers
        [typeof(byte)] = "unsigned char",
        [typeof(ushort)] = "unsigned short",
        [typeof(uint)] = "unsigned int",
        [typeof(ulong)] = "unsigned long long",

        // Floating point
        [typeof(float)] = "float",
        [typeof(double)] = "double",

        // Boolean
        [typeof(bool)] = "bool",

        // Character
        [typeof(char)] = "unsigned short", // C# char is UTF-16
    };

    /// <summary>
    /// Gets the CUDA C++ type name for a given C# type.
    /// </summary>
    /// <param name="csType">The C# type to map.</param>
    /// <returns>The CUDA C++ type name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="csType"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the type cannot be mapped to a CUDA type.
    /// </exception>
    public static string GetCudaType(Type csType)
    {
        ArgumentNullException.ThrowIfNull(csType);

        // Check primitive types first
        if (PrimitiveTypeMap.TryGetValue(csType, out var cudaType))
        {
            return cudaType;
        }

        // Handle Span<T> → T*
        if (csType.IsGenericType)
        {
            var genericTypeDef = csType.GetGenericTypeDefinition();

            // Span<T> or ReadOnlySpan<T>
            if (genericTypeDef.Name is "Span`1" or "ReadOnlySpan`1")
            {
                var elementType = csType.GetGenericArguments()[0];
                var elementCudaType = GetCudaType(elementType);
                return $"{elementCudaType}*";
            }
        }

        // Handle arrays → T*
        if (csType.IsArray)
        {
            var elementType = csType.GetElementType()!;
            var elementCudaType = GetCudaType(elementType);
            return $"{elementCudaType}*";
        }

        // Handle pointer types → T*
        if (csType.IsPointer)
        {
            var elementType = csType.GetElementType()!;
            var elementCudaType = GetCudaType(elementType);
            return $"{elementCudaType}*";
        }

        // Handle value types (structs)
        if (csType.IsValueType)
        {
            // Check if it's a known CUDA vector type
            var vectorType = TryGetVectorType(csType);
            if (vectorType != null)
            {
                return vectorType;
            }

            // Custom struct - assume it has a CUDA definition with the same name
            return csType.Name;
        }

        throw new NotSupportedException(
            $"Type '{csType.FullName}' is not supported in CUDA kernels. " +
            $"Supported types: primitives, Span<T>, arrays, pointers, and value types.");
    }

    /// <summary>
    /// Gets the CUDA C++ parameter declaration for a method parameter.
    /// </summary>
    /// <param name="parameter">The parameter info.</param>
    /// <returns>The CUDA parameter declaration (e.g., "float* data").</returns>
    public static string GetCudaParameterDeclaration(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var cudaType = GetCudaType(parameter.ParameterType);
        var paramName = parameter.Name ?? "param";

        // Handle const modifier for ReadOnlySpan<T>
        var isReadOnly = parameter.ParameterType.IsGenericType &&
                        parameter.ParameterType.GetGenericTypeDefinition().Name == "ReadOnlySpan`1";

        if (isReadOnly)
        {
            return $"const {cudaType} {paramName}";
        }

        return $"{cudaType} {paramName}";
    }

    /// <summary>
    /// Gets the CUDA C++ parameter declaration from type and name.
    /// </summary>
    /// <param name="parameterType">The parameter type.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="isReadOnly">Whether the parameter is read-only.</param>
    /// <returns>The CUDA parameter declaration (e.g., "const float* data").</returns>
    public static string GetCudaParameterDeclaration(Type parameterType, string parameterName, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(parameterType);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        var cudaType = GetCudaType(parameterType);

        if (isReadOnly)
        {
            return $"const {cudaType} {parameterName}";
        }

        return $"{cudaType} {parameterName}";
    }

    /// <summary>
    /// Gets the serialized size in bytes for a MemoryPack message type.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>The maximum serialized size in bytes.</returns>
    /// <remarks>
    /// MemoryPack serialization format:
    /// - Header: 256 bytes (MessageId, MessageType, Timestamp, etc.)
    /// - Payload: Up to 65,536 bytes (configurable max)
    /// Total: 65,792 bytes per message
    /// </remarks>
    public static int GetMemoryPackSerializedSize(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        // MemoryPack message structure:
        // - 16 bytes: Guid MessageId
        // - 256 bytes: string MessageType (max length)
        // - 8 bytes: long Timestamp
        // - Remaining: Message-specific fields (up to 64 KB)
        const int headerSize = 16 + 256 + 8; // 280 bytes
        const int maxPayloadSize = 65536;    // 64 KB

        return headerSize + maxPayloadSize;
    }

    /// <summary>
    /// Gets the size in bytes of a CUDA type.
    /// </summary>
    /// <param name="cudaTypeName">The CUDA type name (e.g., "float", "int").</param>
    /// <returns>The size in bytes.</returns>
    public static int GetCudaTypeSize(string cudaTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cudaTypeName);

        // Strip pointer qualifier
        var baseType = cudaTypeName.TrimEnd('*').Trim();

        return baseType switch
        {
            "char" or "unsigned char" or "bool" => 1,
            "short" or "unsigned short" => 2,
            "int" or "unsigned int" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            "float2" => 8,
            "float3" => 12,
            "float4" => 16,
            "double2" => 16,
            "double3" => 24,
            "double4" => 32,
#pragma warning disable XFIX002 // EndsWith(char) is always ordinal, StringComparison not needed
            _ when cudaTypeName.EndsWith('*') => IntPtr.Size, // Pointer types
#pragma warning restore XFIX002
            _ => throw new NotSupportedException($"Unknown CUDA type size for '{cudaTypeName}'")
        };
    }

    /// <summary>
    /// Checks if a type requires special CUDA includes.
    /// </summary>
    /// <param name="csType">The C# type.</param>
    /// <returns>The required CUDA header file, or null if no special header is needed.</returns>
    public static string? GetRequiredCudaHeader(Type csType)
    {
        ArgumentNullException.ThrowIfNull(csType);

        // Vector types require vector_types.h
        if (TryGetVectorType(csType) != null)
        {
            return "vector_types.h";
        }

        // Check for special math types
        if (csType == typeof(Math) || csType.Namespace == "System.Numerics")
        {
            return "math_functions.h";
        }

        return null;
    }

    /// <summary>
    /// Tries to map a C# type to a CUDA vector type (float2, float3, float4, etc.).
    /// </summary>
    private static string? TryGetVectorType(Type csType)
    {
        // Check for System.Numerics vector types
        if (csType.Namespace == "System.Numerics")
        {
            return csType.Name switch
            {
                "Vector2" => "float2",
                "Vector3" => "float3",
                "Vector4" => "float4",
                _ => null
            };
        }

        // Check for custom vector types by name convention
        if (csType.Name.StartsWith("Vector", StringComparison.Ordinal) && csType.IsValueType)
        {
            // Extract dimension from name (e.g., "Vector3" → 3)
            if (int.TryParse(csType.Name.AsSpan(6), out var dimension))
            {
                return dimension switch
                {
                    2 => "float2",
                    3 => "float3",
                    4 => "float4",
                    _ => null
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Generates a CUDA struct definition from a C# type.
    /// </summary>
    /// <param name="csType">The C# type to generate a struct for.</param>
    /// <param name="includeMembers">Whether to include struct members (default: true).</param>
    /// <returns>The CUDA struct definition.</returns>
    /// <remarks>
    /// This generates a CUDA struct that matches the memory layout of the C# type.
    /// The struct uses explicit layout attributes if present, ensuring ABI compatibility.
    /// </remarks>
    public static string GenerateCudaStruct(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type csType,
        bool includeMembers = true)
    {
        ArgumentNullException.ThrowIfNull(csType);

        if (!csType.IsValueType)
        {
            throw new ArgumentException(
                $"Type '{csType.Name}' is not a value type. Only structs can be converted to CUDA structs.",
                nameof(csType));
        }

        var structName = csType.Name;
        var lines = new List<string>
        {
            $"struct {structName} {{" // Opening brace
        };

        if (includeMembers)
        {
            // Get all instance fields
            var fields = csType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                try
                {
                    var cudaType = GetCudaType(field.FieldType);
                    var fieldName = field.Name.TrimStart('_'); // Remove C# field prefix

                    // Check for FieldOffset attribute (explicit layout)
                    var offsetAttr = field.GetCustomAttribute<FieldOffsetAttribute>();
                    if (offsetAttr != null)
                    {
                        lines.Add($"    {cudaType} {fieldName}; // offset: {offsetAttr.Value}");
                    }
                    else
                    {
                        lines.Add($"    {cudaType} {fieldName};");
                    }
                }
                catch (NotSupportedException ex)
                {
                    // Skip unsupported fields with a comment
                    lines.Add($"    // Skipped field '{field.Name}': {ex.Message}");
                }
            }
        }

        lines.Add("};"); // Closing brace

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Checks if a type is supported for CUDA kernel parameters.
    /// </summary>
    /// <param name="csType">The C# type to check.</param>
    /// <returns>True if the type is supported; otherwise, false.</returns>
    public static bool IsTypeSupported(Type csType)
    {
        ArgumentNullException.ThrowIfNull(csType);

        try
        {
            _ = GetCudaType(csType);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
