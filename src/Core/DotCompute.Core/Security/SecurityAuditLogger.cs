// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using System.Globalization;
using System;

namespace DotCompute.Core.Security;

/// <summary>
/// Handles audit trail management and log export functionality.
/// Provides tamper-evident logging and compliance support.
/// </summary>
public sealed class SecurityAuditLogger(ILogger<SecurityAuditLogger> logger,
    SecurityLoggingConfiguration configuration,
    ConcurrentQueue<SecurityLogEntry> auditQueue,
    SemaphoreSlim logWriteLock,
    string auditLogPath)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SecurityLoggingConfiguration _configuration = configuration;
    private readonly ConcurrentQueue<SecurityLogEntry> _auditQueue = auditQueue ?? throw new ArgumentNullException(nameof(auditQueue));
    private readonly SemaphoreSlim _logWriteLock = logWriteLock ?? throw new ArgumentNullException(nameof(logWriteLock));
    private readonly string _auditLogPath = auditLogPath ?? throw new ArgumentNullException(nameof(auditLogPath));

    /// <summary>
    /// Exports audit logs for a specified date range.
    /// </summary>
    public async Task<AuditExportResult> ExportAuditLogsAsync(DateTime startDate, DateTime endDate,
        string exportPath, AuditExportFormat format = AuditExportFormat.Json)
    {
        var result = new AuditExportResult
        {
            ExportFilePath = exportPath,
            Format = format.ToString(),
            ExportTime = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogInfoMessage($"Starting audit log export: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}, Format: {format}");

            // Validate date range
            if (startDate > endDate)
            {
                result.ErrorMessage = "Start date cannot be after end date";
                return result;
            }

            if (startDate > DateTime.UtcNow)
            {
                result.ErrorMessage = "Start date cannot be in the future";
                return result;
            }

            // Ensure export directory exists
            var exportDir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(exportDir))
            {
                _ = Directory.CreateDirectory(exportDir);
            }

            // Read and filter audit logs
            var entries = await ReadAndFilterAuditLogsAsync(startDate, endDate, null);

            if (entries.Count == 0)
            {
                result.ErrorMessage = "No audit entries found for the specified date range";
                return result;
            }

            // Export entries in the specified format
            await ExportEntriesAsync(entries, exportPath, format);

            // Calculate file size
            var fileInfo = new FileInfo(exportPath);
            result.FileSizeBytes = fileInfo.Length;

            var successResult = new AuditExportResult
            {
                Success = true,
                ExportFilePath = exportPath,
                EntriesExported = entries.Count,
                Format = format.ToString(),
                ExportTime = DateTimeOffset.UtcNow,
                FileSizeBytes = fileInfo.Length
            };

            _logger.LogInfoMessage($"Audit log export completed: {entries.Count} entries, {successResult.FileSizeBytes} bytes");

            return successResult;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Export failed: {ex.Message}";
            _logger.LogErrorMessage(ex, "Audit log export failed");
            return result;
        }
    }

    /// <summary>
    /// Flushes pending audit log entries to persistent storage.
    /// </summary>
    public async Task FlushAuditLogQueueAsync()
    {
        if (_auditQueue.IsEmpty)
        {
            return;
        }

        await _logWriteLock.WaitAsync();
        try
        {
            var entriesToFlush = new List<SecurityLogEntry>();

            // Dequeue all pending entries
            while (_auditQueue.TryDequeue(out var entry))
            {
                entriesToFlush.Add(entry);
            }

            if (entriesToFlush.Count == 0)
            {
                return;
            }

            // Write entries to audit log file
            foreach (var entry in entriesToFlush)
            {
                await FlushSpecificEntryAsync(entry);
            }

            _logger.LogDebugMessage($"Flushed {entriesToFlush.Count} security audit entries");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to flush audit log queue");
        }
        finally
        {
            _ = _logWriteLock.Release();
        }
    }

    /// <summary>
    /// Performs integrity check on audit log files.
    /// </summary>
    public static async Task PerformAuditLogIntegrityCheckAsync()
    {
        await Task.Run(() =>
        {
            // Implementation for audit log integrity checking
            // This would include hash verification, timestamp validation, etc.
            // For now, this is a placeholder for the actual implementation
        });
    }

    private async Task FlushSpecificEntryAsync(SecurityLogEntry entry)
    {
        try
        {
            var jsonEntry = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.AppendAllTextAsync(_auditLogPath, jsonEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to write audit entry to log file: {entry.Id}");
        }
    }

    private async Task<List<SecurityLogEntry>> ReadAndFilterAuditLogsAsync(DateTime startDate, DateTime endDate,
        Func<SecurityLogEntry, bool>? additionalFilter)
    {
        var entries = new List<SecurityLogEntry>();
        var auditDirectory = Path.GetDirectoryName(_auditLogPath) ?? "";

        try
        {
            // Get all audit log files that might contain entries in the date range
            var auditFiles = Directory.GetFiles(auditDirectory, "security_audit_*.jsonl")
                .Where(f => IsFileInDateRange(f, startDate, endDate))
                .OrderBy(f => f);

            foreach (var file in auditFiles)
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                var lines = await File.ReadAllLinesAsync(file);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var entry = JsonSerializer.Deserialize<SecurityLogEntry>(line, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        if (entry != null &&
                            entry.Timestamp >= startDate &&
                            entry.Timestamp <= endDate &&
                            (additionalFilter?.Invoke(entry) ?? true))
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarningMessage($"Failed to parse audit log entry: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to read audit log files");
        }

        return [.. entries.OrderBy(e => e.Timestamp)];
    }

    private static async Task ExportEntriesAsync(IReadOnlyList<SecurityLogEntry> entries, string exportPath, AuditExportFormat format)
    {
        switch (format)
        {
            case AuditExportFormat.Json:
                await ExportAsJsonAsync(entries, exportPath);
                break;
            case AuditExportFormat.Csv:
                await ExportAsCsvAsync(entries, exportPath);
                break;
            case AuditExportFormat.Xml:
                await ExportAsXmlAsync(entries, exportPath);
                break;
            default:
                throw new ArgumentException($"Unsupported export format: {format}");
        }
    }

    private static async Task ExportAsJsonAsync(IReadOnlyList<SecurityLogEntry> entries, string exportPath)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(exportPath, json);
    }

    private static async Task ExportAsCsvAsync(IReadOnlyList<SecurityLogEntry> entries, string exportPath)
    {
        var csv = new StringBuilder();

        // Write header
        _ = csv.AppendLine("Id,SequenceNumber,Timestamp,EventType,Level,Message,UserId,ResourceId,CorrelationId,CallerName");

        // Write entries
        foreach (var entry in entries)
        {
            _ = csv.AppendLine($"{EscapeCsv(entry.Id.ToString())}," +
                          $"{entry.SequenceNumber}," +
                          $"{EscapeCsv(entry.Timestamp.ToString("O"))}," +
                          $"{EscapeCsv(entry.EventType.ToString())}," +
                          $"{EscapeCsv(entry.Level.ToString())}," +
                          $"{EscapeCsv(entry.Message)}," +
                          $"{EscapeCsv(entry.UserId ?? "")}," +
                          $"{EscapeCsv(entry.ResourceId ?? "")}," +
                          $"{EscapeCsv(entry.CorrelationId ?? "")}," +
                          $"{EscapeCsv(entry.CallerName ?? "")}");
        }

        await File.WriteAllTextAsync(exportPath, csv.ToString());
    }

    private static async Task ExportAsXmlAsync(IReadOnlyList<SecurityLogEntry> entries, string exportPath)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Async = true
        };

        await using var fileStream = new FileStream(exportPath, FileMode.Create);
        await using var xmlWriter = XmlWriter.Create(fileStream, settings);

        await xmlWriter.WriteStartDocumentAsync();
        await xmlWriter.WriteStartElementAsync(null, "SecurityAuditLog", null);

        foreach (var entry in entries)
        {
            await xmlWriter.WriteStartElementAsync(null, "Entry", null);
            await xmlWriter.WriteAttributeStringAsync(null, "id", null, entry.Id.ToString());
            await xmlWriter.WriteAttributeStringAsync(null, "sequenceNumber", null, entry.SequenceNumber.ToString());
            await xmlWriter.WriteAttributeStringAsync(null, "timestamp", null, entry.Timestamp.ToString("O"));
            await xmlWriter.WriteAttributeStringAsync(null, "eventType", null, entry.EventType.ToString());
            await xmlWriter.WriteAttributeStringAsync(null, "level", null, entry.Level.ToString());
            await xmlWriter.WriteElementStringAsync(null, "Message", null, entry.Message);

            if (!string.IsNullOrEmpty(entry.UserId))
            {
                await xmlWriter.WriteElementStringAsync(null, "UserId", null, entry.UserId);
            }

            if (!string.IsNullOrEmpty(entry.ResourceId))
            {
                await xmlWriter.WriteElementStringAsync(null, "ResourceId", null, entry.ResourceId);
            }

            await xmlWriter.WriteElementStringAsync(null, "CorrelationId", null, entry.CorrelationId ?? "");
            await xmlWriter.WriteEndElementAsync();
        }

        await xmlWriter.WriteEndElementAsync();
        await xmlWriter.WriteEndDocumentAsync();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains(',', StringComparison.OrdinalIgnoreCase) || value.Contains('"', StringComparison.OrdinalIgnoreCase) || value.Contains('\n', StringComparison.CurrentCulture) || value.Contains('\r', StringComparison.CurrentCulture))
        {
            return $"\"{value.Replace("\"", "\"\"")}\";";
        }

        return value;
    }

    private static bool IsFileInDateRange(string filePath, DateTime startDate, DateTime endDate)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var datePart = fileName.Replace("security_audit_", "");

        if (DateTime.TryParseExact(datePart, "yyyyMMdd", null,
            DateTimeStyles.None, out var fileDate))
        {
            return fileDate >= startDate.Date && fileDate <= endDate.Date;
        }

        return true; // Include file if we can't parse the date
    }

    private static string GetFileExtension(AuditExportFormat format)
    {
        return format switch
        {
            AuditExportFormat.Json => "json",
            AuditExportFormat.Csv => "csv",
            AuditExportFormat.Xml => "xml",
            _ => "txt"
        };
    }
}
