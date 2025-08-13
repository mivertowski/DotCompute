// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions;

/// <summary>
/// Utility methods for common memory management patterns to reduce code duplication.
/// </summary>
public static class MemoryUtilities
{
    /// <summary>
    /// Common pattern for memory allocation with error handling and logging.
    /// </summary>
    /// <param name="sizeInBytes">Size of memory to allocate</param>
    /// <param name="memoryType">Type of memory to allocate</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="allocateFunc">The actual allocation function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The allocated memory buffer</returns>
    public static async ValueTask<IMemoryBuffer> AllocateWithLoggingAsync(
        long sizeInBytes,
        string memoryType,
        ILogger logger,
        string backendType,
        Func<long, CancellationToken, ValueTask<IMemoryBuffer>> allocateFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(allocateFunc);

        logger.LogDebug("Allocating {Size} bytes of {MemoryType} memory in {BackendType}", 
            sizeInBytes, memoryType, backendType);

        try
        {
            var buffer = await allocateFunc(sizeInBytes, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Successfully allocated {Size} bytes", sizeInBytes);
            return buffer;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to allocate {Size} bytes of {MemoryType} memory in {BackendType}", 
                sizeInBytes, memoryType, backendType);
            throw;
        }
    }

    /// <summary>
    /// Common pattern for memory deallocation with error handling and logging.
    /// </summary>
    /// <param name="buffer">Buffer to free</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="freeFunc">The actual free function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async ValueTask FreeWithLoggingAsync(
        IMemoryBuffer buffer,
        ILogger logger,
        string backendType,
        Func<IMemoryBuffer, CancellationToken, ValueTask> freeFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(freeFunc);

        logger.LogDebug("Freeing memory in {BackendType}", backendType);

        try
        {
            await freeFunc(buffer, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Successfully freed memory");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to free memory in {BackendType}", backendType);
            throw;
        }
    }

    /// <summary>
    /// Common pattern for memory copying with error handling and logging.
    /// </summary>
    /// <param name="source">Source buffer</param>
    /// <param name="destination">Destination buffer</param>
    /// <param name="size">Size to copy</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="copyFunc">The actual copy function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async ValueTask CopyWithLoggingAsync(
        IMemoryBuffer source,
        IMemoryBuffer destination,
        long size,
        ILogger logger,
        string backendType,
        Func<IMemoryBuffer, IMemoryBuffer, long, CancellationToken, ValueTask> copyFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(copyFunc);

        if (size > source.SizeInBytes || size > destination.SizeInBytes)
        {
            throw new ArgumentException("Copy size exceeds buffer capacity");
        }

        logger.LogTrace("Copying {Size} bytes in {BackendType}", 
            size, backendType);

        try
        {
            await copyFunc(source, destination, size, cancellationToken).ConfigureAwait(false);
            logger.LogTrace("Successfully copied {Size} bytes", size);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to copy {Size} bytes in {BackendType}",
                size, backendType);
            throw;
        }
    }

    /// <summary>
    /// Common pattern for memory manager initialization with allocation tracking.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="initFunc">Initialization function</param>
    /// <returns>Result of initialization</returns>
    public static T InitializeWithLogging<T>(
        ILogger logger,
        string backendType,
        Func<T> initFunc)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(initFunc);

        try
        {
            logger.LogDebug("Initializing {BackendType} memory manager", backendType);
            var result = initFunc();
            logger.LogDebug("{BackendType} memory manager initialized", backendType);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize {BackendType} memory manager", backendType);
            throw;
        }
    }

    /// <summary>
    /// Common pattern for memory manager disposal with cleanup of allocations.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="allocationCount">Number of active allocations</param>
    /// <param name="cleanupFunc">Function to clean up allocations</param>
    /// <param name="disposables">Objects to dispose</param>
    public static async ValueTask DisposeWithCleanupAsync(
        ILogger logger,
        string backendType,
        int allocationCount,
        Func<ValueTask>? cleanupFunc = null,
        params object?[] disposables)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            logger.LogInformation("Disposing {BackendType} memory manager with {Count} active allocations", 
                backendType, allocationCount);

            // Run cleanup function if provided
            if (cleanupFunc != null)
            {
                try
                {
                    await cleanupFunc().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error during cleanup of {BackendType} memory manager", backendType);
                }
            }

            // Dispose all objects
            await DisposalUtilities.SafeDisposeAllAsync(disposables, logger).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during {BackendType} memory manager disposal", backendType);
        }
    }

    /// <summary>
    /// Common pattern for memory manager disposal with cleanup of allocations (synchronous).
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="allocationCount">Number of active allocations</param>
    /// <param name="cleanupAction">Action to clean up allocations</param>
    /// <param name="disposables">Objects to dispose</param>
    public static void DisposeWithCleanup(
        ILogger logger,
        string backendType,
        int allocationCount,
        Action? cleanupAction = null,
        params object?[] disposables)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            logger.LogInformation("Disposing {BackendType} memory manager (synchronous) with {Count} active allocations", 
                backendType, allocationCount);

            // Run cleanup action if provided
            if (cleanupAction != null)
            {
                try
                {
                    cleanupAction();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error during cleanup of {BackendType} memory manager", backendType);
                }
            }

            // Dispose all objects
            DisposalUtilities.SafeDisposeAll(disposables, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during synchronous {BackendType} memory manager disposal", backendType);
        }
    }

    /// <summary>
    /// Common pattern for memory manager reset with allocation cleanup.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="backendType">Type of backend for logging</param>
    /// <param name="allocationCount">Number of allocations to clean</param>
    /// <param name="resetFunc">Function to perform the reset</param>
    public static void ResetWithLogging(
        ILogger logger,
        string backendType,
        int allocationCount,
        Action resetFunc)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(resetFunc);

        logger.LogInformation("Resetting {BackendType} memory manager - freeing {Count} allocations", 
            backendType, allocationCount);

        try
        {
            resetFunc();
            logger.LogInformation("{BackendType} memory manager reset completed", backendType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during {BackendType} memory manager reset", backendType);
            throw;
        }
    }

    /// <summary>
    /// Validates memory allocation parameters.
    /// </summary>
    /// <param name="size">Size to validate</param>
    /// <param name="parameterName">Name of the size parameter</param>
    public static void ValidateAllocationSize(long size, string parameterName = "size")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size, parameterName);
    }

    /// <summary>
    /// Validates memory copy parameters.
    /// </summary>
    /// <param name="source">Source buffer</param>
    /// <param name="destination">Destination buffer</param>
    /// <param name="size">Size to copy</param>
    /// <param name="sourceOffset">Source offset</param>
    /// <param name="destinationOffset">Destination offset</param>
    public static void ValidateCopyParameters(
        IMemoryBuffer source,
        IMemoryBuffer destination,
        long size,
        long sourceOffset = 0,
        long destinationOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (sourceOffset + size > source.SizeInBytes)
        {
            throw new ArgumentException("Source offset + size exceeds source buffer capacity");
        }

        if (destinationOffset + size > destination.SizeInBytes)
        {
            throw new ArgumentException("Destination offset + size exceeds destination buffer capacity");
        }
    }
}