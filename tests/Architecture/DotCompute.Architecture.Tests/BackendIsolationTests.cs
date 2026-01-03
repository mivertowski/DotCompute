// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using NetArchTest.Rules;

namespace DotCompute.Architecture.Tests;

/// <summary>
/// Tests that verify backends are properly isolated from each other.
/// Each backend should be independently deployable without requiring other backends.
/// </summary>
[Trait("Category", "Architecture")]
public class BackendIsolationTests
{
    [Fact]
    public void CpuBackend_ShouldNotDependOn_OtherBackends()
    {
        var result = Types.InAssembly(typeof(DotCompute.Backends.CPU.CpuAccelerator).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Backends.CUDA",
                "DotCompute.Backends.Metal",
                "DotCompute.Backends.OpenCL")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "CPU backend should be independently deployable. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CudaBackend_ShouldNotDependOn_OtherBackends()
    {
        var result = Types.InAssembly(typeof(DotCompute.Backends.CUDA.CudaAccelerator).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Backends.CPU",
                "DotCompute.Backends.Metal",
                "DotCompute.Backends.OpenCL")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "CUDA backend should be independently deployable. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void MetalBackend_ShouldNotDependOn_OtherBackends()
    {
        var result = Types.InAssembly(typeof(DotCompute.Backends.Metal.MetalAccelerator).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Backends.CPU",
                "DotCompute.Backends.CUDA",
                "DotCompute.Backends.OpenCL")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Metal backend should be independently deployable. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void OpenClBackend_ShouldNotDependOn_OtherBackends()
    {
        var result = Types.InAssembly(typeof(DotCompute.Backends.OpenCL.OpenClAccelerator).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Backends.CPU",
                "DotCompute.Backends.CUDA",
                "DotCompute.Backends.Metal")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "OpenCL backend should be independently deployable. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void AllBackends_ShouldImplement_IAccelerator()
    {
        var acceleratorTypes = new[]
        {
            typeof(DotCompute.Backends.CPU.CpuAccelerator),
            typeof(DotCompute.Backends.CUDA.CudaAccelerator),
            typeof(DotCompute.Backends.Metal.MetalAccelerator),
            typeof(DotCompute.Backends.OpenCL.OpenClAccelerator)
        };

        foreach (var acceleratorType in acceleratorTypes)
        {
            acceleratorType.Should().Implement(typeof(DotCompute.Abstractions.Interfaces.IAccelerator),
                because: $"{acceleratorType.Name} must implement IAccelerator interface from Abstractions");
        }
    }
}
