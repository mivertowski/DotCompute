// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Compiles [RingKernel] C# methods to CUDA PTX using a multi-stage pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This compiler implements a 6-stage compilation pipeline:
/// <list type="number">
/// <item><description><b>Stage 1 (Discovery)</b>: Find [RingKernel] methods via reflection</description></item>
/// <item><description><b>Stage 2 (Analysis)</b>: Extract method signature and metadata</description></item>
/// <item><description><b>Stage 3 (CUDA Generation)</b>: Generate CUDA C++ kernel code</description></item>
/// <item><description><b>Stage 4 (PTX Compilation)</b>: Compile CUDA → PTX with NVRTC</description></item>
/// <item><description><b>Stage 5 (Module Load)</b>: Load PTX module into CUDA context</description></item>
/// <item><description><b>Stage 6 (Verification)</b>: Verify function pointer retrieval</description></item>
/// </list>
/// </para>
/// <para>
/// The compiler integrates with components from Phase 1:
/// - <see cref="RingKernelDiscovery"/> for kernel discovery
/// - <see cref="CudaRingKernelStubGenerator"/> for CUDA code generation
/// - <see cref="PTXCompiler"/> for PTX compilation
/// - <see cref="CudaRuntime"/> for module loading and function pointer retrieval
/// </para>
/// </remarks>
public partial class CudaRingKernelCompiler
{
    // Fields for the 6-stage compilation pipeline
    private readonly RingKernelDiscovery _kernelDiscovery;
    private readonly CudaRingKernelStubGenerator _stubGenerator;
    private readonly ConcurrentDictionary<string, CudaCompiledRingKernel> _compiledKernels = new();

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, Exception?> _sLogCompilationStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "CompilationStarted"),
            "Starting Ring Kernel compilation for '{KernelId}'");

    private static readonly Action<ILogger, string, int, Exception?> _sLogStageCompleted =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(2, "StageCompleted"),
            "Compilation stage {Stage} completed for kernel '{KernelId}'");

    private static readonly Action<ILogger, string, long, int, long, Exception?> _sLogCompilationCompleted =
        LoggerMessage.Define<string, long, int, long>(
            LogLevel.Information,
            new EventId(3, "CompilationCompleted"),
            "Ring Kernel '{KernelId}' compiled successfully: " +
            "PTX={PtxSize} bytes, Module=0x{ModuleHandle:X16}, Function=0x{FunctionPtr:X16}");

    private static readonly Action<ILogger, string, string, Exception> _sLogCompilationError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(4, "CompilationError"),
            "Failed to compile Ring Kernel '{KernelId}': {ErrorMessage}");

    private static readonly Action<ILogger, string, Exception?> _sLogCacheHit =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(5, "CacheHit"),
            "Using cached compiled kernel for '{KernelId}'");

    /// <summary>
    /// Compilation stages for Ring Kernel PTX generation.
    /// </summary>
    public enum CompilationStage
    {
        /// <summary>
        /// No stage (default value).
        /// </summary>
        None = 0,

        /// <summary>
        /// Stage 1: Find [RingKernel] method via reflection.
        /// </summary>
        Discovery = 1,

        /// <summary>
        /// Stage 2: Extract method signature, parameters, and types.
        /// </summary>
        Analysis = 2,

        /// <summary>
        /// Stage 3: Generate CUDA C++ kernel code.
        /// </summary>
        CudaGeneration = 3,

        /// <summary>
        /// Stage 4: Compile CUDA → PTX with NVRTC.
        /// </summary>
        PTXCompilation = 4,

        /// <summary>
        /// Stage 5: Load PTX module into CUDA context.
        /// </summary>
        ModuleLoad = 5,

        /// <summary>
        /// Stage 6: Verify function pointer retrieval.
        /// </summary>
        Verification = 6
    }

    /// <summary>
    /// Compiles a Ring Kernel to executable PTX using the 6-stage pipeline.
    /// </summary>
    /// <param name="kernelId">The unique kernel identifier.</param>
    /// <param name="cudaContext">The CUDA context to load the module into.</param>
    /// <param name="options">Compilation options (optional).</param>
    /// <param name="assemblies">Assemblies to search for the kernel (optional, scans all if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled kernel with PTX module and function pointer.</returns>
    /// <exception cref="ArgumentException">Thrown when kernelId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails at any stage.</exception>
    [RequiresUnreferencedCode("Ring Kernel compilation uses runtime reflection which is not compatible with trimming.")]
    public async Task<CudaCompiledRingKernel> CompileRingKernelAsync(
        string kernelId,
        IntPtr cudaContext,
        CompilationOptions? options = null,
        IEnumerable<Assembly>? assemblies = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentException("CUDA context cannot be zero", nameof(cudaContext));
        }

        // Check cache first
        if (_compiledKernels.TryGetValue(kernelId, out var cachedKernel))
        {
            _sLogCacheHit(_logger, kernelId, null);
            return cachedKernel;
        }

        _sLogCompilationStarted(_logger, kernelId, null);

        try
        {
            // Stage 1: Discovery - Find [RingKernel] method
            var discoveredKernel = await DiscoverKernelAsync(kernelId, assemblies, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Discovery, null);

            // Stage 2: Analysis - Metadata extraction (already done by discovery)
            ValidateKernelMetadata(discoveredKernel);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Analysis, null);

            // Stage 3: CUDA Generation - Generate CUDA C++ kernel code
            var cudaSource = GenerateCudaSource(discoveredKernel);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.CudaGeneration, null);

            // Stage 4: PTX Compilation - Compile CUDA → PTX with NVRTC
            var ptxBytes = await CompileToPTXAsync(cudaSource, kernelId, options, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.PTXCompilation, null);

            // Stage 5: Module Load - Load PTX module into CUDA context
            var moduleHandle = await LoadPTXModuleAsync(ptxBytes, cudaContext, cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.ModuleLoad, null);

            // Stage 6: Verification - Get kernel function pointer
            var functionPtr = await GetKernelFunctionPointerAsync(
                moduleHandle,
                kernelId,
                cudaContext,
                cancellationToken);
            _sLogStageCompleted(_logger, kernelId, (int)CompilationStage.Verification, null);

            // Create compiled kernel
            var compiledKernel = new CudaCompiledRingKernel(
                discoveredKernel,
                moduleHandle,
                functionPtr,
                ptxBytes,
                cudaContext)
            {
                Name = discoveredKernel.KernelId
            };

            // Cache for future use
            _compiledKernels[kernelId] = compiledKernel;

            _sLogCompilationCompleted(
                _logger,
                kernelId,
                ptxBytes.Length,
                (int)moduleHandle,
                (int)functionPtr,
                null);

            return compiledKernel;
        }
        catch (Exception ex)
        {
            _sLogCompilationError(_logger, kernelId, ex.Message, ex);
            throw new InvalidOperationException(
                $"Failed to compile Ring Kernel '{kernelId}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Stage 1: Discovers a Ring Kernel by its kernel ID using reflection.
    /// </summary>
    [RequiresUnreferencedCode("Ring Kernel discovery uses runtime reflection which is not compatible with trimming.")]
    private Task<DiscoveredRingKernel> DiscoverKernelAsync(
        string kernelId,
        IEnumerable<Assembly>? assemblies,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var kernel = _kernelDiscovery.DiscoverKernelById(kernelId, assemblies);
            if (kernel == null)
            {
                throw new InvalidOperationException(
                    $"Ring Kernel '{kernelId}' not found. " +
                    $"Ensure the method has [RingKernel] attribute with KernelId=\"{kernelId}\".");
            }

            return kernel;
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 2: Validates discovered kernel metadata.
    /// </summary>
    private static void ValidateKernelMetadata(DiscoveredRingKernel kernel)
    {
        // Validate that all parameter types are CUDA-compatible
        foreach (var param in kernel.Parameters)
        {
            if (!CudaTypeMapper.IsTypeSupported(param.ElementType))
            {
                throw new InvalidOperationException(
                    $"Parameter '{param.Name}' has unsupported type '{param.ElementType.Name}' for CUDA compilation. " +
                    $"Supported types: primitives, Span<T>, arrays, and value types.");
            }
        }

        // Validate queue sizes
        if (kernel.InputQueueSize <= 0 || kernel.OutputQueueSize <= 0)
        {
            throw new InvalidOperationException(
                $"Ring Kernel '{kernel.KernelId}' has invalid queue sizes: " +
                $"Input={kernel.InputQueueSize}, Output={kernel.OutputQueueSize}");
        }
    }

    /// <summary>
    /// Stage 3: Generates CUDA C++ source code for the kernel.
    /// </summary>
    private string GenerateCudaSource(DiscoveredRingKernel kernel)
    {
        // Use CudaRingKernelStubGenerator from Phase 1.4
        // includeHostLauncher = false: NVRTC (Stage 4) only compiles device code, not host API calls
        return _stubGenerator.GenerateKernelStub(kernel, includeHostLauncher: false);
    }

    /// <summary>
    /// Stage 4: Compiles CUDA source to PTX using NVRTC.
    /// </summary>
    private async Task<ReadOnlyMemory<byte>> CompileToPTXAsync(
        string cudaSource,
        string kernelId,
        CompilationOptions? options,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build compilation options
            var compilationOptions = options?.Clone() ?? new CompilationOptions();

            // Ensure cooperative groups are enabled for Ring Kernels
            if (!compilationOptions.AdditionalFlags.Contains("-rdc=true"))
            {
                compilationOptions.AdditionalFlags.Add("-rdc=true");
            }
            if (!compilationOptions.AdditionalFlags.Contains("--device-c"))
            {
                compilationOptions.AdditionalFlags.Add("--device-c");
            }

            // Use PTXCompiler from existing infrastructure
            var ptxBytes = await PTXCompiler.CompileToPtxAsync(
                cudaSource,
                kernelId,
                compilationOptions,
                _logger);

            return new ReadOnlyMemory<byte>(ptxBytes);
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 5: Loads the PTX module into the CUDA context.
    /// </summary>
    private async Task<IntPtr> LoadPTXModuleAsync(
        ReadOnlyMemory<byte> ptxBytes,
        IntPtr cudaContext,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Set current CUDA context
            var setContextResult = CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to set CUDA context: {setContextResult}");
            }

            // Load PTX module - pin memory for native call
            var moduleHandle = IntPtr.Zero;
            unsafe
            {
                fixed (byte* ptxPtr = ptxBytes.Span)
                {
                    var loadResult = CudaRuntime.cuModuleLoadData(ref moduleHandle, (IntPtr)ptxPtr);

                    if (loadResult != Types.Native.CudaError.Success)
                    {
                        throw new InvalidOperationException(
                            $"Failed to load PTX module: {loadResult}");
                    }
                }
            }

            if (moduleHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "PTX module loaded but handle is null");
            }

            return moduleHandle;
        }, cancellationToken);
    }

    /// <summary>
    /// Stage 6: Retrieves the kernel function pointer from the loaded module.
    /// </summary>
    private static async Task<IntPtr> GetKernelFunctionPointerAsync(
        IntPtr moduleHandle,
        string kernelId,
        IntPtr cudaContext,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Set current CUDA context
            var setContextResult = CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to set CUDA context: {setContextResult}");
            }

            // Get function pointer for the kernel
            // The kernel function name is "{kernelId}_kernel" (from stub generator)
            var functionName = $"{SanitizeKernelName(kernelId)}_kernel";
            var functionPtr = IntPtr.Zero;

            var getResult = CudaRuntime.cuModuleGetFunction(
                ref functionPtr,
                moduleHandle,
                functionName);

            if (getResult != Types.Native.CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to get kernel function pointer for '{functionName}': {getResult}");
            }

            if (functionPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Kernel function '{functionName}' not found in module");
            }

            return functionPtr;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets a compiled kernel from the cache.
    /// </summary>
    /// <param name="kernelId">The kernel ID.</param>
    /// <returns>The compiled kernel, or null if not in cache.</returns>
    public CudaCompiledRingKernel? GetCompiledKernel(string kernelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        return _compiledKernels.TryGetValue(kernelId, out var kernel) ? kernel : null;
    }

    /// <summary>
    /// Gets all compiled kernels.
    /// </summary>
    /// <returns>A read-only collection of all compiled kernels.</returns>
    public IReadOnlyCollection<CudaCompiledRingKernel> GetAllCompiledKernels()
    {
        return _compiledKernels.Values.ToArray();
    }

    /// <summary>
    /// Clears the compiled kernel cache.
    /// </summary>
    public void ClearCache()
    {
        // Dispose all cached kernels
        foreach (var kernel in _compiledKernels.Values)
        {
            kernel.Dispose();
        }

        _compiledKernels.Clear();
    }
}
