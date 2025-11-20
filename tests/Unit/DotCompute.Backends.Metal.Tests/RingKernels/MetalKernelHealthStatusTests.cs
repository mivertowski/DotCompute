// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.RingKernels;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Unit tests for MetalKernelHealthStatus structure and MetalKernelState enum.
/// </summary>
public sealed class MetalKernelHealthStatusTests
{
    [Fact]
    public void KernelState_Enum_Should_Have_Correct_Values()
    {
        // Assert
        Assert.Equal(0, (int)MetalKernelState.Healthy);
        Assert.Equal(1, (int)MetalKernelState.Degraded);
        Assert.Equal(2, (int)MetalKernelState.Failed);
        Assert.Equal(3, (int)MetalKernelState.Recovering);
        Assert.Equal(4, (int)MetalKernelState.Stopped);
    }

    [Fact]
    public void HealthStatus_CreateEmpty_Should_Initialize_All_Fields_To_Zero_Except_State()
    {
        // Act
        var status = MetalKernelHealthStatus.CreateEmpty();

        // Assert
        Assert.Equal(0, status.LastHeartbeatTicks);
        Assert.Equal(0, status.FailedHeartbeats);
        Assert.Equal(0, status.ErrorCount);
        Assert.Equal((int)MetalKernelState.Healthy, status.State);
        Assert.Equal(0, status.LastCheckpointId);
    }

    [Fact]
    public void HealthStatus_CreateInitialized_Should_Set_Current_Timestamp()
    {
        // Act
        var before = DateTime.UtcNow.Ticks;
        var status = MetalKernelHealthStatus.CreateInitialized();
        var after = DateTime.UtcNow.Ticks;

        // Assert
        Assert.InRange(status.LastHeartbeatTicks, before, after);
        Assert.Equal(0, status.FailedHeartbeats);
        Assert.Equal(0, status.ErrorCount);
        Assert.Equal((int)MetalKernelState.Healthy, status.State);
        Assert.Equal(0, status.LastCheckpointId);
    }

    [Fact]
    public void HealthStatus_IsHealthy_Should_Return_True_When_State_Is_Healthy()
    {
        // Arrange
        var status = MetalKernelHealthStatus.CreateEmpty();

        // Act & Assert
        Assert.True(status.IsHealthy());
        Assert.False(status.IsDegraded());
        Assert.False(status.IsFailed());
        Assert.False(status.IsRecovering());
        Assert.False(status.IsStopped());
    }

    [Fact]
    public void HealthStatus_IsDegraded_Should_Return_True_When_State_Is_Degraded()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            State = (int)MetalKernelState.Degraded
        };

        // Act & Assert
        Assert.False(status.IsHealthy());
        Assert.True(status.IsDegraded());
        Assert.False(status.IsFailed());
        Assert.False(status.IsRecovering());
        Assert.False(status.IsStopped());
    }

    [Fact]
    public void HealthStatus_IsFailed_Should_Return_True_When_State_Is_Failed()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            State = (int)MetalKernelState.Failed
        };

        // Act & Assert
        Assert.False(status.IsHealthy());
        Assert.False(status.IsDegraded());
        Assert.True(status.IsFailed());
        Assert.False(status.IsRecovering());
        Assert.False(status.IsStopped());
    }

    [Fact]
    public void HealthStatus_IsRecovering_Should_Return_True_When_State_Is_Recovering()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            State = (int)MetalKernelState.Recovering
        };

        // Act & Assert
        Assert.False(status.IsHealthy());
        Assert.False(status.IsDegraded());
        Assert.False(status.IsFailed());
        Assert.True(status.IsRecovering());
        Assert.False(status.IsStopped());
    }

    [Fact]
    public void HealthStatus_IsStopped_Should_Return_True_When_State_Is_Stopped()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            State = (int)MetalKernelState.Stopped
        };

        // Act & Assert
        Assert.False(status.IsHealthy());
        Assert.False(status.IsDegraded());
        Assert.False(status.IsFailed());
        Assert.False(status.IsRecovering());
        Assert.True(status.IsStopped());
    }

    [Fact]
    public void HealthStatus_IsHeartbeatStale_Should_Return_False_For_Recent_Timestamp()
    {
        // Arrange
        var status = MetalKernelHealthStatus.CreateInitialized();
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        bool isStale = status.IsHeartbeatStale(timeout);

        // Assert
        Assert.False(isStale);
    }

    [Fact]
    public void HealthStatus_IsHeartbeatStale_Should_Return_True_For_Old_Timestamp()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            LastHeartbeatTicks = DateTime.UtcNow.AddSeconds(-10).Ticks,
            State = (int)MetalKernelState.Healthy
        };
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        bool isStale = status.IsHeartbeatStale(timeout);

        // Assert
        Assert.True(isStale);
    }

    [Fact]
    public void HealthStatus_TimeSinceLastHeartbeat_Should_Return_Elapsed_Time()
    {
        // Arrange
        var secondsAgo = 3;
        var status = new MetalKernelHealthStatus
        {
            LastHeartbeatTicks = DateTime.UtcNow.AddSeconds(-secondsAgo).Ticks,
            State = (int)MetalKernelState.Healthy
        };

        // Act
        var elapsed = status.TimeSinceLastHeartbeat();

        // Assert
        Assert.InRange(elapsed.TotalSeconds, secondsAgo - 0.1, secondsAgo + 0.1);
    }

    [Fact]
    public void HealthStatus_Validate_Should_Return_True_For_Valid_Status()
    {
        // Arrange
        var status = MetalKernelHealthStatus.CreateInitialized();

        // Act & Assert
        Assert.True(status.Validate());
    }

    [Fact]
    public void HealthStatus_Validate_Should_Return_False_For_Negative_LastHeartbeatTicks()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            LastHeartbeatTicks = -1,
            State = (int)MetalKernelState.Healthy
        };

        // Act & Assert
        Assert.False(status.Validate());
    }

    [Fact]
    public void HealthStatus_Validate_Should_Return_False_For_Negative_FailedHeartbeats()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            FailedHeartbeats = -1,
            State = (int)MetalKernelState.Healthy
        };

        // Act & Assert
        Assert.False(status.Validate());
    }

    [Fact]
    public void HealthStatus_Validate_Should_Return_False_For_Negative_ErrorCount()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            ErrorCount = -1,
            State = (int)MetalKernelState.Healthy
        };

        // Act & Assert
        Assert.False(status.Validate());
    }

    [Fact]
    public void HealthStatus_Validate_Should_Return_False_For_Invalid_State()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            State = 99  // Invalid state
        };

        // Act & Assert
        Assert.False(status.Validate());
    }

    [Fact]
    public void HealthStatus_Validate_Should_Return_False_For_Negative_LastCheckpointId()
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            LastCheckpointId = -1,
            State = (int)MetalKernelState.Healthy
        };

        // Act & Assert
        Assert.False(status.Validate());
    }

    [Fact]
    public void HealthStatus_Equals_Should_Return_True_For_Identical_Statuses()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var status1 = new MetalKernelHealthStatus
        {
            LastHeartbeatTicks = ticks,
            FailedHeartbeats = 2,
            ErrorCount = 5,
            State = (int)MetalKernelState.Degraded,
            LastCheckpointId = 100
        };

        var status2 = new MetalKernelHealthStatus
        {
            LastHeartbeatTicks = ticks,
            FailedHeartbeats = 2,
            ErrorCount = 5,
            State = (int)MetalKernelState.Degraded,
            LastCheckpointId = 100
        };

        // Act & Assert
        Assert.True(status1.Equals(status2));
        Assert.True(status1 == status2);
        Assert.False(status1 != status2);
    }

    [Fact]
    public void HealthStatus_Equals_Should_Return_False_For_Different_Statuses()
    {
        // Arrange
        var status1 = MetalKernelHealthStatus.CreateEmpty();
        var status2 = new MetalKernelHealthStatus
        {
            ErrorCount = 5,
            State = (int)MetalKernelState.Degraded
        };

        // Act & Assert
        Assert.False(status1.Equals(status2));
        Assert.False(status1 == status2);
        Assert.True(status1 != status2);
    }

    [Fact]
    public void HealthStatus_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var status = MetalKernelHealthStatus.CreateInitialized();

        // Act
        int hash1 = status.GetHashCode();
        int hash2 = status.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HealthStatus_GetHashCode_Should_Differ_For_Different_Statuses()
    {
        // Arrange
        var status1 = MetalKernelHealthStatus.CreateEmpty();
        var status2 = new MetalKernelHealthStatus
        {
            ErrorCount = 5,
            State = (int)MetalKernelState.Degraded
        };

        // Act
        int hash1 = status1.GetHashCode();
        int hash2 = status2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HealthStatus_Size_Should_Be_32_Bytes()
    {
        // Act
        int size = Marshal.SizeOf<MetalKernelHealthStatus>();

        // Assert
        Assert.Equal(32, size);
    }

    [Theory]
    [InlineData(0, 0, MetalKernelState.Healthy)]
    [InlineData(5, 1, MetalKernelState.Healthy)]
    [InlineData(10, 2, MetalKernelState.Degraded)]
    [InlineData(15, 3, MetalKernelState.Failed)]
    public void HealthStatus_Should_Support_Various_Error_And_State_Combinations(int errorCount, int failedHeartbeats, MetalKernelState state)
    {
        // Arrange
        var status = new MetalKernelHealthStatus
        {
            ErrorCount = errorCount,
            FailedHeartbeats = failedHeartbeats,
            State = (int)state
        };

        // Act & Assert
        Assert.Equal(errorCount, status.ErrorCount);
        Assert.Equal(failedHeartbeats, status.FailedHeartbeats);
        Assert.Equal((int)state, status.State);

        // Validate based on expected state
        switch (state)
        {
            case MetalKernelState.Healthy:
                Assert.True(status.IsHealthy());
                break;
            case MetalKernelState.Degraded:
                Assert.True(status.IsDegraded());
                break;
            case MetalKernelState.Failed:
                Assert.True(status.IsFailed());
                break;
        }
    }
}
