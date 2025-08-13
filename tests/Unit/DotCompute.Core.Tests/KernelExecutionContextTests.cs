using Xunit;
using FluentAssertions;
using Xunit;

namespace DotCompute.Tests.Unit;

public class KernelExecutionContextTests
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
        context.Name.Should().Be(name);
        context.Arguments.Should().BeSameAs(args);
        context.WorkDimensions.Should().BeEquivalentTo(workDims);
        context.LocalWorkSize.Should().BeEquivalentTo(localWorkSize);
        context.CancellationToken.Should().Be(cancellationToken);
    }

    [Fact]
    public void Constructor_WithRequiredName_ShouldInitialize()
    {
        // Act & Assert
        var act = () => new KernelExecutionContext { Name = "TestKernel" };
        act.Should().NotThrow();
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
            2 => new long[] { 256, 256 },
            3 => new long[] { 64, 64, 64 },
            _ => throw new ArgumentException("Invalid dimensions", nameof(dimensions))
        };

        // Act
        var context = new KernelExecutionContext
        {
            Name = "TestKernel",
            WorkDimensions = workDims
        };

        // Assert
        context.WorkDimensions.Should().BeEquivalentTo(workDims);
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
        context.LocalWorkSize.Should().BeNull();
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
        context1D.LocalWorkSize.Should().BeEquivalentTo(sizes1D);

        var context2D = new KernelExecutionContext
        {
            Name = "TestKernel",
            LocalWorkSize = sizes2D
        };
        context2D.LocalWorkSize.Should().BeEquivalentTo(sizes2D);

        var context3D = new KernelExecutionContext
        {
            Name = "TestKernel",
            LocalWorkSize = sizes3D
        };
        context3D.LocalWorkSize.Should().BeEquivalentTo(sizes3D);
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
        context.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }
}
