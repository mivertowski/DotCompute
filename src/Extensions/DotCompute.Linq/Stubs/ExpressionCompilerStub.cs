using System;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub expression compiler for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 3.
/// </remarks>
public class ExpressionCompilerStub : IExpressionCompiler
{
    /// <inheritdoc/>
    public CompilationResult Compile<T, TResult>(Expression<Func<IQueryable<T>, TResult>> query)
        => throw new NotImplementedException("Phase 3: Expression Analysis Foundation");

    /// <inheritdoc/>
    public CompilationResult Compile<T, TResult>(Expression<Func<IQueryable<T>, TResult>> query, CompilationOptions options)
        => throw new NotImplementedException("Phase 3: Expression Analysis Foundation");
}
