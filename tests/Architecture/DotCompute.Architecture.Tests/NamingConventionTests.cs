// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using NetArchTest.Rules;

namespace DotCompute.Architecture.Tests;

/// <summary>
/// Tests that verify naming conventions are followed across the codebase.
/// </summary>
[Trait("Category", "Architecture")]
public class NamingConventionTests
{
    private static readonly System.Reflection.Assembly AbstractionsAssembly =
        typeof(DotCompute.Abstractions.Interfaces.IAccelerator).Assembly;

    [Fact]
    public void Interfaces_ShouldStartWith_I()
    {
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All interfaces should start with 'I' prefix. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PortInterfaces_ShouldEndWith_Port()
    {
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .AreInterfaces()
            .And()
            .ResideInNamespace("DotCompute.Abstractions.Ports")
            .Should()
            .HaveNameEndingWith("Port")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Port interfaces should follow the naming convention ending with 'Port'. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Accelerators_ShouldEndWith_Accelerator()
    {
        var backendAssemblies = new[]
        {
            typeof(DotCompute.Backends.CPU.CpuAccelerator).Assembly,
            typeof(DotCompute.Backends.CUDA.CudaAccelerator).Assembly,
            typeof(DotCompute.Backends.Metal.MetalAccelerator).Assembly,
            typeof(DotCompute.Backends.OpenCL.OpenClAccelerator).Assembly
        };

        var result = Types.InAssemblies(backendAssemblies)
            .That()
            .ImplementInterface(typeof(DotCompute.Abstractions.Interfaces.IAccelerator))
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Accelerator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All IAccelerator implementations should end with 'Accelerator'. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Exceptions_ShouldEndWith_Exception()
    {
        var allAssemblies = new[]
        {
            typeof(DotCompute.Abstractions.Interfaces.IAccelerator).Assembly,
            typeof(DotCompute.Core.KernelDefinition).Assembly,
            typeof(DotCompute.Memory.Buffers.GpuBuffer<>).Assembly
        };

        var result = Types.InAssemblies(allAssemblies)
            .That()
            .Inherit(typeof(Exception))
            .Should()
            .HaveNameEndingWith("Exception")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All exception types should end with 'Exception'. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Records_WithSnapshot_ShouldBeImmutable()
    {
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .HaveNameEndingWith("Snapshot")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Snapshot types should be sealed records for immutability. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
