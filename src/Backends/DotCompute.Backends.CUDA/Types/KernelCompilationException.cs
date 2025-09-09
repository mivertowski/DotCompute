// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Exception thrown during kernel compilation
    /// </summary>
    public sealed class KernelCompilationException : Exception
    {
        public string? CompilerOutput { get; }
        public string? SourceCode { get; }
        public int? ErrorCode { get; }

        public KernelCompilationException() : base() { }

        public KernelCompilationException(string message) : base(message) { }

        public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }

        public KernelCompilationException(string message, string? compilerOutput, string? sourceCode = null, int? errorCode = null)
            : base(message)
        {
            CompilerOutput = compilerOutput;
            SourceCode = sourceCode;
            ErrorCode = errorCode;
        }
    }
}