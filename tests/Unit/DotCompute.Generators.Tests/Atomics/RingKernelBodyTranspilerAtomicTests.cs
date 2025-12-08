// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel.Analysis;
using FluentAssertions;
using Xunit;

namespace DotCompute.Generators.Tests.Atomics;

/// <summary>
/// Unit tests for atomic operation transpilation in <see cref="RingKernelBodyTranspiler"/>.
/// </summary>
public class RingKernelBodyTranspilerAtomicTests
{
    #region Context Atomic Operations (ctx.AtomicXxx)

    [Fact]
    public void Transpile_CtxAtomicAdd_TranslatesToCudaAtomicAdd()
    {
        // Arrange
        var code = "ctx.AtomicAdd(ref counter, 1);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicAdd(&counter, 1)");
    }

    [Fact]
    public void Transpile_CtxAtomicSub_TranslatesToCudaAtomicSub()
    {
        // Arrange
        var code = "ctx.AtomicSub(ref counter, 1);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicSub(&counter, 1)");
    }

    [Fact]
    public void Transpile_CtxAtomicCAS_TranslatesToCudaAtomicCAS()
    {
        // Arrange
        var code = "ctx.AtomicCAS(ref value, expected, desired);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicCAS(&value, expected, desired)");
    }

    [Fact]
    public void Transpile_CtxAtomicExch_TranslatesToCudaAtomicExch()
    {
        // Arrange
        var code = "ctx.AtomicExch(ref value, newValue);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicExch(&value, newValue)");
    }

    [Fact]
    public void Transpile_CtxAtomicMin_TranslatesToCudaAtomicMin()
    {
        // Arrange
        var code = "ctx.AtomicMin(ref minimum, value);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicMin(&minimum, value)");
    }

    [Fact]
    public void Transpile_CtxAtomicMax_TranslatesToCudaAtomicMax()
    {
        // Arrange
        var code = "ctx.AtomicMax(ref maximum, value);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicMax(&maximum, value)");
    }

    [Fact]
    public void Transpile_CtxAtomicAnd_TranslatesToCudaAtomicAnd()
    {
        // Arrange
        var code = "ctx.AtomicAnd(ref flags, mask);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicAnd(&flags, mask)");
    }

    [Fact]
    public void Transpile_CtxAtomicOr_TranslatesToCudaAtomicOr()
    {
        // Arrange
        var code = "ctx.AtomicOr(ref flags, mask);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicOr(&flags, mask)");
    }

    [Fact]
    public void Transpile_CtxAtomicXor_TranslatesToCudaAtomicXor()
    {
        // Arrange
        var code = "ctx.AtomicXor(ref flags, mask);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicXor(&flags, mask)");
    }

    #endregion

    #region Static AtomicOps Class Methods

    [Fact]
    public void Transpile_AtomicOpsAdd_TranslatesToCudaAtomicAdd()
    {
        // Arrange
        var code = "AtomicOps.Add(ref counter, 1);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicAdd(&counter, 1)");
    }

    [Fact]
    public void Transpile_AtomicOpsSub_TranslatesToCudaAtomicSub()
    {
        // Arrange
        var code = "AtomicOps.Sub(ref counter, 1);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicSub(&counter, 1)");
    }

    [Fact]
    public void Transpile_AtomicOpsExchange_TranslatesToCudaAtomicExch()
    {
        // Arrange
        var code = "AtomicOps.Exchange(ref value, newValue);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicExch(&value, newValue)");
    }

    [Fact]
    public void Transpile_AtomicOpsCompareExchange_TranslatesToCudaAtomicCAS()
    {
        // Arrange
        var code = "AtomicOps.CompareExchange(ref value, expected, desired);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicCAS(&value, expected, desired)");
    }

    [Fact]
    public void Transpile_AtomicOpsMin_TranslatesToCudaAtomicMin()
    {
        // Arrange
        var code = "AtomicOps.Min(ref minimum, value);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicMin(&minimum, value)");
    }

    [Fact]
    public void Transpile_AtomicOpsMax_TranslatesToCudaAtomicMax()
    {
        // Arrange
        var code = "AtomicOps.Max(ref maximum, value);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicMax(&maximum, value)");
    }

    [Fact]
    public void Transpile_AtomicOpsAnd_TranslatesToCudaAtomicAnd()
    {
        // Arrange
        var code = "AtomicOps.And(ref flags, mask);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicAnd(&flags, mask)");
    }

    [Fact]
    public void Transpile_AtomicOpsOr_TranslatesToCudaAtomicOr()
    {
        // Arrange
        var code = "AtomicOps.Or(ref flags, mask);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicOr(&flags, mask)");
    }

    [Fact]
    public void Transpile_AtomicOpsXor_TranslatesToCudaAtomicXor()
    {
        // Arrange
        var code = "AtomicOps.Xor(ref flags, mask);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicXor(&flags, mask)");
    }

    #endregion

    #region Memory Ordering

    [Fact]
    public void Transpile_AtomicOpsLoad_TranslatesToCudaAtomicLoad()
    {
        // Arrange
        var code = "AtomicOps.Load(ref value, MemoryOrder.Acquire);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__atomic_load_n(&value, __ATOMIC_ACQUIRE)");
    }

    [Fact]
    public void Transpile_AtomicOpsStore_TranslatesToCudaAtomicStore()
    {
        // Arrange
        var code = "AtomicOps.Store(ref value, 42, MemoryOrder.Release);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__atomic_store_n(&value, 42, __ATOMIC_RELEASE)");
    }

    [Theory]
    [InlineData("MemoryOrder.Relaxed", "__ATOMIC_RELAXED")]
    [InlineData("MemoryOrder.Acquire", "__ATOMIC_ACQUIRE")]
    [InlineData("MemoryOrder.Release", "__ATOMIC_RELEASE")]
    [InlineData("MemoryOrder.AcquireRelease", "__ATOMIC_ACQ_REL")]
    [InlineData("MemoryOrder.SequentiallyConsistent", "__ATOMIC_SEQ_CST")]
    public void Transpile_AtomicOpsLoad_WithDifferentMemoryOrders(string memoryOrder, string cudaOrder)
    {
        // Arrange
        var code = $"AtomicOps.Load(ref value, {memoryOrder});";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain($"__atomic_load_n(&value, {cudaOrder})");
    }

    #endregion

    #region Memory Fences

    [Fact]
    public void Transpile_AtomicOpsThreadFence_Workgroup_TranslatesToThreadFenceBlock()
    {
        // Arrange
        var code = "AtomicOps.ThreadFence(MemoryScope.Workgroup);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence_block()");
    }

    [Fact]
    public void Transpile_AtomicOpsThreadFence_Device_TranslatesToThreadFence()
    {
        // Arrange
        var code = "AtomicOps.ThreadFence(MemoryScope.Device);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence()");
    }

    [Fact]
    public void Transpile_AtomicOpsThreadFence_System_TranslatesToThreadFenceSystem()
    {
        // Arrange
        var code = "AtomicOps.ThreadFence(MemoryScope.System);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence_system()");
    }

    [Fact]
    public void Transpile_AtomicOpsMemoryBarrier_TranslatesToThreadFence()
    {
        // Arrange
        var code = "AtomicOps.MemoryBarrier();";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence()");
    }

    #endregion

    #region Context Fences (Existing)

    [Fact]
    public void Transpile_CtxThreadFence_TranslatesToCudaThreadFence()
    {
        // Arrange
        var code = "ctx.ThreadFence();";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence()");
    }

    [Fact]
    public void Transpile_CtxThreadFenceBlock_TranslatesToCudaThreadFenceBlock()
    {
        // Arrange
        var code = "ctx.ThreadFenceBlock();";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence_block()");
    }

    [Fact]
    public void Transpile_CtxThreadFenceSystem_TranslatesToCudaThreadFenceSystem()
    {
        // Arrange
        var code = "ctx.ThreadFenceSystem();";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("__threadfence_system()");
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Transpile_MultipleAtomicOperations_TranslatesAll()
    {
        // Arrange
        var code = @"
ctx.AtomicAdd(ref sum, value);
AtomicOps.Min(ref minimum, value);
AtomicOps.ThreadFence(MemoryScope.Device);
";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("atomicAdd(&sum, value)");
        result.Should().Contain("atomicMin(&minimum, value)");
        result.Should().Contain("__threadfence()");
    }

    [Fact]
    public void Transpile_AtomicInExpression_TranslatesCorrectly()
    {
        // Arrange
        var code = "var old = ctx.AtomicExch(ref value, newValue);";

        // Act
        var result = RingKernelBodyTranspiler.Transpile(code, null);

        // Assert
        result.Should().Contain("auto old = atomicExch(&value, newValue)");
    }

    #endregion
}
