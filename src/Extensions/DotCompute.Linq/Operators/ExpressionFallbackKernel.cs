// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Operators.Interfaces;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Operators.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators;

/// <summary>
/// A fallback kernel implementation for expressions that cannot be compiled to GPU code.
/// </summary>
public class ExpressionFallbackKernel : Interfaces.IKernel
{
    private readonly Expression _expression;
    private readonly ILogger _logger;
    private readonly Func<object[], object>? _compiledFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionFallbackKernel"/> class.
    /// </summary>
    /// <param name="expression">The expression to execute.</param>
    /// <param name="logger">The logger instance.</param>
    public ExpressionFallbackKernel(Expression expression, ILogger logger)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Try to compile the expression to a delegate for CPU execution
        if (expression is LambdaExpression lambda)
        {
            try
            {
                var compiled = lambda.Compile();
                _compiledFunc = args => compiled.DynamicInvoke(args)!;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compile expression to delegate, will use interpreter");
            }
        }
    }

    /// <inheritdoc/>
    public string Name => "ExpressionFallback";

    /// <inheritdoc/>
    public KernelProperties Properties { get; } = new KernelProperties
    {
        MaxThreadsPerBlock = 1,
        SharedMemorySize = 0,
        RegisterCount = 0,
        UsesLocalMemory = false,
        UsesAtomics = false
    };

    /// <inheritdoc/>
    public Task CompileAsync(CancellationToken cancellationToken = default)
        // Already compiled or interpreted
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing fallback kernel for expression type: {ExpressionType}", _expression.NodeType);
        
        // This is a simplified fallback - in production you'd implement proper CPU execution
        if (_compiledFunc != null && parameters.Count > 0)
        {
            var args = parameters.Values.ToArray();
            _ = await Task.Run(() => _compiledFunc(args), cancellationToken);
        }
        else
        {
            throw new NotSupportedException($"Cannot execute expression of type {_expression.NodeType} as fallback");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Parameters.KernelParameter> GetParameterInfo()
    {
        if (_expression is LambdaExpression lambda)
        {
            return lambda.Parameters.Select(p => new Parameters.KernelParameter(
                p.Name ?? $"param_{lambda.Parameters.IndexOf(p)}",
                p.Type,
                ParameterDirection.In
            )).ToList();
        }
        
        return Array.Empty<Parameters.KernelParameter>();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose
    }
}