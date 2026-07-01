// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Initialization;

/// <summary>
/// Resilience tests for <see cref="AcceleratorRuntime"/> accelerator discovery.
/// Regression coverage for GH #182: a backend that cannot initialize on the current machine
/// (e.g. CUDA on a box with no usable GPU) must NOT prevent the other backends (e.g. CPU) from
/// being discovered, and must never crash runtime initialization.
/// </summary>
public sealed class AcceleratorRuntimeTests
{
    private static AcceleratorRuntime CreateRuntime(IServiceProvider provider)
        => new(provider, Substitute.For<ILogger<AcceleratorRuntime>>());

    [Fact]
    public async Task InitializeAsync_SkipsBackendWhoseFactoryReturnsNull_AndKeepsWorkingAccelerator()
    {
        // A backend that cannot initialize returns null from its IAccelerator factory (this is how
        // the CUDA backend now degrades when no usable GPU is present). The working accelerator
        // (e.g. CPU) must still be discovered.
        var working = Substitute.For<IAccelerator>();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IAccelerator>(_ => null!); // unavailable backend
        _ = services.AddSingleton(working);                  // working backend
        using var provider = services.BuildServiceProvider();
        using var runtime = CreateRuntime(provider);

        await runtime.InitializeAsync();

        _ = runtime.GetAccelerators().Should().ContainSingle().Which.Should().BeSameAs(working);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotThrow_WhenABackendFactoryThrows()
    {
        // Defense in depth: a backend factory that THROWS during resolution must not crash runtime
        // initialization. GetServices<IAccelerator>() is all-or-nothing, so an unguarded throw here
        // would abort discovery of every backend and take the process down (the GH #182 symptom).
        var services = new ServiceCollection();
        _ = services.AddSingleton<IAccelerator>(_ => throw new InvalidOperationException("backend init failed"));
        using var provider = services.BuildServiceProvider();
        using var runtime = CreateRuntime(provider);

        var act = async () => await runtime.InitializeAsync();

        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent_AndDiscoversRegisteredAccelerators()
    {
        var a = Substitute.For<IAccelerator>();
        var services = new ServiceCollection();
        _ = services.AddSingleton(a);
        using var provider = services.BuildServiceProvider();
        using var runtime = CreateRuntime(provider);

        await runtime.InitializeAsync();
        await runtime.InitializeAsync(); // second call must not double-register

        _ = runtime.GetAccelerators().Should().ContainSingle().Which.Should().BeSameAs(a);
    }
}
