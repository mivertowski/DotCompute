// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using FluentAssertions;
using NetArchTest.Rules;

namespace DotCompute.Architecture.Tests;

/// <summary>
/// Tests that verify hexagonal (ports and adapters) architecture principles.
/// </summary>
[Trait("Category", "Architecture")]
public class HexagonalArchitectureTests
{
    private static readonly System.Reflection.Assembly AbstractionsAssembly =
        typeof(DotCompute.Abstractions.Interfaces.IAccelerator).Assembly;

    [Fact]
    public void Ports_ShouldResideIn_PortsNamespace()
    {
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameEndingWith("Port")
            .Should()
            .ResideInNamespace("DotCompute.Abstractions.Ports")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All port interfaces should reside in the Ports namespace. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Ports_ShouldBeInterfaces()
    {
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .ResideInNamespace("DotCompute.Abstractions.Ports")
            .And()
            .HaveNameStartingWith("I")
            .Should()
            .BeInterfaces()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All types starting with 'I' in Ports namespace should be interfaces. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PortSupportTypes_ShouldBeRecordsOrEnums()
    {
        // Types in Ports namespace that don't start with 'I' should be DTOs (records/classes/enums)
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .ResideInNamespace("DotCompute.Abstractions.Ports")
            .And()
            .DoNotHaveNameStartingWith("I")
            .Should()
            .BeSealed()
            .Or()
            .BeStatic()
            .GetResult();

        // Note: Enums are not sealed or static, so we check them separately
        var portTypes = Types.InAssembly(AbstractionsAssembly)
            .That()
            .ResideInNamespace("DotCompute.Abstractions.Ports")
            .And()
            .DoNotHaveNameStartingWith("I")
            .GetTypes();

        foreach (var type in portTypes)
        {
            var isValidPortType = type.IsEnum || type.IsSealed || type.IsAbstract;
            isValidPortType.Should().BeTrue(
                because: $"{type.Name} in Ports namespace should be an enum, sealed record, or static class");
        }
    }

    [Fact]
    public void Abstractions_ShouldHaveNoExternalDependencies()
    {
        // Abstractions should only depend on .NET BCL
        var result = Types.InAssembly(AbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotCompute.Core",
                "DotCompute.Memory",
                "DotCompute.Backends",
                "DotCompute.Extensions",
                "DotCompute.Algorithms",
                "DotCompute.Linq")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Abstractions should be the innermost layer with no DotCompute dependencies. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void AbstractionsInterfaces_ShouldNotHaveImplementations()
    {
        // Abstractions should only define interfaces, not provide implementations
        var result = Types.InAssembly(AbstractionsAssembly)
            .That()
            .ResideInNamespace("DotCompute.Abstractions.Interfaces")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .AreNotStatic()
            .Should()
            .NotExist()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Interfaces namespace should not contain concrete implementations. " +
                     $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CorePorts_ShouldExist()
    {
        // Verify the core ports have been defined
        var portTypes = Types.InAssembly(AbstractionsAssembly)
            .That()
            .ResideInNamespace("DotCompute.Abstractions.Ports")
            .And()
            .AreInterfaces()
            .GetTypes()
            .ToList();

        portTypes.Should().NotBeEmpty(
            because: "At least one port interface should be defined");

        // Check for expected core ports
        var portNames = portTypes.Select(t => t.Name).ToList();
        portNames.Should().Contain("IKernelCompilationPort",
            because: "Kernel compilation port should be defined");
        portNames.Should().Contain("IMemoryManagementPort",
            because: "Memory management port should be defined");
        portNames.Should().Contain("IKernelExecutionPort",
            because: "Kernel execution port should be defined");
        portNames.Should().Contain("IDeviceDiscoveryPort",
            because: "Device discovery port should be defined");
        portNames.Should().Contain("IHealthMonitoringPort",
            because: "Health monitoring port should be defined");
    }
}
