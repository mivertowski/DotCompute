// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for MessageQueueBridge - background pump thread that transfers messages from CPU to GPU.
/// </summary>
public sealed class MessageQueueBridgeTests : IDisposable
{
    private readonly MockMessageQueue<BridgeTestMessage> _namedQueue;
    private readonly MockGpuTransferFunction _gpuTransferFunc;
    private readonly MessageQueueOptions _options;
    private MessageQueueBridge<BridgeTestMessage>? _bridge;
    private bool _disposed;

    public MessageQueueBridgeTests()
    {
        _namedQueue = new MockMessageQueue<BridgeTestMessage>();
        _gpuTransferFunc = new MockGpuTransferFunction();
        _options = new MessageQueueOptions
        {
            Capacity = 1024,
            BackpressureStrategy = BackpressureStrategy.Block
        };
    }

    [Fact]
    public void Constructor_ValidParameters_InitializesBridge()
    {
        // Arrange & Act
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>(),
            NullLogger.Instance);

        // Assert
        Assert.NotNull(_bridge);
        Assert.Equal(_namedQueue, _bridge.NamedQueue);
        Assert.Equal(0, _bridge.MessagesTransferred);
        Assert.Equal(0, _bridge.MessagesSerialized);
        Assert.Equal(0, _bridge.MessagesDropped);
        Assert.Equal(0, _bridge.BytesTransferred);
        Assert.True(_bridge.Uptime.TotalMilliseconds >= 0);
    }

    [Fact]
    public void Constructor_NullNamedQueue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageQueueBridge<BridgeTestMessage>(
                null!,
                _gpuTransferFunc.TransferAsync,
                _options));
    }

    [Fact]
    public void Constructor_NullGpuTransferFunc_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageQueueBridge<BridgeTestMessage>(
                _namedQueue,
                null!,
                _options));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageQueueBridge<BridgeTestMessage>(
                _namedQueue,
                _gpuTransferFunc.TransferAsync,
                null!));
    }

    [Fact]
    public async Task PumpThread_SingleMessage_TransfersToGpu()
    {
        // Arrange
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        // Give pump thread time to start
        await Task.Delay(100);

        var message = new BridgeTestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128, // Normal priority
            Payload = "Test message"
        };

        // Act
        await _namedQueue.EnqueueAsync(message, CancellationToken.None);

        // Wait for pump thread to process with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_bridge.MessagesTransferred < 1 && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        Assert.Equal(1, _bridge.MessagesTransferred);
        Assert.Equal(1, _bridge.MessagesSerialized);
        Assert.Equal(0, _bridge.MessagesDropped);
        Assert.True(_bridge.BytesTransferred > 0);
        Assert.Equal(1, _gpuTransferFunc.TransferCount);
    }

    [Fact]
    public async Task PumpThread_MultipleMessages_TransfersInBatches()
    {
        // Arrange
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        var messageCount = 10;
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => new BridgeTestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 128, // Normal priority
                Payload = $"Message {i}"
            })
            .ToList();

        // Act
        foreach (var message in messages)
        {
            await _namedQueue.EnqueueAsync(message, CancellationToken.None);
        }

        // Wait for pump thread to process all messages with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_bridge.MessagesTransferred < messageCount && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        Assert.Equal(messageCount, _bridge.MessagesTransferred);
        Assert.Equal(messageCount, _bridge.MessagesSerialized);
        Assert.Equal(0, _bridge.MessagesDropped);
        Assert.True(_bridge.MessagesPerSecond > 0);
        Assert.True(_bridge.MegabytesPerSecond >= 0);
    }

    [Fact]
    public async Task PumpThread_GpuTransferFailure_DropsMessages()
    {
        // Arrange
        _gpuTransferFunc.ShouldFail = true;
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        var message = new BridgeTestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128, // Normal priority
            Payload = "Test message"
        };

        // Act
        await _namedQueue.EnqueueAsync(message, CancellationToken.None);

        // Wait for pump thread to process with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_gpuTransferFunc.TransferCount < 1 && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        Assert.Equal(0, _bridge.MessagesTransferred);
        Assert.True(_bridge.MessagesDropped > 0);
        Assert.Equal(1, _gpuTransferFunc.TransferCount);
    }

    [Fact]
    public async Task PumpThread_HighThroughput_MaintainsPerformance()
    {
        // Arrange
        var highCapacityOptions = new MessageQueueOptions
        {
            Capacity = 10000,
            BackpressureStrategy = BackpressureStrategy.Block
        };

        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            highCapacityOptions,
            new MockMessageSerializer<BridgeTestMessage>());

        var messageCount = 1000;
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => new BridgeTestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 128, // Normal priority
                Payload = $"High throughput message {i}"
            })
            .ToList();

        // Act
        var startTime = DateTime.UtcNow;
        foreach (var message in messages)
        {
            await _namedQueue.EnqueueAsync(message, CancellationToken.None);
        }

        // Wait for all messages to be processed
        var timeout = TimeSpan.FromSeconds(5);
        while (_bridge.MessagesTransferred < messageCount && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        Assert.Equal(messageCount, _bridge.MessagesTransferred);
        Assert.True(_bridge.MessagesPerSecond > 100, $"Expected >100 msg/s, got {_bridge.MessagesPerSecond:F2}");
        Assert.Equal(0, _bridge.MessagesDropped);
    }

    [Fact]
    public async Task Metrics_MessagesPerSecond_CalculatesCorrectly()
    {
        // Arrange
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        var messageCount = 50;
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => new BridgeTestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 128, // Normal priority
                Payload = $"Message {i}"
            })
            .ToList();

        // Act
        foreach (var message in messages)
        {
            await _namedQueue.EnqueueAsync(message, CancellationToken.None);
        }

        // Wait for processing with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_bridge.MessagesTransferred < messageCount && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        Assert.True(_bridge.Uptime.TotalSeconds > 0);
        Assert.True(_bridge.MessagesPerSecond > 0);
        var expectedRate = _bridge.MessagesTransferred / _bridge.Uptime.TotalSeconds;
        Assert.Equal(expectedRate, _bridge.MessagesPerSecond, precision: 1); // 1 decimal place
    }

    [Fact]
    public async Task Metrics_MegabytesPerSecond_CalculatesCorrectly()
    {
        // Arrange
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        var message = new BridgeTestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128, // Normal priority
            Payload = "Test"
        };

        // Act
        await _namedQueue.EnqueueAsync(message, CancellationToken.None);

        // Wait for processing with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_bridge.MessagesTransferred < 1 && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        var expectedMBps = (_bridge.BytesTransferred / (1024.0 * 1024.0)) / _bridge.Uptime.TotalSeconds;
        Assert.Equal(expectedMBps, _bridge.MegabytesPerSecond, precision: 2); // 2 decimal places
    }

    [Fact]
    public async Task Backpressure_BlockStrategy_ReenqueuesMessages()
    {
        // Arrange
        var blockOptions = new MessageQueueOptions
        {
            Capacity = 5, // Small capacity to trigger backpressure
            BackpressureStrategy = BackpressureStrategy.Block
        };

        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            blockOptions,
            new MockMessageSerializer<BridgeTestMessage>());

        // Act - Enqueue more messages than capacity
        for (var i = 0; i < 10; i++)
        {
            await _namedQueue.EnqueueAsync(new BridgeTestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 128, // Normal priority
                Payload = $"Message {i}"
            }, CancellationToken.None);
        }

        // Wait for processing with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_bridge.MessagesTransferred < 1 && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert - All messages should eventually be transferred (re-enqueued on backpressure)
        Assert.True(_bridge.MessagesTransferred > 0);
        // Some messages might be dropped due to staging buffer full, but Block strategy attempts re-enqueue
    }

    [Fact]
    public async Task Dispose_StopsPumpThread_CleansUpResources()
    {
        // Arrange
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        await _namedQueue.EnqueueAsync(new BridgeTestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128, // Normal priority
            Payload = "Test"
        }, CancellationToken.None);

        // Wait for first message to be processed
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_bridge.MessagesTransferred < 1 && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        var messagesTransferred = _bridge.MessagesTransferred;

        // Act
        await _bridge.DisposeAsync();

        // Enqueue after disposal
        await _namedQueue.EnqueueAsync(new BridgeTestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128,
            Payload = "After disposal"
        }, CancellationToken.None);

        await Task.Delay(100);

        // Assert - No new messages should be transferred after disposal
        Assert.Equal(messagesTransferred, _bridge.MessagesTransferred);
    }

    [Fact]
    public async Task Dispose_MultipleCalls_NoException()
    {
        // Arrange
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            new MockMessageSerializer<BridgeTestMessage>());

        // Act & Assert - Should not throw
        await _bridge.DisposeAsync();
        await _bridge.DisposeAsync();
        await _bridge.DisposeAsync();
    }

    [Fact]
    public async Task Serialization_UsesProvidedSerializer()
    {
        // Arrange
        var customSerializer = new MockMessageSerializer<BridgeTestMessage>();
        _bridge = new MessageQueueBridge<BridgeTestMessage>(
            _namedQueue,
            _gpuTransferFunc.TransferAsync,
            _options,
            customSerializer);

        // Act
        await _namedQueue.EnqueueAsync(new BridgeTestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128, // Normal priority
            Payload = "Test"
        }, CancellationToken.None);

        // Wait for serialization with timeout
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (customSerializer.SerializeCallCount < 1 && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        // Assert
        Assert.True(customSerializer.SerializeCallCount > 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _bridge?.DisposeAsync().AsTask().Wait();
        _namedQueue?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Mock message queue for testing.
/// </summary>
internal sealed class MockMessageQueue<T> : IMessageQueue<T>
    where T : IRingKernelMessage, new()
{
    private readonly Queue<T> _queue = new();
    private readonly object _lock = new();
    private readonly int _capacity;
    private bool _disposed;

    public MockMessageQueue(int capacity = 1024)
    {
        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public int Capacity => _capacity;

    public bool IsFull
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count >= _capacity;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count == 0;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }

    public Task<bool> EnqueueAsync(T message, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _queue.Enqueue(message);
            return Task.FromResult(true);
        }
    }

    public bool TryDequeue(out T? message)
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                message = _queue.Dequeue();
                return true;
            }

            message = default;
            return false;
        }
    }

    public bool TryEnqueue(T message, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_queue.Count >= _capacity)
            {
                return false;
            }

            _queue.Enqueue(message);
            return true;
        }
    }

    public bool TryPeek(out T? message)
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                message = _queue.Peek();
                return true;
            }

            message = default;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            _queue.Clear();
        }

        _disposed = true;
    }
}

/// <summary>
/// Mock GPU transfer function for testing.
/// </summary>
internal sealed class MockGpuTransferFunction
{
    private int _transferCount;

    public int TransferCount => _transferCount;
    public bool ShouldFail { get; set; }

    public async Task<bool> TransferAsync(ReadOnlyMemory<byte> data)
    {
        Interlocked.Increment(ref _transferCount);

        if (ShouldFail)
        {
            return false;
        }

        // Simulate GPU transfer delay
        await Task.Delay(1).ConfigureAwait(false);

        return true;
    }
}

/// <summary>
/// Mock message serializer for testing.
/// </summary>
internal sealed class MockMessageSerializer<T> : IMessageSerializer<T>
    where T : IRingKernelMessage, new()
{
    private int _serializeCallCount;

    public int MaxSerializedSize => 64; // Must match PinnedStagingBuffer message size
    public int SerializeCallCount => _serializeCallCount;

    public int Serialize(T message, Span<byte> destination)
    {
        Interlocked.Increment(ref _serializeCallCount);

        // Simple serialization: write message ID bytes and pad to MaxSerializedSize
        var messageIdBytes = message.MessageId.ToByteArray();
        messageIdBytes.CopyTo(destination);

        // Clear remaining bytes (padding)
        destination[messageIdBytes.Length..MaxSerializedSize].Clear();

        // Return full message size (PinnedStagingBuffer requires exact size match)
        return MaxSerializedSize;
    }

    public T Deserialize(ReadOnlySpan<byte> source)
    {
        var messageId = new Guid(source[..16]);
        return new T
        {
            MessageId = messageId,
            Priority = 128 // Normal priority
        };
    }

    public int GetSerializedSize(T message) => MaxSerializedSize;
}

/// <summary>
/// Test message for MessageQueueBridge unit tests.
/// </summary>
internal sealed class BridgeTestMessage : IRingKernelMessage
{
    private byte[] _serializedBuffer = Array.Empty<byte>();

    public Guid MessageId { get; set; }
    public string MessageType => "BridgeTestMessage";
    public byte Priority { get; set; } = 128; // Normal priority
    public Guid? CorrelationId { get; set; }
    public string Payload { get; set; } = string.Empty;

    public int PayloadSize
    {
        get
        {
            // MessageId (16) + Priority (1) + CorrelationId (17 max) + Payload length
            var payloadBytes = Encoding.UTF8.GetByteCount(Payload);
            return 16 + 1 + 17 + payloadBytes;
        }
    }

    public ReadOnlySpan<byte> Serialize()
    {
        // Simple serialization for testing
        var payloadBytes = Encoding.UTF8.GetBytes(Payload);
        _serializedBuffer = new byte[16 + 1 + payloadBytes.Length];

        MessageId.TryWriteBytes(_serializedBuffer.AsSpan(0, 16));
        _serializedBuffer[16] = Priority;
        payloadBytes.CopyTo(_serializedBuffer, 17);

        return _serializedBuffer;
    }

    public void Deserialize(ReadOnlySpan<byte> data)
    {
        MessageId = new Guid(data[..16]);
        Priority = data[16];
        Payload = Encoding.UTF8.GetString(data[17..]);
    }
}
