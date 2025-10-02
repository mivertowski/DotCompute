// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using MSLogger = Microsoft.Extensions.Logging.ILogger;

namespace DotCompute.Algorithms.Management.Types
{
    /// <summary>
    /// Adapter to bridge Microsoft.Extensions.Logging.ILogger to NuGet.Common.ILogger.
    /// Provides compatibility between .NET logging and NuGet package operations.
    /// </summary>
    internal sealed class NuGetLogger(MSLogger logger) : NuGet.Common.ILogger
    {
        private readonly MSLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        /// <summary>
        /// Performs log debug.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogDebug(string data) => _logger.LogDebug("{Data}", data);
        /// <summary>
        /// Performs log verbose.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogVerbose(string data) => _logger.LogTrace("{Data}", data);
        /// <summary>
        /// Performs log information.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogInformation(string data) => _logger.LogInformation("{Data}", data);
        /// <summary>
        /// Performs log minimal.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogMinimal(string data) => _logger.LogInformation("{Data}", data);
        /// <summary>
        /// Performs log warning.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogWarning(string data) => _logger.LogWarning("{Data}", data);
        /// <summary>
        /// Performs log error.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogError(string data) => _logger.LogError("{Data}", data);
        /// <summary>
        /// Performs log information summary.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogInformationSummary(string data) => _logger.LogInformation("{Data}", data);
        /// <summary>
        /// Performs log error summary.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogErrorSummary(string data) => _logger.LogError("{Data}", data);
        /// <summary>
        /// Performs log.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="data">The data.</param>

        public void Log(NuGet.Common.LogLevel level, string data)
        {
            var msLogLevel = level switch
            {
                NuGet.Common.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                NuGet.Common.LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
                NuGet.Common.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                NuGet.Common.LogLevel.Minimal => Microsoft.Extensions.Logging.LogLevel.Information,
                NuGet.Common.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                NuGet.Common.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                _ => Microsoft.Extensions.Logging.LogLevel.Information
            };

            _logger.Log(msLogLevel, "{Data}", data);
        }
        /// <summary>
        /// Gets log asynchronously.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="data">The data.</param>
        /// <returns>The result of the operation.</returns>

        public async Task LogAsync(NuGet.Common.LogLevel level, string data)
        {
            Log(level, data);
            await Task.CompletedTask;
        }
        /// <summary>
        /// Performs log.
        /// </summary>
        /// <param name="message">The message.</param>

        public void Log(NuGet.Common.ILogMessage message) => Log(message.Level, message.Message);
        /// <summary>
        /// Gets log asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>

        public async Task LogAsync(NuGet.Common.ILogMessage message)
        {
            Log(message);
            await Task.CompletedTask;
        }
    }
}