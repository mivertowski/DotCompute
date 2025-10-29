// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.OpenCL.Compilation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.OpenCL.Tests.Compilation;

/// <summary>
/// Unit tests for the CSharpToOpenCLTranslator.
/// </summary>
public sealed class CSharpToOpenCLTranslatorTests
{
    private readonly ILogger<CSharpToOpenCLTranslator> _logger;
    private readonly CSharpToOpenCLTranslator _translator;

    public CSharpToOpenCLTranslatorTests()
    {
        _logger = NullLogger<CSharpToOpenCLTranslator>.Instance;
        _translator = new CSharpToOpenCLTranslator(_logger);
    }

    [Fact]
    public async Task TranslateAsync_SimpleVectorAdd_GeneratesCorrectOpenCL()
    {
        // Arrange
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

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.NotNull(openclCode);
        Assert.Contains("__kernel void VectorAdd", openclCode);
        Assert.Contains("__global float* a", openclCode);
        Assert.Contains("__global const float* b", openclCode);
        Assert.Contains("__global float* result", openclCode);
        Assert.Contains("get_global_id(0)", openclCode);
        Assert.Contains("result[idx] = a[idx] + b[idx]", openclCode);
    }

    [Fact]
    public async Task TranslateAsync_ThreadIdTranslation_ConvertsToGetGlobalId()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "TestKernel",
            EntryPoint = "TestKernel",
            Source = @"
public static void TestKernel(Span<int> data)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;
    int z = Kernel.ThreadId.Z;
    data[x] = x + y + z;
}",
            Code = "TestKernel",
            Metadata = new Dictionary<string, object>
            {
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "data",
                        ["Type"] = "Span<int>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    }
                }
            }
        };

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.Contains("int x = get_global_id(0)", openclCode);
        Assert.Contains("int y = get_global_id(1)", openclCode);
        Assert.Contains("int z = get_global_id(2)", openclCode);
    }

    [Fact]
    public async Task TranslateAsync_MathFunctions_ConvertsToOpenCLBuiltins()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "MathKernel",
            EntryPoint = "MathKernel",
            Source = @"
public static void MathKernel(Span<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    float val = input[idx];
    output[idx] = Math.Sqrt(val) + Math.Sin(val) + Math.Cos(val);
}",
            Code = "MathKernel",
            Metadata = new Dictionary<string, object>
            {
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

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.Contains("sqrt(val)", openclCode);
        Assert.Contains("sin(val)", openclCode);
        Assert.Contains("cos(val)", openclCode);
        Assert.DoesNotContain("Math.", openclCode);
    }

    [Fact]
    public async Task TranslateAsync_TypeMapping_HandlesAllPrimitiveTypes()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "TypeTestKernel",
            EntryPoint = "TypeTestKernel",
            Source = @"
public static void TypeTestKernel(
    Span<float> floats,
    Span<double> doubles,
    Span<int> ints,
    Span<uint> uints)
{
    int idx = Kernel.ThreadId.X;
    floats[idx] = 1.0f;
    doubles[idx] = 2.0;
    ints[idx] = 3;
    uints[idx] = 4;
}",
            Code = "TypeTestKernel",
            Metadata = new Dictionary<string, object>
            {
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "floats",
                        ["Type"] = "Span<float>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "doubles",
                        ["Type"] = "Span<double>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "ints",
                        ["Type"] = "Span<int>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    },
                    new Dictionary<string, object>
                    {
                        ["Name"] = "uints",
                        ["Type"] = "Span<uint>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    }
                }
            }
        };

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.Contains("__global float* floats", openclCode);
        Assert.Contains("__global double* doubles", openclCode);
        Assert.Contains("__global int* ints", openclCode);
        Assert.Contains("__global uint* uints", openclCode);
    }

    [Fact]
    public async Task TranslateAsync_ReadOnlySpan_GeneratesConstPointer()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "ReadOnlyKernel",
            EntryPoint = "ReadOnlyKernel",
            Source = @"
public static void ReadOnlyKernel(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    output[idx] = input[idx] * 2.0f;
}",
            Code = "ReadOnlyKernel",
            Metadata = new Dictionary<string, object>
            {
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "input",
                        ["Type"] = "ReadOnlySpan<float>",
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

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.Contains("__global const float* input", openclCode);
        Assert.Contains("__global float* output", openclCode);
    }

    [Fact]
    public async Task TranslateAsync_NullDefinition_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _translator.TranslateAsync(null!));
    }

    [Fact]
    public async Task TranslateAsync_EmptySource_ThrowsArgumentException()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "EmptyKernel",
            EntryPoint = "EmptyKernel",
            Source = "",
            Code = "EmptyKernel"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _translator.TranslateAsync(definition));
    }

    [Fact]
    public async Task TranslateAsync_ComplexKernel_GeneratesValidOpenCL()
    {
        // Arrange
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

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.NotNull(openclCode);
        Assert.Contains("__kernel void MatrixMultiply", openclCode);
        Assert.Contains("__global const float* a", openclCode);
        Assert.Contains("__global const float* b", openclCode);
        Assert.Contains("__global float* result", openclCode);
        Assert.Contains("int width", openclCode);
        Assert.Contains("get_global_id(1)", openclCode); // row
        Assert.Contains("get_global_id(0)", openclCode); // col
    }

    [Fact]
    public async Task TranslateAsync_GroupIdTranslation_ConvertsToGetGroupId()
    {
        // Arrange
        var definition = new KernelDefinition
        {
            Name = "GroupIdKernel",
            EntryPoint = "GroupIdKernel",
            Source = @"
public static void GroupIdKernel(Span<int> data)
{
    int groupX = Kernel.GroupId.X;
    int groupY = Kernel.GroupId.Y;
    int localX = Kernel.LocalThreadId.X;
    data[groupX] = groupY + localX;
}",
            Code = "GroupIdKernel",
            Metadata = new Dictionary<string, object>
            {
                ["Parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Name"] = "data",
                        ["Type"] = "Span<int>",
                        ["IsInput"] = false,
                        ["IsOutput"] = true
                    }
                }
            }
        };

        // Act
        var openclCode = await _translator.TranslateAsync(definition);

        // Assert
        Assert.Contains("int groupX = get_group_id(0)", openclCode);
        Assert.Contains("int groupY = get_group_id(1)", openclCode);
        Assert.Contains("int localX = get_local_id(0)", openclCode);
    }
}
