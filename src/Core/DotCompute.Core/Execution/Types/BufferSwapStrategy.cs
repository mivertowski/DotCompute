// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines strategies for buffer swapping in double-buffered execution scenarios.
    /// These strategies control when and how buffers are swapped to maintain continuous
    /// data flow while preventing data races and ensuring consistency.
    /// </summary>
    public enum BufferSwapStrategy
    {
        /// <summary>
        /// Automatic buffer swapping based on completion events.
        /// The system automatically swaps buffers when operations complete,
        /// using event-driven mechanisms to ensure thread-safe buffer transitions.
        /// Provides optimal performance with minimal developer intervention.
        /// </summary>
        Automatic,

        /// <summary>
        /// Manual buffer swapping controlled by the application.
        /// The application explicitly controls when buffers are swapped,
        /// providing maximum control over buffer management and timing.
        /// Requires careful coordination but allows for custom optimization strategies.
        /// </summary>
        Manual,

        /// <summary>
        /// Time-based buffer swapping.
        /// Buffers are swapped at regular time intervals regardless of operation completion.
        /// Useful for real-time scenarios where consistent timing is more important
        /// than optimal resource utilization.
        /// </summary>
        TimeBased
    }
}
