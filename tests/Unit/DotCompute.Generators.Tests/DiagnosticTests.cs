// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Generators.Kernel;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotCompute.Tests.Unit;

public class DiagnosticTests
{
    private readonly KernelSourceGenerator _generator = new();

    [Fact]
    public void Generator_WithValidKernel_ShouldNotProduceDiagnostics()
    {
        // Arrange
        var source = TestHelper.CreateUnsafeKernelSource(
            "ValidKernels",
            "AddArrays",
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        TestHelper.VerifyNoDiagnostics(result);
    }

    [Fact]
    public void Generator_WithInvalidSyntax_ShouldHandleGracefully()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class BrokenKernels
{
    [Kernel]
    public static void BrokenMethod(float[] a, float[] b  // Missing closing paren and body
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        // Generator should not crash, but also shouldn't produce output
        result.GeneratedSources.Should().BeEmpty("Generator should not produce output for invalid syntax");
    }

    [Fact]
    public void Generator_WithMissingNamespace_ShouldUseGlobalNamespace()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

[Kernel]
public static void GlobalKernel(float[] input, float[] output, int length)
{
    for (int i = 0; i < length; i++)
    {
        output[i] = input[i] * 2.0f;
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        // Should handle global scope methods gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public void Generator_WithComplexGenericTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using System;

public class GenericKernels
{
    [Kernel]
    public static void ProcessBuffers(Span<float> input, Span<float> output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_WithNestedClasses_ShouldHandleCorrectly()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class OuterClass
{
    public class NestedKernels
    {
        [Kernel]
        public static void NestedKernel(float[] input, float[] output, int length)
        {
            for (int i = 0; i < length; i++)
            {
                output[i] = input[i] + 1.0f;
            }
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        var registryCode = TestHelper.GetGeneratedSource(result, "KernelRegistry.g.cs");
        registryCode.Should().Contain("OuterClass+NestedKernels.NestedKernel");
    }

    [Fact]
    public void Generator_WithPartialClasses_ShouldCombineCorrectly()
    {
        // Arrange
        var source1 = @"
using DotCompute.Generators.Kernel;

public partial class PartialKernels
{
    [Kernel]
    public static void FirstKernel(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}";

        var source2 = @"
using DotCompute.Generators.Kernel;

public partial class PartialKernels
{
    [Kernel]
    public static void SecondKernel(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] + 1.0f;
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, new[] { source1, source2 });

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("FirstKernel"));
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("SecondKernel"));
        
        var invokerCode = result.GeneratedSources.FirstOrDefault(s => s.HintName.Contains("Invoker.g.cs"));
        if (invokerCode != null)
        {
            var code = invokerCode.SourceText.ToString();
            code.Should().Contain("FirstKernel");
            code.Should().Contain("SecondKernel");
        }
    }

    [Fact]
    public void Generator_WithDuplicateKernelNames_ShouldHandleCorrectly()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class KernelClass1
{
    [Kernel]
    public static void Process(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}

public class KernelClass2
{
    [Kernel]
    public static void Process(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] + 1.0f;
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        
        // Should generate separate implementations for each class
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("KernelClass1_Process"));
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains("KernelClass2_Process"));
        
        var registryCode = TestHelper.GetGeneratedSource(result, "KernelRegistry.g.cs");
        registryCode.Should().Contain("KernelClass1.Process");
        registryCode.Should().Contain("KernelClass2.Process");
    }

    [Fact]
    public void Generator_WithOverloadedKernels_ShouldHandleCorrectly()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class OverloadedKernels
{
    [Kernel]
    public static void Process(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }

    [Kernel]
    public static void Process(double[] input, double[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] * 2.0;
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        
        // Should generate implementations for both overloads
        // The generator should handle this by including parameter types in file names or method names
        result.GeneratedSources.Count(s => s.HintName.Contains("Process")).Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Generator_WithVeryLongMethodName_ShouldHandleCorrectly()
    {
        // Arrange
        var longMethodName = "ProcessDataWithVeryLongMethodNameThatExceedsReasonableLimitsButShouldStillWork";
        var source = TestHelper.CreateKernelSource(
            "LongNameKernels",
            longMethodName,
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.MemoryCopy);

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        result.GeneratedSources.Should().Contain(s => s.HintName.Contains(longMethodName));
    }

    [Fact]
    public void Generator_WithSpecialCharactersInComments_ShouldNotBreak()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class CommentKernels
{
    /// <summary>
    /// This kernel has special characters: <>""&'@#$%^*()[]{}|;:/?+=~`
    /// And unicode: αβγδε ñáéíóú 中文
    /// </summary>
    [Kernel]
    public static void KernelWithSpecialComments(float[] input, float[] output, int length)
    {
        // Comment with special chars: <>""&'@#$%
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i]; // Another comment
        }
        /* Block comment with 中文 and emojis: 🚀🔥💯 */
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_WithExpressionBodiedKernel_ShouldHandleCorrectly()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class ExpressionKernels
{
    [Kernel]
    public static void SimpleExpression(float[] input, float[] output, int length) =>
        ProcessArrays(input, output, length);
    
    private static void ProcessArrays(float[] input, float[] output, int length)
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        var cpuCode = result.GeneratedSources.FirstOrDefault(s => s.HintName.Contains("CPU.g.cs"));
        cpuCode.Should().NotBeNull();
    }

    [Fact]
    public void Generator_WithAsyncKernel_ShouldIgnoreOrHandle()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;
using System.Threading.Tasks;

public class AsyncKernels
{
    [Kernel]
    public static async Task AsyncKernel(float[] input, float[] output, int length)
    {
        await Task.Run(() =>
        {
            for (int i = 0; i < length; i++)
            {
                output[i] = input[i] * 2.0f;
            }
        });
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        // Async kernels might be ignored or handled specially
        result.Should().NotBeNull();
        // The generator should not crash regardless
    }

    [Fact]
    public void Generator_WithGenericKernelMethod_ShouldHandleCorrectly()
    {
        // Arrange
        var source = @"
using DotCompute.Generators.Kernel;

public class GenericKernels
{
    [Kernel]
    public static void GenericProcess<T>(T[] input, T[] output, int length) where T : unmanaged
    {
        for (int i = 0; i < length; i++)
        {
            output[i] = input[i];
        }
    }
}";

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        // Generic kernels might be handled specially or ignored
        result.Should().NotBeNull();
        // The generator should not crash with generic methods
    }

    [Fact]
    public void Generator_WithLargeNumberOfKernels_ShouldHandleEfficiently()
    {
        // Arrange
        var sourceBuilder = new System.Text.StringBuilder();
        sourceBuilder.AppendLine("using DotCompute.Generators.Kernel;");
        sourceBuilder.AppendLine("public class ManyKernels {");
        
        for (int i = 0; i < 50; i++)
        {
            sourceBuilder.AppendLine($@"
    [Kernel]
    public static void Kernel{i:D3}(float[] input, float[] output, int length)
    {{
        for (int j = 0; j < length; j++)
        {{
            output[j] = input[j] * {i + 1}.0f;
        }}
    }}");
        }
        
        sourceBuilder.AppendLine("}");
        var source = sourceBuilder.ToString();

        // Act
        var result = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        result.GeneratedSources.Length.Should().BeGreaterThan(50); // Registry + implementations
        
        var registryCode = TestHelper.GetGeneratedSource(result, "KernelRegistry.g.cs");
        registryCode.Should().NotBeNull();
        
        // Should contain all kernels
        for (int i = 0; i < 50; i++)
        {
            registryCode.Should().Contain($"Kernel{i:D3}");
        }
    }
}