// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel chain profiling service.
    /// </summary>
    public sealed partial class DefaultKernelChainProfiler(ILogger<DefaultKernelChainProfiler>? logger = null) : IKernelChainProfiler
    {
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 15101, Level = MsLogLevel.Debug, Message = "Started profiling session '{ProfileName}'")]
        private static partial void LogProfilingStarted(ILogger logger, string profileName);

        [LoggerMessage(EventId = 15102, Level = MsLogLevel.Debug, Message = "Stopped profiling session '{ProfileName}'")]
        private static partial void LogProfilingStopped(ILogger logger, string profileName);

        [LoggerMessage(EventId = 15103, Level = MsLogLevel.Debug, Message = "Recorded kernel execution: {KernelName} in {ExecutionTimeMs}ms on {Backend}")]
        private static partial void LogKernelExecutionRecorded(ILogger logger, string kernelName, double executionTimeMs, string backend);

        #endregion

        private readonly ILogger<DefaultKernelChainProfiler>? _logger = logger;
        private readonly ConcurrentDictionary<string, KernelChainProfilingResult> _profiles = new();
        private volatile string? _currentProfile;
        private DateTime _startTime;
        /// <summary>
        /// Gets start profiling asynchronously.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task StartProfilingAsync(string profileName, CancellationToken cancellationToken = default)
        {
            _currentProfile = profileName;
            _startTime = DateTime.UtcNow;
            if (_logger != null)
            {
                LogProfilingStarted(_logger, profileName);
            }
            await Task.CompletedTask;
        }
        /// <summary>
        /// Gets stop profiling asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task StopProfilingAsync(CancellationToken cancellationToken = default)
        {
            if (_currentProfile != null)
            {
                var endTime = DateTime.UtcNow;
                var result = new KernelChainProfilingResult
                {
                    ProfileName = _currentProfile,
                    StartTime = _startTime,
                    EndTime = endTime,
                    TotalExecutionTime = endTime - _startTime,
                    KernelExecutions = Array.Empty<KernelExecutionRecord>(),
                    PeakMemoryUsage = GC.GetTotalMemory(false),
                    TotalKernelExecutions = 0
                };


                _ = _profiles.TryAdd(_currentProfile, result);
                if (_logger != null)
                {
                    LogProfilingStopped(_logger, _currentProfile);
                }
                _currentProfile = null;
            }
            await Task.CompletedTask;
        }
        /// <summary>
        /// Gets the profiling result async.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The profiling result async.</returns>

        public async Task<KernelChainProfilingResult?> GetProfilingResultAsync(string profileName, CancellationToken cancellationToken = default)
        {
            _ = _profiles.TryGetValue(profileName, out var result);
            await Task.CompletedTask;
            return result;
        }
        /// <summary>
        /// Gets record kernel execution asynchronously.
        /// </summary>
        /// <param name="kernelName">The kernel name.</param>
        /// <param name="executionTime">The execution time.</param>
        /// <param name="memoryUsed">The memory used.</param>
        /// <param name="backend">The backend.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task RecordKernelExecutionAsync(string kernelName, TimeSpan executionTime, long memoryUsed, string backend, CancellationToken cancellationToken = default)
        {
            if (_logger != null)
            {
                LogKernelExecutionRecorded(_logger, kernelName, executionTime.TotalMilliseconds, backend);
            }
            await Task.CompletedTask;
        }
    }
}
