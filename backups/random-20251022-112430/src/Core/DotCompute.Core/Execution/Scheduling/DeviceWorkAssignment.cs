// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Execution.Scheduling
{
    /// <summary>
    /// Represents work assigned to a specific device.
    /// Contains all the information needed for a device to execute its portion of the workload.
    /// </summary>
    public sealed class DeviceWorkAssignment
    {
        /// <summary>
        /// Gets or sets the accelerator device that will execute this work assignment.
        /// </summary>
        public required IAccelerator Device { get; set; }

        /// <summary>
        /// Gets or sets the starting index in the global workload for this assignment.
        /// This defines where in the overall dataset this device should begin processing.
        /// </summary>
        public required int StartIndex { get; set; }

        /// <summary>
        /// Gets or sets the number of elements this device should process.
        /// This defines the size of the work chunk assigned to this device.
        /// </summary>
        public required int ElementCount { get; set; }

        /// <summary>
        /// Gets or sets the input buffers specific to this device's work assignment.
        /// These contain the data this device needs to process its assigned work.
        /// </summary>
        public required IReadOnlyList<object> InputBuffers { get; init; }

        /// <summary>
        /// Gets or sets the output buffers where this device should store its results.
        /// These will contain the processed data from this device's work assignment.
        /// </summary>
        public required IReadOnlyList<object> OutputBuffers { get; init; }

        /// <summary>
        /// Gets or sets whether work stealing is enabled for this device.
        /// When true, this device can steal work from other devices if it finishes early.
        /// </summary>
        public bool EnableWorkStealing { get; set; }
    }
}