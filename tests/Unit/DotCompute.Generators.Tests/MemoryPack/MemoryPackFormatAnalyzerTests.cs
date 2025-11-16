// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.MemoryPack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Generators.Tests.MemoryPack;

/// <summary>
/// Unit tests for <see cref="MemoryPackFormatAnalyzer"/>.
/// Validates binary format extraction from [MemoryPackable] types.
/// </summary>
public sealed class MemoryPackFormatAnalyzerTests
{
    /// <summary>
    /// Test: Analyze VectorAddRequest and verify binary format matches expected 41-byte layout.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: VectorAddRequest produces correct 41-byte format")]
    public void AnalyzeType_VectorAddRequest_ProducesCorrectBinaryFormat()
    {
        // Arrange: Create in-memory C# code for VectorAddRequest
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class VectorAddRequest
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.VectorAddRequest");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act: Analyze the type
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Verify binary format
        Assert.Equal("Test.VectorAddRequest", spec.TypeName);
        Assert.Equal(42, spec.TotalSize); // 16 + 1 + 17 + 4 + 4 = 42 (CorrelationId nullable: 1 presence + 16 value)
        Assert.True(spec.IsFixedSize);
        Assert.True(spec.HasGuidFields);
        Assert.True(spec.HasNullableFields);
        Assert.True(spec.IsValidRingKernelMessage);

        // Verify field count
        Assert.Equal(5, spec.Fields.Length);

        // Verify MessageId field (Guid, 16 bytes, offset 0)
        var messageIdField = spec.Fields[0];
        Assert.Equal("MessageId", messageIdField.Name);
        Assert.Equal(FieldType.Guid, messageIdField.FieldType);
        Assert.Equal(0, messageIdField.Offset);
        Assert.Equal(16, messageIdField.Size);
        Assert.False(messageIdField.IsNullable);
        Assert.Equal("unsigned char[16]", messageIdField.CudaTypeName);

        // Verify Priority field (byte, 1 byte, offset 16)
        var priorityField = spec.Fields[1];
        Assert.Equal("Priority", priorityField.Name);
        Assert.Equal(FieldType.Byte, priorityField.FieldType);
        Assert.Equal(16, priorityField.Offset);
        Assert.Equal(1, priorityField.Size);
        Assert.False(priorityField.IsNullable);
        Assert.Equal("uint8_t", priorityField.CudaTypeName);

        // Verify CorrelationId field (Guid?, 17 bytes with presence byte, offset 17)
        var correlationIdField = spec.Fields[2];
        Assert.Equal("CorrelationId", correlationIdField.Name);
        Assert.Equal(FieldType.Guid, correlationIdField.FieldType);
        Assert.Equal(17, correlationIdField.Offset);
        Assert.Equal(17, correlationIdField.Size); // 1 (presence) + 16 (Guid)
        Assert.True(correlationIdField.IsNullable);
        Assert.Contains("bool has_value", correlationIdField.CudaTypeName);
        Assert.Contains("unsigned char[16] value", correlationIdField.CudaTypeName);

        // Verify A field (float, 4 bytes, offset 34)
        var aField = spec.Fields[3];
        Assert.Equal("A", aField.Name);
        Assert.Equal(FieldType.Float, aField.FieldType);
        Assert.Equal(34, aField.Offset);
        Assert.Equal(4, aField.Size);
        Assert.False(aField.IsNullable);
        Assert.Equal("float", aField.CudaTypeName);

        // Verify B field (float, 4 bytes, offset 38)
        var bField = spec.Fields[4];
        Assert.Equal("B", bField.Name);
        Assert.Equal(FieldType.Float, bField.FieldType);
        Assert.Equal(38, bField.Offset);
        Assert.Equal(4, bField.Size);
        Assert.False(bField.IsNullable);
        Assert.Equal("float", bField.CudaTypeName);
    }

    /// <summary>
    /// Test: Analyze VectorAddResponse and verify binary format matches expected 37-byte layout.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: VectorAddResponse produces correct 37-byte format")]
    public void AnalyzeType_VectorAddResponse_ProducesCorrectBinaryFormat()
    {
        // Arrange
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class VectorAddResponse
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float Result { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.VectorAddResponse");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert
        Assert.Equal("Test.VectorAddResponse", spec.TypeName);
        Assert.Equal(38, spec.TotalSize); // 16 + 1 + 17 + 4 = 38 (CorrelationId nullable: 1 presence + 16 value)
        Assert.True(spec.IsFixedSize);
        Assert.Equal(4, spec.Fields.Length);

        // Verify Result field (float, 4 bytes, offset 33)
        var resultField = spec.Fields[3];
        Assert.Equal("Result", resultField.Name);
        Assert.Equal(FieldType.Float, resultField.FieldType);
        Assert.Equal(34, resultField.Offset); // 16 (MessageId) + 1 (Priority) + 17 (CorrelationId?)
        Assert.Equal(4, resultField.Size);
    }

    /// <summary>
    /// Test: Analyze type with all primitive types and verify correct CUDA type mappings.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: All primitive types mapped correctly to CUDA")]
    public void AnalyzeType_AllPrimitiveTypes_MapsCorrectlyToCuda()
    {
        // Arrange
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class AllPrimitivesMessage
    {
        public byte ByteField { get; set; }
        public sbyte SByteField { get; set; }
        public short Int16Field { get; set; }
        public ushort UInt16Field { get; set; }
        public int Int32Field { get; set; }
        public uint UInt32Field { get; set; }
        public long Int64Field { get; set; }
        public ulong UInt64Field { get; set; }
        public float FloatField { get; set; }
        public double DoubleField { get; set; }
        public bool BoolField { get; set; }
        public Guid GuidField { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.AllPrimitivesMessage");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Verify CUDA type mappings
        Assert.Equal("uint8_t", spec.Fields.First(f => f.Name == "ByteField").CudaTypeName);
        Assert.Equal("int8_t", spec.Fields.First(f => f.Name == "SByteField").CudaTypeName);
        Assert.Equal("int16_t", spec.Fields.First(f => f.Name == "Int16Field").CudaTypeName);
        Assert.Equal("uint16_t", spec.Fields.First(f => f.Name == "UInt16Field").CudaTypeName);
        Assert.Equal("int32_t", spec.Fields.First(f => f.Name == "Int32Field").CudaTypeName);
        Assert.Equal("uint32_t", spec.Fields.First(f => f.Name == "UInt32Field").CudaTypeName);
        Assert.Equal("int64_t", spec.Fields.First(f => f.Name == "Int64Field").CudaTypeName);
        Assert.Equal("uint64_t", spec.Fields.First(f => f.Name == "UInt64Field").CudaTypeName);
        Assert.Equal("float", spec.Fields.First(f => f.Name == "FloatField").CudaTypeName);
        Assert.Equal("double", spec.Fields.First(f => f.Name == "DoubleField").CudaTypeName);
        Assert.Equal("bool", spec.Fields.First(f => f.Name == "BoolField").CudaTypeName);
        Assert.Equal("unsigned char[16]", spec.Fields.First(f => f.Name == "GuidField").CudaTypeName);

        // Verify sizes
        Assert.Equal(1, spec.Fields.First(f => f.Name == "ByteField").Size);
        Assert.Equal(2, spec.Fields.First(f => f.Name == "Int16Field").Size);
        Assert.Equal(4, spec.Fields.First(f => f.Name == "Int32Field").Size);
        Assert.Equal(8, spec.Fields.First(f => f.Name == "Int64Field").Size);
        Assert.Equal(16, spec.Fields.First(f => f.Name == "GuidField").Size);
    }

    /// <summary>
    /// Test: Nullable value types have presence byte and correct size calculation.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: Nullable types have presence byte")]
    public void AnalyzeType_NullableValueTypes_IncludesPresenceByte()
    {
        // Arrange
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class NullableMessage
    {
        public int? NullableInt { get; set; }
        public float? NullableFloat { get; set; }
        public Guid? NullableGuid { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.NullableMessage");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert
        var nullableIntField = spec.Fields[0];
        Assert.True(nullableIntField.IsNullable);
        Assert.Equal(5, nullableIntField.Size); // 1 (presence) + 4 (int)
        Assert.Contains("bool has_value", nullableIntField.CudaTypeName);
        Assert.Contains("int32_t value", nullableIntField.CudaTypeName);

        var nullableFloatField = spec.Fields[1];
        Assert.True(nullableFloatField.IsNullable);
        Assert.Equal(5, nullableFloatField.Size); // 1 (presence) + 4 (float)

        var nullableGuidField = spec.Fields[2];
        Assert.True(nullableGuidField.IsNullable);
        Assert.Equal(17, nullableGuidField.Size); // 1 (presence) + 16 (Guid)
    }

    /// <summary>
    /// Test: Type without [MemoryPackable] attribute throws InvalidOperationException.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: Type without [MemoryPackable] throws exception")]
    public void AnalyzeType_WithoutMemoryPackableAttribute_ThrowsException()
    {
        // Arrange
        const string code = @"
using System;

namespace Test
{
    public class NotMemoryPackable
    {
        public int Value { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.NotMemoryPackable");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => analyzer.AnalyzeType(typeSymbol));
        Assert.Contains("does not have [MemoryPackable] attribute", exception.Message);
    }

    /// <summary>
    /// Test: Declaration order is preserved (MemoryPack serializes in declaration order).
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: Properties analyzed in declaration order")]
    public void AnalyzeType_PreservesDeclarationOrder()
    {
        // Arrange
        const string code = @"
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class OrderedMessage
    {
        public int First { get; set; }
        public float Second { get; set; }
        public byte Third { get; set; }
        public long Fourth { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.OrderedMessage");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Verify field order and offsets
        Assert.Equal("First", spec.Fields[0].Name);
        Assert.Equal(0, spec.Fields[0].Offset);

        Assert.Equal("Second", spec.Fields[1].Name);
        Assert.Equal(4, spec.Fields[1].Offset); // After 4-byte int

        Assert.Equal("Third", spec.Fields[2].Name);
        Assert.Equal(8, spec.Fields[2].Offset); // After 4-byte float

        Assert.Equal("Fourth", spec.Fields[3].Name);
        Assert.Equal(9, spec.Fields[3].Offset); // After 1-byte byte
    }

    /// <summary>
    /// Test: Alignment requirements are calculated correctly.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: Alignment requirements calculated correctly")]
    public void AnalyzeType_AlignmentRequirements_CalculatedCorrectly()
    {
        // Arrange
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class AlignmentMessage
    {
        public byte ByteField { get; set; }
        public double DoubleField { get; set; }
        public Guid GuidField { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.AlignmentMessage");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Max alignment is from Guid (16 bytes)
        Assert.Equal(16, spec.Alignment);

        // Individual field alignments
        Assert.Equal(1, spec.Fields.First(f => f.Name == "ByteField").Alignment);
        Assert.Equal(8, spec.Fields.First(f => f.Name == "DoubleField").Alignment);
        Assert.Equal(16, spec.Fields.First(f => f.Name == "GuidField").Alignment);
    }

    /// <summary>
    /// Test: Static properties and indexers are ignored.
    /// </summary>
    [Fact(DisplayName = "MemoryPackAnalyzer: Static and indexer properties ignored")]
    public void AnalyzeType_StaticAndIndexerProperties_AreIgnored()
    {
        // Arrange
        const string code = @"
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class PropertiesMessage
    {
        public static int StaticProperty { get; set; }
        public int InstanceProperty { get; set; }
        public int this[int index] => index;
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.PropertiesMessage");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);

        // Act
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Only InstanceProperty should be analyzed
        Assert.Single(spec.Fields);
        Assert.Equal("InstanceProperty", spec.Fields[0].Name);
    }

    /// <summary>
    /// Helper method: Compile C# code and get type symbol for analysis.
    /// </summary>
    private static (CSharpCompilation compilation, SemanticModel semanticModel, INamedTypeSymbol typeSymbol) CompileAndGetType(
        string code,
        string typeName)
    {
        // Add mock MemoryPackable attribute definition
        const string memoryPackableAttributeCode = @"
namespace MemoryPack
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class MemoryPackableAttribute : System.Attribute
    {
    }
}";

        // Create compilation with necessary references
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(code),
            CSharpSyntaxTree.ParseText(memoryPackableAttributeCode)
        };

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Get semantic model and type symbol (use first syntax tree which has the user code)
        var semanticModel = compilation.GetSemanticModel(syntaxTrees[0]);
        var root = syntaxTrees[0].GetRoot();
        var typeDeclaration = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .First();

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol
            ?? throw new InvalidOperationException($"Could not get type symbol for {typeName}");

        return (compilation, semanticModel, typeSymbol);
    }
}
