// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Telemetry;

namespace DotCompute.Core.Telemetry.Implementation;

/// <summary>
/// Timer handle implementation.
/// </summary>
internal sealed class TimerHandle : ITimerHandle
{
    public string OperationName => string.Empty;
    public string OperationId => string.Empty;
    public DateTime StartTime => DateTime.UtcNow;
    public TimeSpan Elapsed => TimeSpan.Zero;
    public TimeSpan Stop(IDictionary<string, object>? metadata = null) => TimeSpan.Zero;
    public TimeSpan AddCheckpoint(string checkpointName) => TimeSpan.Zero;
    public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
    public void Dispose() { }
}