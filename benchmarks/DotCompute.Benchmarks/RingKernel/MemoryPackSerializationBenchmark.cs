// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using MemoryPack;

namespace DotCompute.Benchmarks.RingKernel;

/// <summary>
/// Benchmarks MemoryPack serialization/deserialization performance for ring kernel messages.
/// Target: less than 10ns overhead for serialization/deserialization.
/// </summary>
/// <remarks>
/// <para>
/// <b>Benchmark Categories:</b>
/// - Small Message: 32 bytes (2x float + header)
/// - Medium Message: 128 bytes (8x float + header)
/// - Large Message: 1024 bytes (64x float + header)
/// </para>
/// <para>
/// <b>Performance Targets (Phase 1):</b>
/// - Serialization: less than 10ns overhead
/// - Deserialization: less than 10ns overhead
/// - Round-trip: less than 20ns total
/// </para>
/// </remarks>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, StdDevColumn]
[SimpleJob(RuntimeMoniker.Net90, baseline: true)]
public class MemoryPackSerializationBenchmark
{
    private SmallMessage _smallMessage = null!;
    private MediumMessage _mediumMessage = null!;
    private LargeMessage _largeMessage = null!;

    private byte[] _smallSerializedData = null!;
    private byte[] _mediumSerializedData = null!;
    private byte[] _largeSerializedData = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Small message: 2 floats
        _smallMessage = new SmallMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            X = 1.0f,
            Y = 2.0f
        };

        // Medium message: 8 floats
        _mediumMessage = new MediumMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Values = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f }
        };

        // Large message: 64 floats
        var largeValues = new float[64];
        for (int i = 0; i < 64; i++)
            largeValues[i] = i * 1.0f;

        _largeMessage = new LargeMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Matrix = largeValues
        };

        // Pre-serialize data for deserialization benchmarks
        _smallSerializedData = MemoryPackSerializer.Serialize(_smallMessage);
        _mediumSerializedData = MemoryPackSerializer.Serialize(_mediumMessage);
        _largeSerializedData = MemoryPackSerializer.Serialize(_largeMessage);

        Console.WriteLine($"Benchmark Setup Complete:");
        Console.WriteLine($"  Small Message: {_smallMessage.PayloadSize} bytes");
        Console.WriteLine($"  Medium Message: {_mediumMessage.PayloadSize} bytes");
        Console.WriteLine($"  Large Message: {_largeMessage.PayloadSize} bytes");
    }

    #region Small Message Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Small", "Serialize")]
    public byte[] Serialize_SmallMessage_MemoryPack()
    {
        return MemoryPackSerializer.Serialize(_smallMessage);
    }

    [Benchmark]
    [BenchmarkCategory("Small", "Deserialize")]
    public SmallMessage Deserialize_SmallMessage_MemoryPack()
    {
        return MemoryPackSerializer.Deserialize<SmallMessage>(_smallSerializedData)!;
    }

    [Benchmark]
    [BenchmarkCategory("Small", "RoundTrip")]
    public SmallMessage RoundTrip_SmallMessage_MemoryPack()
    {
        var bytes = MemoryPackSerializer.Serialize(_smallMessage);
        return MemoryPackSerializer.Deserialize<SmallMessage>(bytes)!;
    }

    [Benchmark]
    [BenchmarkCategory("Small", "Baseline")]
    public byte[] Serialize_SmallMessage_Manual()
    {
        var buffer = new byte[64];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var guidPtr = (Guid*)ptr;
                *guidPtr = _smallMessage.MessageId;
                ptr[16] = _smallMessage.Priority;
                var corrPtr = (Guid*)(ptr + 17);
                *corrPtr = _smallMessage.CorrelationId.GetValueOrDefault();
                var floatPtr = (float*)(ptr + 33);
                floatPtr[0] = _smallMessage.X;
                floatPtr[1] = _smallMessage.Y;
            }
        }
        return buffer;
    }

    #endregion

    #region Medium Message Benchmarks

    [Benchmark]
    [BenchmarkCategory("Medium", "Serialize")]
    public byte[] Serialize_MediumMessage_MemoryPack()
    {
        return MemoryPackSerializer.Serialize(_mediumMessage);
    }

    [Benchmark]
    [BenchmarkCategory("Medium", "Deserialize")]
    public MediumMessage Deserialize_MediumMessage_MemoryPack()
    {
        return MemoryPackSerializer.Deserialize<MediumMessage>(_mediumSerializedData)!;
    }

    [Benchmark]
    [BenchmarkCategory("Medium", "RoundTrip")]
    public MediumMessage RoundTrip_MediumMessage_MemoryPack()
    {
        var bytes = MemoryPackSerializer.Serialize(_mediumMessage);
        return MemoryPackSerializer.Deserialize<MediumMessage>(bytes)!;
    }

    #endregion

    #region Large Message Benchmarks

    [Benchmark]
    [BenchmarkCategory("Large", "Serialize")]
    public byte[] Serialize_LargeMessage_MemoryPack()
    {
        return MemoryPackSerializer.Serialize(_largeMessage);
    }

    [Benchmark]
    [BenchmarkCategory("Large", "Deserialize")]
    public LargeMessage Deserialize_LargeMessage_MemoryPack()
    {
        return MemoryPackSerializer.Deserialize<LargeMessage>(_largeSerializedData)!;
    }

    [Benchmark]
    [BenchmarkCategory("Large", "RoundTrip")]
    public LargeMessage RoundTrip_LargeMessage_MemoryPack()
    {
        var bytes = MemoryPackSerializer.Serialize(_largeMessage);
        return MemoryPackSerializer.Deserialize<LargeMessage>(bytes)!;
    }

    #endregion
}

#region Message Type Definitions

/// <summary>
/// Small message for benchmarking (32 bytes).
/// </summary>
[MemoryPackable]
public partial class SmallMessage
{
    public Guid MessageId { get; set; }
    public string MessageType => nameof(SmallMessage);
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }
    public int PayloadSize => 32;

    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>
/// Medium message for benchmarking (128 bytes).
/// </summary>
[MemoryPackable]
public partial class MediumMessage
{
    public Guid MessageId { get; set; }
    public string MessageType => nameof(MediumMessage);
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }
    public int PayloadSize => 128;

    public float[] Values { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Large message for benchmarking (1024 bytes).
/// </summary>
[MemoryPackable]
public partial class LargeMessage
{
    public Guid MessageId { get; set; }
    public string MessageType => nameof(LargeMessage);
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }
    public int PayloadSize => 1024;

    public float[] Matrix { get; set; } = Array.Empty<float>();
}

#endregion
