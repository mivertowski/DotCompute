using System;
using System.Linq.Expressions;

using DotCompute.Abstractions;
namespace DotCompute.Linq.Compilation;

/// <summary>
/// Interface for compiling LINQ expressions to compute kernels.
/// </summary>
/// <remarks>
/// ⚠️ STUB IMPLEMENTATION - Phase 2 (Test Infrastructure)
/// This is a placeholder to enable integration test compilation.
/// Full implementation planned for Phase 3 (Expression Analysis Foundation).
/// </remarks>
public interface IExpressionCompiler
{
    /// <summary>
    /// Compiles a LINQ query expression to a compute kernel.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="query">The LINQ query expression to compile.</param>
    /// <returns>Compilation result containing compiled kernel or error information.</returns>
    public CompilationResult Compile<T, TResult>(Expression<Func<IQueryable<T>, TResult>> query);

    /// <summary>
    /// Compiles a LINQ expression with custom compilation options.
    /// </summary>
    /// <typeparam name="T">Input element type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="query">The LINQ query expression to compile.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>Compilation result containing compiled kernel or error information.</returns>
    public CompilationResult Compile<T, TResult>(
        Expression<Func<IQueryable<T>, TResult>> query,
        CompilationOptions options);
}
