// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Compilation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Tests for <see cref="MetalMemoryPackSerializerGenerator"/>.
/// </summary>
public class MetalMemoryPackSerializerGeneratorTests
{
    #region Single Type Generation Tests

    [Fact]
    public void GenerateSerializer_WithValidType_GeneratesCompleteSource()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.NotNull(source);
        Assert.NotEmpty(source);
        Assert.Contains("test_vector_add_request", source);
        Assert.Contains("inline", source); // MSL uses inline instead of __device__
    }

    [Fact]
    public void GenerateSerializer_IncludesMetalHeader()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("#include <metal_stdlib>", source);
        Assert.Contains("using namespace metal;", source);
    }

    [Fact]
    public void GenerateSerializer_IncludesMemoryPackHeader()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("MEMORYPACK_HEADER_SIZE", source);
        Assert.Contains("buffer[0]", source); // Header byte access
    }

    [Fact]
    public void GenerateSerializer_IncludesGuidTypeDefinition()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("struct guid_t", source);
        Assert.Contains("uchar bytes[16]", source); // MSL uses uchar instead of uint8_t
    }

    [Fact]
    public void GenerateSerializer_IncludesNullableGuidDefinition()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("nullable_guid_t", source);
        Assert.Contains("has_value", source);
    }

    [Fact]
    public void GenerateSerializer_GeneratesStructWithCorrectFields()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // Check struct fields are generated
        Assert.Contains("message_id[16]", source); // Guid as byte array
        Assert.Contains("priority", source);       // byte as uchar
        Assert.Contains("correlation_id", source); // Nullable<Guid>
        Assert.Contains("a", source);              // float
        Assert.Contains("b", source);              // float
    }

    [Fact]
    public void GenerateSerializer_GeneratesDeserializerFunction()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("inline bool deserialize_test_vector_add_request", source);
        Assert.Contains("device const uchar* buffer", source);
        Assert.Contains("int buffer_size", source);
        Assert.Contains("test_vector_add_request* out", source);
    }

    [Fact]
    public void GenerateSerializer_GeneratesSerializerFunction()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("inline int serialize_test_vector_add_request", source);
        Assert.Contains("device uchar* buffer", source);
        Assert.Contains("int buffer_size", source);
    }

    [Fact]
    public void GenerateSerializer_IncludesBufferSizeValidation()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("if (buffer_size <", source);
        Assert.Contains("return false", source);
    }

    [Fact]
    public void GenerateSerializer_IncludesFieldOffsetComments()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // The generator includes field offsets in comments like "// MessageId: offset 0, size 16"
        Assert.Contains("offset", source.ToLowerInvariant());
        Assert.Contains("size", source.ToLowerInvariant());
    }

    [Fact]
    public void GenerateSerializer_HandlesNullableGuidField()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // Verify nullable handling
        Assert.Contains("has_value", source);
        Assert.Contains("!= 0", source); // has_value check
    }

    [Fact]
    public void GenerateSerializer_IncludesLittleEndianHelpers()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("read_le", source);
        Assert.Contains("write_le", source);
        Assert.Contains("as_type<float>", source); // Metal's bitcast for floats
    }

    [Fact]
    public void GenerateSerializer_UsesMslTypes()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // MSL uses uchar instead of uint8_t, etc.
        Assert.Contains("uchar", source);
        Assert.Contains("float", source);
    }

    #endregion

    #region Batch Generation Tests

    [Fact]
    public void GenerateBatchSerializer_WithMultipleTypes_GeneratesSingleCompilationUnit()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var types = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var source = generator.GenerateBatchSerializer(types, "RingKernelMessages");

        // Assert
        Assert.NotNull(source);
        Assert.Contains("test_vector_add_request", source);
        Assert.Contains("test_vector_add_response", source);
        Assert.Contains("Unit Name: RingKernelMessages", source);
    }

    [Fact]
    public void GenerateBatchSerializer_IncludesCommonDefinitionsOnce()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var types = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var source = generator.GenerateBatchSerializer(types, "RingKernelMessages");

        // Assert
        // guid_t should appear once (as definition), not twice
        var guidDefCount = CountOccurrences(source, "struct guid_t {");
        Assert.Equal(1, guidDefCount);
    }

    [Fact]
    public void GenerateBatchSerializer_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateBatchSerializer(Array.Empty<Type>());

        // Assert
        Assert.Empty(source);
    }

    [Fact]
    public void GenerateBatchSerializer_GeneratesOrganizedSections()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var types = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var source = generator.GenerateBatchSerializer(types, "RingKernelMessages");

        // Assert
        Assert.Contains("STRUCT DEFINITIONS", source);
        Assert.Contains("DESERIALIZERS", source);
        Assert.Contains("SERIALIZERS", source);
        Assert.Contains("MESSAGE HANDLERS", source);
    }

    [Fact]
    public void GenerateBatchSerializer_WithSkipHandler_OmitsHandlerSection()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var types = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var source = generator.GenerateBatchSerializer(types, "RingKernelMessages", skipHandlerGeneration: true);

        // Assert
        Assert.Contains("skipped - manual handler provided", source);
        Assert.DoesNotContain("process_test_vector_add_message(", source);
    }

    #endregion

    #region Message Handler Tests

    [Fact]
    public void GenerateSerializer_IncludesMessageHandler()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        Assert.Contains("process_", source);
        Assert.Contains("_message", source);
        Assert.Contains("input_buffer", source);
        Assert.Contains("output_buffer", source);
    }

    [Fact]
    public void GenerateBatchSerializer_GeneratesHandlerForRequestResponsePair()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var types = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var source = generator.GenerateBatchSerializer(types, "RingKernelMessages");

        // Assert
        // Should generate only one handler for the Request/Response pair
        var handlerCount = CountOccurrences(source, "inline bool process_test_vector_add_message(");
        Assert.Equal(1, handlerCount);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void GenerateSerializer_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GenerateSerializer(null!));
    }

    [Fact]
    public void GenerateBatchSerializer_WithNullTypes_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GenerateBatchSerializer(null!));
    }

    [Fact]
    public void GenerateBatchSerializer_WithNullUnitName_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var types = new[] { typeof(TestVectorAddRequest) };

        // Act & Assert
        // ArgumentNullException is thrown by ArgumentException.ThrowIfNullOrWhiteSpace for null values
        Assert.Throws<ArgumentNullException>(() => generator.GenerateBatchSerializer(types, null!));
    }

    [Fact]
    public void GenerateSerializer_WithUnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            generator.GenerateSerializer(typeof(UnsupportedTypeMessage)));
        Assert.Contains("not supported", ex.Message.ToLowerInvariant());
    }

    #endregion

    #region MSL-Specific Tests

    [Fact]
    public void GenerateSerializer_UsesDeviceAddressSpace()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // MSL requires device address space qualifier for GPU memory
        Assert.Contains("device const uchar*", source);
        Assert.Contains("device uchar*", source);
    }

    [Fact]
    public void GenerateSerializer_UsesThreadAddressForOutput()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // Deserializer writes to thread-local struct
        Assert.Contains("thread test_vector_add_request*", source);
    }

    [Fact]
    public void GenerateSerializer_UsesConstantQualifier()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var source = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert
        // MSL uses 'constant' instead of #define for compile-time constants
        Assert.Contains("constant", source);
    }

    #endregion

    #region Helper Methods

    private static int CountOccurrences(string source, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Test message type for VectorAdd request.
    /// </summary>
    public class TestVectorAddRequest
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }
    }

    /// <summary>
    /// Test message type for VectorAdd response.
    /// </summary>
    public class TestVectorAddResponse
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float Result { get; set; }
    }

    /// <summary>
    /// Test message with unsupported type (string).
    /// </summary>
    public class UnsupportedTypeMessage
    {
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}
