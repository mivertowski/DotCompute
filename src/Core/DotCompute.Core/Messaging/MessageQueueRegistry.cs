// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Centralized registry for managing named message queues across all backends.
/// Provides thread-safe registration, discovery, and lifecycle management.
/// </summary>
/// <remarks>
/// <para><b>Performance Note:</b> Logging in this class uses direct ILogger calls
/// rather than LoggerMessage.Define since this is infrastructure code not in hot paths.
/// XFIX003 analyzer warnings are suppressed for this file.</para>
///
/// <para>
/// This registry acts as a global namespace for message queues, allowing:
/// - Cross-backend queue sharing (CPU ↔ CUDA ↔ OpenCL ↔ Metal)
/// - Queue discovery and enumeration
/// - Centralized lifecycle management
/// - Type-safe queue retrieval
/// </para>
///
/// <para><b>Thread Safety:</b> All operations are thread-safe using ConcurrentDictionary.</para>
///
/// <para><b>Disposal:</b> Disposing the registry disposes all registered queues.</para>
/// </remarks>
public sealed class MessageQueueRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, QueueEntry> _queues = new();
    private readonly ILogger<MessageQueueRegistry> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueRegistry"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MessageQueueRegistry(ILogger<MessageQueueRegistry>? logger = null)
    {
        _logger = logger ?? NullLogger<MessageQueueRegistry>.Instance;
        _logger.LogInformation("Message queue registry initialized");
    }

    /// <summary>
    /// Registers a message queue with the specified name.
    /// </summary>
    /// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="queueName">Unique queue name.</param>
    /// <param name="queue">Queue instance to register.</param>
    /// <param name="backend">Optional backend identifier (e.g., "CPU", "CUDA").</param>
    /// <returns>True if registration succeeded; false if a queue with the same name already exists.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="queueName"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="queue"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code not in hot path")]
    public bool TryRegister<T>(string queueName, IMessageQueue<T> queue, string? backend = null)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(queue);
        ThrowIfDisposed();

        var entry = new QueueEntry
        {
            QueueName = queueName,
            MessageType = typeof(T),
            Queue = queue,
            Backend = backend ?? "Unknown",
            RegisteredAt = DateTime.UtcNow
        };

        if (_queues.TryAdd(queueName, entry))
        {
            _logger.LogInformation(
                "Registered message queue '{QueueName}' for type {MessageType} on backend {Backend}",
                queueName, typeof(T).Name, entry.Backend);
            return true;
        }

        _logger.LogWarning("Queue '{QueueName}' already registered", queueName);
        return false;
    }

    /// <summary>
    /// Registers a message queue with the specified name using reflection.
    /// </summary>
    /// <param name="messageType">Message type (must implement <see cref="IRingKernelMessage"/>).</param>
    /// <param name="queueName">Unique queue name.</param>
    /// <param name="queue">Queue instance to register (must be IMessageQueue&lt;T&gt; where T is messageType).</param>
    /// <param name="backend">Optional backend identifier (e.g., "CPU", "CUDA").</param>
    /// <returns>True if registration succeeded; false if a queue with the same name already exists.</returns>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code not in hot path")]
    public bool TryRegister(Type messageType, string queueName, object queue, string? backend = null)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(queue);
        ThrowIfDisposed();

        if (!typeof(IRingKernelMessage).IsAssignableFrom(messageType))
        {
            throw new ArgumentException(
                $"Message type {messageType.Name} must implement IRingKernelMessage",
                nameof(messageType));
        }

        var entry = new QueueEntry
        {
            QueueName = queueName,
            MessageType = messageType,
            Queue = queue,
            Backend = backend ?? "Unknown",
            RegisteredAt = DateTime.UtcNow
        };

        if (_queues.TryAdd(queueName, entry))
        {
            _logger.LogInformation(
                "Registered message queue '{QueueName}' for type {MessageType} on backend {Backend}",
                queueName, messageType.Name, entry.Backend);
            return true;
        }

        _logger.LogWarning("Queue '{QueueName}' already registered", queueName);
        return false;
    }

    /// <summary>
    /// Retrieves a message queue by name with type safety.
    /// </summary>
    /// <typeparam name="T">Expected message type.</typeparam>
    /// <param name="queueName">Queue name to retrieve.</param>
    /// <returns>The queue if found and types match; otherwise null.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="queueName"/> is null or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code not in hot path")]
    public IMessageQueue<T>? TryGet<T>(string queueName) where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        if (_queues.TryGetValue(queueName, out var entry))
        {
            if (entry.MessageType != typeof(T))
            {
                _logger.LogWarning(
                    "Queue '{QueueName}' exists but has type {ActualType}, expected {ExpectedType}",
                    queueName, entry.MessageType.Name, typeof(T).Name);
                return null;
            }

            return (IMessageQueue<T>)entry.Queue;
        }

        return null;
    }

    /// <summary>
    /// Unregisters a message queue and optionally disposes it.
    /// </summary>
    /// <param name="queueName">Queue name to unregister.</param>
    /// <param name="disposeQueue">If true, disposes the queue after unregistering.</param>
    /// <returns>True if the queue was found and unregistered; false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="queueName"/> is null or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code not in hot path")]
    public bool TryUnregister(string queueName, bool disposeQueue = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        if (_queues.TryRemove(queueName, out var entry))
        {
            _logger.LogInformation("Unregistered message queue '{QueueName}'", queueName);

            if (disposeQueue && entry.Queue is IDisposable disposable)
            {
                disposable.Dispose();
                _logger.LogDebug("Disposed message queue '{QueueName}'", queueName);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Lists all registered queue names.
    /// </summary>
    /// <returns>Read-only collection of queue names.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    public IReadOnlyCollection<string> ListQueues()
    {
        ThrowIfDisposed();
        return _queues.Keys.ToList();
    }

    /// <summary>
    /// Lists all queues registered for a specific backend.
    /// </summary>
    /// <param name="backend">Backend identifier to filter by.</param>
    /// <returns>Read-only collection of queue names for the specified backend.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="backend"/> is null or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    public IReadOnlyCollection<string> ListQueuesByBackend(string backend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backend);
        ThrowIfDisposed();

        return _queues
            .Where(kvp => kvp.Value.Backend.Equals(backend, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets metadata about a registered queue.
    /// </summary>
    /// <param name="queueName">Queue name.</param>
    /// <returns>Queue metadata if found; otherwise null.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="queueName"/> is null or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    public QueueMetadata? GetMetadata(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ThrowIfDisposed();

        if (_queues.TryGetValue(queueName, out var entry))
        {
            return new QueueMetadata
            {
                QueueName = entry.QueueName,
                MessageType = entry.MessageType,
                Backend = entry.Backend,
                RegisteredAt = entry.RegisteredAt,
                Capacity = entry.Queue is IMessageQueue q ? q.Capacity : 0,
                Count = entry.Queue is IMessageQueue q2 ? q2.Count : 0
            };
        }

        return null;
    }

    /// <summary>
    /// Gets the total number of registered queues.
    /// </summary>
    public int Count
    {
        get
        {
            ThrowIfDisposed();
            return _queues.Count;
        }
    }

    /// <summary>
    /// Clears all registered queues and optionally disposes them.
    /// </summary>
    /// <param name="disposeQueues">If true, disposes all queues before clearing.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code not in hot path")]
    public void Clear(bool disposeQueues = true)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Clearing {Count} message queues (dispose={DisposeQueues})", _queues.Count, disposeQueues);

        if (disposeQueues)
        {
            foreach (var entry in _queues.Values)
            {
                if (entry.Queue is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing queue '{QueueName}'", entry.QueueName);
                    }
                }
            }
        }

        _queues.Clear();
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code not in hot path")]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing message queue registry with {Count} queues", _queues.Count);

        Clear(disposeQueues: true);
        _disposed = true;

        _logger.LogInformation("Message queue registry disposed");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Internal queue entry with metadata.
    /// </summary>
    private sealed class QueueEntry
    {
        public required string QueueName { get; init; }
        public required Type MessageType { get; init; }
        public required object Queue { get; init; }
        public required string Backend { get; init; }
        public DateTime RegisteredAt { get; init; }
    }
}

/// <summary>
/// Metadata about a registered message queue.
/// </summary>
public sealed class QueueMetadata
{
    /// <summary>
    /// Queue name.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Message type handled by this queue.
    /// </summary>
    public required Type MessageType { get; init; }

    /// <summary>
    /// Backend that registered this queue.
    /// </summary>
    public required string Backend { get; init; }

    /// <summary>
    /// UTC timestamp when the queue was registered.
    /// </summary>
    public DateTime RegisteredAt { get; init; }

    /// <summary>
    /// Queue capacity.
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Current number of messages in the queue.
    /// </summary>
    public int Count { get; init; }
}
