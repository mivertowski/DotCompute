// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.Exceptions;

/// <summary>
/// CUDA exception with error handling context.
/// </summary>
public sealed class CudaException : Exception
{
    /// <summary>
    /// Gets or sets the cuda error.
    /// </summary>
    /// <value>The cuda error.</value>
    public CudaError CudaError { get; }
    /// <summary>
    /// Gets or sets the handling result.
    /// </summary>
    /// <value>The handling result.</value>
    public CudaErrorHandlingResult? HandlingResult { get; }
    /// <summary>
    /// Initializes a new instance of the CudaException class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cudaError">The cuda error.</param>
    /// <param name="handlingResult">The handling result.</param>

    public CudaException(string message, CudaError cudaError, CudaErrorHandlingResult? handlingResult = null)
        : base(message)
    {
        CudaError = cudaError;
        HandlingResult = handlingResult;
    }
    /// <summary>
    /// Initializes a new instance of the CudaException class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cudaError">The cuda error.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="handlingResult">The handling result.</param>

    public CudaException(string message, CudaError cudaError, Exception innerException, CudaErrorHandlingResult? handlingResult = null)
        : base(message, innerException)
    {
        CudaError = cudaError;
        HandlingResult = handlingResult;
    }
}