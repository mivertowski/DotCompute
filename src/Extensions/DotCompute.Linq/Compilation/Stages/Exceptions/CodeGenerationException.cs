using System;

namespace DotCompute.Linq.Compilation.Stages.Exceptions;
/// <summary>
/// Exception thrown during code generation.
/// </summary>
public class CodeGenerationException : Exception
{
    public CodeGenerationException(string message) : base(message) { }
    public CodeGenerationException(string message, Exception innerException) : base(message, innerException) { }
}
