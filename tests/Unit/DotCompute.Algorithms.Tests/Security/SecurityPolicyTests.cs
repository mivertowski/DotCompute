// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types.Security;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Security
{

/// <summary>
/// Comprehensive tests for SecurityPolicy functionality.
/// </summary>
public sealed class SecurityPolicyTests : IDisposable
{
    private readonly SecurityPolicy _securityPolicy;

    public SecurityPolicyTests()
    {
        _securityPolicy = new SecurityPolicy(NullLogger<SecurityPolicy>.Instance);
    }

    [Fact]
    public void SecurityPolicy_DefaultConfiguration_ShouldHaveSecureDefaults()
    {
        // Arrange & Act
        var policy = new SecurityPolicy(NullLogger<SecurityPolicy>.Instance);

        // Assert
        policy.RequireDigitalSignature.Should().BeTrue();
        policy.RequireStrongName.Should().BeTrue();
        policy.MinimumSecurityLevel.Should().Be(SecurityLevel.Medium);
        policy.EnableMetadataAnalysis.Should().BeTrue();
        policy.EnableMalwareScanning.Should().BeTrue();
        policy.MaxAssemblySize.Should().Be(50 * 1024 * 1024);
        policy.TrustedStoreLocation.Should().Be(StoreLocation.LocalMachine);
        policy.TrustedStoreName.Should().Be(StoreName.TrustedPublisher);
    }

    [Fact]
    public void AddSecurityRule_ValidRule_ShouldAddSuccessfully()
    {
        // Arrange
        var rule = new TestSecurityRule { IsAllowed = true, SecurityLevel = SecurityLevel.High };

        // Act
        _securityPolicy.AddSecurityRule("TestRule", rule);

        // Assert
        var context = new SecurityEvaluationContext { AssemblyPath = "test.dll" };
        var result = _securityPolicy.EvaluateRules(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void AddSecurityRule_NullRule_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            _securityPolicy.AddSecurityRule("TestRule", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddSecurityRule_InvalidRuleName_ShouldThrowArgumentException(string? ruleName)
    {
        // Arrange
        var rule = new TestSecurityRule();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _securityPolicy.AddSecurityRule(ruleName!, rule));
    }

    [Fact]
    public void RemoveSecurityRule_ExistingRule_ShouldReturnTrue()
    {
        // Arrange
        var rule = new TestSecurityRule();
        _securityPolicy.AddSecurityRule("TestRule", rule);

        // Act
        var result = _securityPolicy.RemoveSecurityRule("TestRule");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RemoveSecurityRule_NonExistingRule_ShouldReturnFalse()
    {
        // Act
        var result = _securityPolicy.RemoveSecurityRule("NonExistingRule");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EvaluateRules_MultipleRules_ShouldEvaluateAll()
    {
        // Arrange
        var allowedRule = new TestSecurityRule { IsAllowed = true, SecurityLevel = SecurityLevel.High };
        var deniedRule = new TestSecurityRule { IsAllowed = false, SecurityLevel = SecurityLevel.Low };
        
        _securityPolicy.AddSecurityRule("AllowedRule", allowedRule);
        _securityPolicy.AddSecurityRule("DeniedRule", deniedRule);

        var context = new SecurityEvaluationContext { AssemblyPath = "test.dll" };

        // Act
        var result = _securityPolicy.EvaluateRules(context);

        // Assert
        result.IsAllowed.Should().BeFalse(); // Should be false because one rule denies
        result.SecurityLevel.Should().Be(SecurityLevel.Low); // Should be lowest level
        result.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public void EvaluateRules_SecurityLevelBelowMinimum_ShouldDeny()
    {
        // Arrange
        _securityPolicy.MinimumSecurityLevel = SecurityLevel.High;
        var rule = new TestSecurityRule { IsAllowed = true, SecurityLevel = SecurityLevel.Medium };
        _securityPolicy.AddSecurityRule("TestRule", rule);

        var context = new SecurityEvaluationContext { AssemblyPath = "test.dll" };

        // Act
        var result = _securityPolicy.EvaluateRules(context);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Violations.Contain(v => v.Contains("minimum security level"));
    }

    [Theory]
    [InlineData("ABC123DEF456")]
    [InlineData("abcdef123456")]
    public void AddTrustedPublisher_ValidThumbprint_ShouldAddSuccessfully(string thumbprint)
    {
        // Act
        _securityPolicy.AddTrustedPublisher(thumbprint);

        // Assert
        _securityPolicy.IsTrustedPublisherthumbprint.Should().BeTrue();
        _securityPolicy.IsTrustedPublisher(thumbprint.ToUpperInvariant()).Should().BeTrue();
        _securityPolicy.IsTrustedPublisher(thumbprint.ToLowerInvariant()).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddTrustedPublisher_InvalidThumbprint_ShouldThrowArgumentException(string? thumbprint)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _securityPolicy.AddTrustedPublisher(thumbprint!));
    }

    [Fact]
    public void RemoveTrustedPublisher_ExistingPublisher_ShouldReturnTrue()
    {
        // Arrange
        const string thumbprint = "ABC123DEF456";
        _securityPolicy.AddTrustedPublisher(thumbprint);

        // Act
        var result = _securityPolicy.RemoveTrustedPublisher(thumbprint);

        // Assert
        Assert.True(result);
        _securityPolicy.IsTrustedPublisher(thumbprint).Should().BeFalse();
    }

    [Fact]
    public void RemoveTrustedPublisher_NonExistingPublisher_ShouldReturnFalse()
    {
        // Act
        var result = _securityPolicy.RemoveTrustedPublisher("NonExistingThumbprint");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsTrustedPublisher_InvalidThumbprint_ShouldReturnFalse(string? thumbprint)
    {
        // Act
        var result = _securityPolicy.IsTrustedPublisher(thumbprint!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task LoadPolicyFromFile_ValidJson_ShouldLoadConfiguration()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var config = new SecurityPolicyConfiguration
        {
            RequireDigitalSignature = false,
            RequireStrongName = false,
            MinimumSecurityLevel = SecurityLevel.Low,
            EnableMetadataAnalysis = false,
            EnableMalwareScanning = false,
            MaxAssemblySize = 100 * 1024 * 1024,
            TrustedPublishers = ["TestThumbprint1", "TestThumbprint2"],
            BlockedAssemblies = ["BadAssembly.dll"],
            DirectoryPolicies = new Dictionary<string, SecurityLevel>
            {
                { "/trusted", SecurityLevel.High },
                { "/untrusted", SecurityLevel.Low }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config);
        await File.WriteAllTextAsync(tempFile, json);

        try
        {
            // Act
            await _securityPolicy.LoadPolicyFromFileAsync(tempFile);

            // Assert
            _securityPolicy.RequireDigitalSignature.Should().BeFalse();
            _securityPolicy.RequireStrongName.Should().BeFalse();
            _securityPolicy.MinimumSecurityLevel.Should().Be(SecurityLevel.Low);
            _securityPolicy.EnableMetadataAnalysis.Should().BeFalse();
            _securityPolicy.EnableMalwareScanning.Should().BeFalse();
            _securityPolicy.MaxAssemblySize.Should().Be(100 * 1024 * 1024);
            _securityPolicy.Assert.Contains("TESTTHUMBPRINT1", TrustedPublishers);
            _securityPolicy.Assert.Contains("TESTTHUMBPRINT2", TrustedPublishers);
            _securityPolicy.Assert.Contains("BADASSEMBLY.DLL", BlockedAssemblies);
            _securityPolicy.DirectoryPolicies.Should().ContainKey("/trusted");
            _securityPolicy.DirectoryPolicies.Should().ContainKey("/untrusted");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadPolicyFromFile_NonExistentFile_ShouldNotThrow()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        await _securityPolicy.LoadPolicyFromFileAsync(nonExistentFile); // Should not throw
    }

    [Fact]
    public async Task LoadPolicyFromFile_InvalidJson_ShouldThrowException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "{ invalid json }");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
                _securityPolicy.LoadPolicyFromFileAsync(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SavePolicyToFile_ValidConfiguration_ShouldSaveSuccessfully()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _securityPolicy.RequireDigitalSignature = false;
        _securityPolicy.AddTrustedPublisher("TestThumbprint");

        try
        {
            // Act
            await _securityPolicy.SavePolicyToFileAsync(tempFile);

            // Assert
            var savedContent = await File.ReadAllTextAsync(tempFile);
            Assert.NotEmpty(savedContent);
            Assert.Contains("requireDigitalSignature", savedContent);
            Assert.Contains("TestThumbprint", savedContent);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void EvaluateRules_NullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            _securityPolicy.EvaluateRules(null!));
    }

    [Fact]
    public void SecurityEvaluationContext_Properties_ShouldSetCorrectly()
    {
        // Arrange & Act
        var context = new SecurityEvaluationContext
        {
            AssemblyPath = "test.dll",
            AssemblyBytes = [1, 2, 3],
            Certificate = null,
            StrongNameKey = [4, 5, 6]
        };

        context.Metadata["TestKey"] = "TestValue";

        // Assert
        context.AssemblyPath.Should().Be("test.dll");
        context.AssemblyBytes.Equal([1, 2, 3]);
        contextCertificate.Should().BeNull;
        context.StrongNameKey.Equal([4, 5, 6]);
        context.Metadata.Should().ContainKey("TestKey");
        context.Metadata["TestKey"].Should().Be("TestValue");
    }

    [Fact]
    public void SecurityEvaluationResult_DefaultState_ShouldBeCorrect()
    {
        // Arrange & Act
        var result = new SecurityEvaluationResult();

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.SecurityLevel.Should().Be(SecurityLevel.High);
        resultViolations.Should().BeEmpty();
        resultWarnings.Should().BeEmpty();
        resultMetadata.Should().BeEmpty();
    }

    public void Dispose()
    {
        // No specific cleanup needed as SecurityPolicy doesn't implement IDisposable
    }

    /// <summary>
    /// Test security rule for unit testing.
    /// </summary>
    private sealed class TestSecurityRule : SecurityRule
    {
        public bool IsAllowed { get; set; } = true;
        public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.High;
        public List<string> TestViolations { get; set; } = [];
        public List<string> TestWarnings { get; set; } = [];

        public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
        {
            return new SecurityEvaluationResult
            {
                IsAllowed = IsAllowed,
                SecurityLevel = SecurityLevel,
                Violations = [.. TestViolations],
                Warnings = [.. TestWarnings]
            };
        }
    }
}
}
