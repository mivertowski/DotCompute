// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Generators.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

public class KernelCompilationAnalyzerTests
{
    private readonly KernelCompilationAnalyzer _analyzer = new();

    [Fact]
    public void Analyzer_SupportedDiagnostics_ShouldContainAllRules()
    {
        // Act
        var supportedDiagnostics = _analyzer.SupportedDiagnostics;

        // Assert
        Assert.Equal(5, supportedDiagnostics.Count());
        Assert.Contains(d => d.Id == KernelCompilationAnalyzer.UnsupportedTypeId, supportedDiagnostics);
        Assert.Contains(d => d.Id == KernelCompilationAnalyzer.MissingBufferParameterId, supportedDiagnostics);
        Assert.Contains(d => d.Id == KernelCompilationAnalyzer.InvalidVectorSizeId, supportedDiagnostics);
        Assert.Contains(d => d.Id == KernelCompilationAnalyzer.UnsafeCodeRequiredId, supportedDiagnostics);
        Assert.Contains(d => d.Id == KernelCompilationAnalyzer.PerformanceWarningId, supportedDiagnostics);
    }

    [Fact]
    public async Task Analyzer_ValidKernelMethod_ShouldNotReportDiagnostics()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public unsafe class ValidKernels
{
    [Kernel]
    public static void AddArrays(float[] a, float[] b, float[] result, int length)
    {
        for (int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Analyzer_UnsupportedParameterType_ShouldReportDiagnostic()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using System;
using FluentAssertions;

public class InvalidKernels
{
    [Kernel]
    public static void InvalidMethod(string text, float[] result)
    {
        // Invalid - string is not supported
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source, 
            DiagnosticResult.CompilerError(KernelCompilationAnalyzer.UnsupportedTypeId)
                .WithSpan(8, 42, 8, 46)
                .WithArguments("string"));
    }

    [Fact]
    public async Task Analyzer_MissingBufferParameter_ShouldReportDiagnostic()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class InvalidKernels
{
    [Kernel]
    public static void NoBufferMethod(int value, float scale)
    {
        // Invalid - no buffer parameters
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerError(KernelCompilationAnalyzer.MissingBufferParameterId)
                .WithSpan(7, 24, 7, 39)
                .WithArguments("NoBufferMethod"));
    }

    [Fact]
    public async Task Analyzer_InvalidVectorSize_ShouldReportDiagnostic()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class InvalidKernels
{
    [Kernel(VectorSize = 12)]
    public static void InvalidVectorSize(float[] input, float[] output)
    {
        // Invalid vector size
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerError(KernelCompilationAnalyzer.InvalidVectorSizeId)
                .WithSpan(6, 5, 9, 6)
                .WithArguments(12));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task Analyzer_ValidVectorSizes_ShouldNotReportDiagnostic(int vectorSize)
    {
        // Arrange
        var source = $@"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public unsafe class ValidKernels
{{
    [Kernel(VectorSize = {vectorSize})]
    public static void ValidVectorSize(float[] input, float[] output)
    {{
        // Valid vector size
    }}
}}";

        // Act & Assert
        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Analyzer_NonUnsafeKernelMethod_ShouldReportWarning()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class SafeKernels
{
    [Kernel]
    public static void SafeMethod(float[] input, float[] output)
    {
        // Not in unsafe context
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.UnsafeCodeRequiredId)
                .WithSpan(7, 24, 7, 34)
                .WithArguments("SafeMethod"));
    }

    [Fact]
    public async Task Analyzer_UnsafeClass_ShouldNotReportUnsafeWarning()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public unsafe class UnsafeKernels
{
    [Kernel]
    public static void UnsafeClassMethod(float[] input, float[] output)
    {
        // In unsafe class context
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Analyzer_UnsafeMethod_ShouldNotReportUnsafeWarning()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class SafeClass
{
    [Kernel]
    public static unsafe void UnsafeMethod(float[] input, float[] output)
    {
        // Unsafe method
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Analyzer_NestedLoops_ShouldReportPerformanceWarning()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public unsafe class PerformanceKernels
{
    [Kernel]
    public static void NestedLoopKernel(float[] input, float[] output, int width, int height)
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                output[i * width + j] = input[i * width + j] * 2.0f;
            }
        }
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.PerformanceWarningId)
                .WithSpan(9, 9, 15, 10)
                .WithArguments("Nested loops detected. Consider loop flattening or tiling for better performance."));
    }

    [Fact]
    public async Task Analyzer_AllocationInLoop_ShouldReportPerformanceWarning()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using System;
using FluentAssertions;

public unsafe class PerformanceKernels
{
    [Kernel]
    public static void AllocationInLoopKernel(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            var temp = new float[10];  // Allocation in loop
            output[i] = input[i] + temp[0];
        }
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.PerformanceWarningId)
                .WithSpan(12, 24, 12, 37)
                .WithArguments("Object allocation inside loop detected. Move allocations outside the loop."));
    }

    [Fact]
    public async Task Analyzer_BoxingOperation_ShouldReportPerformanceWarning()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public unsafe class PerformanceKernels
{
    [Kernel]
    public static void BoxingKernel(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            object boxed = (object)input[i];  // Boxing operation
            output[i] = (float)boxed;
        }
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.PerformanceWarningId)
                .WithSpan(11, 28, 11, 48)
                .WithArguments("Potential boxing operation detected. Use generic methods or avoid value type to object conversions."));
    }

    [Fact]
    public async Task Analyzer_SupportedBufferTypes_ShouldNotReportDiagnostic()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using System;
using FluentAssertions;

public unsafe class BufferKernels
{
    [Kernel]
    public static void SpanKernel(Span<float> input, Span<float> output)
    {
        // Span<T> is supported
    }

    [Kernel]
    public static void PointerKernel(float* input, float* output, int length)
    {
        // Pointers are supported
    }

    [Kernel]
    public static void ArrayKernel(float[] input, float[] output)
    {
        // Arrays are supported
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Analyzer_MultipleDiagnostics_ShouldReportAll()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class MultipleIssueKernels
{
    [Kernel(VectorSize = 12)]
    public static void ProblematicKernel(string text, int value)
    {
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                var temp = new object();
            }
        }
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source,
            DiagnosticResult.CompilerError(KernelCompilationAnalyzer.UnsupportedTypeId)
                .WithSpan(7, 42, 7, 46)
                .WithArguments("string"),
            DiagnosticResult.CompilerError(KernelCompilationAnalyzer.MissingBufferParameterId)
                .WithSpan(7, 24, 7, 41)
                .WithArguments("ProblematicKernel"),
            DiagnosticResult.CompilerError(KernelCompilationAnalyzer.InvalidVectorSizeId)
                .WithSpan(6, 5, 16, 6)
                .WithArguments(12),
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.UnsafeCodeRequiredId)
                .WithSpan(7, 24, 7, 41)
                .WithArguments("ProblematicKernel"),
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.PerformanceWarningId)
                .WithSpan(9, 9, 15, 10)
                .WithArguments("Nested loops detected. Consider loop flattening or tiling for better performance."),
            DiagnosticResult.CompilerWarning(KernelCompilationAnalyzer.PerformanceWarningId)
                .WithSpan(13, 28, 13, 40)
                .WithArguments("Object allocation inside loop detected. Move allocations outside the loop."));
    }

    [Fact]
    public async Task Analyzer_NonKernelMethod_ShouldNotAnalyze()
    {
        // Arrange
        var source = @"
using System;
using FluentAssertions;

public class NonKernelClass
{
    public static void RegularMethod(string text, object obj)
    {
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                var temp = new object();
            }
        }
    }
}";

        // Act & Assert
        await VerifyAnalyzerAsync(source);
    }

    private async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<KernelCompilationAnalyzer, XUnitVerifier>
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        
        // Add necessary references
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        test.TestState.AdditionalReferences.Add(typeof(KernelAttribute).Assembly);

        await test.RunAsync();
    }
}

/// <summary>
/// XUnit test framework verifier for analyzer tests.
/// </summary>
public class XUnitVerifier : IVerifier
{
    public void Empty<T>(string collectionName, IEnumerable<T> collection)
    {
        collection.Should().BeEmpty(collectionName);
    }

    public void Equal<T>(T expected, T actual, string? message = null)
    {
        Assert.Equal(expected, message, actual);
    }

    public void True(bool assert, string? message = null)
    {
        assert.Should().BeTrue(message);
    }

    public void False(bool assert, string? message = null)
    {
        assert.Should().BeFalse(message);
    }
}
