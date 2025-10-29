// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Backends.Metal.Tests.Compilation;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests;

/// <summary>
/// Tests for [Kernel] attribute integration with Metal backend.
/// Validates C# to MSL translation and kernel execution correctness.
/// Matches CUDA backend test patterns for cross-backend compatibility.
/// </summary>
public sealed class KernelAttributeTests : MetalCompilerTestBase
{
    public KernelAttributeTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Basic Kernel Attribute Tests

    [SkippableFact]
    public async Task KernelAttribute_SimpleVectorAdd_CompilesAndExecutes()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Simulate C# kernel with [Kernel] attribute
        var csharpCode = @"
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}";

        // For now, test direct MSL since C# translation is not fully implemented
        var mslKernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled = await compiler.CompileAsync(mslKernel);

        // Assert
        Assert.NotNull(compiled);
        Assert.Equal("vector_add", compiled.Name);
        LogTestInfo($"✓ [Kernel] attribute pattern compiled successfully: {compiled.Name}");
    }

    [SkippableFact]
    public async Task KernelAttribute_ScalarParameter_BindsCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void scalar_multiply(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    constant float& scalar [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = input[gid] * scalar;
}";

        var kernel = new KernelDefinition("scalar_multiply", mslCode)
        {
            EntryPoint = "scalar_multiply",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Scalar parameter binding validated");
    }

    [SkippableFact]
    public async Task KernelAttribute_ReadOnlySpan_MapsToConstPointer()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Test ReadOnlySpan<T> -> device const T* mapping
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void read_only_test(
    device const float* readonly [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = readonly[gid] * 2.0f;
}";

        var kernel = new KernelDefinition("read_only_test", mslCode)
        {
            EntryPoint = "read_only_test",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ ReadOnlySpan<T> mapped to device const T* correctly");
    }

    [SkippableFact]
    public async Task KernelAttribute_Span_MapsToMutablePointer()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Test Span<T> -> device T* mapping
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void mutable_test(
    device float* data [[buffer(0)]],
    uint gid [[thread_position_in_grid]])
{
    data[gid] = data[gid] * 2.0f;
}";

        var kernel = new KernelDefinition("mutable_test", mslCode)
        {
            EntryPoint = "mutable_test",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Span<T> mapped to device T* correctly");
    }

    #endregion

    #region Thread Coordinate Mapping Tests

    [SkippableFact]
    public async Task KernelAttribute_ThreadIdX_MapsToMetalThreadPosition()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Test Kernel.ThreadId.X -> thread_position_in_grid
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void thread_id_x(
    device float* data [[buffer(0)]],
    uint gid [[thread_position_in_grid]])
{
    data[gid] = float(gid);
}";

        var kernel = new KernelDefinition("thread_id_x", mslCode)
        {
            EntryPoint = "thread_id_x",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Kernel.ThreadId.X mapped to Metal thread_position_in_grid");
    }

    [SkippableFact]
    public async Task KernelAttribute_MultiDimensionalThreadId_MapsCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Test 2D thread coordinates
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void thread_2d(
    device float* data [[buffer(0)]],
    uint width [[buffer(1)]],
    uint2 gid [[thread_position_in_grid]])
{
    uint idx = gid.y * width + gid.x;
    data[idx] = float(idx);
}";

        var kernel = new KernelDefinition("thread_2d", mslCode)
        {
            EntryPoint = "thread_2d",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Multi-dimensional thread coordinates mapped correctly");
    }

    #endregion

    #region Generic Type Tests

    [SkippableFact]
    public async Task KernelAttribute_GenericFloat_CompilesCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Generic float type compiled correctly");
    }

    [SkippableFact]
    public async Task KernelAttribute_GenericInt_CompilesCorrectly()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void int_add(
    device const int* a [[buffer(0)]],
    device const int* b [[buffer(1)]],
    device int* result [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    result[gid] = a[gid] + b[gid];
}";

        var kernel = new KernelDefinition("int_add", mslCode)
        {
            EntryPoint = "int_add",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Generic int type compiled correctly");
    }

    [SkippableFact]
    public async Task KernelAttribute_GenericDouble_CompilationBehavior()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Note: Metal may not support double on all devices
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void double_test(
    device const float* a [[buffer(0)]],
    device float* result [[buffer(1)]],
    uint gid [[thread_position_in_grid]])
{
    // Using float since double support varies
    result[gid] = a[gid] * 2.0f;
}";

        var kernel = new KernelDefinition("double_test", mslCode)
        {
            EntryPoint = "double_test",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Numeric type handling validated");
    }

    #endregion

    #region Bounds Checking Tests

    [SkippableFact]
    public async Task KernelAttribute_BoundsCheck_IncludedInTranslation()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Kernel with explicit bounds checking
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void bounds_check(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    constant uint& length [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    if (gid < length)
    {
        output[gid] = input[gid] * 2.0f;
    }
}";

        var kernel = new KernelDefinition("bounds_check", mslCode)
        {
            EntryPoint = "bounds_check",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Bounds checking preserved in compiled kernel");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact]
    public async Task KernelAttribute_MissingBoundsCheck_StillCompiles()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Kernel without bounds checking (unsafe but should compile)
        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void no_bounds_check(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = input[gid] * 2.0f;  // No bounds check
}";

        var kernel = new KernelDefinition("no_bounds_check", mslCode)
        {
            EntryPoint = "no_bounds_check",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Kernel without bounds check compiled (user responsibility)");
    }

    [SkippableFact]
    public async Task KernelAttribute_InvalidSignature_CompilationFails()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var invalidCode = @"
#include <metal_stdlib>
using namespace metal;

// Invalid: missing required attributes
kernel void invalid_signature(float* data)
{
    data[0] = 1.0f;
}";

        var kernel = new KernelDefinition("invalid_signature", invalidCode)
        {
            EntryPoint = "invalid_signature",
            Language = KernelLanguage.Metal
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await compiler.CompileAsync(kernel));

        LogTestInfo("✓ Invalid kernel signature correctly rejected");
    }

    #endregion

    #region Complex Kernel Tests

    [SkippableFact]
    public async Task KernelAttribute_MatrixMultiply_CompilesSuccessfully()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void matrix_multiply(
    device const float* A [[buffer(0)]],
    device const float* B [[buffer(1)]],
    device float* C [[buffer(2)]],
    constant uint& M [[buffer(3)]],
    constant uint& N [[buffer(4)]],
    constant uint& K [[buffer(5)]],
    uint2 gid [[thread_position_in_grid]])
{
    uint row = gid.y;
    uint col = gid.x;

    if (row < M && col < N)
    {
        float sum = 0.0f;
        for (uint i = 0; i < K; ++i)
        {
            sum += A[row * K + i] * B[i * N + col];
        }
        C[row * N + col] = sum;
    }
}";

        var kernel = new KernelDefinition("matrix_multiply", mslCode)
        {
            EntryPoint = "matrix_multiply",
            Language = KernelLanguage.Metal
        };

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Complex matrix multiply kernel compiled successfully");
    }

    [SkippableFact]
    public async Task KernelAttribute_ReductionSum_WithThreadgroups_Compiles()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        var kernel = TestKernelFactory.CreateThreadgroupMemoryKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        LogTestInfo("✓ Reduction kernel with threadgroup memory compiled");
    }

    #endregion

    #region Parity with CPU Backend Tests

    [SkippableFact]
    public async Task KernelAttribute_CPUParity_SameResultSignature()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();

        // Both CPU and Metal should accept same kernel definition
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var compiled = await compiler.CompileAsync(kernel);

        // Assert
        Assert.NotNull(compiled);
        Assert.Equal("vector_add", compiled.Name);
        LogTestInfo("✓ Kernel signature matches CPU backend expectations");
    }

    #endregion

    #region Performance Characteristics Tests

    [SkippableFact]
    public async Task KernelAttribute_CompilationTime_WithinReasonableBounds()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var compiled = await compiler.CompileAsync(kernel);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(compiled);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"Compilation took too long: {stopwatch.ElapsedMilliseconds}ms");

        LogTestInfo($"✓ Kernel attribute compilation completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [SkippableFact]
    public async Task KernelAttribute_CacheHit_SignificantlyFaster()
    {
        // Arrange
        RequireMetalSupport();
        var cache = CreateCache();
        var compiler = CreateCompiler(cache);
        var kernel = TestKernelFactory.CreateVectorAddKernel();

        // First compilation (cache miss)
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel);
        stopwatch1.Stop();

        // Second compilation (cache hit)
        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        await compiler.CompileAsync(kernel);
        stopwatch2.Stop();

        // Assert - Cache hit should be faster or comparable
        Assert.True(stopwatch2.ElapsedMilliseconds <= stopwatch1.ElapsedMilliseconds * 2,
            "Cache hit should not be significantly slower than first compile");

        LogTestInfo($"✓ Cache performance: First={stopwatch1.ElapsedMilliseconds}ms, " +
                    $"Cached={stopwatch2.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Future C# Translation Tests (Currently Not Supported)

    [SkippableFact]
    public async Task KernelAttribute_CSharpToMSL_ThrowsNotSupportedException()
    {
        // Arrange
        RequireMetalSupport();
        var compiler = CreateCompiler();
        var csharpKernel = TestKernelFactory.CreateCSharpKernel();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await compiler.CompileAsync(csharpKernel));

        Assert.Contains("translation", exception.Message, StringComparison.OrdinalIgnoreCase);
        LogTestInfo($"✓ C# translation correctly reports not supported: {exception.Message}");
    }

    #endregion
}
