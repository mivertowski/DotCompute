// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel chain profiling service.
    /// </summary>
    public sealed class DefaultKernelChainProfiler : IKernelChainProfiler
    {
        private readonly ILogger<DefaultKernelChainProfiler>? _logger;
        private readonly ConcurrentDictionary<string, KernelChainProfilingResult> _profiles;
        private volatile string? _currentProfile;
        private DateTime _startTime;

        public DefaultKernelChainProfiler(ILogger<DefaultKernelChainProfiler>? logger = null)
        {
            _logger = logger;
            _profiles = new ConcurrentDictionary<string, KernelChainProfilingResult>();
        }

        public async Task StartProfilingAsync(string profileName, CancellationToken cancellationToken = default)
        {
            _currentProfile = profileName;
            _startTime = DateTime.UtcNow;
            _logger?.LogDebug("Started profiling session '{ProfileName}'", profileName);
            await Task.CompletedTask;
        }

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
                
                _profiles.TryAdd(_currentProfile, result);
                _logger?.LogDebug("Stopped profiling session '{ProfileName}'", _currentProfile);
                _currentProfile = null;
            }
            await Task.CompletedTask;
        }

        public async Task<KernelChainProfilingResult?> GetProfilingResultAsync(string profileName, CancellationToken cancellationToken = default)
        {
            _profiles.TryGetValue(profileName, out var result);
            await Task.CompletedTask;
            return result;
        }

        public async Task RecordKernelExecutionAsync(string kernelName, TimeSpan executionTime, long memoryUsed, string backend, CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Recorded kernel execution: {KernelName} in {ExecutionTime}ms on {Backend}", 
                kernelName, executionTime.TotalMilliseconds, backend);
            await Task.CompletedTask;
        }
    }
}
