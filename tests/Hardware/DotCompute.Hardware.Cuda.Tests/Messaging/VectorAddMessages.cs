// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using MemoryPack;

namespace DotCompute.Hardware.Cuda.Tests.Messaging;

[MemoryPackable]
public partial class VectorAddRequest : IRingKernelMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string MessageType => "VectorAddRequest";
    public byte Priority { get; set; } = 128;
    public Guid? CorrelationId { get; set; }

    public float A { get; set; }
    public float B { get; set; }

    // IRingKernelMessage serialization (custom binary format matching CUDA code)
    // Binary format: MessageId(16) + Priority(1) + CorrelationId(1+16) + A(4) + B(4) = 42 bytes
    // Note: This matches our auto-generated CUDA deserialization code
    public int PayloadSize => 42;

    public ReadOnlySpan<byte> Serialize()
    {
        var buffer = new byte[PayloadSize];
        int offset = 0;

        // MessageId: 16 bytes (Guid)
        MessageId.TryWriteBytes(buffer.AsSpan(offset, 16));
        offset += 16;

        // Priority: 1 byte
        buffer[offset] = Priority;
        offset += 1;

        // CorrelationId: 1 byte presence + 16 bytes value (nullable Guid)
        buffer[offset] = CorrelationId.HasValue ? (byte)1 : (byte)0;
        offset += 1;
        if (CorrelationId.HasValue)
        {
            CorrelationId.Value.TryWriteBytes(buffer.AsSpan(offset, 16));
        }
        offset += 16;

        // A: 4 bytes (float)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), A);
        offset += 4;

        // B: 4 bytes (float)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), B);

        return buffer;
    }

    public void Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < PayloadSize)
        {
            return;
        }

        int offset = 0;

        // MessageId: 16 bytes
        MessageId = new Guid(data.Slice(offset, 16));
        offset += 16;

        // Priority: 1 byte
        Priority = data[offset];
        offset += 1;

        // CorrelationId: 1 byte presence + 16 bytes value
        bool hasCorrelationId = data[offset] != 0;
        offset += 1;
        if (hasCorrelationId)
        {
            CorrelationId = new Guid(data.Slice(offset, 16));
        }
        else
        {
            CorrelationId = null;
        }
        offset += 16;

        // A: 4 bytes
        A = BitConverter.ToSingle(data.Slice(offset, 4));
        offset += 4;

        // B: 4 bytes
        B = BitConverter.ToSingle(data.Slice(offset, 4));
    }
}

[MemoryPackable]
public partial class VectorAddResponse : IRingKernelMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string MessageType => "VectorAddResponse";
    public byte Priority { get; set; } = 128;
    public Guid? CorrelationId { get; set; }

    public float Result { get; set; }

    // IRingKernelMessage serialization (custom binary format matching CUDA code)
    // Binary format: MessageId(16) + Priority(1) + CorrelationId(1+16) + Result(4) = 38 bytes
    // Note: This matches our auto-generated CUDA deserialization code
    public int PayloadSize => 38;

    public ReadOnlySpan<byte> Serialize()
    {
        var buffer = new byte[PayloadSize];
        int offset = 0;

        // MessageId: 16 bytes (Guid)
        MessageId.TryWriteBytes(buffer.AsSpan(offset, 16));
        offset += 16;

        // Priority: 1 byte
        buffer[offset] = Priority;
        offset += 1;

        // CorrelationId: 1 byte presence + 16 bytes value (nullable Guid)
        buffer[offset] = CorrelationId.HasValue ? (byte)1 : (byte)0;
        offset += 1;
        if (CorrelationId.HasValue)
        {
            CorrelationId.Value.TryWriteBytes(buffer.AsSpan(offset, 16));
        }
        offset += 16;

        // Result: 4 bytes (float)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), Result);

        return buffer;
    }

    public void Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < PayloadSize)
        {
            return;
        }

        int offset = 0;

        // MessageId: 16 bytes
        MessageId = new Guid(data.Slice(offset, 16));
        offset += 16;

        // Priority: 1 byte
        Priority = data[offset];
        offset += 1;

        // CorrelationId: 1 byte presence + 16 bytes value
        bool hasCorrelationId = data[offset] != 0;
        offset += 1;
        if (hasCorrelationId)
        {
            CorrelationId = new Guid(data.Slice(offset, 16));
        }
        else
        {
            CorrelationId = null;
        }
        offset += 16;

        // Result: 4 bytes
        Result = BitConverter.ToSingle(data.Slice(offset, 4));
    }
}
