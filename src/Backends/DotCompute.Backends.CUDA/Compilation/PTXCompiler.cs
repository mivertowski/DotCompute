// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Handles PTX (Parallel Thread Execution) compilation for CUDA kernels using NVRTC.
/// Provides optimized compilation pipeline with name mangling support and error handling.
/// </summary>
internal static partial class PTXCompiler
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6550,
        Level = LogLevel.Debug,
        Message = "PTX compilation successful for kernel {KernelName} in {ElapsedMs}ms, size: {PtxSize} bytes")]
    private static partial void LogPtxCompilationSuccess(ILogger logger, string kernelName, long elapsedMs, int ptxSize);

    [LoggerMessage(
        EventId = 6551,
        Level = LogLevel.Error,
        Message = "PTX compilation failed for kernel {KernelName}")]
    private static partial void LogPtxCompilationFailed(ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(
        EventId = 6552,
        Level = LogLevel.Warning,
        Message = "Failed to cleanup NVRTC program for kernel {KernelName}")]
    private static partial void LogCleanupFailure(ILogger logger, Exception exception, string kernelName);

    #endregion
    // Static storage for mangled function names - shared across all compiler instances
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _mangledNamesCache = new();

    /// <summary>
    /// Checks if NVRTC is available on the system.
    /// </summary>
    /// <returns>True if NVRTC is available; otherwise, false.</returns>
    public static bool IsNvrtcAvailable()
    {
        try
        {
            var result = NvrtcInterop.nvrtcVersion(out _, out _);
            return result == NvrtcResult.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the NVRTC version.
    /// </summary>
    /// <returns>A tuple containing the major and minor version numbers.</returns>
    public static (int major, int minor) GetNvrtcVersion()
    {
        var result = NvrtcInterop.nvrtcVersion(out var major, out var minor);
        if (result != NvrtcResult.Success)
        {
            return (0, 0);
        }
        return (major, minor);
    }

    /// <summary>
    /// Compiles CUDA source code to PTX using NVRTC.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to compile.</param>
    /// <param name="kernelName">Name of the kernel for identification.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="logger">Logger for compilation events.</param>
    /// <returns>Compiled PTX bytecode.</returns>
    public static async Task<byte[]> CompileToPtxAsync(
        string cudaSource,
        string kernelName,
        CompilationOptions? options,
        ILogger logger)
    {
        var stopwatch = Stopwatch.StartNew();
        var program = IntPtr.Zero;
        var registeredFunctionNames = new List<string>();

        try
        {
            // Create NVRTC program
            var result = NvrtcInterop.CreateProgram(
                out program,
                cudaSource,
                kernelName + ".cu",
                null, // headers
                null  // includeNames
            );
            NvrtcInterop.CheckResult(result, "creating NVRTC program");

            // Extract function names from CUDA source and register them with NVRTC
            var functionNames = ExtractKernelFunctionNames(cudaSource);
            foreach (var funcName in functionNames)
            {
                // Register name expression for kernel function
                var nameExpression = $"&{funcName}";
                result = NvrtcInterop.nvrtcAddNameExpression(program, nameExpression);
                if (result == NvrtcResult.Success)
                {
                    registeredFunctionNames.Add(funcName);
                }
            }

            // Build compilation options
            var compilationOptions = BuildPTXCompilationOptions(options);

            // Compile the program
            result = NvrtcInterop.CompileProgram(program, compilationOptions);

            // Get compilation log regardless of success/failure
            var compilerLog = await GetCompilationLogAsync(program).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(compilerLog))
            {
                if (result != NvrtcResult.Success)
                {
                    var errorDetails = $"NVRTC compilation failed for kernel '{kernelName}': {NvrtcInterop.GetErrorString(result)}";
                    if (!string.IsNullOrWhiteSpace(compilerLog))
                    {
                        errorDetails += $"\nCompilation Log:\n{compilerLog}";
                    }
                    throw new KernelCompilationException(errorDetails, compilerLog);
                }
            }

            // Check compilation result
            if (result != NvrtcResult.Success)
            {
                var errorDetails = $"NVRTC compilation failed for kernel '{kernelName}': {NvrtcInterop.GetErrorString(result)}";
                throw new KernelCompilationException(errorDetails, compilerLog);
            }

            // Get lowered (mangled) names for all registered functions
            var mangledNames = new Dictionary<string, string>();
            foreach (var funcName in registeredFunctionNames)
            {
                var nameExpression = $"&{funcName}";
                var mangledName = NvrtcInterop.GetLoweredName(program, nameExpression);
                if (!string.IsNullOrEmpty(mangledName))
                {
                    mangledNames[funcName] = mangledName;
                }
                else
                {
                    // Fallback to original name if mangling fails
                    mangledNames[funcName] = funcName;
                }
            }

            // Store mangled names for later access
            if (mangledNames.Count > 0)
            {
                StoreMangledNames(kernelName, mangledNames);
            }

            // Get PTX code
            var ptxBytes = NvrtcInterop.GetPtxCode(program);

            stopwatch.Stop();
            LogPtxCompilationSuccess(logger, kernelName, stopwatch.ElapsedMilliseconds, ptxBytes.Length);

            return ptxBytes;
        }
        catch (Exception ex) when (ex is not KernelCompilationException)
        {
            LogPtxCompilationFailed(logger, ex, kernelName);
            throw new KernelCompilationException($"NVRTC compilation failed for kernel '{kernelName}'", ex);
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
    /// Builds PTX-specific compilation options.
    /// </summary>
    private static string[] BuildPTXCompilationOptions(CompilationOptions? options)
    {
        var compilationOptions = new List<string>();
        var (major, minor) = GetTargetComputeCapability();

        // Set target compute capability
        compilationOptions.Add($"--gpu-architecture=compute_{major}{minor}");

        // Add optimization level
        var optimizationLevel = options?.OptimizationLevel ?? OptimizationLevel.O2;
        var optFlag = optimizationLevel switch
        {
            OptimizationLevel.None => "-O0",
            OptimizationLevel.O1 => "-O1",
            OptimizationLevel.O2 => "-O2",
            OptimizationLevel.O3 => "-O3",
            _ => "-O2"
        };
        compilationOptions.Add(optFlag);

        // Add debug info if requested
        if (options?.GenerateDebugInfo == true)
        {
            compilationOptions.Add("-g");
            compilationOptions.Add("-lineinfo");
        }

        // Add device debug if requested
        if (options?.EnableDeviceDebugging == true)
        {
            compilationOptions.Add("-G");
        }

        // Add standard includes and common flags
        compilationOptions.Add("-std=c++17");
        compilationOptions.Add("--use_fast_math");

        return [.. compilationOptions];
    }

    /// <summary>
    /// Gets the target compute capability based on the current device.
    /// </summary>
    private static (int major, int minor) GetTargetComputeCapability()
        // Get compute capability from CUDA capability manager
        // Cap at compute_86 for CUDA 12.8 compatibility

        => (8, 6);

    /// <summary>
    /// Extracts kernel function names from CUDA source code.
    /// </summary>
    private static List<string> ExtractKernelFunctionNames(string cudaSource)
    {
        var functionNames = new List<string>();
        var lines = cudaSource.Split('\n');

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("extern \"C\" __global__", StringComparison.OrdinalIgnoreCase) ||
                trimmedLine.StartsWith("__global__", StringComparison.OrdinalIgnoreCase))
            {
                // Extract function name
                var parenIndex = trimmedLine.IndexOf('(', StringComparison.CurrentCulture);
                if (parenIndex > 0)
                {
                    var beforeParen = trimmedLine[..parenIndex];
                    var parts = beforeParen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var functionName = parts[^1]; // Last part is the function name
                        if (!string.IsNullOrEmpty(functionName))
                        {
                            functionNames.Add(functionName);
                        }
                    }
                }
            }
        }

        return functionNames;
    }

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

    /// <summary>
    /// Stores mangled function names for later retrieval.
    /// </summary>
    private static void StoreMangledNames(string kernelName, Dictionary<string, string> mangledNames) => _ = _mangledNamesCache.AddOrUpdate(kernelName, mangledNames, (_, _) => mangledNames);

    /// <summary>
    /// Retrieves stored mangled names for a kernel.
    /// </summary>
    public static Dictionary<string, string>? GetMangledNames(string kernelName) => _mangledNamesCache.TryGetValue(kernelName, out var names) ? names : null;
}