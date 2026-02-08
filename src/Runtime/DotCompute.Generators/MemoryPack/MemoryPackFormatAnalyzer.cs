// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.MemoryPack;

/// <summary>
/// Analyzes MemoryPack-attributed types to extract binary serialization format specifications.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer examines types decorated with <c>[MemoryPackable]</c> attribute and determines:
/// - Field serialization order (declaration order in C#)
/// - Binary layout with byte offsets
/// - Type mappings from C# to CUDA C
/// - Total message size and alignment requirements
/// </para>
/// <para>
/// <b>MemoryPack Serialization Rules:</b>
/// - Fields are serialized in declaration order
/// - Nullable types have a presence byte (1 = present, 0 = null) followed by value
/// - Guid is serialized as 16 bytes in standard byte order
/// - Primitives are little-endian
/// - No padding between fields (packed format)
/// </para>
/// <para>
/// <b>Usage Example:</b>
/// <code>
/// var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
/// var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
/// var spec = analyzer.AnalyzeType(typeSymbol);
/// // spec.TotalSize == 41 for VectorAddRequest
/// </code>
/// </para>
/// </remarks>
public sealed class MemoryPackFormatAnalyzer
{
    private readonly SemanticModel _semanticModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPackFormatAnalyzer"/> class.
    /// </summary>
    /// <param name="semanticModel">The Roslyn semantic model for type analysis.</param>
    public MemoryPackFormatAnalyzer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
    }

    /// <summary>
    /// Analyzes a type and extracts its binary format specification.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to analyze (must have [MemoryPackable] attribute).</param>
    /// <returns>The binary format specification for CUDA code generation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when typeSymbol is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when type is not [MemoryPackable].</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822:Mark members as static", Justification = "SemanticModel may be needed for future complex type analysis")]
    public BinaryFormatSpecification AnalyzeType(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
        {
            throw new ArgumentNullException(nameof(typeSymbol));
        }

        // Verify type has [MemoryPackable] attribute
        if (!HasMemoryPackableAttribute(typeSymbol))
        {
            throw new InvalidOperationException(
                $"Type '{typeSymbol.ToDisplayString()}' does not have [MemoryPackable] attribute. " +
                "Only MemoryPack-attributed types can be analyzed for binary format.");
        }

        var fields = ImmutableArray.CreateBuilder<FieldSpecification>();
        var currentOffset = 0;
        var maxAlignment = 1;
        var isFixedSize = true;

        // Analyze all instance properties and fields in declaration order
        // MemoryPack serializes properties, not private fields
        var members = typeSymbol.GetMembers()
            .Where(m => m.Kind == SymbolKind.Property)
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsIndexer && p.GetMethod != null && p.SetMethod != null)
            .OrderBy(p => p.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0) // Declaration order
            .ToList();

        foreach (var property in members)
        {
            var fieldSpec = AnalyzeProperty(property, ref currentOffset, ref maxAlignment);
            fields.Add(fieldSpec);

            if (!fieldSpec.IsCollection || fieldSpec.FixedLength.HasValue)
            {
                // Fixed-size field is OK
            }
            else
            {
                // Variable-size collection makes the whole message variable-size
                isFixedSize = false;
            }
        }

        return new BinaryFormatSpecification(
            typeName: typeSymbol.ToDisplayString(),
            fields: fields.ToImmutable(),
            totalSize: currentOffset,
            isFixedSize: isFixedSize,
            alignment: maxAlignment);
    }

    private static FieldSpecification AnalyzeProperty(IPropertySymbol property, ref int currentOffset, ref int maxAlignment)
    {
        var propertyType = property.Type;
        var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated;

        // Handle nullable types
        ITypeSymbol underlyingType = propertyType;
        if (isNullable && propertyType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // Nullable<T> - extract T
            underlyingType = namedType.TypeArguments[0];
        }

        // Determine field type and size
        var (fieldType, size, alignment) = GetFieldTypeInfo(underlyingType, isNullable);
        var cudaTypeName = GetCudaTypeName(fieldType, underlyingType, isNullable);

        // Nullable fields have a presence byte
        var presenceByteSize = isNullable ? 1 : 0;
        var totalSize = presenceByteSize + size;

        // Use fully qualified type name (System.Byte instead of byte)
        // For primitive types, manually map to System.* names since ToDisplayString returns simple names
        var fullyQualifiedTypeName = underlyingType.SpecialType != SpecialType.None
            ? GetFullyQualifiedPrimitiveTypeName(underlyingType.SpecialType)
            : underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

        var spec = new FieldSpecification(
            name: property.Name,
            fieldType: fieldType,
            csharpTypeName: fullyQualifiedTypeName,
            cudaTypeName: cudaTypeName,
            offset: currentOffset,
            size: totalSize,
            isNullable: isNullable,
            isCollection: fieldType == FieldType.Array,
            alignment: alignment,
            elementType: null, // TODO: Implement for collections
            fixedLength: null); // TODO: Implement for fixed-length arrays

        currentOffset += totalSize;
        maxAlignment = Math.Max(maxAlignment, alignment);

        return spec;
    }

    private static (FieldType fieldType, int size, int alignment) GetFieldTypeInfo(ITypeSymbol type, bool isNullable)
    {
        var typeName = type.SpecialType != SpecialType.None
            ? type.SpecialType.ToString()
            : type.ToDisplayString();

        return typeName switch
        {
            "System_Byte" or "System.Byte" or "byte" => (FieldType.Byte, 1, 1),
            "System_SByte" or "System.SByte" or "sbyte" => (FieldType.SByte, 1, 1),
            "System_Int16" or "System.Int16" or "short" => (FieldType.Int16, 2, 2),
            "System_UInt16" or "System.UInt16" or "ushort" => (FieldType.UInt16, 2, 2),
            "System_Int32" or "System.Int32" or "int" => (FieldType.Int32, 4, 4),
            "System_UInt32" or "System.UInt32" or "uint" => (FieldType.UInt32, 4, 4),
            "System_Int64" or "System.Int64" or "long" => (FieldType.Int64, 8, 8),
            "System_UInt64" or "System.UInt64" or "ulong" => (FieldType.UInt64, 8, 8),
            "System_Single" or "System.Single" or "float" => (FieldType.Float, 4, 4),
            "System_Double" or "System.Double" or "double" => (FieldType.Double, 8, 8),
            "System_Boolean" or "System.Boolean" or "bool" => (FieldType.Boolean, 1, 1),
            "System.Guid" => (FieldType.Guid, 16, 16), // Guid is 16 bytes with 16-byte alignment
            "System.String" or "string" => (FieldType.String, -1, 4), // Variable length
            _ => (FieldType.Unknown, 0, 1)
        };
    }

    private static string GetCudaTypeName(FieldType fieldType, ITypeSymbol type, bool isNullable)
    {
        if (isNullable)
        {
            // Nullable fields need special struct with presence flag
            var baseType = GetCudaTypeName(fieldType, type, false);

            // Handle array types inside struct: "unsigned char[16]" → "unsigned char" + "value[16]"
            var bracketIndex = baseType.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var cudaBaseType = baseType.Substring(0, bracketIndex).TrimEnd();
                var arrayPart = baseType.Substring(bracketIndex);
                return $"struct {{ bool has_value; {cudaBaseType} value{arrayPart}; }}";
            }

            return $"struct {{ bool has_value; {baseType} value; }}";
        }

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
            FieldType.Guid => "unsigned char[16]", // Guid as byte array
            FieldType.String => "char*", // Pointer for variable-length string
            FieldType.Array => "void*", // Pointer for variable-length array
            _ => "void*" // Unknown type fallback
        };
    }

    private static string GetFullyQualifiedPrimitiveTypeName(SpecialType specialType)
    {
        return specialType switch
        {
            SpecialType.System_Byte => "System.Byte",
            SpecialType.System_SByte => "System.SByte",
            SpecialType.System_Int16 => "System.Int16",
            SpecialType.System_UInt16 => "System.UInt16",
            SpecialType.System_Int32 => "System.Int32",
            SpecialType.System_UInt32 => "System.UInt32",
            SpecialType.System_Int64 => "System.Int64",
            SpecialType.System_UInt64 => "System.UInt64",
            SpecialType.System_Single => "System.Single",
            SpecialType.System_Double => "System.Double",
            SpecialType.System_Boolean => "System.Boolean",
            SpecialType.System_Char => "System.Char",
            SpecialType.System_String => "System.String",
            SpecialType.System_Object => "System.Object",
            _ => specialType.ToString() // Fallback
        };
    }

    private static bool HasMemoryPackableAttribute(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "MemoryPackableAttribute" ||
                        attr.AttributeClass?.ToDisplayString() == "MemoryPack.MemoryPackableAttribute");
    }
}
