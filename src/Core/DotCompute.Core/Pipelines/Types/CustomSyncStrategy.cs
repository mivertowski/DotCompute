// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Types
{
    /// <summary>
    /// Custom synchronization strategies for parallel execution.
    /// </summary>
    internal enum CustomSyncStrategy
    {
        Default,
        BarrierSync,
        ProducerConsumer,
        WorkStealing
    }
}