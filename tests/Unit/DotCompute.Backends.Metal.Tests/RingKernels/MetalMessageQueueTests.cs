// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
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
        var capacity = 256;
        var logger = NullLogger<MetalMessageQueue<TestMessage>>.Instance;

        // Act
        var queue = new MetalMessageQueue<TestMessage>(capacity, logger);

        // Assert
        Assert.NotNull(queue);
        Assert.Equal(256, queue.Capacity);
    }

    [Fact]
    public void Create_Should_Throw_When_Logger_Is_Null()
    {
        // Arrange
        var capacity = 256;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MetalMessageQueue<TestMessage>(capacity, null!));
    }

    [Fact]
    public void Capacity_Should_Be_Power_Of_Two()
    {
        // Arrange
        var capacity = 255; // Not a power of 2
        var logger = NullLogger<MetalMessageQueue<TestMessage>>.Instance;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new MetalMessageQueue<TestMessage>(capacity, logger));
    }

    private struct TestMessage
    {
        public int Value { get; set; }
    }
}
