// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Diagnostic utilities for debugging Ring Kernel message serialization.
/// </summary>
/// <remarks>
/// Provides byte-level inspection of serialized messages to help debug
/// format mismatches between C# MemoryPack serialization and CUDA deserializers.
/// </remarks>
[SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Diagnostic output uses hex/byte formatting which is culture-invariant")]
public static class MessageDiagnostics
{
    /// <summary>
    /// Global flag to enable/disable diagnostic logging.
    /// Set to true during development to get byte-level message dumps.
    /// </summary>
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Formats a byte array as a hex dump with ASCII representation.
    /// </summary>
    /// <param name="data">The byte data to format.</param>
    /// <param name="label">Optional label for the dump.</param>
    /// <param name="maxBytes">Maximum bytes to display (default 256).</param>
    /// <returns>Formatted hex dump string.</returns>
    public static string FormatHexDump(ReadOnlySpan<byte> data, string? label = null, int maxBytes = 256)
    {
        var sb = new StringBuilder();
        var bytesToShow = Math.Min(data.Length, maxBytes);

        if (!string.IsNullOrEmpty(label))
        {
            sb.AppendLine($"=== {label} ({data.Length} bytes) ===");
        }
        else
        {
            sb.AppendLine($"=== Message Dump ({data.Length} bytes) ===");
        }

        sb.AppendLine("Offset   Hex                                              ASCII");
        sb.AppendLine("────────────────────────────────────────────────────────────────");

        for (var offset = 0; offset < bytesToShow; offset += 16)
        {
            sb.Append($"{offset:X4}:    ");

            // Hex bytes
            var hexBuilder = new StringBuilder();
            for (var i = 0; i < 16; i++)
            {
                if (offset + i < bytesToShow)
                {
                    hexBuilder.Append($"{data[offset + i]:X2} ");
                }
                else
                {
                    hexBuilder.Append("   ");
                }

                // Add extra space after 8 bytes for readability
                if (i == 7)
                {
                    hexBuilder.Append(' ');
                }
            }

            sb.Append(hexBuilder.ToString().PadRight(50));
            sb.Append("  ");

            // ASCII representation
            for (var i = 0; i < 16 && offset + i < bytesToShow; i++)
            {
                var b = data[offset + i];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }

            sb.AppendLine();
        }

        if (bytesToShow < data.Length)
        {
            sb.AppendLine($"... ({data.Length - bytesToShow} more bytes truncated)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a message with field annotations based on expected layout.
    /// </summary>
    /// <param name="data">The byte data to format.</param>
    /// <param name="fields">Array of field annotations (name, offset, size).</param>
    /// <returns>Formatted field-annotated dump.</returns>
    public static string FormatAnnotatedDump(ReadOnlySpan<byte> data, params (string Name, int Offset, int Size)[] fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Annotated Message Dump ({data.Length} bytes) ===");
        sb.AppendLine("Field                Offset  Size    Hex Value                           ");
        sb.AppendLine("────────────────────────────────────────────────────────────────────────");

        foreach (var (name, offset, size) in fields)
        {
            if (offset >= data.Length)
            {
                sb.AppendLine($"{name,-20} {offset,6}  {size,4}    [OUT OF BOUNDS]");
                continue;
            }

            var actualSize = Math.Min(size, data.Length - offset);
            var fieldBytes = data.Slice(offset, actualSize);

            // Build hex representation
            var hexBuilder = new StringBuilder();
            for (var i = 0; i < Math.Min(actualSize, 16); i++)
            {
                hexBuilder.Append($"{fieldBytes[i]:X2} ");
            }
            if (actualSize > 16)
            {
                hexBuilder.Append($"... (+{actualSize - 16})");
            }

            sb.AppendLine($"{name,-20} {offset,6}  {actualSize,4}    {hexBuilder}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a VectorAddRequest message for debugging.
    /// </summary>
    /// <param name="data">The serialized message bytes.</param>
    /// <param name="hasMemoryPackHeader">Whether the data includes MemoryPack's 1-byte member count header.</param>
    /// <returns>Formatted annotated dump.</returns>
    public static string FormatVectorAddRequest(ReadOnlySpan<byte> data, bool hasMemoryPackHeader = true)
    {
        var headerOffset = hasMemoryPackHeader ? 1 : 0;

        if (hasMemoryPackHeader && data.Length > 0)
        {
            var header = data[0];
            var headerDump = new StringBuilder();
            headerDump.AppendLine($"=== MemoryPack Header ===");
            headerDump.AppendLine($"Member Count: {header} (expected: 5 for VectorAddRequest)");
            headerDump.AppendLine();
        }

        return FormatAnnotatedDump(data,
            ("Header", 0, 1),
            ("MessageId (Guid)", headerOffset, 16),
            ("Priority (byte)", headerOffset + 16, 1),
            ("CorrelationId.HasValue", headerOffset + 17, 1),
            ("CorrelationId.Value", headerOffset + 18, 16),
            ("A (float)", headerOffset + 34, 4),
            ("B (float)", headerOffset + 38, 4));
    }

    /// <summary>
    /// Formats a VectorAddResponse message for debugging.
    /// </summary>
    /// <param name="data">The serialized message bytes.</param>
    /// <param name="hasMemoryPackHeader">Whether the data includes MemoryPack's 1-byte member count header.</param>
    /// <returns>Formatted annotated dump.</returns>
    public static string FormatVectorAddResponse(ReadOnlySpan<byte> data, bool hasMemoryPackHeader = true)
    {
        var headerOffset = hasMemoryPackHeader ? 1 : 0;

        return FormatAnnotatedDump(data,
            ("Header", 0, 1),
            ("MessageId (Guid)", headerOffset, 16),
            ("Priority (byte)", headerOffset + 16, 1),
            ("CorrelationId.HasValue", headerOffset + 17, 1),
            ("CorrelationId.Value", headerOffset + 18, 16),
            ("Result (float)", headerOffset + 34, 4));
    }

    /// <summary>
    /// Compares two byte arrays and shows differences.
    /// </summary>
    /// <param name="expected">Expected byte data.</param>
    /// <param name="actual">Actual byte data.</param>
    /// <param name="expectedLabel">Label for expected data.</param>
    /// <param name="actualLabel">Label for actual data.</param>
    /// <returns>Comparison report showing differences.</returns>
    public static string CompareByteDumps(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        string expectedLabel = "Expected",
        string actualLabel = "Actual")
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"=== Byte Comparison ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{expectedLabel}: {expected.Length} bytes");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{actualLabel}: {actual.Length} bytes");
        sb.AppendLine();

        var maxLen = Math.Max(expected.Length, actual.Length);
        var differences = 0;

        for (var i = 0; i < maxLen; i++)
        {
            var expByte = i < expected.Length ? expected[i] : (byte?)null;
            var actByte = i < actual.Length ? actual[i] : (byte?)null;

            if (expByte != actByte)
            {
                differences++;
                var expStr = expByte.HasValue ? string.Format(CultureInfo.InvariantCulture, "0x{0:X2}", expByte) : "N/A";
                var actStr = actByte.HasValue ? string.Format(CultureInfo.InvariantCulture, "0x{0:X2}", actByte) : "N/A";
                sb.AppendLine(CultureInfo.InvariantCulture, $"  Offset {i:X4}: {expectedLabel}={expStr}, {actualLabel}={actStr}");
            }
        }

        if (differences == 0)
        {
            sb.AppendLine("  No differences found!");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Total differences: {differences}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extracts and formats a float value from a byte span at the given offset.
    /// </summary>
    /// <param name="data">The byte data.</param>
    /// <param name="offset">Offset to the float value.</param>
    /// <returns>The float value, or NaN if offset is out of bounds.</returns>
    public static float ExtractFloat(ReadOnlySpan<byte> data, int offset)
    {
        if (offset + 4 > data.Length)
        {
            return float.NaN;
        }

        return BitConverter.ToSingle(data.Slice(offset, 4));
    }

    /// <summary>
    /// Extracts and formats a Guid value from a byte span at the given offset.
    /// </summary>
    /// <param name="data">The byte data.</param>
    /// <param name="offset">Offset to the Guid value.</param>
    /// <returns>The Guid value, or Guid.Empty if offset is out of bounds.</returns>
    public static Guid ExtractGuid(ReadOnlySpan<byte> data, int offset)
    {
        if (offset + 16 > data.Length)
        {
            return Guid.Empty;
        }

        return new Guid(data.Slice(offset, 16));
    }

    /// <summary>
    /// Creates a diagnostic summary for a serialized message including key field values.
    /// </summary>
    /// <param name="data">The serialized message bytes.</param>
    /// <param name="hasMemoryPackHeader">Whether the data includes MemoryPack header.</param>
    /// <returns>A summary string with extracted field values.</returns>
    public static string CreateVectorAddRequestSummary(ReadOnlySpan<byte> data, bool hasMemoryPackHeader = true)
    {
        var sb = new StringBuilder();
        var headerOffset = hasMemoryPackHeader ? 1 : 0;

        sb.AppendLine("=== VectorAddRequest Summary ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Size: {data.Length} bytes (expected: {(hasMemoryPackHeader ? 43 : 42)})");

        if (hasMemoryPackHeader && data.Length > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Header (Member Count): {data[0]} (expected: 5)");
        }

        if (data.Length > headerOffset + 16)
        {
            var messageId = ExtractGuid(data, headerOffset);
            sb.AppendLine(CultureInfo.InvariantCulture, $"MessageId: {messageId}");
        }

        if (data.Length > headerOffset + 16)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Priority: {data[headerOffset + 16]}");
        }

        if (data.Length > headerOffset + 17)
        {
            var hasCorrelation = data[headerOffset + 17] != 0;
            sb.AppendLine(CultureInfo.InvariantCulture, $"CorrelationId.HasValue: {hasCorrelation}");
            if (hasCorrelation && data.Length > headerOffset + 34)
            {
                var correlationId = ExtractGuid(data, headerOffset + 18);
                sb.AppendLine(CultureInfo.InvariantCulture, $"CorrelationId.Value: {correlationId}");
            }
        }

        if (data.Length > headerOffset + 38)
        {
            var a = ExtractFloat(data, headerOffset + 34);
            sb.AppendLine(CultureInfo.InvariantCulture, $"A: {a}");
        }

        if (data.Length >= headerOffset + 42)
        {
            var b = ExtractFloat(data, headerOffset + 38);
            sb.AppendLine(CultureInfo.InvariantCulture, $"B: {b}");
        }

        return sb.ToString();
    }
}
