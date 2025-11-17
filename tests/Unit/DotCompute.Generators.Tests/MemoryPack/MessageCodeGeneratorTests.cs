// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Generators.MemoryPack;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Generators.Tests.MemoryPack;

/// <summary>
/// Unit tests for MessageCodeGenerator.
/// Tests batch CUDA code generation with dependency resolution.
/// </summary>
public class MessageCodeGeneratorTests
{
    /// <summary>
    /// Mock definitions for MemoryPackable attribute and IRingKernelMessage interface.
    /// Included in test source code since we can't reference actual assemblies in test compilations.
    /// </summary>
    private const string MockDefinitions = @"
using System;
using MemoryPack;
using DotCompute.Abstractions.Messaging;

// Mock attribute for testing
namespace MemoryPack
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class MemoryPackableAttribute : System.Attribute { }
}

// Mock interface for testing
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
";

    #region Constructor Tests

    [Fact(DisplayName = "GenerateBatch should throw on null compilation")]
    public void GenerateBatch_WithNullCompilation_ShouldThrow()
    {
        // Arrange
        var generator = new MessageCodeGenerator();

        // Act
        Action act = () => generator.GenerateBatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("compilation");
    }

    #endregion

    #region Batch Generation Tests

    [Fact(DisplayName = "GenerateBatch should return empty for compilation with no message types")]
    public void GenerateBatch_WithNoMessageTypes_ShouldReturnEmpty()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    public class NotAMessage { }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().BeEmpty("no message types exist in compilation");
    }

    [Fact(DisplayName = "GenerateBatch should generate code for single message type")]
    public void GenerateBatch_WithSingleMessageType_ShouldGenerateCode()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class SimpleRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(SimpleRequest);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int Value { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().HaveCount(1, "one message type exists");
        var result = results[0];
        result.FileName.Should().Be("SimpleRequestSerialization");
        result.TypeName.Should().Contain("SimpleRequest");
        result.SourceCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "GenerateBatch should generate code for multiple message types")]
    public void GenerateBatch_WithMultipleMessageTypes_ShouldGenerateAll()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class FirstRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(FirstRequest);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public float A { get; set; }
    }

    [MemoryPackable]
    public partial class SecondRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(SecondRequest);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public double B { get; set; }
    }

    [MemoryPackable]
    public partial class ThirdRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(ThirdRequest);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int C { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().HaveCount(3, "three message types exist");
        results.Select(r => r.FileName).Should().Contain(new[]
        {
            "FirstRequestSerialization",
            "SecondRequestSerialization",
            "ThirdRequestSerialization"
        });
    }

    #endregion

    #region Dependency Resolution Tests

    [Fact(DisplayName = "GenerateBatch should handle nested type dependencies correctly")]
    public void GenerateBatch_WithNestedDependencies_ShouldResolveOrder()
    {
        // Arrange - MessageB depends on MessageA
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class MessageA : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(MessageA);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public float Value { get; set; }
    }

    [MemoryPackable]
    public partial class MessageB : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(MessageB);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public MessageA NestedMessage { get; set; } // Dependency on MessageA
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().HaveCount(2, "two message types exist");

        // Find MessageB result
        var messageBResult = results.First(r => r.FileName == "MessageBSerialization");
        messageBResult.Dependencies.Should().Contain("MessageA", "MessageB depends on MessageA");

        // MessageB's source code should include MessageA
        messageBResult.SourceCode.Should().Contain("#include \"MessageASerialization.cu\"",
            "should include dependency header");
    }

    #endregion

    #region Header Guards Tests

    [Fact(DisplayName = "GenerateBatch should add header guards to generated code")]
    public void GenerateBatch_ShouldAddHeaderGuards()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class MyMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(MyMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public int Value { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        var result = results[0];
        result.SourceCode.Should().Contain("#ifndef DOTCOMPUTE_MY_MESSAGE_SERIALIZATION_H");
        result.SourceCode.Should().Contain("#define DOTCOMPUTE_MY_MESSAGE_SERIALIZATION_H");
        result.SourceCode.Should().Contain("#endif // DOTCOMPUTE_MY_MESSAGE_SERIALIZATION_H");
    }

    [Fact(DisplayName = "GenerateBatch should include standard headers")]
    public void GenerateBatch_ShouldIncludeStandardHeaders()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class TestMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(TestMessage);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public long Timestamp { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        var result = results[0];
        result.SourceCode.Should().Contain("#include <cstdint>", "should include standard integer types");
        result.SourceCode.Should().Contain("// Standard CUDA includes");
    }

    #endregion

    #region Code Validation Tests

    [Fact(DisplayName = "GenerateBatch should produce valid CUDA struct definitions")]
    public void GenerateBatch_ShouldProduceValidStructDefinitions()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class VectorRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => nameof(VectorRequest);
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        var result = results[0];

        // Should contain struct definition
        result.SourceCode.Should().Contain("struct vector_request");

        // Should contain field declarations
        result.SourceCode.Should().Contain("float x;");
        result.SourceCode.Should().Contain("float y;");
        result.SourceCode.Should().Contain("float z;");

        // Should contain deserialize function
        result.SourceCode.Should().Contain("__device__ bool deserialize_vector_request(");

        // Should contain serialize function
        result.SourceCode.Should().Contain("__device__ void serialize_vector_request(");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a C# compilation from source code for testing.
    /// </summary>
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Add necessary references
        var references = new List<MetadataReference>
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

        // Add System.Memory if available (for ReadOnlySpan)
        var systemMemory = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Memory");
        if (systemMemory != null && !string.IsNullOrEmpty(systemMemory.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemMemory.Location));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    #endregion
}
