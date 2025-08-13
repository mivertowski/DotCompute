// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive unit tests for the IKernel interface and related types.
/// Note: Since IKernel uses static abstract members, we test with concrete implementations.
/// </summary>
public class IKernelTests
{
    #region Test Kernel Implementations

    /// <summary>
    /// Test implementation of IKernel for testing purposes.
    /// </summary>
    private struct TestKernel : IKernel
    {
        public static string Name => "TestKernel";
        public static string Source => "__global__ void test() { }";
        public static string EntryPoint => "test";
        public static int RequiredSharedMemory => 1024;
    }

    /// <summary>
    /// Another test implementation with different values.
    /// </summary>
    private struct VectorAddKernel : IKernel
    {
        public static string Name => "VectorAdd";
        public static string Source => "__global__ void vectorAdd(float* a, float* b, float* c) { int i = threadIdx.x; c[i] = a[i] + b[i]; }";
        public static string EntryPoint => "vectorAdd";
        public static int RequiredSharedMemory => 0;
    }

    /// <summary>
    /// Test implementation with large shared memory requirement.
    /// </summary>
    private struct SharedMemoryKernel : IKernel
    {
        public static string Name => "SharedMemoryKernel";
        public static string Source => "__global__ void sharedMemTest() { __shared__ float data[1024]; }";
        public static string EntryPoint => "sharedMemTest";
        public static int RequiredSharedMemory => 4096;
    }

    /// <summary>
    /// Test implementation with minimal requirements.
    /// </summary>
    private struct MinimalKernel : IKernel
    {
        public static string Name => "Minimal";
        public static string Source => "void main() {}";
        public static string EntryPoint => "main";
        public static int RequiredSharedMemory => 0;
    }

    #endregion

    #region IKernel Static Abstract Members Tests

    [Fact]
    public void TestKernel_StaticProperties_ShouldReturnCorrectValues()
    {
        // Act & Assert
        TestKernel.Name.Should().Be("TestKernel");
        TestKernel.Source.Should().Be("__global__ void test() { }");
        TestKernel.EntryPoint.Should().Be("test");
        TestKernel.RequiredSharedMemory.Should().Be(1024);
    }

    [Fact]
    public void VectorAddKernel_StaticProperties_ShouldReturnCorrectValues()
    {
        // Act & Assert
        VectorAddKernel.Name.Should().Be("VectorAdd");
        VectorAddKernel.Source.Should().Contain("vectorAdd");
        VectorAddKernel.Source.Should().Contain("float* a, float* b, float* c");
        VectorAddKernel.EntryPoint.Should().Be("vectorAdd");
        VectorAddKernel.RequiredSharedMemory.Should().Be(0);
    }

    [Fact]
    public void SharedMemoryKernel_StaticProperties_ShouldReturnCorrectValues()
    {
        // Act & Assert
        SharedMemoryKernel.Name.Should().Be("SharedMemoryKernel");
        SharedMemoryKernel.Source.Should().Contain("__shared__");
        SharedMemoryKernel.EntryPoint.Should().Be("sharedMemTest");
        SharedMemoryKernel.RequiredSharedMemory.Should().Be(4096);
    }

    [Fact]
    public void MinimalKernel_StaticProperties_ShouldReturnCorrectValues()
    {
        // Act & Assert
        MinimalKernel.Name.Should().Be("Minimal");
        MinimalKernel.Source.Should().Be("void main() {}");
        MinimalKernel.EntryPoint.Should().Be("main");
        MinimalKernel.RequiredSharedMemory.Should().Be(0);
    }

    #endregion

    #region IKernel Property Validation Tests

    [Fact]
    public void IKernel_Name_ShouldNotBeNullOrEmpty()
    {
        // Act & Assert
        TestKernel.Name.Should().NotBeNullOrEmpty();
        VectorAddKernel.Name.Should().NotBeNullOrEmpty();
        SharedMemoryKernel.Name.Should().NotBeNullOrEmpty();
        MinimalKernel.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IKernel_Source_ShouldNotBeNullOrEmpty()
    {
        // Act & Assert
        TestKernel.Source.Should().NotBeNullOrEmpty();
        VectorAddKernel.Source.Should().NotBeNullOrEmpty();
        SharedMemoryKernel.Source.Should().NotBeNullOrEmpty();
        MinimalKernel.Source.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IKernel_EntryPoint_ShouldNotBeNullOrEmpty()
    {
        // Act & Assert
        TestKernel.EntryPoint.Should().NotBeNullOrEmpty();
        VectorAddKernel.EntryPoint.Should().NotBeNullOrEmpty();
        SharedMemoryKernel.EntryPoint.Should().NotBeNullOrEmpty();
        MinimalKernel.EntryPoint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IKernel_RequiredSharedMemory_ShouldBeNonNegative()
    {
        // Act & Assert
        TestKernel.RequiredSharedMemory.Should().BeGreaterOrEqualTo(0);
        VectorAddKernel.RequiredSharedMemory.Should().BeGreaterOrEqualTo(0);
        SharedMemoryKernel.RequiredSharedMemory.Should().BeGreaterOrEqualTo(0);
        MinimalKernel.RequiredSharedMemory.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region IKernel Type Interface Tests

    [Fact]
    public void IKernel_ShouldBeInterface()
    {
        // Arrange & Act
        var kernelType = typeof(IKernel);

        // Assert
        kernelType.IsInterface.Should().BeTrue();
        kernelType.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void TestKernel_ShouldImplementIKernel()
    {
        // Arrange & Act
        var kernelType = typeof(TestKernel);

        // Assert
        kernelType.Should().BeAssignableTo<IKernel>();
        kernelType.IsValueType.Should().BeTrue(); // Should be struct for performance
    }

    [Fact]
    public void AllTestKernels_ShouldImplementIKernel()
    {
        // Assert
        typeof(TestKernel).Should().BeAssignableTo<IKernel>();
        typeof(VectorAddKernel).Should().BeAssignableTo<IKernel>();
        typeof(SharedMemoryKernel).Should().BeAssignableTo<IKernel>();
        typeof(MinimalKernel).Should().BeAssignableTo<IKernel>();
    }

    #endregion

    #region IKernel Static Abstract Member Accessibility Tests

    [Fact]
    public void IKernel_StaticAbstractMembers_ShouldBeAccessible()
    {
        // These tests verify that static abstract members are properly accessible
        // through the interface implementations

        // Act
        var testKernelName = TestKernel.Name;
        var testKernelSource = TestKernel.Source;
        var testKernelEntryPoint = TestKernel.EntryPoint;
        var testKernelSharedMemory = TestKernel.RequiredSharedMemory;

        // Assert
        testKernelName.Should().NotBeNull();
        testKernelSource.Should().NotBeNull();
        testKernelEntryPoint.Should().NotBeNull();
        testKernelSharedMemory.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void IKernel_StaticAbstractMembers_ShouldBeConsistent()
    {
        // Test that accessing static members multiple times returns same values
        
        // Act
        var name1 = TestKernel.Name;
        var name2 = TestKernel.Name;
        var source1 = TestKernel.Source;
        var source2 = TestKernel.Source;
        var entryPoint1 = TestKernel.EntryPoint;
        var entryPoint2 = TestKernel.EntryPoint;
        var sharedMemory1 = TestKernel.RequiredSharedMemory;
        var sharedMemory2 = TestKernel.RequiredSharedMemory;

        // Assert
        name1.Should().Be(name2);
        source1.Should().Be(source2);
        entryPoint1.Should().Be(entryPoint2);
        sharedMemory1.Should().Be(sharedMemory2);
    }

    #endregion

    #region Different Kernel Types Comparison Tests

    [Fact]
    public void DifferentKernels_ShouldHaveDifferentProperties()
    {
        // Assert - Each kernel should have unique characteristics
        TestKernel.Name.Should().NotBe(VectorAddKernel.Name);
        TestKernel.Name.Should().NotBe(SharedMemoryKernel.Name);
        TestKernel.Name.Should().NotBe(MinimalKernel.Name);

        TestKernel.Source.Should().NotBe(VectorAddKernel.Source);
        TestKernel.EntryPoint.Should().NotBe(VectorAddKernel.EntryPoint);
    }

    [Fact]
    public void SharedMemoryKernel_ShouldHaveLargerSharedMemory()
    {
        // Assert
        SharedMemoryKernel.RequiredSharedMemory.Should().BeGreaterThan(TestKernel.RequiredSharedMemory);
        SharedMemoryKernel.RequiredSharedMemory.Should().BeGreaterThan(VectorAddKernel.RequiredSharedMemory);
        SharedMemoryKernel.RequiredSharedMemory.Should().BeGreaterThan(MinimalKernel.RequiredSharedMemory);
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public void IKernel_StaticMemberAccess_ShouldBeEfficient()
    {
        // Test that accessing static members is efficient (no boxing/allocation)
        // This is a performance characteristic test
        
        var startTime = DateTime.UtcNow;
        
        // Act - Access properties many times
        for (int i = 0; i < 10000; i++)
        {
            var _ = TestKernel.Name;
            var __ = TestKernel.Source;
            var ___ = TestKernel.EntryPoint;
            var ____ = TestKernel.RequiredSharedMemory;
        }
        
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert - Should complete quickly (less than 100ms for 10000 iterations)
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void IKernel_KernelStructs_ShouldBeValueTypes()
    {
        // Structs should be value types for better performance and AOT compatibility
        
        // Assert
        typeof(TestKernel).IsValueType.Should().BeTrue();
        typeof(VectorAddKernel).IsValueType.Should().BeTrue();
        typeof(SharedMemoryKernel).IsValueType.Should().BeTrue();
        typeof(MinimalKernel).IsValueType.Should().BeTrue();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void IKernel_Properties_ShouldHandleSpecialCharacters()
    {
        // Test kernels should handle various content types
        
        // Assert - Properties should contain expected content
        VectorAddKernel.Source.Should().Contain("*"); // Pointer syntax
        VectorAddKernel.Source.Should().Contain("+"); // Arithmetic operator
        VectorAddKernel.Source.Should().Contain("["); // Array access
        VectorAddKernel.Source.Should().Contain("]"); // Array access
        
        SharedMemoryKernel.Source.Should().Contain("__shared__"); // CUDA shared memory keyword
    }

    [Fact]
    public void IKernel_Names_ShouldBeValidIdentifiers()
    {
        // Kernel names should be valid programming identifiers
        
        // Assert
        TestKernel.Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        VectorAddKernel.Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        SharedMemoryKernel.Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        MinimalKernel.Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    [Fact]
    public void IKernel_EntryPoints_ShouldBeValidMethodNames()
    {
        // Entry points should be valid method names
        
        // Assert
        TestKernel.EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        VectorAddKernel.EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        SharedMemoryKernel.EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        MinimalKernel.EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    #endregion

    #region Generic Type Parameter Tests

    [Fact]
    public void IKernel_ShouldSupportGenericImplementations()
    {
        // Test that the interface can work with generic constraints
        // This verifies the interface design is flexible
        
        // Arrange & Act
        var kernelType = typeof(IKernel);
        
        // Assert
        kernelType.IsGenericType.Should().BeFalse(); // Interface itself is not generic
        kernelType.IsGenericTypeDefinition.Should().BeFalse();
        
        // But implementations can be generic or constrained
        typeof(TestKernel).IsValueType.Should().BeTrue();
    }

    #endregion

    #region Compilation Context Tests

    [Fact]
    public void IKernel_Properties_ShouldProvideCompilationContext()
    {
        // Test that kernel properties provide enough context for compilation
        
        // Act & Assert
        TestKernel.Name.Should().NotBeNullOrEmpty("Name is required for kernel identification");
        TestKernel.Source.Should().NotBeNullOrEmpty("Source code is required for compilation");
        TestKernel.EntryPoint.Should().NotBeNullOrEmpty("Entry point is required for execution");
        
        // Shared memory can be zero (valid case)
        TestKernel.RequiredSharedMemory.Should().BeGreaterOrEqualTo(0, "Shared memory requirement must be non-negative");
    }

    [Fact]
    public void IKernel_Properties_ShouldBeCompilationReady()
    {
        // Test that properties contain compilation-ready information
        
        // Assert
        VectorAddKernel.Source.Should().Contain("void"); // Should contain function declaration
        VectorAddKernel.EntryPoint.Should().Be("vectorAdd"); // Should match function name
        
        SharedMemoryKernel.RequiredSharedMemory.Should().BePositive("Kernel declares shared memory usage");
        SharedMemoryKernel.Source.Should().Contain("__shared__"); // Should match memory requirement
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void IKernel_StaticMembers_ShouldBeThreadSafe()
    {
        // Test concurrent access to static abstract members
        
        var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        
        // Act - Multiple threads accessing static members
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var name = TestKernel.Name;
                var source = TestKernel.Source;
                var entryPoint = TestKernel.EntryPoint;
                var sharedMemory = TestKernel.RequiredSharedMemory;
                
                results.Add($"{name}:{source}:{entryPoint}:{sharedMemory}");
            }));
        }
        
        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
        
        // Assert - All results should be identical
        results.Should().HaveCount(10);
        results.Distinct().Should().HaveCount(1); // All results should be the same
        
        var expectedResult = $"{TestKernel.Name}:{TestKernel.Source}:{TestKernel.EntryPoint}:{TestKernel.RequiredSharedMemory}";
        results.Should().AllBe(expectedResult);
    }

    #endregion
}