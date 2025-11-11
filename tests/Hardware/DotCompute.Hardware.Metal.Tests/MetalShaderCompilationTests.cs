// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Hardware tests for Metal shader compilation functionality.
/// These tests validate the complete compilation pipeline from MSL source to compute pipeline state.
/// </summary>
public class MetalShaderCompilationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalAccelerator> _logger;
    private readonly MetalAccelerator? _accelerator;
    private readonly bool _isMetalSupported;

    public MetalShaderCompilationTests(ITestOutputHelper output)
    {
        _output = output;

        // Check if Metal is supported on this system
        _isMetalSupported = MetalNative.IsMetalSupported();

        if (!_isMetalSupported)
        {
            _output.WriteLine("Metal is not supported on this system. Tests will be skipped.");
            return;
        }

        // Setup logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<MetalAccelerator>();

        try
        {
            // Create Metal accelerator
            var options = Options.Create(new MetalAcceleratorOptions());
            _accelerator = new MetalAccelerator(options, _logger);
            _output.WriteLine($"Metal accelerator initialized: {_accelerator.Info.Name}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to initialize Metal accelerator: {ex.Message}");
            _isMetalSupported = false;
        }
    }

    [Fact]
    public void Test_MetalIsSupported()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");

        Assert.True(_isMetalSupported);
        _output.WriteLine("Metal is supported on this system");
    }

    [Fact]
    public async Task Test_CompileSimpleVectorAddKernel()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        // Define a simple vector addition kernel in Metal Shading Language
        const string metalCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void vector_add(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint id [[thread_position_in_grid]])
{
    result[id] = a[id] + b[id];
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "vector_add",
            Code = metalCode,
            Language = KernelLanguage.Metal,
            EntryPoint = "vector_add"
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            FastMath = true
        };

        // Compile the kernel
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDef, options);

        Assert.NotNull(compiledKernel);
        Assert.Equal("vector_add", compiledKernel.Name);
        _output.WriteLine($"Successfully compiled kernel: {compiledKernel.Name}");
    }

    [Fact]
    public async Task Test_CompileKernelWithFastMath()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        const string metalCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void fast_math_test(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{
    output[id] = sqrt(input[id]) + sin(input[id]) * cos(input[id]);
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "fast_math_test",
            Code = metalCode,
            Language = KernelLanguage.Metal,
            EntryPoint = "fast_math_test"
        };

        var optionsWithFastMath = new CompilationOptions { FastMath = true };
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDef, optionsWithFastMath);

        Assert.NotNull(compiledKernel);
        _output.WriteLine("Successfully compiled kernel with fast math enabled");
    }

    [Fact]
    public async Task Test_CompileKernelCaching()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        const string metalCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void cached_kernel(
    device float* data [[buffer(0)]],
    uint id [[thread_position_in_grid]])
{
    data[id] = data[id] * 2.0;
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "cached_kernel",
            Code = metalCode,
            Language = KernelLanguage.Metal,
            EntryPoint = "cached_kernel"
        };

        // First compilation
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var compiled1 = await _accelerator.CompileKernelAsync(kernelDef);
        sw1.Stop();

        // Second compilation (should hit cache)
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var compiled2 = await _accelerator.CompileKernelAsync(kernelDef);
        sw2.Stop();

        Assert.NotNull(compiled1);
        Assert.NotNull(compiled2);

        _output.WriteLine($"First compilation: {sw1.ElapsedMilliseconds}ms");
        _output.WriteLine($"Second compilation (cached): {sw2.ElapsedMilliseconds}ms");

        // Cache hit should be significantly faster (typically sub-millisecond)
        Assert.True(sw2.ElapsedMilliseconds < sw1.ElapsedMilliseconds / 2,
            "Cached compilation should be at least 2x faster");
    }

    [Fact]
    public async Task Test_CompileKernelWithDifferentOptimizationLevels()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        const string metalCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void optimization_test(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{
    float x = input[id];
    for (int i = 0; i < 10; i++) {
        x = x * x + 1.0;
    }
    output[id] = x;
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "optimization_test",
            Code = metalCode,
            Language = KernelLanguage.Metal,
            EntryPoint = "optimization_test"
        };

        // Test different optimization levels
        var levels = new[]
        {
            OptimizationLevel.None,
            OptimizationLevel.Default,
            OptimizationLevel.O3
        };

        foreach (var level in levels)
        {
            var options = new CompilationOptions { OptimizationLevel = level };
            var compiled = await _accelerator.CompileKernelAsync(kernelDef, options);

            Assert.NotNull(compiled);
            _output.WriteLine($"Compiled with optimization level {level}");
        }
    }

    [Fact]
    public async Task Test_CompileKernelWithInvalidCode_ShouldFail()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        const string invalidMetalCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void invalid_kernel(
    device float* data [[buffer(0)]]
    // Missing comma and closing parameter
{
    INVALID_SYNTAX_HERE
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "invalid_kernel",
            Code = invalidMetalCode,
            Language = KernelLanguage.Metal,
            EntryPoint = "invalid_kernel"
        };

        // Should throw exception due to compilation error
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _accelerator.CompileKernelAsync(kernelDef);
        });

        _output.WriteLine("Invalid code correctly rejected by compiler");
    }

    [Fact]
    public async Task Test_CompileKernelWithMissingHeaders()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        // Code without Metal headers (compiler should add them)
        const string metalCodeWithoutHeaders = @"
kernel void no_headers_kernel(
    device float* data [[buffer(0)]],
    uint id [[thread_position_in_grid]])
{
    data[id] = data[id] * 2.0;
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "no_headers_kernel",
            Code = metalCodeWithoutHeaders,
            Language = KernelLanguage.Metal,
            EntryPoint = "no_headers_kernel"
        };

        // Should compile successfully with auto-added headers
        var compiled = await _accelerator.CompileKernelAsync(kernelDef);
        Assert.NotNull(compiled);
        _output.WriteLine("Kernel compiled successfully with auto-added headers");
    }

    [Fact]
    public async Task Test_CompileComplexKernelWithThreadgroups()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        const string metalCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void parallel_reduction(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    threadgroup float* shared_data [[threadgroup(0)]],
    uint tid [[thread_position_in_threadgroup]],
    uint gid [[threadgroup_position_in_grid]],
    uint threads_per_group [[threads_per_threadgroup]])
{
    // Load data into shared memory
    shared_data[tid] = input[gid * threads_per_group + tid];
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Parallel reduction
    for (uint stride = threads_per_group / 2; stride > 0; stride >>= 1) {
        if (tid < stride) {
            shared_data[tid] += shared_data[tid + stride];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    // Write result
    if (tid == 0) {
        output[gid] = shared_data[0];
    }
}
";

        var kernelDef = new KernelDefinition
        {
            Name = "parallel_reduction",
            Code = metalCode,
            Language = KernelLanguage.Metal,
            EntryPoint = "parallel_reduction"
        };

        var compiled = await _accelerator.CompileKernelAsync(kernelDef);
        Assert.NotNull(compiled);
        _output.WriteLine("Complex threadgroup kernel compiled successfully");
    }

    [Fact]
    public void Test_GetDeviceCapabilities()
    {
        Skip.IfNot(_isMetalSupported, "Metal is not supported on this system");
        Assert.NotNull(_accelerator);

        var info = _accelerator.Info;

        Assert.NotNull(info);
        Assert.NotNull(info.Name);
        Assert.NotNull(info.Capabilities);

        _output.WriteLine($"Device: {info.Name}");
        _output.WriteLine($"Compute Capability: {info.ComputeCapability}");
        _output.WriteLine($"Memory Size: {info.MemorySize / (1024.0 * 1024.0):F2} MB");
        _output.WriteLine($"Compute Units: {info.ComputeUnits}");
        _output.WriteLine($"Unified Memory: {info.IsUnifiedMemory}");

        foreach (var cap in info.Capabilities)
        {
            _output.WriteLine($"  {cap.Key}: {cap.Value}");
        }
    }

    public void Dispose()
    {
        if (_accelerator != null)
        {
            _accelerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        GC.SuppressFinalize(this);
    }
}

// Helper class for Xunit output logging
internal sealed class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XunitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return new NoOpDisposable();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
                if (exception != null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
            catch
            {
                // Ignore errors during logging
            }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
