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

    #region Handler Translation Tests

    [Fact(DisplayName = "GenerateBatch should translate VectorAddHandler to CUDA")]
    public void GenerateBatch_WithVectorAddHandler_ShouldTranslateHandlerToCuda()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class VectorAddRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""VectorAddRequest"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    public static class VectorAddHandler
    {
        public static bool ProcessMessage(
            Span<byte> inputBuffer,
            int inputSize,
            Span<byte> outputBuffer,
            int outputSize)
        {
            // Validate buffer sizes
            if (inputSize < 42 || outputSize < 38)
            {
                return false;
            }

            int offset = 0;

            // Read A (simplified for test)
            float a = BitConverter.ToSingle(inputBuffer.Slice(34, 4));
            float b = BitConverter.ToSingle(inputBuffer.Slice(38, 4));

            // Execute logic
            float result = a + b;

            // Write result
            if (!BitConverter.TryWriteBytes(outputBuffer.Slice(34, 4), result))
            {
                return false;
            }

            return true;
        }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().HaveCount(1, "one message type exists");

        var vectorAddResult = results[0];
        vectorAddResult.FileName.Should().Be("VectorAddRequestSerialization");

        // Verify handler function is included
        vectorAddResult.SourceCode.Should().Contain("process_vector_add_message",
            "handler function should be included in generated code");

        vectorAddResult.SourceCode.Should().Contain("__device__ bool process_vector_add_message",
            "handler should be a CUDA device function");

        // Verify handler has correct signature
        vectorAddResult.SourceCode.Should().Contain("unsigned char* input_buffer",
            "handler should accept input_buffer parameter");
        vectorAddResult.SourceCode.Should().Contain("int32_t input_size",
            "handler should accept input_size parameter");
        vectorAddResult.SourceCode.Should().Contain("unsigned char* output_buffer",
            "handler should accept output_buffer parameter");
        vectorAddResult.SourceCode.Should().Contain("int32_t output_size",
            "handler should accept output_size parameter");

        // Verify buffer validation is translated
        vectorAddResult.SourceCode.Should().Contain("if (input_size < 42 || output_size < 38)",
            "buffer size validation should be translated");

        // Verify return statements are translated
        vectorAddResult.SourceCode.Should().Contain("return false",
            "return false statement should be translated");
        vectorAddResult.SourceCode.Should().Contain("return true",
            "return true statement should be translated");
    }

    [Fact(DisplayName = "GenerateBatch without handler should only include serialization code")]
    public void GenerateBatch_WithoutHandler_ShouldOnlyIncludeSerializationCode()
    {
        // Arrange - no handler class defined
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class SimpleMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""SimpleMessage"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int Value { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }
}";

        var compilation = CreateCompilation(source);
        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert
        results.Should().HaveCount(1);

        var result = results[0];
        result.FileName.Should().Be("SimpleMessageSerialization");

        // Should NOT contain handler function (no handler was defined)
        result.SourceCode.Should().NotContain("process_simple_message",
            "handler function should not be included when no handler class exists");
    }

    [Fact(DisplayName = "DIAGNOSTIC: Output VectorAddHandler translation for debugging")]
    public void Diagnostic_VectorAddHandlerTranslation_OutputToFile()
    {
        // Arrange
        var source = MockDefinitions + @"
namespace Test
{
    [MemoryPackable]
    public partial class VectorAddRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""VectorAddRequest"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }
        public int PayloadSize => 42;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    public static class VectorAddHandler
    {
        public static bool ProcessMessage(
            Span<byte> inputBuffer,
            int inputSize,
            Span<byte> outputBuffer,
            int outputSize)
        {
            if (inputSize < 42 || outputSize < 38)
            {
                return false;
            }

            float a = BitConverter.ToSingle(inputBuffer.Slice(34, 4));
            float b = BitConverter.ToSingle(inputBuffer.Slice(38, 4));
            float result = a + b;

            if (!BitConverter.TryWriteBytes(outputBuffer.Slice(34, 4), result))
            {
                return false;
            }

            return true;
        }
    }
}";

        var compilation = CreateCompilation(source);

        // Debug: List all types in compilation
        var allTypes = compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
            .Select(c => c.Identifier.Text)
            .ToList();

        System.Console.WriteLine($"=== All classes in compilation: {string.Join(", ", allTypes)}");

        var generator = new MessageCodeGenerator();

        // Act
        var results = generator.GenerateBatch(compilation);

        // Assert and output
        results.Should().HaveCount(1);

        var vectorAddResult = results[0];

        // Write to file for manual inspection
        System.IO.File.WriteAllText("/tmp/VectorAddRequest_Generated.cu", vectorAddResult.SourceCode);
        System.Console.WriteLine($"=== Generated code written to /tmp/VectorAddRequest_Generated.cu");
        System.Console.WriteLine($"=== Contains 'process_vector_add_message': {vectorAddResult.SourceCode.Contains("process_vector_add_message")}");
        System.Console.WriteLine($"=== File name: {vectorAddResult.FileName}");
        System.Console.WriteLine($"=== Type name: {vectorAddResult.TypeName}");

        // This diagnostic test always passes - it's for manual inspection
        Assert.True(true);
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
