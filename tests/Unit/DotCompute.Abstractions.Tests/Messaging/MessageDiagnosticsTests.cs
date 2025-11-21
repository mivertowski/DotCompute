// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using Xunit;

namespace DotCompute.Abstractions.Tests.Messaging;

/// <summary>
/// Tests for <see cref="MessageDiagnostics"/>.
/// </summary>
public class MessageDiagnosticsTests
{
    #region FormatHexDump Tests

    [Fact]
    public void FormatHexDump_WithEmptyData_ReturnsHeaderOnly()
    {
        // Arrange
        var data = Array.Empty<byte>();

        // Act
        var result = MessageDiagnostics.FormatHexDump(data);

        // Assert
        Assert.Contains("0 bytes", result);
        Assert.Contains("Message Dump", result);
    }

    [Fact]
    public void FormatHexDump_WithLabel_IncludesLabel()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = MessageDiagnostics.FormatHexDump(data, "TestLabel");

        // Assert
        Assert.Contains("TestLabel", result);
        Assert.Contains("3 bytes", result);
    }

    [Fact]
    public void FormatHexDump_WithData_FormatsAsHex()
    {
        // Arrange
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        // Act
        var result = MessageDiagnostics.FormatHexDump(data);

        // Assert
        Assert.Contains("DE", result);
        Assert.Contains("AD", result);
        Assert.Contains("BE", result);
        Assert.Contains("EF", result);
    }

    [Fact]
    public void FormatHexDump_WithPrintableAscii_ShowsAsciiRepresentation()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        var result = MessageDiagnostics.FormatHexDump(data);

        // Assert
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void FormatHexDump_WithMaxBytesLimit_TruncatesOutput()
    {
        // Arrange
        var data = new byte[100];
        for (int i = 0; i < 100; i++)
        {
            data[i] = (byte)i;
        }

        // Act
        var result = MessageDiagnostics.FormatHexDump(data, maxBytes: 32);

        // Assert
        Assert.Contains("truncated", result.ToLowerInvariant());
        Assert.Contains("68 more bytes", result);
    }

    [Fact]
    public void FormatHexDump_With16Bytes_FormatsOnOneLine()
    {
        // Arrange
        var data = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = (byte)i;
        }

        // Act
        var result = MessageDiagnostics.FormatHexDump(data);

        // Assert
        // Should have offset 0000 but not 0010 (second line)
        Assert.Contains("0000:", result);
    }

    #endregion

    #region FormatAnnotatedDump Tests

    [Fact]
    public void FormatAnnotatedDump_WithValidFields_ShowsFieldAnnotations()
    {
        // Arrange
        var data = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            data[i] = (byte)i;
        }

        var fields = new (string Name, int Offset, int Size)[]
        {
            ("Header", 0, 4),
            ("Payload", 4, 28)
        };

        // Act
        var result = MessageDiagnostics.FormatAnnotatedDump(data, fields);

        // Assert
        Assert.Contains("Header", result);
        Assert.Contains("Payload", result);
        Assert.Contains("0", result); // Offset 0
        Assert.Contains("4", result); // Offset 4 or size 4
    }

    [Fact]
    public void FormatAnnotatedDump_WithOutOfBoundsField_IndicatesOutOfBounds()
    {
        // Arrange
        var data = new byte[10];
        var fields = new (string Name, int Offset, int Size)[]
        {
            ("OutOfBounds", 100, 4)
        };

        // Act
        var result = MessageDiagnostics.FormatAnnotatedDump(data, fields);

        // Assert
        Assert.Contains("OUT OF BOUNDS", result);
    }

    #endregion

    #region ExtractFloat Tests

    [Fact]
    public void ExtractFloat_WithValidData_ReturnsCorrectValue()
    {
        // Arrange - IEEE 754 representation of 10.0f
        var data = new byte[] { 0x00, 0x00, 0x20, 0x41 };

        // Act
        var result = MessageDiagnostics.ExtractFloat(data, 0);

        // Assert
        Assert.Equal(10.0f, result, precision: 5);
    }

    [Fact]
    public void ExtractFloat_WithInsufficientData_ReturnsNaN()
    {
        // Arrange
        var data = new byte[] { 0x00, 0x00 }; // Only 2 bytes

        // Act
        var result = MessageDiagnostics.ExtractFloat(data, 0);

        // Assert
        Assert.True(float.IsNaN(result));
    }

    [Fact]
    public void ExtractFloat_WithOffsetOutOfBounds_ReturnsNaN()
    {
        // Arrange
        var data = new byte[] { 0x00, 0x00, 0x20, 0x41 };

        // Act
        var result = MessageDiagnostics.ExtractFloat(data, 10);

        // Assert
        Assert.True(float.IsNaN(result));
    }

    #endregion

    #region ExtractGuid Tests

    [Fact]
    public void ExtractGuid_WithValidData_ReturnsCorrectGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var data = expectedGuid.ToByteArray();

        // Act
        var result = MessageDiagnostics.ExtractGuid(data, 0);

        // Assert
        Assert.Equal(expectedGuid, result);
    }

    [Fact]
    public void ExtractGuid_WithInsufficientData_ReturnsEmptyGuid()
    {
        // Arrange
        var data = new byte[10]; // Less than 16 bytes

        // Act
        var result = MessageDiagnostics.ExtractGuid(data, 0);

        // Assert
        Assert.Equal(Guid.Empty, result);
    }

    [Fact]
    public void ExtractGuid_WithOffsetOutOfBounds_ReturnsEmptyGuid()
    {
        // Arrange
        var data = new byte[16];

        // Act
        var result = MessageDiagnostics.ExtractGuid(data, 10);

        // Assert
        Assert.Equal(Guid.Empty, result);
    }

    #endregion

    #region CompareByteDumps Tests

    [Fact]
    public void CompareByteDumps_WithIdenticalData_ReportsNoDifferences()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var result = MessageDiagnostics.CompareByteDumps(data, data);

        // Assert
        Assert.Contains("No differences found", result);
    }

    [Fact]
    public void CompareByteDumps_WithDifferentData_ReportsDifferences()
    {
        // Arrange
        var expected = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var actual = new byte[] { 0x01, 0xFF, 0x03, 0x04 };

        // Act
        var result = MessageDiagnostics.CompareByteDumps(expected, actual);

        // Assert
        Assert.Contains("Total differences:", result);
        Assert.Contains("0x02", result);
        Assert.Contains("0xFF", result);
    }

    [Fact]
    public void CompareByteDumps_WithDifferentLengths_ReportsAllDifferences()
    {
        // Arrange
        var expected = new byte[] { 0x01, 0x02, 0x03 };
        var actual = new byte[] { 0x01, 0x02 };

        // Act
        var result = MessageDiagnostics.CompareByteDumps(expected, actual);

        // Assert
        Assert.Contains("N/A", result); // Missing byte shows as N/A
    }

    [Fact]
    public void CompareByteDumps_WithCustomLabels_UsesLabels()
    {
        // Arrange
        var expected = new byte[] { 0x01 };
        var actual = new byte[] { 0x02 };

        // Act
        var result = MessageDiagnostics.CompareByteDumps(expected, actual, "Host", "GPU");

        // Assert
        Assert.Contains("Host", result);
        Assert.Contains("GPU", result);
    }

    #endregion

    #region IsEnabled Property Tests

    [Fact]
    public void IsEnabled_DefaultValue_IsFalse()
    {
        // Save original state
        var original = MessageDiagnostics.IsEnabled;

        try
        {
            // Reset to verify default behavior
            MessageDiagnostics.IsEnabled = false;

            // Assert
            Assert.False(MessageDiagnostics.IsEnabled);
        }
        finally
        {
            // Restore original state
            MessageDiagnostics.IsEnabled = original;
        }
    }

    [Fact]
    public void IsEnabled_CanBeSetToTrue()
    {
        // Save original state
        var original = MessageDiagnostics.IsEnabled;

        try
        {
            // Act
            MessageDiagnostics.IsEnabled = true;

            // Assert
            Assert.True(MessageDiagnostics.IsEnabled);
        }
        finally
        {
            // Restore original state
            MessageDiagnostics.IsEnabled = original;
        }
    }

    #endregion

    #region VectorAddRequest Format Tests

    [Fact]
    public void FormatVectorAddRequest_WithValidData_FormatsAllFields()
    {
        // Arrange - Create a 43-byte buffer matching VectorAddRequest format
        var data = new byte[43];
        data[0] = 5; // Member count header

        // MessageId (bytes 1-16)
        var messageId = Guid.NewGuid();
        messageId.TryWriteBytes(data.AsSpan(1, 16));

        // Priority (byte 17)
        data[17] = 128;

        // CorrelationId.HasValue (byte 18)
        data[18] = 1;

        // CorrelationId.Value (bytes 19-34)
        var correlationId = Guid.NewGuid();
        correlationId.TryWriteBytes(data.AsSpan(19, 16));

        // A (bytes 35-38) - 10.0f
        BitConverter.TryWriteBytes(data.AsSpan(35, 4), 10.0f);

        // B (bytes 39-42) - 20.0f
        BitConverter.TryWriteBytes(data.AsSpan(39, 4), 20.0f);

        // Act
        var result = MessageDiagnostics.FormatVectorAddRequest(data);

        // Assert
        Assert.Contains("Header", result);
        Assert.Contains("MessageId", result);
        Assert.Contains("Priority", result);
        Assert.Contains("CorrelationId", result);
        Assert.Contains("A (float)", result);
        Assert.Contains("B (float)", result);
    }

    [Fact]
    public void CreateVectorAddRequestSummary_WithValidData_ExtractsValues()
    {
        // Arrange
        var data = new byte[43];
        data[0] = 5; // Header

        var messageId = Guid.NewGuid();
        messageId.TryWriteBytes(data.AsSpan(1, 16));
        data[17] = 128; // Priority
        data[18] = 0; // No CorrelationId

        BitConverter.TryWriteBytes(data.AsSpan(35, 4), 10.0f);
        BitConverter.TryWriteBytes(data.AsSpan(39, 4), 20.0f);

        // Act
        var result = MessageDiagnostics.CreateVectorAddRequestSummary(data);

        // Assert
        Assert.Contains("VectorAddRequest Summary", result);
        Assert.Contains("43 bytes", result);
        Assert.Contains(messageId.ToString(), result);
        Assert.Contains("128", result); // Priority
        Assert.Contains("10", result);  // A value
        Assert.Contains("20", result);  // B value
    }

    #endregion
}
