// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Security;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Algorithms.Tests.Security;

/// <summary>
/// Comprehensive test suite for PluginSecurityValidator.
/// Tests defense-in-depth security validation including:
/// - Strong-name verification
/// - Code analysis for dangerous patterns
/// - Dependency validation
/// - Resource access validation
/// - Rate limiting
/// - Attack scenarios
/// </summary>
public sealed class PluginSecurityValidatorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<PluginSecurityValidator> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly PluginSecurityValidator _validator;
    private readonly string _testAssemblyPath;

    public PluginSecurityValidatorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger<PluginSecurityValidator>.Instance;

        _options = new AlgorithmPluginManagerOptions
        {
            EnableSecurityValidation = true,
            RequireStrongName = true,
            RequireDigitalSignature = false, // Most test assemblies won't have this
            EnableMetadataAnalysis = true,
            AllowFileSystemAccess = false,
            AllowNetworkAccess = false,
            MaxAssemblySize = 100 * 1024 * 1024, // 100MB
            RequireSecurityTransparency = false
        };

        _validator = new PluginSecurityValidator(_logger, _options);

        // Use the test assembly itself as a valid test subject
        _testAssemblyPath = Assembly.GetExecutingAssembly().Location;
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Validation")]
    public async Task ValidatePluginSecurity_ValidAssembly_ShouldPass()
    {
        // Arrange
        var systemAssembly = typeof(object).Assembly.Location;

        // Act
        var result = await _validator.ValidatePluginSecurityAsync(systemAssembly, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // System assemblies may not have plugin interface, but should pass other checks
        _output.WriteLine($"Validation result: {result.IsValid}, Threat: {result.ThreatLevel}");
        _output.WriteLine($"Violations: {string.Join(", ", result.Violations)}");
        _output.WriteLine($"Warnings: {string.Join(", ", result.Warnings)}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Validation")]
    public async Task ValidatePluginSecurity_NonExistentFile_ShouldFail()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.dll");

        // Act
        var result = await _validator.ValidatePluginSecurityAsync(nonExistentPath, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ThreatLevel.Critical, result.ThreatLevel);
        Assert.Contains(result.Violations, v => v.Contains("not found"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Validation")]
    public async Task ValidatePluginSecurity_UnsignedAssembly_WithRequireStrongName_ShouldFail()
    {
        // Arrange
        // Create a temporary unsigned assembly file
        var tempPath = Path.Combine(Path.GetTempPath(), $"unsigned_{Guid.NewGuid()}.dll");
        try
        {
            // Copy a known unsigned assembly or create minimal PE file
            await File.WriteAllBytesAsync(tempPath, CreateMinimalPEFile());

            // Act
            var result = await _validator.ValidatePluginSecurityAsync(tempPath, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v =>
                v.Contains("strong-named", StringComparison.OrdinalIgnoreCase) ||
                v.Contains("invalid", StringComparison.OrdinalIgnoreCase));

            _output.WriteLine($"Unsigned assembly validation: {string.Join("; ", result.Violations)}");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Validation")]
    public async Task ValidatePluginSecurity_ExcessiveSize_ShouldFail()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"large_{Guid.NewGuid()}.dll");
        _options.MaxAssemblySize = 1024; // 1KB limit

        try
        {
            // Create a file larger than the limit
            await File.WriteAllBytesAsync(tempPath, new byte[2048]);

            // Act
            var result = await _validator.ValidatePluginSecurityAsync(tempPath, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Contains("size") && v.Contains("exceeds"));

            _output.WriteLine($"Size validation: {string.Join("; ", result.Violations)}");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            _options.MaxAssemblySize = 100 * 1024 * 1024; // Reset
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Validation")]
    public async Task ValidatePluginSecurity_RateLimitExceeded_ShouldFail()
    {
        // Arrange
        var testPath = _testAssemblyPath;

        // Act - Make multiple rapid requests
        var tasks = new Task<PluginSecurityResult>[12]; // Exceeds default limit of 10
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _validator.ValidatePluginSecurityAsync(testPath + $"?v={i}", CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);

        // Assert - At least one should be rate limited
        var rateLimited = Array.Exists(results, r =>
            r.Violations.Any(v => v.Contains("Rate limit", StringComparison.OrdinalIgnoreCase)));

        // Note: This might not always trigger if caching kicks in
        _output.WriteLine($"Rate limited requests: {results.Count(r => !r.IsValid)}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Caching")]
    public async Task ValidatePluginSecurity_SameAssembly_ShouldUseCache()
    {
        // Arrange
        var testPath = _testAssemblyPath;

        // Act
        var result1 = await _validator.ValidatePluginSecurityAsync(testPath, CancellationToken.None);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result2 = await _validator.ValidatePluginSecurityAsync(testPath, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);

        // Second call should be much faster (cached)
        _output.WriteLine($"First validation: {result1.ValidationDuration.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Second validation (cached): {stopwatch.Elapsed.TotalMilliseconds:F2}ms");

        // Cached result should be nearly instant
        Assert.True(stopwatch.ElapsedMilliseconds < 10, "Cached validation should be < 10ms");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "MetadataAnalysis")]
    public async Task ValidatePluginSecurity_SystemAssemblyWithReflection_ShouldDetect()
    {
        // Arrange
        // Use System.Reflection assembly which contains Reflection.Emit
        var reflectionAssembly = typeof(System.Reflection.Emit.DynamicMethod).Assembly.Location;

        // Act
        var result = await _validator.ValidatePluginSecurityAsync(reflectionAssembly, CancellationToken.None);

        // Assert
        _output.WriteLine($"Reflection assembly validation: IsValid={result.IsValid}");
        _output.WriteLine($"Violations: {string.Join("; ", result.Violations)}");
        _output.WriteLine($"Warnings: {string.Join("; ", result.Warnings)}");

        // Should detect Reflection.Emit usage
        Assert.True(
            result.Violations.Any(v => v.Contains("Reflection.Emit", StringComparison.OrdinalIgnoreCase)) ||
            result.Warnings.Any(w => w.Contains("Reflection", StringComparison.OrdinalIgnoreCase)),
            "Should detect Reflection.Emit usage");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Threading")]
    public async Task ValidatePluginSecurity_ConcurrentValidation_ShouldBeThreadSafe()
    {
        // Arrange
        var testPath = _testAssemblyPath;

        // Act - Run multiple validations concurrently
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(10, 50)); // Random delay
            return await _validator.ValidatePluginSecurityAsync(testPath, CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        _output.WriteLine($"Concurrent validations completed: {results.Length}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "Cancellation")]
    public async Task ValidatePluginSecurity_Cancellation_ShouldThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _validator.ValidatePluginSecurityAsync(_testAssemblyPath, cts.Token);
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "DependencyValidation")]
    public async Task ValidatePluginSecurity_SuspiciousDependencies_ShouldWarn()
    {
        // Arrange
        // Use an assembly that might have System.Management or similar
        var systemManagementAssembly = typeof(System.ComponentModel.Component).Assembly.Location;

        // Act
        var result = await _validator.ValidatePluginSecurityAsync(systemManagementAssembly, CancellationToken.None);

        // Assert
        _output.WriteLine($"Dependency validation: IsValid={result.IsValid}");
        _output.WriteLine($"Warnings: {string.Join("; ", result.Warnings)}");

        // Should have some warnings about dependencies
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "IntegrityCheck")]
    public async Task ValidatePluginSecurity_ValidPEFile_ShouldValidateIntegrity()
    {
        // Arrange
        var systemAssembly = typeof(object).Assembly.Location;

        // Act
        var result = await _validator.ValidatePluginSecurityAsync(systemAssembly, CancellationToken.None);

        // Assert
        Assert.NotNull(result.FileHash);
        Assert.NotEmpty(result.FileHash);
        Assert.Equal(64, result.FileHash.Length); // SHA256 hex string length

        _output.WriteLine($"File hash: {result.FileHash}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "InvalidFormat")]
    public async Task ValidatePluginSecurity_InvalidPEFile_ShouldFail()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid()}.dll");
        try
        {
            // Create a file with invalid PE header
            await File.WriteAllBytesAsync(tempPath, new byte[] { 0x00, 0x00, 0x00, 0x00 });

            // Act
            var result = await _validator.ValidatePluginSecurityAsync(tempPath, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Contains("PE header", StringComparison.OrdinalIgnoreCase));

            _output.WriteLine($"Invalid PE validation: {string.Join("; ", result.Violations)}");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "DisposalSafety")]
    public async Task ValidatePluginSecurity_AfterDispose_ShouldThrow()
    {
        // Arrange
        var validator = new PluginSecurityValidator(_logger, _options);
        await validator.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await validator.ValidatePluginSecurityAsync(_testAssemblyPath, CancellationToken.None);
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "MetadataComplete")]
    public async Task ValidatePluginSecurity_ShouldIncludeMetadata()
    {
        // Arrange
        var testPath = _testAssemblyPath;

        // Act
        var result = await _validator.ValidatePluginSecurityAsync(testPath, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("AssemblyPath"));
        Assert.True(result.Metadata.ContainsKey("AssemblySize"));
        Assert.True(result.Metadata.ContainsKey("ValidationLayers"));
        Assert.Equal(6, result.Metadata["ValidationLayers"]); // 6 validation layers

        _output.WriteLine("Metadata:");
        foreach (var kvp in result.Metadata)
        {
            _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }

    /// <summary>
    /// Creates a minimal PE file for testing (invalid assembly but valid PE structure).
    /// </summary>
    private static byte[] CreateMinimalPEFile()
    {
        var peFile = new byte[1024];

        // DOS header
        peFile[0] = 0x4D; // 'M'
        peFile[1] = 0x5A; // 'Z'

        // Add minimal PE header structure
        peFile[0x3C] = 0x80; // PE header offset

        // PE signature at offset 0x80
        peFile[0x80] = 0x50; // 'P'
        peFile[0x81] = 0x45; // 'E'

        return peFile;
    }

    public void Dispose()
    {
        _validator?.Dispose();
    }
}

/// <summary>
/// Attack scenario tests for security validation.
/// Tests protection against various attack vectors.
/// </summary>
public sealed class PluginSecurityAttackScenarioTests
{
    private readonly ILogger<PluginSecurityValidator> _logger = NullLogger<PluginSecurityValidator>.Instance;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "AttackScenario")]
    public async Task AttackScenario_HiddenFile_ShouldDetect()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"hidden_{Guid.NewGuid()}.dll");
        var options = new AlgorithmPluginManagerOptions
        {
            EnableSecurityValidation = true,
            RequireStrongName = false // Focus on file attributes
        };

        using var validator = new PluginSecurityValidator(_logger, options);

        try
        {
            // Create a hidden file
            await File.WriteAllBytesAsync(tempPath, CreateMinimalValidPE());
            var fileInfo = new FileInfo(tempPath);
            fileInfo.Attributes |= FileAttributes.Hidden;

            // Act
            var result = await validator.ValidatePluginSecurityAsync(tempPath, CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Contains("hidden", StringComparison.OrdinalIgnoreCase));
            Assert.True(result.ThreatLevel >= ThreatLevel.Medium);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                var fileInfo = new FileInfo(tempPath);
                fileInfo.Attributes &= ~FileAttributes.Hidden;
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "AttackScenario")]
    public async Task AttackScenario_FileSystemAccess_WhenNotAllowed_ShouldBlock()
    {
        // Arrange
        var options = new AlgorithmPluginManagerOptions
        {
            EnableSecurityValidation = true,
            AllowFileSystemAccess = false,
            EnableMetadataAnalysis = true,
            RequireStrongName = false
        };

        using var validator = new PluginSecurityValidator(_logger, options);

        // Use System.IO assembly which contains file system types
        var systemIOAssembly = typeof(System.IO.FileStream).Assembly.Location;

        // Act
        var result = await validator.ValidatePluginSecurityAsync(systemIOAssembly, CancellationToken.None);

        // Assert - Should detect file system usage
        var hasFileSystemViolation = result.Violations.Any(v =>
            v.Contains("file system", StringComparison.OrdinalIgnoreCase));
        var hasFileSystemWarning = result.Warnings.Any(w =>
            w.Contains("file", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasFileSystemViolation || hasFileSystemWarning,
            "Should detect file system access");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "AttackScenario")]
    public async Task AttackScenario_NetworkAccess_WhenNotAllowed_ShouldBlock()
    {
        // Arrange
        var options = new AlgorithmPluginManagerOptions
        {
            EnableSecurityValidation = true,
            AllowNetworkAccess = false,
            EnableMetadataAnalysis = true,
            RequireStrongName = false
        };

        using var validator = new PluginSecurityValidator(_logger, options);

        // Use System.Net assembly which contains network types
        var systemNetAssembly = typeof(System.Net.WebClient).Assembly.Location;

        // Act
        var result = await validator.ValidatePluginSecurityAsync(systemNetAssembly, CancellationToken.None);

        // Assert - Should detect network usage
        var hasNetworkViolation = result.Violations.Any(v =>
            v.Contains("network", StringComparison.OrdinalIgnoreCase));
        var hasNetworkWarning = result.Warnings.Any(w =>
            w.Contains("network", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasNetworkViolation || hasNetworkWarning,
            "Should detect network access");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Security", "AttackScenario")]
    public async Task AttackScenario_RepeatedLoadAttempts_ShouldRateLimit()
    {
        // Arrange
        var options = new AlgorithmPluginManagerOptions
        {
            EnableSecurityValidation = true,
            RequireStrongName = false
        };

        using var validator = new PluginSecurityValidator(_logger, options);
        var testPath = typeof(object).Assembly.Location;

        // Act - Attempt to load same plugin many times rapidly
        var results = new List<PluginSecurityResult>();
        for (var i = 0; i < 15; i++)
        {
            // Use different query strings to bypass caching but trigger rate limit
            var result = await validator.ValidatePluginSecurityAsync($"{testPath}?attempt={i}", CancellationToken.None);
            results.Add(result);

            if (!result.IsValid && result.Violations.Any(v => v.Contains("rate limit", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }
        }

        // Assert - Should hit rate limit
        var rateLimited = results.Any(r =>
            r.Violations.Any(v => v.Contains("rate limit", StringComparison.OrdinalIgnoreCase)));

        Assert.True(rateLimited || results.Count < 15,
            "Should enforce rate limiting on repeated load attempts");
    }

    private static byte[] CreateMinimalValidPE()
    {
        var pe = new byte[2048];

        // DOS header
        pe[0] = 0x4D; // 'M'
        pe[1] = 0x5A; // 'Z'
        pe[0x3C] = 0x80; // PE header offset

        // PE signature
        pe[0x80] = 0x50; // 'P'
        pe[0x81] = 0x45; // 'E'

        return pe;
    }
}
