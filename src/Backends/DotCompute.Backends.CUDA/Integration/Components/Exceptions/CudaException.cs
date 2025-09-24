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
    public CudaError CudaError { get; }
    public CudaErrorHandlingResult? HandlingResult { get; }

    public CudaException(string message, CudaError cudaError, CudaErrorHandlingResult? handlingResult = null)
        : base(message)
    {
        CudaError = cudaError;
        HandlingResult = handlingResult;
    }

    public CudaException(string message, CudaError cudaError, Exception innerException, CudaErrorHandlingResult? handlingResult = null)
        : base(message, innerException)
    {
        CudaError = cudaError;
        HandlingResult = handlingResult;
    }
}