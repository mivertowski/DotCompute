// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Utility methods for common accelerator patterns to reduce code duplication.
    /// </summary>
    public static partial class AcceleratorUtilities
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 3001,
            Level = LogLevel.Debug,
            Message = "Compiling kernel '{KernelName}' for {AcceleratorType}")]
        private static partial void LogCompilingKernel(ILogger logger, string kernelName, string acceleratorType);

        [LoggerMessage(
            EventId = 3002,
            Level = LogLevel.Debug,
            Message = "Successfully compiled kernel '{KernelName}'")]
        private static partial void LogKernelCompiled(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 3003,
            Level = LogLevel.Error,
            Message = "Failed to compile kernel '{KernelName}' for {AcceleratorType}")]
        private static partial void LogKernelCompilationFailed(ILogger logger, Exception ex, string kernelName, string acceleratorType);

        [LoggerMessage(
            EventId = 3004,
            Level = LogLevel.Trace,
            Message = "Synchronizing {AcceleratorType} accelerator")]
        private static partial void LogSynchronizingAccelerator(ILogger logger, string acceleratorType);

        [LoggerMessage(
            EventId = 3005,
            Level = LogLevel.Trace,
            Message = "{AcceleratorType} accelerator synchronized")]
        private static partial void LogAcceleratorSynchronized(ILogger logger, string acceleratorType);

        [LoggerMessage(
            EventId = 3006,
            Level = LogLevel.Error,
            Message = "Failed to synchronize {AcceleratorType} accelerator")]
        private static partial void LogSynchronizationFailed(ILogger logger, Exception ex, string acceleratorType);

        [LoggerMessage(
            EventId = 3007,
            Level = LogLevel.Information,
            Message = "Initializing {AcceleratorType} accelerator{DeviceInfo}")]
        private static partial void LogInitializingAccelerator(ILogger logger, string acceleratorType, string deviceInfo);

        [LoggerMessage(
            EventId = 3008,
            Level = LogLevel.Information,
            Message = "{AcceleratorType} accelerator initialized successfully")]
        private static partial void LogAcceleratorInitialized(ILogger logger, string acceleratorType);

        [LoggerMessage(
            EventId = 3009,
            Level = LogLevel.Error,
            Message = "Failed to initialize {AcceleratorType} accelerator")]
        private static partial void LogInitializationFailed(ILogger logger, Exception ex, string acceleratorType);

        [LoggerMessage(
            EventId = 3010,
            Level = LogLevel.Information,
            Message = "Disposing {AcceleratorType} accelerator")]
        private static partial void LogDisposingAccelerator(ILogger logger, string acceleratorType);

        [LoggerMessage(
            EventId = 3011,
            Level = LogLevel.Warning,
            Message = "Error during synchronization before disposal of {AcceleratorType}")]
        private static partial void LogDisposalSynchronizationError(ILogger logger, Exception ex, string acceleratorType);

        [LoggerMessage(
            EventId = 3012,
            Level = LogLevel.Error,
            Message = "Error during {AcceleratorType} accelerator disposal")]
        private static partial void LogDisposalError(ILogger logger, Exception ex, string acceleratorType);

        [LoggerMessage(
            EventId = 3013,
            Level = LogLevel.Information,
            Message = "Disposing {AcceleratorType} accelerator (synchronous)")]
        private static partial void LogDisposingAcceleratorSync(ILogger logger, string acceleratorType);

        [LoggerMessage(
            EventId = 3014,
            Level = LogLevel.Error,
            Message = "Error during synchronous {AcceleratorType} accelerator disposal")]
        private static partial void LogSyncDisposalError(ILogger logger, Exception ex, string acceleratorType);

        #endregion

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

            LogCompilingKernel(logger, definition.Name, acceleratorType);

            try
            {
                var compiledKernel = await compileFunc(definition, options, cancellationToken).ConfigureAwait(false);
                LogKernelCompiled(logger, definition.Name);
                return compiledKernel;
            }
            catch (Exception ex)
            {
                LogKernelCompilationFailed(logger, ex, definition.Name, acceleratorType);
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
                LogSynchronizingAccelerator(logger, acceleratorType);

                await syncFunc(cancellationToken).ConfigureAwait(false);

                LogAcceleratorSynchronized(logger, acceleratorType);
            }
            catch (Exception ex)
            {
                LogSynchronizationFailed(logger, ex, acceleratorType);
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
                LogInitializingAccelerator(logger, acceleratorType, deviceInfo != null ? $" ({deviceInfo})" : "");

                var result = initFunc();

                LogAcceleratorInitialized(logger, acceleratorType);
                return result;
            }
            catch (Exception ex)
            {
                LogInitializationFailed(logger, ex, acceleratorType);
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
                LogDisposingAccelerator(logger, acceleratorType);

                // Synchronize before disposal if function provided
                if (syncFunc != null)
                {
                    try
                    {
                        await syncFunc().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogDisposalSynchronizationError(logger, ex, acceleratorType);
                    }
                }

                // Dispose all objects
                await DisposalUtilities.SafeDisposeAllAsync(disposables, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposalError(logger, ex, acceleratorType);
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
                LogDisposingAcceleratorSync(logger, acceleratorType);

                // Synchronize before disposal if action provided
                if (syncAction != null)
                {
                    try
                    {
                        syncAction();
                    }
                    catch (Exception ex)
                    {
                        LogDisposalSynchronizationError(logger, ex, acceleratorType);
                    }
                }

                // Dispose all objects
                DisposalUtilities.SafeDisposeAll(disposables, logger);
            }
            catch (Exception ex)
            {
                LogSyncDisposalError(logger, ex, acceleratorType);
            }
        }

        /// <summary>
        /// Helper method to validate accelerator is not disposed.
        /// </summary>
        /// <param name="disposed">Disposed flag</param>
        /// <param name="instance">Instance to check</param>
        public static void ThrowIfDisposed(bool disposed, object instance) => ObjectDisposedException.ThrowIf(disposed, instance);

        /// <summary>
        /// Helper method to validate accelerator is not disposed using Interlocked pattern.
        /// </summary>
        /// <param name="disposed">Disposed flag as int</param>
        /// <param name="instance">Instance to check</param>
        public static void ThrowIfDisposed(int disposed, object instance) => ObjectDisposedException.ThrowIf(disposed != 0, instance);
    }
}
