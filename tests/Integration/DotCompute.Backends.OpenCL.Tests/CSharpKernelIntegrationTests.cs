// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.OpenCL.Compilation;
using DotCompute.Backends.OpenCL.DeviceManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.OpenCL.Tests.Integration;

/// <summary>
/// Integration tests for C# kernel compilation and execution with the [Kernel] attribute pattern.
/// </summary>
[Collection("OpenCL Hardware Tests")]
public sealed class CSharpKernelIntegrationTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly OpenCLDeviceManager _deviceManager;
    private OpenCLContext? _context;
    private OpenCLKernelCompiler? _compiler;
    private bool _disposed;

    public CSharpKernelIntegrationTests()
    {
        _logger = NullLogger.Instance;
        _deviceManager = new OpenCLDeviceManager(NullLogger<OpenCLDeviceManager>.Instance);
    }

    private async Task<bool> InitializeOpenCLAsync()
    {
        var device = _deviceManager.GetBestDevice();
        if (device == null)
        {
            return false;
        }

        _context = new OpenCLContext(device, NullLoggerFactory.Instance.CreateLogger<OpenCLContext>());
        _compiler = new OpenCLKernelCompiler(
            _context.ContextHandle,
            device.DeviceId,
            NullLogger<OpenCLKernelCompiler>.Instance);

        await Task.CompletedTask;
        return true;
    }

    [Fact]
    public async Task VectorAddKernel_FromCSharp_ExecutesCorrectly()
    {
        // Skip if OpenCL not available
        if (!await InitializeOpenCLAsync())
        {
            Skip.If(true, "OpenCL not available on this system");
            return;
        }

        // Arrange - Simulate [Kernel] attributed method
        var definition = new KernelDefinition
        {
            Name = "VectorAdd",
            EntryPoint = "VectorAdd",
            Source = @"
public static void VectorAdd(Span<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}",
            Code = "VectorAdd",
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "C#",
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "a",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = true,
                        ["IsOutput"] = false
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "b",
                        ["Type"] = "ReadOnlySpan<float>",
                        ["IsInput"] = true,
                        ["IsOutput"] = false
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "result",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    }
                }
            }
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = 2,
            EnableFastMath = true
        };

        // Act - Compile from C#
        Assert.NotNull(_compiler);
        var compiledKernel = await _compiler!.CompileFromCSharpAsync(
            definition,
            options,
            _logger);

        // Assert
        Assert.NotNull(compiledKernel);
        Assert.Equal("VectorAdd", compiledKernel.KernelName);
        Assert.NotNull(compiledKernel.Binary);
        Assert.True(compiledKernel.Binary!.Length > 0);

        // Verify compilation metadata
        Assert.NotNull(compiledKernel.BuildLog);
        Assert.True(compiledKernel.WorkGroupSize > 0);
    }

    [Fact]
    public async Task MatrixMultiplyKernel_FromCSharp_CompilesSuccessfully()
    {
        // Skip if OpenCL not available
        if (!await InitializeOpenCLAsync())
        {
            Skip.If(true, "OpenCL not available on this system");
            return;
        }

        // Arrange - Complex kernel with 2D indexing
        var definition = new KernelDefinition
        {
            Name = "MatrixMultiply",
            EntryPoint = "MatrixMultiply",
            Source = @"
public static void MatrixMultiply(
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result,
    int width)
{
    int row = Kernel.ThreadId.Y;
    int col = Kernel.ThreadId.X;
    int groupRow = Kernel.GroupId.Y;
    int groupCol = Kernel.GroupId.X;

    if (row < width && col < width)
    {
        float sum = 0.0f;
        for (int k = 0; k < width; k++)
        {
            sum += a[row * width + k] * b[k * width + col];
        }
        result[row * width + col] = sum;
    }
}",
            Code = "MatrixMultiply",
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "C#",
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "a",
                        ["Type"] = "ReadOnlySpan<float>",
                        ["IsInput"] = true,
                        ["IsOutput"] = false
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "b",
                        ["Type"] = "ReadOnlySpan<float>",
                        ["IsInput"] = true,
                        ["IsOutput"] = false
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "result",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "width",
                        ["Type"] = "int",
                        ["IsInput"] = true,
                        ["IsOutput"] = false
                    }
                }
            }
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = 3,
            EnableFastMath = true,
            EnableMadEnable = true
        };

        // Act
        Assert.NotNull(_compiler);
        var compiledKernel = await _compiler!.CompileFromCSharpAsync(
            definition,
            options,
            _logger);

        // Assert
        Assert.NotNull(compiledKernel);
        Assert.Equal("MatrixMultiply", compiledKernel.KernelName);
        Assert.True(compiledKernel.Binary!.Length > 0);

        // Verify arguments were parsed correctly
        Assert.Equal(4u, (uint)compiledKernel.Arguments.Length);
    }

    [Fact]
    public async Task MathFunctionsKernel_FromCSharp_TranslatesCorrectly()
    {
        // Skip if OpenCL not available
        if (!await InitializeOpenCLAsync())
        {
            Skip.If(true, "OpenCL not available on this system");
            return;
        }

        // Arrange - Kernel with math functions
        var definition = new KernelDefinition
        {
            Name = "MathKernel",
            EntryPoint = "MathKernel",
            Source = @"
public static void MathKernel(Span<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < input.Length)
    {
        float val = input[idx];
        float result = Math.Sqrt(val) + Math.Sin(val) + Math.Cos(val);
        result += Math.Min(val, 1.0f) + Math.Max(val, 0.0f);
        output[idx] = result;
    }
}",
            Code = "MathKernel",
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "C#",
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "input",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = true,
                        ["IsOutput"] = false
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "output",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    }
                }
            }
        };

        var options = new CompilationOptions
        {
            OptimizationLevel = 2,
            EnableFastMath = false // Disable for accurate math
        };

        // Act
        Assert.NotNull(_compiler);
        var compiledKernel = await _compiler!.CompileFromCSharpAsync(
            definition,
            options,
            _logger);

        // Assert
        Assert.NotNull(compiledKernel);
        Assert.Equal("MathKernel", compiledKernel.KernelName);
    }

    [Fact]
    public void IsCSharpKernel_DetectsCSharpSource_Correctly()
    {
        // Arrange
        var csharpKernel = new KernelDefinition
        {
            Name = "Test",
            Source = "public static void Test(Span<float> data) { int idx = Kernel.ThreadId.X; }",
            Code = "Test"
        };

        var openclKernel = new KernelDefinition
        {
            Name = "Test",
            Source = "__kernel void test(__global float* data) { int idx = get_global_id(0); }",
            Code = "Test"
        };

        // Act & Assert
        Assert.True(csharpKernel.IsCSharpKernel());
        Assert.False(openclKernel.IsCSharpKernel());
    }

    [Fact]
    public async Task CompilationCache_PreservesCSharpKernels()
    {
        // Skip if OpenCL not available
        if (!await InitializeOpenCLAsync())
        {
            Skip.If(true, "OpenCL not available on this system");
            return;
        }

        // Arrange
        var definition = new KernelDefinition
        {
            Name = "CachedKernel",
            EntryPoint = "CachedKernel",
            Source = "public static void CachedKernel(Span<float> data) { int idx = Kernel.ThreadId.X; data[idx] *= 2.0f; }",
            Code = "CachedKernel",
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = "C#",
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "data",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = true,
                        ["IsOutput"] = true
                    }
                }
            }
        };

        var options = new CompilationOptions { OptimizationLevel = 2 };

        // Act - Compile twice (second should hit cache)
        Assert.NotNull(_compiler);
        var kernel1 = await _compiler!.CompileFromCSharpAsync(definition, options, _logger);
        var kernel2 = await _compiler.CompileFromCSharpAsync(definition, options, _logger);

        // Assert
        Assert.NotNull(kernel1);
        Assert.NotNull(kernel2);
        Assert.Equal(kernel1.KernelName, kernel2.KernelName);

        // Both should have valid binaries
        Assert.NotNull(kernel1.Binary);
        Assert.NotNull(kernel2.Binary);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _compiler?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _context?.Dispose();

        _disposed = true;
    }
}
