// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Recovery;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests.Recovery;

/// <summary>
/// Comprehensive tests for ResetOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Recovery")]
public sealed class ResetOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        // Act
        var options = ResetOptions.Default;

        // Assert
        options.ResetType.Should().Be(ResetType.Soft);
        options.WaitForCompletion.Should().BeTrue();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.ClearMemoryPool.Should().BeFalse();
        options.ClearKernelCache.Should().BeFalse();
        options.Reinitialize.Should().BeFalse();
    }

    [Theory]
    [InlineData(ResetType.Soft)]
    [InlineData(ResetType.Context)]
    [InlineData(ResetType.Hard)]
    [InlineData(ResetType.Full)]
    public void PropertyInitialization_WithResetType_SetsCorrectly(ResetType resetType)
    {
        // Act
        var options = new ResetOptions { ResetType = resetType };

        // Assert
        options.ResetType.Should().Be(resetType);
    }

    [Fact]
    public void Soft_CreatesCorrectOptions()
    {
        // Act
        var options = ResetOptions.Soft;

        // Assert
        options.ResetType.Should().Be(ResetType.Soft);
        options.WaitForCompletion.Should().BeTrue();
        options.ClearMemoryPool.Should().BeFalse();
        options.ClearKernelCache.Should().BeFalse();
    }

    [Fact]
    public void Context_CreatesCorrectOptions()
    {
        // Act
        var options = ResetOptions.Context;

        // Assert
        options.ResetType.Should().Be(ResetType.Context);
        options.WaitForCompletion.Should().BeTrue();
        options.ClearKernelCache.Should().BeTrue();
    }

    [Fact]
    public void Hard_CreatesCorrectOptions()
    {
        // Act
        var options = ResetOptions.Hard;

        // Assert
        options.ResetType.Should().Be(ResetType.Hard);
        options.WaitForCompletion.Should().BeTrue();
        options.ClearMemoryPool.Should().BeTrue();
        options.ClearKernelCache.Should().BeTrue();
    }

    [Fact]
    public void Full_CreatesCorrectOptions()
    {
        // Act
        var options = ResetOptions.Full;

        // Assert
        options.ResetType.Should().Be(ResetType.Full);
        options.WaitForCompletion.Should().BeTrue();
        options.ClearMemoryPool.Should().BeTrue();
        options.ClearKernelCache.Should().BeTrue();
        options.Reinitialize.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    public void Timeout_CanBeCustomized(int seconds)
    {
        // Arrange
        var expectedTimeout = TimeSpan.FromSeconds(seconds);

        // Act
        var options = new ResetOptions
        {
            ResetType = ResetType.Soft,
            Timeout = expectedTimeout
        };

        // Assert
        options.Timeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void WaitForCompletion_CanBeToggled()
    {
        // Act
        var optionsWithWait = new ResetOptions { ResetType = ResetType.Soft, WaitForCompletion = true };
        var optionsWithoutWait = new ResetOptions { ResetType = ResetType.Soft, WaitForCompletion = false };

        // Assert
        optionsWithWait.WaitForCompletion.Should().BeTrue();
        optionsWithoutWait.WaitForCompletion.Should().BeFalse();
    }

    [Fact]
    public void ClearMemoryPool_CanBeToggled()
    {
        // Act
        var optionsWithClear = new ResetOptions { ResetType = ResetType.Hard, ClearMemoryPool = true };
        var optionsWithoutClear = new ResetOptions { ResetType = ResetType.Hard, ClearMemoryPool = false };

        // Assert
        optionsWithClear.ClearMemoryPool.Should().BeTrue();
        optionsWithoutClear.ClearMemoryPool.Should().BeFalse();
    }

    [Fact]
    public void ClearKernelCache_CanBeToggled()
    {
        // Act
        var optionsWithClear = new ResetOptions { ResetType = ResetType.Context, ClearKernelCache = true };
        var optionsWithoutClear = new ResetOptions { ResetType = ResetType.Context, ClearKernelCache = false };

        // Assert
        optionsWithClear.ClearKernelCache.Should().BeTrue();
        optionsWithoutClear.ClearKernelCache.Should().BeFalse();
    }

    [Fact]
    public void Reinitialize_CanBeToggled()
    {
        // Act
        var optionsWithReinit = new ResetOptions { ResetType = ResetType.Full, Reinitialize = true };
        var optionsWithoutReinit = new ResetOptions { ResetType = ResetType.Full, Reinitialize = false };

        // Assert
        optionsWithReinit.Reinitialize.Should().BeTrue();
        optionsWithoutReinit.Reinitialize.Should().BeFalse();
    }

    [Fact]
    public void CustomOptions_CanCombineSettings()
    {
        // Act
        var options = new ResetOptions
        {
            ResetType = ResetType.Hard,
            WaitForCompletion = false,
            Timeout = TimeSpan.FromMinutes(2),
            ClearMemoryPool = true,
            ClearKernelCache = false,
            Reinitialize = false
        };

        // Assert
        options.ResetType.Should().Be(ResetType.Hard);
        options.WaitForCompletion.Should().BeFalse();
        options.Timeout.Should().Be(TimeSpan.FromMinutes(2));
        options.ClearMemoryPool.Should().BeTrue();
        options.ClearKernelCache.Should().BeFalse();
        options.Reinitialize.Should().BeFalse();
    }
}
