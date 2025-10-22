// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the different kinds of spans in distributed tracing.
/// Each kind indicates the role of the span in the overall trace context.
/// </summary>
public enum SpanKind
{
    /// <summary>
    /// Internal span - represents an operation within the application.
    /// Used for internal operations that don't cross service boundaries.
    /// </summary>
    Internal,

    /// <summary>
    /// Server span - represents a request being handled by the service.
    /// Used when the service is acting as a server receiving requests.
    /// </summary>
    Server,

    /// <summary>
    /// Client span - represents a request made to another service.
    /// Used when the service is acting as a client making outbound calls.
    /// </summary>
    Client,

    /// <summary>
    /// Producer span - represents a message being sent to a messaging system.
    /// Used in message queue or event streaming scenarios.
    /// </summary>
    Producer,

    /// <summary>
    /// Consumer span - represents a message being received from a messaging system.
    /// Used when processing messages from queues or event streams.
    /// </summary>
    Consumer
}