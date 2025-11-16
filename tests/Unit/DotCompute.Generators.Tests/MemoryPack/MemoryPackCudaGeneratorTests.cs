// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Generators.MemoryPack;
using Xunit;

namespace DotCompute.Generators.Tests.MemoryPack;

/// <summary>
/// Unit tests for <see cref="MemoryPackCudaGenerator"/>.
/// Validates CUDA code generation from binary format specifications.
/// </summary>
public sealed class MemoryPackCudaGeneratorTests
{
    /// <summary>
    /// Test: Generate CUDA code for VectorAddRequest with proper struct and functions.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: VectorAddRequest generates correct struct")]
    public void GenerateCode_VectorAddRequest_ProducesCorrectStruct()
    {
        // Arrange: Create specification for VectorAddRequest
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act: Generate CUDA code
        var code = generator.GenerateCode(spec);

        // Assert: Verify struct definition
        Assert.Contains("struct vector_add_request", code, StringComparison.Ordinal);
        Assert.Contains("unsigned char message_id[16];", code, StringComparison.Ordinal);
        Assert.Contains("uint8_t priority;", code, StringComparison.Ordinal);
        Assert.Contains("struct { bool has_value; unsigned char value[16]; } correlation_id;", code, StringComparison.Ordinal);
        Assert.Contains("float a;", code, StringComparison.Ordinal);
        Assert.Contains("float b;", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Generate deserialize function with bounds checking.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Generates deserialize function with bounds check")]
    public void GenerateCode_VectorAddRequest_ProducesDeserializeFunction()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Verify deserialize function signature
        Assert.Contains("__device__ bool deserialize_vector_add_request(", code, StringComparison.Ordinal);
        Assert.Contains("const unsigned char* buffer,", code, StringComparison.Ordinal);
        Assert.Contains("int buffer_size,", code, StringComparison.Ordinal);
        Assert.Contains("vector_add_request* out)", code, StringComparison.Ordinal);

        // Verify bounds checking
        Assert.Contains("if (buffer_size < 42)", code, StringComparison.Ordinal);
        Assert.Contains("return false; // Buffer too small", code, StringComparison.Ordinal);

        // Verify field deserialization
        Assert.Contains("// MessageId:", code, StringComparison.Ordinal);
        Assert.Contains("out->message_id[i] = buffer[0 + i];", code, StringComparison.Ordinal);
        Assert.Contains("// Priority:", code, StringComparison.Ordinal);
        Assert.Contains("out->priority = buffer[16];", code, StringComparison.Ordinal);
        Assert.Contains("// CorrelationId:", code, StringComparison.Ordinal);
        Assert.Contains("out->correlation_id.has_value = buffer[17] != 0;", code, StringComparison.Ordinal);

        // Verify success return
        Assert.Contains("return true; // Deserialization successful", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Generate serialize function for GPU-to-CPU communication.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Generates serialize function")]
    public void GenerateCode_VectorAddRequest_ProducesSerializeFunction()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Verify serialize function signature
        Assert.Contains("__device__ void serialize_vector_add_request(", code, StringComparison.Ordinal);
        Assert.Contains("const vector_add_request* input,", code, StringComparison.Ordinal);
        Assert.Contains("unsigned char* buffer)", code, StringComparison.Ordinal);

        // Verify field serialization
        Assert.Contains("buffer[0 + i] = input->message_id[i];", code, StringComparison.Ordinal);
        Assert.Contains("buffer[16] = input->priority;", code, StringComparison.Ordinal);
        Assert.Contains("buffer[17] = input->correlation_id.has_value ? 1 : 0;", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Nullable Guid fields have presence byte + 16 bytes.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Nullable Guid has presence byte handling")]
    public void GenerateCode_NullableGuid_HasPresenceByteHandling()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Nullable Guid deserialization
        Assert.Contains("out->correlation_id.has_value = buffer[17] != 0;", code, StringComparison.Ordinal);
        Assert.Contains("if (out->correlation_id.has_value)", code, StringComparison.Ordinal);
        Assert.Contains("out->correlation_id.value[i] = buffer[18 + i];", code, StringComparison.Ordinal);

        // Assert: Nullable Guid serialization
        Assert.Contains("buffer[17] = input->correlation_id.has_value ? 1 : 0;", code, StringComparison.Ordinal);
        Assert.Contains("if (input->correlation_id.has_value)", code, StringComparison.Ordinal);
        Assert.Contains("buffer[18 + i] = input->correlation_id.value[i];", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Primitive types use reinterpret_cast for multi-byte values.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Float fields use reinterpret_cast")]
    public void GenerateCode_FloatFields_UseReinterpretCast()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Float deserialization
        Assert.Contains("out->a = *reinterpret_cast<const float*>(&buffer[34]);", code, StringComparison.Ordinal);
        Assert.Contains("out->b = *reinterpret_cast<const float*>(&buffer[38]);", code, StringComparison.Ordinal);

        // Assert: Float serialization
        Assert.Contains("*reinterpret_cast<float*>(&buffer[34]) = input->a;", code, StringComparison.Ordinal);
        Assert.Contains("*reinterpret_cast<float*>(&buffer[38]) = input->b;", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: All primitive types have correct CUDA mappings.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: All primitive types mapped correctly")]
    public void GenerateCode_AllPrimitives_MappedCorrectly()
    {
        // Arrange: Specification with all primitive types
        var fields = ImmutableArray.Create(
            new FieldSpecification("ByteField", FieldType.Byte, "System.Byte", "uint8_t", 0, 1, false, false, 1),
            new FieldSpecification("Int16Field", FieldType.Int16, "System.Int16", "int16_t", 1, 2, false, false, 2),
            new FieldSpecification("Int32Field", FieldType.Int32, "System.Int32", "int32_t", 3, 4, false, false, 4),
            new FieldSpecification("FloatField", FieldType.Float, "System.Single", "float", 7, 4, false, false, 4),
            new FieldSpecification("DoubleField", FieldType.Double, "System.Double", "double", 11, 8, false, false, 8),
            new FieldSpecification("BoolField", FieldType.Boolean, "System.Boolean", "bool", 19, 1, false, false, 1)
        );

        var spec = new BinaryFormatSpecification(
            typeName: "Test.AllPrimitivesMessage",
            fields: fields,
            totalSize: 20,
            isFixedSize: true,
            alignment: 8);

        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Struct has all fields
        Assert.Contains("uint8_t byte_field;", code, StringComparison.Ordinal);
        Assert.Contains("int16_t int16_field;", code, StringComparison.Ordinal);
        Assert.Contains("int32_t int32_field;", code, StringComparison.Ordinal);
        Assert.Contains("float float_field;", code, StringComparison.Ordinal);
        Assert.Contains("double double_field;", code, StringComparison.Ordinal);
        Assert.Contains("bool bool_field;", code, StringComparison.Ordinal);

        // Assert: Deserialization uses correct types
        Assert.Contains("out->byte_field = buffer[0];", code, StringComparison.Ordinal);
        Assert.Contains("*reinterpret_cast<const int16_t*>(&buffer[1])", code, StringComparison.Ordinal);
        Assert.Contains("*reinterpret_cast<const int32_t*>(&buffer[3])", code, StringComparison.Ordinal);
        Assert.Contains("*reinterpret_cast<const float*>(&buffer[7])", code, StringComparison.Ordinal);
        Assert.Contains("*reinterpret_cast<const double*>(&buffer[11])", code, StringComparison.Ordinal);
        Assert.Contains("out->bool_field = buffer[19];", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Nullable primitive types have presence byte.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Nullable primitives have presence byte")]
    public void GenerateCode_NullablePrimitive_HasPresenceByte()
    {
        // Arrange: Nullable int field
        var fields = ImmutableArray.Create(
            new FieldSpecification(
                "NullableInt",
                FieldType.Int32,
                "System.Int32",
                "struct { bool has_value; int32_t value; }",
                offset: 0,
                size: 5, // 1 (presence) + 4 (int32)
                isNullable: true,
                isCollection: false,
                alignment: 4)
        );

        var spec = new BinaryFormatSpecification(
            typeName: "Test.NullableMessage",
            fields: fields,
            totalSize: 5,
            isFixedSize: true,
            alignment: 4);

        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Struct definition
        Assert.Contains("struct { bool has_value; int32_t value; } nullable_int;", code, StringComparison.Ordinal);

        // Assert: Deserialization
        Assert.Contains("out->nullable_int.has_value = buffer[0] != 0;", code, StringComparison.Ordinal);
        Assert.Contains("if (out->nullable_int.has_value)", code, StringComparison.Ordinal);
        Assert.Contains("out->nullable_int.value = *reinterpret_cast<const int32_t*>(&buffer[1]);", code, StringComparison.Ordinal);

        // Assert: Serialization
        Assert.Contains("buffer[0] = input->nullable_int.has_value ? 1 : 0;", code, StringComparison.Ordinal);
        Assert.Contains("if (input->nullable_int.has_value)", code, StringComparison.Ordinal);
        Assert.Contains("*reinterpret_cast<int32_t*>(&buffer[1]) = input->nullable_int.value;", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: PascalCase names convert to snake_case.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: PascalCase converts to snake_case")]
    public void GenerateCode_NamingConvention_ConvertsToSnakeCase()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Type name conversion
        Assert.Contains("struct vector_add_request", code, StringComparison.Ordinal);
        Assert.Contains("__device__ bool deserialize_vector_add_request(", code, StringComparison.Ordinal);
        Assert.Contains("__device__ void serialize_vector_add_request(", code, StringComparison.Ordinal);

        // Assert: Field name conversion
        Assert.Contains("message_id", code, StringComparison.Ordinal);
        Assert.Contains("correlation_id", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Generated code has header comment with metadata.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Includes header comment with metadata")]
    public void GenerateCode_HasHeaderComment()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Header comment
        Assert.Contains("// Auto-generated MemoryPack CUDA serialization code", code, StringComparison.Ordinal);
        Assert.Contains("// Type: Test.VectorAddRequest", code, StringComparison.Ordinal);
        Assert.Contains("// Total Size: 42 bytes", code, StringComparison.Ordinal);
        Assert.Contains("// Fixed Size: Yes", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Guid fields use #pragma unroll for loop optimization.
    /// </summary>
    [Fact(DisplayName = "CudaGenerator: Guid fields have pragma unroll")]
    public void GenerateCode_GuidFields_HavePragmaUnroll()
    {
        // Arrange
        var spec = CreateVectorAddRequestSpec();
        var generator = new MemoryPackCudaGenerator();

        // Act
        var code = generator.GenerateCode(spec);

        // Assert: Pragma unroll for Guid loops
        var pragmaCount = code.Split(new[] { "#pragma unroll" }, StringSplitOptions.None).Length - 1;
        Assert.True(pragmaCount >= 4, $"Expected at least 4 #pragma unroll directives for Guids, found {pragmaCount}"); // 2 Guids × (deserialize + serialize)
    }

    /// <summary>
    /// Helper: Create VectorAddRequest specification.
    /// </summary>
    private static BinaryFormatSpecification CreateVectorAddRequestSpec()
    {
        var fields = ImmutableArray.Create(
            new FieldSpecification(
                "MessageId",
                FieldType.Guid,
                "System.Guid",
                "unsigned char[16]",
                offset: 0,
                size: 16,
                isNullable: false,
                isCollection: false,
                alignment: 16),
            new FieldSpecification(
                "Priority",
                FieldType.Byte,
                "System.Byte",
                "uint8_t",
                offset: 16,
                size: 1,
                isNullable: false,
                isCollection: false,
                alignment: 1),
            new FieldSpecification(
                "CorrelationId",
                FieldType.Guid,
                "System.Guid",
                "struct { bool has_value; unsigned char value[16]; }",
                offset: 17,
                size: 17, // 1 (presence) + 16 (Guid)
                isNullable: true,
                isCollection: false,
                alignment: 16),
            new FieldSpecification(
                "A",
                FieldType.Float,
                "System.Single",
                "float",
                offset: 34,
                size: 4,
                isNullable: false,
                isCollection: false,
                alignment: 4),
            new FieldSpecification(
                "B",
                FieldType.Float,
                "System.Single",
                "float",
                offset: 38,
                size: 4,
                isNullable: false,
                isCollection: false,
                alignment: 4)
        );

        return new BinaryFormatSpecification(
            typeName: "Test.VectorAddRequest",
            fields: fields,
            totalSize: 42,
            isFixedSize: true,
            alignment: 16);
    }
}
