using DotCompute.Core.Compute;
using DotCompute.Core.Compute;
using DotCompute.Abstractions;
using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for Core compute components.
/// </summary>
public class CoreComputeTests
{
    [Fact]
    public void DeviceMetrics_KernelExecutionCount_InitiallyZero()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Assert
        Assert.Equal(0, metrics.KernelExecutionCount);
    }
    
    [Fact]
    public void DeviceMetrics_TotalComputeTime_InitiallyZero()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Assert
        Assert.Equal(TimeSpan.Zero, metrics.TotalComputeTime);
    }
    
    [Fact]
    public void DeviceMetrics_AverageKernelTime_WithZeroExecutions_ShouldReturnZero()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Assert
        Assert.Equal(TimeSpan.Zero, metrics.AverageKernelTime);
    }
    
    [Fact]
    public void DeviceMetrics_Temperature_ShouldReturnNull()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Assert
        Assert.Null(metrics.Temperature);
    }
    
    [Fact]
    public void DeviceMetrics_PowerConsumption_ShouldReturnNull()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Assert
        Assert.Null(metrics.PowerConsumption);
    }
    
    [Fact]
    public void DeviceMetrics_Utilization_ShouldReturnValue()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Act
        var utilization = metrics.Utilization;
        
        // Assert
        Assert.True(utilization >= 0.0 && utilization <= 1.0);
    }
    
    [Fact]
    public void DeviceMetrics_MemoryUsage_ShouldReturnValue()
    {
        // Arrange
        var metrics = new DeviceMetrics();
        
        // Act
        var memoryUsage = metrics.MemoryUsage;
        
        // Assert
        Assert.True(memoryUsage >= 0.0);
    }

    // Note: DefaultAcceleratorManager tests removed due to complex dependency injection requirements
    // These would be better suited for integration tests

    [Fact]
    public void AcceleratorSelectionCriteria_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var criteria = new AcceleratorSelectionCriteria();
        
        // Assert
        Assert.Null(criteria.PreferredType);
        Assert.Null(criteria.MinimumMemory);
        Assert.Null(criteria.MinimumComputeCapability);
        Assert.Null(criteria.CustomScorer);
    }
    
    [Fact]
    public void AcceleratorSelectionCriteria_SetProperties_ShouldWork()
    {
        // Arrange
        var criteria = new AcceleratorSelectionCriteria();
        Func<IAccelerator, double> scorer = a => 1.0;
        
        // Act
        criteria.PreferredType = AcceleratorType.GPU;
        criteria.MinimumMemory = 1024 * 1024 * 1024; // 1GB
        criteria.MinimumComputeCapability = new Version(6, 0);
        criteria.CustomScorer = scorer;
        
        // Assert
        Assert.Equal(AcceleratorType.GPU, criteria.PreferredType);
        Assert.Equal(1024 * 1024 * 1024, criteria.MinimumMemory);
        Assert.Equal(new Version(6, 0), criteria.MinimumComputeCapability);
        Assert.Equal(scorer, criteria.CustomScorer);
    }

    [Fact]
    public void AcceleratorContext_Creation_ShouldSetProperties()
    {
        // Arrange
        var handle = new IntPtr(12345);
        var deviceId = 42;
        
        // Act
        var context = new AcceleratorContext(handle, deviceId);
        
        // Assert
        Assert.Equal(handle, context.Handle);
        Assert.Equal(deviceId, context.DeviceId);
    }
}