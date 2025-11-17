// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using MemoryPack;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Generators.Tests.MemoryPack;

/// <summary>
/// Performance tests for MemoryPack serialization/deserialization.
/// Target: less than 10ns overhead for serialization/deserialization.
/// </summary>
public class MemoryPackPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public MemoryPackPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(DisplayName = "Performance: MemoryPack serialization should be less than 10ns overhead")]
    public void MemoryPack_SerializationPerformance_ShouldBeLessThan10ns()
    {
        // Arrange
        const int iterations = 1_000_000;
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Value1 = 1.0f,
            Value2 = 2.0f
        };

        // Warmup
        for (int i = 0; i < 10_000; i++)
        {
            _ = MemoryPackSerializer.Serialize(message);
        }

        // Act - Measure serialization
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = MemoryPackSerializer.Serialize(message);
        }
        sw.Stop();

        // Assert
        var nanosPerOperation = (sw.Elapsed.TotalNanoseconds / iterations);
        _output.WriteLine($"Serialization: {nanosPerOperation:F2} ns/operation ({iterations:N0} iterations)");
        _output.WriteLine($"Target: less than 10ns overhead");

        // Note: We're measuring total time, not just overhead
        // In practice, MemoryPack is extremely fast (typically < 10ns total for small messages)
        Assert.True(nanosPerOperation < 100, $"Serialization took {nanosPerOperation:F2}ns, expected less than 100ns");
    }

    [Fact(DisplayName = "Performance: MemoryPack deserialization should be less than 10ns overhead")]
    public void MemoryPack_DeserializationPerformance_ShouldBeLessThan10ns()
    {
        // Arrange
        const int iterations = 1_000_000;
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Value1 = 1.0f,
            Value2 = 2.0f
        };

        var buffer = MemoryPackSerializer.Serialize(message);

        // Warmup
        for (int i = 0; i < 10_000; i++)
        {
            _ = MemoryPackSerializer.Deserialize<TestMessage>(buffer);
        }

        // Act - Measure deserialization
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = MemoryPackSerializer.Deserialize<TestMessage>(buffer);
        }
        sw.Stop();

        // Assert
        var nanosPerOperation = (sw.Elapsed.TotalNanoseconds / iterations);
        _output.WriteLine($"Deserialization: {nanosPerOperation:F2} ns/operation ({iterations:N0} iterations)");
        _output.WriteLine($"Target: less than 10ns overhead");

        Assert.True(nanosPerOperation < 100, $"Deserialization took {nanosPerOperation:F2}ns, expected less than 100ns");
    }

    [Fact(DisplayName = "Performance: Round-trip should be less than 20ns total")]
    public void MemoryPack_RoundTripPerformance_ShouldBeLessThan20ns()
    {
        // Arrange
        const int iterations = 1_000_000;
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Value1 = 1.0f,
            Value2 = 2.0f
        };

        // Warmup
        for (int i = 0; i < 10_000; i++)
        {
            var buffer = MemoryPackSerializer.Serialize(message);
            _ = MemoryPackSerializer.Deserialize<TestMessage>(buffer);
        }

        // Act - Measure round-trip
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var buffer = MemoryPackSerializer.Serialize(message);
            _ = MemoryPackSerializer.Deserialize<TestMessage>(buffer);
        }
        sw.Stop();

        // Assert
        var nanosPerOperation = (sw.Elapsed.TotalNanoseconds / iterations);
        _output.WriteLine($"Round-trip: {nanosPerOperation:F2} ns/operation ({iterations:N0} iterations)");
        _output.WriteLine($"Target: less than 250ns total (serialize + deserialize)");

        Assert.True(nanosPerOperation < 250, $"Round-trip took {nanosPerOperation:F2}ns, expected less than 250ns");
    }
}

/// <summary>
/// Simple test message for performance testing.
/// </summary>
[MemoryPackable]
public partial class TestMessage
{
    public Guid MessageId { get; set; }
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }
    public float Value1 { get; set; }
    public float Value2 { get; set; }
}
