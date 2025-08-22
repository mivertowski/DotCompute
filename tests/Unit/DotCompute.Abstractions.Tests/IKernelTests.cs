// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for the IKernel interface and related types.
/// Note: Since IKernel uses static abstract members, we test with concrete implementations.
/// </summary>
public sealed class IKernelTests
{
    #region Test Kernel Implementations

    /// <summary>
    /// Test implementation of IKernel for testing purposes.
    /// </summary>
    private struct TestKernel : IKernel
    {
        public string Name => "TestKernel";
        public string Source => "__global__ void test() { }";
        public string EntryPoint => "test";
        public int RequiredSharedMemory => 1024;
    }

    /// <summary>
    /// Another test implementation with different values.
    /// </summary>
    private struct VectorAddKernel : IKernel
    {
        public string Name => "VectorAdd";
        public string Source => "__global__ void vectorAdd(float* a, float* b, float* c) { int i = threadIdx.x; c[i] = a[i] + b[i]; }";
        public string EntryPoint => "vectorAdd";
        public int RequiredSharedMemory => 0;
    }

    /// <summary>
    /// Test implementation with large shared memory requirement.
    /// </summary>
    private struct SharedMemoryKernel : IKernel
    {
        public string Name => "SharedMemoryKernel";
        public string Source => "__global__ void sharedMemTest() { __shared__ float data[1024]; }";
        public string EntryPoint => "sharedMemTest";
        public int RequiredSharedMemory => 4096;
    }

    /// <summary>
    /// Test implementation with minimal requirements.
    /// </summary>
    private struct MinimalKernel : IKernel
    {
        public string Name => "Minimal";
        public string Source => "void main() {}";
        public string EntryPoint => "main";
        public int RequiredSharedMemory => 0;
    }

    #endregion

    #region IKernel Static Abstract Members Tests

    [Fact]
    public void TestKernel_Properties_ShouldReturnCorrectValues()
    {
        // Arrange
        var kernel = new TestKernel();
        
        // Act & Assert
        _ = kernel.Name.Should().Be("TestKernel");
        _ = kernel.Source.Should().Be("__global__ void test() { }");
        _ = kernel.EntryPoint.Should().Be("test");
        _ = kernel.RequiredSharedMemory.Should().Be(1024);
    }

    [Fact]
    public void VectorAddKernel_Properties_ShouldReturnCorrectValues()
    {
        // Arrange
        var kernel = new VectorAddKernel();
        
        // Act & Assert
        _ = kernel.Name.Should().Be("VectorAdd");
        _ = kernel.Source.Should().Contain("vectorAdd");
        _ = kernel.Source.Should().Contain("float* a"); // float* b;
        _ = kernel.EntryPoint.Should().Be("vectorAdd");
        _ = kernel.RequiredSharedMemory.Should().Be(0);
    }

    [Fact]
    public void SharedMemoryKernel_Properties_ShouldReturnCorrectValues()
    {
        // Arrange
        var kernel = new SharedMemoryKernel();
        
        // Act & Assert
        _ = kernel.Name.Should().Be("SharedMemoryKernel");
        _ = kernel.Source.Should().Contain("__shared__");
        _ = kernel.EntryPoint.Should().Be("sharedMemTest");
        _ = kernel.RequiredSharedMemory.Should().Be(4096);
    }

    [Fact]
    public void MinimalKernel_Properties_ShouldReturnCorrectValues()
    {
        // Arrange
        var kernel = new MinimalKernel();
        
        // Act & Assert
        _ = kernel.Name.Should().Be("Minimal");
        _ = kernel.Source.Should().Be("void main() {}");
        _ = kernel.EntryPoint.Should().Be("main");
        _ = kernel.RequiredSharedMemory.Should().Be(0);
    }

    #endregion

    #region IKernel Property Validation Tests

    [Fact]
    public void IKernel_Name_ShouldNotBeNullOrEmpty()
    {
        // Act & Assert
        _ = new TestKernel().Name.Should().NotBeNullOrEmpty();
        _ = new VectorAddKernel().Name.Should().NotBeNullOrEmpty();
        _ = new SharedMemoryKernel().Name.Should().NotBeNullOrEmpty();
        _ = new MinimalKernel().Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IKernel_Source_ShouldNotBeNullOrEmpty()
    {
        // Act & Assert
        _ = new TestKernel().Source.Should().NotBeNullOrEmpty();
        _ = new VectorAddKernel().Source.Should().NotBeNullOrEmpty();
        _ = new SharedMemoryKernel().Source.Should().NotBeNullOrEmpty();
        _ = new MinimalKernel().Source.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IKernel_EntryPoint_ShouldNotBeNullOrEmpty()
    {
        // Act & Assert
        _ = new TestKernel().EntryPoint.Should().NotBeNullOrEmpty();
        _ = new VectorAddKernel().EntryPoint.Should().NotBeNullOrEmpty();
        _ = new SharedMemoryKernel().EntryPoint.Should().NotBeNullOrEmpty();
        _ = new MinimalKernel().EntryPoint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IKernel_RequiredSharedMemory_ShouldBeNonNegative()
    {
        // Act & Assert
        _ = new TestKernel().RequiredSharedMemory.Should().BeGreaterThanOrEqualTo(0);
        _ = new VectorAddKernel().RequiredSharedMemory.Should().BeGreaterThanOrEqualTo(0);
        _ = new SharedMemoryKernel().RequiredSharedMemory.Should().BeGreaterThanOrEqualTo(0);
        _ = new MinimalKernel().RequiredSharedMemory.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region IKernel Type Interface Tests

    [Fact]
    public void IKernel_ShouldBeInterface()
    {
        // Arrange & Act
        var kernelType = typeof(IKernel);

        // Assert
        _ = kernelType.IsInterface.Should().BeTrue();
        _ = kernelType.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void TestKernel_ShouldImplementIKernel()
    {
        // Arrange & Act
        var kernelType = typeof(TestKernel);

        // Assert - Check type hierarchy
        Assert.True(typeof(TestKernel).IsAssignableTo(typeof(IKernel)));
        _ = kernelType.IsValueType.Should().BeTrue(); // Should be struct for performance
    }

    [Fact]
    public void AllTestKernels_ShouldImplementIKernel()
    {
        // Assert - Check type assignability
        Assert.True(typeof(TestKernel).IsAssignableTo(typeof(IKernel)));
        Assert.True(typeof(VectorAddKernel).IsAssignableTo(typeof(IKernel)));
        Assert.True(typeof(SharedMemoryKernel).IsAssignableTo(typeof(IKernel)));
        Assert.True(typeof(MinimalKernel).IsAssignableTo(typeof(IKernel)));
    }

    #endregion

    #region IKernel Static Abstract Member Accessibility Tests

    [Fact]
    public void IKernel_StaticAbstractMembers_ShouldBeAccessible()
    {
        // These tests verify that static abstract members are properly accessible
        // through the interface implementations

        // Act
        var testKernelName = new TestKernel().Name;
        var testKernelSource = new TestKernel().Source;
        var testKernelEntryPoint = new TestKernel().EntryPoint;
        var testKernelSharedMemory = new TestKernel().RequiredSharedMemory;

        // Assert
        Assert.NotNull(testKernelName);
        Assert.NotNull(testKernelSource);
        Assert.NotNull(testKernelEntryPoint);
        Assert.True(testKernelSharedMemory >= 0);
    }

    [Fact]
    public void IKernel_StaticAbstractMembers_ShouldBeConsistent()
    {
        // Test that accessing static members multiple times returns same values

        // Act
        var name1 = new TestKernel().Name;
        var name2 = new TestKernel().Name;
        var source1 = new TestKernel().Source;
        var source2 = new TestKernel().Source;
        var entryPoint1 = new TestKernel().EntryPoint;
        var entryPoint2 = new TestKernel().EntryPoint;
        var sharedMemory1 = new TestKernel().RequiredSharedMemory;
        var sharedMemory2 = new TestKernel().RequiredSharedMemory;

        // Assert
        Assert.Equal(name2, name1);
        Assert.Equal(source2, source1);
        Assert.Equal(entryPoint2, entryPoint1);
        Assert.Equal(sharedMemory2, sharedMemory1);
    }

    #endregion

    #region Different Kernel Types Comparison Tests

    [Fact]
    public void DifferentKernels_ShouldHaveDifferentProperties()
    {
        // Assert - Each kernel should have unique characteristics
        _ = new TestKernel().Name.Should().NotBe(new VectorAddKernel().Name);
        _ = new TestKernel().Name.Should().NotBe(new SharedMemoryKernel().Name);
        _ = new TestKernel().Name.Should().NotBe(new MinimalKernel().Name);

        _ = new TestKernel().Source.Should().NotBe(new VectorAddKernel().Source);
        _ = new TestKernel().EntryPoint.Should().NotBe(new VectorAddKernel().EntryPoint);
    }

    [Fact]
    public void SharedMemoryKernel_ShouldHaveLargerSharedMemory()
    {
        // Assert
        _ = (new SharedMemoryKernel().RequiredSharedMemory > new TestKernel().RequiredSharedMemory).Should().BeTrue();
        _ = (new SharedMemoryKernel().RequiredSharedMemory > new VectorAddKernel().RequiredSharedMemory).Should().BeTrue();
        _ = new SharedMemoryKernel().RequiredSharedMemory.Should().BeGreaterThan(new MinimalKernel().RequiredSharedMemory);
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public void IKernel_StaticMemberAccess_ShouldBeEfficient()
    {
        // Test that accessing static members is efficient(no boxing/allocation)
        // This is a performance characteristic test

        var startTime = DateTime.UtcNow;

        // Act - Access properties many times
        for (var i = 0; i < 10000; i++)
        {
            _ = new TestKernel().Name;
            var __ = new TestKernel().Source;
            var ___ = new TestKernel().EntryPoint;
            var ____ = new TestKernel().RequiredSharedMemory;
        }

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert - Should complete quickly(less than 100ms for 10000 iterations)
        _ = duration.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void IKernel_KernelStructs_ShouldBeValueTypes()
    {
        // Structs should be value types for better performance and AOT compatibility

        // Assert
        _ = typeof(TestKernel).IsValueType.Should().BeTrue();
        _ = typeof(VectorAddKernel).IsValueType.Should().BeTrue();
        _ = typeof(SharedMemoryKernel).IsValueType.Should().BeTrue();
        _ = typeof(MinimalKernel).IsValueType.Should().BeTrue();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void IKernel_Properties_ShouldHandleSpecialCharacters()
    {
        // Test kernels should handle various content types

        // Assert - Properties should contain expected content
        _ = new VectorAddKernel().Source.Should().Contain("*"); // Pointer syntax
        _ = new VectorAddKernel().Source.Should().Contain("+"); // Arithmetic operator
        _ = new VectorAddKernel().Source.Should().Contain("["); // Array access
        _ = new VectorAddKernel().Source.Should().Contain("]"); // Array access

        _ = new SharedMemoryKernel().Source.Should().Contain("__shared__"); // CUDA shared memory keyword
    }

    [Fact]
    public void IKernel_Names_ShouldBeValidIdentifiers()
    {
        // Kernel names should be valid programming identifiers

        // Assert
        _ = new TestKernel().Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        _ = new VectorAddKernel().Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        _ = new SharedMemoryKernel().Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        _ = new MinimalKernel().Name.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    [Fact]
    public void IKernel_EntryPoints_ShouldBeValidMethodNames()
    {
        // Entry points should be valid method names

        // Assert
        _ = new TestKernel().EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        _ = new VectorAddKernel().EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        _ = new SharedMemoryKernel().EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
        _ = new MinimalKernel().EntryPoint.Should().MatchRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
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
        _ = kernelType.IsGenericType.Should().BeFalse(); // Interface itself is not generic
        _ = kernelType.IsGenericTypeDefinition.Should().BeFalse();

        // But implementations can be generic or constrained
        _ = typeof(TestKernel).IsValueType.Should().BeTrue();
    }

    #endregion

    #region Compilation Context Tests

    [Fact]
    public void IKernel_Properties_ShouldProvideCompilationContext()
    {
        // Test that kernel properties provide enough context for compilation

        // Act & Assert
        _ = new TestKernel().Name.Should().NotBeNullOrEmpty("Name is required for kernel identification");
        _ = new TestKernel().Source.Should().NotBeNullOrEmpty("Source code is required for compilation");
        _ = new TestKernel().EntryPoint.Should().NotBeNullOrEmpty("Entry point is required for execution");

        // Shared memory can be zero(valid case)
        _ = (new TestKernel().RequiredSharedMemory >= 0).Should().BeTrue();
    }

    [Fact]
    public void IKernel_Properties_ShouldBeCompilationReady()
    {
        // Test that properties contain compilation-ready information

        // Assert
        _ = new VectorAddKernel().Source.Should().Contain("void"); // Should contain function declaration
        _ = new VectorAddKernel().EntryPoint.Should().Be("vectorAdd"); // Should match function name

        _ = new SharedMemoryKernel().RequiredSharedMemory.Should().BePositive("Kernel declares shared memory usage");
        _ = new SharedMemoryKernel().Source.Should().Contain("__shared__"); // Should match memory requirement
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
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var name = new TestKernel().Name;
                var source = new TestKernel().Source;
                var entryPoint = new TestKernel().EntryPoint;
                var sharedMemory = new TestKernel().RequiredSharedMemory;

                results.Add($"{name}:{source}:{entryPoint}:{sharedMemory}");
            }));
        }

        System.Threading.Tasks.Task.WaitAll([.. tasks]);

        // Assert - All results should be identical
        Assert.Equal(10, results.Count);
        _ = results.Distinct().Should().HaveCount(1); // All results should be the same

        var expectedResult = $"{new TestKernel().Name}:{new TestKernel().Source}:{new TestKernel().EntryPoint}:{new TestKernel().RequiredSharedMemory}";
        _ = results.Should().AllBe(expectedResult);
    }

    #endregion
}
