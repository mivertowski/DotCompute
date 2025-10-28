// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelCompiler"/> class.
    /// </summary>
    /// <param name="context">OpenCL context for compilation.</param>
    /// <param name="device">Target device for compilation.</param>
    /// <param name="logger">Logger for compilation events.</param>
    public OpenCLKernelCompiler(
        OpenCLContextHandle context,
        OpenCLDeviceId device,
        ILogger<OpenCLKernelCompiler> logger)
    {
        _context = context;
        _device = device;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compiles OpenCL kernel source code to an executable program.
    /// </summary>
    /// <param name="sourceCode">OpenCL kernel source code.</param>
    /// <param name="kernelName">Name of the kernel function to compile.</param>
    /// <param name="options">Compilation options including vendor-specific settings.</param>
    /// <param name="cancellationToken">Token to cancel the compilation operation.</param>
    /// <returns>A compiled kernel ready for execution.</returns>
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
        OpenCLProgram program = default;

        try
        {
            // Create program from source
            program = await Task.Run(() => CreateProgramFromSource(sourceCode), cancellationToken)
                .ConfigureAwait(false);

            // Build program with options
            await BuildProgramAsync(program, options, cancellationToken)
                .ConfigureAwait(false);

            // Extract binary for caching
            var binary = await ExtractProgramBinaryAsync(program, cancellationToken)
                .ConfigureAwait(false);

            // Get build log for diagnostic purposes
            var buildLog = GetBuildLog(program);

            // Create kernel
            var kernel = CreateKernel(program, kernelName);

            stopwatch.Stop();
            LogCompilationSucceeded(_logger, kernelName, stopwatch.ElapsedMilliseconds, binary?.Length ?? 0);

            return new CompiledKernel(program, kernel, kernelName, binary, buildLog, options, stopwatch.Elapsed);
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
            throw;
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
}
