// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Device
{
    /// <summary>
    /// Represents the current operational status of a compute device.
    /// This status indicates the device's availability and readiness for computation.
    /// </summary>
    /// <remarks>
    /// The device status is used by the scheduler to determine which devices can accept
    /// new work and for monitoring system health. Status changes may trigger events
    /// for load balancing and fault tolerance mechanisms.
    /// </remarks>
    public enum DeviceStatus
    {
        /// <summary>
        /// Device is available and ready to accept new compute tasks.
        /// </summary>
        /// <remarks>
        /// This is the normal operational state where the device can be allocated
        /// for kernel execution. The device has completed initialization and
        /// passed all health checks.
        /// </remarks>
        Available,

        /// <summary>
        /// Device is currently executing tasks and may have limited capacity.
        /// </summary>
        /// <remarks>
        /// The device is operational but actively processing kernels or other operations.
        /// New tasks may be queued or may need to wait for current operations to complete.
        /// The scheduler may choose to distribute load to other available devices.
        /// </remarks>
        Busy,

        /// <summary>
        /// Device is offline and not available for computation.
        /// </summary>
        /// <remarks>
        /// The device is not accessible, either due to hardware disconnection,
        /// driver issues, power management, or administrative shutdown.
        /// Tasks cannot be scheduled on offline devices.
        /// </remarks>
        Offline,

        /// <summary>
        /// Device has encountered an error and requires attention.
        /// </summary>
        /// <remarks>
        /// The device has experienced a fault, hardware error, or other issue
        /// that prevents normal operation. Manual intervention or automatic
        /// recovery procedures may be required to restore functionality.
        /// Error details should be available through device diagnostics.
        /// </remarks>
        Error,

        /// <summary>
        /// Device is in the process of initialization and not yet ready.
        /// </summary>
        /// <remarks>
        /// The device is powering up, loading drivers, performing self-tests,
        /// or completing other startup procedures. This is a transitional state
        /// that should progress to Available once initialization completes.
        /// </remarks>
        Initializing
    }
}
