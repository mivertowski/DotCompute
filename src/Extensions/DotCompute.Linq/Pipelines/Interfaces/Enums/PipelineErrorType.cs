// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Linq.Pipelines.Interfaces.Enums
{
    /// <summary>
    /// Pipeline error types for comprehensive error handling.
    /// </summary>
    [Flags]
    public enum PipelineErrorType
    {
        /// <summary>No errors.</summary>
        None = 0,

        /// <summary>Data validation errors.</summary>
        DataValidationError = 1 << 0,

        /// <summary>Computation errors (overflow, NaN, etc.).</summary>
        ComputationError = 1 << 1,

        /// <summary>Memory allocation errors.</summary>
        MemoryAllocationError = 1 << 2,

        /// <summary>Backend compilation errors.</summary>
        CompilationError = 1 << 3,

        /// <summary>Kernel execution errors.</summary>
        KernelExecutionError = 1 << 4,

        /// <summary>Resource unavailability errors.</summary>
        ResourceUnavailableError = 1 << 5,

        /// <summary>Timeout errors.</summary>
        TimeoutError = 1 << 6,

        /// <summary>Synchronization errors.</summary>
        SynchronizationError = 1 << 7,

        /// <summary>Data transfer errors.</summary>
        DataTransferError = 1 << 8,

        /// <summary>Configuration errors.</summary>
        ConfigurationError = 1 << 9,

        /// <summary>Network communication errors.</summary>
        NetworkError = 1 << 10,

        /// <summary>Security or permission errors.</summary>
        SecurityError = 1 << 11,

        /// <summary>Hardware errors.</summary>
        HardwareError = 1 << 12,

        /// <summary>All error types.</summary>
        All = int.MaxValue
    }
}