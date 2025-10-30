// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums
{
    /// <summary>
    /// Types of errors that can occur during pipeline execution.
    /// </summary>
    public enum PipelineErrorType
    {
        /// <summary>
        /// General execution error.
        /// </summary>
        ExecutionError = 0,

        /// <summary>
        /// Configuration error during pipeline setup.
        /// </summary>
        ConfigurationError = 1,

        /// <summary>
        /// Memory allocation or management error.
        /// </summary>
        MemoryError = 2,

        /// <summary>
        /// Data validation or format error.
        /// </summary>
        ValidationError = 3,

        /// <summary>
        /// Kernel compilation error.
        /// </summary>
        CompilationError = 4,

        /// <summary>
        /// Timeout error during execution.
        /// </summary>
        TimeoutError = 5,

        /// <summary>
        /// Resource unavailable error.
        /// </summary>
        ResourceError = 6,

        /// <summary>
        /// Backend-specific error.
        /// </summary>
        BackendError = 7,

        /// <summary>
        /// Data transfer error between devices.
        /// </summary>
        TransferError = 8,

        /// <summary>
        /// Synchronization error.
        /// </summary>
        SynchronizationError = 9
    }
}
