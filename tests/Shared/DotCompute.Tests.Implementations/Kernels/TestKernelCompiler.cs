using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Shared.Kernels;

/// <summary>
/// Base test kernel compiler implementation for testing.
/// </summary>
public abstract class TestKernelCompilerBase
{
    protected readonly ConcurrentDictionary<string, CompiledKernelInfo> _compilationCache;
    protected readonly List<CompilationDiagnostic> _diagnostics;
    protected long _compilationCount;
    protected TimeSpan _totalCompilationTime;

    protected TestKernelCompilerBase()
    {
        _compilationCache = new ConcurrentDictionary<string, CompiledKernelInfo>();
        _diagnostics = new List<CompilationDiagnostic>();
    }

    public abstract KernelLanguage SupportedLanguage { get; }
    public abstract string CompilerName { get; }

    public long CompilationCount => _compilationCount;
    public TimeSpan TotalCompilationTime => _totalCompilationTime;
    public double AverageCompilationTimeMs => _compilationCount > 0 
        ? _totalCompilationTime.TotalMilliseconds / _compilationCount 
        : 0;

    public IReadOnlyList<CompilationDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

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
        Interlocked.Increment(ref _compilationCount);
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

    protected abstract string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options);

    protected string GenerateCacheKey(IKernelSource source, CompilationOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(source.Name);
        builder.Append('_');
        builder.Append(source.Language);
        builder.Append('_');
        builder.Append(options.OptimizationLevel);
        builder.Append('_');
        builder.Append(options.FastMath);
        builder.Append('_');
        builder.Append(options.UnrollLoops);
        return builder.ToString();
    }

    public void ClearCache()
    {
        _compilationCache.Clear();
    }

    public void ClearDiagnostics()
    {
        _diagnostics.Clear();
    }
}

/// <summary>
/// Test CUDA kernel compiler implementation.
/// </summary>
public class TestCudaKernelCompiler : TestKernelCompilerBase
{
    public override KernelLanguage SupportedLanguage => KernelLanguage.Cuda;
    public override string CompilerName => "Test CUDA Compiler (nvcc simulator)";

    public async Task<CompiledKernelInfo> CompileAsync(
        IKernelSource source,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (source.Language != KernelLanguage.Cuda && source.Language != KernelLanguage.Ptx)
        {
            throw new ArgumentException($"Unsupported language: {source.Language}. Expected CUDA or PTX.", nameof(source));
        }

        return await CompileInternalAsync(source, options, cancellationToken);
    }

    protected override string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options)
    {
        var assembly = new StringBuilder();
        assembly.AppendLine($"// PTX Assembly for {source.Name}");
        assembly.AppendLine($"// Compiled with optimization level: {options.OptimizationLevel}");
        assembly.AppendLine($".version 7.5");
        assembly.AppendLine($".target sm_86");
        assembly.AppendLine($".address_size 64");
        assembly.AppendLine();
        assembly.AppendLine($".visible .entry {source.EntryPoint}(");
        assembly.AppendLine($"    .param .u64 param_0,");
        assembly.AppendLine($"    .param .u64 param_1,");
        assembly.AppendLine($"    .param .u32 param_2");
        assembly.AppendLine($")");
        assembly.AppendLine($"{{");
        assembly.AppendLine($"    .reg .pred %p<2>;");
        assembly.AppendLine($"    .reg .b32 %r<8>;");
        assembly.AppendLine($"    .reg .b64 %rd<8>;");
        assembly.AppendLine();
        assembly.AppendLine($"    // Kernel implementation");
        assembly.AppendLine($"    mov.u32 %r1, %tid.x;");
        assembly.AppendLine($"    mov.u32 %r2, %ctaid.x;");
        assembly.AppendLine($"    mov.u32 %r3, %ntid.x;");
        assembly.AppendLine($"    mad.lo.s32 %r4, %r2, %r3, %r1;");
        assembly.AppendLine();
        
        if (options.FastMath)
        {
            assembly.AppendLine($"    // Fast math operations");
            assembly.AppendLine($"    mul.ftz.f32 %r5, %r4, 0.5;");
        }
        
        if (options.UnrollLoops)
        {
            assembly.AppendLine($"    // Unrolled loop");
            assembly.AppendLine($"    #pragma unroll 4");
        }
        
        assembly.AppendLine($"    ret;");
        assembly.AppendLine($"}}");
        
        return assembly.ToString();
    }
}

/// <summary>
/// Test OpenCL kernel compiler implementation.
/// </summary>
public class TestOpenCLKernelCompiler : TestKernelCompilerBase
{
    public override KernelLanguage SupportedLanguage => KernelLanguage.OpenCL;
    public override string CompilerName => "Test OpenCL Compiler";

    public async Task<CompiledKernelInfo> CompileAsync(
        IKernelSource source,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (source.Language != KernelLanguage.OpenCL && source.Language != KernelLanguage.SPIRV)
        {
            throw new ArgumentException($"Unsupported language: {source.Language}. Expected OpenCL or SPIRV.", nameof(source));
        }

        return await CompileInternalAsync(source, options, cancellationToken);
    }

    protected override string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options)
    {
        var assembly = new StringBuilder();
        assembly.AppendLine($"; SPIR-V Assembly for {source.Name}");
        assembly.AppendLine($"; Compiled with optimization level: {options.OptimizationLevel}");
        assembly.AppendLine($"OpCapability Kernel");
        assembly.AppendLine($"OpCapability Addresses");
        assembly.AppendLine($"OpCapability Int64");
        assembly.AppendLine($"OpMemoryModel Physical64 OpenCL");
        assembly.AppendLine($"OpEntryPoint Kernel %{source.EntryPoint} \"{source.EntryPoint}\"");
        assembly.AppendLine($"OpSource OpenCL_C 120");
        assembly.AppendLine();
        assembly.AppendLine($"; Decorations");
        assembly.AppendLine($"OpDecorate %global_id BuiltIn GlobalInvocationId");
        assembly.AppendLine($"OpDecorate %local_id BuiltIn LocalInvocationId");
        assembly.AppendLine();
        assembly.AppendLine($"; Types");
        assembly.AppendLine($"%void = OpTypeVoid");
        assembly.AppendLine($"%uint = OpTypeInt 32 0");
        assembly.AppendLine($"%float = OpTypeFloat 32");
        assembly.AppendLine($"%v3uint = OpTypeVector %uint 3");
        assembly.AppendLine();
        assembly.AppendLine($"; Function");
        assembly.AppendLine($"%func_type = OpTypeFunction %void");
        assembly.AppendLine($"%{source.EntryPoint} = OpFunction %void None %func_type");
        assembly.AppendLine($"%entry = OpLabel");
        
        if (options.FastMath)
        {
            assembly.AppendLine($"; Fast math enabled");
            assembly.AppendLine($"OpDecorate %mul_result FPFastMathMode Fast");
        }
        
        assembly.AppendLine($"OpReturn");
        assembly.AppendLine($"OpFunctionEnd");
        
        return assembly.ToString();
    }
}

/// <summary>
/// Test DirectCompute/HLSL kernel compiler implementation.
/// </summary>
public class TestDirectComputeCompiler : TestKernelCompilerBase
{
    public override KernelLanguage SupportedLanguage => KernelLanguage.HLSL;
    public override string CompilerName => "Test DirectCompute Compiler (dxc simulator)";

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

    protected override string GeneratePseudoAssembly(IKernelSource source, CompilationOptions options)
    {
        var assembly = new StringBuilder();
        assembly.AppendLine($"// DXIL Assembly for {source.Name}");
        assembly.AppendLine($"// Compiled with optimization level: {options.OptimizationLevel}");
        assembly.AppendLine($"// Target: cs_6_0");
        assembly.AppendLine();
        assembly.AppendLine($"dcl_globalFlags refactoringAllowed");
        assembly.AppendLine($"dcl_compute_shader");
        assembly.AppendLine($"dcl_thread_group 256, 1, 1");
        assembly.AppendLine();
        assembly.AppendLine($"dcl_resource_structured t0, 4");
        assembly.AppendLine($"dcl_resource_structured t1, 4");
        assembly.AppendLine($"dcl_uav_structured u0, 4");
        assembly.AppendLine();
        assembly.AppendLine($"dcl_input vThreadID.x");
        assembly.AppendLine($"dcl_temps 4");
        assembly.AppendLine();
        assembly.AppendLine($"// Main compute shader");
        assembly.AppendLine($"mov r0.x, vThreadID.x");
        assembly.AppendLine($"ld_structured r1.x, r0.x, 0, t0.x");
        assembly.AppendLine($"ld_structured r2.x, r0.x, 0, t1.x");
        
        if (options.FastMath)
        {
            assembly.AppendLine($"// Fast math");
            assembly.AppendLine($"mad r3.x, r1.x, r2.x, 0.5");
        }
        else
        {
            assembly.AppendLine($"add r3.x, r1.x, r2.x");
        }
        
        assembly.AppendLine($"store_structured u0.x, r0.x, 0, r3.x");
        assembly.AppendLine($"ret");
        
        return assembly.ToString();
    }
}

/// <summary>
/// Information about a compiled kernel.
/// </summary>
public class CompiledKernelInfo
{
    public string Name { get; set; } = "";
    public string EntryPoint { get; set; } = "";
    public string Assembly { get; set; } = "";
    public DateTime CompiledAt { get; set; }
    public CompilationOptions Options { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Compilation diagnostic information.
/// </summary>
public class CompilationDiagnostic
{
    public DiagnosticLevel Level { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Diagnostic severity level.
/// </summary>
public enum DiagnosticLevel
{
    Info,
    Warning,
    Error
}