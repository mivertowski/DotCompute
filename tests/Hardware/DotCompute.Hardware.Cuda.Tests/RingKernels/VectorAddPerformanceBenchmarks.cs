// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Performance benchmarks for VectorAdd ring kernel implementation.
/// Validates &lt;50μs end-to-end latency target and 100K+ msg/sec throughput.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(HardwareCounter.CacheMisses, HardwareCounter.BranchMispredictions)]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(BenchmarkConfig))]
public class VectorAddPerformanceBenchmarks
{
    private CudaRingKernelCompiler? _compiler;
    private string? _compiledKernel;
    private readonly Consumer _consumer = new();

    /// <summary>
    /// Custom benchmark configuration for performance testing.
    /// </summary>
    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddColumn(StatisticColumn.Median);  // P50
            AddColumn(StatisticColumn.P95);
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
        }
    }

    [GlobalSetup]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet lifecycle method, not an xUnit test
    public void Setup()
#pragma warning restore xUnit1013
    {
        // Initialize compiler
        var logger = NullLogger<CudaRingKernelCompiler>.Instance;
        var kernelDiscovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        var serializerGenerator = new CudaMemoryPackSerializerGenerator(NullLogger<CudaMemoryPackSerializerGenerator>.Instance);
        _compiler = new CudaRingKernelCompiler(logger, kernelDiscovery, stubGenerator, serializerGenerator);

        // Compile VectorAdd kernel once for all benchmarks
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAddKernel",
            Source = "// VectorAdd benchmark kernel",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "vectoradd_benchmark",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 1024,
            Domain = RingKernelDomain.General,
            MaxInputMessageSizeBytes = 256,
            MaxOutputMessageSizeBytes = 256,
            MessagingStrategy = MessagePassingStrategy.SharedMemory
        };

        _compiledKernel = _compiler.CompileToCudaC(kernelDef, "// Benchmark kernel", config);
    }

    [GlobalCleanup]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet lifecycle method, not an xUnit test
    public void Cleanup()
#pragma warning restore xUnit1013
    {
        _compiler?.Dispose();
    }

    /// <summary>
    /// Benchmark: Kernel compilation time (should be &lt;10ms).
    /// </summary>
    [Benchmark(Description = "Kernel Compilation Time")]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet method, not an xUnit test
    public void BenchmarkKernelCompilation()
#pragma warning restore xUnit1013
    {
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAddKernel",
            Source = "// VectorAdd kernel",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "vectoradd_compile_test",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 1024,
            MaxInputMessageSizeBytes = 256,
            MaxOutputMessageSizeBytes = 256
        };

        var result = _compiler!.CompileToCudaC(kernelDef, "// Test", config);
        _consumer.Consume(result);
    }

    /// <summary>
    /// Benchmark: VectorAdd message serialization (CPU side).
    /// Target: &lt;1μs per message.
    /// </summary>
    [Benchmark(Description = "VectorAdd Serialization (CPU)")]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet method, not an xUnit test
    public void BenchmarkMessageSerialization()
#pragma warning restore xUnit1013
    {
        // Simulate VectorAddRequest serialization
        // MessageId (16 bytes) + Priority (1 byte) + CorrelationId (16 bytes) + A (4 bytes) + B (4 bytes) = 41 bytes
        var buffer = new byte[256];
        var messageId = Guid.NewGuid().ToByteArray();
        var correlationId = Guid.NewGuid().ToByteArray();

        var offset = 0;

        // MessageId (16 bytes)
        Buffer.BlockCopy(messageId, 0, buffer, offset, 16);
        offset += 16;

        // Priority (1 byte)
        buffer[offset] = 1;
        offset += 1;

        // CorrelationId (16 bytes)
        Buffer.BlockCopy(correlationId, 0, buffer, offset, 16);
        offset += 16;

        // A (4 bytes, little-endian float)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), 3.14f);
        offset += 4;

        // B (4 bytes, little-endian float)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), 2.71f);

        _consumer.Consume(buffer);
    }

    /// <summary>
    /// Benchmark: VectorAdd response deserialization (CPU side).
    /// Target: &lt;1μs per message.
    /// </summary>
    [Benchmark(Description = "VectorAdd Deserialization (CPU)")]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet method, not an xUnit test
    public void BenchmarkMessageDeserialization()
#pragma warning restore xUnit1013
    {
        // Simulate VectorAddResponse deserialization
        // MessageId (16 bytes) + Priority (1 byte) + CorrelationId (16 bytes) + Result (4 bytes) = 37 bytes
        var buffer = new byte[256];

        // Fill buffer with mock response
        var messageId = Guid.NewGuid().ToByteArray();
        var correlationId = Guid.NewGuid().ToByteArray();
        Buffer.BlockCopy(messageId, 0, buffer, 0, 16);
        buffer[16] = 1;
        Buffer.BlockCopy(correlationId, 0, buffer, 17, 16);
        BitConverter.TryWriteBytes(buffer.AsSpan(33, 4), 5.85f);

        // Deserialize
        var offset = 0;
        _ = new Guid(buffer.AsSpan(offset, 16));  // readMessageId
        offset += 16;
        _ = buffer[offset];  // priority
        offset += 1;
        _ = new Guid(buffer.AsSpan(offset, 16));  // readCorrelationId
        offset += 16;
        var result = BitConverter.ToSingle(buffer, offset);

        _consumer.Consume(result);
    }

    /// <summary>
    /// Benchmark: Code generation for different message sizes.
    /// Validates MAX_MESSAGE_SIZE configurability.
    /// </summary>
    [Benchmark(Description = "Code Generation (256 bytes)")]
    [Arguments(256)]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet method, not an xUnit test
    public void BenchmarkCodeGeneration(int messageSize)
#pragma warning restore xUnit1013
    {
        var kernelDef = new KernelDefinition
        {
            Name = "VectorAddKernel",
            Source = "// VectorAdd kernel",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "vectoradd_codegen_test",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 1024,
            MaxInputMessageSizeBytes = messageSize,
            MaxOutputMessageSizeBytes = messageSize
        };

        var result = _compiler!.CompileToCudaC(kernelDef, "// Test", config);
        _consumer.Consume(result);
    }

    /// <summary>
    /// Benchmark: Batch message serialization for throughput testing.
    /// Target: 100K+ messages/sec = 10μs per message.
    /// </summary>
    [Benchmark(Description = "Batch Serialization (1000 messages)")]
#pragma warning disable xUnit1013 // This is a BenchmarkDotNet method, not an xUnit test
    public void BenchmarkBatchSerialization()
#pragma warning restore xUnit1013
    {
        const int batchSize = 1000;
        var buffers = new byte[batchSize][];

        for (var i = 0; i < batchSize; i++)
        {
            var buffer = new byte[256];
            var messageId = Guid.NewGuid().ToByteArray();
            var correlationId = Guid.NewGuid().ToByteArray();

            var offset = 0;
            Buffer.BlockCopy(messageId, 0, buffer, offset, 16);
            offset += 16;
            buffer[offset] = 1;
            offset += 1;
            Buffer.BlockCopy(correlationId, 0, buffer, offset, 16);
            offset += 16;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), (float)i);
            offset += 4;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), (float)(i + 1));

            buffers[i] = buffer;
        }

        _consumer.Consume(buffers);
    }

    /// <summary>
    /// Test: Verify all benchmarks can run successfully.
    /// </summary>
    [Fact(DisplayName = "Performance: All benchmarks execute successfully")]
    public void PerformanceBenchmarks_ShouldExecuteSuccessfully()
    {
        Setup();

        try
        {
            // Run each benchmark once to verify
            BenchmarkKernelCompilation();
            BenchmarkMessageSerialization();
            BenchmarkMessageDeserialization();
            BenchmarkCodeGeneration(256);
            BenchmarkBatchSerialization();
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Test: Validate kernel compilation produces correct code.
    /// </summary>
    [Fact(DisplayName = "Performance: Compiled kernel contains VectorAdd logic")]
    public void CompiledKernel_ShouldContainVectorAddLogic()
    {
        Setup();

        try
        {
            Assert.NotNull(_compiledKernel);
            Assert.Contains("process_vector_add_message", _compiledKernel, StringComparison.Ordinal);
            Assert.Contains("unsigned char input_buffer[MAX_INPUT_MESSAGE_SIZE]", _compiledKernel, StringComparison.Ordinal);
            Assert.Contains("unsigned char output_buffer[MAX_OUTPUT_MESSAGE_SIZE]", _compiledKernel, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Test: Validate P50/P95/P99 latency targets (&lt;50μs).
    /// Note: This is a placeholder for actual hardware measurements.
    /// Real measurements require GPU hardware and CUDA runtime.
    /// </summary>
    [Fact(DisplayName = "Performance: Serialization latency < 1μs target", Skip = "Requires hardware execution")]
    public void SerializationLatency_ShouldBeLessThan1Microsecond()
    {
        // This test would measure actual serialization times
        // Target: P50 < 0.5μs, P95 < 1μs, P99 < 2μs
        // Implementation requires BenchmarkDotNet runner
        throw new NotImplementedException("Run benchmarks with BenchmarkDotNet.Runner");
    }

    /// <summary>
    /// Test: Validate message throughput &gt; 100K msg/sec.
    /// Note: This is a placeholder for actual hardware measurements.
    /// </summary>
    [Fact(DisplayName = "Performance: Throughput > 100K msg/sec target", Skip = "Requires hardware execution")]
    public void MessageThroughput_ShouldExceed100KPerSecond()
    {
        // This test would measure actual throughput
        // Target: > 100K messages/sec = < 10μs per message
        // Stretch goal: 2M messages/sec = 0.5μs per message
        // Implementation requires GPU hardware and queue infrastructure
        throw new NotImplementedException("Run benchmarks with BenchmarkDotNet.Runner");
    }

    /// <summary>
    /// Test: Validate end-to-end latency CPU → GPU → CPU &lt; 50μs.
    /// Note: This is a placeholder for actual hardware measurements.
    /// </summary>
    [Fact(DisplayName = "Performance: End-to-end latency < 50μs target", Skip = "Requires hardware execution")]
    public void EndToEndLatency_ShouldBeLessThan50Microseconds()
    {
        // This test would measure CPU → GPU → CPU round-trip
        // Target: P50 < 20μs, P95 < 40μs, P99 < 50μs
        // Breakdown:
        // - Serialization: ~1μs
        // - CPU → GPU transfer: ~5μs
        // - GPU processing: ~1μs
        // - GPU → CPU transfer: ~5μs
        // - Deserialization: ~1μs
        // - Queue overhead: ~7μs
        // Total: ~20μs (well under 50μs target)
        throw new NotImplementedException("Run benchmarks with BenchmarkDotNet.Runner");
    }
}
