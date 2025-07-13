// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Core.Pipelines;
using Xunit;
using FluentAssertions;

namespace DotCompute.Core.Tests.Pipelines;

public class PerformanceMonitorTests
{
    [Fact]
    public void GetCpuUtilization_Should_Return_Value_Between_Zero_And_One()
    {
        // Act
        var utilization = PerformanceMonitor.GetCpuUtilization();
        
        // Assert
        utilization.Should().BeInRange(0.0, 1.0);
    }
    
    [Fact]
    public void GetMemoryBandwidthUtilization_Should_Return_Value_Between_Zero_And_One()
    {
        // Act
        var utilization = PerformanceMonitor.GetMemoryBandwidthUtilization();
        
        // Assert
        utilization.Should().BeInRange(0.0, 1.0);
    }
    
    [Fact]
    public void GetComputeUtilization_Should_Return_Value_Based_On_Work()
    {
        // Arrange
        var executionTime = TimeSpan.FromMilliseconds(100);
        var workItems = 1000L;
        
        // Act
        var utilization = PerformanceMonitor.GetComputeUtilization(executionTime, workItems);
        
        // Assert
        utilization.Should().BeInRange(0.0, 1.0);
    }
    
    [Fact]
    public void GetMemoryStats_Should_Return_Valid_Values()
    {
        // Act
        var (workingSet, privateMemory, virtualMemory) = PerformanceMonitor.GetMemoryStats();
        
        // Assert
        workingSet.Should().BeGreaterThan(0);
        privateMemory.Should().BeGreaterThan(0);
        virtualMemory.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void GetThreadPoolStats_Should_Return_Valid_Values()
    {
        // Act
        var (activeWorkers, activeCompletionPorts, availableWorkers, availableCompletionPorts) = 
            PerformanceMonitor.GetThreadPoolStats();
        
        // Assert
        activeWorkers.Should().BeGreaterOrEqualTo(0);
        activeCompletionPorts.Should().BeGreaterOrEqualTo(0);
        availableWorkers.Should().BeGreaterThan(0);
        availableCompletionPorts.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task ExecutionMetrics_Should_Track_Performance()
    {
        // Arrange
        PerformanceMonitor.ExecutionMetrics.StartExecution();
        
        // Act - Do some work
        await Task.Delay(50);
        var array = new byte[1024 * 1024]; // Allocate 1MB
        Array.Fill(array, (byte)42);
        
        var (cpuTime, allocatedBytes, elapsedMs) = PerformanceMonitor.ExecutionMetrics.EndExecution();
        
        // Assert
        elapsedMs.Should().BeGreaterThan(40); // Should be at least 40ms (accounting for timing variations)
        allocatedBytes.Should().BeGreaterThan(1024 * 1024); // Should have allocated at least 1MB
        cpuTime.Should().BeGreaterOrEqualTo(0); // CPU time should be non-negative
    }
    
    [Fact]
    public void Performance_Monitor_Should_Handle_Rapid_Calls()
    {
        // This tests that the performance monitor can handle rapid successive calls
        // without throwing exceptions or returning invalid values
        
        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var cpu = PerformanceMonitor.GetCpuUtilization();
            var memory = PerformanceMonitor.GetMemoryBandwidthUtilization();
            
            cpu.Should().BeInRange(0.0, 1.0);
            memory.Should().BeInRange(0.0, 1.0);
            
            Thread.Yield(); // Give other threads a chance
        }
    }
}