// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for the BaseAccelerator consolidation that eliminated 68% of duplicate code.
/// </summary>
public class BaseAcceleratorTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly TestAccelerator _accelerator;

    public BaseAcceleratorTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMemory = new Mock<IUnifiedMemoryManager>();
        
        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Test Accelerator",
            "1.0",
            1024 * 1024 * 1024,
            4,
            3000,
            new Version(1, 0),
            1024 * 1024,
            true
        );
        
        _accelerator = new TestAccelerator(info, _mockMemory.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties_Correctly()
    {
        // Assert
        Assert.NotNull(_accelerator.Info);
        Assert.Equal(AcceleratorType.CPU, _accelerator.Type);
        Assert.Equal(_mockMemory.Object, _accelerator.Memory);
        // Context is a value type - no null check needed
        Assert.False(_accelerator.IsDisposed);
    }

    [Fact]
    public async Task CompileKernelAsync_ValidatesDefinition_BeforeCompilation()
    {
        // Arrange
        var invalidDefinition = new KernelDefinition("", null!, null!);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _accelerator.CompileKernelAsync(invalidDefinition));
    }

    [Fact]
    public async Task CompileKernelAsync_CallsCompileKernelCoreAsync_ForValidDefinition()
    {
        // Arrange
        var definition = new KernelDefinition("test", "test source", "main");
        var options = new CompilationOptions();
        
        // Act
        var result = await _accelerator.CompileKernelAsync(definition, options);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(_accelerator.CompileKernelCoreCalled);
    }

    [Fact]
    public async Task SynchronizeAsync_CallsSynchronizeCoreAsync()
    {
        // Act
        await _accelerator.SynchronizeAsync();
        
        // Assert
        Assert.True(_accelerator.SynchronizeCoreCalled);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOnlyOnce()
    {
        // Act
        await _accelerator.DisposeAsync();
        await _accelerator.DisposeAsync(); // Second call
        
        // Assert
        Assert.True(_accelerator.IsDisposed);
        Assert.Equal(1, _accelerator.DisposeCallCount);
    }

    [Fact]
    public void ThrowIfDisposed_ThrowsObjectDisposedException_WhenDisposed()
    {
        // Arrange
        _accelerator.SimulateDispose();
        
        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _accelerator.TestThrowIfDisposed());
    }

    [Fact]
    public void GetEffectiveOptions_ReturnsDefaults_WhenNullProvided()
    {
        // Act
        var options = _accelerator.TestGetEffectiveOptions(null);
        
        // Assert
        Assert.NotNull(options);
        Assert.Equal(OptimizationLevel.Balanced, options.OptimizationLevel);
        Assert.False(options.EnableDebugInfo);
        Assert.False(options.EnableProfiling);
    }

    [Fact]
    public void LogCompilationMetrics_LogsDebugMessage()
    {
        // Arrange
        var kernelName = "test_kernel";
        var compilationTime = TimeSpan.FromMilliseconds(100);
        var byteCodeSize = 1024L;
        
        // Act
        _accelerator.TestLogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test implementation of BaseAccelerator for testing purposes.
    /// </summary>
    private sealed class TestAccelerator : BaseAccelerator
    {
        public bool CompileKernelCoreCalled { get; private set; }
        public bool SynchronizeCoreCalled { get; private set; }
        public int DisposeCallCount { get; private set; }

        public TestAccelerator(AcceleratorInfo info, IUnifiedMemoryManager memory, ILogger logger)
            : base(info, AcceleratorType.CPU, memory, new AcceleratorContext(IntPtr.Zero, 0), logger)
        {
        }

        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            CompileKernelCoreCalled = true;
            var mockKernel = new Mock<ICompiledKernel>();
            mockKernel.Setup(x => x.Name).Returns(definition.Name);
            return ValueTask.FromResult(mockKernel.Object);
        }

        protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
        {
            SynchronizeCoreCalled = true;
            return ValueTask.CompletedTask;
        }

        protected override async ValueTask DisposeCoreAsync()
        {
            DisposeCallCount++;
            await base.DisposeCoreAsync();
            return ValueTask.CompletedTask;
        }

        public void SimulateDispose()
        {
            var field = typeof(BaseAccelerator).GetField("_disposed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, 1);
        }

        public void TestThrowIfDisposed() => ThrowIfDisposed();
        
        public CompilationOptions TestGetEffectiveOptions(CompilationOptions? options) 
            => GetEffectiveOptions(options);
        
        public void TestLogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize)
            => LogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
    }
}