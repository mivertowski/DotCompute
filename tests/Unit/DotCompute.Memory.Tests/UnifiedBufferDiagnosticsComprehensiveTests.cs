// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedBufferDiagnostics covering all diagnostic functionality.
/// Target: 50-60 tests covering 475-line diagnostics class.
/// Tests all diagnostic methods, validation, error handling, and edge cases.
/// </summary>
public sealed class UnifiedBufferDiagnosticsComprehensiveTests : IDisposable
{
    private readonly IUnifiedMemoryManager _mockMemoryManager;
    private readonly List<IDisposable> _disposables = [];

    public UnifiedBufferDiagnosticsComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Setup mock for memory operations
        _mockMemoryManager.AllocateDevice(Arg.Any<long>()).Returns(new DeviceMemory(new IntPtr(0x1000), 1024));
        _mockMemoryManager.When(x => x.CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
        _mockMemoryManager.CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region GetTransferStats Tests

    [Fact]
    public void GetTransferStats_WhenNewBuffer_ReturnsZeroStats()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var stats = buffer.GetTransferStats();

        // Assert
        stats.Should().NotBeNull();
        stats.HostToDeviceTransfers.Should().Be(0);
        stats.DeviceToHostTransfers.Should().Be(0);
        stats.TotalTransfers.Should().Be(0);
        stats.TotalTransferTimeMs.Should().Be(0);
        stats.AverageTransferTimeMs.Should().Be(0);
        stats.LastAccessTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        stats.CurrentState.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void GetTransferStats_AfterHostToDeviceTransfer_IncrementsCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnDevice(); // Trigger host-to-device transfer
        var stats = buffer.GetTransferStats();

        // Assert
        stats.HostToDeviceTransfers.Should().BeGreaterThanOrEqualTo(1);
        stats.TotalTransfers.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetTransferStats_AfterDeviceToHostTransfer_IncrementsCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();

        // Act
        buffer.EnsureOnHost(); // Trigger device-to-host transfer
        var stats = buffer.GetTransferStats();

        // Assert
        stats.DeviceToHostTransfers.Should().BeGreaterThanOrEqualTo(1);
        stats.TotalTransfers.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetTransferStats_AfterMultipleTransfers_CalculatesAverageCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();
        buffer.EnsureOnHost();
        var stats = buffer.GetTransferStats();

        // Assert
        stats.TotalTransfers.Should().BeGreaterThan(0);
        if (stats.TotalTransfers > 0)
        {
            stats.AverageTransferTimeMs.Should().Be(stats.TotalTransferTimeMs / stats.TotalTransfers);
        }
    }

    [Fact]
    public void GetTransferStats_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.GetTransferStats();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetTransferStats_LastAccessTime_UpdatesAfterOperations()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var initialStats = buffer.GetTransferStats();
        var initialTime = initialStats.LastAccessTime;

        Thread.Sleep(10);

        // Act
        buffer.EnsureOnDevice();
        var updatedStats = buffer.GetTransferStats();

        // Assert
        updatedStats.LastAccessTime.Should().BeAfter(initialTime);
    }

    #endregion

    #region ResetTransferStats Tests

    [Fact]
    public void ResetTransferStats_WhenCalled_ClearsAllStats()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();
        buffer.EnsureOnHost();

        // Act
        buffer.ResetTransferStats();
        var stats = buffer.GetTransferStats();

        // Assert
        stats.HostToDeviceTransfers.Should().Be(0);
        stats.DeviceToHostTransfers.Should().Be(0);
        stats.TotalTransfers.Should().Be(0);
        stats.TotalTransferTimeMs.Should().Be(0);
        stats.AverageTransferTimeMs.Should().Be(0);
    }

    [Fact]
    public void ResetTransferStats_UpdatesLastAccessTime()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var beforeReset = DateTime.UtcNow;

        // Act
        buffer.ResetTransferStats();
        var stats = buffer.GetTransferStats();

        // Assert
        stats.LastAccessTime.Should().BeOnOrAfter(beforeReset);
    }

    [Fact]
    public void ResetTransferStats_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.ResetTransferStats();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ResetTransferStats_MultipleTimes_KeepsStatsAtZero()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.ResetTransferStats();
        buffer.ResetTransferStats();
        buffer.ResetTransferStats();
        var stats = buffer.GetTransferStats();

        // Assert
        stats.TotalTransfers.Should().Be(0);
    }

    #endregion

    #region ValidateIntegrity Tests

    [Fact]
    public void ValidateIntegrity_WhenNewBuffer_ReturnsTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var isValid = buffer.ValidateIntegrity();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_WhenHostOnly_ValidatesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var isValid = buffer.ValidateIntegrity();

        // Assert
        isValid.Should().BeTrue();
        buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_WhenDeviceOnly_ValidatesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();

        // Act
        var isValid = buffer.ValidateIntegrity();

        // Assert
        isValid.Should().BeTrue();
        buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_WhenSynchronized_ValidatesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        var isValid = buffer.ValidateIntegrity();

        // Assert
        isValid.Should().BeTrue();
        buffer.IsOnHost.Should().BeTrue();
        buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_WhenHostDirty_ValidatesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();
        buffer.InvalidateDevice();

        // Act
        var isValid = buffer.ValidateIntegrity();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_WhenDeviceDirty_ValidatesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();
        buffer.InvalidateHost();

        // Act
        var isValid = buffer.ValidateIntegrity();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.ValidateIntegrity();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region GetDiagnosticInfo Tests

    [Fact]
    public void GetDiagnosticInfo_WhenCalled_ReturnsCompleteInformation()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var info = buffer.GetDiagnosticInfo();

        // Assert
        info.Should().NotBeNull();
        info.Length.Should().Be(100);
        info.SizeInBytes.Should().Be(400); // 100 * sizeof(int)
        info.ElementType.Should().Be("Int32");
        info.State.Should().Be(BufferState.HostOnly);
        info.IsDisposed.Should().BeFalse();
        info.MemoryInfo.Should().NotBeNull();
        info.TransferStats.Should().NotBeNull();
        info.IsIntegrityValid.Should().BeTrue();
    }

    [Fact]
    public void GetDiagnosticInfo_MemoryInfo_ContainsCorrectData()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var info = buffer.GetDiagnosticInfo();

        // Assert
        info.MemoryInfo.SizeInBytes.Should().BeGreaterThan(0);
        info.MemoryInfo.HostAllocated.Should().BeTrue();
    }

    [Fact]
    public void GetDiagnosticInfo_TransferStats_ContainsCorrectData()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var info = buffer.GetDiagnosticInfo();

        // Assert
        info.TransferStats.TotalTransfers.Should().BeGreaterThan(0);
        info.TransferStats.CurrentState.Should().NotBe(BufferState.Uninitialized);
    }

    [Fact]
    public void GetDiagnosticInfo_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.GetDiagnosticInfo();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetDiagnosticInfo_AfterMultipleOperations_ReflectsCurrentState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.Synchronize();
        buffer.InvalidateHost();

        // Act
        var info = buffer.GetDiagnosticInfo();

        // Assert
        info.IsIntegrityValid.Should().BeTrue();
        info.TransferStats.TotalTransfers.Should().BeGreaterThan(0);
    }

    #endregion

    #region CreateSnapshot Tests

    [Fact]
    public void CreateSnapshot_WhenCalled_ReturnsValidSnapshot()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var snapshot = buffer.CreateSnapshot();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Id.Should().NotBeEmpty();
        snapshot.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        snapshot.Length.Should().Be(100);
        snapshot.SizeInBytes.Should().Be(400);
        snapshot.State.Should().Be(BufferState.HostOnly);
        snapshot.IsOnHost.Should().BeTrue();
        snapshot.IsOnDevice.Should().BeFalse();
        snapshot.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void CreateSnapshot_MultipleTimes_GeneratesUniqueIds()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var snapshot1 = buffer.CreateSnapshot();
        var snapshot2 = buffer.CreateSnapshot();
        var snapshot3 = buffer.CreateSnapshot();

        // Assert
        snapshot1.Id.Should().NotBe(snapshot2.Id);
        snapshot2.Id.Should().NotBe(snapshot3.Id);
        snapshot1.Id.Should().NotBe(snapshot3.Id);
    }

    [Fact]
    public void CreateSnapshot_AfterStateChanges_ReflectsNewState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        var initialSnapshot = buffer.CreateSnapshot();

        // Act
        buffer.EnsureOnDevice();
        var afterDeviceSnapshot = buffer.CreateSnapshot();

        // Assert
        initialSnapshot.IsOnDevice.Should().BeFalse();
        afterDeviceSnapshot.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void CreateSnapshot_TracksTransferCount()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        var initialSnapshot = buffer.CreateSnapshot();

        // Act
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();
        buffer.EnsureOnHost();
        var finalSnapshot = buffer.CreateSnapshot();

        // Assert
        finalSnapshot.TransferCount.Should().BeGreaterThan(initialSnapshot.TransferCount);
    }

    [Fact]
    public void CreateSnapshot_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.CreateSnapshot();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region PerformDeepValidation Tests

    [Fact]
    public void PerformDeepValidation_WhenNewBuffer_ReturnsValidResult()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void PerformDeepValidation_ChecksBasicProperties()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PerformDeepValidation_ChecksSizeCalculation()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.IsValid.Should().BeTrue();
        // Size should match Length * sizeof(T)
    }

    [Fact]
    public void PerformDeepValidation_ChecksIntegrityValidation()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PerformDeepValidation_WarnsOnHighTransferCount()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Perform 101+ transfers to trigger warning
        for (int i = 0; i < 51; i++)
        {
            buffer.EnsureOnDevice();
            buffer.InvalidateHost();
            buffer.EnsureOnHost();
            buffer.InvalidateDevice();
        }

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("High transfer count"));
    }

    [Fact]
    public void PerformDeepValidation_WarnsOnLargeDuplicatedBuffer()
    {
        // Arrange - Create buffer > 1MB
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 300000); // ~1.2 MB
        _disposables.Add(buffer);
        buffer.Synchronize(); // Keep data in both host and device

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("Large buffer duplicated"));
    }

    [Fact]
    public void PerformDeepValidation_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.PerformDeepValidation();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WhenCalled_SetsDisposedFlag()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.Dispose();

        // Assert
        var act = () => buffer.GetTransferStats();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.Dispose();
        var act = () => buffer.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_FreesMemoryResources()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.EnsureOnDevice();

        // Act
        buffer.Dispose();

        // Assert
        _mockMemoryManager.Received().FreeDevice(Arg.Any<DeviceMemory>());
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_WhenCalled_SetsDisposedFlag()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        await buffer.DisposeAsync();

        // Assert
        var act = () => buffer.GetTransferStats();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_MultipleTimes_DoesNotThrow()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        await buffer.DisposeAsync();
        var act = async () => await buffer.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_FreesMemoryResources()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        await buffer.EnsureOnDeviceAsync();

        // Act
        await buffer.DisposeAsync();

        // Assert
        _mockMemoryManager.Received().FreeDevice(Arg.Any<DeviceMemory>());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CompleteWorkflow_DiagnosticsAndOperations_WorksTogether()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var initialSnapshot = buffer.CreateSnapshot();
        buffer.EnsureOnDevice();
        var afterDeviceSnapshot = buffer.CreateSnapshot();
        buffer.Synchronize();
        var stats = buffer.GetTransferStats();
        var validation = buffer.PerformDeepValidation();
        var diagnostics = buffer.GetDiagnosticInfo();

        // Assert
        initialSnapshot.TransferCount.Should().BeLessThan(afterDeviceSnapshot.TransferCount);
        stats.TotalTransfers.Should().BeGreaterThan(0);
        validation.IsValid.Should().BeTrue();
        diagnostics.IsIntegrityValid.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteWorkflow_AsyncDiagnostics_WorksTogether()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        await buffer.EnsureOnDeviceAsync();
        await buffer.SynchronizeAsync();
        var stats = buffer.GetTransferStats();
        var validation = buffer.PerformDeepValidation();

        // Assert
        stats.TotalTransfers.Should().BeGreaterThan(0);
        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MultipleSnapshots_TrackStateProgression()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var snapshots = new List<BufferSnapshot>();

        // Act
        snapshots.Add(buffer.CreateSnapshot());
        buffer.EnsureOnDevice();
        snapshots.Add(buffer.CreateSnapshot());
        buffer.Synchronize();
        snapshots.Add(buffer.CreateSnapshot());
        buffer.InvalidateHost();
        snapshots.Add(buffer.CreateSnapshot());

        // Assert
        snapshots.Should().HaveCount(4);
        snapshots[0].State.Should().Be(BufferState.HostOnly);
        snapshots[1].IsOnDevice.Should().BeTrue();
        snapshots[2].IsOnHost.Should().BeTrue();
        snapshots[2].IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void ValidationResult_IssuesAndWarnings_AreIndependent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var result = buffer.PerformDeepValidation();

        // Assert
        result.Issues.Should().NotBeNull();
        result.Warnings.Should().NotBeNull();
        // Warnings can exist even when IsValid is true
    }

    [Fact]
    public void ResetStats_AfterMultipleOperations_StartsClean()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnDevice();
        buffer.Synchronize();
        var beforeReset = buffer.GetTransferStats();
        buffer.ResetTransferStats();
        var afterReset = buffer.GetTransferStats();

        // Assert
        beforeReset.TotalTransfers.Should().BeGreaterThan(0);
        afterReset.TotalTransfers.Should().Be(0);
    }

    [Fact]
    public void DiagnosticInfo_ElementType_MatchesBufferType()
    {
        // Arrange - Test with different types
        var intBuffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        var floatBuffer = new UnifiedBuffer<float>(_mockMemoryManager, 10);
        var doubleBuffer = new UnifiedBuffer<double>(_mockMemoryManager, 10);
        _disposables.Add(intBuffer);
        _disposables.Add(floatBuffer);
        _disposables.Add(doubleBuffer);

        // Act
        var intInfo = intBuffer.GetDiagnosticInfo();
        var floatInfo = floatBuffer.GetDiagnosticInfo();
        var doubleInfo = doubleBuffer.GetDiagnosticInfo();

        // Assert
        intInfo.ElementType.Should().Be("Int32");
        floatInfo.ElementType.Should().Be("Single");
        doubleInfo.ElementType.Should().Be("Double");
    }

    [Fact]
    public void DiagnosticInfo_SizeInBytes_MatchesTypeSize()
    {
        // Arrange
        var intBuffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        var doubleBuffer = new UnifiedBuffer<double>(_mockMemoryManager, 10);
        _disposables.Add(intBuffer);
        _disposables.Add(doubleBuffer);

        // Act
        var intInfo = intBuffer.GetDiagnosticInfo();
        var doubleInfo = doubleBuffer.GetDiagnosticInfo();

        // Assert
        intInfo.SizeInBytes.Should().Be(40); // 10 * 4 bytes
        doubleInfo.SizeInBytes.Should().Be(80); // 10 * 8 bytes
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void GetTransferStats_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task<BufferTransferStats>[10];

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => buffer.GetTransferStats());
        }
        Task.WaitAll(tasks);

        // Assert
        tasks.Should().OnlyContain(t => t.Result != null);
    }

    [Fact]
    public void CreateSnapshot_ConcurrentCalls_GeneratesUniqueSnapshots()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task<BufferSnapshot>[10];

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => buffer.CreateSnapshot());
        }
        Task.WaitAll(tasks);

        var ids = tasks.Select(t => t.Result.Id).ToList();

        // Assert
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ValidateIntegrity_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task<bool>[10];

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => buffer.ValidateIntegrity());
        }
        Task.WaitAll(tasks);

        // Assert
        tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
    }

    #endregion
}
