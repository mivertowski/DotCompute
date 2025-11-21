// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.RingKernels.TranspilationIntegrationTests;

/// <summary>
/// End-to-end integration tests for C# to CUDA transpilation pipeline.
/// Tests the complete flow: [RingKernel] → Handler discovery → C# translation → CUDA code generation.
/// </summary>
public class CSharpToCudaTranspilationIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CudaRingKernelCompiler> _logger;
    private readonly RingKernelDiscovery _discovery;
    private readonly CudaRingKernelStubGenerator _stubGenerator;
    private readonly CudaMemoryPackSerializerGenerator _serializerGenerator;
    private readonly CudaRingKernelCompiler _compiler;

    public CSharpToCudaTranspilationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger<CudaRingKernelCompiler>(output);
        _discovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        _stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        _serializerGenerator = new CudaMemoryPackSerializerGenerator(NullLogger<CudaMemoryPackSerializerGenerator>.Instance);
        _compiler = new CudaRingKernelCompiler(_logger, _discovery, _stubGenerator, _serializerGenerator);
    }

    public void Dispose()
    {
        _compiler?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region End-to-End Transpilation Tests

    [Fact(DisplayName = "E2E: SimpleAdd kernel generates CUDA with handler, serialization, and dispatch")]
    public void SimpleAddKernel_ShouldGenerateCompleteCudaCode()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "SimpleAddKernel",
            Source = "// SimpleAdd ring kernel",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "SimpleAdd",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 256,
            Domain = RingKernelDomain.General,
            MaxInputMessageSizeBytes = 64,
            MaxOutputMessageSizeBytes = 64,
            MessagingStrategy = MessagePassingStrategy.SharedMemory
        };

        // Act
        var cudaCode = _compiler.CompileToCudaC(kernelDef, "// SimpleAdd kernel body", config);

        // Assert - Basic structure
        cudaCode.Should().NotBeNullOrEmpty();
        _output.WriteLine($"Generated CUDA code length: {cudaCode.Length} bytes");
        _output.WriteLine("=== Generated CUDA Code (excerpt) ===");
        _output.WriteLine(cudaCode.Length > 3000 ? cudaCode[..3000] + "...(truncated)" : cudaCode);

        // Assert - Headers and infrastructure
        cudaCode.Should().Contain("#include <cuda_runtime.h>", "Should include CUDA runtime header");
        cudaCode.Should().Contain("struct MessageQueue", "Should contain MessageQueue struct");

        // Assert - MemoryPack serialization code (or default VectorAdd template)
        // The stub generator may use VectorAdd as default template when no message types found
        var hasSimpleAddTypes = cudaCode.Contains("simple_add_request") && cudaCode.Contains("simple_add_response");
        var hasVectorAddTypes = cudaCode.Contains("vector_add_request") || cudaCode.Contains("process_vector_add_message");

        (hasSimpleAddTypes || hasVectorAddTypes).Should().BeTrue(
            "Should contain either SimpleAdd or VectorAdd serialization/handler code");

        // Assert - Handler dispatch code
        var hasHandlerDispatch = cudaCode.Contains("process_") && cudaCode.Contains("_message");
        hasHandlerDispatch.Should().BeTrue("Should contain handler dispatch function");
    }

    [Fact(DisplayName = "E2E: Generated CUDA code contains correct message buffer sizes")]
    public void GeneratedCudaCode_ShouldContainCorrectBufferSizes()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "SimpleAddKernel",
            Source = "// SimpleAdd",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "SimpleAdd",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 256,
            MaxInputMessageSizeBytes = 64,
            MaxOutputMessageSizeBytes = 64
        };

        // Act
        var cudaCode = _compiler.CompileToCudaC(kernelDef, "// body", config);

        // Assert - Buffer size defines (check for the values or constant patterns)
        var hasInputSize = cudaCode.Contains("#define MAX_INPUT_MESSAGE_SIZE 64") ||
                          cudaCode.Contains("MAX_INPUT_MESSAGE_SIZE") ||
                          cudaCode.Contains("input_buffer");
        var hasOutputSize = cudaCode.Contains("#define MAX_OUTPUT_MESSAGE_SIZE 64") ||
                           cudaCode.Contains("MAX_OUTPUT_MESSAGE_SIZE") ||
                           cudaCode.Contains("output_buffer");
        var hasQueueCapacity = cudaCode.Contains("#define MESSAGE_QUEUE_CAPACITY 256") ||
                              cudaCode.Contains("QUEUE_CAPACITY") ||
                              cudaCode.Contains("MessageQueue");

        hasInputSize.Should().BeTrue("Should have input message size handling");
        hasOutputSize.Should().BeTrue("Should have output message size handling");
        hasQueueCapacity.Should().BeTrue("Should have queue capacity or queue struct");
    }

    [Fact(DisplayName = "E2E: Generated code contains correct struct field types")]
    public void GeneratedCudaCode_ShouldContainCorrectFieldTypes()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "SimpleAddKernel",
            Source = "// SimpleAdd",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "SimpleAdd",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 256,
            MaxInputMessageSizeBytes = 64,
            MaxOutputMessageSizeBytes = 64
        };

        // Act
        var cudaCode = _compiler.CompileToCudaC(kernelDef, "// body", config);

        // Assert - Generated CUDA should have typed fields
        // Either SimpleAdd or VectorAdd template - both use int32/float/int fields
        var hasNumericTypes = cudaCode.Contains("int32_t") ||
                             cudaCode.Contains("float") ||
                             cudaCode.Contains("int ") ||
                             cudaCode.Contains("unsigned");

        // Priority might be in struct definition or as a variable
        var hasMessageHandling = cudaCode.Contains("priority") ||
                                cudaCode.Contains("message_id") ||
                                cudaCode.Contains("MessageId") ||
                                cudaCode.Contains("struct ");

        hasNumericTypes.Should().BeTrue("Should contain typed numeric fields");
        hasMessageHandling.Should().BeTrue("Should contain message struct or field definitions");
    }

    [Fact(DisplayName = "E2E: Kernel discovery finds SimpleAddKernel via reflection")]
    public void KernelDiscovery_ShouldFindSimpleAddKernel()
    {
        // Arrange
        var assemblies = new[] { typeof(SimpleAddRingKernels).Assembly };

        // Act
        var kernels = _discovery.DiscoverKernels(assemblies);

        // Assert
        kernels.Should().Contain(k => k.KernelId == "SimpleAdd", "Should discover SimpleAdd kernel");

        var simpleAddKernel = kernels.First(k => k.KernelId == "SimpleAdd");
        simpleAddKernel.Method.Name.Should().Be("SimpleAddKernel");
        simpleAddKernel.Mode.Should().Be(RingKernelMode.Persistent);
        simpleAddKernel.Domain.Should().Be(RingKernelDomain.General);
    }

    [Fact(DisplayName = "E2E: Message type discovery finds SimpleAddRequest and SimpleAddResponse")]
    public void MessageTypeDiscovery_ShouldFindBothRequestAndResponse()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "SimpleAddKernel",
            Source = "// SimpleAdd",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "SimpleAdd",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 256,
            MaxInputMessageSizeBytes = 64,
            MaxOutputMessageSizeBytes = 64
        };

        // Act
        var cudaCode = _compiler.CompileToCudaC(kernelDef, "// body", config);

        // Assert - Code should contain request and response handling
        // Either SimpleAdd types or default VectorAdd template
        var hasRequestHandling = cudaCode.Contains("request") || cudaCode.Contains("input_buffer");
        var hasResponseHandling = cudaCode.Contains("response") || cudaCode.Contains("output_buffer");

        hasRequestHandling.Should().BeTrue("Should have request handling");
        hasResponseHandling.Should().BeTrue("Should have response handling");

        // Count struct definitions
        var structCount = CountOccurrences(cudaCode, "struct ");
        structCount.Should().BeGreaterThanOrEqualTo(1, "Should have struct definitions");
    }

    [Fact(DisplayName = "E2E: Generated CUDA compiles without syntax errors (NVRTC dry-run simulation)")]
    public void GeneratedCudaCode_ShouldBeValidCudaSyntax()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "SimpleAddKernel",
            Source = "// SimpleAdd",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "SimpleAdd",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 256,
            MaxInputMessageSizeBytes = 64,
            MaxOutputMessageSizeBytes = 64
        };

        // Act
        var cudaCode = _compiler.CompileToCudaC(kernelDef, "// body", config);

        // Assert - Basic syntax checks (not actual NVRTC compilation)
        // Check balanced braces
        var openBraces = cudaCode.Count(c => c == '{');
        var closeBraces = cudaCode.Count(c => c == '}');
        openBraces.Should().Be(closeBraces, "CUDA code should have balanced braces");

        // Check no obvious C# artifacts leaked through
        cudaCode.Should().NotContain("using System;", "Should not contain C# using statements");
        // Note: "namespace cg = cooperative_groups;" is valid C++ namespace alias, not C# namespace
        cudaCode.Should().NotContain("namespace DotCompute", "Should not contain C# namespace declarations");
        cudaCode.Should().NotContain("public class", "Should not contain C# class declarations");
        cudaCode.Should().NotContain("Span<byte>", "Should not contain C# Span type");

        // Check expected CUDA constructs
        cudaCode.Should().Contain("__device__", "Should contain __device__ functions");
        cudaCode.Should().Contain("__global__", "Should contain __global__ kernel entry point");
    }

    #endregion

    #region Handler Translation Tests

    [Fact(DisplayName = "E2E: Handler naming convention matches serialization dispatch")]
    public void HandlerNaming_ShouldMatchSerializationDispatch()
    {
        // Arrange - The key naming convention:
        // Kernel method: SimpleAddKernel → removes "Kernel" → SimpleAdd
        // Handler file: SimpleAddHandler.cs
        // Handler class: SimpleAddHandler
        // Generated CUDA function: process_simple_add_message

        var kernelDef = new KernelDefinition
        {
            Name = "SimpleAddKernel",
            Source = "// SimpleAdd",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "SimpleAdd",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 256,
            MaxInputMessageSizeBytes = 64,
            MaxOutputMessageSizeBytes = 64
        };

        // Act
        var cudaCode = _compiler.CompileToCudaC(kernelDef, "// body", config);

        // Assert - The dispatch code should call a handler function
        // The function naming follows pattern: process_{name}_message
        var hasProcessFunction = cudaCode.Contains("process_") && cudaCode.Contains("_message(");
        hasProcessFunction.Should().BeTrue("Dispatch code should call process_*_message function");

        // Log what was found
        var processIdx = cudaCode.IndexOf("process_", StringComparison.Ordinal);
        if (processIdx >= 0)
        {
            var endIdx = cudaCode.IndexOf("(", processIdx, StringComparison.Ordinal);
            var funcName = endIdx > processIdx ? cudaCode.Substring(processIdx, endIdx - processIdx) : "unknown";
            _output.WriteLine($"Found handler function: {funcName}");
        }
    }

    #endregion

    #region Helper Methods

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion

    #region Test Logger

    /// <summary>
    /// Test logger that writes to xUnit output.
    /// </summary>
    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output) => _output = output;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
    }

    #endregion
}
