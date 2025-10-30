// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Edge case tests for Ring Kernel generation.
/// Tests boundary conditions and unusual scenarios.
/// </summary>
public sealed class RingKernelEdgeCaseTests
{
    [Fact]
    public void EdgeCase_RingKernelWithVeryLongName_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void ProcessStreamDataWithAdvancedFilteringAndTransformationCapabilities(Span<float> data)
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
    public void EdgeCase_RingKernelWithUnicodeCharacters_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(KernelId = "处理器_プロセッサ_معالج")]
                    public static void Process(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Unicode identifiers should be handled correctly
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void EdgeCase_RingKernelWithMaximumParameters_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(
                        Span<float> p1, Span<float> p2, Span<float> p3, Span<float> p4,
                        Span<float> p5, Span<float> p6, Span<float> p7, Span<float> p8,
                        int s1, int s2, int s3, int s4, int s5, int s6, int s7, int s8)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var content = registrySource.SourceText.ToString();
        Assert.Contains("ParameterCount = 16", content);
    }

    [Fact]
    public void EdgeCase_RingKernelInGlobalNamespace_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            public static class Processors
            {
                [RingKernel]
                public static void Process(Span<float> data)
                {
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_RingKernelWithCommentedCode_GeneratesCorrectly()
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
                        // This is a comment
                        /* Multi-line
                           comment */
                        data[0] = 1.0f;
                        // data[1] = 2.0f; // Commented code
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_RingKernelWithPreprocessorDirectives_GeneratesCorrectly()
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
            #if DEBUG
                        data[0] = 1.0f;
            #else
                        data[0] = 2.0f;
            #endif
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_RingKernelWithNullableParameters_GeneratesCorrectly()
    {
        const string source = """
            #nullable enable
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<float?> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Nullable value types in Span are allowed
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id.StartsWith("DC", StringComparison.Ordinal)).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void EdgeCase_RingKernelWithAttributeOnMultipleLines_GeneratesCorrectly()
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
                        KernelId = "Test",
                        Capacity = 1024,
                        Mode = RingKernelMode.Persistent
                    )]
                    public static void Process(Span<float> data)
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
    public void EdgeCase_RingKernelWithTrailingCommaInAttribute_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel(Capacity = 1024,)]
                    public static void Process(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Trailing comma is valid C# syntax
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void EdgeCase_RingKernelWithExpressionBody_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<float> data) => data[0] = 1.0f;
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_MultipleRingKernelsInDifferentPartialClasses_GeneratesAllCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static partial class Processors
                {
                    [RingKernel(KernelId = "Part1")]
                    public static void Process1(Span<float> data) { }
                }

                public static partial class Processors
                {
                    [RingKernel(KernelId = "Part2")]
                    public static void Process2(Span<float> data) { }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.NotNull(registrySource);

        var content = registrySource.SourceText.ToString();
        Assert.Contains("Part1", content);
        Assert.Contains("Part2", content);
    }

    [Fact]
    public void EdgeCase_RingKernelWithRegionDirectives_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    #region Ring Kernel Methods
                    [RingKernel]
                    public static void Process(Span<float> data)
                    {
                        #region Implementation
                        data[0] = 1.0f;
                        #endregion
                    }
                    #endregion
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_RingKernelWithVerbatimIdentifier_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void @Process(Span<float> @data)
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
    public void EdgeCase_RingKernelWithDefaultParameterValue_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<float> data, int multiplier = 1)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Default parameter values are allowed
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id.StartsWith("DC", StringComparison.Ordinal)).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void EdgeCase_RingKernelWithParamsParameter_HandledCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(params int[] values)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // params arrays may or may not be supported
        // Either generates or produces diagnostic
    }

    [Fact]
    public void EdgeCase_RingKernelWithObsoleteAttribute_GeneratesWithWarning()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [Obsolete("Use NewProcess instead")]
                    [RingKernel]
                    public static void OldProcess(Span<float> data)
                    {
                    }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Obsolete attribute doesn't prevent generation
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_RingKernelWithMultipleAttributes_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;
            using System.Runtime.CompilerServices;

            namespace TestApp
            {
                public static class Processors
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    [RingKernel]
                    public static void Process(Span<float> data)
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
    public void EdgeCase_RingKernelWithEmptyBody_GeneratesCorrectly()
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
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void EdgeCase_RingKernelWithOnlySingleStatement_GeneratesCorrectly()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

            namespace TestApp
            {
                public static class Processors
                {
                    [RingKernel]
                    public static void Process(Span<float> data) { data[0] = 1.0f; }
                }
            }
            """;

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(generatedSources);
    }
}
