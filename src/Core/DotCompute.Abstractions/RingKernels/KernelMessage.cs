// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Represents a message passed between ring kernels.
/// </summary>
/// <typeparam name="T">
/// The payload type. Must be an unmanaged type (blittable) for GPU transfer.
/// </typeparam>
/// <remarks>
/// KernelMessage is designed for efficient GPU memory transfers and atomic operations.
/// The structure is carefully laid out to avoid padding and ensure efficient memory access.
///
/// Message size should be kept small (typically 64-128 bytes) for optimal performance.
/// Large payloads should use indirect references (buffer indices) rather than embedding data.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct KernelMessage<T> : IEquatable<KernelMessage<T>> where T : unmanaged
{
    /// <summary>
    /// ID of the sender kernel instance.
    /// </summary>
    /// <remarks>
    /// Used for routing replies and tracking message origin.
    /// Sender IDs are assigned by the runtime when kernels are launched.
    /// </remarks>
    public int SenderId { get; init; }

    /// <summary>
    /// ID of the intended receiver kernel instance.
    /// </summary>
    /// <remarks>
    /// Used for message routing. A value of -1 indicates broadcast to all kernels.
    /// Specific IDs route to individual kernel instances.
    /// </remarks>
    public int ReceiverId { get; init; }

    /// <summary>
    /// Message type (Data, Control, Terminate, etc.).
    /// </summary>
    /// <remarks>
    /// Receivers can filter messages by type for priority handling.
    /// Control and lifecycle messages typically have priority over data messages.
    /// </remarks>
    public MessageType Type { get; init; }

    /// <summary>
    /// Application-specific payload data.
    /// </summary>
    /// <remarks>
    /// The payload type T must be unmanaged (no managed references).
    /// Common payload types:
    /// - Primitive types (int, float, etc.)
    /// - Small structs (Vector3, Matrix4x4)
    /// - Fixed-size arrays using 'fixed' keyword
    /// - Indirect references (buffer indices, offsets)
    /// </remarks>
    public T Payload { get; init; }

    /// <summary>
    /// Timestamp when the message was sent (microseconds since epoch).
    /// </summary>
    /// <remarks>
    /// Used for latency measurements, message ordering, and timeout detection.
    /// Set automatically by the message queue on enqueue.
    /// </remarks>
    public long Timestamp { get; init; }

    /// <summary>
    /// Sequence number for ordering and duplicate detection.
    /// </summary>
    /// <remarks>
    /// Monotonically increasing sequence number per sender.
    /// Enables detection of message reordering and lost messages.
    /// </remarks>
    public ulong SequenceNumber { get; init; }

    /// <summary>
    /// Creates a new message with specified properties.
    /// </summary>
    /// <param name="senderId">Sender kernel ID.</param>
    /// <param name="receiverId">Receiver kernel ID (-1 for broadcast).</param>
    /// <param name="type">Message type.</param>
    /// <param name="payload">Application payload.</param>
    /// <returns>A new KernelMessage instance.</returns>
    public static KernelMessage<T> Create(int senderId, int receiverId, MessageType type, T payload)
    {
        return new KernelMessage<T>
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Type = type,
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000, // Convert to microseconds
            SequenceNumber = 0 // Set by sender
        };
    }

    /// <summary>
    /// Creates a data message with simplified parameters.
    /// </summary>
    /// <param name="senderId">Sender kernel ID.</param>
    /// <param name="receiverId">Receiver kernel ID.</param>
    /// <param name="payload">Application payload.</param>
    /// <returns>A new data message.</returns>
    public static KernelMessage<T> CreateData(int senderId, int receiverId, T payload)
    {
        return Create(senderId, receiverId, MessageType.Data, payload);
    }

    /// <summary>
    /// Creates a control message.
    /// </summary>
    /// <param name="senderId">Sender kernel ID.</param>
    /// <param name="receiverId">Receiver kernel ID.</param>
    /// <param name="payload">Control payload.</param>
    /// <returns>A new control message.</returns>
    public static KernelMessage<T> CreateControl(int senderId, int receiverId, T payload)
    {
        return Create(senderId, receiverId, MessageType.Control, payload);
    }

    /// <summary>
    /// Creates a termination message.
    /// </summary>
    /// <param name="senderId">Sender kernel ID.</param>
    /// <param name="receiverId">Receiver kernel ID (-1 for all).</param>
    /// <returns>A new termination message.</returns>
    public static KernelMessage<T> CreateTerminate(int senderId, int receiverId)
    {
        return Create(senderId, receiverId, MessageType.Terminate, default);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KernelMessage<T> other && Equals(other);
    /// <inheritdoc/>
    public bool Equals(KernelMessage<T> other) =>
        SenderId == other.SenderId &&
        ReceiverId == other.ReceiverId &&
        Type == other.Type &&
        EqualityComparer<T>.Default.Equals(Payload, other.Payload) &&
        Timestamp == other.Timestamp &&
        SequenceNumber == other.SequenceNumber;
    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(SenderId, ReceiverId, Type, Payload, Timestamp, SequenceNumber);
    /// <summary>
    /// Determines whether two instances are equal.
    /// </summary>
    public static bool operator ==(KernelMessage<T> left, KernelMessage<T> right) =>
        left.Equals(right);
    /// <summary>
    /// Determines whether two instances are not equal.
    /// </summary>
    public static bool operator !=(KernelMessage<T> left, KernelMessage<T> right) =>
        !left.Equals(right);
}
