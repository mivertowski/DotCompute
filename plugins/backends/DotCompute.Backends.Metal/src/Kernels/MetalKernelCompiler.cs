// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// Compiles kernels to Metal Shading Language and creates compute pipeline states.
/// </summary>
public sealed class MetalKernelCompiler : IKernelCompiler, IDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly ILogger _logger;
    private readonly Dictionary<string, IntPtr> _libraryCache;
    private readonly SemaphoreSlim _compilationSemaphore;
    private int _disposed;

    public MetalKernelCompiler(IntPtr device, IntPtr commandQueue, ILogger logger)
    {
        _device = device;
        _commandQueue = commandQueue;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _libraryCache = new Dictionary<string, IntPtr>();
        _compilationSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <inheritdoc/>
    public string Name => "Metal Shader Compiler";

    /// <inheritdoc/>
    public KernelSourceType[] SupportedSourceTypes => new[] 
    { 
        KernelSourceType.Metal,
        KernelSourceType.Binary
    };

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        if (_disposed > 0)
        {
            throw new ObjectDisposedException(nameof(MetalKernelCompiler));
        }

        var validation = Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Kernel validation failed: {validation.ErrorMessage}");
        }

        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Generate or extract Metal code
            var metalCode = ExtractMetalCode(definition);
            var codeHash = ComputeHash(metalCode);

            // Check cache
            if (_libraryCache.TryGetValue(codeHash, out var cachedLibrary))
            {
                _logger.LogDebug("Using cached Metal library for kernel: {Name}", definition.Name);
                return CreateCompiledKernel(definition, cachedLibrary, stopwatch.Elapsed);
            }

            // Compile Metal code
            _logger.LogDebug("Compiling Metal kernel: {Name}", definition.Name);
            var library = await CompileMetalCodeAsync(metalCode, definition.Name, options, cancellationToken).ConfigureAwait(false);
            
            // Cache the compiled library
            _libraryCache[codeHash] = library;

            return CreateCompiledKernel(definition, library, stopwatch.Elapsed);
        }
        finally
        {
            _compilationSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public ValidationResult Validate(KernelDefinition definition)
    {
        if (definition == null)
        {
            return ValidationResult.Failure("Kernel definition cannot be null");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return ValidationResult.Failure("Kernel name cannot be empty");
        }

        if (definition.Code == null || definition.Code.Length == 0)
        {
            return ValidationResult.Failure("Kernel code cannot be empty");
        }

        // Check if this looks like Metal code or binary
        var codeString = Encoding.UTF8.GetString(definition.Code);
        if (!codeString.Contains("kernel") && !codeString.Contains("metal") && !IsBinaryCode(definition.Code))
        {
            return ValidationResult.Failure("Code does not appear to be valid Metal shader language or compiled binary");
        }

        return ValidationResult.Success();
    }

    private string ExtractMetalCode(KernelDefinition definition)
    {
        // If it's binary code, we'll need to handle it differently
        if (IsBinaryCode(definition.Code))
        {
            throw new NotSupportedException("Pre-compiled Metal binaries are not yet supported");
        }

        // Assume it's text-based Metal code
        var code = Encoding.UTF8.GetString(definition.Code);
        
        // If the code doesn't include Metal headers, add them
        if (!code.Contains("#include <metal_stdlib>"))
        {
            var sb = new StringBuilder();
            sb.AppendLine("#include <metal_stdlib>");
            sb.AppendLine("#include <metal_compute>");
            sb.AppendLine("using namespace metal;");
            sb.AppendLine();
            sb.Append(code);
            return sb.ToString();
        }

        return code;
    }

    private bool IsBinaryCode(byte[] code)
    {
        // Check for common binary signatures
        if (code.Length < 4)
        {
            return false;
        }
        
        // Metal library magic number
        return code[0] == 0x4D && code[1] == 0x54 && code[2] == 0x4C && code[3] == 0x42;
    }

    private async Task<IntPtr> CompileMetalCodeAsync(string code, string kernelName, CompilationOptions options, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            // Create compile options
            var compileOptions = MetalNative.CreateCompileOptions();
            
            // Set optimization level
            var enableFastMath = options.OptimizationLevel >= OptimizationLevel.Default;
            MetalNative.SetCompileOptionsFastMath(compileOptions, enableFastMath);
            
            // Set language version
            MetalNative.SetCompileOptionsLanguageVersion(compileOptions, MetalLanguageVersion.Metal30);

            try
            {
                // Compile the code
                var error = IntPtr.Zero;
                var library = MetalNative.CompileLibrary(_device, code, compileOptions, ref error);

                if (library == IntPtr.Zero)
                {
                    var errorMessage = error != IntPtr.Zero ? 
                        Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error" :
                        "Failed to compile Metal library";
                    
                    if (error != IntPtr.Zero)
                    {
                        MetalNative.ReleaseError(error);
                    }

                    throw new InvalidOperationException($"Metal compilation failed: {errorMessage}");
                }

                _logger.LogDebug("Successfully compiled Metal kernel: {Name}", kernelName);
                return library;
            }
            finally
            {
                MetalNative.ReleaseCompileOptions(compileOptions);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private MetalCompiledKernel CreateCompiledKernel(KernelDefinition definition, IntPtr library, TimeSpan compilationTime)
    {
        var entryPoint = definition.EntryPoint ?? definition.Name;
        
        // Get the kernel function from the library
        var function = MetalNative.GetFunction(library, entryPoint);
        if (function == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Kernel function '{entryPoint}' not found in compiled library");
        }

        try
        {
            // Create compute pipeline state
            var error = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(_device, function, ref error);

            if (pipelineState == IntPtr.Zero)
            {
                var errorMessage = error != IntPtr.Zero ?
                    Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error" :
                    "Failed to create compute pipeline state";

                if (error != IntPtr.Zero)
                {
                    MetalNative.ReleaseError(error);
                }

                throw new InvalidOperationException($"Failed to create compute pipeline state: {errorMessage}");
            }

            // Get kernel characteristics
            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidth(pipelineState);

            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = compilationTime.TotalMilliseconds,
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { $"Max threads per threadgroup: {maxThreadsPerThreadgroup}" }
            };

            return new MetalCompiledKernel(
                definition,
                pipelineState,
                function,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger);
        }
        finally
        {
            // Function is retained by the pipeline state, so we can release our reference
            MetalNative.ReleaseFunction(function);
        }
    }

    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Release cached libraries
        foreach (var library in _libraryCache.Values)
        {
            MetalNative.ReleaseLibrary(library);
        }
        _libraryCache.Clear();

        _compilationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}