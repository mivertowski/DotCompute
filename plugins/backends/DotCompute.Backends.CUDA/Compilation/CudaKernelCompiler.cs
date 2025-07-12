// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DotCompute.Core.Abstractions;
using DotCompute.Core.Abstractions.Compilation;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// CUDA kernel compiler implementation using NVRTC
/// </summary>
public class CudaKernelCompiler : IKernelCompiler
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CudaCompiledKernel> _kernelCache;
    private readonly string _tempDirectory;
    private bool _disposed;

    public CudaKernelCompiler(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernelCache = new ConcurrentDictionary<string, CudaCompiledKernel>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DotCompute.CUDA", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    public async Task<ICompiledKernel> CompileAsync(IKernelSource source, CompilationOptions? options = null)
    {
        ThrowIfDisposed();
        
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        try
        {
            _logger.LogInformation("Compiling CUDA kernel: {KernelName}", source.Name);

            // Check cache first
            var cacheKey = GenerateCacheKey(source, options);
            if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
            {
                _logger.LogDebug("Using cached kernel: {KernelName}", source.Name);
                return cachedKernel;
            }

            // Prepare CUDA source code
            var cudaSource = await PrepareCudaSourceAsync(source, options);
            
            // Compile to PTX
            var ptx = await CompileToPtxAsync(cudaSource, source.Name, options);
            
            // Create compiled kernel
            var compiledKernel = new CudaCompiledKernel(
                _context,
                source.Name,
                source.EntryPoint,
                ptx,
                options,
                _logger);

            // Cache the compiled kernel
            _kernelCache.TryAdd(cacheKey, compiledKernel);

            _logger.LogInformation("Successfully compiled CUDA kernel: {KernelName}", source.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile CUDA kernel: {KernelName}", source.Name);
            throw new CompilationException($"Failed to compile CUDA kernel '{source.Name}'", ex);
        }
    }

    public async Task<ICompiledKernel[]> CompileBatchAsync(IKernelSource[] sources, CompilationOptions? options = null)
    {
        ThrowIfDisposed();
        
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));

        // Compile kernels in parallel
        var tasks = sources.Select(source => CompileAsync(source, options)).ToArray();
        return await Task.WhenAll(tasks);
    }

    public bool TryGetCached(string kernelName, out ICompiledKernel? compiledKernel)
    {
        ThrowIfDisposed();
        
        // Simple cache lookup by name (without options consideration)
        var cachedKernel = _kernelCache.Values.FirstOrDefault(k => k.Name == kernelName);
        compiledKernel = cachedKernel;
        return cachedKernel != null;
    }

    public void ClearCache()
    {
        ThrowIfDisposed();
        
        _logger.LogInformation("Clearing CUDA kernel cache");
        
        foreach (var kernel in _kernelCache.Values)
        {
            kernel.Dispose();
        }
        
        _kernelCache.Clear();
    }

    private async Task<string> PrepareCudaSourceAsync(IKernelSource source, CompilationOptions? options)
    {
        var builder = new StringBuilder();

        // Add standard CUDA headers
        builder.AppendLine("// Auto-generated CUDA kernel");
        builder.AppendLine("#include <cuda_runtime.h>");
        builder.AppendLine("#include <device_launch_parameters.h>");
        builder.AppendLine();

        // Add custom includes from options
        if (options?.IncludePaths != null)
        {
            foreach (var include in options.IncludePaths)
            {
                builder.AppendLine($"#include \"{include}\"");
            }
            builder.AppendLine();
        }

        // Add preprocessor definitions
        if (options?.Defines != null)
        {
            foreach (var (key, value) in options.Defines)
            {
                builder.AppendLine($"#define {key} {value}");
            }
            builder.AppendLine();
        }

        // Add the kernel source code
        switch (source.Language)
        {
            case KernelLanguage.Cuda:
                builder.Append(source.Code);
                break;
                
            case KernelLanguage.OpenCL:
                // Convert OpenCL to CUDA syntax
                builder.Append(ConvertOpenClToCuda(source.Code));
                break;
                
            default:
                throw new NotSupportedException($"Kernel language '{source.Language}' is not supported by CUDA compiler");
        }

        return builder.ToString();
    }

    private string ConvertOpenClToCuda(string openClCode)
    {
        // Basic OpenCL to CUDA conversion
        // This is a simplified conversion - a production system would need more sophisticated translation
        var cudaCode = openClCode;

        // Replace OpenCL keywords with CUDA equivalents
        cudaCode = cudaCode.Replace("__kernel", "__global__");
        cudaCode = cudaCode.Replace("__global", "__device__");
        cudaCode = cudaCode.Replace("__local", "__shared__");
        cudaCode = cudaCode.Replace("__constant", "__constant__");
        cudaCode = cudaCode.Replace("get_global_id(0)", "blockIdx.x * blockDim.x + threadIdx.x");
        cudaCode = cudaCode.Replace("get_global_id(1)", "blockIdx.y * blockDim.y + threadIdx.y");
        cudaCode = cudaCode.Replace("get_global_id(2)", "blockIdx.z * blockDim.z + threadIdx.z");
        cudaCode = cudaCode.Replace("get_local_id(0)", "threadIdx.x");
        cudaCode = cudaCode.Replace("get_local_id(1)", "threadIdx.y");
        cudaCode = cudaCode.Replace("get_local_id(2)", "threadIdx.z");
        cudaCode = cudaCode.Replace("get_group_id(0)", "blockIdx.x");
        cudaCode = cudaCode.Replace("get_group_id(1)", "blockIdx.y");
        cudaCode = cudaCode.Replace("get_group_id(2)", "blockIdx.z");
        cudaCode = cudaCode.Replace("barrier(CLK_LOCAL_MEM_FENCE)", "__syncthreads()");

        return cudaCode;
    }

    private async Task<byte[]> CompileToPtxAsync(string cudaSource, string kernelName, CompilationOptions? options)
    {
        // For production, we would use NVRTC (NVIDIA Runtime Compilation) library
        // For now, we'll use nvcc command-line compiler
        var sourceFile = Path.Combine(_tempDirectory, $"{kernelName}.cu");
        var ptxFile = Path.Combine(_tempDirectory, $"{kernelName}.ptx");

        try
        {
            // Write source to file
            await File.WriteAllTextAsync(sourceFile, cudaSource);

            // Build nvcc command
            var nvccArgs = new StringBuilder();
            nvccArgs.Append($"-ptx \"{sourceFile}\" -o \"{ptxFile}\"");

            // Add architecture
            var arch = options?.TargetArchitecture ?? "sm_50"; // Default to compute capability 5.0
            nvccArgs.Append($" -arch={arch}");

            // Add optimization level
            var optLevel = options?.OptimizationLevel ?? OptimizationLevel.O2;
            nvccArgs.Append($" -{optLevel.ToString().ToLower()}");

            // Add include paths
            if (options?.IncludePaths != null)
            {
                foreach (var includePath in options.IncludePaths)
                {
                    nvccArgs.Append($" -I\"{includePath}\"");
                }
            }

            // Additional flags
            if (options?.DebugInfo == true)
            {
                nvccArgs.Append(" -g -G");
            }

            if (options?.FastMath == true)
            {
                nvccArgs.Append(" -use_fast_math");
            }

            // Run nvcc
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvcc",
                    Arguments = nvccArgs.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug("Running nvcc: {Command}", $"nvcc {nvccArgs}");

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new CompilationException($"nvcc compilation failed:\n{error}\n{output}");
            }

            // Read PTX file
            return await File.ReadAllBytesAsync(ptxFile);
        }
        finally
        {
            // Clean up temporary files
            try
            {
                if (File.Exists(sourceFile)) File.Delete(sourceFile);
                if (File.Exists(ptxFile)) File.Delete(ptxFile);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    private string GenerateCacheKey(IKernelSource source, CompilationOptions? options)
    {
        var key = new StringBuilder();
        key.Append(source.Name);
        key.Append('_');
        key.Append(source.Code.GetHashCode());
        
        if (options != null)
        {
            key.Append('_');
            key.Append(options.TargetArchitecture ?? "default");
            key.Append('_');
            key.Append(options.OptimizationLevel);
            key.Append('_');
            key.Append(options.DebugInfo ? "debug" : "release");
        }

        return key.ToString();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaKernelCompiler));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            ClearCache();

            // Clean up temp directory
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch { /* Ignore cleanup errors */ }
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA kernel compiler disposal");
        }
    }
}