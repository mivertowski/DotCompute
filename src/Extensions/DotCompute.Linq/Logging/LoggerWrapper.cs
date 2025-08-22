// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Logging;

/// <summary>
/// Generic logger wrapper that adapts one logger type to another.
/// </summary>
/// <typeparam name="T">The target logger type to adapt to.</typeparam>
/// <remarks>
/// This generic wrapper provides a flexible way to adapt logger instances
/// from one type to another, enabling unified logging across different components
/// while maintaining type safety and proper categorization.
/// </remarks>
public class LoggerWrapper<T> : ILogger<T>
{
    private readonly ILogger _innerLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerWrapper{T}"/> class.
    /// </summary>
    /// <param name="innerLogger">The inner logger to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerLogger"/> is null.</exception>
    public LoggerWrapper(ILogger innerLogger)
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