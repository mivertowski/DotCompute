// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

public class IncrementalGenerationTests
{
    private readonly KernelSourceGenerator _generator = new();

    [Fact]
    public void IncrementalGeneration_WhenSourceUnchanged_ShouldReuseResults()
    {
        // Arrange
        var source = TestHelper.CreateKernelSource(
            "TestKernels", 
            "AddArrays", 
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        // Act - Run generator twice with same input
        var result1 = TestHelper.RunIncrementalGenerator(_generator, source);
        var result2 = TestHelper.RunIncrementalGenerator(_generator, source);

        // Assert
        result1.GeneratedSources.Length.Should().Be(result2.GeneratedSources.Length);
        result1.Diagnostics.Length.Should().Be(result2.Diagnostics.Length);
        
        for(int i = 0; i < result1.GeneratedSources.Length; i++)
        {
            _ = result1.GeneratedSources[i].SourceText.ToString()
                _ = .Should().Be(result2.GeneratedSources[i].SourceText.ToString());
        }
    }

    [Fact]
    public void IncrementalGeneration_WhenKernelAdded_ShouldRegenerateAffectedFiles()
    {
        // Arrange
        var initialSource = TestHelper.CreateKernelSource(
            "TestKernels", 
            "AddArrays", 
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        var updatedSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class TestKernels
{
    [Kernel]
    public static void AddArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [Kernel]
    public static void MultiplyArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] * b[i];
        }
    }
}";

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        (updatedResult.GeneratedSources.Length > initialResult.GeneratedSources.Length).Should().BeTrue();
        updatedResult.GeneratedSources.Contain(s => s.HintName.Contains("MultiplyArrays"));
    }

    [Fact]
    public void IncrementalGeneration_WhenKernelRemoved_ShouldRemoveGeneratedFiles()
    {
        // Arrange
        var initialSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class TestKernels
{
    [Kernel]
    public static void AddArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [Kernel]
    public static void MultiplyArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] * b[i];
        }
    }
}";

        var updatedSource = TestHelper.CreateKernelSource(
            "TestKernels", 
            "AddArrays", 
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        initialResult.GeneratedSources.Contain(s => s.HintName.Contains("MultiplyArrays"));
        updatedResult.GeneratedSources.Should().NotContain(s => s.HintName.Contains("MultiplyArrays"));
    }

    [Fact]
    public void IncrementalGeneration_WhenKernelAttributeChanged_ShouldRegenerateImplementation()
    {
        // Arrange
        var initialSource = TestHelper.CreateKernelWithAttributes(
            "TestKernels",
            "ProcessData",
            "VectorSize = 8",
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.ScalarOperation);

        var updatedSource = TestHelper.CreateKernelWithAttributes(
            "TestKernels",
            "ProcessData",
            "VectorSize = 16, Backends = KernelBackends.CPU | KernelBackends.CUDA",
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.ScalarOperation);

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        // Should have CUDA implementation in updated version
        updatedResult.GeneratedSources.Contain(s => s.HintName.Contains("CUDA"));
        initialResult.GeneratedSources.Should().NotContain(s => s.HintName.Contains("CUDA"));
    }

    [Fact]
    public void IncrementalGeneration_WhenMethodBodyChanged_ShouldRegenerateImplementation()
    {
        // Arrange
        var initialSource = TestHelper.CreateKernelSource(
            "TestKernels", 
            "ProcessData", 
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.ScalarOperation);

        var updatedSource = TestHelper.CreateKernelSource(
            "TestKernels", 
            "ProcessData", 
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.ComplexOperation);

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        var initialCpu = initialResult.GeneratedSources.First(s => s.HintName.Contains("CPU"));
        var updatedCpu = updatedResult.GeneratedSources.First(s => s.HintName.Contains("CPU"));
        
        _ = initialCpu.SourceText.ToString().Should().Not.Be(updatedCpu.SourceText.ToString());
    }

    [Fact]
    public void IncrementalGeneration_WhenNonKernelCodeChanged_ShouldNotAffectGeneration()
    {
        // Arrange
        var initialSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class TestKernels
{
    [Kernel]
    public static void AddArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    public static void RegularMethod()
    {
        Console.WriteLine(""Hello"");
    }
}";

        var updatedSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class TestKernels
{
    [Kernel]
    public static void AddArrays(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    public static void RegularMethod()
    {
        Console.WriteLine(""World"");
    }

    public static void AnotherRegularMethod()
    {
        // New non-kernel method
    }
}";

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        initialResult.GeneratedSources.Length.Should().Be(updatedResult.GeneratedSources.Length);
        
        // Generated source should be identical
        for(int i = 0; i < initialResult.GeneratedSources.Length; i++)
        {
            _ = initialResult.GeneratedSources[i].SourceText.ToString()
                _ = .Should().Be(updatedResult.GeneratedSources[i].SourceText.ToString());
        }
    }

    [Fact]
    public void IncrementalGeneration_WhenMultipleFilesChanged_ShouldHandleCorrectly()
    {
        // Arrange
        var file1Initial = TestHelper.CreateKernelSource(
            "Kernels1", 
            "Add", 
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        var file2Initial = TestHelper.CreateKernelSource(
            "Kernels2", 
            "Multiply", 
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleMultiply);

        var file1Updated = TestHelper.CreateKernelWithAttributes(
            "Kernels1",
            "Add",
            "VectorSize = 16",
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        var file2Updated = file2Initial; // No change

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, new[] { file1Initial, file2Initial });
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, new[] { file1Updated, file2Updated });

        // Assert
        // Should have same number of generated files
        updatedResult.GeneratedSources.Length.Should().Be(initialResult.GeneratedSources.Length);
        
        // Kernels1-related files should be different
        var initialKernels1Files = initialResult.GeneratedSources.Where(s => s.HintName.Contains("Kernels1"));
        var updatedKernels1Files = updatedResult.GeneratedSources.Where(s => s.HintName.Contains("Kernels1"));
        
        initialKernels1Files.NotBeEquivalentTo(updatedKernels1Files);
        
        // Kernels2-related files should be the same
        var initialKernels2Files = initialResult.GeneratedSources.Where(s => s.HintName.Contains("Kernels2"));
        var updatedKernels2Files = updatedResult.GeneratedSources.Where(s => s.HintName.Contains("Kernels2"));
        
        initialKernels2Files.Select(f => f.SourceText.ToString())
            .Should().BeEquivalentTo(updatedKernels2Files.Select(f => f.SourceText.ToString());
    }

    [Fact]
    public void IncrementalGeneration_WhenClassRenamed_ShouldUpdateGeneratedFiles()
    {
        // Arrange
        var initialSource = TestHelper.CreateKernelSource(
            "OldClassName", 
            "ProcessData", 
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.MemoryCopy);

        var updatedSource = TestHelper.CreateKernelSource(
            "NewClassName", 
            "ProcessData", 
            TestHelper.Parameters.SingleArray,
            TestHelper.KernelBodies.MemoryCopy);

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        initialResult.GeneratedSources.Contain(s => s.HintName.Contains("OldClassName"));
        initialResult.GeneratedSources.Should().NotContain(s => s.HintName.Contains("NewClassName"));
        
        updatedResult.GeneratedSources.Contain(s => s.HintName.Contains("NewClassName"));
        updatedResult.GeneratedSources.Should().NotContain(s => s.HintName.Contains("OldClassName"));
    }

    [Fact]
    public void IncrementalGeneration_WithComplexChanges_ShouldHandleCorrectly()
    {
        // Arrange - Start with multiple kernels
        var initialSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class MathKernels
{
    [Kernel]
    public static void Add(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    [Kernel(VectorSize = 8)]
    public static void Multiply(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] * b[i];
        }
    }

    [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA)]
    public static void Divide(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] / b[i];
        }
    }
}";

        // Complex change: Remove one kernel, modify another, add new one
        var updatedSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class MathKernels
{
    [Kernel(VectorSize = 16)] // Changed vector size
    public static void Add(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    // Removed Multiply kernel

    [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA)]
    public static void Divide(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] / b[i];
        }
    }

    // New kernel
    [Kernel(IsParallel = true)]
    public static void Subtract(float[] a, float[] b, float[] result, int length)
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] - b[i];
        }
    }
}";

        // Act
        var initialResult = TestHelper.RunIncrementalGenerator(_generator, initialSource);
        var updatedResult = TestHelper.RunIncrementalGenerator(_generator, updatedSource);

        // Assert
        // Should have Multiply in initial but not updated
        initialResult.GeneratedSources.Contain(s => s.HintName.Contains("Multiply"));
        updatedResult.GeneratedSources.Should().NotContain(s => s.HintName.Contains("Multiply"));

        // Should have Subtract in updated but not initial
        initialResult.GeneratedSources.Should().NotContain(s => s.HintName.Contains("Subtract"));
        updatedResult.GeneratedSources.Contain(s => s.HintName.Contains("Subtract"));

        // Should have Add in both, but different due to vector size change
        var initialAdd = initialResult.GeneratedSources.Where(s => s.HintName.Contains("Add")).ToList();
        var updatedAdd = updatedResult.GeneratedSources.Where(s => s.HintName.Contains("Add")).ToList();
        
        Assert.NotEmpty(initialAdd);
        Assert.NotEmpty(updatedAdd);
        initialAdd.Select(s => s.SourceText.ToString())
            .NotBeEquivalentTo(updatedAdd.Select(s => s.SourceText.ToString();
    }

    [Fact]
    public void IncrementalGeneration_WithSyntaxErrors_ShouldHandleGracefully()
    {
        // Arrange
        var validSource = TestHelper.CreateKernelSource(
            "ValidKernels", 
            "Add", 
            TestHelper.Parameters.TwoArrays,
            TestHelper.KernelBodies.SimpleAdd);

        var invalidSource = @"
using DotCompute.Generators.Kernel;
using FluentAssertions;

public class InvalidKernels
{
    [Kernel]
    public static void BrokenKernel(float[] a, float[] b  // Missing closing parenthesis
    {
        for(int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }
}";

        // Act
        var validResult = TestHelper.RunIncrementalGenerator(_generator, validSource);
        var invalidResult = TestHelper.RunIncrementalGenerator(_generator, invalidSource);

        // Assert
        validResult.GeneratedSources.Should().NotBeEmpty();
        invalidResult.GeneratedSources.Should().BeEmpty("Generator should not produce output for invalid syntax");
    }
}
