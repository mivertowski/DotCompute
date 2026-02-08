// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Linq;

namespace DotCompute.Generators.MemoryPack;

/// <summary>
/// Represents the binary serialization format specification for a MemoryPack-attributed type.
/// </summary>
/// <remarks>
/// This specification describes how a C# type is laid out in binary format by MemoryPack,
/// enabling automatic generation of matching CUDA serialization code.
/// </remarks>
public sealed class BinaryFormatSpecification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryFormatSpecification"/> class.
    /// </summary>
    public BinaryFormatSpecification(
        string typeName,
        ImmutableArray<FieldSpecification> fields,
        int totalSize,
        bool isFixedSize,
        int alignment)
    {
        TypeName = typeName;
        Fields = fields;
        TotalSize = totalSize;
        IsFixedSize = isFixedSize;
        Alignment = alignment;
    }

    /// <summary>
    /// Gets the fully qualified type name (e.g., "MyNamespace.VectorAddRequest").
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the list of fields in serialization order.
    /// </summary>
    /// <remarks>
    /// MemoryPack serializes fields in declaration order. This list maintains that order.
    /// </remarks>
    public ImmutableArray<FieldSpecification> Fields { get; }

    /// <summary>
    /// Gets the total size of the serialized message in bytes.
    /// </summary>
    /// <remarks>
    /// This is the sum of all field sizes. For variable-size messages (with collections),
    /// this represents the minimum size (header + fixed fields).
    /// </remarks>
    public int TotalSize { get; }

    /// <summary>
    /// Gets a value indicating whether the message has a fixed size.
    /// </summary>
    /// <remarks>
    /// Fixed-size messages contain only primitive types and fixed-length arrays.
    /// Variable-size messages contain collections (List, Array with runtime length, strings).
    /// </remarks>
    public bool IsFixedSize { get; }

    /// <summary>
    /// Gets the alignment requirement in bytes.
    /// </summary>
    /// <remarks>
    /// CUDA requires proper alignment for coalesced memory access.
    /// Typically 4 bytes for float, 8 bytes for double/long, 16 bytes for Guid.
    /// </remarks>
    public int Alignment { get; }

    /// <summary>
    /// Gets a value indicating whether this type contains any Guid fields.
    /// </summary>
    /// <remarks>
    /// Guid requires special handling (16 bytes, specific byte order matching C#).
    /// </remarks>
    public bool HasGuidFields => Fields.Any(f => f.FieldType == FieldType.Guid);

    /// <summary>
    /// Gets a value indicating whether this type contains any nullable fields.
    /// </summary>
    /// <remarks>
    /// Nullable fields require presence byte handling in MemoryPack format.
    /// </remarks>
    public bool HasNullableFields => Fields.Any(f => f.IsNullable);

    /// <summary>
    /// Gets a value indicating whether this type is a valid ring kernel message.
    /// </summary>
    /// <remarks>
    /// Ring kernel messages should be fixed-size for optimal GPU performance.
    /// </remarks>
    public bool IsValidRingKernelMessage => IsFixedSize && TotalSize <= 65536;
}

/// <summary>
/// Represents a single field in the binary format specification.
/// </summary>
public sealed class FieldSpecification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldSpecification"/> class.
    /// </summary>
    public FieldSpecification(
        string name,
        FieldType fieldType,
        string csharpTypeName,
        string cudaTypeName,
        int offset,
        int size,
        bool isNullable,
        bool isCollection,
        int alignment,
        FieldType? elementType = null,
        int? fixedLength = null)
    {
        Name = name;
        FieldType = fieldType;
        CSharpTypeName = csharpTypeName;
        CudaTypeName = cudaTypeName;
        Offset = offset;
        Size = size;
        IsNullable = isNullable;
        IsCollection = isCollection;
        Alignment = alignment;
        ElementType = elementType;
        FixedLength = fixedLength;
    }

    /// <summary>
    /// Gets the field name as declared in C#.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the field type category.
    /// </summary>
    public FieldType FieldType { get; }

    /// <summary>
    /// Gets the fully qualified C# type name (e.g., "System.Single", "System.Guid").
    /// </summary>
    public string CSharpTypeName { get; }

    /// <summary>
    /// Gets the corresponding CUDA C type name (e.g., "float", "unsigned char[16]").
    /// </summary>
    public string CudaTypeName { get; }

    /// <summary>
    /// Gets the byte offset of this field in the serialized message.
    /// </summary>
    /// <remarks>
    /// Offset is calculated based on MemoryPack serialization order and alignment rules.
    /// </remarks>
    public int Offset { get; }

    /// <summary>
    /// Gets the size of this field in bytes.
    /// </summary>
    /// <remarks>
    /// For primitive types, this is the type size (e.g., 4 bytes for float).
    /// For Guid, this is 16 bytes.
    /// For nullable types, this includes the presence byte.
    /// </remarks>
    public int Size { get; }

    /// <summary>
    /// Gets a value indicating whether this field is nullable.
    /// </summary>
    /// <remarks>
    /// Nullable fields have an extra presence byte before the value in MemoryPack format.
    /// </remarks>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets a value indicating whether this field is an array or collection.
    /// </summary>
    public bool IsCollection { get; }

    /// <summary>
    /// Gets the alignment requirement for this field in bytes.
    /// </summary>
    public int Alignment { get; }

    /// <summary>
    /// Gets the element type for collections, or null for non-collection fields.
    /// </summary>
    public FieldType? ElementType { get; }

    /// <summary>
    /// Gets the fixed length for arrays, or null for variable-length collections.
    /// </summary>
    /// <remarks>
    /// Fixed-length arrays (e.g., byte[16] for Guid storage) have a known length at compile time.
    /// Variable-length collections (e.g., List&lt;float&gt;) require runtime length encoding.
    /// </remarks>
    public int? FixedLength { get; }
}

/// <summary>
/// Categorizes field types for binary format analysis.
/// </summary>
/// <remarks>
/// CA1720: Enum values intentionally match C# type names for clarity in binary format mapping.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Enum values intentionally match C# type system for binary format analysis")]
public enum FieldType
{
    /// <summary>Unknown or unsupported field type.</summary>
    Unknown = 0,

    /// <summary>8-bit unsigned integer (byte).</summary>
    Byte = 1,

    /// <summary>8-bit signed integer (sbyte).</summary>
    SByte = 2,

    /// <summary>16-bit signed integer (short).</summary>
    Int16 = 3,

    /// <summary>16-bit unsigned integer (ushort).</summary>
    UInt16 = 4,

    /// <summary>32-bit signed integer (int).</summary>
    Int32 = 5,

    /// <summary>32-bit unsigned integer (uint).</summary>
    UInt32 = 6,

    /// <summary>64-bit signed integer (long).</summary>
    Int64 = 7,

    /// <summary>64-bit unsigned integer (ulong).</summary>
    UInt64 = 8,

    /// <summary>32-bit floating point (float).</summary>
    Float = 9,

    /// <summary>64-bit floating point (double).</summary>
    Double = 10,

    /// <summary>Boolean value (bool).</summary>
    Boolean = 11,

    /// <summary>16-byte globally unique identifier (Guid).</summary>
    Guid = 12,

    /// <summary>Variable-length UTF-8 string.</summary>
    String = 13,

    /// <summary>Array or collection of elements.</summary>
    Array = 14,

    /// <summary>Nested MemoryPack-serializable type.</summary>
    Nested = 15,
}
