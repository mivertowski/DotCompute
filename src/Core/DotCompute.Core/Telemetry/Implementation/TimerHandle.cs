// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Telemetry;

namespace DotCompute.Core.Telemetry.Implementation;

/// <summary>
/// Timer handle implementation.
/// </summary>
internal sealed class TimerHandle : ITimerHandle
{
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    /// <value>The operation name.</value>
    public string OperationName => string.Empty;
    /// <summary>
    /// Gets or sets the operation identifier.
    /// </summary>
    /// <value>The operation id.</value>
    public string OperationId => string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTime StartTime => DateTime.UtcNow;
    /// <summary>
    /// Gets or sets the elapsed.
    /// </summary>
    /// <value>The elapsed.</value>
    public TimeSpan Elapsed => TimeSpan.Zero;
    /// <summary>
    /// Gets stop.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <returns>The result of the operation.</returns>
    public TimeSpan Stop(IDictionary<string, object>? metadata = null) => TimeSpan.Zero;
    /// <summary>
    /// Gets add checkpoint.
    /// </summary>
    /// <param name="checkpointName">The checkpoint name.</param>
    /// <returns>The result of the operation.</returns>
    public TimeSpan AddCheckpoint(string checkpointName) => TimeSpan.Zero;
    /// <summary>
    /// Gets the checkpoints.
    /// </summary>
    /// <returns>The checkpoints.</returns>
    public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
    /// <summary>
    /// Performs dispose.
    /// </summary>
    public void Dispose() { }
}