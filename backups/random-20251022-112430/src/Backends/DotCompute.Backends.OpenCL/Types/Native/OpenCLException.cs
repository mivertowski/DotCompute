// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// Exception thrown when OpenCL operations fail.
/// </summary>
public class OpenCLException : Exception
{
    /// <summary>
    /// Gets the OpenCL error code that caused this exception.
    /// </summary>
    public OpenCLError ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLException"/> class.
    /// </summary>
    /// <param name="errorCode">The OpenCL error code.</param>
    public OpenCLException(OpenCLError errorCode)
        : base($"OpenCL error: {errorCode}")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLException"/> class.
    /// </summary>
    /// <param name="errorCode">The OpenCL error code.</param>
    /// <param name="message">Custom error message.</param>
    public OpenCLException(OpenCLError errorCode, string message)
        : base($"OpenCL error: {errorCode} - {message}")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLException"/> class.
    /// </summary>
    /// <param name="errorCode">The OpenCL error code.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public OpenCLException(OpenCLError errorCode, string message, Exception innerException)
        : base($"OpenCL error: {errorCode} - {message}", innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Throws an OpenCL exception if the error code indicates failure.
    /// </summary>
    /// <param name="errorCode">The error code to check.</param>
    /// <param name="operation">Optional operation name for context.</param>
    internal static void ThrowIfError(OpenCLError errorCode, string? operation = null)
    {
        if (errorCode != OpenCLError.Success)
        {
            var message = operation != null ? $"Operation '{operation}' failed" : "OpenCL operation failed";
            throw new OpenCLException(errorCode, message);
        }
    }
}