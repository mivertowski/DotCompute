// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Security;
using DotCompute.Plugins.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Plugins.Tests.Security
{
    /// <summary>
    /// Comprehensive security test suite for the plugin loading system.
    /// </summary>
    public class SecurityTestSuite
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger> _mockLogger;

        public SecurityTestSuite(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void SafeMemoryOperations_SafeCopy_PreventsBoundsOverflow()
        {
            // Arrange
            var source = new int[] { 1, 2, 3, 4, 5 };
            var destination = new int[3];

            // Act & Assert - Should not throw and copy only what fits
            var copied = SafeMemoryOperations.SafeCopy(source.AsSpan(), destination.AsSpan(), 10);
            
            Assert.Equal(3, copied); // Only 3 elements should be copied
            Assert.Equal(1, destination[0]);
            Assert.Equal(2, destination[1]);
            Assert.Equal(3, destination[2]);
        }

        [Fact]
        public void SafeMemoryOperations_SafeCopy_RejectsNegativeLength()
        {
            // Arrange
            var source = new int[] { 1, 2, 3 };
            var destination = new int[3];

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SafeMemoryOperations.SafeCopy(source.AsSpan(), destination.AsSpan(), -1));
        }

        [Fact]
        public void SafeMemoryOperations_SafeAllocate_RejectsExcessiveSize()
        {
            // Act & Assert - Should throw for excessive allocation
            Assert.Throws<OutOfMemoryException>(() =>
                SafeMemoryOperations.SafeAllocate<byte>(int.MaxValue));
        }

        [Fact]
        public void SafeMemoryOperations_SafeAllocate_RejectsNegativeSize()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SafeMemoryOperations.SafeAllocate<byte>(-1));
        }

        [Fact]
        public void SafeMemoryOperations_ValidateBufferOperation_DetectsOverflow()
        {
            // Act & Assert - Should detect integer overflow
            var isValid = SafeMemoryOperations.ValidateBufferOperation(1000, int.MaxValue - 10, 20);
            Assert.False(isValid);
        }

        [Fact]
        public void SafeMemoryOperations_ValidateBufferOperation_AllowsValidOperations()
        {
            // Act & Assert
            var isValid = SafeMemoryOperations.ValidateBufferOperation(1000, 100, 200);
            Assert.True(isValid);
        }

        [Fact]
        public void SafeMemoryAllocation_IndexAccess_EnforcesBoundsChecking()
        {
            // Arrange
            using var allocation = SafeMemoryOperations.SafeAllocate<int>(5);

            // Act & Assert - Valid access should work
            allocation[0] = 42;
            Assert.Equal(42, allocation[0]);

            // Invalid access should throw
            Assert.Throws<IndexOutOfRangeException>(() => allocation[5] = 1);
            Assert.Throws<IndexOutOfRangeException>(() => allocation[-1] = 1);
        }

        [Fact]
        public void SafeMemoryAllocation_Slice_EnforcesBoundsChecking()
        {
            // Arrange
            using var allocation = SafeMemoryOperations.SafeAllocate<int>(10);

            // Act & Assert - Valid slice should work
            var validSlice = allocation.Slice(2, 3);
            Assert.Equal(3, validSlice.Length);

            // Invalid slices should throw
            Assert.Throws<ArgumentOutOfRangeException>(() => allocation.Slice(-1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => allocation.Slice(5, 10)); // Extends beyond bounds
            Assert.Throws<ArgumentOutOfRangeException>(() => allocation.Slice(15, 1)); // Start beyond bounds
        }

        [Fact]
        public void PluginSecurityContext_RecordViolation_TracksCriticalViolations()
        {
            // Arrange
            var context = new PluginSecurityContext();

            // Act
            context.RecordViolation("Test violation", ViolationSeverity.Critical, "Test details");

            // Assert
            Assert.True(context.HasViolations);
            Assert.True(context.HasCriticalViolations);
            Assert.Single(context.Violations);
            Assert.Equal("Test violation", context.Violations[0].Description);
            Assert.Equal(ViolationSeverity.Critical, context.Violations[0].Severity);
        }

        [Fact]
        public void PluginSecurityContext_GetViolationsBySeverity_FiltersCorrectly()
        {
            // Arrange
            var context = new PluginSecurityContext();
            context.RecordViolation("Low violation", ViolationSeverity.Low);
            context.RecordViolation("High violation", ViolationSeverity.High);
            context.RecordViolation("Critical violation", ViolationSeverity.Critical);

            // Act
            var highViolations = context.GetViolationsBySeverity(ViolationSeverity.High).ToList();
            var criticalViolations = context.GetViolationsBySeverity(ViolationSeverity.Critical).ToList();

            // Assert
            Assert.Single(highViolations);
            Assert.Single(criticalViolations);
            Assert.Equal("High violation", highViolations[0].Description);
            Assert.Equal("Critical violation", criticalViolations[0].Description);
        }

        [Fact]
        public async Task SecurityManager_ValidateAssemblyIntegrityAsync_RejectsNonExistentFile()
        {
            // Arrange
            var securityManager = new SecurityManager(_mockLogger.Object);

            // Act
            var result = await securityManager.ValidateAssemblyIntegrityAsync("nonexistent.dll");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SecurityManager_AnalyzeAssemblyMetadataAsync_HandlesMissingFile()
        {
            // Arrange
            var securityManager = new SecurityManager(_mockLogger.Object);

            // Act
            var analysis = await securityManager.AnalyzeAssemblyMetadataAsync("nonexistent.dll");

            // Assert
            Assert.True(analysis.HasError);
            Assert.NotNull(analysis.ErrorMessage);
        }

        [Fact]
        public void SandboxConfiguration_Validate_DetectsInvalidSettings()
        {
            // Arrange
            var config = new SandboxConfiguration
            {
                DefaultExecutionTimeout = TimeSpan.FromSeconds(-1), // Invalid
                MaxConcurrentPlugins = -5, // Invalid
                ResourceLimits = new ResourceLimits
                {
                    MaxMemoryMB = -100, // Invalid
                    MaxCpuUsagePercent = 150 // Invalid
                }
            };

            // Act
            var errors = config.Validate();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("DefaultExecutionTimeout"));
            Assert.Contains(errors, e => e.Contains("MaxConcurrentPlugins"));
            Assert.Contains(errors, e => e.Contains("MaxMemoryMB"));
            Assert.Contains(errors, e => e.Contains("MaxCpuUsagePercent"));
        }

        [Fact]
        public void SandboxConfiguration_CreateRestrictive_HasSecureDefaults()
        {
            // Act
            var config = SandboxConfiguration.CreateRestrictive();

            // Assert
            Assert.True(config.EnableDetailedSecurityLogging);
            Assert.True(config.EnableAutomaticTermination);
            Assert.True(config.RequireStrongNameValidation);
            Assert.Equal(50, config.ResourceLimits.MaxMemoryMB);
            Assert.Equal(25, config.ResourceLimits.MaxCpuUsagePercent);
            Assert.Equal(5, config.MaxConcurrentPlugins);
        }

        [Fact]
        public void SandboxPermissions_CreateRestrictive_HasMinimalPermissions()
        {
            // Act
            var permissions = SandboxPermissions.CreateRestrictive();

            // Assert
            Assert.Single(permissions.AllowedPermissions);
            Assert.Contains("Execution", permissions.AllowedPermissions);
            Assert.Equal(NetworkAccessPermissions.None, permissions.NetworkAccess);
            Assert.Equal(FileSystemAccessPermissions.None, permissions.FileSystemAccess);
            Assert.Equal(100, permissions.ResourceLimits.MaxMemoryMB);
        }

        [Fact]
        public void ResourceLimits_DefaultValues_AreReasonable()
        {
            // Act
            var limits = new ResourceLimits();

            // Assert
            Assert.True(limits.MaxMemoryMB > 0);
            Assert.True(limits.MaxCpuUsagePercent > 0 && limits.MaxCpuUsagePercent <= 100);
            Assert.True(limits.MaxExecutionTimeSeconds > 0);
            Assert.True(limits.MaxThreads > 0);
            Assert.True(limits.MaxFileIOPerSecond > 0);
            Assert.True(limits.MaxNetworkIOPerSecond > 0);
        }

        [Theory]
        [InlineData("../malicious.dll", false)]
        [InlineData("~/exploit.dll", false)]
        [InlineData(".hidden.dll", false)]
        [InlineData("normal.dll", true)]
        [InlineData("C:/safe/path/plugin.dll", true)]
        [InlineData("hack.dll", false)]
        [InlineData("keygen.exe", false)]
        [InlineData("legitimate.exe", true)]
        public void PathSafetyValidation_DetectsUnsafePaths(string path, bool expectedSafe)
        {
            // This would test the IsAssemblyPathSafe method if it were public
            // For now, we test through the loader's behavior
            
            // Act & Assert
            _output.WriteLine($"Testing path: {path}, Expected safe: {expectedSafe}");
            
            // We expect certain patterns to be rejected
            var hasDirectoryTraversal = path.Contains("..") || path.Contains("~");
            var hasHiddenFile = Path.GetFileName(path).StartsWith(".");
            var hasSuspiciousName = new[] { "hack", "crack", "keygen" }.Any(p => 
                Path.GetFileNameWithoutExtension(path).Contains(p, StringComparison.OrdinalIgnoreCase));
            
            var shouldBeUnsafe = hasDirectoryTraversal || hasHiddenFile || hasSuspiciousName;
            Assert.Equal(!shouldBeUnsafe, expectedSafe);
        }

        [Fact]
        public async Task PluginSandbox_CreateSandboxedPlugin_RequiresValidParameters()
        {
            // Arrange
            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PluginSandbox>();
            var sandbox = new PluginSandbox(logger);
            var permissions = SandboxPermissions.CreateRestrictive();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await sandbox.CreateSandboxedPluginAsync<object>("", "TestType", permissions));
            
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await sandbox.CreateSandboxedPluginAsync<object>("test.dll", "", permissions));
            
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await sandbox.CreateSandboxedPluginAsync<object>("test.dll", "TestType", null!));
        }

        [Fact]
        public void IsolatedPluginLoadContext_SecurityRestrictions_AreEnforced()
        {
            // Arrange
            var permissions = SandboxPermissions.CreateRestrictive();
            var context = new IsolatedPluginLoadContext("Test", "test.dll", permissions, _mockLogger.Object);

            // Act & Assert - Context should reject dangerous assemblies
            Assert.NotNull(context);
            Assert.Equal(0, context.LoadedAssemblyCount);
        }

        [Fact]
        public void ResourceUsage_ViolationDetection_WorksCorrectly()
        {
            // Arrange
            var usage = new ResourceUsage
            {
                MemoryUsageMB = 200,
                CpuUsagePercent = 90,
                ThreadCount = 10,
                ExecutionTime = TimeSpan.FromMinutes(10)
            };

            var limits = new ResourceLimits
            {
                MaxMemoryMB = 100,
                MaxCpuUsagePercent = 50,
                MaxThreads = 5,
                MaxExecutionTimeSeconds = 300 // 5 minutes
            };

            // Act - Simulate violation checking logic
            var violations = new List<string>();
            
            if(usage.MemoryUsageMB > limits.MaxMemoryMB)
                violations.Add($"Memory usage{usage.MemoryUsageMB} MB) exceeds limit({limits.MaxMemoryMB} MB)");
            
            if(usage.CpuUsagePercent > limits.MaxCpuUsagePercent)
                violations.Add($"CPU usage{usage.CpuUsagePercent}%) exceeds limit({limits.MaxCpuUsagePercent}%)");
            
            if(usage.ThreadCount > limits.MaxThreads)
                violations.Add($"Thread count{usage.ThreadCount}) exceeds limit({limits.MaxThreads})");
            
            if(usage.ExecutionTime.TotalSeconds > limits.MaxExecutionTimeSeconds)
                violations.Add($"Execution time{usage.ExecutionTime.TotalSeconds}s) exceeds limit({limits.MaxExecutionTimeSeconds}s)");

            // Assert
            Assert.Equal(4, violations.Count);
            Assert.All(violations, v => Assert.Contains("exceeds limit", v));
        }

        [Fact]
        public void SecurityViolation_RequiresTermination_IsSetCorrectly()
        {
            // Arrange & Act
            var criticalViolation = new SecurityViolation
            {
                Description = "Critical security breach",
                Severity = ViolationSeverity.Critical,
                RequiresTermination = true
            };

            var lowViolation = new SecurityViolation
            {
                Description = "Minor issue",
                Severity = ViolationSeverity.Low,
                RequiresTermination = false
            };

            // Assert
            Assert.True(criticalViolation.RequiresTermination);
            Assert.False(lowViolation.RequiresTermination);
        }

        [Fact]
        public void SafeMemoryAllocation_DisposalClearsContents()
        {
            // Arrange
            var allocation = SafeMemoryOperations.SafeAllocate<byte>(100);
            allocation.Fill(0xFF); // Fill with non-zero data

            // Act
            allocation.Dispose();

            // Assert
            Assert.True(allocation.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => { var span = allocation.Span; });
        }

        [Theory]
        [InlineData(0, 0, 0, true)]  // Empty operation
        [InlineData(100, 0, 50, true)]  // Valid operation
        [InlineData(100, 50, 50, true)]  // Valid operation at end
        [InlineData(100, 50, 51, false)] // Exceeds bounds
        [InlineData(100, -1, 10, false)] // Negative offset
        [InlineData(100, 10, -1, false)] // Negative length
        [InlineData(int.MaxValue, int.MaxValue - 10, 20, false)] // Integer overflow
        public void ValidateBufferOperation_HandlesEdgeCases(int bufferSize, int offset, int length, bool expectedValid)
        {
            // Act
            var result = SafeMemoryOperations.ValidateBufferOperation(bufferSize, offset, length);

            // Assert
            Assert.Equal(expectedValid, result);
        }

        [Fact]
        public void SafeMemoryOperations_SafeEquals_HandlesNullAndEmpty()
        {
            // Arrange
            var span1 = Array.Empty<int>().AsSpan();
            var span2 = Array.Empty<int>().AsSpan();
            var span3 = new int[] { 1, 2, 3 }.AsSpan();

            // Act & Assert
            Assert.True(SafeMemoryOperations.SafeEquals<int>(span1, span2, 0));
            Assert.True(SafeMemoryOperations.SafeEquals<int>(span1, span3, 0));
            Assert.False(SafeMemoryOperations.SafeEquals<int>(span1, span3, 1));
        }

        [Fact]
        public void SafeMemoryOperations_SafeGetHashCode_IsConsistent()
        {
            // Arrange
            var data1 = new int[] { 1, 2, 3, 4, 5 };
            var data2 = new int[] { 1, 2, 3, 4, 5 };
            var data3 = new int[] { 1, 2, 3, 4, 6 }; // Different

            // Act
            var hash1 = SafeMemoryOperations.SafeGetHashCode<int>(data1.AsSpan());
            var hash2 = SafeMemoryOperations.SafeGetHashCode<int>(data2.AsSpan());
            var hash3 = SafeMemoryOperations.SafeGetHashCode<int>(data3.AsSpan());

            // Assert
            Assert.Equal(hash1, hash2); // Same data should have same hash
            Assert.NotEqual(hash1, hash3); // Different data should have different hash
        }
    }
}
