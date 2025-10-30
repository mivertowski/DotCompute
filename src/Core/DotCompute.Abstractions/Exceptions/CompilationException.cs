// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
public class CompilationException : AcceleratorException
{
    /// <summary>
    /// Gets the compilation error details.
    /// </summary>
    public string? CompilationErrors { get; }

    /// <summary>
    /// Gets the kernel source that failed to compile.
    /// </summary>
    public string? KernelSource { get; }

    /// <summary>
    /// Gets the target platform.
    /// </summary>
    public string? TargetPlatform { get; }

    /// <summary>
    /// Initializes a new instance of the CompilationException class.
    /// </summary>
    public CompilationException() : base("Compilation failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the CompilationException class with a specified error message.
    /// </summary>
    public CompilationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the CompilationException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public CompilationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the CompilationException class with detailed compilation information.
    /// </summary>
    public CompilationException(string message, string? compilationErrors = null, string? kernelSource = null, string? targetPlatform = null)
        : base(message)
    {
        CompilationErrors = compilationErrors;
        KernelSource = kernelSource;
        TargetPlatform = targetPlatform;
    }

    /// <summary>
    /// Initializes a new instance of the CompilationException class with detailed compilation information and inner exception.
    /// </summary>
    public CompilationException(string message, Exception innerException, string? compilationErrors = null, string? kernelSource = null, string? targetPlatform = null)
        : base(message, innerException)
    {
        CompilationErrors = compilationErrors;
        KernelSource = kernelSource;
        TargetPlatform = targetPlatform;
    }
}
