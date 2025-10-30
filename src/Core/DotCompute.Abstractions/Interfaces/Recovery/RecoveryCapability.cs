// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Recovery;

/// <summary>
/// Recovery capabilities that strategies can handle
/// </summary>
public enum RecoveryCapability
{
    None = 0,
    GpuErrors = 1 << 0,
    MemoryErrors = 1 << 1,
    CompilationErrors = 1 << 2,
    NetworkErrors = 1 << 3,
    PluginErrors = 1 << 4,
    TimeoutErrors = 1 << 5,
    ResourceExhaustion = 1 << 6,
    DataCorruption = 1 << 7,
    All = int.MaxValue
}
