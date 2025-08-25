// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// GPU recovery strategies
/// </summary>
public enum GpuRecoveryStrategy
{
    SimpleRetry,
    MemoryRecovery,
    KernelTermination,
    ContextReset,
    DeviceReset
}