// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Tests.Common;

namespace DotCompute.Core.Tests.Disposal;

/// <summary>
/// Tests to validate proper disposal patterns and CA2000 compliance.
/// </summary>
public class DisposalValidationTests
{
    [Fact]
    public void TestMemoryBuffer_ProperDisposalPattern_ShouldNotLeakResources()
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<int>(1024);
        var initialState = buffer.IsDisposed;

        // Assert
        _ = initialState.Should().BeFalse("buffer should not be disposed initially");

        // The using statement will properly dispose the buffer
        // This test validates the pattern used to fix CA2000 warnings
    }

    [Fact]
    public async Task TestMemoryBuffer_AsyncDisposalPattern_ShouldNotLeakResources()
    {
        // Arrange & Act
        await using var buffer = new TestMemoryBuffer<int>(1024);
        var initialState = buffer.IsDisposed;

        // Assert
        _ = initialState.Should().BeFalse("buffer should not be disposed initially");

        // The await using statement will properly dispose the buffer asynchronously
    }

    [Fact]
    public void DisposableField_InTestClass_ShouldBeProperlyDisposed()
    {
        // This test validates the pattern where disposable objects are stored as fields
        // and disposed in the class's Dispose method (as implemented in test classes)

        var testInstance = new DisposableTestClass();
        var resource = testInstance.GetResource();

        _ = resource.Should().NotBeNull();
        _ = resource.IsDisposed.Should().BeFalse();

        testInstance.Dispose();

        _ = resource.IsDisposed.Should().BeTrue("resource should be disposed when parent is disposed");
    }

    [Fact]
    public void MappedMemoryReturn_WithSuppressionAttribute_ShouldTransferOwnership()
    {
        // This test validates the pattern where a disposable is returned to the caller
        // and the CA2000 warning is suppressed with proper justification

        using var buffer = new TestMemoryBuffer<int>(1024);
        using var mapped = buffer.Map(); // This returns a disposable that transfers ownership

        _ = mapped.Should().NotBeNull();
        // The caller is responsible for disposing the mapped memory
    }

    /// <summary>
    /// Test class that demonstrates proper disposal of field resources.
    /// </summary>
    private sealed class DisposableTestClass : IDisposable
    {
        private readonly TestMemoryBuffer<int> _resource;
        private bool _disposed;

        public DisposableTestClass()
        {
            _resource = new TestMemoryBuffer<int>(1024);
        }

        public TestMemoryBuffer<int> GetResource() => _resource;

        public void Dispose()
        {
            if (!_disposed)
            {
                _resource?.Dispose();
                _disposed = true;
            }
        }
    }
}