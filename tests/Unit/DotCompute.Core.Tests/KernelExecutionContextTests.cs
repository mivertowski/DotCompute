using DotCompute.Core;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;


public sealed class KernelExecutionContextTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeProperties()
    {
        // Arrange
        var name = "TestKernel";
        var args = new object[] { 1, 2.0f, "test" };
        var workDims = new long[] { 256, 256 };
        var localWorkSize = new long[] { 16, 16 };
        var cancellationToken = new CancellationToken();

        // Act
        var context = new KernelExecutionContext
        {
            Name = name,
            Arguments = args,
            WorkDimensions = workDims,
            LocalWorkSize = localWorkSize,
            CancellationToken = cancellationToken
        };

        // Assert
        _ = context.Name.Should().Be(name);
        _ = context.Arguments.Should().BeSameAs(args);
        _ = context.WorkDimensions.Should().BeEquivalentTo(workDims);
        _ = context.LocalWorkSize.Should().BeEquivalentTo(localWorkSize);
        _ = context.CancellationToken.Should().Be(cancellationToken);
    }

    [Fact]
    public void Constructor_WithRequiredName_ShouldInitialize()
    {
        // Act & Assert
        var act = () => new KernelExecutionContext { Name = "TestKernel" };
        _ = act(); // Should not throw
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void WorkDimensions_ShouldAcceptValidArrays(int dimensions)
    {
        // Arrange
        IReadOnlyList<long> workDims = dimensions switch
        {
            1 => new long[] { 256 },
            2 => [256, 256],
            3 => [64, 64, 64],
            _ => throw new ArgumentException("Invalid dimensions", nameof(dimensions))
        };

        // Act
        var context = new KernelExecutionContext
        {
            Name = "TestKernel",
            WorkDimensions = workDims
        };

        // Assert
        _ = context.WorkDimensions.Should().BeEquivalentTo(workDims);
    }

    [Fact]
    public void LocalWorkSize_CanBeNull()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "TestKernel",
            LocalWorkSize = null
        };

        // Assert
        _ = context.LocalWorkSize.Should().BeNull();
    }

    [Fact]
    public void LocalWorkSize_CanHaveDifferentDimensions()
    {
        // Arrange
        var sizes1D = new long[] { 64 };
        var sizes2D = new long[] { 16, 16 };
        var sizes3D = new long[] { 8, 8, 4 };

        // Act & Assert
        var context1D = new KernelExecutionContext
        {
            Name = "TestKernel",
            LocalWorkSize = sizes1D
        };
        _ = context1D.LocalWorkSize.Should().BeEquivalentTo(sizes1D);

        var context2D = new KernelExecutionContext
        {
            Name = "TestKernel",
            LocalWorkSize = sizes2D
        };
        _ = context2D.LocalWorkSize.Should().BeEquivalentTo(sizes2D);

        var context3D = new KernelExecutionContext
        {
            Name = "TestKernel",
            LocalWorkSize = sizes3D
        };
        _ = context3D.LocalWorkSize.Should().BeEquivalentTo(sizes3D);
    }

    [Fact]
    public void CancellationToken_CanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var context = new KernelExecutionContext
        {
            Name = "TestKernel",
            CancellationToken = cts.Token
        };

        // Act
        cts.Cancel();

        // Assert
        _ = context.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }
}
