// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Handles CUBIN (CUDA Binary) compilation for optimized kernel execution.
/// Provides direct binary compilation for improved performance on compatible hardware.
/// </summary>
internal static partial class CubinCompiler
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6050,
        Level = LogLevel.Debug,
        Message = "Starting CUBIN compilation for kernel {KernelName}")]
    private static partial void LogCubinCompilationStart(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 6051,
        Level = LogLevel.Debug,
        Message = "CUBIN compilation options: {Options}")]
    private static partial void LogCubinCompilationOptions(ILogger logger, string options);

    [LoggerMessage(
        EventId = 6052,
        Level = LogLevel.Debug,
        Message = "CUBIN compilation successful for kernel {KernelName} in {ElapsedMs}ms, size: {CubinSize} bytes")]
    private static partial void LogCubinCompilationSuccess(ILogger logger, string kernelName, long elapsedMs, int cubinSize);

    [LoggerMessage(
        EventId = 6053,
        Level = LogLevel.Error,
        Message = "CUBIN compilation failed for kernel {KernelName}")]
    private static partial void LogCubinCompilationFailure(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 6054,
        Level = LogLevel.Warning,
        Message = "Failed to cleanup NVRTC program for CUBIN compilation of kernel {KernelName}")]
    private static partial void LogCleanupFailure(ILogger logger, Exception ex, string kernelName);

    #endregion
    /// <summary>
    /// Compiles CUDA source code to CUBIN format using NVRTC.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to compile.</param>
    /// <param name="kernelName">Name of the kernel for identification.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="logger">Logger for compilation events.</param>
    /// <returns>Compiled CUBIN bytecode.</returns>
    public static async Task<byte[]> CompileToCubinAsync(
        string cudaSource,
        string kernelName,
        CompilationOptions? options,
        ILogger logger)
    {
        var stopwatch = Stopwatch.StartNew();
        var program = IntPtr.Zero;

        try
        {
            LogCubinCompilationStart(logger, kernelName);

            // Create NVRTC program
            var result = NvrtcInterop.CreateProgram(
                out program,
                cudaSource,
                kernelName + ".cu",
                null, // headers
                null  // includeNames
            );
            NvrtcInterop.CheckResult(result, "creating NVRTC program for CUBIN");

            // Build CUBIN-specific compilation options
            var compilationOptions = BuildCubinCompilationOptions(options);

            LogCubinCompilationOptions(logger, string.Join(" ", compilationOptions));

            // Compile the program to CUBIN
            result = NvrtcInterop.CompileProgram(program, compilationOptions);

            // Get compilation log
            var compilerLog = await GetCompilationLogAsync(program).ConfigureAwait(false);

            if (result != NvrtcResult.Success)
            {
                var errorDetails = $"NVRTC CUBIN compilation failed for kernel '{kernelName}': {NvrtcInterop.GetErrorString(result)}";
                if (!string.IsNullOrWhiteSpace(compilerLog))
                {
                    errorDetails += $"\nCompilation Log:\n{compilerLog}";
                }
                throw new KernelCompilationException(errorDetails, compilerLog);
            }

            // Get CUBIN code
            var cubinBytes = NvrtcInterop.GetCubinCode(program);

            stopwatch.Stop();
            LogCubinCompilationSuccess(logger, kernelName, stopwatch.ElapsedMilliseconds, cubinBytes.Length);

            return cubinBytes;
        }
        catch (Exception ex) when (ex is not KernelCompilationException)
        {
            LogCubinCompilationFailure(logger, ex, kernelName);
            throw new KernelCompilationException($"NVRTC CUBIN compilation failed for kernel '{kernelName}'", ex);
        }
        finally
        {
            // Clean up NVRTC program
            if (program != IntPtr.Zero)
            {
                try
                {
                    _ = NvrtcInterop.nvrtcDestroyProgram(ref program);
                }
                catch (Exception ex)
                {
                    LogCleanupFailure(logger, ex, kernelName);
                }
            }
        }
    }

    /// <summary>
    /// Determines if CUBIN compilation should be used based on options and device capability.
    /// </summary>
    /// <param name="options">Compilation options.</param>
    /// <returns>True if CUBIN should be used, false for PTX.</returns>
    public static bool ShouldUseCubin(CompilationOptions? options)
    {
        // Use CUBIN for optimization levels O2 and above, and when device capability is sufficient
        var useOptimizations = options?.OptimizationLevel >= OptimizationLevel.O2;

        var (major, _) = GetTargetComputeCapability();
        var supportsCubin = major >= 7; // CUBIN works best on compute capability 7.0+

        return useOptimizations && supportsCubin && !IsDebugMode(options);
    }

    /// <summary>
    /// Builds CUBIN-specific compilation options.
    /// </summary>
    private static string[] BuildCubinCompilationOptions(CompilationOptions? options)
    {
        var compilationOptions = new List<string>();
        var (major, minor) = GetTargetComputeCapability();

        // Set target architecture for CUBIN (uses compute capability directly)
        compilationOptions.Add($"--gpu-architecture=sm_{major}{minor}");

        // Add optimization level (CUBIN benefits from aggressive optimization)
        var optimizationLevel = options?.OptimizationLevel ?? OptimizationLevel.O3;
        var optFlag = optimizationLevel switch
        {
            OptimizationLevel.None => "-O0",
            OptimizationLevel.O1 => "-O1",
            OptimizationLevel.O2 => "-O2",
            OptimizationLevel.O3 => "-O3",
            OptimizationLevel.Size => "-Os",
            _ => "-O3" // Default to highest optimization for CUBIN
        };
        compilationOptions.Add(optFlag);

        // CUBIN-specific optimizations
        compilationOptions.Add("--use_fast_math");
        compilationOptions.Add("--extra-device-vectorization");

        // Add debug info if requested (though this reduces optimization)
        if (options?.GenerateDebugInfo == true)
        {
            compilationOptions.Add("-lineinfo");
        }

        // Add device debug if explicitly requested
        if (options?.EnableDeviceDebugging == true)
        {
            compilationOptions.Add("-G");
        }

        // Standard language version
        compilationOptions.Add("-std=c++17");

        // CUBIN-specific flags for better performance
        if (options?.OptimizationLevel >= OptimizationLevel.O2)
        {
            compilationOptions.Add("--maxrregcount=32"); // Limit register usage for better occupancy
        }

        return [.. compilationOptions];
    }

    /// <summary>
    /// Gets the target compute capability for CUBIN compilation.
    /// </summary>
    private static (int major, int minor) GetTargetComputeCapability()
        // For CUBIN, we target the actual device capability
        // Cap at compute_86 for CUDA 12.8 compatibility

        => (8, 6);

    /// <summary>
    /// Checks if debug mode is enabled.
    /// </summary>
    private static bool IsDebugMode(CompilationOptions? options) => options?.GenerateDebugInfo == true || options?.EnableDeviceDebugging == true;

    /// <summary>
    /// Gets the compilation log from NVRTC program.
    /// </summary>
    private static Task<string> GetCompilationLogAsync(IntPtr program)
    {
        try
        {
            var log = NvrtcInterop.GetCompilationLog(program);
            return Task.FromResult(log);
        }
        catch (Exception)
        {
            return Task.FromResult(string.Empty);
        }
    }
}