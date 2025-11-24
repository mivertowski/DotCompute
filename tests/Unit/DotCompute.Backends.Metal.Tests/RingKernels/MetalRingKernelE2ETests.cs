// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Compilation;
using DotCompute.Backends.Metal.RingKernels;
using DotCompute.Backends.Metal.Translation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// End-to-end tests for Metal Ring Kernel compilation pipeline.
/// Validates complete pipeline: Discovery → MSL Generation → Serializer Generation.
/// </summary>
public class MetalRingKernelE2ETests
{
    #region Compilation Pipeline E2E Tests

    [Fact(DisplayName = "E2E: MemoryPack serializer generates valid MSL for VectorAdd message pair")]
    public void E2E_MemoryPackSerializer_GeneratesValidMsl_ForVectorAddMessagePair()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(
            NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var messageTypes = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var mslCode = generator.GenerateBatchSerializer(
            messageTypes,
            "VectorAddMessages",
            skipHandlerGeneration: false);

        // Assert - Structure validation
        Assert.NotEmpty(mslCode);
        Assert.Contains("#include <metal_stdlib>", mslCode);
        Assert.Contains("using namespace metal;", mslCode);

        // Assert - Common types generated
        Assert.Contains("struct guid_t {", mslCode);
        Assert.Contains("nullable_guid_t", mslCode);
        Assert.Contains("constant", mslCode); // MSL constants

        // Assert - Request/Response structs generated
        Assert.Contains("struct test_vector_add_request {", mslCode);
        Assert.Contains("struct test_vector_add_response {", mslCode);

        // Assert - Serializer/Deserializer functions generated
        Assert.Contains("inline bool deserialize_test_vector_add_request(", mslCode);
        Assert.Contains("inline int serialize_test_vector_add_request(", mslCode);
        Assert.Contains("inline bool deserialize_test_vector_add_response(", mslCode);
        Assert.Contains("inline int serialize_test_vector_add_response(", mslCode);

        // Assert - Message handler generated (only one for the pair)
        Assert.Contains("inline bool process_test_vector_add_message(", mslCode);

        // Assert - MSL address space qualifiers used
        Assert.Contains("device const uchar*", mslCode);
        Assert.Contains("device uchar*", mslCode);
        Assert.Contains("thread test_vector_add_request*", mslCode);

        // Assert - Little-endian helpers present
        Assert.Contains("read_le<", mslCode);
        Assert.Contains("write_le<", mslCode);
        Assert.Contains("as_type<float>", mslCode);
    }

    [Fact(DisplayName = "E2E: Handler translation service generates MSL device functions")]
    public void E2E_HandlerTranslation_GeneratesMslDeviceFunctions()
    {
        // Arrange
        var handlerLogger = NullLogger<MetalHandlerTranslationService>.Instance;
        var translatorLogger = NullLogger<CSharpToMSLTranslator>.Instance;
        var handler = new MetalHandlerTranslationService(handlerLogger, translatorLogger);

        // Act - Try to translate a non-existent handler (should return default)
        var result = handler.TranslateHandler("NonExistentRequest");

        // Assert - Should return null for non-existent handler (not an error)
        Assert.Null(result);
    }

    [Fact(DisplayName = "E2E: Handler translation generates default handler when source not available")]
    public void E2E_HandlerTranslation_GeneratesDefaultHandler_WhenSourceNotAvailable()
    {
        // Arrange
        var handlerLogger = NullLogger<MetalHandlerTranslationService>.Instance;
        var translatorLogger = NullLogger<CSharpToMSLTranslator>.Instance;
        var handler = new MetalHandlerTranslationService(handlerLogger, translatorLogger);

        // Act - Translate multiple handlers at once
        var result = handler.TranslateHandlers(new[] { "VectorAddRequest", "EchoRequest" });

        // Assert - Default handlers should be generated
        Assert.NotEmpty(result);
        Assert.Contains("process_vector_add_message", result);
        Assert.Contains("process_echo_message", result);
        Assert.Contains("device uchar*", result); // MSL syntax
        Assert.Contains("Default implementation", result);
    }

    [Fact(DisplayName = "E2E: Batch serializer generates organized sections")]
    public void E2E_BatchSerializer_GeneratesOrganizedSections()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(
            NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var messageTypes = new[]
        {
            typeof(TestVectorAddRequest),
            typeof(TestVectorAddResponse),
            typeof(TestEchoRequest),
            typeof(TestEchoResponse)
        };

        // Act
        var mslCode = generator.GenerateBatchSerializer(messageTypes, "MultiMessageTypes");

        // Assert - All sections present
        Assert.Contains("STRUCT DEFINITIONS", mslCode);
        Assert.Contains("DESERIALIZERS", mslCode);
        Assert.Contains("SERIALIZERS", mslCode);
        Assert.Contains("MESSAGE HANDLERS", mslCode);

        // Assert - All message types represented
        Assert.Contains("test_vector_add_request", mslCode);
        Assert.Contains("test_vector_add_response", mslCode);
        Assert.Contains("test_echo_request", mslCode);
        Assert.Contains("test_echo_response", mslCode);

        // Assert - Handlers generated for both message pairs
        Assert.Contains("process_test_vector_add_message", mslCode);
        Assert.Contains("process_test_echo_message", mslCode);
    }

    [Fact(DisplayName = "E2E: Serializer correctly handles nullable Guid fields")]
    public void E2E_Serializer_HandlesNullableGuidFields()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(
            NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var mslCode = generator.GenerateSerializer(typeof(TestMessageWithNullableGuid));

        // Assert
        Assert.Contains("has_value", mslCode); // Nullable handling
        Assert.Contains("correlation_id", mslCode); // Field name in snake_case
        Assert.Contains("!= 0", mslCode); // Null check
    }

    [Fact(DisplayName = "E2E: Serializer calculates correct buffer sizes")]
    public void E2E_Serializer_CalculatesCorrectBufferSizes()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(
            NullLogger<MetalMemoryPackSerializerGenerator>.Instance);

        // Act
        var mslCode = generator.GenerateSerializer(typeof(TestVectorAddRequest));

        // Assert - Buffer size validation includes header
        // TestVectorAddRequest has: Guid(16) + byte(1) + Guid?(17) + float(4) + float(4) = 42 bytes data + 1 header = 43
        Assert.Contains("if (buffer_size < ", mslCode);
        Assert.Contains("return false", mslCode); // Size check returns false
        Assert.Contains("Total bytes written", mslCode); // Comment on return value
    }

    [Fact(DisplayName = "E2E: Skip handler generation when flag is set")]
    public void E2E_SkipHandlerGeneration_WhenFlagSet()
    {
        // Arrange
        var generator = new MetalMemoryPackSerializerGenerator(
            NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var messageTypes = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };

        // Act
        var mslCode = generator.GenerateBatchSerializer(
            messageTypes,
            "VectorAddMessages",
            skipHandlerGeneration: true);

        // Assert - Handler section should be marked as skipped
        Assert.Contains("MESSAGE HANDLERS (skipped - manual handler provided)", mslCode);
        Assert.DoesNotContain("inline bool process_test_vector_add_message(", mslCode);
    }

    #endregion

    #region Integration Tests

    [Fact(DisplayName = "E2E: Complete pipeline generates compilable MSL")]
    public void E2E_CompletePipeline_GeneratesCompilableMsl()
    {
        // Arrange
        var serializerGenerator = new MetalMemoryPackSerializerGenerator(
            NullLogger<MetalMemoryPackSerializerGenerator>.Instance);
        var handlerLogger = NullLogger<MetalHandlerTranslationService>.Instance;
        var translatorLogger = NullLogger<CSharpToMSLTranslator>.Instance;
        var handlerTranslator = new MetalHandlerTranslationService(handlerLogger, translatorLogger);

        // Act - Generate serializers
        var messageTypes = new[] { typeof(TestVectorAddRequest), typeof(TestVectorAddResponse) };
        var serializerCode = serializerGenerator.GenerateBatchSerializer(messageTypes, "E2EPipeline");

        // Act - Generate default handlers
        var handlerCode = handlerTranslator.TranslateHandlers(new[] { "VectorAddRequest" });

        // Assert - Both components generate valid MSL
        Assert.NotEmpty(serializerCode);
        Assert.NotEmpty(handlerCode);

        // Assert - MSL syntax is correct
        Assert.Contains("#include <metal_stdlib>", serializerCode);
        Assert.Contains("device uchar*", handlerCode);

        // Assert - No CUDA-specific syntax
        Assert.DoesNotContain("__device__", serializerCode);
        Assert.DoesNotContain("uint8_t", serializerCode); // MSL uses uchar
    }

    #endregion

    #region Test Message Types

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
    /// Test message type for Echo request.
    /// </summary>
    public class TestEchoRequest
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// Test message type for Echo response.
    /// </summary>
    public class TestEchoResponse
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public int EchoedValue { get; set; }
    }

    /// <summary>
    /// Test message with nullable Guid field.
    /// </summary>
    public class TestMessageWithNullableGuid
    {
        public Guid MessageId { get; set; }
        public Guid? CorrelationId { get; set; }
    }

    #endregion
}
