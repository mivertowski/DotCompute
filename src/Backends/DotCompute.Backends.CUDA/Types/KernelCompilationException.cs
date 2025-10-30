// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Exception thrown during kernel compilation
    /// </summary>
    public sealed class KernelCompilationException : Exception
    {
        /// <summary>
        /// Gets or sets the compiler output.
        /// </summary>
        /// <value>The compiler output.</value>
        public string? CompilerOutput { get; }
        /// <summary>
        /// Gets or sets the source code.
        /// </summary>
        /// <value>The source code.</value>
        public string? SourceCode { get; }
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        /// <value>The error code.</value>
        public int? ErrorCode { get; }
        /// <summary>
        /// Initializes a new instance of the KernelCompilationException class.
        /// </summary>

        public KernelCompilationException() : base() { }
        /// <summary>
        /// Initializes a new instance of the KernelCompilationException class.
        /// </summary>
        /// <param name="message">The message.</param>

        public KernelCompilationException(string message) : base(message) { }
        /// <summary>
        /// Initializes a new instance of the KernelCompilationException class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>

        public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }
        /// <summary>
        /// Initializes a new instance of the KernelCompilationException class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="compilerOutput">The compiler output.</param>
        /// <param name="sourceCode">The source code.</param>
        /// <param name="errorCode">The error code.</param>

        public KernelCompilationException(string message, string? compilerOutput, string? sourceCode = null, int? errorCode = null)
            : base(message)
        {
            CompilerOutput = compilerOutput;
            SourceCode = sourceCode;
            ErrorCode = errorCode;
        }
    }
}
