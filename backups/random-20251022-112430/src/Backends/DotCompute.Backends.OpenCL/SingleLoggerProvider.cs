// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL;

/// <summary>
/// Simple logger provider that wraps a single logger instance.
/// Used when we need to create a logger factory from an existing logger.
/// </summary>
internal sealed class SingleLoggerProvider : ILoggerProvider
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleLoggerProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger to provide.</param>
    public SingleLoggerProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a logger with the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for messages produced by the logger.</param>
    /// <returns>The logger instance.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return _logger;
    }

    /// <summary>
    /// Disposes the logger provider.
    /// </summary>
    public void Dispose()
    {
        // Nothing to dispose
    }
}