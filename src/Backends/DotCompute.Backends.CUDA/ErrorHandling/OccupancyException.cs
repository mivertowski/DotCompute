// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.ErrorHandling
{
    /// <summary>
    /// Exception thrown when occupancy calculation fails.
    /// </summary>
    internal sealed class OccupancyException : AcceleratorException
    {
        /// <summary>
        /// Initializes a new instance of the OccupancyException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public OccupancyException(string message) : base(message)
        {

        }

        /// <summary>
        /// Initializes a new instance of the OccupancyException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public OccupancyException(string message, Exception innerException)

            : base(message, innerException)
        {

        }
        /// <summary>
        /// Initializes a new instance of the OccupancyException class.
        /// </summary>
        public OccupancyException()
        {
        }
    }
}
