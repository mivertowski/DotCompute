// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.P2P
{
    public sealed partial class CudaP2PManager
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 27046,
            Level = LogLevel.Information,
            Message = "CUDA P2P Manager initialized for multi-GPU operations")]
        private static partial void LogManagerInitialized(ILogger logger);

        [LoggerMessage(
            EventId = 27047,
            Level = LogLevel.Debug,
            Message = "P2P connection available: {SourceDevice} -> {DestinationDevice} ({BandwidthGBps:F2} GB/s)")]
        private static partial void LogConnectionAvailable(ILogger logger, int sourceDevice, int destinationDevice, double bandwidthGBps);

        [LoggerMessage(
            EventId = 27048,
            Level = LogLevel.Information,
            Message = "Discovered P2P topology: {DeviceCount} devices, {ConnectionCount} connections, fully connected: {IsFullyConnected}")]
        private static partial void LogTopologyDiscovered(ILogger logger, int deviceCount, int connectionCount, bool isFullyConnected);

        [LoggerMessage(
            EventId = 27049,
            Level = LogLevel.Information,
            Message = "Enabled P2P access: {SourceDevice} -> {DestinationDevice}")]
        private static partial void LogAccessEnabled(ILogger logger, int sourceDevice, int destinationDevice);

        [LoggerMessage(
            EventId = 27050,
            Level = LogLevel.Error,
            Message = "Failed to enable P2P access: {SourceDevice} -> {DestinationDevice}")]
        private static partial void LogEnableAccessError(ILogger logger, Exception ex, int sourceDevice, int destinationDevice);

        [LoggerMessage(
            EventId = 27051,
            Level = LogLevel.Information,
            Message = "Disabled P2P access: {SourceDevice} -> {DestinationDevice}")]
        private static partial void LogAccessDisabled(ILogger logger, int sourceDevice, int destinationDevice);

        [LoggerMessage(
            EventId = 27052,
            Level = LogLevel.Error,
            Message = "Failed to disable P2P access: {SourceDevice} -> {DestinationDevice}")]
        private static partial void LogDisableAccessError(ILogger logger, Exception ex, int sourceDevice, int destinationDevice);

        [LoggerMessage(
            EventId = 27053,
            Level = LogLevel.Debug,
            Message = "P2P transfer completed: {SourceDevice} -> {DestinationDevice}, {SizeBytes} bytes, {BandwidthGBps:F2} GB/s")]
        private static partial void LogTransferCompleted(ILogger logger, int sourceDevice, int destinationDevice, ulong sizeBytes, double bandwidthGBps);

        [LoggerMessage(
            EventId = 27054,
            Level = LogLevel.Error,
            Message = "P2P transfer failed: {SourceDevice} -> {DestinationDevice}, {SizeBytes} bytes")]
        private static partial void LogTransferError(ILogger logger, Exception ex, int sourceDevice, int destinationDevice, ulong sizeBytes);

        [LoggerMessage(
            EventId = 27055,
            Level = LogLevel.Information,
            Message = "Optimized data placement for {ChunkCount} chunks across {DeviceCount} devices")]
        private static partial void LogDataPlacementOptimized(ILogger logger, int chunkCount, int deviceCount);

        [LoggerMessage(
            EventId = 27056,
            Level = LogLevel.Debug,
            Message = "Initializing P2P manager with {DeviceCount} devices")]
        private static partial void LogInitializingManager(ILogger logger, int deviceCount);

        [LoggerMessage(
            EventId = 27057,
            Level = LogLevel.Warning,
            Message = "Error testing P2P access {Source} -> {Destination}")]
        private static partial void LogTestAccessError(ILogger logger, Exception ex, int source, int destination);

        [LoggerMessage(
            EventId = 27058,
            Level = LogLevel.Warning,
            Message = "Error measuring P2P bandwidth {Source} -> {Destination}")]
        private static partial void LogMeasureBandwidthError(ILogger logger, Exception ex, int source, int destination);

        [LoggerMessage(
            EventId = 27059,
            Level = LogLevel.Debug,
            Message = "P2P Status: {EnabledConnections}/{TotalConnections} connections enabled, {TotalTransfers} transfers, {AverageBandwidthGBps:F2} GB/s avg")]
        private static partial void LogConnectionStatus(ILogger logger, int enabledConnections, int totalConnections, long totalTransfers, double averageBandwidthGBps);

        [LoggerMessage(
            EventId = 27060,
            Level = LogLevel.Warning,
            Message = "Error monitoring P2P connections")]
        private static partial void LogMonitoringError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 27061,
            Level = LogLevel.Warning,
            Message = "Error disabling P2P connection during disposal: {Source} -> {Destination}")]
        private static partial void LogDisposalError(ILogger logger, Exception ex, int source, int destination);

        [LoggerMessage(
            EventId = 27062,
            Level = LogLevel.Information,
            Message = "CUDA P2P Manager disposed")]
        private static partial void LogManagerDisposed(ILogger logger);

        #endregion
    }
}
