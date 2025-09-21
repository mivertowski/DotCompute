// <copyright file="ExpressionFallbackKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Types;
namespace DotCompute.Linq.Operators.Kernels;
/// <summary>
/// Kernel implementation that falls back to CPU execution for unsupported expressions.
/// </summary>
internal class ExpressionFallbackKernel : IKernel
{
    private readonly Expression _expression;
    private readonly Func<object> _compiledExpression;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionFallbackKernel"/> class.
    /// </summary>
    /// <param name="expression">The expression to compile for CPU fallback.</param>
    public ExpressionFallbackKernel(Expression expression)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        // Pre-compile the expression for CPU execution
        var lambda = Expression.Lambda(_expression);
        _compiledExpression = lambda.Compile() as Func<object> ??
            throw new InvalidOperationException("Failed to compile expression for CPU fallback");
        Name = $"FallbackKernel_{expression.NodeType}";
        Properties = new KernelProperties
        {
            MaxThreadsPerBlock = 1, // CPU execution is single-threaded
            SharedMemorySize = 0,
            RegisterCount = 0
        };
    }
    /// Gets the kernel name.
    public string Name { get; }
    /// Gets the source code or IL representation of the kernel.
    public string Source => _expression.ToString();
    /// Gets the entry point method name for the kernel.
    public string EntryPoint => "Execute";
    /// Gets the required shared memory size in bytes.
    public int RequiredSharedMemory => 0;
    /// Gets the kernel properties.
    public KernelProperties Properties { get; }
    /// Compiles the kernel (no-op for fallback kernel).
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed task.</returns>
    public static Task CompileAsync(CancellationToken cancellationToken = default)
        // Already compiled during construction
        => Task.CompletedTask;
    /// Executes the expression on CPU.
    /// <param name="workItems">The work items (ignored for CPU fallback).</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        return Task.Run(() =>
            try
            {
                // Execute the compiled expression
                var result = _compiledExpression();
                // Store result in parameters if output parameter exists
                if (parameters.ContainsKey("output"))
                {
                    parameters["output"] = result;
                }
            }
            catch (Exception ex)
                throw new InvalidOperationException($"CPU fallback execution failed: {ex.Message}", ex);
        }, cancellationToken);
    /// Gets parameter information (empty for fallback kernel).
    /// <returns>An empty array of kernel parameters.</returns>
    public static IReadOnlyList<KernelParameter> GetParameterInfo() => Array.Empty<KernelParameter>();
    /// Disposes the fallback kernel.
    public void Dispose()
        if (!_disposed)
            _disposed = true;
        }
}
