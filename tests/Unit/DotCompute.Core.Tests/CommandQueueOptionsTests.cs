using Xunit;
using FluentAssertions;
using Xunit;
using DotCompute.Core;

namespace DotCompute.Tests.Unit;

public class CommandQueueOptionsTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        // Act
        var options = CommandQueueOptions.Default;

        // Assert
        options.EnableProfiling.Should().BeFalse();
        options.EnableOutOfOrderExecution.Should().BeFalse();
        options.Priority.Should().Be(QueuePriority.Normal);
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
        options.EnableProfiling.Should().BeTrue();
        options.EnableOutOfOrderExecution.Should().BeTrue();
        options.Priority.Should().Be(QueuePriority.High);
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
        options.Priority.Should().Be(priority);
    }

    [Fact]
    public void EnableProfiling_WhenTrue_ShouldAllowProfiling()
    {
        // Act
        var options = new CommandQueueOptions { EnableProfiling = true };

        // Assert
        options.EnableProfiling.Should().BeTrue();
    }

    [Fact]
    public void EnableOutOfOrderExecution_WhenTrue_ShouldAllowOutOfOrder()
    {
        // Act
        var options = new CommandQueueOptions { EnableOutOfOrderExecution = true };

        // Assert
        options.EnableOutOfOrderExecution.Should().BeTrue();
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
        options1.EnableProfiling.Should().BeTrue();
        options1.Priority.Should().Be(QueuePriority.High);
        options2.EnableProfiling.Should().BeFalse();
        options2.Priority.Should().Be(QueuePriority.Low);
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
        defaultOptions.EnableProfiling.Should().BeFalse();
        defaultOptions.EnableOutOfOrderExecution.Should().BeFalse();
        defaultOptions.Priority.Should().Be(QueuePriority.Normal);
    }
}
