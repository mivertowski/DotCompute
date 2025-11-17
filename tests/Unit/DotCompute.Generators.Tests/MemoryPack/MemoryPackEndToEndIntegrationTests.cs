// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using DotCompute.Generators.MemoryPack;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Generators.Tests.MemoryPack;

/// <summary>
/// End-to-end integration tests for the complete MemoryPack code generation pipeline.
/// Tests discovery, dependency resolution, and code generation for multiple message types.
/// </summary>
public class MemoryPackEndToEndIntegrationTests
{
    /// <summary>
    /// Source code containing 5+ message types with various complexities.
    /// </summary>
    private const string MultipleMessageTypesSource = @"
using System;
using MemoryPack;
using DotCompute.Abstractions.Messaging;

// Mock definitions (needed for compilation)
namespace MemoryPack
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class MemoryPackableAttribute : System.Attribute { }
}

namespace DotCompute.Abstractions.Messaging
{
    public interface IRingKernelMessage
    {
        Guid MessageId { get; set; }
        string MessageType { get; }
        byte Priority { get; set; }
        Guid? CorrelationId { get; set; }
        int PayloadSize { get; }
        ReadOnlySpan<byte> Serialize();
        void Deserialize(ReadOnlySpan<byte> data);
    }
}

namespace RingKernelMessages
{
    // Message Type 1: VectorAdd (simple, 2 floats)
    [MemoryPackable]
    public partial class VectorAddMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(VectorAddMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 40;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public float A { get; set; }
        public float B { get; set; }
    }

    // Message Type 2: MatrixMultiply (medium, array of floats)
    [MemoryPackable]
    public partial class MatrixMultiplyMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(MatrixMultiplyMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 128;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int Rows { get; set; }
        public int Cols { get; set; }
        public float[] Elements { get; set; } = Array.Empty<float>();
    }

    // Message Type 3: Reduction (simple, single int)
    [MemoryPackable]
    public partial class ReductionMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(ReductionMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 36;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int Value { get; set; }
    }

    // Message Type 4: ConvolutionFilter (large, 2D filter kernel)
    [MemoryPackable]
    public partial class ConvolutionFilterMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(ConvolutionFilterMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 512;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int FilterSize { get; set; }
        public float[] FilterKernel { get; set; } = Array.Empty<float>();
    }

    // Message Type 5: HistogramUpdate (medium, bucket counts)
    [MemoryPackable]
    public partial class HistogramUpdateMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(HistogramUpdateMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 256;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int NumBuckets { get; set; }
        public int[] BucketCounts { get; set; } = Array.Empty<int>();
    }

    // Message Type 6: CompositeMessage (complex, nested message)
    [MemoryPackable]
    public partial class CompositeMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(CompositeMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 160;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public VectorAddMessage? NestedVectorAdd { get; set; }
        public ReductionMessage? NestedReduction { get; set; }
        public float Multiplier { get; set; }
    }

    // Message Type 7: TransformMessage (4x4 matrix transformation)
    [MemoryPackable]
    public partial class TransformMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(TransformMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 96;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public float[] Matrix4x4 { get; set; } = new float[16];
    }

    // Message Type 8: ParticleStateMessage (physics simulation)
    [MemoryPackable]
    public partial class ParticleStateMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(ParticleStateMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 80;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float VelocityZ { get; set; }
        public float AccelerationX { get; set; }
        public float AccelerationY { get; set; }
        public float AccelerationZ { get; set; }
        public float Mass { get; set; }
    }

    // Message Type 9: ScanMessage (prefix sum operation)
    [MemoryPackable]
    public partial class ScanMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(ScanMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 192;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int[] InputValues { get; set; } = Array.Empty<int>();
        public bool Inclusive { get; set; }
    }

    // Message Type 10: BitonicSortMessage (sorting operation)
    [MemoryPackable]
    public partial class BitonicSortMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(BitonicSortMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 320;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int[] SortKeys { get; set; } = Array.Empty<int>();
        public int[] SortValues { get; set; } = Array.Empty<int>();
        public bool Ascending { get; set; }
    }
}
";

    [Fact(DisplayName = "EndToEnd: Discover all 10 message types from source code")]
    public void EndToEnd_DiscoverAllMessageTypes_ShouldFind10Types()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);

        // Act
        var messageTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        messageTypes.Should().HaveCount(10, "source contains 10 [MemoryPackable] message types");

        var typeNames = messageTypes.Select(t => t.Name).ToList();
        typeNames.Should().Contain("VectorAddMessage");
        typeNames.Should().Contain("MatrixMultiplyMessage");
        typeNames.Should().Contain("ReductionMessage");
        typeNames.Should().Contain("ConvolutionFilterMessage");
        typeNames.Should().Contain("HistogramUpdateMessage");
        typeNames.Should().Contain("CompositeMessage");
        typeNames.Should().Contain("TransformMessage");
        typeNames.Should().Contain("ParticleStateMessage");
        typeNames.Should().Contain("ScanMessage");
        typeNames.Should().Contain("BitonicSortMessage");
    }

    [Fact(DisplayName = "EndToEnd: Build dependency graph for all types")]
    public void EndToEnd_BuildDependencyGraph_ShouldIncludeAllTypes()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var messageTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var dependencyGraph = MessageTypeDiscovery.BuildDependencyGraph(messageTypes);

        // Assert
        dependencyGraph.Should().HaveCount(10, "10 types in dependency graph");

        // Verify all 10 types are in the graph
        var typeNames = dependencyGraph.Select(n => n.Type.Name).ToList();
        typeNames.Should().Contain(new[]
        {
            "VectorAddMessage",
            "MatrixMultiplyMessage",
            "ReductionMessage",
            "ConvolutionFilterMessage",
            "HistogramUpdateMessage",
            "CompositeMessage",
            "TransformMessage",
            "ParticleStateMessage",
            "ScanMessage",
            "BitonicSortMessage"
        });

        // Independent messages should have no dependencies
        var vectorAddNode = dependencyGraph.First(n => n.Type.Name == "VectorAddMessage");
        vectorAddNode.Dependencies.Should().BeEmpty("VectorAddMessage has no nested messages");
    }

    [Fact(DisplayName = "EndToEnd: Generate CUDA code for all 10 message types")]
    public void EndToEnd_GenerateCudaCodeForAll_ShouldProduceAll10Files()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().HaveCount(10, "10 message types should generate 10 CUDA files");

        var fileNames = results.Select(r => r.FileName).ToList();
        fileNames.Should().Contain(new[]
        {
            "VectorAddMessageSerialization",
            "MatrixMultiplyMessageSerialization",
            "ReductionMessageSerialization",
            "ConvolutionFilterMessageSerialization",
            "HistogramUpdateMessageSerialization",
            "CompositeMessageSerialization",
            "TransformMessageSerialization",
            "ParticleStateMessageSerialization",
            "ScanMessageSerialization",
            "BitonicSortMessageSerialization"
        });

        // All results should have generated source code
        foreach (var result in results)
        {
            result.SourceCode.Should().NotBeNullOrWhiteSpace($"{result.FileName} should have generated code");
        }
    }

    [Fact(DisplayName = "EndToEnd: Verify CUDA header guards for all types")]
    public void EndToEnd_VerifyHeaderGuards_ShouldExist()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert - Verify each file has proper header guards
        foreach (var result in results)
        {
            result.SourceCode.Should().Contain("#ifndef DOTCOMPUTE_",
                $"{result.FileName} should have #ifndef guard");
            result.SourceCode.Should().Contain("#define DOTCOMPUTE_",
                $"{result.FileName} should have #define guard");
            result.SourceCode.Should().Contain("#endif // DOTCOMPUTE_",
                $"{result.FileName} should have #endif guard");
        }
    }

    [Fact(DisplayName = "EndToEnd: Verify CUDA code quality for all types")]
    public void EndToEnd_VerifyCudaCodeQuality_ShouldBeComplete()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert - Verify all generated code is complete and non-empty
        foreach (var result in results)
        {
            result.SourceCode.Should().NotBeNullOrWhiteSpace($"{result.FileName} should have code");
            result.SourceCode.Length.Should().BeGreaterThan(100, $"{result.FileName} should have substantial code");
            result.TypeName.Should().NotBeNullOrWhiteSpace($"{result.FileName} should have type name");
            result.TotalSize.Should().BeGreaterThan(0, $"{result.FileName} should have calculated size");
        }
    }

    [Fact(DisplayName = "EndToEnd: Verify CUDA struct definitions for all types")]
    public void EndToEnd_VerifyStructDefinitions_ShouldExistForAll()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert - Verify all 10 struct definitions exist
        results[0].SourceCode.Should().Contain("struct vector_add_message");
        results[1].SourceCode.Should().Contain("struct matrix_multiply_message");
        results[2].SourceCode.Should().Contain("struct reduction_message");
        results[3].SourceCode.Should().Contain("struct convolution_filter_message");
        results[4].SourceCode.Should().Contain("struct histogram_update_message");
        results[5].SourceCode.Should().Contain("struct composite_message");
        results[6].SourceCode.Should().Contain("struct transform_message");
        results[7].SourceCode.Should().Contain("struct particle_state_message");
        results[8].SourceCode.Should().Contain("struct scan_message");
        results[9].SourceCode.Should().Contain("struct bitonic_sort_message");
    }

    [Fact(DisplayName = "EndToEnd: Verify serialization/deserialization functions")]
    public void EndToEnd_VerifyFunctions_ShouldExistForAll()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        foreach (var result in results)
        {
            // The generated code uses the struct name, which is derived from the type name
            // For example: VectorAddMessage -> vector_add_message
            result.SourceCode.Should().Contain("__device__ bool deserialize_",
                $"deserialize function should exist for {result.FileName}");
            result.SourceCode.Should().Contain("__device__ void serialize_",
                $"serialize function should exist for {result.FileName}");
        }
    }

    [Fact(DisplayName = "EndToEnd: Verify standard CUDA includes in all files")]
    public void EndToEnd_VerifyStandardIncludes_ShouldExistInAll()
    {
        // Arrange
        var compilation = CreateCompilation(MultipleMessageTypesSource);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        foreach (var result in results)
        {
            result.SourceCode.Should().Contain("#include <cstdint>",
                $"{result.FileName} should include standard integer types");
            result.SourceCode.Should().Contain("// Auto-generated MemoryPack CUDA serialization code",
                $"{result.FileName} should have generation comment");
        }
    }

    #region Helper Methods

    /// <summary>
    /// Creates a C# compilation from source code for testing.
    /// </summary>
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };

        // Add System.Runtime reference for Span<T>
        var systemRuntime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (systemRuntime != null && !string.IsNullOrEmpty(systemRuntime.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
        }

        // Add System.Memory for ReadOnlySpan<T>
        var systemMemory = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Memory");
        if (systemMemory != null && !string.IsNullOrEmpty(systemMemory.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemMemory.Location));
        }

        return CSharpCompilation.Create(
            "EndToEndTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Converts PascalCase to snake_case for CUDA naming.
    /// </summary>
    private static string ConvertToSnakeCase(string text)
    {
        return string.Concat(
            text.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c).ToString() : char.ToLowerInvariant(c).ToString()));
    }

    #endregion
}
