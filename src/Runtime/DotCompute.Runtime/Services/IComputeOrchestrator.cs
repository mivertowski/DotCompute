// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This interface has been moved to DotCompute.Abstractions.Interfaces
// to avoid circular dependencies between Core and Runtime projects.

namespace DotCompute.Runtime.Services
{
    /// <summary>
    /// Re-export for backward compatibility. 
    /// Use DotCompute.Abstractions.Interfaces.IComputeOrchestrator instead.
    /// </summary>
    [Obsolete("Use DotCompute.Abstractions.Interfaces.IComputeOrchestrator instead")]
    public interface IComputeOrchestrator : DotCompute.Abstractions.Interfaces.IComputeOrchestrator
    {
    }
}