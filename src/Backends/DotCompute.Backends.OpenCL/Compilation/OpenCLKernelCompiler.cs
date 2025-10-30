// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Backends.OpenCL.Interop;
using DotCompute.Backends.OpenCL.Types.Native;
using DotCompute.Backends.OpenCL.Vendor;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Compilation;

/// <summary>
/// Comprehensive OpenCL kernel compiler providing source and binary compilation with vendor-specific optimizations.
/// Handles the complete compilation pipeline including build log capture, binary extraction, and caching support.
/// </summary>
/// <remarks>
/// This compiler supports:
/// - Source compilation using clCreateProgramWithSource and clBuildProgram
/// - Binary compilation for pre-compiled kernels
/// - Vendor-specific compiler options via <see cref="IOpenCLVendorAdapter"/>
/// - Comprehensive error reporting with detailed build logs
/// - Binary extraction for caching compiled kernels
/// - Async/await patterns for non-blocking compilation
/// </remarks>
public sealed partial class OpenCLKernelCompiler : IAsyncDisposable
{
    private readonly OpenCLContextHandle _context;
    private readonly OpenCLDeviceId _device;
    private readonly ILogger<OpenCLKernelCompiler> _logger;
    private readonly OpenCLCompilationCache _cache;
    private readonly IOpenCLVendorAdapter? _vendorAdapter;
    private readonly string _deviceName;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelCompiler"/> class.
    /// </summary>
    /// <param name="context">OpenCL context for compilation.</param>
    /// <param name="device">Target device for compilation.</param>
    /// <param name="logger">Logger for compilation events.</param>
    /// <param name="cache">Compilation cache for storing and retrieving compiled binaries.</param>
    /// <param name="vendorAdapter">Optional vendor-specific adapter for optimizations.</param>
    public OpenCLKernelCompiler(
        OpenCLContextHandle context,
        OpenCLDeviceId device,
        ILogger<OpenCLKernelCompiler> logger,
        OpenCLCompilationCache? cache = null,
        IOpenCLVendorAdapter? vendorAdapter = null)
    {
        _context = context;
        _device = device;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? OpenCLCompilationCache.Instance;
        _vendorAdapter = vendorAdapter;

        // Get device name for cache key generation
        try
        {
            _deviceName = GetDeviceInfoString(device, DeviceInfo.Name);
        }
        catch
        {
            _deviceName = "Unknown_Device";
        }
    }

    /// <summary>
    /// Compiles OpenCL kernel source code to an executable program with caching support.
    /// </summary>
    /// <param name="sourceCode">OpenCL kernel source code.</param>
    /// <param name="kernelName">Name of the kernel function to compile.</param>
    /// <param name="options">Compilation options including vendor-specific settings.</param>
    /// <param name="cancellationToken">Token to cancel the compilation operation.</param>
    /// <returns>A compiled kernel ready for execution with complete metadata.</returns>
    /// <exception cref="CompilationException">Thrown when compilation fails.</exception>
    public async Task<CompiledKernel> CompileAsync(
        string sourceCode,
        string kernelName,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(options);

        LogCompilationStarted(_logger, kernelName, sourceCode.Length);

        var stopwatch = Stopwatch.StartNew();

        // Apply vendor-specific optimizations if adapter is provided
        var effectiveOptions = ApplyVendorOptimizations(options);

        // Try to load from cache first
        var cachedBinary = await _cache.TryGetBinaryAsync(sourceCode, effectiveOptions, _deviceName);
        if (cachedBinary != null)
        {
            try
            {
                var cachedKernel = await CompileFromBinaryAsync(cachedBinary, kernelName, cancellationToken)
                    .ConfigureAwait(false);

                // Enrich with reflection metadata
                await EnrichKernelMetadataAsync(cachedKernel, cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();
                LogCacheHit(_logger, kernelName, stopwatch.ElapsedMilliseconds);

                return cachedKernel;
            }
            catch (CompilationException ex)
            {
                // Cache might be corrupted or incompatible - invalidate and recompile
                LogCacheInvalid(_logger, ex, kernelName);
                _cache.Invalidate(sourceCode);
            }
        }

        OpenCLProgram program = default;

        try
        {
            // Create program from source
            program = await Task.Run(() => CreateProgramFromSource(sourceCode), cancellationToken)
                .ConfigureAwait(false);

            // Build program with options
            await BuildProgramAsync(program, effectiveOptions, cancellationToken)
                .ConfigureAwait(false);

            // Extract binary for caching
            var binary = await ExtractProgramBinaryAsync(program, cancellationToken)
                .ConfigureAwait(false);

            // Get build log for diagnostic purposes
            var buildLog = GetBuildLog(program);

            // Create kernel
            var kernel = CreateKernel(program, kernelName);

            // Create compiled kernel result
            var compiledKernel = new CompiledKernel(
                program, kernel, kernelName, binary, buildLog, effectiveOptions, stopwatch.Elapsed);

            // Enrich with reflection metadata (argument info, work group sizes, memory usage)
            await EnrichKernelMetadataAsync(compiledKernel, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            LogCompilationSucceeded(_logger, kernelName, stopwatch.ElapsedMilliseconds, binary?.Length ?? 0);

            // Store in cache for future use
            if (binary != null)
            {
                await _cache.StoreBinaryAsync(sourceCode, binary, effectiveOptions, _deviceName, kernelName);
            }

            return compiledKernel;
        }
        catch (Exception ex) when (ex is not CompilationException)
        {
            // Clean up program on failure
            if (program.Handle != nint.Zero)
            {
                try
                {
                    OpenCLRuntime.clReleaseProgram(program);
                }
                catch (Exception cleanupEx)
                {
                    LogCleanupFailed(_logger, cleanupEx, kernelName);
                }
            }

            stopwatch.Stop();
            LogCompilationFailed(_logger, ex, kernelName, stopwatch.ElapsedMilliseconds);

            // Parse build log for better error reporting
            var buildLog = program.Handle != nint.Zero ? GetBuildLog(program) : string.Empty;
            var parsedError = ParseBuildLogError(buildLog, sourceCode);

            throw new CompilationException(
                $"Failed to compile kernel '{kernelName}': {ex.Message}\n{parsedError.Suggestion}",
                buildLog,
                sourceCode,
                effectiveOptions);
        }
    }

    /// <summary>
    /// Compiles a kernel from pre-compiled binary code.
    /// </summary>
    /// <param name="binary">Pre-compiled binary code.</param>
    /// <param name="kernelName">Name of the kernel function.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A compiled kernel ready for execution.</returns>
    /// <exception cref="CompilationException">Thrown when loading or linking fails.</exception>
    public async Task<CompiledKernel> CompileFromBinaryAsync(
        byte[] binary,
        string kernelName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(binary);
        ArgumentNullException.ThrowIfNull(kernelName);

        LogBinaryCompilationStarted(_logger, kernelName, binary.Length);

        var stopwatch = Stopwatch.StartNew();
        OpenCLProgram program = default;

        try
        {
            // Create program from binary
            program = await Task.Run(() => CreateProgramFromBinary(binary), cancellationToken)
                .ConfigureAwait(false);

            // Build (link) the binary program
            await Task.Run(() =>
            {
                var devices = new[] { _device.Handle };
                var error = OpenCLRuntime.clBuildProgram(program, 1, devices, null, nint.Zero, nint.Zero);

                if (error != OpenCLError.Success)
                {
                    var buildLog = GetBuildLog(program);
                    throw new CompilationException(
                        $"Failed to build program from binary: {error}",
                        buildLog,
                        "<binary>",
                        new CompilationOptions());
                }
            }, cancellationToken).ConfigureAwait(false);

            // Create kernel
            var kernel = CreateKernel(program, kernelName);

            stopwatch.Stop();
            LogBinaryCompilationSucceeded(_logger, kernelName, stopwatch.ElapsedMilliseconds);

            return new CompiledKernel(
                program,
                kernel,
                kernelName,
                binary,
                string.Empty,
                new CompilationOptions(),
                stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not CompilationException)
        {
            // Clean up program on failure
            if (program.Handle != nint.Zero)
            {
                try
                {
                    OpenCLRuntime.clReleaseProgram(program);
                }
                catch (Exception cleanupEx)
                {
                    LogCleanupFailed(_logger, cleanupEx, kernelName);
                }
            }

            stopwatch.Stop();
            LogBinaryCompilationFailed(_logger, ex, kernelName, stopwatch.ElapsedMilliseconds);
            throw new CompilationException($"Failed to compile kernel '{kernelName}' from binary", ex);
        }
    }

    private OpenCLProgram CreateProgramFromSource(string sourceCode)
    {
        var sources = new[] { sourceCode };
        var program = OpenCLRuntime.clCreateProgramWithSource(
            _context,
            1,
            sources,
            null,
            out var error);

        if (error != OpenCLError.Success)
        {
            throw new CompilationException(
                $"Failed to create program from source: {error}",
                string.Empty,
                sourceCode,
                new CompilationOptions());
        }

        return program;
    }

    private OpenCLProgram CreateProgramFromBinary(byte[] binary)
    {
        unsafe
        {
            fixed (byte* binaryPtr = binary)
            {
                var binaryLength = (nuint)binary.Length;
                var devices = new[] { _device.Handle };
                var lengths = new[] { binaryLength };
                var binaries = new[] { (nint)binaryPtr };
                int binaryStatus = 0;

                var program = OpenCLRuntimeExtensions.clCreateProgramWithBinary(
                    _context,
                    1,
                    devices,
                    lengths,
                    binaries,
                    (nint)(&binaryStatus),
                    out var error);

                if (error != OpenCLError.Success)
                {
                    throw new CompilationException(
                        $"Failed to create program from binary: {error}, binary status: {binaryStatus}",
                        string.Empty,
                        "<binary>",
                        new CompilationOptions());
                }

                return program;
            }
        }
    }

    private async Task BuildProgramAsync(
        OpenCLProgram program,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var optionsString = options.GetCompilerOptionsString();
            LogBuildOptions(_logger, optionsString);

            var devices = new[] { _device.Handle };
            var error = OpenCLRuntime.clBuildProgram(
                program,
                1,
                devices,
                optionsString,
                nint.Zero,
                nint.Zero);

            if (error != OpenCLError.Success)
            {
                var buildLog = GetBuildLog(program);
                throw new CompilationException(
                    $"Program build failed: {error}",
                    buildLog,
                    "<source>",
                    options);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private string GetBuildLog(OpenCLProgram program)
    {
        // Get build log size
        var error = OpenCLRuntime.clGetProgramBuildInfo(
            program,
            _device,
            (uint)ProgramBuildInfo.BuildLog,
            0,
            nint.Zero,
            out var logSize);

        if (error != OpenCLError.Success || logSize == 0)
        {
            return string.Empty;
        }

        // Allocate buffer and get log
        var buffer = new byte[logSize];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                error = OpenCLRuntime.clGetProgramBuildInfo(
                    program,
                    _device,
                    (uint)ProgramBuildInfo.BuildLog,
                    logSize,
                    (nint)ptr,
                    out _);

                if (error != OpenCLError.Success)
                {
                    return string.Empty;
                }
            }
        }

        // Convert to string, removing null terminator
        return Encoding.UTF8.GetString(buffer, 0, (int)logSize - 1);
    }

    private async Task<byte[]?> ExtractProgramBinaryAsync(
        OpenCLProgram program,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            unsafe
            {
                // Get binary size
                nuint binarySize = 0;
                var error = OpenCLRuntimeExtensions.clGetProgramInfo(
                    program,
                    (uint)ProgramInfo.BinarySizes,
                    (nuint)sizeof(nuint),
                    (nint)(&binarySize),
                    out _);

                if (error != OpenCLError.Success || binarySize == 0)
                {
                    LogBinaryExtractionFailed(_logger, error);
                    return null;
                }

                // Allocate buffer for binary
                var binary = new byte[binarySize];

                fixed (byte* binaryPtr = binary)
                {
                    nint binaryPtrValue = (nint)binaryPtr;
                    error = OpenCLRuntimeExtensions.clGetProgramInfo(
                        program,
                        (uint)ProgramInfo.Binaries,
                        (nuint)sizeof(nint),
                        (nint)(&binaryPtrValue),
                        out _);

                    if (error != OpenCLError.Success)
                    {
                        LogBinaryExtractionFailed(_logger, error);
                        return null;
                    }
                }

                LogBinaryExtracted(_logger, binary.Length);
                return binary;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static OpenCLKernel CreateKernel(OpenCLProgram program, string kernelName)
    {
        var kernel = OpenCLRuntime.clCreateKernel(program, kernelName, out var error);

        if (error != OpenCLError.Success)
        {
            throw new CompilationException(
                $"Failed to create kernel '{kernelName}': {error}",
                string.Empty,
                kernelName,
                new CompilationOptions());
        }

        return kernel;
    }

    /// <summary>
    /// Applies vendor-specific optimizations to compilation options.
    /// </summary>
    /// <param name="options">Original compilation options.</param>
    /// <returns>Enhanced options with vendor-specific settings.</returns>
    private CompilationOptions ApplyVendorOptimizations(CompilationOptions options)
    {
        if (_vendorAdapter == null || options.VendorAdapter != null)
        {
            return options;
        }

        // Create new options with vendor adapter applied
        return options with { VendorAdapter = _vendorAdapter };
    }

    /// <summary>
    /// Enriches compiled kernel with reflection metadata.
    /// </summary>
    /// <param name="kernel">The compiled kernel to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task EnrichKernelMetadataAsync(CompiledKernel kernel, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Query kernel argument count
            var argumentCount = GetKernelArgumentCount(kernel.Kernel);
            var arguments = new KernelArgument[argumentCount];

            for (uint i = 0; i < argumentCount; i++)
            {
                arguments[i] = GetKernelArgumentInfo(kernel.Kernel, i);
            }

            kernel.Arguments = arguments;

            // Query work group size
            kernel.WorkGroupSize = GetKernelWorkGroupSize(kernel.Kernel);

            // Query local memory usage
            kernel.LocalMemorySize = GetKernelLocalMemorySize(kernel.Kernel);

            // Query private memory usage
            kernel.PrivateMemorySize = GetKernelPrivateMemorySize(kernel.Kernel);

            // Apply vendor-specific work group size recommendations
            if (_vendorAdapter != null)
            {
                // Note: This would require device info, which we can query from _device
                // For now, we log the optimal size
                LogVendorWorkGroupSize(_logger, kernel.KernelName, (int)kernel.WorkGroupSize);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the number of arguments for a kernel.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <returns>Number of arguments.</returns>
    private static uint GetKernelArgumentCount(OpenCLKernel kernel)
    {
        var error = OpenCLRuntimeExtensions.clGetKernelInfo(
            kernel,
            (uint)KernelInfo.NumArgs,
            (nuint)sizeof(uint),
            out uint numArgs,
            out _);

        return error == OpenCLError.Success ? numArgs : 0;
    }

    /// <summary>
    /// Gets information about a specific kernel argument.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <param name="index">Argument index.</param>
    /// <returns>Kernel argument information.</returns>
    private static KernelArgument GetKernelArgumentInfo(OpenCLKernel kernel, uint index)
    {
        // Get argument name
        var name = GetKernelArgInfoString(kernel.Handle, index, KernelArgInfo.Name);

        // Get argument type name
        var typeName = GetKernelArgInfoString(kernel.Handle, index, KernelArgInfo.TypeName);

        // Get access qualifier (read_only, write_only, read_write)
        var accessQualifier = GetKernelArgInfoValue<uint>(kernel.Handle, index, KernelArgInfo.AccessQualifier);

        // Get address qualifier (global, local, constant, private)
        var addressQualifier = GetKernelArgInfoValue<uint>(kernel.Handle, index, KernelArgInfo.AddressQualifier);

        return new KernelArgument
        {
            Index = index,
            Name = name,
            TypeName = typeName,
            AccessQualifier = (ArgumentAccessQualifier)accessQualifier,
            AddressQualifier = (ArgumentAddressQualifier)addressQualifier
        };
    }

    /// <summary>
    /// Gets the maximum work group size for a kernel.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <returns>Maximum work group size.</returns>
    private unsafe nuint GetKernelWorkGroupSize(OpenCLKernel kernel)
    {
        var error = OpenCLRuntimeExtensions.clGetKernelWorkGroupInfo(
            kernel,
            _device,
            (uint)KernelWorkGroupInfo.WorkGroupSize,
            (nuint)sizeof(nuint),
            out nuint workGroupSize,
            out _);

        return error == OpenCLError.Success ? workGroupSize : 0;
    }

    /// <summary>
    /// Gets the local memory usage for a kernel.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <returns>Local memory size in bytes.</returns>
    private nuint GetKernelLocalMemorySize(OpenCLKernel kernel)
    {
        var error = OpenCLRuntimeExtensions.clGetKernelWorkGroupInfo(
            kernel,
            _device,
            (uint)KernelWorkGroupInfo.LocalMemSize,
            (nuint)sizeof(ulong),
            out ulong localMemSize,
            out _);

        return error == OpenCLError.Success ? (nuint)localMemSize : 0;
    }

    /// <summary>
    /// Gets the private memory usage for a kernel.
    /// </summary>
    /// <param name="kernel">Kernel to query.</param>
    /// <returns>Private memory size in bytes.</returns>
    private nuint GetKernelPrivateMemorySize(OpenCLKernel kernel)
    {
        var error = OpenCLRuntimeExtensions.clGetKernelWorkGroupInfo(
            kernel,
            _device,
            (uint)KernelWorkGroupInfo.PrivateMemSize,
            (nuint)sizeof(ulong),
            out ulong privateMemSize,
            out _);

        return error == OpenCLError.Success ? (nuint)privateMemSize : 0;
    }

    /// <summary>
    /// Parses build log to extract error information and generate suggestions.
    /// </summary>
    /// <param name="buildLog">Build log from compilation.</param>
    /// <param name="sourceCode">Source code that failed to compile.</param>
    /// <returns>Parsed error information with suggestions.</returns>
    private static BuildErrorInfo ParseBuildLogError(string buildLog, string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(buildLog))
        {
            return new BuildErrorInfo
            {
                LineNumber = -1,
                ErrorMessage = "Unknown compilation error",
                Suggestion = "Enable debug information (-g) for more detailed error messages."
            };
        }

        // Parse common error patterns
        // Format: "kernel.cl:42:5: error: undeclared identifier 'foo'"
        var patterns = new[]
        {
            @":(\d+):(\d+):\s*error:\s*(.+)",  // Standard format with line and column
            @"line\s+(\d+):\s*error:\s*(.+)",   // Alternative format
            @"error:\s*(.+?)\s+at\s+line\s+(\d+)"  // Another variant
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(buildLog, pattern);
            if (match.Success)
            {
                var lineNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var errorMessage = match.Groups[match.Groups.Count - 1].Value.Trim();

                return new BuildErrorInfo
                {
                    LineNumber = lineNumber,
                    ErrorMessage = errorMessage,
                    Suggestion = GenerateErrorSuggestion(errorMessage, sourceCode, lineNumber)
                };
            }
        }

        // If no specific pattern matched, provide general suggestions
        var suggestion = GenerateGeneralSuggestion(buildLog);

        return new BuildErrorInfo
        {
            LineNumber = -1,
            ErrorMessage = buildLog.Split('\n')[0],
            Suggestion = suggestion
        };
    }

    /// <summary>
    /// Generates contextual error suggestions based on error message.
    /// </summary>
    /// <param name="errorMessage">The error message from the compiler.</param>
    /// <param name="sourceCode">The source code.</param>
    /// <param name="lineNumber">Line number where error occurred.</param>
    /// <returns>Helpful suggestion for fixing the error.</returns>
#pragma warning disable CA1308
    private static string GenerateErrorSuggestion(string errorMessage, string sourceCode, int lineNumber)
    {
        var errorLower = errorMessage.ToLowerInvariant();

        // Undeclared identifier
        if (errorLower.Contains("undeclared", StringComparison.OrdinalIgnoreCase) || errorLower.Contains("not declared", StringComparison.OrdinalIgnoreCase))
        {
            return "Suggestion: Ensure all variables and functions are declared before use. " +
                   "Check for typos in variable names and verify all required headers/functions are included.";
        }

        // Type mismatch
        if (errorLower.Contains("type", StringComparison.OrdinalIgnoreCase) && (errorLower.Contains("mismatch", StringComparison.OrdinalIgnoreCase) || errorLower.Contains("incompatible", StringComparison.OrdinalIgnoreCase)))
        {
            return "Suggestion: Verify argument types match the function signature. " +
                   "Use explicit casts if needed (e.g., (float) or (int)).";
        }

        // Missing semicolon
        if (errorLower.Contains("expected", StringComparison.OrdinalIgnoreCase) && errorLower.Contains(';', StringComparison.OrdinalIgnoreCase))
        {
            return $"Suggestion: Missing semicolon on line {lineNumber}. " +
                   "Check the previous line as well, as the error may be reported on the following line.";
        }

        // Address space issues
        if (errorLower.Contains("address", StringComparison.OrdinalIgnoreCase) || errorLower.Contains("__global", StringComparison.OrdinalIgnoreCase) ||
            errorLower.Contains("__local", StringComparison.OrdinalIgnoreCase) || errorLower.Contains("__constant", StringComparison.OrdinalIgnoreCase))
        {
            return "Suggestion: OpenCL requires explicit address space qualifiers (__global, __local, __constant, __private). " +
                   "Ensure all pointer parameters have the correct qualifier.";
        }

        // Atomic operations
        if (errorLower.Contains("atomic", StringComparison.OrdinalIgnoreCase))
        {
            return "Suggestion: Atomic operations require OpenCL 1.2+ and may need specific extensions. " +
                   "Check device capabilities and use appropriate atomic functions (atomic_add, atomic_xchg, etc.).";
        }

        return $"Suggestion: Review the compiler error on line {lineNumber} and check OpenCL specification for details.";
    }
#pragma warning restore CA1308

    /// <summary>
    /// Generates general suggestions when specific error pattern is not matched.
    /// </summary>
    /// <param name="buildLog">Full build log.</param>
    /// <returns>General suggestions for fixing compilation issues.</returns>
#pragma warning disable CA1308
    private static string GenerateGeneralSuggestion(string buildLog)
    {
        var logLower = buildLog.ToLowerInvariant();

        if (logLower.Contains("extension", StringComparison.OrdinalIgnoreCase) || logLower.Contains("not supported", StringComparison.OrdinalIgnoreCase))
        {
            return "Suggestion: This device may not support the required OpenCL extension. " +
                   "Check device capabilities using clGetDeviceInfo and enable extensions with #pragma OPENCL EXTENSION.";
        }

        if (logLower.Contains("out of", StringComparison.OrdinalIgnoreCase) && logLower.Contains("memory", StringComparison.OrdinalIgnoreCase))
        {
            return "Suggestion: Kernel may be using too much local or private memory. " +
                   "Reduce local array sizes or optimize memory usage.";
        }

        if (logLower.Contains("optimization", StringComparison.OrdinalIgnoreCase))
        {
            return "Suggestion: Try reducing optimization level or disabling specific optimizations " +
                   "if the kernel compiles at -O0 but fails at higher levels.";
        }

        return "Suggestion: Review the build log for details. Try compiling with -g for debug information " +
               "and ensure the kernel follows OpenCL C specification.";
    }
#pragma warning restore CA1308

    /// <summary>
    /// Gets device information as string.
    /// </summary>
    /// <param name="device">Device to query.</param>
    /// <param name="paramName">Parameter to query.</param>
    /// <returns>Device information string.</returns>
    private static string GetDeviceInfoString(OpenCLDeviceId device, DeviceInfo paramName)
    {
        // Get size
        var error = OpenCLRuntimeExtensions.clGetDeviceInfo(
            device,
            (uint)paramName,
            0,
            nint.Zero,
            out var size);

        if (error != OpenCLError.Success || size == 0)
        {
            return string.Empty;
        }

        // Get value
        var buffer = new byte[size];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                error = OpenCLRuntimeExtensions.clGetDeviceInfo(
                    device,
                    (uint)paramName,
                    size,
                    (nint)ptr,
                    out _);

                if (error != OpenCLError.Success)
                {
                    return string.Empty;
                }
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, (int)size - 1);
    }

    /// <summary>
    /// Gets kernel argument info as string.
    /// </summary>
    private static string GetKernelArgInfoString(nint kernel, uint argIndex, KernelArgInfo paramName)
    {
        unsafe
        {
            // Get size
            var error = (OpenCLError)OpenCLNative.clGetKernelArgInfo(kernel, argIndex, (uint)paramName, 0, nint.Zero, out var size);
            if (error != OpenCLError.Success || size == 0)
            {
                return string.Empty;
            }

            // Get value
            var buffer = new byte[size];
            fixed (byte* ptr = buffer)
            {
                error = (OpenCLError)OpenCLNative.clGetKernelArgInfo(kernel, argIndex, (uint)paramName, size, (nint)ptr, out _);
                if (error != OpenCLError.Success)
                {
                    return string.Empty;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, (int)size - 1);
        }
    }

    /// <summary>
    /// Gets kernel argument info as value type.
    /// </summary>
    private static T GetKernelArgInfoValue<T>(nint kernel, uint argIndex, KernelArgInfo paramName) where T : unmanaged
    {
        unsafe
        {
            T value = default;
            var error = (OpenCLError)OpenCLNative.clGetKernelArgInfo(
                kernel,
                argIndex,
                (uint)paramName,
                (nuint)sizeof(T),
                (nint)(&value),
                out _);

            return error == OpenCLError.Success ? value : default;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.CompletedTask.ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #region LoggerMessage Definitions

    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Information,
        Message = "Starting compilation of kernel '{KernelName}' ({SourceLength} characters)")]
    private static partial void LogCompilationStarted(
        ILogger logger,
        string kernelName,
        int sourceLength);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Kernel '{KernelName}' compiled successfully in {ElapsedMs}ms, binary size: {BinarySize} bytes")]
    private static partial void LogCompilationSucceeded(
        ILogger logger,
        string kernelName,
        long elapsedMs,
        int binarySize);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Error,
        Message = "Kernel '{KernelName}' compilation failed after {ElapsedMs}ms")]
    private static partial void LogCompilationFailed(
        ILogger logger,
        Exception exception,
        string kernelName,
        long elapsedMs);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Debug,
        Message = "Build options: {Options}")]
    private static partial void LogBuildOptions(
        ILogger logger,
        string? options);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Warning,
        Message = "Failed to extract program binary: {Error}")]
    private static partial void LogBinaryExtractionFailed(
        ILogger logger,
        OpenCLError error);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Debug,
        Message = "Extracted program binary: {Size} bytes")]
    private static partial void LogBinaryExtracted(
        ILogger logger,
        int size);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Warning,
        Message = "Failed to cleanup program for kernel '{KernelName}'")]
    private static partial void LogCleanupFailed(
        ILogger logger,
        Exception exception,
        string kernelName);

    [LoggerMessage(
        EventId = 7007,
        Level = LogLevel.Information,
        Message = "Starting binary compilation for kernel '{KernelName}' ({BinarySize} bytes)")]
    private static partial void LogBinaryCompilationStarted(
        ILogger logger,
        string kernelName,
        int binarySize);

    [LoggerMessage(
        EventId = 7008,
        Level = LogLevel.Information,
        Message = "Kernel '{KernelName}' compiled from binary successfully in {ElapsedMs}ms")]
    private static partial void LogBinaryCompilationSucceeded(
        ILogger logger,
        string kernelName,
        long elapsedMs);

    [LoggerMessage(
        EventId = 7009,
        Level = LogLevel.Error,
        Message = "Kernel '{KernelName}' binary compilation failed after {ElapsedMs}ms")]
    private static partial void LogBinaryCompilationFailed(
        ILogger logger,
        Exception exception,
        string kernelName,
        long elapsedMs);

    [LoggerMessage(
        EventId = 7010,
        Level = LogLevel.Debug,
        Message = "Cache hit for kernel '{KernelName}' in {ElapsedMs}ms")]
    private static partial void LogCacheHit(
        ILogger logger,
        string kernelName,
        long elapsedMs);

    [LoggerMessage(
        EventId = 7011,
        Level = LogLevel.Warning,
        Message = "Cached binary for kernel '{KernelName}' is invalid, recompiling")]
    private static partial void LogCacheInvalid(
        ILogger logger,
        Exception exception,
        string kernelName);

    [LoggerMessage(
        EventId = 7012,
        Level = LogLevel.Debug,
        Message = "Vendor-specific work group size for kernel '{KernelName}': {WorkGroupSize}")]
    private static partial void LogVendorWorkGroupSize(
        ILogger logger,
        string kernelName,
        int workGroupSize);

    #endregion
}

/// <summary>
/// Represents a successfully compiled OpenCL kernel with its associated program and metadata.
/// </summary>
public sealed class CompiledKernel : IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the OpenCL program handle.
    /// </summary>
    public OpenCLProgram Program { get; }

    /// <summary>
    /// Gets the OpenCL kernel handle.
    /// </summary>
    public OpenCLKernel Kernel { get; }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string KernelName { get; }

    /// <summary>
    /// Gets the compiled binary (may be null if extraction failed).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Binary data is immutable and not modified")]
    public byte[]? Binary { get; }

    /// <summary>
    /// Gets the build log from compilation.
    /// </summary>
    public string BuildLog { get; }

    /// <summary>
    /// Gets the compilation options used.
    /// </summary>
    public CompilationOptions Options { get; }

    /// <summary>
    /// Gets the compilation time.
    /// </summary>
    public TimeSpan CompilationTime { get; }

    /// <summary>
    /// Gets or sets the kernel arguments metadata.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Metadata is populated after construction")]
    public KernelArgument[] Arguments { get; internal set; } = Array.Empty<KernelArgument>();

    /// <summary>
    /// Gets or sets the maximum work group size for this kernel on the target device.
    /// </summary>
    public nuint WorkGroupSize { get; internal set; }

    /// <summary>
    /// Gets or sets the local memory size used by this kernel.
    /// </summary>
    public nuint LocalMemorySize { get; internal set; }

    /// <summary>
    /// Gets or sets the private memory size used by this kernel.
    /// </summary>
    public nuint PrivateMemorySize { get; internal set; }

    internal CompiledKernel(
        OpenCLProgram program,
        OpenCLKernel kernel,
        string kernelName,
        byte[]? binary,
        string buildLog,
        CompilationOptions options,
        TimeSpan compilationTime)
    {
        Program = program;
        Kernel = kernel;
        KernelName = kernelName;
        Binary = binary;
        BuildLog = buildLog;
        Options = options;
        CompilationTime = compilationTime;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            if (Kernel.Handle != nint.Zero)
            {
                _ = OpenCLRuntime.clReleaseKernel(Kernel);
            }

            if (Program.Handle != nint.Zero)
            {
                _ = OpenCLRuntime.clReleaseProgram(Program);
            }
        }).ConfigureAwait(false);

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Compilation options for OpenCL kernel compilation.
/// </summary>
public sealed record CompilationOptions
{
    /// <summary>
    /// Gets or initializes the optimization level (0-3).
    /// </summary>
    public int OptimizationLevel { get; init; } = 2;

    /// <summary>
    /// Gets or initializes a value indicating whether to enable fast math optimizations.
    /// </summary>
    public bool EnableFastMath { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether to generate debug information.
    /// </summary>
    public bool EnableDebugInfo { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether to enable MAD (multiply-add) optimizations.
    /// </summary>
    public bool EnableMadEnable { get; init; } = true;

    /// <summary>
    /// Gets or initializes additional compiler flags.
    /// </summary>
    public IReadOnlyList<string> AdditionalFlags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes additional compiler options (alternative to AdditionalFlags).
    /// </summary>
    public string? AdditionalOptions { get; init; }

    /// <summary>
    /// Gets or initializes the vendor adapter for vendor-specific optimizations.
    /// </summary>
    public IOpenCLVendorAdapter? VendorAdapter { get; init; }

    /// <summary>
    /// Builds the compiler options string for clBuildProgram.
    /// </summary>
    /// <returns>The compiler options string.</returns>
    public string? GetCompilerOptionsString()
    {
        var options = new List<string>();

        // Optimization level
        if (OptimizationLevel > 0)
        {
            options.Add("-cl-opt-enable");
        }
        else
        {
            options.Add("-cl-opt-disable");
        }

        if (EnableDebugInfo)
        {
            options.Add("-g");
        }

        if (EnableMadEnable)
        {
            options.Add("-cl-mad-enable");
        }

        if (EnableFastMath)
        {
            options.Add("-cl-fast-relaxed-math");
        }

        // Add vendor-specific options
        if (VendorAdapter != null)
        {
            var vendorOptions = VendorAdapter.GetCompilerOptions(OptimizationLevel > 0);
            if (!string.IsNullOrWhiteSpace(vendorOptions))
            {
                options.Add(vendorOptions);
            }
        }

        // Add additional flags
        foreach (var flag in AdditionalFlags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                options.Add(flag);
            }
        }

        // Add additional custom options
        if (!string.IsNullOrWhiteSpace(AdditionalOptions))
        {
            options.Add(AdditionalOptions);
        }

        return options.Count > 0 ? string.Join(" ", options) : null;
    }
}

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Specialized exception with required compilation context")]
public sealed class CompilationException : Exception
{
    /// <summary>
    /// Gets the build log from the compiler.
    /// </summary>
    public string BuildLog { get; }

    /// <summary>
    /// Gets the source code that failed to compile.
    /// </summary>
    public string SourceCode { get; }

    /// <summary>
    /// Gets the compilation options used.
    /// </summary>
    public CompilationOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="buildLog">The build log from the compiler.</param>
    /// <param name="sourceCode">The source code that failed to compile.</param>
    /// <param name="options">The compilation options used.</param>
    public CompilationException(
        string message,
        string buildLog,
        string sourceCode,
        CompilationOptions options)
        : base(FormatMessage(message, buildLog))
    {
        BuildLog = buildLog;
        SourceCode = sourceCode;
        Options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CompilationException(string message, Exception innerException)
        : base(message, innerException)
    {
        BuildLog = string.Empty;
        SourceCode = string.Empty;
        Options = new CompilationOptions();
    }

    private static string FormatMessage(string message, string buildLog)
    {
        if (string.IsNullOrWhiteSpace(buildLog))
        {
            return message;
        }

        return $"{message}\n\nBuild Log:\n{buildLog}";
    }
}

/// <summary>
/// OpenCL program information parameters.
/// </summary>
internal enum ProgramInfo : uint
{
    ReferenceCount = 0x1160,
    Context = 0x1161,
    NumDevices = 0x1162,
    Devices = 0x1163,
    Source = 0x1164,
    BinarySizes = 0x1165,
    Binaries = 0x1166,
    NumKernels = 0x1167,
    KernelNames = 0x1168
}

/// <summary>
/// OpenCL program build information parameters.
/// </summary>
internal enum ProgramBuildInfo : uint
{
    BuildStatus = 0x1181,
    BuildOptions = 0x1182,
    BuildLog = 0x1183,
    BinaryType = 0x1184
}

/// <summary>
/// OpenCL kernel information parameters.
/// </summary>
internal enum KernelInfo : uint
{
    FunctionName = 0x1190,
    NumArgs = 0x1191,
    ReferenceCount = 0x1192,
    Context = 0x1193,
    Program = 0x1194
}

/// <summary>
/// OpenCL kernel argument information parameters.
/// </summary>
internal enum KernelArgInfo : uint
{
    AddressQualifier = 0x1196,
    AccessQualifier = 0x1197,
    TypeName = 0x1198,
    TypeQualifier = 0x1199,
    Name = 0x119A
}

/// <summary>
/// OpenCL kernel work group information parameters.
/// </summary>
internal enum KernelWorkGroupInfo : uint
{
    WorkGroupSize = 0x11B0,
    CompileWorkGroupSize = 0x11B1,
    LocalMemSize = 0x11B2,
    PreferredWorkGroupSizeMultiple = 0x11B3,
    PrivateMemSize = 0x11B4
}

/// <summary>
/// OpenCL device information parameters.
/// </summary>
internal enum DeviceInfo : uint
{
    Type = 0x1000,
    VendorId = 0x1001,
    MaxComputeUnits = 0x1002,
    MaxWorkItemDimensions = 0x1003,
    MaxWorkGroupSize = 0x1004,
    MaxWorkItemSizes = 0x1005,
    Name = 0x102B,
    Vendor = 0x102C,
    DriverVersion = 0x102D,
    Profile = 0x102E,
    Version = 0x102F,
    Extensions = 0x1030
}

/// <summary>
/// Kernel argument access qualifiers.
/// </summary>
public enum ArgumentAccessQualifier : int
{
    /// <summary>No access qualifier specified.</summary>
    None = 0,
    /// <summary>Read-only access (__read_only or read_only).</summary>
    ReadOnly = 1,
    /// <summary>Write-only access (__write_only or write_only).</summary>
    WriteOnly = 2,
    /// <summary>Read-write access (__read_write or read_write).</summary>
    ReadWrite = 3
}

/// <summary>
/// Kernel argument address space qualifiers.
/// </summary>
public enum ArgumentAddressQualifier : int
{
    /// <summary>Global address space (__global).</summary>
    Global = 0,
    /// <summary>Local address space (__local).</summary>
    Local = 1,
    /// <summary>Constant address space (__constant).</summary>
    Constant = 2,
    /// <summary>Private address space (__private).</summary>
    Private = 3
}

/// <summary>
/// Information about a kernel argument.
/// </summary>
public sealed record KernelArgument
{
    /// <summary>
    /// Gets or initializes the argument index.
    /// </summary>
    public required uint Index { get; init; }

    /// <summary>
    /// Gets or initializes the argument name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the argument type name.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets or initializes the access qualifier.
    /// </summary>
    public required ArgumentAccessQualifier AccessQualifier { get; init; }

    /// <summary>
    /// Gets or initializes the address space qualifier.
    /// </summary>
    public required ArgumentAddressQualifier AddressQualifier { get; init; }
}

/// <summary>
/// Build error information with suggestions.
/// </summary>
internal sealed record BuildErrorInfo
{
    /// <summary>
    /// Gets or initializes the line number where error occurred (-1 if unknown).
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Gets or initializes the error message.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Gets or initializes the suggestion for fixing the error.
    /// </summary>
    public required string Suggestion { get; init; }
}

/// <summary>
/// Helper class for OpenCL runtime queries.
/// </summary>
internal static class OpenCLRuntimeHelpers
{
    /// <summary>
    /// Gets device information as string.
    /// </summary>
    public static string GetDeviceInfoString(nint device, DeviceInfo paramName)
    {
        // Get size
        var error = OpenCLRuntimeExtensions.clGetDeviceInfo(
            new OpenCLDeviceId(device),
            (uint)paramName,
            0,
            nint.Zero,
            out var size);

        if (error != OpenCLError.Success || size == 0)
        {
            return string.Empty;
        }

        // Get value
        var buffer = new byte[size];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                error = OpenCLRuntimeExtensions.clGetDeviceInfo(
                    new OpenCLDeviceId(device),
                    (uint)paramName,
                    size,
                    (nint)ptr,
                    out _);

                if (error != OpenCLError.Success)
                {
                    return string.Empty;
                }
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, (int)size - 1);
    }
}

/// <summary>
/// Extension methods for OpenCL runtime functions not in the main runtime class.
/// </summary>
internal static class OpenCLRuntimeExtensions
{
    private const string LibraryName = "OpenCL";

    /// <summary>
    /// Create program from binary.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint clCreateProgramWithBinary(
        nint context,
        uint numDevices,
        [In] nint[] deviceList,
        [In] nuint[] lengths,
        [In] nint[] binaries,
        nint binaryStatus,
        out OpenCLError errcodeRet);

    /// <summary>
    /// Get program information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetProgramInfo(
        nint program,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Get program information (generic version for value types).
    /// </summary>
    internal static unsafe OpenCLError clGetProgramInfo<T>(
        nint program,
        uint paramName,
        nuint paramValueSize,
        out T paramValue,
        out nuint paramValueSizeRet) where T : unmanaged
    {
        fixed (T* ptr = &paramValue)
        {
            return clGetProgramInfo(
                program,
                paramName,
                paramValueSize,
                (nint)ptr,
                out paramValueSizeRet);
        }
    }

    /// <summary>
    /// Get kernel information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetKernelInfo(
        OpenCLKernel kernel,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Get kernel information (generic version for value types).
    /// </summary>
    internal static unsafe OpenCLError clGetKernelInfo<T>(
        OpenCLKernel kernel,
        uint paramName,
        nuint paramValueSize,
        out T paramValue,
        out nuint paramValueSizeRet) where T : unmanaged
    {
        fixed (T* ptr = &paramValue)
        {
            return clGetKernelInfo(
                kernel,
                paramName,
                paramValueSize,
                (nint)ptr,
                out paramValueSizeRet);
        }
    }

    /// <summary>
    /// Get kernel argument information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetKernelArgInfo(
        nint kernel,
        uint argIndex,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Get kernel work group information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetKernelWorkGroupInfo(
        OpenCLKernel kernel,
        OpenCLDeviceId device,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);

    /// <summary>
    /// Get kernel work group information (generic version for value types).
    /// </summary>
    internal static unsafe OpenCLError clGetKernelWorkGroupInfo<T>(
        OpenCLKernel kernel,
        OpenCLDeviceId device,
        uint paramName,
        nuint paramValueSize,
        out T paramValue,
        out nuint paramValueSizeRet) where T : unmanaged
    {
        fixed (T* ptr = &paramValue)
        {
            return clGetKernelWorkGroupInfo(
                kernel,
                device,
                paramName,
                paramValueSize,
                (nint)ptr,
                out paramValueSizeRet);
        }
    }

    /// <summary>
    /// Get device information.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern OpenCLError clGetDeviceInfo(
        OpenCLDeviceId device,
        uint paramName,
        nuint paramValueSize,
        nint paramValue,
        out nuint paramValueSizeRet);
}
