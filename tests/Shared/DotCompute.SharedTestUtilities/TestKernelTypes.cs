// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Shared;
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
/// Kernel factory methods for creating production types in tests.
/// </summary>
public static class KernelFactory
{
    /// <summary>
    /// Creates a simple CUDA kernel definition for testing.
    /// </summary>
    public static KernelDefinition CreateSimpleKernel(string name = "TestKernel")
    {
        var code = @"
                __global__ void TestKernel(float* input, float* output, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        output[idx] = input[idx] * 2.0f;
                    }
                }";
        
        var kernelSource = new TextKernelSource(
            code: code,
            name: name,
            language: Abstractions.KernelLanguage.Cuda,
            entryPoint: "TestKernel");
        
        return new KernelDefinition(
            name, 
            kernelSource, 
            new CompilationOptions());
    }

    /// <summary>
    /// Creates test kernel arguments.
    /// </summary>
    public static KernelArguments CreateTestArguments()
    {
        var args = KernelArguments.Create(3);
        args.Set(0, new float[] { 1.0f, 2.0f, 3.0f });
        args.Set(1, new float[3]);
        args.Set(2, 3);
        return args;
    }

    /// <summary>
    /// Creates a compiled kernel for testing.
    /// </summary>
    public static CompiledKernel CreateCompiledKernel(
        Guid? id = null,
        IntPtr nativeHandle = default,
        int sharedMemorySize = 0)
    {
        var kernelId = id ?? Guid.NewGuid();
        var configuration = new KernelConfiguration(
            new Dim3(1), // Grid dimensions
            new Dim3(256) // Block dimensions
        );
        
        return new CompiledKernel(
            kernelId,
            nativeHandle,
            sharedMemorySize,
            configuration);
    }
}
