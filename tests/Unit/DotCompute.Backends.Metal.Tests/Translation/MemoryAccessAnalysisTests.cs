// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution.Types.Core;
using DotCompute.Backends.Metal.Translation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Translation;

/// <summary>
/// Tests for memory access pattern analysis and optimization in CSharpToMSLTranslator.
/// Verifies detection of coalesced, strided, and scattered access patterns.
/// </summary>
public sealed class MemoryAccessAnalysisTests
{
    private readonly CSharpToMSLTranslator _translator;

    public MemoryAccessAnalysisTests()
    {
        _translator = new CSharpToMSLTranslator(NullLogger.Instance);
    }

    [Fact]
    public void Translate_CoalescedAccess_NoWarnings()
    {
        // Arrange: Optimal GPU access pattern
        const string csharpCode = @"
            public static void VectorAdd(Span<float> a, Span<float> b, Span<float> result)
            {
                int idx = Kernel.ThreadId.X;
                if (idx < result.Length)
                {
                    result[idx] = a[idx] + b[idx];
                }
            }";

        // Act
        var mslCode = _translator.Translate(csharpCode, "VectorAdd", "vector_add");
        var diagnostics = _translator.GetDiagnostics();

        // Assert: No warnings for optimal pattern
        Assert.NotEmpty(mslCode);
        Assert.DoesNotContain(diagnostics, d => d.Severity == MetalDiagnosticMessage.SeverityLevel.Warning);
    }

    [Fact]
    public void Translate_StridedAccess_InfoDiagnostic()
    {
        // Arrange: Strided access pattern
        const string csharpCode = @"
            public static void StridedCopy(Span<float> input, Span<float> output, int stride)
            {
                int idx = Kernel.ThreadId.X;
                if (idx < output.Length)
                {
                    output[idx] = input[idx * stride];
                }
            }";

        // Act
        var mslCode = _translator.Translate(csharpCode, "StridedCopy", "strided_copy");
        var diagnostics = _translator.GetDiagnostics();

        // Assert: Info diagnostic for strided access
        Assert.NotEmpty(mslCode);
        Assert.Contains(diagnostics, d =>
            d.Severity == MetalDiagnosticMessage.SeverityLevel.Info &&
            d.Component == "MemoryAccessAnalyzer" &&
            d.Message.Contains("Strided memory access detected"));

        // Verify suggestion is present
        var stridedDiag = diagnostics.First(d => d.Message.Contains("Strided"));
        Assert.True(stridedDiag.Context.ContainsKey("Suggestion"));
        Assert.Contains("threadgroup", stridedDiag.Context["Suggestion"].ToString()!);
    }

    [Fact(Skip = "Advanced memory access diagnostic feature pending Phase 3 implementation")]
    public void Translate_ScatteredAccess_Warning()
    {
        // Arrange: Scattered (indirect) access pattern
        const string csharpCode = @"
            public static void GatherData(Span<float> data, Span<int> indices, Span<float> result)
            {
                int idx = Kernel.ThreadId.X;
                if (idx < result.Length)
                {
                    result[idx] = data[indices[idx]];
                }
            }";

        // Act
        var mslCode = _translator.Translate(csharpCode, "GatherData", "gather_data");
        var diagnostics = _translator.GetDiagnostics();

        // Assert: Warning for scattered access
        Assert.NotEmpty(mslCode);
        Assert.Contains(diagnostics, d =>
            d.Severity == MetalDiagnosticMessage.SeverityLevel.Warning &&
            d.Component == "MemoryAccessAnalyzer" &&
            d.Message.Contains("Scattered memory access detected"));

        // Verify performance impact mentioned
        var scatteredDiag = diagnostics.First(d => d.Message.Contains("Scattered"));
        Assert.Contains("50-80%", scatteredDiag.Message);
        Assert.True(scatteredDiag.Context.ContainsKey("Pattern"));
        Assert.Equal("Scattered", scatteredDiag.Context["Pattern"]);
    }

    [Fact(Skip = "Advanced memory access diagnostic feature pending Phase 3 implementation")]
    public void Translate_MultipleAccessPatterns_AllDetected()
    {
        // Arrange: Kernel with multiple access patterns
        const string csharpCode = @"
            public static void ComplexKernel(
                Span<float> input1,
                Span<float> input2,
                Span<int> indices,
                Span<float> output,
                int stride)
            {
                int idx = Kernel.ThreadId.X;
                if (idx < output.Length)
                {
                    // Coalesced access
                    float a = input1[idx];

                    // Strided access
                    float b = input2[idx * stride];

                    // Scattered access
                    float c = input1[indices[idx]];

                    output[idx] = a + b + c;
                }
            }";

        // Act
        var mslCode = _translator.Translate(csharpCode, "ComplexKernel", "complex_kernel");
        var diagnostics = _translator.GetDiagnostics();

        // Assert: All patterns detected
        Assert.NotEmpty(mslCode);

        // Should have at least 2 diagnostics (strided info + scattered warning)
        Assert.True(diagnostics.Count >= 2,
            $"Expected at least 2 diagnostics, got {diagnostics.Count}");

        // Verify strided pattern detected
        Assert.Contains(diagnostics, d =>
            d.Message.Contains("Strided") &&
            d.Context.ContainsKey("Stride"));

        // Verify scattered pattern detected
        Assert.Contains(diagnostics, d =>
            d.Message.Contains("Scattered") &&
            d.Severity == MetalDiagnosticMessage.SeverityLevel.Warning);
    }

    [Fact]
    public void Translate_StrideExtraction_CorrectValue()
    {
        // Arrange: Strided access with named variable
        const string csharpCode = @"
            public static void StridedAccess(Span<float> data, int customStride)
            {
                int idx = Kernel.ThreadId.X;
                data[idx] = data[idx * customStride];
            }";

        // Act
        var mslCode = _translator.Translate(csharpCode, "StridedAccess", "strided_access");
        var diagnostics = _translator.GetDiagnostics();

        // Assert: Stride value extracted correctly
        var stridedDiag = diagnostics.FirstOrDefault(d => d.Message.Contains("Strided"));
        Assert.NotNull(stridedDiag);
        Assert.True(stridedDiag.Context.ContainsKey("Stride"));
        Assert.Equal("customStride", stridedDiag.Context["Stride"]);
    }

    [Fact(Skip = "Advanced memory access diagnostic feature pending Phase 3 implementation")]
    public void Translate_DiagnosticContext_ContainsLineNumber()
    {
        // Arrange: Simple kernel for line number tracking
        const string csharpCode = @"
            public static void TestKernel(Span<int> indices, Span<float> data, Span<float> output)
            {
                int idx = Kernel.ThreadId.X;
                output[idx] = data[indices[idx]];
            }";

        // Act
        var mslCode = _translator.Translate(csharpCode, "TestKernel", "test_kernel");
        var diagnostics = _translator.GetDiagnostics();

        // Assert: Diagnostic contains line number
        var diagnostic = diagnostics.FirstOrDefault();
        Assert.NotNull(diagnostic);
        Assert.True(diagnostic.Context.ContainsKey("LineNumber"));
        Assert.IsType<int>(diagnostic.Context["LineNumber"]);
        Assert.True((int)diagnostic.Context["LineNumber"] > 0);
    }

    [Fact(Skip = "Advanced memory access diagnostic feature pending Phase 3 implementation")]
    public void GetDiagnostics_AfterMultipleTranslations_OnlyLatest()
    {
        // Arrange
        const string kernel1 = @"
            public static void K1(Span<int> indices, Span<float> data)
            {
                data[indices[0]] = 1.0f;
            }";

        const string kernel2 = @"
            public static void K2(Span<float> data)
            {
                data[Kernel.ThreadId.X] = 2.0f;
            }";

        // Act: Translate first kernel
        _translator.Translate(kernel1, "K1", "k1");
        var firstCount = _translator.GetDiagnostics().Count;

        // Translate second kernel
        _translator.Translate(kernel2, "K2", "k2");
        var secondCount = _translator.GetDiagnostics().Count;

        // Assert: Only diagnostics from latest translation
        Assert.True(firstCount > 0, "First kernel should generate diagnostics");
        Assert.Equal(0, secondCount); // K2 has optimal access, no diagnostics
    }
}
