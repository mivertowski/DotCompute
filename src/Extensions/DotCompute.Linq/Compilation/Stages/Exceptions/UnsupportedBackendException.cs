using System;

namespace DotCompute.Linq.Compilation.Stages.Exceptions;
{
/// <summary>
/// Exception for unsupported backend types.
/// </summary>
public class UnsupportedBackendException : Exception
{
    public UnsupportedBackendException(string message) : base(message) { }
        {
}
