// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.ErrorHandling.Exceptions;

/// <summary>
/// Exception for CUDA device-specific failures.
/// </summary>
public sealed class CudaDeviceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CudaDeviceException"/> class.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="message">The message.</param>
    public CudaDeviceException(int deviceId, string message)
        : base($"CUDA device {deviceId} error: {message}")
    {
        DeviceId = deviceId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaDeviceException"/> class.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CudaDeviceException(int deviceId, string message, Exception innerException)
        : base($"CUDA device {deviceId} error: {message}", innerException)
    {
        DeviceId = deviceId;
    }

    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    /// <value>
    /// The device identifier.
    /// </value>
    public int DeviceId { get; }
}