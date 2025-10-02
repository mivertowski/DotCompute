// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Recovery;

/// <summary>
/// Options for recovery operations
/// </summary>
public class RecoveryOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool AllowFallback { get; set; } = true;
    public bool ForceGarbageCollection { get; set; }

    public TimeSpan MaxRecoveryTime { get; set; } = TimeSpan.FromSeconds(30);
    public Dictionary<string, object> Context { get; } = [];
}