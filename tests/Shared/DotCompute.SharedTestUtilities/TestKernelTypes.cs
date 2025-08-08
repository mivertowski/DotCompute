// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions;

namespace DotCompute.SharedTestUtilities;

/// <summary>
/// Test kernel configuration for unit tests.
/// </summary>
public class KernelConfiguration
{
    public string? Name { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int MaxThreadsPerBlock { get; set; } = 256;
    public int SharedMemorySize { get; set; }
}

/// <summary>
/// Test kernel argument for unit tests.
/// </summary>
public class KernelArgument
{
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public Type Type { get; set; } = typeof(object);
    public bool IsOutput { get; set; }
}

/// <summary>
/// Test compiled kernel for unit tests.
/// </summary>
public class CompiledKernel
{
    public string Name { get; set; } = string.Empty;
    public byte[]? ByteCode { get; set; }
    public KernelConfiguration Configuration { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Kernel language enumeration for tests.
/// </summary>
public enum KernelLanguage
{
    CUDA,
    OpenCL,
    HLSL,
    Metal,
    SPIRV
}

/// <summary>
/// Memory test utilities.
/// </summary>
public static class MemoryTestUtilities
{
    public static byte[] CreateTestData(int size)
    {
        var data = new byte[size];
        var random = new Random(42);
        random.NextBytes(data);
        return data;
    }

    public static float[] CreateFloatTestData(int count)
    {
        var data = new float[count];
        var random = new Random(42);
        for (int i = 0; i < count; i++)
        {
            data[i] = (float)random.NextDouble();
        }
        return data;
    }
}

/// <summary>
/// Edge case test utilities.
/// </summary>
public static class EdgeCaseUtilities
{
    public static IEnumerable<object[]> GetMemorySizes()
    {
        yield return new object[] { 0 };
        yield return new object[] { 1 };
        yield return new object[] { 1024 };
        yield return new object[] { 1024 * 1024 };
        yield return new object[] { int.MaxValue };
    }

    public static IEnumerable<object[]> GetThreadCounts()
    {
        yield return new object[] { 1 };
        yield return new object[] { 32 };
        yield return new object[] { 256 };
        yield return new object[] { 1024 };
    }
}

/// <summary>
/// Test kernel types for mocking.
/// </summary>
public static class TestKernelTypes
{
    public static KernelDefinition CreateSimpleKernel(string name = "TestKernel")
    {
        return new KernelDefinition
        {
            Name = name,
            Source = @"
                __global__ void TestKernel(float* input, float* output, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        output[idx] = input[idx] * 2.0f;
                    }
                }",
            Language = "CUDA"
        };
    }

    public static KernelArguments CreateTestArguments()
    {
        var args = new KernelArguments();
        args.Set("input", new float[] { 1.0f, 2.0f, 3.0f });
        args.Set("output", new float[3]);
        args.Set("n", 3);
        return args;
    }
}