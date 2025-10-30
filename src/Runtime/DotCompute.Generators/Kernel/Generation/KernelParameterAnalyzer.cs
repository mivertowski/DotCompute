// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Analyzes kernel method parameters to extract type information and memory characteristics.
/// Provides detailed analysis of parameter types, memory access patterns, and compatibility
/// with different backend accelerators.
/// </summary>
/// <remarks>
/// This class specializes in parameter analysis for kernel methods, determining:
/// - Parameter type compatibility with different backends
/// - Memory access patterns (read-only, read-write)
/// - Buffer vs scalar parameter classification
/// - Type mapping for cross-platform code generation
/// </remarks>
public sealed class KernelParameterAnalyzer
{
    /// <summary>
    /// Analyzes all parameters of a kernel method.
    /// </summary>
    /// <param name="methodSymbol">The method symbol containing the parameters to analyze.</param>
    /// <returns>A list of parameter information objects.</returns>
    /// <remarks>
    /// This method performs comprehensive analysis of each parameter including:
    /// - Type analysis and categorization
    /// - Memory access pattern determination
    /// - Backend compatibility assessment
    /// - Buffer vs scalar classification
    /// </remarks>
    public static IReadOnlyList<ParameterInfo> AnalyzeParameters(IMethodSymbol methodSymbol) => [.. methodSymbol.Parameters.Select(AnalyzeParameter)];

    /// <summary>
    /// Analyzes a single parameter and extracts detailed information.
    /// </summary>
    /// <param name="parameter">The parameter symbol to analyze.</param>
    /// <returns>A parameter information object containing analysis results.</returns>
    /// <remarks>
    /// This method examines parameter characteristics including:
    /// - Type information and display string
    /// - Memory buffer classification
    /// - Read-only access determination
    /// - Backend-specific type mapping compatibility
    /// </remarks>
    public static ParameterInfo AnalyzeParameter(IParameterSymbol parameter)
    {
        var typeDisplayString = parameter.Type.ToDisplayString();
        var isBuffer = IsBufferType(parameter.Type);
        var isReadOnly = DetermineReadOnlyStatus(parameter);

        return new ParameterInfo
        {
            Name = parameter.Name,
            Type = typeDisplayString,
            IsBuffer = isBuffer,
            IsReadOnly = isReadOnly
        };
    }

    /// <summary>
    /// Determines if a parameter type represents a memory buffer.
    /// </summary>
    /// <param name="type">The type symbol to examine.</param>
    /// <returns>True if the type is a buffer type; otherwise, false.</returns>
    /// <remarks>
    /// Buffer types include:
    /// - Arrays (T[])
    /// - Span&lt;T&gt; and ReadOnlySpan&lt;T&gt;
    /// - UnifiedBuffer&lt;T&gt; and other DotCompute buffer types
    /// - Pointer types (T*)
    /// - Types implementing IBuffer interface
    /// </remarks>
    private static bool IsBufferType(ITypeSymbol type)
    {
        // Check for array types
        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

        // Check for pointer types
        if (type.TypeKind == TypeKind.Pointer)
        {
            return true;
        }

        // Check for span types
        if (IsSpanType(type))
        {
            return true;
        }

        // Check for DotCompute buffer types
        if (IsDotComputeBufferType(type))
        {
            return true;
        }

        // Check for types implementing IBuffer interface
        if (ImplementsBufferInterface(type))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is a Span or ReadOnlySpan type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a span type; otherwise, false.</returns>
    private static bool IsSpanType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var typeName = namedType.Name;
        return typeName is "Span" or "ReadOnlySpan";
    }

    /// <summary>
    /// Determines if a type is a DotCompute buffer type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a DotCompute buffer type; otherwise, false.</returns>
    private static bool IsDotComputeBufferType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var fullName = namedType.ToDisplayString();
        return fullName.Contains("UnifiedBuffer<") ||
               fullName.Contains("Buffer<") ||
               fullName.Contains("DeviceBuffer<") ||
               fullName.Contains("HostBuffer<");
    }

    /// <summary>
    /// Determines if a type implements a buffer interface.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type implements IBuffer; otherwise, false.</returns>
    private static bool ImplementsBufferInterface(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i =>
            i.Name is "IBuffer" or
            "IUnifiedMemoryBuffer" or
            "IDeviceBuffer");
    }

    /// <summary>
    /// Determines the read-only status of a parameter.
    /// </summary>
    /// <param name="parameter">The parameter to analyze.</param>
    /// <returns>True if the parameter is read-only; otherwise, false.</returns>
    /// <remarks>
    /// Read-only determination is based on:
    /// - RefKind.In parameters
    /// - ReadOnlySpan&lt;T&gt; types
    /// - Const modifier (where applicable)
    /// - Type-level readonly indicators
    /// </remarks>
    private static bool DetermineReadOnlyStatus(IParameterSymbol parameter)
    {
        // Check for 'in' parameter modifier
        if (parameter.RefKind == RefKind.In)
        {
            return true;
        }

        // Check for ReadOnlySpan type
        if (IsReadOnlySpanType(parameter.Type))
        {
            return true;
        }

        // Check for readonly type characteristics
        if (parameter.Type.IsReadOnly)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is ReadOnlySpan&lt;T&gt;.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is ReadOnlySpan; otherwise, false.</returns>
    private static bool IsReadOnlySpanType(ITypeSymbol type) => type is INamedTypeSymbol namedType && namedType.Name == "ReadOnlySpan";

    /// <summary>
    /// Gets the element type from a buffer type.
    /// </summary>
    /// <param name="bufferType">The buffer type to analyze.</param>
    /// <returns>The element type, or null if not a buffer type.</returns>
    /// <remarks>
    /// This method extracts the element type from various buffer types:
    /// - T[] → T
    /// - Span&lt;T&gt; → T
    /// - UnifiedBuffer&lt;T&gt; → T
    /// </remarks>
    public static ITypeSymbol? GetElementType(ITypeSymbol bufferType)
    {
        // Handle array types
        if (bufferType is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        // Handle generic types (Span<T>, Buffer<T>, etc.)
        if (bufferType is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            return namedType.TypeArguments[0];
        }

        // Handle pointer types
        if (bufferType is IPointerTypeSymbol pointerType)
        {
            return pointerType.PointedAtType;
        }

        return null;
    }

    /// <summary>
    /// Validates parameter compatibility with specific backend types.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <param name="backend">The target backend (CPU, CUDA, Metal, etc.).</param>
    /// <returns>True if the parameter is compatible with the backend; otherwise, false.</returns>
    /// <remarks>
    /// Different backends have varying levels of type support:
    /// - CPU: All managed types supported
    /// - CUDA: Primitive types and arrays
    /// - Metal: Metal-compatible types only
    /// - OpenCL: OpenCL-compatible types only
    /// </remarks>
    public static bool ValidateBackendCompatibility(ParameterInfo parameter, string backend)
    {
        return backend switch
        {
            "CPU" => ValidateCpuCompatibility(parameter),
            "CUDA" => ValidateCudaCompatibility(parameter),
            "Metal" => ValidateMetalCompatibility(parameter),
            "OpenCL" => ValidateOpenCLCompatibility(parameter),
            _ => false
        };
    }

    /// <summary>
    /// Validates CPU backend compatibility.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <returns>True if compatible with CPU backend; otherwise, false.</returns>
    private static bool ValidateCpuCompatibility(ParameterInfo parameter)
        // CPU backend supports all managed types

        => true;

    /// <summary>
    /// Validates CUDA backend compatibility.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <returns>True if compatible with CUDA backend; otherwise, false.</returns>
    private static bool ValidateCudaCompatibility(ParameterInfo parameter)
    {
        // CUDA supports primitive types and their arrays/buffers
        var elementType = ExtractElementType(parameter.Type);
        return IsCudaCompatibleType(elementType);
    }

    /// <summary>
    /// Validates Metal backend compatibility.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <returns>True if compatible with Metal backend; otherwise, false.</returns>
    private static bool ValidateMetalCompatibility(ParameterInfo parameter)
    {
        // Metal supports specific primitive types
        var elementType = ExtractElementType(parameter.Type);
        return IsMetalCompatibleType(elementType);
    }

    /// <summary>
    /// Validates OpenCL backend compatibility.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <returns>True if compatible with OpenCL backend; otherwise, false.</returns>
    private static bool ValidateOpenCLCompatibility(ParameterInfo parameter)
    {
        // OpenCL supports standard primitive types
        var elementType = ExtractElementType(parameter.Type);
        return IsOpenCLCompatibleType(elementType);
    }

    /// <summary>
    /// Extracts the base element type from a parameter type string.
    /// </summary>
    /// <param name="typeString">The type string to analyze.</param>
    /// <returns>The element type name.</returns>
    private static string ExtractElementType(string typeString)
    {
        // Handle generic types
        if (typeString.Contains('<') && typeString.Contains('>'))
        {
            var startIndex = typeString.IndexOf('<') + 1;
            var endIndex = typeString.LastIndexOf('>');
            if (startIndex > 0 && endIndex > startIndex)
            {
                return typeString.Substring(startIndex, endIndex - startIndex);
            }
        }

        // Handle array types
        if (typeString.EndsWith("[]", StringComparison.Ordinal))
        {
            return typeString.Substring(0, typeString.Length - 2);
        }

        return typeString;
    }

    /// <summary>
    /// Determines if a type is compatible with CUDA.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if CUDA compatible; otherwise, false.</returns>
    private static bool IsCudaCompatibleType(string typeName)
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
    /// Determines if a type is compatible with Metal.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if Metal compatible; otherwise, false.</returns>
    private static bool IsMetalCompatibleType(string typeName)
    {
        return typeName switch
        {
            "int" or "uint" => true,
            "float" => true, // Metal has limited double support
            "byte" or "sbyte" or "short" or "ushort" => true,
            "bool" => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a type is compatible with OpenCL.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if OpenCL compatible; otherwise, false.</returns>
    private static bool IsOpenCLCompatibleType(string typeName)
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
}
