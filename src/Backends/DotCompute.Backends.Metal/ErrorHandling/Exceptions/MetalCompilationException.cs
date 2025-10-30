// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.ErrorHandling.Exceptions;

/// <summary>
/// Exception thrown during Metal kernel compilation.
/// </summary>
public sealed class MetalCompilationException : Exception
{
    /// <summary>
    /// Gets the compiler output.
    /// </summary>
    public string? CompilerOutput { get; }

    /// <summary>
    /// Gets the source code.
    /// </summary>
    public string? SourceCode { get; }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalCompilationException"/> class.
    /// </summary>
    public MetalCompilationException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalCompilationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public MetalCompilationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalCompilationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MetalCompilationException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalCompilationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="compilerOutput">The compiler output.</param>
    /// <param name="sourceCode">The source code.</param>
    /// <param name="errorCode">The error code.</param>
    public MetalCompilationException(string message, string? compilerOutput, string? sourceCode = null, int? errorCode = null)
        : base(message)
    {
        CompilerOutput = compilerOutput;
        SourceCode = sourceCode;
        ErrorCode = errorCode;
    }
}
