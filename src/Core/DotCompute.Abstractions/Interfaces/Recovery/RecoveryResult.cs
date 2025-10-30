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

    /// <summary>
    /// Creates a successful recovery result
    /// </summary>
    /// <param name="message">Success message</param>
    /// <param name="strategy">Optional recovery strategy used</param>
    /// <returns>A successful recovery result</returns>
    public static RecoveryResult CreateSuccess(string message, string? strategy = null)
    {
        return new RecoveryResult
        {
            Success = true,
            Message = message,
            Strategy = strategy ?? string.Empty
        };
    }

    /// <summary>
    /// Creates a failed recovery result
    /// </summary>
    /// <param name="message">Failure message</param>
    /// <param name="strategy">Optional recovery strategy used</param>
    /// <returns>A failed recovery result</returns>
    public static RecoveryResult CreateFailure(string message, string? strategy = null)
    {
        return new RecoveryResult
        {
            Success = false,
            Message = message,
            Strategy = strategy ?? string.Empty
        };
    }

    /// <summary>
    /// Creates a failed recovery result with exception
    /// </summary>
    /// <param name="message">Failure message</param>
    /// <param name="strategy">Recovery strategy used</param>
    /// <param name="exception">The exception that caused the failure</param>
    /// <returns>A failed recovery result</returns>
    public static RecoveryResult CreateFailure(string message, string strategy, Exception exception)
    {
        return new RecoveryResult
        {
            Success = false,
            Message = message,
            Strategy = strategy,
            Exception = exception
        };
    }
}
