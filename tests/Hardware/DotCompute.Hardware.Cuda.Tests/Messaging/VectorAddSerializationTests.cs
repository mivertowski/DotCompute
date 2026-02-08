// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.Messaging;

/// <summary>
/// Tests for MemoryPack serialization of VectorAdd messages.
/// Validates binary format matches expected MemoryPack layout.
/// </summary>
public class VectorAddSerializationTests
{
    [Fact(DisplayName = "VectorAddRequest: Serialization produces correct binary size")]
    public void VectorAddRequest_Serialize_ProducesCorrectSize()
    {
        // Arrange
        var request = new VectorAddRequest
        {
            MessageId = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            Priority = 128,
            CorrelationId = Guid.Parse("87654321-4321-4321-4321-210987654321"),
            A = 10.5f,
            B = 20.3f
        };

        // Act
        var bytes = request.Serialize();

        // Assert: MemoryPack format is 42 bytes
        // MessageId(16) + Priority(1) + CorrelationId(1+16) + A(4) + B(4) = 42
        bytes.Length.Should().Be(42, "MemoryPack serializes with nullable Guid presence byte");
    }

    [Fact(DisplayName = "VectorAddRequest: PayloadSize matches serialized size")]
    public void VectorAddRequest_PayloadSize_MatchesSerializedSize()
    {
        // Arrange
        var request = new VectorAddRequest
        {
            A = 1.0f,
            B = 2.0f
        };

        // Act
        var bytes = request.Serialize();

        // Assert
        request.PayloadSize.Should().Be(bytes.Length, "PayloadSize property must match actual serialized size");
    }

    [Fact(DisplayName = "VectorAddRequest: Nullable CorrelationId presence byte is correct")]
    public void VectorAddRequest_NullableCorrelationId_PresenceByteCorrect()
    {
        // Arrange
        var request = new VectorAddRequest
        {
            MessageId = Guid.NewGuid(),
            Priority = 100,
            CorrelationId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            A = 5.0f,
            B = 7.0f
        };

        // Act
        var bytes = request.Serialize();

        // Assert: Byte at offset 17 should be 1 (has value)
        bytes.ToArray()[17].Should().Be(1, "Nullable Guid with value should have presence byte = 1");
    }

    [Fact(DisplayName = "VectorAddRequest: Null CorrelationId presence byte is correct")]
    public void VectorAddRequest_NullCorrelationId_PresenceByteCorrect()
    {
        // Arrange
        var request = new VectorAddRequest
        {
            MessageId = Guid.NewGuid(),
            Priority = 100,
            CorrelationId = null, // Explicitly null
            A = 5.0f,
            B = 7.0f
        };

        // Act
        var bytes = request.Serialize();

        // Assert: Byte at offset 17 should be 0 (no value)
        bytes.ToArray()[17].Should().Be(0, "Nullable Guid without value should have presence byte = 0");
    }

    [Fact(DisplayName = "VectorAddRequest: Round-trip serialization preserves data")]
    public void VectorAddRequest_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new VectorAddRequest
        {
            MessageId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Priority = 200,
            CorrelationId = Guid.Parse("ffffffff-eeee-dddd-cccc-bbbbbbbbbbbb"),
            A = 123.456f,
            B = 789.012f
        };

        // Act
        var bytes = original.Serialize();
        var deserialized = new VectorAddRequest();
        deserialized.Deserialize(bytes);

        // Assert
        deserialized.MessageId.Should().Be(original.MessageId);
        deserialized.Priority.Should().Be(original.Priority);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.A.Should().BeApproximately(original.A, 0.0001f);
        deserialized.B.Should().BeApproximately(original.B, 0.0001f);
    }

    [Fact(DisplayName = "VectorAddResponse: Serialization produces correct binary size")]
    public void VectorAddResponse_Serialize_ProducesCorrectSize()
    {
        // Arrange
        var response = new VectorAddResponse
        {
            MessageId = Guid.NewGuid(),
            Priority = 128,
            CorrelationId = Guid.NewGuid(),
            Result = 30.8f
        };

        // Act
        var bytes = response.Serialize();

        // Assert: MemoryPack format is 38 bytes
        // MessageId(16) + Priority(1) + CorrelationId(1+16) + Result(4) = 38
        bytes.Length.Should().Be(38, "MemoryPack serializes with nullable Guid presence byte");
    }

    [Fact(DisplayName = "VectorAddResponse: PayloadSize matches serialized size")]
    public void VectorAddResponse_PayloadSize_MatchesSerializedSize()
    {
        // Arrange
        var response = new VectorAddResponse
        {
            Result = 42.0f
        };

        // Act
        var bytes = response.Serialize();

        // Assert
        response.PayloadSize.Should().Be(bytes.Length, "PayloadSize property must match actual serialized size");
    }

    [Fact(DisplayName = "VectorAddResponse: Round-trip serialization preserves data")]
    public void VectorAddResponse_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new VectorAddResponse
        {
            MessageId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Priority = 150,
            CorrelationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Result = 999.999f
        };

        // Act
        var bytes = original.Serialize();
        var deserialized = new VectorAddResponse();
        deserialized.Deserialize(bytes);

        // Assert
        deserialized.MessageId.Should().Be(original.MessageId);
        deserialized.Priority.Should().Be(original.Priority);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.Result.Should().BeApproximately(original.Result, 0.0001f);
    }

    [Fact(DisplayName = "VectorAddRequest: Binary format field offsets are correct")]
    public void VectorAddRequest_BinaryFormat_FieldOffsetsCorrect()
    {
        // Arrange
        var messageId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var correlationId = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");

        var request = new VectorAddRequest
        {
            MessageId = messageId,
            Priority = 42,
            CorrelationId = correlationId,
            A = 1.5f,
            B = 2.5f
        };

        // Act
        var bytes = request.Serialize().ToArray();

        // Assert field offsets
        // MessageId at offset 0 (16 bytes)
        var deserializedMessageId = new Guid(bytes.AsSpan(0, 16));
        deserializedMessageId.Should().Be(messageId);

        // Priority at offset 16 (1 byte)
        bytes[16].Should().Be(42);

        // CorrelationId presence byte at offset 17
        bytes[17].Should().Be(1, "CorrelationId has value");

        // CorrelationId value at offset 18 (16 bytes)
        var deserializedCorrelationId = new Guid(bytes.AsSpan(18, 16));
        deserializedCorrelationId.Should().Be(correlationId);

        // A at offset 34 (4 bytes)
        var aValue = BitConverter.ToSingle(bytes.AsSpan(34, 4));
        aValue.Should().BeApproximately(1.5f, 0.0001f);

        // B at offset 38 (4 bytes)
        var bValue = BitConverter.ToSingle(bytes.AsSpan(38, 4));
        bValue.Should().BeApproximately(2.5f, 0.0001f);
    }
}
