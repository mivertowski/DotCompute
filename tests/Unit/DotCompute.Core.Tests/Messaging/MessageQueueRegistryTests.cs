// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for MessageQueueRegistry centralized queue management.
/// Covers registration, retrieval, type safety, backend filtering, and disposal.
/// </summary>
public sealed class MessageQueueRegistryTests : IDisposable
{
    private readonly MessageQueueRegistry _registry;
    private readonly ILogger<MessageQueueRegistry> _logger;
    private bool _disposed;

    public MessageQueueRegistryTests()
    {
        _logger = NullLogger<MessageQueueRegistry>.Instance;
        _registry = new MessageQueueRegistry(_logger);
    }

    #region Registration Tests

    [Fact]
    public void TryRegister_ValidQueue_ReturnsTrue()
    {
        // Arrange
        var queue = CreateTestQueue();

        // Act
        var result = _registry.TryRegister("test-queue", queue, "CPU");

        // Assert
        Assert.True(result);
        Assert.Equal(1, _registry.Count);
    }

    [Fact]
    public void TryRegister_DuplicateName_ReturnsFalse()
    {
        // Arrange
        var queue1 = CreateTestQueue();
        var queue2 = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue1, "CPU");

        // Act
        var result = _registry.TryRegister("test-queue", queue2, "CUDA");

        // Assert
        Assert.False(result);
        Assert.Equal(1, _registry.Count);
    }

    [Fact]
    public void TryRegister_NullQueueName_ThrowsArgumentException()
    {
        // Arrange
        var queue = CreateTestQueue();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.TryRegister(null!, queue, "CPU"));
        Assert.Throws<ArgumentException>(() => _registry.TryRegister(string.Empty, queue, "CPU"));
        Assert.Throws<ArgumentException>(() => _registry.TryRegister("   ", queue, "CPU"));
    }

    [Fact]
    public void TryRegister_NullQueue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _registry.TryRegister<TestMessage>("test-queue", null!, "CPU"));
    }

    [Fact]
    public void TryRegister_WithoutBackend_UsesUnknown()
    {
        // Arrange
        var queue = CreateTestQueue();

        // Act
        _ = _registry.TryRegister("test-queue", queue);
        var metadata = _registry.GetMetadata("test-queue");

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("Unknown", metadata.Backend);
    }

    #endregion

    #region Retrieval Tests

    [Fact]
    public void TryGet_ExistingQueue_ReturnsQueue()
    {
        // Arrange
        var queue = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue, "CPU");

        // Act
        var retrieved = _registry.TryGet<TestMessage>("test-queue");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Same(queue, retrieved);
    }

    [Fact]
    public void TryGet_NonExistentQueue_ReturnsNull()
    {
        // Act
        var retrieved = _registry.TryGet<TestMessage>("nonexistent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void TryGet_WrongType_ReturnsNull()
    {
        // Arrange
        var queue = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue, "CPU");

        // Act
        var retrieved = _registry.TryGet<TestMessage2>("test-queue");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void TryGet_NullQueueName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.TryGet<TestMessage>(null!));
        Assert.Throws<ArgumentException>(() => _registry.TryGet<TestMessage>(string.Empty));
        Assert.Throws<ArgumentException>(() => _registry.TryGet<TestMessage>("   "));
    }

    #endregion

    #region Unregistration Tests

    [Fact]
    public void TryUnregister_ExistingQueue_ReturnsTrue()
    {
        // Arrange
        var queue = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue, "CPU");

        // Act
        var result = _registry.TryUnregister("test-queue", disposeQueue: false);

        // Assert
        Assert.True(result);
        Assert.Equal(0, _registry.Count);
    }

    [Fact]
    public void TryUnregister_NonExistentQueue_ReturnsFalse()
    {
        // Act
        var result = _registry.TryUnregister("nonexistent", disposeQueue: false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryUnregister_WithDispose_DisposesQueue()
    {
        // Arrange
        var queue = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue, "CPU");

        // Act
        var result = _registry.TryUnregister("test-queue", disposeQueue: true);

        // Assert
        Assert.True(result);
        Assert.Throws<ObjectDisposedException>(() => queue.TryEnqueue(new TestMessage()));
    }

    [Fact]
    public void TryUnregister_NullQueueName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.TryUnregister(null!));
        Assert.Throws<ArgumentException>(() => _registry.TryUnregister(string.Empty));
        Assert.Throws<ArgumentException>(() => _registry.TryUnregister("   "));
    }

    #endregion

    #region List Operations Tests

    [Fact]
    public void ListQueues_EmptyRegistry_ReturnsEmptyCollection()
    {
        // Act
        var queues = _registry.ListQueues();

        // Assert
        Assert.Empty(queues);
    }

    [Fact]
    public void ListQueues_MultipleQueues_ReturnsAllNames()
    {
        // Arrange
        _ = _registry.TryRegister("queue1", CreateTestQueue(), "CPU");
        _ = _registry.TryRegister("queue2", CreateTestQueue(), "CUDA");
        _ = _registry.TryRegister("queue3", CreateTestQueue(), "OpenCL");

        // Act
        var queues = _registry.ListQueues();

        // Assert
        Assert.Equal(3, queues.Count);
        Assert.Contains("queue1", queues);
        Assert.Contains("queue2", queues);
        Assert.Contains("queue3", queues);
    }

    [Fact]
    public void ListQueuesByBackend_EmptyRegistry_ReturnsEmptyCollection()
    {
        // Act
        var queues = _registry.ListQueuesByBackend("CPU");

        // Assert
        Assert.Empty(queues);
    }

    [Fact]
    public void ListQueuesByBackend_FiltersByBackend()
    {
        // Arrange
        _ = _registry.TryRegister("cpu1", CreateTestQueue(), "CPU");
        _ = _registry.TryRegister("cpu2", CreateTestQueue(), "CPU");
        _ = _registry.TryRegister("cuda1", CreateTestQueue(), "CUDA");

        // Act
        var cpuQueues = _registry.ListQueuesByBackend("CPU");
        var cudaQueues = _registry.ListQueuesByBackend("CUDA");

        // Assert
        Assert.Equal(2, cpuQueues.Count);
        Assert.Contains("cpu1", cpuQueues);
        Assert.Contains("cpu2", cpuQueues);
        Assert.Single(cudaQueues);
        Assert.Contains("cuda1", cudaQueues);
    }

    [Fact]
    public void ListQueuesByBackend_CaseInsensitive()
    {
        // Arrange
        _ = _registry.TryRegister("queue1", CreateTestQueue(), "CPU");

        // Act
        var queues = _registry.ListQueuesByBackend("cpu");

        // Assert
        Assert.Single(queues);
        Assert.Contains("queue1", queues);
    }

    [Fact]
    public void ListQueuesByBackend_NullBackend_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.ListQueuesByBackend(null!));
        Assert.Throws<ArgumentException>(() => _registry.ListQueuesByBackend(string.Empty));
        Assert.Throws<ArgumentException>(() => _registry.ListQueuesByBackend("   "));
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void GetMetadata_ExistingQueue_ReturnsMetadata()
    {
        // Arrange
        var queue = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue, "CPU");

        // Act
        var metadata = _registry.GetMetadata("test-queue");

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("test-queue", metadata.QueueName);
        Assert.Equal(typeof(TestMessage), metadata.MessageType);
        Assert.Equal("CPU", metadata.Backend);
        Assert.Equal(16, metadata.Capacity);
        Assert.Equal(0, metadata.Count);
    }

    [Fact]
    public void GetMetadata_NonExistentQueue_ReturnsNull()
    {
        // Act
        var metadata = _registry.GetMetadata("nonexistent");

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public void GetMetadata_NullQueueName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.GetMetadata(null!));
        Assert.Throws<ArgumentException>(() => _registry.GetMetadata(string.Empty));
        Assert.Throws<ArgumentException>(() => _registry.GetMetadata("   "));
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllQueues()
    {
        // Arrange
        _ = _registry.TryRegister("queue1", CreateTestQueue(), "CPU");
        _ = _registry.TryRegister("queue2", CreateTestQueue(), "CUDA");

        // Act
        _registry.Clear(disposeQueues: false);

        // Assert
        Assert.Equal(0, _registry.Count);
        Assert.Empty(_registry.ListQueues());
    }

    [Fact]
    public void Clear_WithDispose_DisposesAllQueues()
    {
        // Arrange
        var queue1 = CreateTestQueue();
        var queue2 = CreateTestQueue();
        _ = _registry.TryRegister("queue1", queue1, "CPU");
        _ = _registry.TryRegister("queue2", queue2, "CUDA");

        // Act
        _registry.Clear(disposeQueues: true);

        // Assert
        Assert.Equal(0, _registry.Count);
        Assert.Throws<ObjectDisposedException>(() => queue1.TryEnqueue(new TestMessage()));
        Assert.Throws<ObjectDisposedException>(() => queue2.TryEnqueue(new TestMessage()));
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ClearsAndDisposesAllQueues()
    {
        // Arrange
        var queue = CreateTestQueue();
        _ = _registry.TryRegister("test-queue", queue, "CPU");

        // Act
        _registry.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => _registry.Count);
        Assert.Throws<ObjectDisposedException>(() => queue.TryEnqueue(new TestMessage()));
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Act & Assert
        _registry.Dispose();
        _registry.Dispose(); // Should not throw
    }

    [Fact]
    public void AfterDispose_AllMethodsThrow()
    {
        // Arrange
        var queue = CreateTestQueue();
        _registry.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _registry.TryRegister("test", queue, "CPU"));
        Assert.Throws<ObjectDisposedException>(() => _registry.TryGet<TestMessage>("test"));
        Assert.Throws<ObjectDisposedException>(() => _registry.TryUnregister("test"));
        Assert.Throws<ObjectDisposedException>(() => _registry.ListQueues());
        Assert.Throws<ObjectDisposedException>(() => _registry.ListQueuesByBackend("CPU"));
        Assert.Throws<ObjectDisposedException>(() => _registry.GetMetadata("test"));
        Assert.Throws<ObjectDisposedException>(() => _registry.Clear());
        Assert.Throws<ObjectDisposedException>(() => _ = _registry.Count);
    }

    #endregion

    #region Count Property Tests

    [Fact]
    public void Count_EmptyRegistry_ReturnsZero()
    {
        // Assert
        Assert.Equal(0, _registry.Count);
    }

    [Fact]
    public void Count_AfterRegistrations_ReturnsCorrectValue()
    {
        // Arrange & Act
        _ = _registry.TryRegister("queue1", CreateTestQueue(), "CPU");
        var count1 = _registry.Count;

        _ = _registry.TryRegister("queue2", CreateTestQueue(), "CUDA");
        var count2 = _registry.Count;

        _ = _registry.TryUnregister("queue1");
        var count3 = _registry.Count;

        // Assert
        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
        Assert.Equal(1, count3);
    }

    #endregion

    #region Helper Methods

    private static MessageQueue<TestMessage> CreateTestQueue()
    {
        var options = new MessageQueueOptions
        {
            Capacity = 16,
            BackpressureStrategy = BackpressureStrategy.Reject,
            EnableDeduplication = false
        };
        return new MessageQueue<TestMessage>(options);
    }

    #endregion

    #region Test Message Types

    private sealed class TestMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => "TestMessage";
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 16; // MessageId only

        public ReadOnlySpan<byte> Serialize() => MessageId.ToByteArray();

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length >= 16)
            {
                MessageId = new Guid(data[..16]);
            }
        }
    }

    private sealed class TestMessage2 : IRingKernelMessage
    {
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => "TestMessage2";
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 16; // MessageId only

        public ReadOnlySpan<byte> Serialize() => MessageId.ToByteArray();

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length >= 16)
            {
                MessageId = new Guid(data[..16]);
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _registry?.Dispose();
        _disposed = true;
    }

    #endregion
}
