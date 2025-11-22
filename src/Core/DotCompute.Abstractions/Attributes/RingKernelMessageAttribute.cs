// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions.RingKernels;

namespace DotCompute.Abstractions.Attributes;

/// <summary>
/// Marks a type as a ring kernel message, enabling automatic serialization and routing code generation.
/// </summary>
/// <remarks>
/// <para>
/// Types marked with this attribute will have their serialization code automatically generated
/// for GPU communication. The attribute also specifies the message direction which helps the
/// code generator optimize routing and memory allocation.
/// </para>
/// <para>
/// <b>Usage Examples:</b>
/// </para>
/// <code>
/// [RingKernelMessage(Direction = MessageDirection.Input)]
/// public partial record AddRequest(int A, int B);
///
/// [RingKernelMessage(Direction = MessageDirection.Output)]
/// public partial record AddResponse(int Result);
///
/// [RingKernelMessage(Direction = MessageDirection.KernelToKernel)]
/// public partial record ForwardMessage(int Value, string TargetKernelId);
/// </code>
/// <para>
/// <b>Generated Code:</b>
/// </para>
/// <list type="bullet">
/// <item><description>MemoryPack serialization/deserialization</description></item>
/// <item><description>CUDA struct definition matching binary layout</description></item>
/// <item><description>CUDA serialize/deserialize functions</description></item>
/// <item><description>Message routing metadata for K2K communication</description></item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RingKernelMessageAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the direction of message flow for this message type.
    /// </summary>
    /// <value>
    /// The message direction. Defaults to <see cref="MessageDirection.Bidirectional"/>.
    /// </value>
    /// <remarks>
    /// <para>Direction affects code generation and memory allocation:</para>
    /// <list type="bullet">
    /// <item><description><b>Input</b>: Allocates in input queue, generates host→kernel serialization</description></item>
    /// <item><description><b>Output</b>: Allocates in output queue, generates kernel→host serialization</description></item>
    /// <item><description><b>KernelToKernel</b>: Uses K2K routing infrastructure</description></item>
    /// <item><description><b>Bidirectional</b>: Full serialization in both directions</description></item>
    /// </list>
    /// </remarks>
    public MessageDirection Direction { get; set; } = MessageDirection.Bidirectional;

    /// <summary>
    /// Gets or sets the kernel ID this message is associated with.
    /// </summary>
    /// <value>
    /// The kernel identifier, or empty string for auto-detection based on naming convention.
    /// </value>
    /// <remarks>
    /// When empty, the generator will attempt to match the message type to a kernel
    /// based on naming conventions (e.g., <c>AddRequest</c> matches <c>Add</c> kernel).
    /// </remarks>
    public string KernelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message type identifier for routing.
    /// </summary>
    /// <value>
    /// A unique integer identifier for this message type, or 0 for auto-assignment.
    /// </value>
    /// <remarks>
    /// Used for type discrimination in polymorphic message handling.
    /// When 0, a unique ID is generated based on the type's full name hash.
    /// </remarks>
    public int MessageTypeId { get; set; }

    /// <summary>
    /// Gets or sets whether this message type requires causal ordering guarantees.
    /// </summary>
    /// <value>
    /// <c>true</c> to include HLC timestamp in serialization; <c>false</c> for best-effort ordering.
    /// Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, messages include Hybrid Logical Clock timestamps for causal ordering.
    /// This adds ~12 bytes overhead per message but enables happened-before relationships.
    /// </remarks>
    public bool RequiresCausalOrdering { get; set; }

    /// <summary>
    /// Gets or sets the priority level for this message type.
    /// </summary>
    /// <value>
    /// Priority value from 0 (lowest) to 255 (highest). Defaults to 128 (normal).
    /// </value>
    /// <remarks>
    /// Higher priority messages are processed before lower priority ones when
    /// multiple messages are waiting in the queue.
    /// </remarks>
    public byte Priority { get; set; } = 128;
}
