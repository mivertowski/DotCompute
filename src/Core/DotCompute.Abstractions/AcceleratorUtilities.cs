// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions;

/// <summary>
/// Utility methods for common accelerator patterns to reduce code duplication.
/// </summary>
public static class AcceleratorUtilities
{
    /// <summary>
    /// Common pattern for compiling kernels with error handling and logging.
    /// </summary>
    /// <param name="definition">The kernel definition to compile</param>
    /// <param name="options">Compilation options</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="acceleratorType">Type of accelerator for logging</param>
    /// <param name="compileFunc">The actual compilation function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The compiled kernel</returns>
    public static async ValueTask<ICompiledKernel> CompileKernelWithLoggingAsync(
        KernelDefinition definition,
        CompilationOptions? options,
        ILogger logger,
        string acceleratorType,
        Func<KernelDefinition, CompilationOptions, CancellationToken, ValueTask<ICompiledKernel>> compileFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(compileFunc);

        options ??= new CompilationOptions();

        logger.LogDebug("Compiling kernel '{KernelName}' for {AcceleratorType}", 
            definition.Name, acceleratorType);

        try
        {
            var compiledKernel = await compileFunc(definition, options, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Successfully compiled kernel '{KernelName}'", definition.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compile kernel '{KernelName}' for {AcceleratorType}", 
                definition.Name, acceleratorType);
            throw;
        }
    }

    /// <summary>
    /// Common pattern for synchronizing accelerators with error handling and logging.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="acceleratorType">Type of accelerator for logging</param>
    /// <param name="syncFunc">The actual synchronization function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async ValueTask SynchronizeWithLoggingAsync(
        ILogger logger,
        string acceleratorType,
        Func<CancellationToken, ValueTask> syncFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(syncFunc);

        try
        {
            logger.LogTrace("Synchronizing {AcceleratorType} accelerator", acceleratorType);
            
            await syncFunc(cancellationToken).ConfigureAwait(false);
            
            logger.LogTrace("{AcceleratorType} accelerator synchronized", acceleratorType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize {AcceleratorType} accelerator", acceleratorType);
            throw;
        }
    }

    /// <summary>
    /// Common pattern for accelerator initialization with error handling and logging.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="acceleratorType">Type of accelerator for logging</param>
    /// <param name="initFunc">The initialization function</param>
    /// <param name="deviceInfo">Device information for logging</param>
    /// <returns>The result of the initialization function</returns>
    public static T InitializeWithLogging<T>(
        ILogger logger,
        string acceleratorType,
        Func<T> initFunc,
        string? deviceInfo = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(initFunc);

        try
        {
            logger.LogInformation("Initializing {AcceleratorType} accelerator{DeviceInfo}", 
                acceleratorType, deviceInfo != null ? $" ({deviceInfo})" : "");

            var result = initFunc();

            logger.LogInformation("{AcceleratorType} accelerator initialized successfully", acceleratorType);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize {AcceleratorType} accelerator", acceleratorType);
            throw new InvalidOperationException($"Failed to initialize {acceleratorType} accelerator", ex);
        }
    }

    /// <summary>
    /// Common pattern for accelerator disposal with synchronization.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="acceleratorType">Type of accelerator for logging</param>
    /// <param name="syncFunc">Optional synchronization function to call before disposal</param>
    /// <param name="disposables">Objects to dispose</param>
    public static async ValueTask DisposeWithSynchronizationAsync(
        ILogger logger,
        string acceleratorType,
        Func<ValueTask>? syncFunc,
        params object?[] disposables)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            logger.LogInformation("Disposing {AcceleratorType} accelerator", acceleratorType);

            // Synchronize before disposal if function provided
            if (syncFunc != null)
            {
                try
                {
                    await syncFunc().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error during synchronization before disposal of {AcceleratorType}", 
                        acceleratorType);
                }
            }

            // Dispose all objects
            await DisposalUtilities.SafeDisposeAllAsync(disposables, logger).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during {AcceleratorType} accelerator disposal", acceleratorType);
        }
    }

    /// <summary>
    /// Common pattern for accelerator disposal with synchronization (synchronous version).
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="acceleratorType">Type of accelerator for logging</param>
    /// <param name="syncAction">Optional synchronization action to call before disposal</param>
    /// <param name="disposables">Objects to dispose</param>
    public static void DisposeWithSynchronization(
        ILogger logger,
        string acceleratorType,
        Action? syncAction,
        params object?[] disposables)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            logger.LogInformation("Disposing {AcceleratorType} accelerator (synchronous)", acceleratorType);

            // Synchronize before disposal if action provided
            if (syncAction != null)
            {
                try
                {
                    syncAction();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error during synchronization before disposal of {AcceleratorType}", 
                        acceleratorType);
                }
            }

            // Dispose all objects
            DisposalUtilities.SafeDisposeAll(disposables, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during synchronous {AcceleratorType} accelerator disposal", acceleratorType);
        }
    }

    /// <summary>
    /// Helper method to validate accelerator is not disposed.
    /// </summary>
    /// <param name="disposed">Disposed flag</param>
    /// <param name="instance">Instance to check</param>
    public static void ThrowIfDisposed(bool disposed, object instance)
    {
        ObjectDisposedException.ThrowIf(disposed, instance);
    }

    /// <summary>
    /// Helper method to validate accelerator is not disposed using Interlocked pattern.
    /// </summary>
    /// <param name="disposed">Disposed flag as int</param>
    /// <param name="instance">Instance to check</param>
    public static void ThrowIfDisposed(int disposed, object instance)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, instance);
    }
}