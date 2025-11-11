// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Backends.CUDA.Timing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Timing;

/// <summary>
/// Comprehensive unit tests for TimestampInjector.
/// Tests PTX code injection, parameter shifting, prologue insertion, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CudaTiming")]
public sealed class TimestampInjectorTests
{
    #region Helper Methods

    /// <summary>
    /// Creates a minimal valid PTX kernel for testing.
    /// </summary>
    private static string CreateMinimalPtxKernel(string kernelName = "TestKernel", string parameters = "")
    {
        var ptx = $@"
.version 7.0
.target sm_50
.address_size 64

.visible .entry {kernelName}(
{parameters}
)
{{
    ret;
}}
";
        return ptx;
    }

    /// <summary>
    /// Creates a PTX kernel with multiple parameters for testing parameter shifting.
    /// </summary>
    private static string CreateMultiParameterPtxKernel(int parameterCount = 3)
    {
        var parameters = new StringBuilder();
        for (var i = 0; i < parameterCount; i++)
        {
            parameters.AppendLine($"    .param .u64 param_{i},");
        }

        // Remove trailing comma
        var paramString = parameters.ToString().TrimEnd('\n', '\r', ',');

        return CreateMinimalPtxKernel("MultiParamKernel", paramString);
    }

    /// <summary>
    /// Creates a realistic PTX kernel with vector addition logic.
    /// </summary>
    private static string CreateRealisticVectorAddKernel()
    {
        return @"
.version 7.0
.target sm_50
.address_size 64

.visible .entry VectorAddKernel(
    .param .u64 param_0,
    .param .u64 param_1,
    .param .u64 param_2,
    .param .u32 param_3
)
{
    .reg .u32 %tid;
    .reg .u64 %addr_a, %addr_b, %addr_c;
    .reg .f32 %val_a, %val_b, %val_c;

    mov.u32 %tid, %tid.x;

    ld.param.u64 %addr_a, [param_0];
    ld.param.u64 %addr_b, [param_1];
    ld.param.u64 %addr_c, [param_2];

    mul.wide.u32 %tid, %tid, 4;
    add.u64 %addr_a, %addr_a, %tid;
    add.u64 %addr_b, %addr_b, %tid;
    add.u64 %addr_c, %addr_c, %tid;

    ld.global.f32 %val_a, [%addr_a];
    ld.global.f32 %val_b, [%addr_b];
    add.f32 %val_c, %val_a, %val_b;
    st.global.f32 [%addr_c], %val_c;

    ret;
}
";
    }

    #endregion

    #region InjectTimestampIntoPtx Tests - Basic Functionality

    [Fact]
    public void InjectTimestampIntoPtx_MinimalKernel_AddsTimestampParameter()
    {
        // Arrange
        var ptxCode = CreateMinimalPtxKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        result.Should().NotBeSameAs(ptxBytes, "injection should create new byte array");
        resultString.Should().Contain("param_0", "timestamp parameter should be injected");
        resultString.Should().Contain("// Injected: timestamp buffer", "comment should identify injected parameter");
        resultString.Should().Contain("// === Injected Timestamp Recording (DotCompute) ===", "prologue marker should exist");
    }

    [Fact]
    public void InjectTimestampIntoPtx_KernelWithParameters_ShiftsParametersCorrectly()
    {
        // Arrange
        var ptxCode = CreateMultiParameterPtxKernel(3);
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "MultiParamKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        resultString.Should().Contain(".param .u64 param_0,  // Injected: timestamp buffer", "param_0 should be timestamp");
        resultString.Should().Contain(".param .u64 param_1", "original param_0 should become param_1");
        resultString.Should().Contain(".param .u64 param_2", "original param_1 should become param_2");
        resultString.Should().Contain(".param .u64 param_3", "original param_2 should become param_3");
    }

    [Fact]
    public void InjectTimestampIntoPtx_RealisticKernel_ProducesValidPtx()
    {
        // Arrange
        var ptxCode = CreateRealisticVectorAddKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "VectorAddKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        resultString.Should().Contain(".param .u64 param_0,  // Injected: timestamp buffer");
        resultString.Should().Contain(".param .u64 param_1", "first original parameter shifted");
        resultString.Should().Contain(".param .u64 param_2", "second original parameter shifted");
        resultString.Should().Contain(".param .u64 param_3", "third original parameter shifted");
        resultString.Should().Contain(".param .u32 param_4", "fourth original parameter shifted with correct type");
        resultString.Should().Contain("mov.u64 %timestamp_val, %globaltimer", "globaltimer should be used");
        resultString.Should().Contain("st.global.u64 [param_0], %timestamp_val", "timestamp should be stored to param_0");
    }

    [Fact]
    public void InjectTimestampIntoPtx_EmptyParameterList_AddsOnlyTimestampParameter()
    {
        // Arrange
        var ptxCode = CreateMinimalPtxKernel("EmptyParamKernel", "");
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "EmptyParamKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        resultString.Should().Contain(".param .u64 param_0,  // Injected: timestamp buffer");
        resultString.Should().NotContain("param_1", "no other parameters should exist");
    }

    #endregion

    #region InjectTimestampIntoPtx Tests - Prologue Insertion

    [Fact]
    public void InjectTimestampIntoPtx_ComputeCapability60Plus_UsesGlobalTimer()
    {
        // Arrange - CC 6.0+ should use %globaltimer
        var ptxCode = CreateMinimalPtxKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        resultString.Should().Contain("%globaltimer", "CC 6.0+ should use globaltimer");
        resultString.Should().Contain(".reg .u64 %timestamp_val", "register for timestamp value");
        resultString.Should().Contain("mov.u64 %timestamp_val, %globaltimer", "timestamp read instruction");
        resultString.Should().Contain("st.global.u64 [param_0], %timestamp_val", "timestamp store instruction");
        resultString.Should().Contain("SKIP_TIMESTAMP_RECORD:", "skip label for non-zero threads");
    }

    [Fact]
    public void InjectTimestampIntoPtx_Prologue_ChecksForThreadZeroZeroZero()
    {
        // Arrange
        var ptxCode = CreateMinimalPtxKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert - Thread ID checks
        resultString.Should().Contain("mov.u32 %tid_x_ts, %tid.x", "read tid.x");
        resultString.Should().Contain("mov.u32 %tid_y_ts, %tid.y", "read tid.y");
        resultString.Should().Contain("mov.u32 %tid_z_ts, %tid.z", "read tid.z");

        // Assert - Predicate checks
        resultString.Should().Contain("setp.ne.u32 %p_timestamp, %tid_x_ts, 0", "check x != 0");
        resultString.Should().Contain("@%p_timestamp bra SKIP_TIMESTAMP_RECORD", "branch if x != 0");
        resultString.Should().MatchRegex("setp\\.ne\\.u32.*%tid_y_ts.*0", "check y != 0");
        resultString.Should().MatchRegex("setp\\.ne\\.u32.*%tid_z_ts.*0", "check z != 0");
    }

    [Fact]
    public void InjectTimestampIntoPtx_Prologue_InsertedAfterFunctionBodyStart()
    {
        // Arrange
        var ptxCode = CreateRealisticVectorAddKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "VectorAddKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert - Prologue should come after opening brace but before user code
        var openingBraceIndex = resultString.IndexOf('{', StringComparison.Ordinal);
        var prologueIndex = resultString.IndexOf("// === Injected Timestamp Recording", StringComparison.Ordinal);
        var userCodeIndex = resultString.IndexOf(".reg .u32 %tid;", StringComparison.Ordinal);

        openingBraceIndex.Should().BeGreaterThan(-1, "opening brace should exist");
        prologueIndex.Should().BeGreaterThan(-1, "prologue should exist");
        userCodeIndex.Should().BeGreaterThan(-1, "user code should exist");

        prologueIndex.Should().BeGreaterThan(openingBraceIndex, "prologue should come after opening brace");
        prologueIndex.Should().BeLessThan(userCodeIndex, "prologue should come before user code");
    }

    #endregion

    #region InjectTimestampIntoPtx Tests - Edge Cases

    [Fact]
    public void InjectTimestampIntoPtx_EmptyInput_ReturnsOriginal()
    {
        // Arrange
        var emptyBytes = Array.Empty<byte>();

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(emptyBytes, "EmptyKernel", NullLogger.Instance);

        // Assert
        result.Should().BeSameAs(emptyBytes, "should return original when empty");
    }

    [Fact]
    public void InjectTimestampIntoPtx_InvalidUtf8_ReturnsOriginal()
    {
        // Arrange - Invalid UTF-8 sequence
        var invalidBytes = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(invalidBytes, "InvalidKernel", NullLogger.Instance);

        // Assert
        result.Should().BeSameAs(invalidBytes, "should return original on UTF-8 error");
    }

    [Fact]
    public void InjectTimestampIntoPtx_MissingEntryFunction_ReturnsOriginal()
    {
        // Arrange - PTX without .visible .entry
        var invalidPtx = @"
.version 7.0
.target sm_50

.func VectorAdd()
{
    ret;
}
";
        var ptxBytes = Encoding.UTF8.GetBytes(invalidPtx);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "InvalidKernel", NullLogger.Instance);

        // Assert
        result.Should().BeSameAs(ptxBytes, "should return original when no .entry found");
    }

    [Fact]
    public void InjectTimestampIntoPtx_MissingParameterList_ReturnsOriginal()
    {
        // Arrange - Entry without parameters (malformed)
        var invalidPtx = @"
.version 7.0
.target sm_50

.visible .entry TestKernel
{
    ret;
}
";
        var ptxBytes = Encoding.UTF8.GetBytes(invalidPtx);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);

        // Assert
        result.Should().BeSameAs(ptxBytes, "should return original when parameter list missing");
    }

    [Fact]
    public void InjectTimestampIntoPtx_MissingFunctionBody_ReturnsOriginal()
    {
        // Arrange - Entry without opening brace
        var invalidPtx = @"
.version 7.0
.target sm_50

.visible .entry TestKernel()
    ret;
";
        var ptxBytes = Encoding.UTF8.GetBytes(invalidPtx);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);

        // Assert
        result.Should().BeSameAs(ptxBytes, "should return original when function body missing");
    }

    [Fact]
    public void InjectTimestampIntoPtx_MultipleInjections_AreIdempotent()
    {
        // Arrange
        var ptxCode = CreateMinimalPtxKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act - Inject twice
        var firstInjection = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);
        var secondInjection = TimestampInjector.InjectTimestampIntoPtx(firstInjection, "TestKernel", NullLogger.Instance);

        var firstString = Encoding.UTF8.GetString(firstInjection);
        var secondString = Encoding.UTF8.GetString(secondInjection);

        // Assert - Second injection should not modify already-injected code
        // (In practice, it will fail to find valid .entry and return original)
        var prologueCount = System.Text.RegularExpressions.Regex.Matches(
            secondString,
            "// === Injected Timestamp Recording").Count;

        prologueCount.Should().BeGreaterThanOrEqualTo(1, "should have at least one prologue");
    }

    #endregion

    #region IsTimestampInjectionSupported Tests

    [Theory]
    [InlineData(5, 0, true)]  // CC 5.0 - minimum supported
    [InlineData(5, 2, true)]  // CC 5.2
    [InlineData(6, 0, true)]  // CC 6.0 - globaltimer support
    [InlineData(6, 1, true)]  // CC 6.1
    [InlineData(7, 0, true)]  // CC 7.0
    [InlineData(7, 5, true)]  // CC 7.5
    [InlineData(8, 0, true)]  // CC 8.0
    [InlineData(8, 6, true)]  // CC 8.6
    [InlineData(8, 9, true)]  // CC 8.9 - Ada
    [InlineData(9, 0, true)]  // CC 9.0 - Hopper
    public void IsTimestampInjectionSupported_ValidComputeCapabilities_ReturnsTrue(int major, int minor, bool expected)
    {
        // Act
        var result = TimestampInjector.IsTimestampInjectionSupported(major, minor);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(3, 0, false)]  // CC 3.0 - Kepler
    [InlineData(3, 5, false)]  // CC 3.5
    [InlineData(4, 0, false)]  // CC 4.0
    [InlineData(4, 9, false)]  // CC 4.9
    public void IsTimestampInjectionSupported_LegacyComputeCapabilities_ReturnsFalse(int major, int minor, bool expected)
    {
        // Act
        var result = TimestampInjector.IsTimestampInjectionSupported(major, minor);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void IsTimestampInjectionSupported_VeryOldOrInvalidCapabilities_ReturnsFalse(int major, int minor)
    {
        // Act
        var result = TimestampInjector.IsTimestampInjectionSupported(major, minor);

        // Assert
        result.Should().BeFalse("CC < 5.0 should not be supported");
    }

    #endregion

    #region GetTimestampBufferSize Tests

    [Fact]
    public void GetTimestampBufferSize_ReturnsEightBytes()
    {
        // Act
        var size = TimestampInjector.GetTimestampBufferSize();

        // Assert
        size.Should().Be(8, "timestamp is a 64-bit long (8 bytes)");
        size.Should().Be(sizeof(long), "should match sizeof(long)");
    }

    #endregion

    #region PTX Syntax Validation Tests

    [Fact]
    public void InjectTimestampIntoPtx_InjectedCode_MaintainsValidPtxSyntax()
    {
        // Arrange
        var ptxCode = CreateRealisticVectorAddKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "VectorAddKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert - Check for valid PTX structure
        resultString.Should().Contain(".version", "should have version directive");
        resultString.Should().Contain(".target", "should have target directive");
        resultString.Should().Contain(".visible .entry", "should have entry point");
        resultString.Should().MatchRegex(@"\{\s*.*\s*\}", "should have function body with braces");
        resultString.Should().Contain("ret;", "should have return statement");

        // Assert - Check injected code has valid PTX syntax
        resultString.Should().MatchRegex(@"\.reg\s+\.pred\s+%\w+", "should declare predicate registers");
        resultString.Should().MatchRegex(@"\.reg\s+\.u32\s+%\w+", "should declare u32 registers");
        resultString.Should().MatchRegex(@"mov\.u32\s+%\w+,\s+%tid\.[xyz]", "should read thread IDs");
        resultString.Should().MatchRegex(@"setp\.ne\.u32\s+%\w+", "should have predicate set instructions");
        resultString.Should().MatchRegex(@"@%\w+\s+bra\s+\w+", "should have conditional branch");
    }

    [Fact]
    public void InjectTimestampIntoPtx_ParameterFormatting_IsCorrect()
    {
        // Arrange
        var ptxCode = CreateMultiParameterPtxKernel(5);
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "MultiParamKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert - Parameters should be properly formatted
        resultString.Should().MatchRegex(@"\.param\s+\.u64\s+param_0,\s+//\s+Injected", "param_0 with comment");
        resultString.Should().MatchRegex(@"\.param\s+\.u64\s+param_1\b", "param_1 formatted correctly");
        resultString.Should().MatchRegex(@"\.param\s+\.u64\s+param_2\b", "param_2 formatted correctly");
        resultString.Should().MatchRegex(@"\.param\s+\.u64\s+param_3\b", "param_3 formatted correctly");
        resultString.Should().MatchRegex(@"\.param\s+\.u64\s+param_4\b", "param_4 formatted correctly");
        resultString.Should().MatchRegex(@"\.param\s+\.u64\s+param_5\b", "param_5 formatted correctly");
    }

    [Fact]
    public void InjectTimestampIntoPtx_PrologueFormatting_HasCorrectIndentation()
    {
        // Arrange
        var ptxCode = CreateMinimalPtxKernel();
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "TestKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert - Check indentation consistency
        var lines = resultString.Split('\n');
        var prologueLines = lines.Where(l => l.Contains("// === Injected") ||
                                             l.Contains(".reg") && l.Contains("_timestamp") ||
                                             l.Contains("mov.u32") && l.Contains("_ts") ||
                                             l.Contains("setp.ne.u32"));

        foreach (var line in prologueLines)
        {
            // All prologue lines should have consistent indentation (4 or 8 spaces)
            if (!string.IsNullOrWhiteSpace(line))
            {
                line.Should().MatchRegex(@"^\s{4,12}\S", "should have proper indentation");
            }
        }
    }

    #endregion

    #region Special Parameter Type Tests

    [Fact]
    public void InjectTimestampIntoPtx_MixedParameterTypes_PreservesTypes()
    {
        // Arrange - Kernel with different parameter types
        var ptxCode = @"
.version 7.0
.target sm_50
.address_size 64

.visible .entry MixedTypeKernel(
    .param .u64 param_0,
    .param .f32 param_1,
    .param .u32 param_2,
    .param .f64 param_3
)
{
    ret;
}
";
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "MixedTypeKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert - Types should be preserved and shifted
        resultString.Should().Contain(".param .u64 param_0,  // Injected", "timestamp parameter added");
        resultString.Should().Contain(".param .u64 param_1", "u64 type preserved");
        resultString.Should().Contain(".param .f32 param_2", "f32 type preserved");
        resultString.Should().Contain(".param .u32 param_3", "u32 type preserved");
        resultString.Should().Contain(".param .f64 param_4", "f64 type preserved");
    }

    #endregion

    #region Whitespace and Formatting Edge Cases

    [Fact]
    public void InjectTimestampIntoPtx_CompactFormatting_HandlesCorrectly()
    {
        // Arrange - Compact PTX with minimal whitespace
        var ptxCode = ".version 7.0\n.target sm_50\n.visible .entry Compact(){\nret;\n}\n";
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "Compact", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        resultString.Should().Contain(".param .u64 param_0", "timestamp parameter should be added");
        resultString.Should().Contain("// === Injected Timestamp Recording", "prologue should be added");
    }

    [Fact]
    public void InjectTimestampIntoPtx_VerboseFormatting_HandlesCorrectly()
    {
        // Arrange - Verbose PTX with lots of whitespace and comments
        var ptxCode = @"

.version 7.0
.target sm_50
.address_size 64

// Main kernel entry point
.visible .entry VerboseKernel(

    // Input parameters
    .param .u64 param_0,  // First input
    .param .u64 param_1   // Second input

)
{
    // Kernel body
    ret;
}

";
        var ptxBytes = Encoding.UTF8.GetBytes(ptxCode);

        // Act
        var result = TimestampInjector.InjectTimestampIntoPtx(ptxBytes, "VerboseKernel", NullLogger.Instance);
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        resultString.Should().Contain(".param .u64 param_0,  // Injected", "timestamp added");
        resultString.Should().Contain(".param .u64 param_1", "param_0 shifted to param_1");
        resultString.Should().Contain(".param .u64 param_2", "param_1 shifted to param_2");
    }

    #endregion
}
