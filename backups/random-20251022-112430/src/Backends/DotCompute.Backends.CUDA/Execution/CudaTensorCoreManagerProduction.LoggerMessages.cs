// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Advanced;

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

        [LoggerMessage(
            EventId = 5952,
            Level = LogLevel.Debug,
            Message = "Launching tensor core GEMM: [{M}x{K}] x [{K}x{N}] = [{M}x{N}], Type: {Input}->{Output}")]
        public static partial void LogTensorGemmLaunch(
            ILogger logger, int m, int k, int n, DataType input, DataType output);

        [LoggerMessage(
            EventId = 5953,
            Level = LogLevel.Information,
            Message = "Tensor core GEMM completed in {Time:F2}ms, {GFLOPS:F2} GFLOPS ({Efficiency:F1}% efficiency)")]
        public static partial void LogTensorGemmComplete(
            ILogger logger, double time, double gflops, double efficiency);

        [LoggerMessage(
            EventId = 5954,
            Level = LogLevel.Error,
            Message = "Tensor core operation failed")]
        public static partial void LogTensorOperationFailed(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 5955,
            Level = LogLevel.Debug,
            Message = "Launching tensor core convolution: Input[{N},{C},{H},{W}], Filter[{K},{FC},{R},{S}]")]
        public static partial void LogConvolutionLaunch(
            ILogger logger, int n, int c, int h, int w, int k, int fc, int r, int s);

        [LoggerMessage(
            EventId = 5956,
            Level = LogLevel.Debug,
            Message = "Using cached tensor core kernel")]
        public static partial void LogCachedKernel(ILogger logger);

        [LoggerMessage(
            EventId = 5957,
            Level = LogLevel.Information,
            Message = "Compiling new tensor core kernel")]
        public static partial void LogCompilingKernel(ILogger logger);

        [LoggerMessage(
            EventId = 5958,
            Level = LogLevel.Debug,
            Message = "Launching kernel with grid({Gx},{Gy},{Gz}) block({Bx},{By},{Bz})")]
        public static partial void LogKernelLaunch(
            ILogger logger, uint gx, uint gy, uint gz, uint bx, uint by, uint bz);
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
