// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Common disposal patterns and utilities for accelerator implementations.
    /// </summary>
    public static partial class DisposalUtilities
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 2001,
            Level = LogLevel.Trace,
            Message = "Disposing {ObjectName} (async)")]
        private static partial void LogDisposing(ILogger logger, string objectName);

        [LoggerMessage(
            EventId = 2002,
            Level = LogLevel.Trace,
            Message = "Disposing {ObjectName} (sync)")]
        private static partial void LogDisposingSync(ILogger logger, string objectName);

        [LoggerMessage(
            EventId = 2003,
            Level = LogLevel.Trace,
            Message = "{ObjectName} does not implement IDisposable")]
        private static partial void LogNotDisposable(ILogger logger, string objectName);

        [LoggerMessage(
            EventId = 2004,
            Level = LogLevel.Warning,
            Message = "Error disposing {ObjectName}")]
        private static partial void LogDisposeError(ILogger logger, Exception ex, string objectName);

        [LoggerMessage(
            EventId = 2005,
            Level = LogLevel.Trace,
            Message = "Disposing {ObjectName} (async - blocking)")]
        private static partial void LogDisposingAsyncBlocking(ILogger logger, string objectName);

        [LoggerMessage(
            EventId = 2006,
            Level = LogLevel.Warning,
            Message = "Timeout waiting for async disposal of {ObjectName}")]
        private static partial void LogAsyncDisposalTimeout(ILogger logger, string objectName);

        [LoggerMessage(
            EventId = 2007,
            Level = LogLevel.Trace,
            Message = "Synchronizing {ComponentName} before disposal")]
        private static partial void LogSynchronizingBeforeDisposal(ILogger logger, string componentName);

        [LoggerMessage(
            EventId = 2008,
            Level = LogLevel.Warning,
            Message = "Error during synchronization of {ComponentName} before disposal")]
        private static partial void LogSynchronizationError(ILogger logger, Exception ex, string componentName);

        [LoggerMessage(
            EventId = 2009,
            Level = LogLevel.Trace,
            Message = "Synchronizing {ComponentName} before disposal (sync)")]
        private static partial void LogSynchronizingBeforeDisposalSync(ILogger logger, string componentName);

        [LoggerMessage(
            EventId = 2010,
            Level = LogLevel.Trace,
            Message = "Releasing native resource {ResourceName} for {ComponentName}")]
        private static partial void LogReleasingNativeResource(ILogger logger, string resourceName, string componentName);

        [LoggerMessage(
            EventId = 2011,
            Level = LogLevel.Warning,
            Message = "Error releasing native resource {ResourceName} for {ComponentName}")]
        private static partial void LogNativeResourceReleaseError(ILogger logger, Exception ex, string resourceName, string componentName);

        #endregion
        /// <summary>
        /// Safely disposes an object with logging, handling both sync and async disposal patterns.
        /// </summary>
        /// <param name="disposable">The object to dispose</param>
        /// <param name="logger">Logger for recording disposal events</param>
        /// <param name="objectName">Name of the object being disposed for logging</param>
        public static async ValueTask SafeDisposeAsync(object? disposable, ILogger? logger, string objectName = "object")
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                switch (disposable)
                {
                    case IAsyncDisposable asyncDisposable:
                        if (logger != null)
                        {
                            LogDisposing(logger, objectName);
                        }
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;

                    case IDisposable syncDisposable:
                        if (logger != null)
                        {
                            LogDisposingSync(logger, objectName);
                        }
                        syncDisposable.Dispose();
                        break;

                    default:
                        if (logger != null)
                        {
                            LogNotDisposable(logger, objectName);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    LogDisposeError(logger, ex, objectName);
                }
            }
        }

        /// <summary>
        /// Safely disposes an object synchronously with logging.
        /// </summary>
        /// <param name="disposable">The object to dispose</param>
        /// <param name="logger">Logger for recording disposal events</param>
        /// <param name="objectName">Name of the object being disposed for logging</param>
        public static void SafeDispose(object? disposable, ILogger? logger, string objectName = "object")
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                switch (disposable)
                {
                    case IDisposable syncDisposable:
                        if (logger != null)
                        {
                            LogDisposingSync(logger, objectName);
                        }
                        syncDisposable.Dispose();
                        break;

                    case IAsyncDisposable asyncDisposable:
                        if (logger != null)
                        {
                            LogDisposingAsyncBlocking(logger, objectName);
                        }
                        // Best effort sync disposal of async disposable
                        // VSTHRD002: This is intentional - synchronous context requires blocking wait
                        // This is a fallback for async resources in sync disposal
                        try
                        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                            _ = asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
#pragma warning restore VSTHRD002
                        }
                        catch (TimeoutException)
                        {
                            if (logger != null)
                            {
                                LogAsyncDisposalTimeout(logger, objectName);
                            }
                        }
                        break;

                    default:
                        if (logger != null)
                        {
                            LogNotDisposable(logger, objectName);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    LogDisposeError(logger, ex, objectName);
                }
            }
        }

        /// <summary>
        /// Safely disposes multiple objects in sequence.
        /// </summary>
        /// <param name="disposables">The objects to dispose</param>
        /// <param name="logger">Logger for recording disposal events</param>
        /// <param name="objectNames">Names of the objects being disposed (optional)</param>
        public static async ValueTask SafeDisposeAllAsync(
            IEnumerable<object?> disposables,
            ILogger? logger,
            IEnumerable<string>? objectNames = null)
        {
            var names = objectNames?.ToArray();
            var index = 0;

            foreach (var disposable in disposables)
            {
                var objectName = names != null && index < names.Length
                    ? names[index]
                    : $"object[{index}]";

                await SafeDisposeAsync(disposable, logger, objectName).ConfigureAwait(false);
                index++;
            }
        }

        /// <summary>
        /// Safely disposes multiple objects in sequence synchronously.
        /// </summary>
        /// <param name="disposables">The objects to dispose</param>
        /// <param name="logger">Logger for recording disposal events</param>
        /// <param name="objectNames">Names of the objects being disposed (optional)</param>
        public static void SafeDisposeAll(
            IEnumerable<object?> disposables,
            ILogger? logger,
            IEnumerable<string>? objectNames = null)
        {
            var names = objectNames?.ToArray();
            var index = 0;

            foreach (var disposable in disposables)
            {
                var objectName = names != null && index < names.Length
                    ? names[index]
                    : $"object[{index}]";

                SafeDispose(disposable, logger, objectName);
                index++;
            }
        }

        /// <summary>
        /// Common disposal pattern for components with synchronization requirements.
        /// </summary>
        /// <param name="synchronizeFunc">Function to synchronize the component</param>
        /// <param name="disposables">Objects to dispose after synchronization</param>
        /// <param name="logger">Logger for recording events</param>
        /// <param name="componentName">Name of the component for logging</param>
        public static async ValueTask DisposeWithSynchronizationAsync(
            Func<ValueTask> synchronizeFunc,
            IEnumerable<object?> disposables,
            ILogger? logger,
            string componentName = "component")
        {
            try
            {
                if (logger != null)
                {
                    LogSynchronizingBeforeDisposal(logger, componentName);
                }
                await synchronizeFunc().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    LogSynchronizationError(logger, ex, componentName);
                }
            }

            await SafeDisposeAllAsync(disposables, logger).ConfigureAwait(false);
        }

        /// <summary>
        /// Common disposal pattern for components with synchronization requirements (synchronous).
        /// </summary>
        /// <param name="synchronizeAction">Action to synchronize the component</param>
        /// <param name="disposables">Objects to dispose after synchronization</param>
        /// <param name="logger">Logger for recording events</param>
        /// <param name="componentName">Name of the component for logging</param>
        public static void DisposeWithSynchronization(
            Action synchronizeAction,
            IEnumerable<object?> disposables,
            ILogger? logger,
            string componentName = "component")
        {
            try
            {
                if (logger != null)
                {
                    LogSynchronizingBeforeDisposalSync(logger, componentName);
                }
                synchronizeAction();
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    LogSynchronizationError(logger, ex, componentName);
                }
            }

            SafeDisposeAll(disposables, logger);
        }

        /// <summary>
        /// Thread-safe disposal state management helper.
        /// </summary>
        /// <param name="disposed">Reference to the disposed flag</param>
        /// <param name="lockObject">Lock object for thread safety</param>
        /// <returns>True if disposal should proceed, false if already disposed</returns>
        public static bool TrySetDisposed(ref bool disposed, object lockObject)
        {
            lock (lockObject)
            {
                if (disposed)
                {
                    return false;
                }

                disposed = true;
                return true;
            }
        }

        /// <summary>
        /// Thread-safe disposal state management helper using Interlocked.
        /// </summary>
        /// <param name="disposed">Reference to the disposed flag (as int, 0=not disposed, 1=disposed)</param>
        /// <returns>True if disposal should proceed, false if already disposed</returns>
        public static bool TrySetDisposedInterlocked(ref int disposed) => Interlocked.Exchange(ref disposed, 1) == 0;

        /// <summary>
        /// Common pattern for components that need to free native resources.
        /// </summary>
        /// <param name="nativeHandles">Dictionary of native handles to release</param>
        /// <param name="releaseFunc">Function to release a native handle</param>
        /// <param name="logger">Logger for recording events</param>
        /// <param name="componentName">Name of the component for logging</param>
        public static void ReleaseNativeResources<T>(
        IDictionary<string, T> nativeHandles,
        Action<T> releaseFunc,
        ILogger? logger,
        string componentName = "component")
        {
            foreach (var kvp in nativeHandles)
            {
                try
                {
                    if (!EqualityComparer<T>.Default.Equals(kvp.Value, default!))
                    {
                        if (logger != null)
                        {
                            LogReleasingNativeResource(logger, kvp.Key, componentName);
                        }
                        releaseFunc(kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    if (logger != null)
                    {
                        LogNativeResourceReleaseError(logger, ex, kvp.Key, componentName);
                    }
                }
            }

            nativeHandles.Clear();
        }

        /// <summary>
        /// Validates that an object is not disposed and throws ObjectDisposedException if it is.
        /// </summary>
        /// <param name="disposed">The disposed flag</param>
        /// <param name="objectInstance">The object instance to validate</param>
        public static void ThrowIfDisposed(bool disposed, object objectInstance) => ObjectDisposedException.ThrowIf(disposed, objectInstance);

        /// <summary>
        /// Validates that an object is not disposed using Interlocked and throws ObjectDisposedException if it is.
        /// </summary>
        /// <param name="disposed">The disposed flag (as int)</param>
        /// <param name="objectInstance">The object instance to validate</param>
        public static void ThrowIfDisposedInterlocked(int disposed, object objectInstance) => ObjectDisposedException.ThrowIf(disposed != 0, objectInstance);
    }
}
