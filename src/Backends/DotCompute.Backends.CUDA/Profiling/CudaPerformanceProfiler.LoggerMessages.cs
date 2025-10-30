// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Profiling
{
    /// <summary>
    /// LoggerMessage delegates for CudaPerformanceProfiler.
    /// </summary>
    public sealed partial class CudaPerformanceProfiler
    {
        // Event IDs 5900-5949

        [LoggerMessage(
            EventId = 5900,
            Level = LogLevel.Information,
            Message = "CUDA Performance Profiler initialized")]
        private partial void LogProfilerInitialized();

        [LoggerMessage(
            EventId = 5901,
            Level = LogLevel.Warning,
            Message = "Profiling already active")]
        private partial void LogProfilingAlreadyActive();

        [LoggerMessage(
            EventId = 5902,
            Level = LogLevel.Information,
            Message = "Starting profiling session with config: {Config}")]
        private partial void LogProfilingSessionStart(object config);

        [LoggerMessage(
            EventId = 5903,
            Level = LogLevel.Information,
            Message = "Profiling session started successfully")]
        private partial void LogProfilingSessionStarted();

        [LoggerMessage(
            EventId = 5904,
            Level = LogLevel.Warning,
            Message = "No active profiling session")]
        private partial void LogNoActiveSession();

        [LoggerMessage(
            EventId = 5905,
            Level = LogLevel.Information,
            Message = "Stopping profiling session")]
        private partial void LogStoppingProfilingSession();

        [LoggerMessage(
            EventId = 5906,
            Level = LogLevel.Information,
            Message = "Profiling session stopped. Kernels profiled: {KernelCount}, Memory ops: {MemoryCount}")]
        private partial void LogProfilingSessionStopped(int kernelCount, int memoryCount);

        [LoggerMessage(
            EventId = 5907,
            Level = LogLevel.Debug,
            Message = "Profiling kernel {KernelName}")]
        private partial void LogProfilingKernel(string kernelName);

        [LoggerMessage(
            EventId = 5908,
            Level = LogLevel.Information,
            Message = "Kernel {KernelName} profiled: Avg={AvgTime:F3}ms, Min={MinTime:F3}ms, Max={MaxTime:F3}ms, StdDev={StdDev:F3}ms")]
        private partial void LogKernelProfiled(string kernelName, double avgTime, double minTime, double maxTime, double stdDev);

        [LoggerMessage(
            EventId = 5909,
            Level = LogLevel.Error,
            Message = "Error collecting GPU metrics")]
        private partial void LogGpuMetricsError(Exception ex);

        [LoggerMessage(
            EventId = 5910,
            Level = LogLevel.Error,
            Message = "Error in CUPTI callback handler")]
        private partial void LogCuptiCallbackError(Exception ex);

        [LoggerMessage(
            EventId = 5911,
            Level = LogLevel.Error,
            Message = "Error processing profiling event")]
        private partial void LogProfilingEventError(Exception ex);

        [LoggerMessage(
            EventId = 5912,
            Level = LogLevel.Debug,
            Message = "Kernel launch event captured")]
        private partial void LogKernelLaunchEvent();

        [LoggerMessage(
            EventId = 5913,
            Level = LogLevel.Debug,
            Message = "Memory transfer event captured")]
        private partial void LogMemoryTransferEvent();

        [LoggerMessage(
            EventId = 5914,
            Level = LogLevel.Debug,
            Message = "Driver event captured: {CallbackId}")]
        private partial void LogDriverEvent(uint callbackId);

        [LoggerMessage(
            EventId = 5915,
            Level = LogLevel.Debug,
            Message = "Resource event captured: {CallbackId}")]
        private partial void LogResourceEvent(uint callbackId);

        [LoggerMessage(
            EventId = 5916,
            Level = LogLevel.Debug,
            Message = "GPU Metrics - Util: {GpuUtil}%, Mem: {MemUtil}%, Temp: {Temp}°C, Power: {Power}W")]
        private partial void LogGpuMetrics(uint gpuUtil, uint memUtil, uint temp, double power);

        [LoggerMessage(
            EventId = 5917,
            Level = LogLevel.Error,
            Message = "Error collecting periodic metrics")]
        private partial void LogPeriodicMetricsError(Exception ex);

        [LoggerMessage(
            EventId = 5918,
            Level = LogLevel.Information,
            Message = "Profiling report exported to {FilePath}")]
        private partial void LogReportExported(string filePath);

        [LoggerMessage(
            EventId = 5919,
            Level = LogLevel.Error,
            Message = "Error exporting profiling report")]
        private partial void LogReportExportError(Exception ex);

        [LoggerMessage(
            EventId = 5920,
            Level = LogLevel.Information,
            Message = "NVML initialized successfully")]
        private partial void LogNvmlInitialized();

        [LoggerMessage(
            EventId = 5921,
            Level = LogLevel.Warning,
            Message = "Failed to initialize NVML: {Result}")]
        private partial void LogNvmlInitFailed(object result);

        [LoggerMessage(
            EventId = 5922,
            Level = LogLevel.Warning,
            Message = "NVML not available, GPU metrics will be limited")]
        private partial void LogNvmlNotAvailable(Exception ex);
    }
}
