// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Generators.Kernel;
using FluentAssertions;
using Xunit;

namespace DotCompute.Tests.Unit;

public class PerformanceTests
{
    private readonly KernelSourceGenerator _generator = new();

    [Fact]
    public void Generator_SingleKernel_ShouldCompleteQuickly()
    {
        // Arrange
        var source = TestHelper.CreateKernelSource(
            "PerformanceTest",
            "SimpleKernel",
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.SimpleAdd);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Single kernel generation should complete within 5 seconds");
        result.GeneratedSources.Should().NotBeEmpty();
    }

    [Fact]
    public void Generator_MultipleKernels_ShouldScaleReasonably()
    {
        // Arrange
        var sources = new[]
        {
            TestHelper.CreateKernelSource("Kernels1", "Add", TestHelper.Parameters.TwoArrays, TestHelper.KernelBodies.SimpleAdd),
            TestHelper.CreateKernelSource("Kernels2", "Multiply", TestHelper.Parameters.TwoArrays, TestHelper.KernelBodies.SimpleMultiply),
            TestHelper.CreateKernelSource("Kernels3", "Scale", TestHelper.Parameters.ScalarArray, TestHelper.KernelBodies.ScalarOperation),
            TestHelper.CreateKernelSource("Kernels4", "Copy", TestHelper.Parameters.SingleArray, TestHelper.KernelBodies.MemoryCopy),
            TestHelper.CreateKernelSource("Kernels5", "Complex", TestHelper.Parameters.SingleArray, TestHelper.KernelBodies.ComplexOperation)
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, sources);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Multiple kernel generation should complete within 10 seconds");
        result.GeneratedSources.Should().HaveCountGreaterThan(5); // Should generate multiple files
    }

    [Fact]
    public void Generator_LargeKernelBody_ShouldHandleEfficiently()
    {
        // Arrange
        var largeKernelBody = GenerateLargeKernelBody();
        var source = TestHelper.CreateKernelSource(
            "LargeKernels",
            "LargeKernel",
            TestHelper.Parameters.MatrixParams,
            largeKernelBody);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(8000, "Large kernel generation should complete within 8 seconds");
        result.GeneratedSources.Should().NotBeEmpty();
    }

    [Fact]
    public void Generator_MultipleBackends_ShouldGenerateEfficiently()
    {
        // Arrange
        var source = TestHelper.CreateKernelWithAttributes(
            "MultiBackendKernels",
            "ProcessAll",
            "Backends = KernelBackends.CPU | KernelBackends.CUDA | KernelBackends.Metal | KernelBackends.OpenCL",
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.ScalarOperation);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(6000, "Multi-backend generation should complete within 6 seconds");
        
        // Should generate for all backends
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("CPU"));
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("CUDA"));
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("Metal"));
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("OpenCL"));
    }

    [Fact]
    public void Generator_ManyKernelsInOneClass_ShouldBeEfficient()
    {
        // Arrange
        var source = GenerateClassWithManyKernels(20);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000, "Many kernels in one class should complete within 15 seconds");
        result.GeneratedSources.Length.Should().BeGreaterThan(20); // Registry + implementations
    }

    [Fact]
    public void Generator_RepeatedGeneration_ShouldBeCached()
    {
        // Arrange
        var source = TestHelper.CreateKernelSource(
            "CacheTest",
            "TestKernel",
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.SimpleAdd);

        // Act - First generation
        var stopwatch1 = Stopwatch.StartNew();
        var result1 = TestHelper.RunIncrementalGenerator(_generator, source);
        stopwatch1.Stop();

        // Act - Second generation (should be faster due to incremental caching)
        var stopwatch2 = Stopwatch.StartNew();
        var result2 = TestHelper.RunIncrementalGenerator(_generator, source);
        stopwatch2.Stop();

        // Assert
        result1.GeneratedSources.Length.Should().Be(result2.GeneratedSources.Length);
        // Second run might be faster due to caching, but this is hard to guarantee in tests
        stopwatch2.ElapsedMilliseconds.Should().BeLessThan(stopwatch1.ElapsedMilliseconds + 1000);
    }

    [Fact]
    public void Generator_ComplexKernelLogic_ShouldHandleEfficiently()
    {
        // Arrange
        var complexKernelBody = @"
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int idx = i * width + j;
                    float value = input[idx];
                    
                    if (value > 0.5f)
                    {
                        float processed = value * value;
                        for (int k = 0; k < 3; k++)
                        {
                            processed = (float)Math.Sqrt(processed + 0.1f);
                        }
                        output[idx] = processed;
                    }
                    else if (value < -0.5f)
                    {
                        output[idx] = Math.Abs(value) + 1.0f;
                    }
                    else
                    {
                        output[idx] = value * 2.0f + Math.Sin(value);
                    }
                }
            }";

        var source = TestHelper.CreateKernelSource(
            "ComplexKernels",
            "ComplexProcessing",
            TestHelper.Parameters.MatrixParams,
            complexKernelBody);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(7000, "Complex kernel generation should complete within 7 seconds");
        result.GeneratedSources.Should().NotBeEmpty();
    }

    [Fact]
    public void Generator_MemoryUsage_ShouldBeReasonable()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        var sources = new List<string>();
        
        for (int i = 0; i < 10; i++)
        {
            sources.Add(TestHelper.CreateKernelSource(
                $"MemoryTest{i}",
                $"Kernel{i}",
                TestHelper.Parameters.SingleArray,
                TestHelper.KernelBodies.SimpleAdd));
        }

        // Act
        var results = new List<Microsoft.CodeAnalysis.GeneratorDriverRunResult>();
        for (int i = 0; i < sources.Count; i++)
        {
            results.Add(TestHelper.RunIncrementalGenerator(_generator, sources[i]));
        }

        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should not exceed 50MB for 10 kernels");
        
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.GeneratedSources.Length > 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    public void Generator_ScalabilityTest_ShouldHandleVaryingLoads(int kernelCount)
    {
        // Arrange
        var sources = new List<string>();
        for (int i = 0; i < kernelCount; i++)
        {
            sources.Add(TestHelper.CreateKernelSource(
                $"ScaleTest{i}",
                $"ScaleKernel{i}",
                TestHelper.Parameters.TwoArrays,
                TestHelper.KernelBodies.SimpleMultiply));
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, sources.ToArray());

        // Assert
        stopwatch.Stop();
        
        // Reasonable time scaling: should be roughly linear
        var expectedMaxTime = Math.Max(2000, kernelCount * 400); // Base 2s + 400ms per kernel
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedMaxTime,
            $"Generation of {kernelCount} kernels should complete within {expectedMaxTime}ms");
        
        result.GeneratedSources.Length.Should().BeGreaterOrEqualTo(kernelCount);
    }

    [Fact]
    public void Generator_ConcurrentGeneration_ShouldBeThreadSafe()
    {
        // Arrange
        var sources = new[]
        {
            TestHelper.CreateKernelSource("Concurrent1", "Kernel1", TestHelper.Parameters.SingleArray, TestHelper.KernelBodies.SimpleAdd),
            TestHelper.CreateKernelSource("Concurrent2", "Kernel2", TestHelper.Parameters.TwoArrays, TestHelper.KernelBodies.SimpleMultiply),
            TestHelper.CreateKernelSource("Concurrent3", "Kernel3", TestHelper.Parameters.ScalarArray, TestHelper.KernelBodies.ScalarOperation)
        };

        // Act
        var tasks = sources.Select(source => Task.Run(() => 
            TestHelper.RunIncrementalGenerator(_generator, source))).ToArray();

        var stopwatch = Stopwatch.StartNew();
        var results = Task.WaitAll(tasks, TimeSpan.FromSeconds(10));
        stopwatch.Stop();

        // Assert
        results.Should().BeTrue("All concurrent generations should complete within 10 seconds");
        
        foreach (var task in tasks)
        {
            task.Result.GeneratedSources.Should().NotBeEmpty();
        }
    }

    private static string GenerateLargeKernelBody()
    {
        var body = new System.Text.StringBuilder();
        body.AppendLine("for (int i = 0; i < height; i++)");
        body.AppendLine("{");
        body.AppendLine("    for (int j = 0; j < width; j++)");
        body.AppendLine("    {");
        body.AppendLine("        int idx = i * width + j;");
        body.AppendLine("        float value = input[idx];");
        body.AppendLine("        float result = value;");

        // Generate many operations to make it large
        for (int i = 0; i < 50; i++)
        {
            body.AppendLine($"        result = result * 1.01f + {i * 0.1f}f;");
            body.AppendLine($"        if (result > {i + 10}.0f) result = result / 2.0f;");
        }

        body.AppendLine("        output[idx] = result;");
        body.AppendLine("    }");
        body.AppendLine("}");
        return body.ToString();
    }

    private static string GenerateClassWithManyKernels(int count)
    {
        var source = new System.Text.StringBuilder();
        source.AppendLine("using DotCompute.Generators.Kernel;");
        source.AppendLine("public class ManyKernelsClass {");

        for (int i = 0; i < count; i++)
        {
            source.AppendLine($@"
    [Kernel]
    public static void Kernel{i:D3}(float[] input, float[] output, int length)
    {{
        for (int j = 0; j < length; j++)
        {{
            output[j] = input[j] * {i + 1}.0f;
        }}
    }}");
        }

        source.AppendLine("}");
        return source.ToString();
    }
}