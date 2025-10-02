// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Diagnostic message for Metal execution
/// </summary>
public sealed class MetalDiagnosticMessage
{
    /// <summary>
    /// Message severity level
    /// </summary>
    public enum SeverityLevel
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Info,

        /// <summary>
        /// Warning message
        /// </summary>
        Warning,

        /// <summary>
        /// Error message
        /// </summary>
        Error,

        /// <summary>
        /// Critical error message
        /// </summary>
        Critical
    }

    /// <summary>
    /// Severity of the message
    /// </summary>
    public SeverityLevel Severity { get; set; }

    /// <summary>
    /// Message text
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Component that generated the message
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Operation ID associated with the message
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Timestamp when message was generated
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional context data
    /// </summary>
    public Dictionary<string, object> Context { get; } = [];
}