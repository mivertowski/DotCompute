using System;

namespace DotCompute.Linq.Compilation.Stages.Utilities;
/// <summary>
/// Activity tracking for code generation stages.
/// </summary>
internal static class CodeGenerationActivity
{
    public static IDisposable Start(string operationName)
    {
        return new NoOpDisposable();
    }
    private class NoOpDisposable : IDisposable
        public void Dispose() { }
}
