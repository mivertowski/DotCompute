// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Advanced;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    /// <summary>
    /// LoggerMessage delegates for CudaTensorCoreManagerProduction.
    /// </summary>
    public static partial class CudaTensorCoreManagerProductionLoggers
    {
        // Event IDs 5950-5999

        [LoggerMessage(
            EventId = 5950,
            Level = LogLevel.Warning,
            Message = "Tensor cores not available on this device")]
        public static partial void LogTensorCoresNotAvailable(ILogger logger);

        [LoggerMessage(
            EventId = 5951,
            Level = LogLevel.Information,
            Message = "{Capabilities}")]
        public static partial void LogCapabilities(ILogger logger, string capabilities);

    }
}

namespace DotCompute.Backends.CUDA.Advanced
{
    public sealed partial class CudaTensorCoreManagerProduction
    {
        private void LogCapabilities()
        {
            if (!_tensorCoresAvailable)
            {
                Execution.CudaTensorCoreManagerProductionLoggers.LogTensorCoresNotAvailable(_logger);
                return;
            }

            var caps = new System.Text.StringBuilder();
            _ = caps.AppendLine("Tensor Core Capabilities:");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  WMMA: {_capabilities.WmmaSupported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  FP16: {_capabilities.Fp16Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  BF16: {_capabilities.Bf16Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  TF32: {_capabilities.Tf32Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  FP8: {_capabilities.Fp8Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  INT8: {_capabilities.Int8Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  INT4: {_capabilities.Int4Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  FP64: {_capabilities.Fp64Supported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Sparsity: {_capabilities.SparsitySupported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Transformer Engine: {_capabilities.TransformerEngineSupported}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Max Tile: {_capabilities.MaxWmmaM}x{_capabilities.MaxWmmaN}x{_capabilities.MaxWmmaK}");
            _ = caps.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Peak TFLOPS: {_capabilities.PeakTflops:F2}");

            Execution.CudaTensorCoreManagerProductionLoggers.LogCapabilities(_logger, caps.ToString());
        }
    }
}
