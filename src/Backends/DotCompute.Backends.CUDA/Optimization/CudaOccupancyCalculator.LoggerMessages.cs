// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Optimization
{
    /// <summary>
    /// LoggerMessage delegates for CudaOccupancyCalculator
    /// Event IDs: 25026-25040
    /// </summary>
    public sealed partial class CudaOccupancyCalculator
    {
        [LoggerMessage(EventId = 25026, Level = LogLevel.Information,
            Message = "CUDA Occupancy Calculator initialized")]
        private static partial void LogInitialized(ILogger logger);

        [LoggerMessage(EventId = 25027, Level = LogLevel.Debug,
            Message = "Calculating optimal launch config for kernel on device {DeviceId}")]
        private static partial void LogCalculatingConfig(ILogger logger, int deviceId);

        [LoggerMessage(EventId = 25028, Level = LogLevel.Information,
            Message = "Optimal launch config: Grid={GridSize}, Block={BlockSize}, Occupancy={Occupancy:P}")]
        private static partial void LogOptimalConfig(ILogger logger, object gridSize, object blockSize, double occupancy);

        [LoggerMessage(EventId = 25029, Level = LogLevel.Warning,
            Message = "Failed to calculate occupancy for block size {BlockSize}")]
        private static partial void LogOccupancyCalculationFailed(ILogger logger, Exception ex, int blockSize);

        [LoggerMessage(EventId = 25030, Level = LogLevel.Warning,
            Message = "Failed to test 2D configuration {BlockDim}")]
        private static partial void Log2DConfigTestFailed(ILogger logger, Exception ex, object blockDim);

        [LoggerMessage(EventId = 25031, Level = LogLevel.Information,
            Message = "Optimal 2D config: Grid=({GridX},{GridY}), Block=({BlockX},{BlockY}), Occupancy={Occupancy:P}")]
        private static partial void LogOptimal2DConfig(ILogger logger, int gridX, int gridY, int blockX, int blockY, double occupancy);

        [LoggerMessage(EventId = 25032, Level = LogLevel.Information,
            Message = "Dynamic parallelism config: Max depth={Depth}, Max children/parent={MaxChildren}")]
        private static partial void LogDynamicParallelismConfig(ILogger logger, int depth, int maxChildren);
    }
}
