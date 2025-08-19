using DotCompute.Core.Execution;
using DotCompute.Core.Types;
using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Tests for Core execution and type system components.
/// </summary>
public class CoreExecutionTypesTests
{
    [Fact]
    public void ExecutionStrategyType_EnumValues_ShouldBeCorrect()
    {
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), ExecutionStrategyType.Single));
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), ExecutionStrategyType.DataParallel));
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), ExecutionStrategyType.ModelParallel));
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), ExecutionStrategyType.PipelineParallel));
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), ExecutionStrategyType.WorkStealing));
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), ExecutionStrategyType.Heterogeneous));
    }

    [Fact]
    public void DataParallelismOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new DataParallelismOptions();
        
        // Assert
        Assert.Null(options.TargetDevices);
        Assert.Null(options.MaxDevices);
        Assert.True(options.EnablePeerToPeer);
        Assert.Equal(SynchronizationStrategy.EventBased, options.SyncStrategy);
    }
    
    [Fact]
    public void DataParallelismOptions_SetProperties_ShouldWork()
    {
        // Arrange
        var options = new DataParallelismOptions();
        var devices = new[] { "gpu-0", "gpu-1" };
        
        // Act
        options.TargetDevices = devices;
        options.MaxDevices = 2;
        options.EnablePeerToPeer = false;
        options.SyncStrategy = SynchronizationStrategy.Barrier;
        
        // Assert
        Assert.Equal(devices, options.TargetDevices);
        Assert.Equal(2, options.MaxDevices);
        Assert.False(options.EnablePeerToPeer);
        Assert.Equal(SynchronizationStrategy.Barrier, options.SyncStrategy);
    }

    [Fact]
    public void Dimensions3D_Constructor_WithXOnly_ShouldSetCorrectValues()
    {
        // Act
        var dims = new Dimensions3D(100);
        
        // Assert
        Assert.Equal(100, dims.X);
        Assert.Equal(1, dims.Y);
        Assert.Equal(1, dims.Z);
    }
    
    [Fact]
    public void Dimensions3D_Constructor_WithXY_ShouldSetCorrectValues()
    {
        // Act
        var dims = new Dimensions3D(100, 200);
        
        // Assert
        Assert.Equal(100, dims.X);
        Assert.Equal(200, dims.Y);
        Assert.Equal(1, dims.Z);
    }
    
    [Fact]
    public void Dimensions3D_Constructor_WithXYZ_ShouldSetCorrectValues()
    {
        // Act
        var dims = new Dimensions3D(100, 200, 300);
        
        // Assert
        Assert.Equal(100, dims.X);
        Assert.Equal(200, dims.Y);
        Assert.Equal(300, dims.Z);
    }
    
    [Fact]
    public void Dimensions3D_ImplicitConversion_FromInt_ShouldWork()
    {
        // Act
        Dimensions3D dims = 500;
        
        // Assert
        Assert.Equal(500, dims.X);
        Assert.Equal(1, dims.Y);
        Assert.Equal(1, dims.Z);
    }
    
    [Fact]
    public void Dimensions3D_ImplicitConversion_FromTuple2D_ShouldWork()
    {
        // Act
        Dimensions3D dims =(100, 200);
        
        // Assert
        Assert.Equal(100, dims.X);
        Assert.Equal(200, dims.Y);
        Assert.Equal(1, dims.Z);
    }
    
    [Fact]
    public void Dimensions3D_ImplicitConversion_FromTuple3D_ShouldWork()
    {
        // Act
        Dimensions3D dims =(100, 200, 300);
        
        // Assert
        Assert.Equal(100, dims.X);
        Assert.Equal(200, dims.Y);
        Assert.Equal(300, dims.Z);
    }
    
    [Fact]
    public void Dimensions3D_ImplicitConversion_ToDim3_ShouldWork()
    {
        // Arrange
        var dims3d = new Dimensions3D(100, 200, 300);
        
        // Act
        Dim3 dim3 = dims3d;
        
        // Assert
        Assert.Equal(100, dim3.X);
        Assert.Equal(200, dim3.Y);
        Assert.Equal(300, dim3.Z);
    }
    
    [Fact]
    public void Dimensions3D_ImplicitConversion_FromDim3_ShouldWork()
    {
        // Arrange
        var dim3 = new Dim3(100, 200, 300);
        
        // Act
        Dimensions3D dims3d = dim3;
        
        // Assert
        Assert.Equal(100, dims3d.X);
        Assert.Equal(200, dims3d.Y);
        Assert.Equal(300, dims3d.Z);
    }
    
    [Fact]
    public void Dimensions3D_Equals_SameDimensions_ShouldReturnTrue()
    {
        // Arrange
        var dims1 = new Dimensions3D(100, 200, 300);
        var dims2 = new Dimensions3D(100, 200, 300);
        
        // Act & Assert
        Assert.True(dims1.Equals(dims2));
        Assert.True(dims1 == dims2);
        Assert.False(dims1 != dims2);
    }
    
    [Fact]
    public void Dimensions3D_Equals_DifferentDimensions_ShouldReturnFalse()
    {
        // Arrange
        var dims1 = new Dimensions3D(100, 200, 300);
        var dims2 = new Dimensions3D(100, 200, 400);
        
        // Act & Assert
        Assert.False(dims1.Equals(dims2));
        Assert.False(dims1 == dims2);
        Assert.True(dims1 != dims2);
    }
    
    [Fact]
    public void Dimensions3D_GetHashCode_SameDimensions_ShouldReturnSameHash()
    {
        // Arrange
        var dims1 = new Dimensions3D(100, 200, 300);
        var dims2 = new Dimensions3D(100, 200, 300);
        
        // Act & Assert
        Assert.Equal(dims1.GetHashCode(), dims2.GetHashCode());
    }
    
    [Fact]
    public void Dimensions3D_ToString_ShouldReturnCorrectFormat()
    {
        // Arrange
        var dims = new Dimensions3D(100, 200, 300);
        
        // Act
        var result = dims.ToString();
        
        // Assert
        Assert.Equal("(100, 200, 300)", result);
    }

    [Theory]
    [InlineData(SynchronizationStrategy.EventBased)]
    [InlineData(SynchronizationStrategy.Barrier)]
    [InlineData(SynchronizationStrategy.LockFree)]
    public void SynchronizationStrategy_EnumValues_ShouldBeValid(SynchronizationStrategy strategy)
    {
        // Assert that all enum values are defined
        Assert.True(Enum.IsDefined(typeof(SynchronizationStrategy), strategy));
    }
}
}
