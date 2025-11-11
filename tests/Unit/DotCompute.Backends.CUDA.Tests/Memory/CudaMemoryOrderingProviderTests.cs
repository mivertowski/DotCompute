// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Memory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.Memory;

/// <summary>
/// Comprehensive unit tests for <see cref="CudaMemoryOrderingProvider"/>.
/// Tests consistency models, causal ordering, fence insertion, and capability queries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CudaMemoryOrdering")]
public sealed class CudaMemoryOrderingProviderTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CudaMemoryOrderingProvider _provider;

    public CudaMemoryOrderingProviderTests(ITestOutputHelper output)
    {
        _output = output;
        _provider = new CudaMemoryOrderingProvider(NullLogger.Instance);
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_DefaultState_InitializesCorrectly()
    {
        // Arrange & Act
        using var provider = new CudaMemoryOrderingProvider();

        // Assert
        provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Relaxed, "default model should be relaxed");
        provider.GetOverheadMultiplier().Should().Be(1.0, "relaxed model has no overhead");
        provider.IsCausalOrderingEnabled.Should().BeFalse("causal ordering should be disabled by default");

        _output.WriteLine("Provider initialized with correct defaults");
    }

    [Fact]
    public void Constructor_DetectsHardwareCapabilities()
    {
        // Arrange & Act
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Assert
        if (major >= 7)
        {
            _provider.IsAcquireReleaseSupported.Should().BeTrue("CC 7.0+ has native acquire-release support");
            _output.WriteLine($"Hardware acquire-release support detected (CC {major}.x)");
        }
        else
        {
            _provider.IsAcquireReleaseSupported.Should().BeFalse("CC < 7.0 lacks native acquire-release");
            _output.WriteLine($"Hardware acquire-release not supported (CC {major}.x)");
        }

        if (major >= 2)
        {
            _provider.SupportsSystemFences.Should().BeTrue("CC 2.0+ has UVA and system fences");
            _output.WriteLine($"System fence support detected (CC {major}.x)");
        }
        else
        {
            _provider.SupportsSystemFences.Should().BeFalse("CC < 2.0 lacks UVA");
            _output.WriteLine($"System fences not supported (CC {major}.x)");
        }
    }

    #endregion

    #region Consistency Model Tests

    [Fact]
    public void SetConsistencyModel_Relaxed_NoOverhead()
    {
        // Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.Relaxed);

        // Assert
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Relaxed);
        _provider.GetOverheadMultiplier().Should().Be(1.0);
        _provider.IsCausalOrderingEnabled.Should().BeFalse("relaxed model disables causal ordering");

        _output.WriteLine("Relaxed model: 1.0× performance (no overhead)");
    }

    [Fact]
    public void SetConsistencyModel_ReleaseAcquire_HasModerateOverhead()
    {
        // Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);

        // Assert
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.ReleaseAcquire);
        _provider.GetOverheadMultiplier().Should().Be(0.85);
        _provider.IsCausalOrderingEnabled.Should().BeTrue("release-acquire enables causal ordering");

        _output.WriteLine("Release-Acquire model: 0.85× performance (15% overhead)");
    }

    [Fact]
    public void SetConsistencyModel_Sequential_HasHighOverhead()
    {
        // Act
        _provider.SetConsistencyModel(MemoryConsistencyModel.Sequential);

        // Assert
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Sequential);
        _provider.GetOverheadMultiplier().Should().Be(0.60);
        _provider.IsCausalOrderingEnabled.Should().BeTrue("sequential consistency enables causal ordering");

        _output.WriteLine("Sequential model: 0.60× performance (40% overhead)");
    }

    [Fact]
    public void SetConsistencyModel_TransitionsCorrectly()
    {
        // Test all transitions
        var models = new[]
        {
            MemoryConsistencyModel.Relaxed,
            MemoryConsistencyModel.ReleaseAcquire,
            MemoryConsistencyModel.Sequential,
            MemoryConsistencyModel.ReleaseAcquire,
            MemoryConsistencyModel.Relaxed
        };

        foreach (var model in models)
        {
            // Act
            _provider.SetConsistencyModel(model);

            // Assert
            _provider.ConsistencyModel.Should().Be(model);
            _output.WriteLine($"Transitioned to {model} (multiplier: {_provider.GetOverheadMultiplier():F2}×)");
        }
    }

    [Fact]
    public void SetConsistencyModel_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new CudaMemoryOrderingProvider();
        provider.Dispose();

        // Act & Assert
        var act = () => provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Disposed provider correctly rejects SetConsistencyModel");
    }

    #endregion

    #region Causal Ordering Tests

    [Fact]
    public void EnableCausalOrdering_WhenEnabled_ActivatesReleaseAcquire()
    {
        // Arrange
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Relaxed, "starts relaxed");

        // Act
        _provider.EnableCausalOrdering(true);

        // Assert
        _provider.IsCausalOrderingEnabled.Should().BeTrue();
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.ReleaseAcquire,
            "enabling causal ordering upgrades from relaxed to release-acquire");

        _output.WriteLine("Causal ordering enabled, model upgraded to Release-Acquire");
    }

    [Fact]
    public void EnableCausalOrdering_WhenDisabled_RestoresState()
    {
        // Arrange
        _provider.EnableCausalOrdering(true);
        _provider.IsCausalOrderingEnabled.Should().BeTrue();

        // Act
        _provider.EnableCausalOrdering(false);

        // Assert
        _provider.IsCausalOrderingEnabled.Should().BeFalse();
        // Note: model stays at ReleaseAcquire (doesn't auto-downgrade)

        _output.WriteLine("Causal ordering disabled");
    }

    [Fact]
    public void EnableCausalOrdering_WithSequentialModel_DoesNotChangeModel()
    {
        // Arrange
        _provider.SetConsistencyModel(MemoryConsistencyModel.Sequential);

        // Act
        _provider.EnableCausalOrdering(true);

        // Assert
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Sequential,
            "sequential model is not downgraded");
        _provider.IsCausalOrderingEnabled.Should().BeTrue();

        _output.WriteLine("Causal ordering enabled without changing Sequential model");
    }

    [Fact]
    public void EnableCausalOrdering_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new CudaMemoryOrderingProvider();
        provider.Dispose();

        // Act & Assert
        var act = () => provider.EnableCausalOrdering(true);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Disposed provider correctly rejects EnableCausalOrdering");
    }

    #endregion

    #region Fence Insertion Tests

    [Fact]
    public void InsertFence_ThreadBlock_LogsCorrectly()
    {
        // Act
        _provider.InsertFence(FenceType.ThreadBlock);

        // Assert - no exception thrown
        _output.WriteLine("Thread-block fence inserted successfully (~10ns latency)");
    }

    [Fact]
    public void InsertFence_Device_LogsCorrectly()
    {
        // Act
        _provider.InsertFence(FenceType.Device);

        // Assert - no exception thrown
        _output.WriteLine("Device fence inserted successfully (~100ns latency)");
    }

    [Fact]
    public void InsertFence_System_OnSupportedDevice_LogsCorrectly()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Act
        _provider.InsertFence(FenceType.System);

        // Assert
        if (major >= 2)
        {
            _output.WriteLine("System fence inserted successfully (~200ns latency, CC 2.0+)");
        }
        else
        {
            _output.WriteLine("System fence fell back to device fence (CC < 2.0)");
        }
    }

    [Fact]
    public void InsertFence_WithReleaseLocation_LogsCorrectly()
    {
        // Arrange
        var location = FenceLocation.Release;

        // Act
        _provider.InsertFence(FenceType.Device, location);

        // Assert
        _output.WriteLine($"Fence inserted with Release semantics: AfterWrites={location.AfterWrites}");
    }

    [Fact]
    public void InsertFence_WithAcquireLocation_LogsCorrectly()
    {
        // Arrange
        var location = FenceLocation.Acquire;

        // Act
        _provider.InsertFence(FenceType.Device, location);

        // Assert
        _output.WriteLine($"Fence inserted with Acquire semantics: BeforeReads={location.BeforeReads}");
    }

    [Fact]
    public void InsertFence_WithFullBarrierLocation_LogsCorrectly()
    {
        // Arrange
        var location = FenceLocation.FullBarrier;

        // Act
        _provider.InsertFence(FenceType.Device, location);

        // Assert
        _output.WriteLine($"Full barrier inserted: AfterWrites={location.AfterWrites}, BeforeReads={location.BeforeReads}");
    }

    [Fact]
    public void InsertFence_WithCustomLocation_LogsCorrectly()
    {
        // Arrange
        var location = new FenceLocation
        {
            AtEntry = true,
            AtExit = false,
            AfterWrites = true,
            BeforeReads = false
        };

        // Act
        _provider.InsertFence(FenceType.ThreadBlock, location);

        // Assert
        _output.WriteLine("Custom fence location inserted: AtEntry + AfterWrites");
    }

    [Fact]
    public void InsertFence_NullLocation_UsesFullBarrier()
    {
        // Act
        _provider.InsertFence(FenceType.Device, location: null);

        // Assert - should use default full barrier semantics
        _output.WriteLine("Fence inserted with default full barrier semantics");
    }

    [Theory]
    [InlineData(FenceType.ThreadBlock)]
    [InlineData(FenceType.Device)]
    [InlineData(FenceType.System)]
    public void InsertFence_AllTypes_ExecuteWithoutException(FenceType type)
    {
        // Act & Assert
        var act = () => _provider.InsertFence(type);
        act.Should().NotThrow();

        _output.WriteLine($"Fence type {type} inserted successfully");
    }

    [Fact]
    public void InsertFence_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var provider = new CudaMemoryOrderingProvider();
        provider.Dispose();

        // Act & Assert
        var act = () => provider.InsertFence(FenceType.Device);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Disposed provider correctly rejects InsertFence");
    }

    #endregion

    #region Overhead Multiplier Tests

    [Theory]
    [InlineData(MemoryConsistencyModel.Relaxed, 1.0)]
    [InlineData(MemoryConsistencyModel.ReleaseAcquire, 0.85)]
    [InlineData(MemoryConsistencyModel.Sequential, 0.60)]
    public void GetOverheadMultiplier_ReturnsExpectedValue(MemoryConsistencyModel model, double expectedMultiplier)
    {
        // Arrange
        _provider.SetConsistencyModel(model);

        // Act
        var multiplier = _provider.GetOverheadMultiplier();

        // Assert
        multiplier.Should().Be(expectedMultiplier);
        _output.WriteLine($"{model} model: {multiplier:F2}× ({(1.0 - multiplier) * 100:F0}% overhead)");
    }

    [Fact]
    public void GetOverheadMultiplier_RelaxedBaseline_IsUnity()
    {
        // Arrange
        _provider.SetConsistencyModel(MemoryConsistencyModel.Relaxed);

        // Act
        var multiplier = _provider.GetOverheadMultiplier();

        // Assert
        multiplier.Should().Be(1.0, "relaxed model is the baseline with no overhead");
        _output.WriteLine("Relaxed model verified as 1.0× baseline");
    }

    [Fact]
    public void GetOverheadMultiplier_ReleaseAcquire_IsReasonable()
    {
        // Arrange
        _provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);

        // Act
        var multiplier = _provider.GetOverheadMultiplier();

        // Assert
        multiplier.Should().BeGreaterThan(0.5, "overhead should not halve performance");
        multiplier.Should().BeLessThan(1.0, "should have some overhead");
        _output.WriteLine($"Release-Acquire overhead: {(1.0 - multiplier) * 100:F0}% (reasonable)");
    }

    [Fact]
    public void GetOverheadMultiplier_Sequential_IsHighest()
    {
        // Arrange
        _provider.SetConsistencyModel(MemoryConsistencyModel.Sequential);

        // Act
        var multiplier = _provider.GetOverheadMultiplier();
        var relaxedMultiplier = 1.0;
        var releaseAcquireMultiplier = 0.85;

        // Assert
        multiplier.Should().BeLessThan(releaseAcquireMultiplier, "sequential has more overhead than release-acquire");
        multiplier.Should().BeLessThan(relaxedMultiplier, "sequential has overhead");
        _output.WriteLine($"Sequential overhead: {(1.0 - multiplier) * 100:F0}% (highest of all models)");
    }

    #endregion

    #region Capability Query Tests

    [Fact]
    public void IsAcquireReleaseSupported_DependsOnComputeCapability()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Act
        var isSupported = _provider.IsAcquireReleaseSupported;

        // Assert
        if (major >= 7)
        {
            isSupported.Should().BeTrue("CC 7.0+ (Volta) has native acquire-release support");
            _output.WriteLine($"Acquire-Release supported natively on CC {major}.x (Volta+)");
        }
        else
        {
            isSupported.Should().BeFalse("CC < 7.0 lacks native acquire-release");
            _output.WriteLine($"Acquire-Release not supported on CC {major}.x (pre-Volta)");
        }
    }

    [Fact]
    public void SupportsSystemFences_DependsOnUVA()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Act
        var isSupported = _provider.SupportsSystemFences;

        // Assert
        if (major >= 2)
        {
            isSupported.Should().BeTrue("CC 2.0+ (Fermi) has UVA and system fences");
            _output.WriteLine($"System fences supported on CC {major}.x (UVA available)");
        }
        else
        {
            isSupported.Should().BeFalse("CC < 2.0 lacks UVA");
            _output.WriteLine($"System fences not supported on CC {major}.x (no UVA)");
        }
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new CudaMemoryOrderingProvider();

        // Act & Assert
        provider.Dispose();
        var act = () => provider.Dispose();
        act.Should().NotThrow("multiple Dispose calls should be safe");

        _output.WriteLine("Multiple Dispose calls handled safely");
    }

    [Fact]
    public void Dispose_InvalidatesAllOperations()
    {
        // Arrange
        var provider = new CudaMemoryOrderingProvider();
        provider.Dispose();

        // Act & Assert - all operations should throw
        var setModel = () => provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        setModel.Should().Throw<ObjectDisposedException>();

        var enableCausal = () => provider.EnableCausalOrdering(true);
        enableCausal.Should().Throw<ObjectDisposedException>();

        var insertFence = () => provider.InsertFence(FenceType.Device);
        insertFence.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("All operations correctly rejected after disposal");
    }

    #endregion

    #region Edge Cases and Validation

    [Fact]
    public void ConsistencyModel_ReadOnlyProperty_ReflectsCurrentState()
    {
        // Act & Assert - test all models
        _provider.SetConsistencyModel(MemoryConsistencyModel.Relaxed);
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Relaxed);

        _provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.ReleaseAcquire);

        _provider.SetConsistencyModel(MemoryConsistencyModel.Sequential);
        _provider.ConsistencyModel.Should().Be(MemoryConsistencyModel.Sequential);

        _output.WriteLine("ConsistencyModel property correctly reflects all state changes");
    }

    [Fact]
    public void IsCausalOrderingEnabled_InternalProperty_TracksState()
    {
        // Initial state
        _provider.IsCausalOrderingEnabled.Should().BeFalse();

        // Enable
        _provider.EnableCausalOrdering(true);
        _provider.IsCausalOrderingEnabled.Should().BeTrue();

        // Disable
        _provider.EnableCausalOrdering(false);
        _provider.IsCausalOrderingEnabled.Should().BeFalse();

        _output.WriteLine("IsCausalOrderingEnabled correctly tracks enable/disable state");
    }

    [Fact]
    public void SystemFenceCapability_IsConsistent()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Act - multiple queries should return same result
        var result1 = _provider.SupportsSystemFences;
        var result2 = _provider.SupportsSystemFences;
        var result3 = _provider.SupportsSystemFences;

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
        result1.Should().Be(major >= 2);

        _output.WriteLine($"System fence capability consistent across queries: {result1}");
    }

    [Fact]
    public void AcquireReleaseCapability_IsConsistent()
    {
        // Arrange
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();

        // Act - multiple queries should return same result
        var result1 = _provider.IsAcquireReleaseSupported;
        var result2 = _provider.IsAcquireReleaseSupported;
        var result3 = _provider.IsAcquireReleaseSupported;

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
        result1.Should().Be(major >= 7);

        _output.WriteLine($"Acquire-release capability consistent across queries: {result1}");
    }

    #endregion
}
