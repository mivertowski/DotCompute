// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Abstractions;
// Fully qualified type names used to avoid ambiguity with Metal-specific UnifiedValidationResult

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Translation;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using ValidationResult = DotCompute.Abstractions.Validation.UnifiedValidationResult;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements
#pragma warning disable SYSLIB1045 // GeneratedRegexAttribute - using compiled regex for runtime flexibility

namespace DotCompute.Backends.Metal.Kernels;


/// <summary>
/// Compiles kernels to Metal Shading Language and creates compute pipeline states.
/// </summary>
public sealed partial class MetalKernelCompiler : IUnifiedKernelCompiler, IDisposable
{
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly ILogger _logger;
    private readonly MetalCommandBufferPool? _commandBufferPool;
    private readonly MetalKernelCache _kernelCache;
    private readonly CSharpToMSLTranslator _translator;
    private readonly MetalThreadgroupOptimizer? _threadgroupOptimizer;
    private readonly SemaphoreSlim _compilationSemaphore = new(1, 1);
    private readonly string _gpuFamily;
    private int _disposed;

    [GeneratedRegex(@"kernel\s+void\s+(\w+)\s*[<(]", RegexOptions.Multiline)]
    private static partial Regex KernelFunctionNamePattern();


    public MetalKernelCompiler(
        IntPtr device,

        IntPtr commandQueue,

        ILogger logger,

        MetalCommandBufferPool? commandBufferPool = null,
        MetalKernelCache? kernelCache = null)
    {
        _device = device;
        _commandQueue = commandQueue;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandBufferPool = commandBufferPool;

        // Create or use provided kernel cache

        if (kernelCache != null)
        {
            _kernelCache = kernelCache;
        }
        else
        {
            var cacheLogger = logger is ILogger<MetalKernelCache> cacheTypedLogger
                ? cacheTypedLogger

                : new LoggerFactory().CreateLogger<MetalKernelCache>();


            _kernelCache = new MetalKernelCache(
                cacheLogger,
                maxCacheSize: 500,
                defaultTtl: TimeSpan.FromHours(2),
                persistentCachePath: Path.Combine(Path.GetTempPath(), "DotCompute", "MetalCache"));
        }

        // Initialize the C# to MSL translator
        _translator = new CSharpToMSLTranslator(logger);

        // Initialize threadgroup optimizer
        _threadgroupOptimizer = new MetalThreadgroupOptimizer(logger);

        // Detect GPU family for optimization macros
        _gpuFamily = DetectGpuFamily();
    }

    /// <inheritdoc/>
    public string Name => "Metal Shader Compiler";





    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays - Required by IUnifiedKernelCompiler interface
    public IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; } = new List<KernelLanguage>
    {
        KernelLanguage.Metal,
        KernelLanguage.Binary
    }.AsReadOnly();
#pragma warning restore CA1819

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Capabilities { get; } = new Dictionary<string, object>
    {
        ["SupportsAsync"] = true,
        ["SupportsOptimization"] = true,
        ["SupportsCaching"] = true,
        ["SupportsValidation"] = true,
        ["SupportedLanguageVersions"] = new[] { "Metal 2.0", "Metal 2.1", "Metal 2.2", "Metal 2.3", "Metal 2.4" }
    }.AsReadOnly();

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
            throw new InvalidOperationException($"Kernel validation failed: {validation.ErrorMessage ?? "Unknown validation error"}");
        }

        // Check the cache first

        if (_kernelCache.TryGetKernel(definition, options, out _, out _, out var cachedPipelineState))
        {
            _logger.LogDebug("Cache hit for kernel '{Name}' - using cached pipeline state", definition.Name);

            // Get metadata from cached pipeline state

            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(cachedPipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(cachedPipelineState);


            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = 0, // No compilation time for cached kernel
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { "Kernel loaded from cache" }
            };


            return (ICompiledKernel)new MetalCompiledKernel(
                definition,
                cachedPipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool,
                _threadgroupOptimizer);
        }

        await _compilationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Generate or extract Metal code
            var metalCode = ExtractMetalCode(definition);

            // Inject GPU family-specific optimization macros
            metalCode = InjectGpuFamilyMacros(metalCode, _gpuFamily);

            _logger.LogDebug("Injected GPU family macros for '{Family}' into kernel '{Name}'", _gpuFamily, definition.Name);

            // Inject barrier and fence primitives (Phase 3)
            // This processes marker comments like @BARRIER, @FENCE:DEVICE, etc.
            metalCode = InjectBarrierAndFenceCode(metalCode);

            _logger.LogDebug("Processed barrier/fence markers for kernel '{Name}'", definition.Name);

            // Determine the actual function name to use
            string functionName;
            if (definition.EntryPoint == "main")
            {
                // EntryPoint is the default "main", try to extract actual function name from Metal code
                var extractedName = ExtractKernelFunctionName(metalCode);
                functionName = extractedName ?? definition.Name;
                _logger.LogDebug("Extracted kernel function name '{FunctionName}' from Metal code", functionName);
            }
            else
            {
                // Use explicitly set EntryPoint or fallback to Name
                functionName = definition.EntryPoint ?? definition.Name;
            }

            // Compile Metal code
            _logger.LogDebug("Compiling kernel '{Name}' from source", definition.Name);
            var library = await CompileMetalCodeAsync(metalCode, definition.Name, options, cancellationToken).ConfigureAwait(false);

            // Create function and pipeline state
            var function = MetalNative.GetFunction(library, functionName);
            if (function == IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(library);
                throw new InvalidOperationException($"Failed to get function '{functionName}' from Metal library");
            }


            var pipelineState = MetalNative.CreateComputePipelineState(_device, function);
            if (pipelineState == IntPtr.Zero)
            {
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                throw new InvalidOperationException($"Failed to create compute pipeline state for kernel '{definition.Name}'");
            }


            stopwatch.Stop();
            var compilationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;

            // Add to cache

            byte[]? binaryData = null;
            try
            {
                // Try to get binary data for persistent caching
                var dataSize = MetalNative.GetLibraryDataSize(library);
                if (dataSize > 0)
                {
                    binaryData = new byte[dataSize];
                    var handle = GCHandle.Alloc(binaryData, GCHandleType.Pinned);
                    try
                    {
                        _ = MetalNative.GetLibraryData(library, handle.AddrOfPinnedObject(), dataSize);
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract binary data for kernel '{Name}'", definition.Name);
            }


            _kernelCache.AddKernel(definition, options, library, function, pipelineState, binaryData, compilationTimeMs);
            _logger.LogInformation("Compiled and cached kernel '{Name}' in {Time}ms", definition.Name, compilationTimeMs);

            // Get metadata

            var maxThreadsPerThreadgroup = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
            var threadExecutionWidth = MetalNative.GetThreadExecutionWidthTuple(pipelineState);


            var metadata = new CompilationMetadata
            {
                CompilationTimeMs = compilationTimeMs,
                MemoryUsage = { ["MaxThreadsPerThreadgroup"] = maxThreadsPerThreadgroup },
                Warnings = { $"Max threads per threadgroup: {maxThreadsPerThreadgroup}" }
            };

            // Release function reference (pipeline state retains it)

            MetalNative.ReleaseFunction(function);


            return (ICompiledKernel)new MetalCompiledKernel(
                definition,
                pipelineState,
                _commandQueue,
                maxThreadsPerThreadgroup,
                threadExecutionWidth,
                metadata,
                _logger,
                _commandBufferPool,
                _threadgroupOptimizer);
        }
        finally
        {
            _ = _compilationSemaphore.Release();
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
        var codeString = definition.Code ?? string.Empty;
        if (!codeString.Contains("kernel", StringComparison.Ordinal) && !codeString.Contains("metal", StringComparison.Ordinal) && !IsBinaryCode(Encoding.UTF8.GetBytes(codeString)))
        {
            return ValidationResult.Failure("Code does not appear to be valid Metal shader language or compiled binary");
        }

        return ValidationResult.Success();
    }

    private static string? ExtractKernelFunctionName(string metalCode)
    {
        // Extract kernel function name from Metal code using regex
        // Matches patterns like: "kernel void function_name(" or "kernel void function_name<T>("
        var match = KernelFunctionNamePattern().Match(metalCode);

        return match.Success ? match.Groups[1].Value : null;
    }

    private string ExtractMetalCode(KernelDefinition definition)
    {
        // If it's binary code, we'll need to handle it differently
        if (definition.Code != null && IsBinaryCode(Encoding.UTF8.GetBytes(definition.Code)))
        {
            throw new NotSupportedException("Pre-compiled Metal binaries are not yet supported");
        }

        // Get the source code
        var code = definition.Code ?? string.Empty;

        // Check if this is already MSL code (generated from C# or handwritten)
        if (code.Contains("#include <metal_stdlib>", StringComparison.Ordinal) ||
            code.Contains("kernel void", StringComparison.Ordinal))
        {
            // Already Metal code, return as-is
            return code;
        }

        // Check if the language is specified as Metal

        if (definition.Language == KernelLanguage.Metal)
        {
            // If Metal is specified but headers are missing, add them
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

        // If it's OpenCL C or unspecified, we would need translation
        // For now, throw an error indicating MSL is required
        if (code.Contains("__kernel", StringComparison.Ordinal) ||
            code.Contains("__global", StringComparison.Ordinal) ||
            definition.Language == KernelLanguage.OpenCL)
        {
            throw new NotSupportedException(
                "OpenCL C to Metal Shading Language translation is not implemented. " +
                "Please provide Metal Shading Language code directly or use the [Kernel] attribute " +
                "to generate Metal kernels from C# code.");
        }

        // Check if this looks like C# kernel code (from [Kernel] attribute)
        if (code.Contains("Kernel.ThreadId", StringComparison.Ordinal) ||
            code.Contains("ThreadId.X", StringComparison.Ordinal) ||
            (code.Contains("ReadOnlySpan<", StringComparison.Ordinal) && code.Contains("Span<", StringComparison.Ordinal)))
        {
            // Translate C# to MSL using the comprehensive translator
            var entryPoint = definition.EntryPoint ?? definition.Name;
            return TranslateFromCSharp(code, definition.Name, entryPoint);
        }

        // Try to add Metal headers if the code looks like it might be Metal
        var sb2 = new StringBuilder();
        _ = sb2.AppendLine("#include <metal_stdlib>");
        _ = sb2.AppendLine("#include <metal_compute>");
        _ = sb2.AppendLine("using namespace metal;");
        _ = sb2.AppendLine();
        _ = sb2.Append(code);
        return sb2.ToString();
    }

    /// <summary>
    /// Translates C# kernel code (from [Kernel] attribute) to Metal Shading Language.
    /// Maps C# constructs to their Metal equivalents for GPU execution.
    /// </summary>
    /// <param name="csharpCode">The C# kernel code to translate</param>
    /// <param name="kernelName">Name of the kernel for error reporting</param>
    /// <param name="entryPoint">Entry point function name</param>
    /// <returns>Metal Shading Language code ready for compilation</returns>
    private string TranslateFromCSharp(string csharpCode, string kernelName, string entryPoint)
    {
        // Delegate to the comprehensive translator module
        return _translator.Translate(csharpCode, kernelName, entryPoint);
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

    /// <summary>
    /// Compiles Metal Shading Language source code to a Metal library.
    /// Provides comprehensive error diagnostics and recovery strategies.
    /// </summary>
    /// <param name="code">MSL source code to compile</param>
    /// <param name="kernelName">Kernel name for diagnostics</param>
    /// <param name="options">Compilation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compiled Metal library handle</returns>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails</exception>
    private async Task<IntPtr> CompileMetalCodeAsync(string code, string kernelName, CompilationOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(options);

        return await Task.Run(() =>
        {
            // Create compile options
            var compileOptions = MetalNative.CreateCompileOptions();
            if (compileOptions == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Metal compile options");
            }

            // Configure optimization settings
            ConfigureOptimizationOptions(compileOptions, options, kernelName);

            // Set language version based on system capabilities
            var languageVersion = GetOptimalLanguageVersion();
            MetalNative.SetCompileOptionsLanguageVersion(compileOptions, languageVersion);

            _logger.LogDebug(
                "Compiling Metal kernel '{KernelName}' with language version {Version}, optimization level {Level}",
                kernelName,
                languageVersion,
                options.OptimizationLevel);

            try
            {
                // Compile the code
                var error = IntPtr.Zero;
                var library = MetalNative.CompileLibrary(_device, code, compileOptions, ref error);

                if (library == IntPtr.Zero)
                {
                    // Extract comprehensive error diagnostics
                    var errorDiagnostics = ExtractCompilationError(error, kernelName, code);

                    if (error != IntPtr.Zero)
                    {
                        MetalNative.ReleaseError(error);
                    }

                    // Log detailed error information
                    _logger.LogError(
                        "Metal compilation failed for kernel '{KernelName}':\n{Diagnostics}",
                        kernelName,
                        errorDiagnostics.FullDiagnostics);

                    // Attempt recovery if possible
                    if (options.EnableDebugInfo && errorDiagnostics.IsRecoverable)
                    {
                        _logger.LogInformation(
                            "Attempting compilation recovery for kernel '{KernelName}'",
                            kernelName);

                        // Could implement recovery strategies here (e.g., retry with relaxed settings)
                    }

                    throw new InvalidOperationException(
                        $"Metal compilation failed for kernel '{kernelName}': {errorDiagnostics.Summary}");
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

    /// <summary>
    /// Extracts comprehensive compilation error diagnostics from Metal error object.
    /// </summary>
    /// <param name="errorPtr">Metal error handle</param>
    /// <param name="kernelName">Kernel name for context</param>
    /// <param name="sourceCode">Source code that failed compilation</param>
    /// <returns>Structured error diagnostics</returns>
    private static CompilationErrorDiagnostics ExtractCompilationError(IntPtr errorPtr, string kernelName, string sourceCode)
    {
        var diagnostics = new CompilationErrorDiagnostics
        {
            KernelName = kernelName
        };

        if (errorPtr == IntPtr.Zero)
        {
            diagnostics.Summary = "Unknown compilation error (no error object)";
            diagnostics.FullDiagnostics = diagnostics.Summary;
            diagnostics.IsRecoverable = false;
            return diagnostics;
        }

        // Extract error message
        var errorMessage = Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(errorPtr));
        diagnostics.Summary = errorMessage ?? "Unknown error";

        // Build comprehensive diagnostics
        var diagBuilder = new StringBuilder();
        _ = diagBuilder.AppendLine("=== Metal Shader Compilation Error ===");
        _ = diagBuilder.AppendLine(CultureInfo.InvariantCulture, $"Kernel: {kernelName}");
        _ = diagBuilder.AppendLine(CultureInfo.InvariantCulture, $"Error: {diagnostics.Summary}");
        _ = diagBuilder.AppendLine();

        // Parse error message for line numbers and specific issues
        ParseErrorDetails(diagnostics, errorMessage, sourceCode, diagBuilder);

        diagnostics.FullDiagnostics = diagBuilder.ToString();

        return diagnostics;
    }

    /// <summary>
    /// Parses error message details to extract line numbers, error types, and suggestions.
    /// </summary>
    private static void ParseErrorDetails(CompilationErrorDiagnostics diagnostics, string? errorMessage, string sourceCode, StringBuilder diagBuilder)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        // Check for common error patterns
        diagnostics.IsRecoverable = errorMessage.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                                    errorMessage.Contains("deprecated", StringComparison.OrdinalIgnoreCase);

        // Try to extract line number (format: "error: <file>:<line>:<column>: message")
        var lineNumberMatch = Regex.Match(errorMessage, @":(\d+):(\d+):");
        if (lineNumberMatch.Success)
        {
            _ = int.TryParse(lineNumberMatch.Groups[1].Value, out var lineNumber);
            _ = int.TryParse(lineNumberMatch.Groups[2].Value, out var columnNumber);

            diagnostics.ErrorLine = lineNumber;
            diagnostics.ErrorColumn = columnNumber;

            _ = diagBuilder.AppendLine(CultureInfo.InvariantCulture, $"Location: Line {lineNumber}, Column {columnNumber}");
            _ = diagBuilder.AppendLine();

            // Show problematic line with context
            ShowSourceContext(sourceCode, lineNumber, columnNumber, diagBuilder);
        }

        // Add suggestions based on error type
        AddErrorSuggestions(errorMessage, diagBuilder);
    }

    /// <summary>
    /// Shows source code context around the error location.
    /// </summary>
    private static void ShowSourceContext(string sourceCode, int errorLine, int errorColumn, StringBuilder diagBuilder)
    {
        var lines = sourceCode.Split('\n');
        if (errorLine <= 0 || errorLine > lines.Length)
        {
            return;
        }

        _ = diagBuilder.AppendLine("Source Context:");

        // Show 2 lines before, the error line, and 2 lines after
        var startLine = Math.Max(1, errorLine - 2);
        var endLine = Math.Min(lines.Length, errorLine + 2);

        for (var i = startLine; i <= endLine; i++)
        {
            var prefix = i == errorLine ? ">>> " : "    ";
            _ = diagBuilder.AppendLine(CultureInfo.InvariantCulture, $"{prefix}{i,4}: {lines[i - 1]}");

            // Add caret indicator for error column
            if (i == errorLine && errorColumn > 0 && errorColumn <= lines[i - 1].Length)
            {
                _ = diagBuilder.Append("         ");
                _ = diagBuilder.Append(new string(' ', errorColumn - 1));
                _ = diagBuilder.AppendLine("^");
            }
        }

        _ = diagBuilder.AppendLine();
    }

    /// <summary>
    /// Adds helpful suggestions based on the error message content.
    /// </summary>
    private static void AddErrorSuggestions(string errorMessage, StringBuilder diagBuilder)
    {
        _ = diagBuilder.AppendLine("Suggestions:");

        if (errorMessage.Contains("undeclared identifier", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("use of undeclared", StringComparison.OrdinalIgnoreCase))
        {
            _ = diagBuilder.AppendLine("  - Check for typos in variable or function names");
            _ = diagBuilder.AppendLine("  - Ensure all variables are declared before use");
            _ = diagBuilder.AppendLine("  - Verify that Metal standard library functions are properly namespaced (metal::)");
        }
        else if (errorMessage.Contains("type mismatch", StringComparison.OrdinalIgnoreCase) ||
                 errorMessage.Contains("cannot convert", StringComparison.OrdinalIgnoreCase))
        {
            _ = diagBuilder.AppendLine("  - Check parameter types match the kernel signature");
            _ = diagBuilder.AppendLine("  - Ensure pointer types are correctly specified (device, constant, threadgroup)");
            _ = diagBuilder.AppendLine("  - Verify buffer attributes [[buffer(n)]] are consecutive");
        }
        else if (errorMessage.Contains("syntax error", StringComparison.OrdinalIgnoreCase))
        {
            _ = diagBuilder.AppendLine("  - Check for missing semicolons or brackets");
            _ = diagBuilder.AppendLine("  - Verify Metal Shading Language syntax is correct");
            _ = diagBuilder.AppendLine("  - Ensure C# to MSL translation was successful");
        }
        else if (errorMessage.Contains("attribute", StringComparison.OrdinalIgnoreCase))
        {
            _ = diagBuilder.AppendLine("  - Verify thread attributes are properly specified");
            _ = diagBuilder.AppendLine("  - Check buffer attributes use correct syntax [[buffer(index)]]");
            _ = diagBuilder.AppendLine("  - Ensure threadgroup memory uses [[threadgroup(index)]]");
        }
        else
        {
            _ = diagBuilder.AppendLine("  - Review Metal Shading Language specification");
            _ = diagBuilder.AppendLine("  - Check that the translated code is valid MSL");
            _ = diagBuilder.AppendLine("  - Enable debug info for more detailed diagnostics");
        }

        _ = diagBuilder.AppendLine();
    }

    /// <summary>
    /// Contains structured information about a Metal compilation error.
    /// </summary>
    private sealed class CompilationErrorDiagnostics
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public string KernelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a brief error summary.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full diagnostic message with context.
        /// </summary>
        public string FullDiagnostics { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the line number where the error occurred.
        /// </summary>
        public int ErrorLine { get; set; }

        /// <summary>
        /// Gets or sets the column number where the error occurred.
        /// </summary>
        public int ErrorColumn { get; set; }

        /// <summary>
        /// Gets or sets whether the error is potentially recoverable.
        /// </summary>
        public bool IsRecoverable { get; set; }
    }



    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Dispose the kernel cache (logs statistics)
        _kernelCache?.Dispose();

        _compilationSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Configures Metal compilation options for optimization.
    /// Applies fast math and other performance optimizations based on compilation settings.
    /// </summary>
    /// <param name="compileOptions">Metal compile options handle</param>
    /// <param name="options">DotCompute compilation options</param>
    /// <param name="kernelName">Kernel name for logging</param>
    private void ConfigureOptimizationOptions(IntPtr compileOptions, CompilationOptions options, string kernelName)
    {
        // Configure fast math based on optimization level and options
        // Fast math is now enabled by default (changed from false to true)
        var enableFastMath = options.FastMath || options.OptimizationLevel >= OptimizationLevel.Default;

        if (enableFastMath)
        {
            MetalNative.SetCompileOptionsFastMath(compileOptions, true);
            _logger.LogDebug("Fast math optimization enabled for kernel: {Name} (trades precision for performance)", kernelName);
        }

        // Additional optimization hints based on optimization level
        if (options.OptimizationLevel == OptimizationLevel.O3)
        {
            _logger.LogDebug("Maximum optimization (O3) requested for kernel: {Name}", kernelName);
            // Metal doesn't expose many additional optimization flags through the public API
            // but the fast math flag combined with O3 provides aggressive optimizations
        }
        else if (options.OptimizationLevel == OptimizationLevel.None)
        {
            _logger.LogDebug("Debug mode (no optimization) for kernel: {Name}", kernelName);
        }
    }

    /// <summary>
    /// Detects the GPU family from the Metal device for optimization macros.
    /// </summary>
    /// <returns>GPU family string (e.g., "Apple9" for M3, "Apple8" for M2, "Apple7" for M1)</returns>
    private string DetectGpuFamily()
    {
        try
        {
            var deviceInfo = MetalNative.GetDeviceInfo(_device);
            var familiesString = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? string.Empty;

            _logger.LogDebug("Detected GPU families: {Families}", familiesString);

            // Return the highest priority family for macro injection
            if (familiesString.Contains("Apple9", StringComparison.Ordinal))
            {
                return "Apple9";
            }

            if (familiesString.Contains("Apple8", StringComparison.Ordinal))
            {
                return "Apple8";
            }

            if (familiesString.Contains("Apple7", StringComparison.Ordinal))
            {
                return "Apple7";
            }

            if (familiesString.Contains("Apple6", StringComparison.Ordinal))
            {
                return "Apple6";
            }

            if (familiesString.Contains("Apple", StringComparison.Ordinal))
            {
                return "AppleGeneric";
            }

            return "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect GPU family, using generic configuration");
            return "Unknown";
        }
    }

    /// <summary>
    /// Injects GPU family-specific optimization macros into MSL code.
    /// Provides compile-time constants for GPU capabilities (M1/M2/M3 detection, SIMD width, memory sizes).
    /// </summary>
    /// <param name="mslCode">Original Metal Shading Language code</param>
    /// <param name="gpuFamily">Detected GPU family string</param>
    /// <returns>MSL code with injected optimization macros</returns>
    private static string InjectGpuFamilyMacros(string mslCode, string gpuFamily)
    {
        var builder = new StringBuilder();

        // Common Metal definitions
        builder.AppendLine("#include <metal_stdlib>");
        builder.AppendLine("using namespace metal;");
        builder.AppendLine();

        // GPU family detection macros
        if (gpuFamily.Contains("Apple9", StringComparison.Ordinal))
        {
            builder.AppendLine("// Apple M3 GPU Family (Apple9)");
            builder.AppendLine("#define APPLE_M3_GPU 1");
            builder.AppendLine("#define GPU_FAMILY_APPLE9 1");
            builder.AppendLine("#define SIMDGROUP_SIZE 32");
            builder.AppendLine("#define THREADGROUP_MEM_64KB 1");
            builder.AppendLine("#define THREADGROUP_MEM_SIZE 65536");
            builder.AppendLine("#define SUPPORTS_RAYTRACING 1");
            builder.AppendLine("#define SUPPORTS_MESH_SHADERS 1");
            builder.AppendLine("#define COMPUTE_UNITS 40");
            builder.AppendLine("#define MAX_SIMD_WIDTH 32");
        }
        else if (gpuFamily.Contains("Apple8", StringComparison.Ordinal))
        {
            builder.AppendLine("// Apple M2 GPU Family (Apple8)");
            builder.AppendLine("#define APPLE_M2_GPU 1");
            builder.AppendLine("#define GPU_FAMILY_APPLE8 1");
            builder.AppendLine("#define SIMDGROUP_SIZE 32");
            builder.AppendLine("#define THREADGROUP_MEM_32KB 1");
            builder.AppendLine("#define THREADGROUP_MEM_SIZE 32768");
            builder.AppendLine("#define COMPUTE_UNITS 20");
            builder.AppendLine("#define MAX_SIMD_WIDTH 32");
            builder.AppendLine("#define SUPPORTS_FUNCTION_POINTERS 1");
        }
        else if (gpuFamily.Contains("Apple7", StringComparison.Ordinal))
        {
            builder.AppendLine("// Apple M1 GPU Family (Apple7)");
            builder.AppendLine("#define APPLE_M1_GPU 1");
            builder.AppendLine("#define GPU_FAMILY_APPLE7 1");
            builder.AppendLine("#define SIMDGROUP_SIZE 32");
            builder.AppendLine("#define THREADGROUP_MEM_32KB 1");
            builder.AppendLine("#define THREADGROUP_MEM_SIZE 32768");
            builder.AppendLine("#define COMPUTE_UNITS 16");
            builder.AppendLine("#define MAX_SIMD_WIDTH 32");
        }
        else if (gpuFamily.Contains("Apple6", StringComparison.Ordinal))
        {
            builder.AppendLine("// Apple A14/A15 GPU Family (Apple6)");
            builder.AppendLine("#define GPU_FAMILY_APPLE6 1");
            builder.AppendLine("#define SIMDGROUP_SIZE 32");
            builder.AppendLine("#define THREADGROUP_MEM_16KB 1");
            builder.AppendLine("#define THREADGROUP_MEM_SIZE 16384");
            builder.AppendLine("#define COMPUTE_UNITS 8");
            builder.AppendLine("#define MAX_SIMD_WIDTH 32");
        }
        else if (gpuFamily.Contains("Apple", StringComparison.Ordinal))
        {
            builder.AppendLine("// Generic Apple Silicon");
            builder.AppendLine("#define GPU_FAMILY_APPLE 1");
            builder.AppendLine("#define SIMDGROUP_SIZE 32");
            builder.AppendLine("#define THREADGROUP_MEM_16KB 1");
            builder.AppendLine("#define THREADGROUP_MEM_SIZE 16384");
        }
        else
        {
            builder.AppendLine("// Unknown GPU family - using safe defaults");
            builder.AppendLine("#define SIMDGROUP_SIZE 32");
            builder.AppendLine("#define THREADGROUP_MEM_SIZE 16384");
        }

        // Universal Apple Silicon optimizations
        if (gpuFamily.Contains("Apple", StringComparison.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine("// Apple Silicon universal capabilities");
            builder.AppendLine("#define UNIFIED_MEMORY_NATIVE 1");
            builder.AppendLine("#define TBDR_ARCHITECTURE 1");
            builder.AppendLine("#define SUPPORTS_INDIRECT_COMMAND_BUFFERS 1");
            builder.AppendLine("#define SUPPORTS_ATOMIC_OPERATIONS 1");
        }

        builder.AppendLine();

        // Remove Metal standard library includes from the original code if present
        // to avoid duplicate includes
        var lines = mslCode.Split('\n');
        var filteredCode = new StringBuilder();
        var skipNextEmptyLine = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip duplicate includes
            if (trimmedLine.StartsWith("#include <metal_stdlib>", StringComparison.Ordinal) ||
                trimmedLine.StartsWith("#include <metal_compute>", StringComparison.Ordinal) ||
                trimmedLine.StartsWith("using namespace metal;", StringComparison.Ordinal))
            {
                skipNextEmptyLine = true;
                continue;
            }

            // Skip one empty line after removed includes
            if (skipNextEmptyLine && string.IsNullOrWhiteSpace(trimmedLine))
            {
                skipNextEmptyLine = false;
                continue;
            }

            skipNextEmptyLine = false;
            filteredCode.AppendLine(line);
        }

        builder.Append(filteredCode);

        return builder.ToString();
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


    /// <inheritdoc/>
    public ValueTask<ValidationResult> ValidateAsync(KernelDefinition kernel, CancellationToken cancellationToken = default) => ValueTask.FromResult(Validate(kernel));

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> OptimizeAsync(ICompiledKernel kernel, OptimizationLevel level, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        // Only optimize Metal compiled kernels
        if (kernel is not MetalCompiledKernel metalKernel)
        {
            _logger.LogWarning("Cannot optimize non-Metal kernel '{Name}'", kernel.Name);
            return kernel;
        }

        // Skip optimization if level is None
        if (level == OptimizationLevel.None)
        {
            _logger.LogDebug("Skipping optimization for kernel '{Name}' (level: None)", kernel.Name);
            return kernel;
        }

        try
        {
            _logger.LogInformation("Optimizing Metal kernel '{Name}' with level {Level}", kernel.Name, level);

            // Create kernel definition from the compiled kernel
            var definition = new KernelDefinition(kernel.Name, metalKernel.SourceCode ?? string.Empty)
            {
                EntryPoint = kernel.Name,
                Language = KernelLanguage.Metal
            };

            // Create optimizer instance
            var optimizer = new MetalKernelOptimizer(_device, _logger);

            // Perform optimization
            var (optimizedDefinition, telemetry) = await optimizer.OptimizeAsync(definition, level, cancellationToken).ConfigureAwait(false);

            // Log optimization results
            _logger.LogInformation(
                "Kernel '{Name}' optimization completed in {Time}ms. Applied {Count} optimizations. " +
                "Threadgroup size: {Original} -> {Optimized}",
                telemetry.KernelName,
                telemetry.OptimizationTimeMs,
                telemetry.AppliedOptimizations.Count,
                telemetry.OriginalThreadgroupSize,
                telemetry.OptimizedThreadgroupSize);

            // Recompile the optimized kernel
            var compilationOptions = new CompilationOptions
            {
                OptimizationLevel = level,
                EnableDebugInfo = level == OptimizationLevel.None,
                FastMath = level >= OptimizationLevel.Default
            };

            var optimizedKernel = await CompileAsync(optimizedDefinition, compilationOptions, cancellationToken).ConfigureAwait(false);

            // Add optimization metadata to the compiled kernel
            if (optimizedKernel is MetalCompiledKernel optimizedMetalKernel)
            {
                var metadata = optimizedMetalKernel.CompilationMetadata;
                metadata.Warnings.Add($"Optimization profile: {telemetry.Profile}");
                metadata.Warnings.Add($"Applied {telemetry.AppliedOptimizations.Count} optimizations");

                if (telemetry.HasThreadgroupOptimization)
                {
                    metadata.Warnings.Add($"Threadgroup size optimized: {telemetry.OriginalThreadgroupSize} -> {telemetry.OptimizedThreadgroupSize}");
                }

                if (telemetry.HasMemoryCoalescing)
                {
                    metadata.Warnings.Add("Memory coalescing optimizations applied");
                }
            }

            return optimizedKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize Metal kernel '{Name}'", kernel.Name);

            // Return original kernel on optimization failure
            return kernel;
        }
    }

    // Backward compatibility methods for legacy IKernelCompiler interface

    /// <inheritdoc />
    public async Task<ICompiledKernel> CompileAsync(
        KernelDefinition kernelDefinition,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinition);
        ArgumentNullException.ThrowIfNull(accelerator);

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType
        };

        return await CompileAsync(kernelDefinition, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> CanCompileAsync(KernelDefinition kernelDefinition, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinition);
        ArgumentNullException.ThrowIfNull(accelerator);

        try
        {
            var validationResult = Validate(kernelDefinition);
            return Task.FromResult(validationResult.IsValid);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public CompilationOptions GetSupportedOptions(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O3,
            EnableDebugInfo = false,
            TargetArchitecture = accelerator.Info.DeviceType,
            AllowUnsafeCode = false, // Metal doesn't support unsafe code
            PreferredBlockSize = new Dim3(64, 1, 1), // Good default for Metal
            SharedMemorySize = 32 * 1024 // 32KB default for Metal threadgroup memory
        };
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, ICompiledKernel>> BatchCompileAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelDefinitions);
        ArgumentNullException.ThrowIfNull(accelerator);

        var results = new Dictionary<string, ICompiledKernel>();

        // Metal can benefit from batch compilation by building a single library
        foreach (var kernelDef in kernelDefinitions)
        {
            try
            {
                var compiled = await CompileAsync(kernelDef, accelerator, cancellationToken);
                results[kernelDef.Name] = compiled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile kernel {KernelName} in batch operation", kernelDef.Name);
                throw new InvalidOperationException($"Batch compilation failed for kernel {kernelDef.Name}: {ex.Message}", ex);
            }
        }

        return results;
    }
}
