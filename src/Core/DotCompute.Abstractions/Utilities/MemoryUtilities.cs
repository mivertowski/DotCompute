// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions.Utilities
{

    /// <summary>
    /// Utility methods for common memory management patterns to reduce code duplication.
    /// </summary>
    public static partial class MemoryUtilities
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 4001,
            Level = LogLevel.Debug,
            Message = "Allocating {Size} bytes of {MemoryType} memory in {BackendType}")]
        private static partial void LogAllocating(ILogger logger, long size, string memoryType, string backendType);

        [LoggerMessage(
            EventId = 4002,
            Level = LogLevel.Debug,
            Message = "Successfully allocated {Size} bytes")]
        private static partial void LogAllocatedSuccessfully(ILogger logger, long size);

        [LoggerMessage(
            EventId = 4003,
            Level = LogLevel.Error,
            Message = "Failed to allocate {Size} bytes of {MemoryType} memory in {BackendType}")]
        private static partial void LogAllocationFailed(ILogger logger, Exception ex, long size, string memoryType, string backendType);

        [LoggerMessage(
            EventId = 4004,
            Level = LogLevel.Debug,
            Message = "Freeing memory in {BackendType}")]
        private static partial void LogFreeingMemory(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 4005,
            Level = LogLevel.Debug,
            Message = "Successfully freed memory")]
        private static partial void LogFreedSuccessfully(ILogger logger);

        [LoggerMessage(
            EventId = 4006,
            Level = LogLevel.Error,
            Message = "Failed to free memory in {BackendType}")]
        private static partial void LogFreeFailed(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 4007,
            Level = LogLevel.Trace,
            Message = "Copying {Size} bytes in {BackendType}")]
        private static partial void LogCopyingMemory(ILogger logger, long size, string backendType);

        [LoggerMessage(
            EventId = 4008,
            Level = LogLevel.Trace,
            Message = "Successfully copied {Size} bytes")]
        private static partial void LogCopiedSuccessfully(ILogger logger, long size);

        [LoggerMessage(
            EventId = 4009,
            Level = LogLevel.Error,
            Message = "Failed to copy {Size} bytes in {BackendType}")]
        private static partial void LogCopyFailed(ILogger logger, Exception ex, long size, string backendType);

        [LoggerMessage(
            EventId = 4010,
            Level = LogLevel.Debug,
            Message = "Initializing {BackendType} memory manager")]
        private static partial void LogInitializing(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 4011,
            Level = LogLevel.Debug,
            Message = "{BackendType} memory manager initialized")]
        private static partial void LogInitialized(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 4012,
            Level = LogLevel.Error,
            Message = "Failed to initialize {BackendType} memory manager")]
        private static partial void LogInitializeFailed(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 4013,
            Level = LogLevel.Information,
            Message = "Disposing {BackendType} memory manager with {Count} active allocations")]
        private static partial void LogDisposingMemoryManager(ILogger logger, string backendType, int count);

        [LoggerMessage(
            EventId = 4014,
            Level = LogLevel.Warning,
            Message = "Error during cleanup of {BackendType} memory manager")]
        private static partial void LogCleanupError(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 4015,
            Level = LogLevel.Error,
            Message = "Error during {BackendType} memory manager disposal")]
        private static partial void LogDisposalError(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 4016,
            Level = LogLevel.Information,
            Message = "Disposing {BackendType} memory manager (synchronous) with {Count} active allocations")]
        private static partial void LogDisposingMemoryManagerSync(ILogger logger, string backendType, int count);

        [LoggerMessage(
            EventId = 4017,
            Level = LogLevel.Error,
            Message = "Error during synchronous {BackendType} memory manager disposal")]
        private static partial void LogSyncDisposalError(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 4018,
            Level = LogLevel.Information,
            Message = "Resetting {BackendType} memory manager - freeing {Count} allocations")]
        private static partial void LogResetting(ILogger logger, string backendType, int count);

        [LoggerMessage(
            EventId = 4019,
            Level = LogLevel.Information,
            Message = "{BackendType} memory manager reset completed")]
        private static partial void LogResetCompleted(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 4020,
            Level = LogLevel.Error,
            Message = "Error during {BackendType} memory manager reset")]
        private static partial void LogResetError(ILogger logger, Exception ex, string backendType);

        #endregion
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
        public static async ValueTask<IUnifiedMemoryBuffer> AllocateWithLoggingAsync(
            long sizeInBytes,
            string memoryType,
            ILogger logger,
            string backendType,
            Func<long, CancellationToken, ValueTask<IUnifiedMemoryBuffer>> allocateFunc,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(allocateFunc);

            LogAllocating(logger, sizeInBytes, memoryType, backendType);

            try
            {
                var buffer = await allocateFunc(sizeInBytes, cancellationToken).ConfigureAwait(false);
                LogAllocatedSuccessfully(logger, sizeInBytes);
                return buffer;
            }
            catch (Exception ex)
            {
                LogAllocationFailed(logger, ex, sizeInBytes, memoryType, backendType);
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
            IUnifiedMemoryBuffer buffer,
            ILogger logger,
            string backendType,
            Func<IUnifiedMemoryBuffer, CancellationToken, ValueTask> freeFunc,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(freeFunc);

            LogFreeingMemory(logger, backendType);

            try
            {
                await freeFunc(buffer, cancellationToken).ConfigureAwait(false);
                LogFreedSuccessfully(logger);
            }
            catch (Exception ex)
            {
                LogFreeFailed(logger, ex, backendType);
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
            IUnifiedMemoryBuffer source,
            IUnifiedMemoryBuffer destination,
            long size,
            ILogger logger,
            string backendType,
            Func<IUnifiedMemoryBuffer, IUnifiedMemoryBuffer, long, CancellationToken, ValueTask> copyFunc,
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

            LogCopyingMemory(logger, size, backendType);

            try
            {
                await copyFunc(source, destination, size, cancellationToken).ConfigureAwait(false);
                LogCopiedSuccessfully(logger, size);
            }
            catch (Exception ex)
            {
                LogCopyFailed(logger, ex, size, backendType);
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
                LogInitializing(logger, backendType);
                var result = initFunc();
                LogInitialized(logger, backendType);
                return result;
            }
            catch (Exception ex)
            {
                LogInitializeFailed(logger, ex, backendType);
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
                LogDisposingMemoryManager(logger, backendType, allocationCount);

                // Run cleanup function if provided
                if (cleanupFunc != null)
                {
                    try
                    {
                        await cleanupFunc().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogCleanupError(logger, ex, backendType);
                    }
                }

                // Dispose all objects
                await DisposalUtilities.SafeDisposeAllAsync(disposables, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposalError(logger, ex, backendType);
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
                LogDisposingMemoryManagerSync(logger, backendType, allocationCount);

                // Run cleanup action if provided
                if (cleanupAction != null)
                {
                    try
                    {
                        cleanupAction();
                    }
                    catch (Exception ex)
                    {
                        LogCleanupError(logger, ex, backendType);
                    }
                }

                // Dispose all objects
                DisposalUtilities.SafeDisposeAll(disposables, logger);
            }
            catch (Exception ex)
            {
                LogSyncDisposalError(logger, ex, backendType);
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

            LogResetting(logger, backendType, allocationCount);

            try
            {
                resetFunc();
                LogResetCompleted(logger, backendType);
            }
            catch (Exception ex)
            {
                LogResetError(logger, ex, backendType);
                throw;
            }
        }

        /// <summary>
        /// Validates memory allocation parameters.
        /// </summary>
        /// <param name="size">Size to validate</param>
        /// <param name="parameterName">Name of the size parameter</param>
        public static void ValidateAllocationSize(long size, string parameterName = "size") => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size, parameterName);

        /// <summary>
        /// Validates memory copy parameters.
        /// </summary>
        /// <param name="source">Source buffer</param>
        /// <param name="destination">Destination buffer</param>
        /// <param name="size">Size to copy</param>
        /// <param name="sourceOffset">Source offset</param>
        /// <param name="destinationOffset">Destination offset</param>
        public static void ValidateCopyParameters(
        IUnifiedMemoryBuffer source,
        IUnifiedMemoryBuffer destination,
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
}
