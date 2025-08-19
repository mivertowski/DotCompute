using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Implementations.Kernels;


/// <summary>
/// Base test kernel compiler implementation for testing.
/// </summary>
public abstract class TestKernelCompilerBase
{
    private readonly ConcurrentDictionary<string, CompiledKernelInfo> _compilationCache;
    private readonly List<CompilationDiagnostic> _diagnostics;
    private long _compilationCount;
    private TimeSpan _totalCompilationTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestKernelCompilerBase"/> class.
    /// </summary>
    protected TestKernelCompilerBase()
    {
        _compilationCache = new ConcurrentDictionary<string, CompiledKernelInfo>();
        _diagnostics = [];
    }

    /// <summary>
    /// Gets the supported language.
    /// </summary>
    /// <value>
    /// The supported language.
    /// </value>
    public abstract KernelLanguage SupportedLanguage { get; }

    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    /// <value>
    /// The name of the compiler.
    /// </value>
    public abstract string CompilerName { get; }

    /// <summary>
    /// Gets the compilation count.
    /// </summary>
    /// <value>
    /// The compilation count.
    /// </value>
    public long CompilationCount => _compilationCount;

    /// <summary>
    /// Gets the total compilation time.
    /// </summary>
    /// <value>
    /// The total compilation time.
    /// </value>
    public TimeSpan TotalCompilationTime => _totalCompilationTime;

    /// <summary>
    /// Gets the average compilation time ms.
    /// </summary>
    /// <value>
    /// The average compilation time ms.
    /// </value>
    public double AverageCompilationTimeMs => _compilationCount > 0
        ? _totalCompilationTime.TotalMilliseconds / _compilationCount
        : 0;

    /// <summary>
    /// Gets the diagnostics.
    /// </summary>
    /// <value>
    /// The diagnostics.
    /// </value>
    public IReadOnlyList<CompilationDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    /// <summary>
    /// Compiles the internal asynchronous.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    protected virtual async Task<CompiledKernelInfo> CompileInternalAsync(
        IKernelSource source,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Simulate compilation delay
        await Task.Delay(10, cancellationToken);

        // Generate pseudo-assembly based on language
        var assembly = GeneratePseudoAssembly(source, options);

        // Create compiled kernel info
        var info = new CompiledKernelInfo
        {
            Name = source.Name,
            EntryPoint = source.EntryPoint,
            Assembly = assembly,
            CompiledAt = DateTime.UtcNow,
            Options = options,
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = source.Language.ToString(),
                ["CompilerVersion"] = "1.0.0",
                ["OptimizationLevel"] = options.OptimizationLevel.ToString()
            }
        };

        stopwatch.Stop();
        _ = Interlocked.Increment(ref _compilationCount);
        _totalCompilationTime = _totalCompilationTime.Add(stopwatch.Elapsed);

        // Add to cache
        var cacheKey = GenerateCacheKey(source, options);
        _compilationCache[cacheKey] = info;

        // Add diagnostic info
        _diagnostics.Add(new CompilationDiagnostic
        {
            Level = DiagnosticLevel.Info,
            Message = $"Successfully compiled kernel '{source.Name}' in {stopwatch.ElapsedMilliseconds}ms",
            Timestamp = DateTime.UtcNow
        });

        return info;
    }

    /// <summary>
    /// Generates the pseudo assembly.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    protected abstract string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options);

    /// <summary>
    /// Generates the cache key.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    protected static string GenerateCacheKey(IKernelSource source, CompilationOptions options)
    {
        var builder = new StringBuilder();
        _ = builder.Append(source.Name);
        _ = builder.Append('_');
        _ = builder.Append(source.Language);
        _ = builder.Append('_');
        _ = builder.Append(options.OptimizationLevel);
        _ = builder.Append('_');
        _ = builder.Append(options.FastMath);
        _ = builder.Append('_');
        _ = builder.Append(options.UnrollLoops);
        return builder.ToString();
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void ClearCache() => _compilationCache.Clear();

    /// <summary>
    /// Clears the diagnostics.
    /// </summary>
    public void ClearDiagnostics() => _diagnostics.Clear();
}

/// <summary>
/// Test CUDA kernel compiler implementation.
/// </summary>
public sealed class TestCudaKernelCompiler : TestKernelCompilerBase
{
    /// <summary>
    /// Gets the supported language.
    /// </summary>
    /// <value>
    /// The supported language.
    /// </value>
    public override KernelLanguage SupportedLanguage => KernelLanguage.Cuda;

    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    /// <value>
    /// The name of the compiler.
    /// </value>
    public override string CompilerName => "Test CUDA Compiler(nvcc simulator)";

    /// <summary>
    /// Compiles the asynchronous.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentException">Unsupported language: {source.Language}. Expected CUDA or PTX. - source</exception>
    public async Task<CompiledKernelInfo> CompileAsync(
        IKernelSource source,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (source.Language is not KernelLanguage.Cuda and not KernelLanguage.Ptx)
        {
            throw new ArgumentException($"Unsupported language: {source.Language}. Expected CUDA or PTX.", nameof(source));
        }

        return await CompileInternalAsync(source, options, cancellationToken);
    }

    /// <summary>
    /// Generates the pseudo assembly.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    protected override string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options)
    {
        var assembly = new StringBuilder();
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"// PTX Assembly for {source.Name}"));
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"// Compiled with optimization level: {options.OptimizationLevel}"));
        _ = assembly.AppendLine(".version 7.5");
        _ = assembly.AppendLine(".target sm_86");
        _ = assembly.AppendLine(".address_size 64");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $".visible .entry {source.EntryPoint}("));
        _ = assembly.AppendLine("    .param .u64 param_0,");
        _ = assembly.AppendLine("    .param .u64 param_1,");
        _ = assembly.AppendLine("    .param .u32 param_2");
        _ = assembly.AppendLine(")");
        _ = assembly.AppendLine("{");
        _ = assembly.AppendLine("    .reg .pred %p<2>;");
        _ = assembly.AppendLine("    .reg .b32 %r<8>;");
        _ = assembly.AppendLine("    .reg .b64 %rd<8>;");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("    // Kernel implementation");
        _ = assembly.AppendLine("    mov.u32 %r1, %tid.x;");
        _ = assembly.AppendLine("    mov.u32 %r2, %ctaid.x;");
        _ = assembly.AppendLine("    mov.u32 %r3, %ntid.x;");
        _ = assembly.AppendLine("    mad.lo.s32 %r4, %r2, %r3, %r1;");
        _ = assembly.AppendLine();

        if (options.FastMath)
        {
            _ = assembly.AppendLine("    // Fast math operations");
            _ = assembly.AppendLine("    mul.ftz.f32 %r5, %r4, 0.5;");
        }

        if (options.UnrollLoops)
        {
            _ = assembly.AppendLine("    // Unrolled loop");
            _ = assembly.AppendLine("    #pragma unroll 4");
        }

        _ = assembly.AppendLine("    ret;");
        _ = assembly.AppendLine("}");

        return assembly.ToString();
    }
}

/// <summary>
/// Test OpenCL kernel compiler implementation.
/// </summary>
public sealed class TestOpenCLKernelCompiler : TestKernelCompilerBase
{
    /// <summary>
    /// Gets the supported language.
    /// </summary>
    /// <value>
    /// The supported language.
    /// </value>
    public override KernelLanguage SupportedLanguage => KernelLanguage.OpenCL;

    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    /// <value>
    /// The name of the compiler.
    /// </value>
    public override string CompilerName => "Test OpenCL Compiler";

    /// <summary>
    /// Compiles the asynchronous.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentException">Unsupported language: {source.Language}. Expected OpenCL or SPIRV. - source</exception>
    public async Task<CompiledKernelInfo> CompileAsync(
        IKernelSource source,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (source.Language is not KernelLanguage.OpenCL and not KernelLanguage.SPIRV)
        {
            throw new ArgumentException($"Unsupported language: {source.Language}. Expected OpenCL or SPIRV.", nameof(source));
        }

        return await CompileInternalAsync(source, options, cancellationToken);
    }

    /// <summary>
    /// Generates the pseudo assembly.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    protected override string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options)
    {
        var assembly = new StringBuilder();
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"; SPIR-V Assembly for {source.Name}"));
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"; Compiled with optimization level: {options.OptimizationLevel}"));
        _ = assembly.AppendLine("OpCapability Kernel");
        _ = assembly.AppendLine("OpCapability Addresses");
        _ = assembly.AppendLine("OpCapability Int64");
        _ = assembly.AppendLine("OpMemoryModel Physical64 OpenCL");
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"OpEntryPoint Kernel %{source.EntryPoint} \"{source.EntryPoint}\""));
        _ = assembly.AppendLine("OpSource OpenCL_C 120");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("; Decorations");
        _ = assembly.AppendLine("OpDecorate %global_id BuiltIn GlobalInvocationId");
        _ = assembly.AppendLine("OpDecorate %local_id BuiltIn LocalInvocationId");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("; Types");
        _ = assembly.AppendLine("%void = OpTypeVoid");
        _ = assembly.AppendLine("%uint = OpTypeInt 32 0");
        _ = assembly.AppendLine("%float = OpTypeFloat 32");
        _ = assembly.AppendLine("%v3uint = OpTypeVector %uint 3");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("; Function");
        _ = assembly.AppendLine("%func_type = OpTypeFunction %void");
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"%{source.EntryPoint} = OpFunction %void None %func_type"));
        _ = assembly.AppendLine("%entry = OpLabel");

        if (options.FastMath)
        {
            _ = assembly.AppendLine("; Fast math enabled");
            _ = assembly.AppendLine("OpDecorate %mul_result FPFastMathMode Fast");
        }

        _ = assembly.AppendLine("OpReturn");
        _ = assembly.AppendLine("OpFunctionEnd");

        return assembly.ToString();
    }
}

/// <summary>
/// Test DirectCompute/HLSL kernel compiler implementation.
/// </summary>
public sealed class TestDirectComputeCompiler : TestKernelCompilerBase
{
    /// <summary>
    /// Gets the supported language.
    /// </summary>
    /// <value>
    /// The supported language.
    /// </value>
    public override KernelLanguage SupportedLanguage => KernelLanguage.HLSL;

    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    /// <value>
    /// The name of the compiler.
    /// </value>
    public override string CompilerName => "Test DirectCompute Compiler(dxc simulator)";

    /// <summary>
    /// Compiles the asynchronous.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentException">Unsupported language: {source.Language}. Expected HLSL. - source</exception>
    public async Task<CompiledKernelInfo> CompileAsync(
        IKernelSource source,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (source.Language != KernelLanguage.HLSL)
        {
            throw new ArgumentException($"Unsupported language: {source.Language}. Expected HLSL.", nameof(source));
        }

        return await CompileInternalAsync(source, options, cancellationToken);
    }

    /// <summary>
    /// Generates the pseudo assembly.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    protected override string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options)
    {
        var assembly = new StringBuilder();
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"// DXIL Assembly for {source.Name}"));
        _ = assembly.AppendLine(string.Create(CultureInfo.InvariantCulture, $"// Compiled with optimization level: {options.OptimizationLevel}"));
        _ = assembly.AppendLine("// Target: cs_6_0");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("dcl_globalFlags refactoringAllowed");
        _ = assembly.AppendLine("dcl_compute_shader");
        _ = assembly.AppendLine("dcl_thread_group 256, 1, 1");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("dcl_resource_structured t0, 4");
        _ = assembly.AppendLine("dcl_resource_structured t1, 4");
        _ = assembly.AppendLine("dcl_uav_structured u0, 4");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("dcl_input vThreadID.x");
        _ = assembly.AppendLine("dcl_temps 4");
        _ = assembly.AppendLine();
        _ = assembly.AppendLine("// Main compute shader");
        _ = assembly.AppendLine("mov r0.x, vThreadID.x");
        _ = assembly.AppendLine("ld_structured r1.x, r0.x, 0, t0.x");
        _ = assembly.AppendLine("ld_structured r2.x, r0.x, 0, t1.x");

        if (options.FastMath)
        {
            _ = assembly.AppendLine("// Fast math");
            _ = assembly.AppendLine("mad r3.x, r1.x, r2.x, 0.5");
        }
        else
        {
            _ = assembly.AppendLine("add r3.x, r1.x, r2.x");
        }

        _ = assembly.AppendLine("store_structured u0.x, r0.x, 0, r3.x");
        _ = assembly.AppendLine("ret");

        return assembly.ToString();
    }
}

/// <summary>
/// Information about a compiled kernel.
/// </summary>
public sealed class CompiledKernelInfo
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>
    /// The name.
    /// </value>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the entry point.
    /// </summary>
    /// <value>
    /// The entry point.
    /// </value>
    public string EntryPoint { get; set; } = "";

    /// <summary>
    /// Gets or sets the assembly.
    /// </summary>
    /// <value>
    /// The assembly.
    /// </value>
    public string Assembly { get; set; } = "";

    /// <summary>
    /// Gets or sets the compiled at.
    /// </summary>
    /// <value>
    /// The compiled at.
    /// </value>
    public DateTime CompiledAt { get; set; }

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>
    /// The options.
    /// </value>
    public CompilationOptions Options { get; set; } = new();

    /// <summary>
    /// Gets the metadata.
    /// </summary>
    /// <value>
    /// The metadata.
    /// </value>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Compilation diagnostic information.
/// </summary>
public sealed class CompilationDiagnostic
{
    /// <summary>
    /// Gets or sets the level.
    /// </summary>
    /// <value>
    /// The level.
    /// </value>
    public DiagnosticLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>
    /// The message.
    /// </value>
    public string Message { get; set; } = "";

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>
    /// The timestamp.
    /// </value>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    /// <value>
    /// The location.
    /// </value>
    public string? Location { get; set; }
}

/// <summary>
/// Diagnostic severity level.
/// </summary>
public enum DiagnosticLevel
{
    /// <summary>
    /// The information
    /// </summary>
    Info,

    /// <summary>
    /// The warning
    /// </summary>
    Warning,

    /// <summary>
    /// The error
    /// </summary>
    Error
}
