// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Profiling
{
    /// <summary>
    /// Exception thrown when CUDA profiling operations fail.
    /// </summary>
    public class ProfilingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilingException"/> class.
        /// </summary>
        public ProfilingException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilingException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ProfilingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilingException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ProfilingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
