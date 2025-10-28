// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.ErrorHandling.Exceptions;

/// <summary>
/// Exception for Metal device-specific failures.
/// </summary>
public sealed class MetalDeviceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetalDeviceException"/> class.
    /// </summary>
    public MetalDeviceException()
        : base("Metal device error")
    {
        DeviceName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalDeviceException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public MetalDeviceException(string message)
        : base(message)
    {
        DeviceName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalDeviceException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MetalDeviceException(string message, Exception innerException)
        : base(message, innerException)
    {
        DeviceName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalDeviceException"/> class.
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    /// <param name="message">The message.</param>
    public MetalDeviceException(string deviceName, string message)
        : base($"Metal device '{deviceName}' error: {message}")
    {
        DeviceName = deviceName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalDeviceException"/> class.
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MetalDeviceException(string deviceName, string message, Exception innerException)
        : base($"Metal device '{deviceName}' error: {message}", innerException)
    {
        DeviceName = deviceName;
    }

    /// <summary>
    /// Gets the device name.
    /// </summary>
    public string DeviceName { get; }
}
