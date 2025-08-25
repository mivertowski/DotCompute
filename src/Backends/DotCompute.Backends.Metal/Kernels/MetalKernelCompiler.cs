// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using AbstractionsValidationResult = DotCompute.Abstractions.UnifiedValidationResult;

using DotCompute.Abstractions.Kernels;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements

namespace DotCompute.Backends.Metal.Kernels;


/// <summary>
/// Compiles kernels to Metal Shading Language and creates compute pipeline states.
/// </summary>
public sealed class MetalKernelCompiler(IntPtr device, IntPtr commandQueue, ILogger logger, MetalCommandBufferPool? commandBufferPool = null) : IUnifiedKernelCompiler, IDisposable
{
    private readonly IntPtr _device = device;
    private readonly IntPtr _commandQueue = commandQueue;
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MetalCommandBufferPool? _commandBufferPool = commandBufferPool;
    private readonly Dictionary<string, IntPtr> _libraryCache = [];
    private readonly SemaphoreSlim _compilationSemaphore = new(1, 1);
    private int _disposed;

    /// <inheritdoc/>
    public string Name => "Metal Shader Compiler";

    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays - Required by IUnifiedKernelCompiler interface
    public KernelSourceType[] SupportedSourceTypes { get; } =
    [
        KernelSourceType.Metal,
    KernelSourceType.Binary
    ];
#pragma warning restore CA1819

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        ObjectDisposedException.ThrowIf(_disposed > 0, this);

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
            _ = _compilationSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public AbstractionsValidationResult Validate(KernelDefinition definition)
    {
        if (definition == null)
        {
            return AbstractionsValidationResult.Failure("Kernel definition cannot be null");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return AbstractionsValidationResult.Failure("Kernel name cannot be empty");
        }

        if (definition.Code == null || definition.Code.Length == 0)
        {
            return AbstractionsValidationResult.Failure("Kernel code cannot be empty");
        }

        // Check if this looks like Metal code or binary
        var codeString = definition.Code ?? string.Empty;
        if (!codeString.Contains("kernel", StringComparison.Ordinal) && !codeString.Contains("metal", StringComparison.Ordinal) && !IsBinaryCode(Encoding.UTF8.GetBytes(codeString)))
        {
            return AbstractionsValidationResult.Failure("Code does not appear to be valid Metal shader language or compiled binary");
        }

        return AbstractionsValidationResult.Success();
    }

    private static string ExtractMetalCode(KernelDefinition definition)
    {
        // If it's binary code, we'll need to handle it differently
        if (definition.Code != null && IsBinaryCode(System.Text.Encoding.UTF8.GetBytes(definition.Code)))
        {
            throw new NotSupportedException("Pre-compiled Metal binaries are not yet supported");
        }

        // Assume it's text-based Metal code
        var code = definition.Code ?? string.Empty;

        // If the code doesn't include Metal headers, add them
        if (!code.Contains("#include <metal_stdlib>", StringComparison.Ordinal))
        {
            var sb = new StringBuilder();
            _ = sb.AppendLine("#include <metal_stdlib>");
            _ = sb.AppendLine("#include <metal_compute>");
            _ = sb.AppendLine("using namespace metal;");
            _ = sb.AppendLine();
            _ = sb.Append(code);
            return sb.ToString();
        }

        return code;
    }

    private static bool IsBinaryCode(byte[] code)
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

            // Configure optimization settings
            ConfigureOptimizationOptions(compileOptions, options, kernelName);

            // Set language version based on system capabilities
            var languageVersion = GetOptimalLanguageVersion();
            MetalNative.SetCompileOptionsLanguageVersion(compileOptions, languageVersion);

            try
            {
                // Compile the code
                var error = IntPtr.Zero;
                var library = MetalNative.CompileLibrary(_device, code, compileOptions, ref error);

                if (library == IntPtr.Zero)
                {
                    var errorMessage = error != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error"
                        : "Failed to compile Metal library";

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
                var errorMessage = error != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error"
                    : "Failed to create compute pipeline state";

                if (error != IntPtr.Zero)
                {
                    MetalNative.ReleaseError(error);
                }

                throw new InvalidOperationException($"Failed to create compute pipeline state: {errorMessage}");
            }

            // Get kernel characteristics
            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(pipelineState);

            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = compilationTime.TotalMilliseconds,
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { $"Max threads per threadgroup: {maxThreadsPerThreadgroup}" }
            };

            return new MetalCompiledKernel(
                definition,
                pipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool);
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

    private void ConfigureOptimizationOptions(IntPtr compileOptions, CompilationOptions options, string kernelName)
    {
        // Configure fast math based on optimization level
        var enableFastMath = options.OptimizationLevel >= OptimizationLevel.Default;
        if (options.FastMath || enableFastMath)
        {
            MetalNative.SetCompileOptionsFastMath(compileOptions, true);
            _logger.LogTrace("Enabled fast math optimizations for kernel: {KernelName}", kernelName);
        }

        // Additional optimization hints could be set here based on the options
        if (options.OptimizationLevel == OptimizationLevel.Maximum)
        {
            _logger.LogTrace("Maximum optimization level requested for kernel: {KernelName}", kernelName);
            // Metal doesn't expose many additional optimization flags through the public API
            // but we can log this for debugging purposes
        }
    }

    private static MetalLanguageVersion GetOptimalLanguageVersion()
    {
        // Determine the best Metal language version based on system capabilities
        try
        {
            var osVersion = Environment.OSVersion.Version;

            // macOS 13.0+ supports Metal 3.1
            if (osVersion.Major >= 13 || (osVersion.Major == 13 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal31;
            }

            // macOS 12.0+ supports Metal 3.0
            if (osVersion.Major >= 12 || (osVersion.Major == 12 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal30;
            }

            // macOS 11.0+ supports Metal 2.4
            if (osVersion.Major >= 11 || (osVersion.Major == 11 && osVersion.Minor >= 0))
            {
                return MetalLanguageVersion.Metal24;
            }

            // macOS 10.15+ supports Metal 2.3
            if (osVersion.Major >= 10 && osVersion.Minor >= 15)
            {
                return MetalLanguageVersion.Metal23;
            }

            // macOS 10.14+ supports Metal 2.1
            if (osVersion.Major >= 10 && osVersion.Minor >= 14)
            {
                return MetalLanguageVersion.Metal21;
            }

            // macOS 10.13+ supports Metal 2.0
            if (osVersion.Major >= 10 && osVersion.Minor >= 13)
            {
                return MetalLanguageVersion.Metal20;
            }

            // Fallback to Metal 1.2 for older systems
            return MetalLanguageVersion.Metal12;
        }
        catch
        {
            // If we can't determine OS version, use a safe default
            return MetalLanguageVersion.Metal20;
        }
    }
}
