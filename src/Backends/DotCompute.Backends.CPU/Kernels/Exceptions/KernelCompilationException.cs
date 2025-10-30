// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Exceptions;

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
public sealed class KernelCompilationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCompilationException"/> class.
    /// </summary>
    public KernelCompilationException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCompilationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public KernelCompilationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCompilationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }
}
