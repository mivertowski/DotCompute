// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Recovery;

/// <summary>
/// Result of a recovery operation
/// </summary>
public class RecoveryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public TimeSpan Duration { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public int RetryAttempt { get; set; }
    public bool RequiresManualIntervention { get; set; }
    public Dictionary<string, object> Metadata { get; } = [];
}