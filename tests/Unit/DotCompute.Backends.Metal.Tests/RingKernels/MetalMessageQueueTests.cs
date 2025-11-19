// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.Metal.RingKernels;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalMessageQueue.
/// </summary>
public sealed class MetalMessageQueueTests
{
    [Fact]
    public void Create_Should_Initialize_With_Correct_Capacity()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 256,
            MaxMessageSize = 1024
        };

        // Act
        var queue = MetalMessageQueue<TestMessage>.Create(IntPtr.Zero, options);

        // Assert
        Assert.NotNull(queue);
        Assert.Equal(256, queue.Capacity);
    }

    [Fact]
    public void Create_Should_Throw_When_Device_Is_Zero()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 256,
            MaxMessageSize = 1024
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MetalMessageQueue<TestMessage>.Create(IntPtr.Zero, options));
    }

    [Fact]
    public void Capacity_Should_Be_Power_Of_Two()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 255, // Not a power of 2
            MaxMessageSize = 1024
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MetalMessageQueue<TestMessage>.Create(new IntPtr(1), options));
    }

    private class TestMessage
    {
        public int Value { get; set; }
    }
}
