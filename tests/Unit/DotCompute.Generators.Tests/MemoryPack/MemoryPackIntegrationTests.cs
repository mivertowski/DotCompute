// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using DotCompute.Generators.MemoryPack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Generators.Tests.MemoryPack;

/// <summary>
/// Integration tests for MemoryPack CUDA pipeline with real message types.
/// </summary>
public sealed class MemoryPackIntegrationTests
{
    /// <summary>
    /// Test: End-to-end pipeline for VectorAddRequest (analyze + generate).
    /// </summary>
    [Fact(DisplayName = "Integration: VectorAddRequest full pipeline produces valid CUDA code")]
    public void EndToEnd_VectorAddRequest_ProducesValidCudaCode()
    {
        // Arrange: Real VectorAddRequest type
        const string code = @"
using System;
using MemoryPack;

namespace DotCompute.Hardware.Cuda.Tests.Messaging
{
    [MemoryPackable]
    public partial class VectorAddRequest
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddRequest");

        // Act: Analyze type
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Binary format is correct
        Assert.Equal("DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddRequest", spec.TypeName);
        Assert.Equal(42, spec.TotalSize); // 16 + 1 + 17 + 4 + 4
        Assert.True(spec.IsFixedSize);
        Assert.Equal(5, spec.Fields.Length);

        // Act: Generate CUDA code
        var generator = new MemoryPackCudaGenerator();
        var cudaCode = generator.GenerateCode(spec);

        // DEBUG: Print generated code and field info
        System.Console.WriteLine("=== FIELD SPECIFICATIONS ===");
        foreach (var field in spec.Fields)
        {
            System.Console.WriteLine($"Field: {field.Name}, Type: {field.FieldType}, CudaType: {field.CudaTypeName}, IsNullable: {field.IsNullable}, Size: {field.Size}");
        }
        System.Console.WriteLine("=== GENERATED CUDA CODE ===");
        System.Console.WriteLine(cudaCode);
        System.Console.WriteLine("=== END GENERATED CODE ===");

        // Assert: Generated CUDA code is valid
        Assert.Contains("struct vector_add_request", cudaCode, StringComparison.Ordinal);
        Assert.Contains("unsigned char message_id[16];", cudaCode, StringComparison.Ordinal);
        Assert.Contains("uint8_t priority;", cudaCode, StringComparison.Ordinal);
        Assert.Contains("struct { bool has_value; unsigned char value[16]; } correlation_id;", cudaCode, StringComparison.Ordinal);
        Assert.Contains("float a;", cudaCode, StringComparison.Ordinal);
        Assert.Contains("float b;", cudaCode, StringComparison.Ordinal);

        // Assert: Deserialize function
        Assert.Contains("__device__ bool deserialize_vector_add_request(", cudaCode, StringComparison.Ordinal);
        Assert.Contains("const unsigned char* buffer,", cudaCode, StringComparison.Ordinal);
        Assert.Contains("int buffer_size,", cudaCode, StringComparison.Ordinal);
        Assert.Contains("vector_add_request* out)", cudaCode, StringComparison.Ordinal);
        Assert.Contains("if (buffer_size < 42)", cudaCode, StringComparison.Ordinal);
        Assert.Contains("return false; // Buffer too small", cudaCode, StringComparison.Ordinal);

        // Assert: Serialize function
        Assert.Contains("__device__ void serialize_vector_add_request(", cudaCode, StringComparison.Ordinal);
        Assert.Contains("const vector_add_request* input,", cudaCode, StringComparison.Ordinal);
        Assert.Contains("unsigned char* buffer)", cudaCode, StringComparison.Ordinal);

        // Assert: Header comment with metadata
        Assert.Contains("// Auto-generated MemoryPack CUDA serialization code", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// Type: DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddRequest", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// Total Size: 42 bytes", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// Fixed Size: Yes", cudaCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: End-to-end pipeline for VectorAddResponse.
    /// </summary>
    [Fact(DisplayName = "Integration: VectorAddResponse full pipeline produces valid CUDA code")]
    public void EndToEnd_VectorAddResponse_ProducesValidCudaCode()
    {
        // Arrange: Real VectorAddResponse type
        const string code = @"
using System;
using MemoryPack;

namespace DotCompute.Hardware.Cuda.Tests.Messaging
{
    [MemoryPackable]
    public partial class VectorAddResponse
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float Result { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddResponse");

        // Act: Analyze type
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Binary format is correct
        Assert.Equal("DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddResponse", spec.TypeName);
        Assert.Equal(38, spec.TotalSize); // 16 + 1 + 17 + 4
        Assert.True(spec.IsFixedSize);
        Assert.Equal(4, spec.Fields.Length);

        // Act: Generate CUDA code
        var generator = new MemoryPackCudaGenerator();
        var cudaCode = generator.GenerateCode(spec);

        // Assert: Generated CUDA code is valid
        Assert.Contains("struct vector_add_response", cudaCode, StringComparison.Ordinal);
        Assert.Contains("unsigned char message_id[16];", cudaCode, StringComparison.Ordinal);
        Assert.Contains("uint8_t priority;", cudaCode, StringComparison.Ordinal);
        Assert.Contains("struct { bool has_value; unsigned char value[16]; } correlation_id;", cudaCode, StringComparison.Ordinal);
        Assert.Contains("float result;", cudaCode, StringComparison.Ordinal);

        // Assert: Deserialize function with correct size
        Assert.Contains("if (buffer_size < 38)", cudaCode, StringComparison.Ordinal);

        // Assert: Header comment
        Assert.Contains("// Total Size: 38 bytes", cudaCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test: Binary format consistency between analyzer and manual serialization.
    /// </summary>
    [Fact(DisplayName = "Integration: Analyzer format matches manual PayloadSize")]
    public void Integration_AnalyzerFormat_MatchesManualPayloadSize()
    {
        // Arrange: VectorAddRequest with manual PayloadSize = 41
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class VectorAddRequest
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }

        // Manual serialization claims 41 bytes (incorrect - missing nullable presence byte)
        public int PayloadSize => 16 + 1 + 16 + 8;
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.VectorAddRequest");

        // Act: Analyze type
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
        var spec = analyzer.AnalyzeType(typeSymbol);

        // Assert: Analyzer correctly identifies 42 bytes (16 + 1 + 17 + 4 + 4)
        // The manual PayloadSize of 41 is incorrect because it doesn't account for nullable presence byte
        Assert.Equal(42, spec.TotalSize);
        Assert.NotEqual(41, spec.TotalSize); // Manual serialization is wrong!

        // Assert: CorrelationId field is 17 bytes (1 presence + 16 Guid)
        var correlationIdField = spec.Fields.FirstOrDefault(f => f.Name == "CorrelationId");
        Assert.NotNull(correlationIdField);
        Assert.Equal(17, correlationIdField.Size);
        Assert.True(correlationIdField.IsNullable);
    }

    /// <summary>
    /// Test: Generated CUDA code has correct field offsets.
    /// </summary>
    [Fact(DisplayName = "Integration: Generated CUDA has correct field offsets")]
    public void Integration_GeneratedCuda_HasCorrectFieldOffsets()
    {
        // Arrange
        const string code = @"
using System;
using MemoryPack;

namespace Test
{
    [MemoryPackable]
    public partial class VectorAddRequest
    {
        public Guid MessageId { get; set; }
        public byte Priority { get; set; }
        public Guid? CorrelationId { get; set; }
        public float A { get; set; }
        public float B { get; set; }
    }
}";

        var (compilation, semanticModel, typeSymbol) = CompileAndGetType(code, "Test.VectorAddRequest");
        var analyzer = new MemoryPackFormatAnalyzer(semanticModel);
        var spec = analyzer.AnalyzeType(typeSymbol);
        var generator = new MemoryPackCudaGenerator();

        // Act
        var cudaCode = generator.GenerateCode(spec);

        // DEBUG: Print generated comments
        System.Console.WriteLine("=== GENERATED CUDA CODE COMMENTS ===");
        var lines = cudaCode.Split('\n');
        foreach (var line in lines.Where(l => l.Contains("// MessageId:") || l.Contains("// Priority:") || l.Contains("// CorrelationId:") || l.Contains("// A:") || l.Contains("// B:")))
        {
            System.Console.WriteLine(line);
        }
        System.Console.WriteLine("=== END COMMENTS ===");

        // Assert: Field offsets in comments match specification
        Assert.Contains("// MessageId: System.Guid at offset 0 (16 bytes)", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// Priority: System.Byte at offset 16 (1 bytes)", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// CorrelationId: System.Guid at offset 17 (17 bytes)", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// A: System.Single at offset 34 (4 bytes)", cudaCode, StringComparison.Ordinal);
        Assert.Contains("// B: System.Single at offset 38 (4 bytes)", cudaCode, StringComparison.Ordinal);

        // Assert: Buffer access uses correct offsets
        Assert.Contains("buffer[0 + i]", cudaCode, StringComparison.Ordinal); // MessageId[0]
        Assert.Contains("buffer[16]", cudaCode, StringComparison.Ordinal); // Priority
        Assert.Contains("buffer[17]", cudaCode, StringComparison.Ordinal); // CorrelationId.has_value
        Assert.Contains("buffer[18 + i]", cudaCode, StringComparison.Ordinal); // CorrelationId.value[0]
        Assert.Contains("buffer[34]", cudaCode, StringComparison.Ordinal); // A
        Assert.Contains("buffer[38]", cudaCode, StringComparison.Ordinal); // B
    }

    /// <summary>
    /// Helper: Compile code and extract type symbol.
    /// </summary>
    private static (CSharpCompilation compilation, SemanticModel semanticModel, INamedTypeSymbol typeSymbol) CompileAndGetType(
        string code,
        string typeName)
    {
        // Add mock MemoryPackable attribute definition
        const string memoryPackableAttributeCode = @"
namespace MemoryPack
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class MemoryPackableAttribute : System.Attribute
    {
    }
}";

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(code),
            CSharpSyntaxTree.ParseText(memoryPackableAttributeCode)
        };

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "IntegrationTestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTrees[0]);

        // Find the type symbol
        var typeSymbol = compilation.GetTypeByMetadataName(typeName);
        if (typeSymbol == null)
        {
            throw new System.InvalidOperationException($"Type '{typeName}' not found in compilation");
        }

        return (compilation, semanticModel, typeSymbol);
    }
}
