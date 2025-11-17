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
/// Tests for MessageTypeDiscovery that validates automatic discovery of message types
/// suitable for CUDA code generation.
/// </summary>
public class MessageTypeDiscoveryTests
{
    private const string TestCode = @"
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

namespace TestNamespace
{
    // Valid message type: [MemoryPackable] + IRingKernelMessage
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

    // Valid message type: [MemoryPackable] + IRingKernelMessage
    [MemoryPackable]
    public partial class VectorAddResponse : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""VectorAddResponse"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float Result { get; set; }
        public int PayloadSize => 38;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Invalid: Missing [MemoryPackable]
    public class InvalidNoAttribute : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""Invalid"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 0;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Invalid: Missing IRingKernelMessage
    [MemoryPackable]
    public partial class InvalidNoInterface
    {
        public string Name { get; set; }
    }

    // Invalid: Abstract class
    [MemoryPackable]
    public abstract partial class InvalidAbstract : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public abstract string MessageType { get; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 0;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Valid struct message type
    [MemoryPackable]
    public partial struct StructMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""StructMessage"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 33;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }
}
";

    private const string NestedTypesCode = @"
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

namespace TestNamespace
{
    // Message with no dependencies
    [MemoryPackable]
    public partial class SimpleMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""SimpleMessage"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int Value { get; set; }
        public int PayloadSize => 37;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Message that depends on SimpleMessage
    [MemoryPackable]
    public partial class ComplexMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""ComplexMessage"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public SimpleMessage Nested { get; set; }
        public int PayloadSize => 74;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Message that depends on ComplexMessage (transitive dependency)
    [MemoryPackable]
    public partial class VeryComplexMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""VeryComplexMessage"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public ComplexMessage Nested { get; set; }
        public int PayloadSize => 111;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }
}
";

    private const string PairingCode = @"
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

namespace TestNamespace
{
    // Paired messages: MatrixMultiplyRequest + MatrixMultiplyResponse
    [MemoryPackable]
    public partial class MatrixMultiplyRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""MatrixMultiplyRequest"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 33;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    [MemoryPackable]
    public partial class MatrixMultiplyResponse : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""MatrixMultiplyResponse"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 33;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Unpaired message: Request without Response
    [MemoryPackable]
    public partial class SearchRequest : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""SearchRequest"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 33;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Unpaired message: Response without Request
    [MemoryPackable]
    public partial class NotificationResponse : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""NotificationResponse"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 33;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    // Message not following naming convention
    [MemoryPackable]
    public partial class StatusMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""StatusMessage"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 33;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }
}
";

    [Fact(DisplayName = "DiscoverMessageTypes: Finds valid message types with [MemoryPackable] and IRingKernelMessage")]
    public void DiscoverMessageTypes_FindsValidMessageTypes()
    {
        // Arrange
        var compilation = CreateCompilation(TestCode);

        // Act
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        discoveredTypes.Should().HaveCount(3, "should find VectorAddRequest, VectorAddResponse, and StructMessage");

        var typeNames = discoveredTypes.Select(t => t.Name).ToArray();
        typeNames.Should().Contain("VectorAddRequest");
        typeNames.Should().Contain("VectorAddResponse");
        typeNames.Should().Contain("StructMessage");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Excludes types without [MemoryPackable] attribute")]
    public void DiscoverMessageTypes_ExcludesTypesWithoutAttribute()
    {
        // Arrange
        var compilation = CreateCompilation(TestCode);

        // Act
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        var typeNames = discoveredTypes.Select(t => t.Name).ToArray();
        typeNames.Should().NotContain("InvalidNoAttribute", "types without [MemoryPackable] should be excluded");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Excludes types without IRingKernelMessage interface")]
    public void DiscoverMessageTypes_ExcludesTypesWithoutInterface()
    {
        // Arrange
        var compilation = CreateCompilation(TestCode);

        // Act
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        var typeNames = discoveredTypes.Select(t => t.Name).ToArray();
        typeNames.Should().NotContain("InvalidNoInterface", "types without IRingKernelMessage should be excluded");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Excludes abstract types")]
    public void DiscoverMessageTypes_ExcludesAbstractTypes()
    {
        // Arrange
        var compilation = CreateCompilation(TestCode);

        // Act
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        var typeNames = discoveredTypes.Select(t => t.Name).ToArray();
        typeNames.Should().NotContain("InvalidAbstract", "abstract types should be excluded");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Supports both classes and structs")]
    public void DiscoverMessageTypes_SupportsBothClassesAndStructs()
    {
        // Arrange
        var compilation = CreateCompilation(TestCode);

        // Act
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        var structMessage = discoveredTypes.FirstOrDefault(t => t.Name == "StructMessage");
        structMessage.Should().NotBeNull("should discover struct message types");
        structMessage!.TypeKind.Should().Be(TypeKind.Struct);

        var classMessage = discoveredTypes.FirstOrDefault(t => t.Name == "VectorAddRequest");
        classMessage.Should().NotBeNull("should discover class message types");
        classMessage!.TypeKind.Should().Be(TypeKind.Class);
    }

    [Fact(DisplayName = "ExtractMessagePairs: Matches Request and Response types by naming convention")]
    public void ExtractMessagePairs_MatchesRequestAndResponse()
    {
        // Arrange
        var compilation = CreateCompilation(PairingCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var pairs = MessageTypeDiscovery.ExtractMessagePairs(discoveredTypes);

        // Assert
        pairs.Should().HaveCount(1, "should find one matched pair (MatrixMultiply)");

        var pair = pairs.First();
        pair.OperationName.Should().Be("MatrixMultiply");
        pair.RequestType.Name.Should().Be("MatrixMultiplyRequest");
        pair.ResponseType.Name.Should().Be("MatrixMultiplyResponse");
    }

    [Fact(DisplayName = "ExtractMessagePairs: Excludes unpaired Request types")]
    public void ExtractMessagePairs_ExcludesUnpairedRequests()
    {
        // Arrange
        var compilation = CreateCompilation(PairingCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var pairs = MessageTypeDiscovery.ExtractMessagePairs(discoveredTypes);

        // Assert
        var operationNames = pairs.Select(p => p.OperationName).ToArray();
        operationNames.Should().NotContain("Unpaired", "unpaired requests should not create pairs");
    }

    [Fact(DisplayName = "ExtractMessagePairs: Excludes unpaired Response types")]
    public void ExtractMessagePairs_ExcludesUnpairedResponses()
    {
        // Arrange
        var compilation = CreateCompilation(PairingCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var pairs = MessageTypeDiscovery.ExtractMessagePairs(discoveredTypes);

        // Assert
        var operationNames = pairs.Select(p => p.OperationName).ToArray();
        operationNames.Should().NotContain("Unpaired", "unpaired responses should not create pairs");
    }

    [Fact(DisplayName = "ExtractMessagePairs: Excludes types not following naming convention")]
    public void ExtractMessagePairs_ExcludesNonConventionalTypes()
    {
        // Arrange
        var compilation = CreateCompilation(PairingCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var pairs = MessageTypeDiscovery.ExtractMessagePairs(discoveredTypes);

        // Assert
        var allTypeNames = pairs.SelectMany(p => new[] { p.RequestType.Name, p.ResponseType.Name }).ToArray();
        allTypeNames.Should().NotContain("StatusMessage", "types not ending in Request/Response should not be paired");
    }

    [Fact(DisplayName = "BuildDependencyGraph: Orders types with dependencies first")]
    public void BuildDependencyGraph_OrdersDependenciesFirst()
    {
        // Arrange
        var compilation = CreateCompilation(NestedTypesCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var graph = MessageTypeDiscovery.BuildDependencyGraph(discoveredTypes);

        // Assert
        graph.Should().HaveCount(3, "should have 3 message types");

        var orderedNames = graph.Select(n => n.Type.Name).ToArray();

        // SimpleMessage should come before ComplexMessage (ComplexMessage depends on SimpleMessage)
        var simpleIndex = Array.IndexOf(orderedNames, "SimpleMessage");
        var complexIndex = Array.IndexOf(orderedNames, "ComplexMessage");
        simpleIndex.Should().BeLessThan(complexIndex, "SimpleMessage should be generated before ComplexMessage");

        // ComplexMessage should come before VeryComplexMessage (VeryComplexMessage depends on ComplexMessage)
        var veryComplexIndex = Array.IndexOf(orderedNames, "VeryComplexMessage");
        complexIndex.Should().BeLessThan(veryComplexIndex, "ComplexMessage should be generated before VeryComplexMessage");
    }

    [Fact(DisplayName = "BuildDependencyGraph: Detects dependencies from properties")]
    public void BuildDependencyGraph_DetectsDependenciesFromProperties()
    {
        // Arrange
        var compilation = CreateCompilation(NestedTypesCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var graph = MessageTypeDiscovery.BuildDependencyGraph(discoveredTypes);

        // Assert
        var complexNode = graph.First(n => n.Type.Name == "ComplexMessage");
        complexNode.Dependencies.Should().HaveCount(1, "ComplexMessage depends on SimpleMessage");
        complexNode.Dependencies[0].Name.Should().Be("SimpleMessage");

        var veryComplexNode = graph.First(n => n.Type.Name == "VeryComplexMessage");
        veryComplexNode.Dependencies.Should().HaveCount(1, "VeryComplexMessage depends on ComplexMessage");
        veryComplexNode.Dependencies[0].Name.Should().Be("ComplexMessage");
    }

    [Fact(DisplayName = "BuildDependencyGraph: Handles types with no dependencies")]
    public void BuildDependencyGraph_HandlesTypesWithNoDependencies()
    {
        // Arrange
        var compilation = CreateCompilation(NestedTypesCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var graph = MessageTypeDiscovery.BuildDependencyGraph(discoveredTypes);

        // Assert
        var simpleNode = graph.First(n => n.Type.Name == "SimpleMessage");
        simpleNode.Dependencies.Should().BeEmpty("SimpleMessage has no dependencies on other message types");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Handles null compilation gracefully")]
    public void DiscoverMessageTypes_HandlesNullCompilation()
    {
        // Act
        var result = MessageTypeDiscovery.DiscoverMessageTypes((Compilation)null!);

        // Assert
        result.Should().BeEmpty("null compilation should return empty array");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Handles null semantic model gracefully")]
    public void DiscoverMessageTypes_HandlesNullSemanticModel()
    {
        // Act
        var result = MessageTypeDiscovery.DiscoverMessageTypes((SemanticModel)null!);

        // Assert
        result.Should().BeEmpty("null semantic model should return empty array");
    }

    [Fact(DisplayName = "DiscoverMessageTypes: Handles empty compilation")]
    public void DiscoverMessageTypes_HandlesEmptyCompilation()
    {
        // Arrange
        var compilation = CreateCompilation("");

        // Act
        var result = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Assert
        result.Should().BeEmpty("empty compilation should return empty array");
    }

    [Fact(DisplayName = "BuildDependencyGraph: Handles circular dependencies gracefully")]
    public void BuildDependencyGraph_HandlesCircularDependencies()
    {
        // Arrange: This test verifies that circular dependencies don't cause infinite loops
        var circularCode = @"
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

namespace TestNamespace
{
    [MemoryPackable]
    public partial class MessageA : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""MessageA"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public MessageB Nested { get; set; }
        public int PayloadSize => 66;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    [MemoryPackable]
    public partial class MessageB : IRingKernelMessage
    {
        public Guid MessageId { get; set; }
        public string MessageType => ""MessageB"";
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public MessageA Nested { get; set; }
        public int PayloadSize => 66;
        public ReadOnlySpan<byte> Serialize() => default;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }
}
";
        var compilation = CreateCompilation(circularCode);
        var discoveredTypes = MessageTypeDiscovery.DiscoverMessageTypes(compilation);

        // Act
        var graph = MessageTypeDiscovery.BuildDependencyGraph(discoveredTypes);

        // Assert
        graph.Should().HaveCount(2, "should handle circular dependencies without infinite loop");
        graph.Should().Contain(n => n.Type.Name == "MessageA");
        graph.Should().Contain(n => n.Type.Name == "MessageB");
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Get required assembly locations
        var systemRuntime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        var systemMemory = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Memory");

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };

        // Add System.Runtime if available
        if (systemRuntime != null && !string.IsNullOrEmpty(systemRuntime.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
        }

        // Add System.Memory if available (for ReadOnlySpan)
        if (systemMemory != null && !string.IsNullOrEmpty(systemMemory.Location))
        {
            references.Add(MetadataReference.CreateFromFile(systemMemory.Location));
        }

        // Note: We don't actually need MemoryPack or IRingKernelMessage assemblies
        // because the tests check for attribute/interface names as strings, not the actual types

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
