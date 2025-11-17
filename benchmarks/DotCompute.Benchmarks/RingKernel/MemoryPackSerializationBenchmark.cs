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

    private byte[] _smallBuffer = null!;
    private byte[] _mediumBuffer = null!;
    private byte[] _largeBuffer = null!;

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

        // Pre-allocate buffers
        _smallBuffer = new byte[128];
        _mediumBuffer = new byte[256];
        _largeBuffer = new byte[1024];

        Console.WriteLine($"Benchmark Setup Complete:");
        Console.WriteLine($"  Small Message: {_smallMessage.PayloadSize} bytes");
        Console.WriteLine($"  Medium Message: {_mediumMessage.PayloadSize} bytes");
        Console.WriteLine($"  Large Message: {_largeMessage.PayloadSize} bytes");
    }

    #region Small Message Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Small", "Serialize")]
    public void Serialize_SmallMessage_MemoryPack()
    {
        MemoryPackSerializer.Serialize(_smallBuffer, _smallMessage);
    }

    [Benchmark]
    [BenchmarkCategory("Small", "Deserialize")]
    public SmallMessage Deserialize_SmallMessage_MemoryPack()
    {
        return MemoryPackSerializer.Deserialize<SmallMessage>(_smallBuffer)!;
    }

    [Benchmark]
    [BenchmarkCategory("Small", "RoundTrip")]
    public SmallMessage RoundTrip_SmallMessage_MemoryPack()
    {
        MemoryPackSerializer.Serialize(_smallBuffer, _smallMessage);
        return MemoryPackSerializer.Deserialize<SmallMessage>(_smallBuffer)!;
    }

    [Benchmark]
    [BenchmarkCategory("Small", "Baseline")]
    public void Serialize_SmallMessage_Manual()
    {
        unsafe
        {
            fixed (byte* ptr = _smallBuffer)
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
    }

    #endregion

    #region Medium Message Benchmarks

    [Benchmark]
    [BenchmarkCategory("Medium", "Serialize")]
    public void Serialize_MediumMessage_MemoryPack()
    {
        MemoryPackSerializer.Serialize(_mediumBuffer, _mediumMessage);
    }

    [Benchmark]
    [BenchmarkCategory("Medium", "Deserialize")]
    public MediumMessage Deserialize_MediumMessage_MemoryPack()
    {
        return MemoryPackSerializer.Deserialize<MediumMessage>(_mediumBuffer)!;
    }

    [Benchmark]
    [BenchmarkCategory("Medium", "RoundTrip")]
    public MediumMessage RoundTrip_MediumMessage_MemoryPack()
    {
        MemoryPackSerializer.Serialize(_mediumBuffer, _mediumMessage);
        return MemoryPackSerializer.Deserialize<MediumMessage>(_mediumBuffer)!;
    }

    #endregion

    #region Large Message Benchmarks

    [Benchmark]
    [BenchmarkCategory("Large", "Serialize")]
    public void Serialize_LargeMessage_MemoryPack()
    {
        MemoryPackSerializer.Serialize(_largeBuffer, _largeMessage);
    }

    [Benchmark]
    [BenchmarkCategory("Large", "Deserialize")]
    public LargeMessage Deserialize_LargeMessage_MemoryPack()
    {
        return MemoryPackSerializer.Deserialize<LargeMessage>(_largeBuffer)!;
    }

    [Benchmark]
    [BenchmarkCategory("Large", "RoundTrip")]
    public LargeMessage RoundTrip_LargeMessage_MemoryPack()
    {
        MemoryPackSerializer.Serialize(_largeBuffer, _largeMessage);
        return MemoryPackSerializer.Deserialize<LargeMessage>(_largeBuffer)!;
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
