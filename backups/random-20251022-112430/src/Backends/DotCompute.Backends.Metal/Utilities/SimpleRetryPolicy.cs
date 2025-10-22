// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Simple retry policy interface for Metal operations.
/// </summary>
public interface IAsyncPolicy
{
    /// <summary>
    /// Executes an action with retry logic.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple retry policy interface for Metal operations with return value.
/// </summary>
/// <typeparam name="T">The return type.</typeparam>
public interface IAsyncPolicy<T>
{
    /// <summary>
    /// Executes a function with retry logic.
    /// </summary>
    /// <param name="function">The function to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation with a return value.</returns>
    public Task<T> ExecuteAsync(Func<Task<T>> function, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple implementation of retry policy for Metal operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SimpleRetryPolicy"/> class.
/// </remarks>
/// <param name="maxRetries">Maximum number of retries.</param>
/// <param name="delay">Delay between retries.</param>
/// <param name="logger">Logger for diagnostics.</param>
public sealed class SimpleRetryPolicy(int maxRetries = 3, TimeSpan delay = default, ILogger? logger = null) : IAsyncPolicy, IAsyncPolicy<object>
{
    private readonly int _maxRetries = maxRetries;
    private readonly TimeSpan _delay = delay == default ? TimeSpan.FromMilliseconds(100) : delay;
    private readonly ILogger? _logger = logger;

    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        var attempts = 0;
        while (true)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempts < _maxRetries)
            {
                attempts++;
                _logger?.LogWarning("Retry attempt {Attempt} of {MaxRetries} after error: {Error}",

                    attempts, _maxRetries, ex.Message);


                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }


                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<object> ExecuteAsync(Func<Task<object>> function, CancellationToken cancellationToken = default)
    {
        var attempts = 0;
        while (true)
        {
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempts < _maxRetries)
            {
                attempts++;
                _logger?.LogWarning("Retry attempt {Attempt} of {MaxRetries} after error: {Error}",

                    attempts, _maxRetries, ex.Message);


                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }


                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Generic implementation of retry policy for Metal operations.
/// </summary>
/// <typeparam name="T">The return type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SimpleRetryPolicy{T}"/> class.
/// </remarks>
/// <param name="maxRetries">Maximum number of retries.</param>
/// <param name="delay">Delay between retries.</param>
/// <param name="logger">Logger for diagnostics.</param>
public sealed class SimpleRetryPolicy<T>(int maxRetries = 3, TimeSpan delay = default, ILogger? logger = null) : IAsyncPolicy<T>
{
    private readonly int _maxRetries = maxRetries;
    private readonly TimeSpan _delay = delay == default ? TimeSpan.FromMilliseconds(100) : delay;
    private readonly ILogger? _logger = logger;

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync(Func<Task<T>> function, CancellationToken cancellationToken = default)
    {
        var attempts = 0;
        while (true)
        {
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempts < _maxRetries)
            {
                attempts++;
                _logger?.LogWarning("Retry attempt {Attempt} of {MaxRetries} after error: {Error}",

                    attempts, _maxRetries, ex.Message);


                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }


                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}