// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Provides serialization and deserialization for <see cref="IRingKernelMessage"/> types.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// Serializers must produce deterministic, platform-independent byte layouts suitable
/// for GPU consumption. The serialized format is:
/// </para>
/// <code>
/// ┌─────────────────┬──────────────────┬─────────────┐
/// │ Size (4 bytes)  │ MessageId (8B)   │ Payload...  │
/// └─────────────────┴──────────────────┴─────────────┘
/// </code>
/// <para>
/// Implementations must be thread-safe for concurrent serialization calls.
/// </para>
/// </remarks>
public interface IMessageSerializer<T> where T : IRingKernelMessage
{
    /// <summary>
    /// Gets the maximum serialized size for messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// This is used to allocate staging buffers. For variable-size messages,
    /// return a conservative upper bound. For fixed-size messages, return the exact size.
    /// </remarks>
    public int MaxSerializedSize { get; }

    /// <summary>
    /// Serializes a message into the provided destination span.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <param name="destination">The destination buffer (must be at least <see cref="MaxSerializedSize"/> bytes).</param>
    /// <returns>The actual number of bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="destination"/> is too small.
    /// </exception>
    /// <remarks>
    /// This method must be thread-safe. It should not allocate managed memory.
    /// </remarks>
    public int Serialize(T message, Span<byte> destination);

    /// <summary>
    /// Deserializes a message from the provided source span.
    /// </summary>
    /// <param name="source">The source buffer containing serialized message data.</param>
    /// <returns>The deserialized message.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="source"/> contains invalid or corrupted data.
    /// </exception>
    /// <remarks>
    /// This method must be thread-safe. It may allocate managed memory for the message instance.
    /// </remarks>
    public T Deserialize(ReadOnlySpan<byte> source);

    /// <summary>
    /// Gets the serialized size for a specific message instance.
    /// </summary>
    /// <param name="message">The message to measure.</param>
    /// <returns>The number of bytes required to serialize this message.</returns>
    /// <remarks>
    /// For fixed-size messages, this always returns <see cref="MaxSerializedSize"/>.
    /// For variable-size messages, this returns the exact size for this instance.
    /// </remarks>
    public int GetSerializedSize(T message);
}

/// <summary>
/// Default binary serializer for <see cref="IRingKernelMessage"/> types.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// This implementation uses the built-in Serialize/Deserialize methods from IRingKernelMessage.
/// The serialized format is determined by the message implementation (typically via source generator).
/// </para>
/// <para>
/// Format (from IRingKernelMessage):
/// - MessageId (16 bytes Guid)
/// - Priority (1 byte)
/// - CorrelationId (17 bytes: 1 byte null flag + 16 bytes Guid if present)
/// - Application data (variable, type-specific)
/// </para>
/// </remarks>
public sealed class DefaultMessageSerializer<T> : IMessageSerializer<T> where T : IRingKernelMessage, new()
{
    private const int MaxPayloadSize = 65536; // 64 KB maximum payload (conservative estimate)

    /// <inheritdoc/>
    public int MaxSerializedSize => MaxPayloadSize;

    /// <inheritdoc/>
    public int Serialize(T message, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Use IRingKernelMessage's built-in Serialize method
        var serialized = message.Serialize();

        if (serialized.Length > destination.Length)
        {
            throw new ArgumentException(
                $"Destination buffer too small. Need {serialized.Length} bytes, got {destination.Length}",
                nameof(destination));
        }

        // Copy serialized data to destination
        serialized.CopyTo(destination);

        return serialized.Length;
    }

    /// <inheritdoc/>
    public T Deserialize(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source buffer is empty", nameof(source));
        }

        // Create new instance and use IRingKernelMessage's built-in Deserialize method
        var message = new T();
        message.Deserialize(source);

        return message;
    }

    /// <inheritdoc/>
    public int GetSerializedSize(T message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Use PayloadSize property from IRingKernelMessage
        return message.PayloadSize;
    }
}
