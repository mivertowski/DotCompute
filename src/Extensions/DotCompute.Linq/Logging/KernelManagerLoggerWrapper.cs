// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Logging;

/// <summary>
/// Adapter to use ILogger&lt;GPULINQProvider&gt; as ILogger&lt;KernelManager&gt;.
/// </summary>
/// <remarks>
/// This wrapper allows the GPULINQProvider's logger to be used for KernelManager
/// operations, providing unified logging while maintaining type safety.
/// </remarks>
internal class KernelManagerLoggerWrapper : ILogger<KernelManager>
{
    private readonly ILogger<GPULINQProvider> _innerLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelManagerLoggerWrapper"/> class.
    /// </summary>
    /// <param name="innerLogger">The inner logger to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerLogger"/> is null.</exception>
    public KernelManagerLoggerWrapper(ILogger<GPULINQProvider> innerLogger)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _innerLogger.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
        => _innerLogger.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _innerLogger.Log(logLevel, eventId, state, exception, formatter);
}