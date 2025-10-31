// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Comprehensive tests for Ring Kernel source generation.
/// Tests all [RingKernel] attribute configurations and code generation patterns.
/// </summary>
public sealed class RingKernelGeneratorTests
{
    [Fact]
    public void Generator_SimpleRingKernel_GeneratesRegistry()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class StreamProcessing
                {
                    [RingKernel(KernelId = "DataPipeline", Capacity = 1024)]
                    public static void ProcessStream(ReadOnlySpan<float> input, Span<float> output)
                    {
                        int idx = 0; // Ring Kernel processes messages
                        if (idx < output.Length)
                        {
                            output[idx] = input[idx] * 2.0f;
                        }
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("GetAvailableRingKernels", registryContent);
        Assert.Contains("GetRingKernelInfo", registryContent);
        Assert.Contains("DataPipeline", registryContent);
    }

    [Fact]
    public void Generator_RingKernel_GeneratesWrapperWithLifecycleMethods()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(KernelId = "TestKernel")]
                    public static void Process(ReadOnlySpan<int> data)
                    {
                        // Process implementation
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        System.Console.WriteLine($"\nGenerated {generatedSources.Length} sources:");
        foreach (var src in generatedSources)
        {
            System.Console.WriteLine($"  - {src.HintName}");
        }

        System.Console.WriteLine($"\nError diagnostics: {diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)}");
        foreach (var d in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(5))
        {
            System.Console.WriteLine($"  Error: {d.Id} - {d.GetMessage()}");
        }

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var wrapperSource = generatedSources.FirstOrDefault(s => s.HintName.Contains("Process_RingKernelWrapper"));
        System.Console.WriteLine($"\nWrapper search: HintName='{wrapperSource.HintName}', SourceText null={wrapperSource.SourceText == null}");

        Assert.False(string.IsNullOrEmpty(wrapperSource.HintName), "Wrapper source was not generated");

        var wrapperContent = wrapperSource.SourceText.ToString();
        Assert.Contains("LaunchAsync", wrapperContent);
        Assert.Contains("ActivateAsync", wrapperContent);
        Assert.Contains("DeactivateAsync", wrapperContent);
        Assert.Contains("TerminateAsync", wrapperContent);
        Assert.Contains("GetStatusAsync", wrapperContent);
        Assert.Contains("GetMetricsAsync", wrapperContent);
    }

    [Fact]
    public void Generator_RingKernel_GeneratesRuntimeFactory()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var factorySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRuntimeFactory"));
        Assert.NotNull(factorySource);

        var factoryContent = factorySource.SourceText.ToString();
        Assert.Contains("CreateRuntime", factoryContent);
        Assert.Contains("CreateCpuRuntime", factoryContent);
        Assert.Contains("CreateCudaRuntime", factoryContent);
        Assert.Contains("CreateOpenCLRuntime", factoryContent);
    }

    [Fact]
    public void Generator_RingKernelWithCustomCapacity_GeneratesCorrectMetadata()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(Capacity = 2048, InputQueueSize = 512, OutputQueueSize = 512)]
                    public static void Process(Span<double> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("Capacity = 2048", registryContent);
        Assert.Contains("InputQueueSize = 512", registryContent);
        Assert.Contains("OutputQueueSize = 512", registryContent);
    }

    [Fact]
    public void Generator_RingKernelPersistentMode_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using DotCompute.Abstractions.Enums;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(Mode = RingKernelMode.Persistent)]
                    public static void Process(Span<int> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("Mode = \"Persistent\"", registryContent);
    }

    [Fact]
    public void Generator_RingKernelEventDrivenMode_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using DotCompute.Abstractions.Enums;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(Mode = RingKernelMode.EventDriven)]
                    public static void Process(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("Mode = \"EventDriven\"", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithMessagingStrategy_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using DotCompute.Abstractions.Enums;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(MessagingStrategy = MessagingStrategy.AtomicQueue)]
                    public static void Process(Span<int> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("MessagingStrategy = \"AtomicQueue\"", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithDomain_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using DotCompute.Abstractions.Enums;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(Domain = ComputeDomain.VideoProcessing)]
                    public static void Process(Span<byte> frameData)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("Domain = \"VideoProcessing\"", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithSharedMemory_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(UseSharedMemory = true, SharedMemorySize = 4096)]
                    public static void Process(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("UseSharedMemory = true", registryContent);
        Assert.Contains("SharedMemorySize = 4096", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithGridAndBlockDimensions_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(GridDimensions = new[] { 16, 16, 1 }, BlockDimensions = new[] { 256, 1, 1 })]
                    public static void Process(Span<int> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Debug output
        System.Console.WriteLine($"Generated {generatedSources.Length} sources:");
        foreach (var src in generatedSources)
        {
            System.Console.WriteLine($"  - {src.HintName}");
        }

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        System.Console.WriteLine($"Errors: {errors.Count}");
        foreach (var err in errors.Take(5))
        {
            System.Console.WriteLine($"  {err.Id}: {err.GetMessage()}");
        }

        Assert.Empty(errors);

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.False(string.IsNullOrEmpty(registrySource.HintName), "Registry source was not generated");

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("GridDimensions", registryContent);
        Assert.Contains("BlockDimensions", registryContent);
    }

    [Fact]
    public void Generator_MultipleRingKernels_GeneratesAllRegistrations()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(KernelId = "Kernel1")]
                    public static void Process1(Span<float> data) { }

                    [RingKernel(KernelId = "Kernel2")]
                    public static void Process2(Span<double> data) { }

                    [RingKernel(KernelId = "Kernel3")]
                    public static void Process3(Span<int> data) { }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("Kernel1", registryContent);
        Assert.Contains("Kernel2", registryContent);
        Assert.Contains("Kernel3", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithAutoGeneratedId_GeneratesValidId()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void MyProcessor(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        // Auto-generated ID format: TypeName_MethodName
        Assert.Contains("Processors_MyProcessor", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWrapperImplementsIDisposable()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<int> data) { }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var wrapperSource = generatedSources.FirstOrDefault(s => s.HintName.Contains("Process_RingKernelWrapper"));
        Assert.NotNull(wrapperSource);

        var wrapperContent = wrapperSource.SourceText.ToString();
        Assert.Contains("IDisposable", wrapperContent);
        Assert.Contains("IAsyncDisposable", wrapperContent);
        Assert.Contains("void Dispose()", wrapperContent);
        Assert.Contains("ValueTask DisposeAsync()", wrapperContent);
    }

    [Fact]
    public void Generator_RingKernelWrapperHasStateTracking()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<int> data) { }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var wrapperSource = generatedSources.FirstOrDefault(s => s.HintName.Contains("Process_RingKernelWrapper"));
        Assert.NotNull(wrapperSource);

        var wrapperContent = wrapperSource.SourceText.ToString();
        Assert.Contains("_isLaunched", wrapperContent);
        Assert.Contains("_isActive", wrapperContent);
        Assert.Contains("_disposed", wrapperContent);
    }

    [Fact]
    public void Generator_RingKernelWrapperHasValidation()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<int> data) { }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var wrapperSource = generatedSources.FirstOrDefault(s => s.HintName.Contains("Process_RingKernelWrapper"));
        Assert.NotNull(wrapperSource);

        var wrapperContent = wrapperSource.SourceText.ToString();
        Assert.Contains("ObjectDisposedException.ThrowIf", wrapperContent);
        Assert.Contains("Ring Kernel is already launched", wrapperContent);
        Assert.Contains("Ring Kernel must be launched before activation", wrapperContent);
    }

    [Fact]
    public void Generator_RingKernelWithMultipleParameters_GeneratesCorrectSignature()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(ReadOnlySpan<float> input, Span<float> output, int multiplier)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("ParameterCount = 3", registryContent);
    }

    [Fact]
    public void Generator_RingKernelInNestedClass_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class OuterClass
                {
                    public static class InnerProcessors
                    {
                        [RingKernel]
                        public static void Process(Span<int> data) { }
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("TestApp.OuterClass.InnerProcessors", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithBackendSpecification_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using DotCompute.Abstractions.Enums;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(Backends = KernelBackends.CUDA | KernelBackends.OpenCL)]
                    public static void Process(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("CUDA", registryContent);
        Assert.Contains("OpenCL", registryContent);
    }

    [Fact]
    public void Generator_RingKernelWithComplexConfiguration_GeneratesAllMetadata()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using DotCompute.Abstractions.Enums;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(
                        KernelId = "AdvancedProcessor",
                        Capacity = 4096,
                        InputQueueSize = 1024,
                        OutputQueueSize = 1024,
                        Mode = RingKernelMode.Persistent,
                        MessagingStrategy = MessagingStrategy.SharedMemory,
                        Domain = ComputeDomain.MachineLearning,
                        UseSharedMemory = true,
                        SharedMemorySize = 8192,
                        IsParallel = true,
                        VectorSize = 16)]
                    public static void AdvancedProcess(Span<double> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Debug output
        System.Console.WriteLine($"Generated {generatedSources.Length} sources:");
        foreach (var src in generatedSources)
        {
            System.Console.WriteLine($"  - {src.HintName}");
        }

        var allDiags = diagnostics.ToList();
        System.Console.WriteLine($"All Diagnostics: {allDiags.Count}");
        foreach (var diag in allDiags.Take(15))
        {
            System.Console.WriteLine($"  {diag.Severity} {diag.Id}: {diag.GetMessage()}");
        }

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        System.Console.WriteLine($"Errors: {errors.Count}");
        foreach (var err in errors.Take(10))
        {
            System.Console.WriteLine($"  {err.Id}: {err.GetMessage()}");
        }

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.False(string.IsNullOrEmpty(registrySource.HintName), "Registry source was not generated");

        var registryContent = registrySource.SourceText.ToString();
        Assert.Contains("AdvancedProcessor", registryContent);
        Assert.Contains("Capacity = 4096", registryContent);
        Assert.Contains("InputQueueSize = 1024", registryContent);
        Assert.Contains("OutputQueueSize = 1024", registryContent);
        Assert.Contains("Mode = \"Persistent\"", registryContent);
        Assert.Contains("MessagingStrategy = \"SharedMemory\"", registryContent);
        Assert.Contains("Domain = \"MachineLearning\"", registryContent);
        Assert.Contains("UseSharedMemory = true", registryContent);
        Assert.Contains("SharedMemorySize = 8192", registryContent);
        Assert.Contains("IsParallel = true", registryContent);
        Assert.Contains("VectorSize = 16", registryContent);
    }

    [Fact]
    public void Generator_RingKernelAndStandardKernelTogether_GeneratesBothRegistries()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [Kernel]
                    public static void StandardProcess(Span<float> data)
                    {
                        int idx = Kernel.ThreadId.X;
                        if (idx < data.Length)
                        {
                            data[idx] *= 2.0f;
                        }
                    }

                    [RingKernel]
                    public static void RingProcess(Span<float> data)
                    {
                        // Ring kernel implementation
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var kernelRegistry = generatedSources.FirstOrDefault(s => s.HintName.Contains("KernelRegistry") && !s.HintName.Contains("Ring"));
        var ringKernelRegistry = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));

        Assert.NotNull(kernelRegistry);
        Assert.NotNull(ringKernelRegistry);

        var kernelContent = kernelRegistry.SourceText.ToString();
        var ringContent = ringKernelRegistry.SourceText.ToString();

        Assert.Contains("StandardProcess", kernelContent);
        Assert.Contains("RingProcess", ringContent);
    }

    [Fact]
    public void Generator_RingKernelWithReadOnlySpan_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(ReadOnlySpan<byte> input, Span<byte> output)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Generator_RingKernelRuntimeFactory_HasAllBackendMethods()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<int> data) { }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var factorySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRuntimeFactory"));
        Assert.NotNull(factorySource);

        var factoryContent = factorySource.SourceText.ToString();
        Assert.Contains("public static IRingKernelRuntime CreateRuntime", factoryContent);
        Assert.Contains("private static IRingKernelRuntime CreateCpuRuntime", factoryContent);
        Assert.Contains("private static IRingKernelRuntime CreateCudaRuntime", factoryContent);
        Assert.Contains("private static IRingKernelRuntime CreateOpenCLRuntime", factoryContent);
        Assert.Contains("private static IRingKernelRuntime CreateMetalRuntime", factoryContent);
    }
}
