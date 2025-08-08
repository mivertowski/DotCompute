using DotCompute.Core;
using DotCompute.Core.Kernels;
using DotCompute.Abstractions;
using Xunit;
using NSubstitute;

namespace DotCompute.Core.UnitTests;

/// <summary>
/// Tests for Core kernel types and components.
/// </summary>
public class CoreKernelTypesTests
{
    [Fact]
    public void KernelExecutionContext_Creation_WithRequiredProperties_ShouldWork()
    {
        // Arrange & Act
        var context = new KernelExecutionContext
        {
            Name = "test_kernel"
        };
        
        // Assert
        Assert.Equal("test_kernel", context.Name);
        Assert.Null(context.Arguments);
        Assert.Null(context.WorkDimensions);
        Assert.Null(context.LocalWorkSize);
        Assert.Equal(default, context.CancellationToken);
    }
    
    [Fact]
    public void KernelExecutionContext_Creation_WithAllProperties_ShouldWork()
    {
        // Arrange
        var arguments = new object[] { 1, 2.5f, "test" };
        var workDimensions = new long[] { 1024, 768 };
        var localWorkSize = new long[] { 16, 16 };
        var cancellationToken = new CancellationToken();
        
        // Act
        var context = new KernelExecutionContext
        {
            Name = "complex_kernel",
            Arguments = arguments,
            WorkDimensions = workDimensions,
            LocalWorkSize = localWorkSize,
            CancellationToken = cancellationToken
        };
        
        // Assert
        Assert.Equal("complex_kernel", context.Name);
        Assert.Equal(arguments, context.Arguments);
        Assert.Equal(workDimensions, context.WorkDimensions);
        Assert.Equal(localWorkSize, context.LocalWorkSize);
        Assert.Equal(cancellationToken, context.CancellationToken);
    }
    
    [Fact]
    public void KernelExecutionContext_Arguments_ShouldAcceptVariousTypes()
    {
        // Arrange
        var arguments = new object[] 
        { 
            42,                    // int
            3.14159f,             // float
            2.718281828,          // double
            true,                 // bool
            "hello world",        // string
            new byte[] { 1, 2, 3 } // array
        };
        
        // Act
        var context = new KernelExecutionContext
        {
            Name = "multi_type_kernel",
            Arguments = arguments
        };
        
        // Assert
        Assert.Equal("multi_type_kernel", context.Name);
        Assert.Equal(6, context.Arguments!.Length);
        Assert.Equal(42, context.Arguments[0]);
        Assert.Equal(3.14159f, context.Arguments[1]);
        Assert.Equal(2.718281828, context.Arguments[2]);
        Assert.Equal(true, context.Arguments[3]);
        Assert.Equal("hello world", context.Arguments[4]);
        Assert.Equal(new byte[] { 1, 2, 3 }, context.Arguments[5]);
    }
    
    [Fact]
    public void KernelExecutionContext_WorkDimensions_1D_ShouldWork()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "1d_kernel",
            WorkDimensions = new long[] { 1024 }
        };
        
        // Assert
        Assert.Equal("1d_kernel", context.Name);
        Assert.Single(context.WorkDimensions!);
        Assert.Equal(1024, context.WorkDimensions[0]);
    }
    
    [Fact]
    public void KernelExecutionContext_WorkDimensions_2D_ShouldWork()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "2d_kernel",
            WorkDimensions = new long[] { 1024, 768 }
        };
        
        // Assert
        Assert.Equal("2d_kernel", context.Name);
        Assert.Equal(2, context.WorkDimensions!.Count);
        Assert.Equal(1024, context.WorkDimensions[0]);
        Assert.Equal(768, context.WorkDimensions[1]);
    }
    
    [Fact]
    public void KernelExecutionContext_WorkDimensions_3D_ShouldWork()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "3d_kernel",
            WorkDimensions = new long[] { 64, 64, 64 }
        };
        
        // Assert
        Assert.Equal("3d_kernel", context.Name);
        Assert.Equal(3, context.WorkDimensions!.Count);
        Assert.Equal(64, context.WorkDimensions[0]);
        Assert.Equal(64, context.WorkDimensions[1]);
        Assert.Equal(64, context.WorkDimensions[2]);
    }
    
    [Fact]
    public void KernelExecutionContext_LocalWorkSize_ShouldMatchWorkDimensions()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "local_work_kernel",
            WorkDimensions = new long[] { 1024, 768 },
            LocalWorkSize = new long[] { 16, 16 }
        };
        
        // Assert
        Assert.Equal("local_work_kernel", context.Name);
        Assert.Equal(2, context.WorkDimensions!.Count);
        Assert.Equal(2, context.LocalWorkSize!.Count);
        Assert.Equal(1024, context.WorkDimensions[0]);
        Assert.Equal(768, context.WorkDimensions[1]);
        Assert.Equal(16, context.LocalWorkSize[0]);
        Assert.Equal(16, context.LocalWorkSize[1]);
    }
    
    [Fact]
    public void KernelExecutionContext_CancellationToken_ShouldStoreCorrectly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        
        // Act
        var context = new KernelExecutionContext
        {
            Name = "cancellable_kernel",
            CancellationToken = token
        };
        
        // Assert
        Assert.Equal("cancellable_kernel", context.Name);
        Assert.Equal(token, context.CancellationToken);
        Assert.False(context.CancellationToken.IsCancellationRequested);
    }
    
    [Fact]
    public void KernelExecutionContext_CancellationToken_Cancelled_ShouldReflectState()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var token = cts.Token;
        
        // Act
        var context = new KernelExecutionContext
        {
            Name = "cancelled_kernel",
            CancellationToken = token
        };
        
        // Assert
        Assert.Equal("cancelled_kernel", context.Name);
        Assert.Equal(token, context.CancellationToken);
        Assert.True(context.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void IKernelCompiler_Interface_ShouldDefineRequiredMembers()
    {
        // This test verifies the interface contract exists
        var compilerType = typeof(DotCompute.Core.Kernels.IKernelCompiler);
        
        Assert.True(compilerType.IsInterface);
        Assert.NotNull(compilerType.GetProperty("AcceleratorType"));
        Assert.NotNull(compilerType.GetMethod("CompileAsync"));
        Assert.NotNull(compilerType.GetMethod("Validate"));
        Assert.NotNull(compilerType.GetMethod("GetDefaultOptions"));
    }
    
    [Fact]
    public void IKernelExecutor_Interface_ShouldDefineRequiredMembers()
    {
        // This test verifies the interface contract exists
        var executorType = typeof(IKernelExecutor);
        
        Assert.True(executorType.IsInterface);
        Assert.NotNull(executorType.GetProperty("Accelerator"));
        Assert.NotNull(executorType.GetMethod("ExecuteAsync"));
        Assert.NotNull(executorType.GetMethod("ExecuteAndWaitAsync"));
    }
    
    [Fact]
    public void KernelCompilerExtensions_StaticClass_ShouldExist()
    {
        // This test verifies the extension class exists
        var extensionsType = typeof(KernelCompilerExtensions);
        
        Assert.True(extensionsType.IsSealed);
        Assert.True(extensionsType.IsAbstract); // Static classes are abstract and sealed
    }
    
    [Theory]
    [InlineData("vector_add")]
    [InlineData("matrix_multiply")]
    [InlineData("convolution_2d")]
    [InlineData("reduce_sum")]
    [InlineData("fft_1d")]
    public void KernelExecutionContext_KernelNames_ShouldAcceptValidNames(string kernelName)
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = kernelName
        };
        
        // Assert
        Assert.Equal(kernelName, context.Name);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void KernelExecutionContext_WorkDimensions_ValidSizes_ShouldWork(long workSize)
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "parameterized_kernel",
            WorkDimensions = new long[] { workSize }
        };
        
        // Assert
        Assert.Equal("parameterized_kernel", context.Name);
        Assert.Single(context.WorkDimensions!);
        Assert.Equal(workSize, context.WorkDimensions[0]);
    }
    
    [Fact]
    public void KernelExecutionContext_EmptyArguments_ShouldWork()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "no_args_kernel",
            Arguments = Array.Empty<object>()
        };
        
        // Assert
        Assert.Equal("no_args_kernel", context.Name);
        Assert.NotNull(context.Arguments);
        Assert.Empty(context.Arguments);
    }
    
    [Fact]
    public void KernelExecutionContext_LargeWorkDimensions_ShouldWork()
    {
        // Act
        var context = new KernelExecutionContext
        {
            Name = "large_kernel",
            WorkDimensions = new long[] { 1048576, 1048576 } // 1M x 1M
        };
        
        // Assert
        Assert.Equal("large_kernel", context.Name);
        Assert.Equal(2, context.WorkDimensions!.Count);
        Assert.Equal(1048576, context.WorkDimensions[0]);
        Assert.Equal(1048576, context.WorkDimensions[1]);
    }
}