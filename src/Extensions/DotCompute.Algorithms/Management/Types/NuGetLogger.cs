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
    internal sealed class NuGetLogger : NuGet.Common.ILogger
    {
        private readonly MSLogger _logger;

        public NuGetLogger(MSLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LogDebug(string data)
        {
            _logger.LogDebug("{Data}", data);
        }

        public void LogVerbose(string data)
        {
            _logger.LogTrace("{Data}", data);
        }

        public void LogInformation(string data)
        {
            _logger.LogInformation("{Data}", data);
        }

        public void LogMinimal(string data)
        {
            _logger.LogInformation("{Data}", data);
        }

        public void LogWarning(string data)
        {
            _logger.LogWarning("{Data}", data);
        }

        public void LogError(string data)
        {
            _logger.LogError("{Data}", data);
        }

        public void LogInformationSummary(string data)
        {
            _logger.LogInformation("{Data}", data);
        }

        public void LogErrorSummary(string data)
        {
            _logger.LogError("{Data}", data);
        }

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

        public async Task LogAsync(NuGet.Common.LogLevel level, string data)
        {
            Log(level, data);
            await Task.CompletedTask;
        }

        public void Log(NuGet.Common.ILogMessage message)
        {
            Log(message.Level, message.Message);
        }

        public async Task LogAsync(NuGet.Common.ILogMessage message)
        {
            Log(message);
            await Task.CompletedTask;
        }
    }
}