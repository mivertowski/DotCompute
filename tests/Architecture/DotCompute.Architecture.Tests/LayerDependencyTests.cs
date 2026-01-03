// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using NetArchTest.Rules;

namespace DotCompute.Architecture.Tests;

/// <summary>
/// Tests that verify layer dependencies follow hexagonal architecture principles.
/// </summary>
[Trait("Category", "Architecture")]
public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly[] CoreAssemblies =
    [
        typeof(DotCompute.Abstractions.Interfaces.IAccelerator).Assembly,
        typeof(DotCompute.Core.KernelDefinition).Assembly,
        typeof(DotCompute.Memory.Buffers.GpuBuffer<>).Assembly
    ];

    private static readonly System.Reflection.Assembly[] BackendAssemblies =
    [
        typeof(DotCompute.Backends.CPU.CpuAccelerator).Assembly,
        typeof(DotCompute.Backends.CUDA.CudaAccelerator).Assembly,
        typeof(DotCompute.Backends.Metal.MetalAccelerator).Assembly,
        typeof(DotCompute.Backends.OpenCL.OpenClAccelerator).Assembly
    ];

    private static readonly System.Reflection.Assembly[] ExtensionAssemblies =
    [
        typeof(DotCompute.Algorithms.ComputeAlgorithms).Assembly,
        typeof(DotCompute.Linq.ParallelGpuQuery<>).Assembly
    ];

    [Fact]
    public void Core_ShouldNotDependOn_Backends()
    {
        // Core layer should never depend on specific backend implementations
        var result = Types.InAssemblies(CoreAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Backends.CPU",
                "DotCompute.Backends.CUDA",
                "DotCompute.Backends.Metal",
                "DotCompute.Backends.OpenCL")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Core layer should not depend on specific backend implementations. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Core_ShouldNotDependOn_Extensions()
    {
        // Core layer should not depend on extension libraries
        var result = Types.InAssemblies(CoreAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Algorithms",
                "DotCompute.Linq")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Core layer should not depend on extension libraries. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Abstractions_ShouldNotDependOn_Core()
    {
        // Abstractions should be the innermost layer
        var result = Types.InAssembly(typeof(DotCompute.Abstractions.Interfaces.IAccelerator).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Core",
                "DotCompute.Memory")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Abstractions should be the innermost layer with no dependencies on other DotCompute projects. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Extensions_ShouldOnlyDependOn_CoreAndAbstractions()
    {
        // Extensions can depend on Core and Abstractions but not Backends
        var result = Types.InAssemblies(ExtensionAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Backends.CPU",
                "DotCompute.Backends.CUDA",
                "DotCompute.Backends.Metal",
                "DotCompute.Backends.OpenCL")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Extensions should only depend on Core and Abstractions, not on specific backends. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Backends_MustDependOn_Abstractions()
    {
        // All backends must depend on Abstractions to implement interfaces
        foreach (var assembly in BackendAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .HaveNameEndingWith("Accelerator")
                .Should()
                .HaveDependencyOn("DotCompute.Abstractions")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"Backend {assembly.GetName().Name} should depend on Abstractions. " +
                         $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}
