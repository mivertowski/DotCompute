using Xunit;
using FluentAssertions;
using DotCompute.Core;

namespace DotCompute.Core.Tests;


public sealed class CommandQueueOptionsTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        // Act
        var options = CommandQueueOptions.Default;

        // Assert
        _ = options.EnableProfiling.Should().BeFalse();
        _ = options.EnableOutOfOrderExecution.Should().BeFalse();
        _ = options.Priority.Should().Be(QueuePriority.Normal);
    }

    [Fact]
    public void Constructor_WithInitializer_ShouldSetProperties()
    {
        // Act
        var options = new CommandQueueOptions
        {
            EnableProfiling = true,
            EnableOutOfOrderExecution = true,
            Priority = QueuePriority.High
        };

        // Assert
        _ = options.EnableProfiling.Should().BeTrue();
        _ = options.EnableOutOfOrderExecution.Should().BeTrue();
        _ = options.Priority.Should().Be(QueuePriority.High);
    }

    [Theory]
    [InlineData(QueuePriority.Low)]
    [InlineData(QueuePriority.Normal)]
    [InlineData(QueuePriority.High)]
    public void Priority_ShouldAcceptAllValues(QueuePriority priority)
    {
        // Act
        var options = new CommandQueueOptions { Priority = priority };

        // Assert
        _ = options.Priority.Should().Be(priority);
    }

    [Fact]
    public void EnableProfiling_WhenTrue_ShouldAllowProfiling()
    {
        // Act
        var options = new CommandQueueOptions { EnableProfiling = true };

        // Assert
        _ = options.EnableProfiling.Should().BeTrue();
    }

    [Fact]
    public void EnableOutOfOrderExecution_WhenTrue_ShouldAllowOutOfOrder()
    {
        // Act
        var options = new CommandQueueOptions { EnableOutOfOrderExecution = true };

        // Assert
        _ = options.EnableOutOfOrderExecution.Should().BeTrue();
    }

    [Fact]
    public void MultipleInstances_ShouldBeIndependent()
    {
        // Arrange
        var options1 = new CommandQueueOptions
        {
            EnableProfiling = true,
            Priority = QueuePriority.High
        };

        var options2 = new CommandQueueOptions
        {
            EnableProfiling = false,
            Priority = QueuePriority.Low
        };

        // Assert
        _ = options1.EnableProfiling.Should().BeTrue();
        _ = options1.Priority.Should().Be(QueuePriority.High);
        _ = options2.EnableProfiling.Should().BeFalse();
        _ = options2.Priority.Should().Be(QueuePriority.Low);
    }

    [Fact]
    public void DefaultInstance_ShouldNotBeModifiable()
    {
        // Act
        var defaultOptions = CommandQueueOptions.Default;
        _ = new CommandQueueOptions
        {
            EnableProfiling = true,
            EnableOutOfOrderExecution = true,
            Priority = QueuePriority.High
        };

        // Assert
        _ = defaultOptions.EnableProfiling.Should().BeFalse();
        _ = defaultOptions.EnableOutOfOrderExecution.Should().BeFalse();
        _ = defaultOptions.Priority.Should().Be(QueuePriority.Normal);
    }
}
