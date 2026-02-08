// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using MemoryPack;

namespace DotCompute.Hardware.OpenCL.Tests.Messaging;

[MemoryPackable]
public partial class VectorAddRequest : IRingKernelMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string MessageType => "VectorAddRequest";
    public byte Priority { get; set; } = 128;
    public Guid? CorrelationId { get; set; }

    public float A { get; set; }
    public float B { get; set; }

    public int PayloadSize => 16 + 1 + 16 + 8;

    public ReadOnlySpan<byte> Serialize()
    {
        var buffer = new byte[PayloadSize];
        var offset = 0;

        MessageId.TryWriteBytes(buffer.AsSpan(offset, 16));
        offset += 16;

        buffer[offset] = Priority;
        offset += 1;

        if (CorrelationId.HasValue)
        {
            CorrelationId.Value.TryWriteBytes(buffer.AsSpan(offset, 16));
        }
        offset += 16;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), A);
        offset += 4;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), B);

        return buffer;
    }

    public void Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < PayloadSize)
        {
            return;
        }

        var offset = 0;

        MessageId = new Guid(data.Slice(offset, 16));
        offset += 16;

        Priority = data[offset];
        offset += 1;

        var correlationBytes = data.Slice(offset, 16);
        if (!correlationBytes.ToArray().All(b => b == 0))
        {
            CorrelationId = new Guid(correlationBytes);
        }
        offset += 16;

        A = BitConverter.ToSingle(data.Slice(offset, 4));
        offset += 4;

        B = BitConverter.ToSingle(data.Slice(offset, 4));
    }
}
