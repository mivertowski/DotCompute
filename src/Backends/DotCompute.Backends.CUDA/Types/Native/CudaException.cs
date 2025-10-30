// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Native.Exceptions
{
    /// <summary>
    /// Exception thrown when a CUDA operation fails.
    /// </summary>
    /// <remarks>
    /// This exception provides detailed information about CUDA errors, including
    /// the specific error code returned by the CUDA runtime or driver API.
    /// The error code can be used to determine the exact cause of the failure
    /// and take appropriate corrective action.
    /// </remarks>
    public class CudaException : Exception
    {
        /// <summary>
        /// Gets the CUDA error code associated with this exception.
        /// </summary>
        /// <value>
        /// The <see cref="CudaError"/> that caused this exception to be thrown.
        /// </value>
        public CudaError ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaException"/> class.
        /// </summary>
        public CudaException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public CudaException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CudaException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaException"/> class with a specified error message
        /// and CUDA error code.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="errorCode">The CUDA error code that caused this exception.</param>
        public CudaException(string message, CudaError errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
