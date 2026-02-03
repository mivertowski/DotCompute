// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Generators;

/// <summary>
/// Marks a type as a ring kernel message, enabling automatic serialization and routing code generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RingKernelMessageAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the direction of message flow for this message type.
    /// </summary>
    public MessageDirection Direction { get; set; } = MessageDirection.Bidirectional;

    /// <summary>
    /// Gets or sets the kernel ID this message is associated with.
    /// Empty string for auto-detection based on naming convention.
    /// </summary>
    public string KernelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message type identifier for routing.
    /// 0 for auto-assignment based on type name hash.
    /// </summary>
    public int MessageTypeId { get; set; }

    /// <summary>
    /// Gets or sets whether this message type requires causal ordering guarantees.
    /// </summary>
    public bool RequiresCausalOrdering { get; set; }

    /// <summary>
    /// Gets or sets the priority level for this message type (0-255).
    /// </summary>
    public byte Priority { get; set; } = 128;
}
