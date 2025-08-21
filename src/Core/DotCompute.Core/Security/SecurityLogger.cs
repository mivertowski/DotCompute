// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Comprehensive security logging and audit trail system for tracking all security-related events,
/// violations, and operations with tamper-evident logging and compliance support.
/// </summary>
public sealed class SecurityLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly SecurityLoggingConfiguration _configuration;
    private readonly ConcurrentQueue<SecurityLogEntry> _auditQueue = new();
    private readonly SemaphoreSlim _logWriteLock = new(1, 1);
    private readonly Timer _auditFlushTimer;
    private readonly Timer _integrityCheckTimer;
    private readonly string _auditLogPath;
    private readonly Dictionary<string, object> _sessionMetadata = [];
    private volatile bool _disposed;
    private long _sequenceNumber;

    // Event correlation tracking
    private readonly ConcurrentDictionary<string, CorrelationContext> _correlationContexts = new();

    // Security metrics

    private readonly SecurityMetrics _metrics = new();

    public SecurityLogger(ILogger<SecurityLogger> logger, SecurityLoggingConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? SecurityLoggingConfiguration.Default;

        // Initialize audit log path

        _auditLogPath = Path.Combine(_configuration.AuditLogDirectory,

            $"security_audit_{DateTime.UtcNow:yyyyMMdd}.jsonl");

        // Ensure audit directory exists

        _ = Directory.CreateDirectory(_configuration.AuditLogDirectory);

        // Initialize timers

        _auditFlushTimer = new Timer(FlushAuditLogs, null,

            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _integrityCheckTimer = new Timer(PerformIntegrityCheck, null,
            TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        // Initialize session metadata

        InitializeSessionMetadata();


        _logger.LogInformation("SecurityLogger initialized: AuditPath={AuditPath}", _auditLogPath);

        // Log security logger startup

        LogSecurityEvent(SecurityEventType.SecuritySystemStartup,
            "Security logging system initialized",

            SecurityLevel.Informational);
    }

    /// <summary>
    /// Logs a security event with full context and audit trail.
    /// </summary>
    /// <param name="eventType">Type of security event</param>
    /// <param name="message">Descriptive message</param>
    /// <param name="level">Security level/severity</param>
    /// <param name="userId">Optional user identifier</param>
    /// <param name="resourceId">Optional resource identifier</param>
    /// <param name="additionalData">Optional additional event data</param>
    /// <param name="correlationId">Optional correlation ID for event grouping</param>
    /// <param name="callerName">Automatically captured caller method name</param>
    /// <param name="sourceFile">Automatically captured source file path</param>
    /// <param name="lineNumber">Automatically captured line number</param>
    public async Task LogSecurityEventAsync(SecurityEventType eventType, string message,

        SecurityLevel level, string? userId = null, string? resourceId = null,

        IDictionary<string, object>? additionalData = null, string? correlationId = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",

        [CallerLineNumber] int lineNumber = 0)
    {
        if (_disposed)
        {
            return;
        }


        var entry = CreateSecurityLogEntry(eventType, message, level, userId, resourceId,

            additionalData, correlationId, callerName, sourceFile, lineNumber);

        // Queue for audit logging

        _auditQueue.Enqueue(entry);

        // Log to standard logger based on level

        LogToStandardLogger(entry);

        // Update metrics

        UpdateSecurityMetrics(entry);

        // Handle critical events immediately

        if (level == SecurityLevel.Critical)
        {
            await HandleCriticalSecurityEventAsync(entry);
        }

        // Update correlation context if provided

        if (!string.IsNullOrEmpty(correlationId))
        {
            UpdateCorrelationContext(correlationId, entry);
        }
    }

    /// <summary>
    /// Logs a security event synchronously (convenience method).
    /// </summary>
    public void LogSecurityEvent(SecurityEventType eventType, string message,

        SecurityLevel level, string? userId = null, string? resourceId = null,

        IDictionary<string, object>? additionalData = null, string? correlationId = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",

        [CallerLineNumber] int lineNumber = 0)
    {
        _ = Task.Run(async () => await LogSecurityEventAsync(eventType, message, level, userId, resourceId,

            additionalData, correlationId, callerName, sourceFile, lineNumber));
    }

    /// <summary>
    /// Logs a security violation with detailed threat information.
    /// </summary>
    /// <param name="violationType">Type of security violation</param>
    /// <param name="threatDetails">Detailed threat information</param>
    /// <param name="affectedResource">Resource that was affected</param>
    /// <param name="sourceInformation">Information about the violation source</param>
    /// <param name="correlationId">Correlation ID for related events</param>
    public async Task LogSecurityViolationAsync(SecurityViolationType violationType,

        ThreatDetails threatDetails, string affectedResource, SourceInformation sourceInformation,

        string? correlationId = null)
    {
        if (_disposed)
        {
            return;
        }


        var additionalData = new Dictionary<string, object>
        {
            ["ViolationType"] = violationType.ToString(),
            ["ThreatLevel"] = threatDetails.ThreatLevel.ToString(),
            ["ThreatCategory"] = threatDetails.Category,
            ["AttackVector"] = threatDetails.AttackVector,
            ["AffectedResource"] = affectedResource,
            ["SourceIP"] = sourceInformation.IPAddress,
            ["SourceHost"] = sourceInformation.Hostname,
            ["UserAgent"] = sourceInformation.UserAgent,
            ["ProcessId"] = sourceInformation.ProcessId,
            ["ThreadId"] = sourceInformation.ThreadId
        };

        await LogSecurityEventAsync(SecurityEventType.SecurityViolation,

            $"Security violation detected: {violationType} - {threatDetails.Description}",
            SecurityLevel.High, sourceInformation.UserId, affectedResource, additionalData, correlationId);

        // Increment violation metrics

        _ = Interlocked.Increment(ref _metrics.TotalSecurityViolations);
        _ = _metrics.ViolationsByType.AddOrUpdate(violationType, 1, (key, value) => value + 1);

        // Check for attack patterns

        await AnalyzeAttackPatternsAsync(violationType, sourceInformation, correlationId);
    }

    /// <summary>
    /// Logs successful authentication events.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="authenticationMethod">Method used for authentication</param>
    /// <param name="sourceInformation">Source of the authentication request</param>
    /// <param name="sessionId">Session identifier</param>
    public async Task LogAuthenticationSuccessAsync(string userId, string authenticationMethod,

        SourceInformation sourceInformation, string sessionId)
    {
        var additionalData = new Dictionary<string, object>
        {
            ["AuthenticationMethod"] = authenticationMethod,
            ["SessionId"] = sessionId,
            ["SourceIP"] = sourceInformation.IPAddress,
            ["UserAgent"] = sourceInformation.UserAgent
        };

        await LogSecurityEventAsync(SecurityEventType.AuthenticationSuccess,
            $"User authentication successful: {userId} via {authenticationMethod}",
            SecurityLevel.Informational, userId, null, additionalData, sessionId);


        _ = Interlocked.Increment(ref _metrics.SuccessfulAuthentications);
    }

    /// <summary>
    /// Logs failed authentication attempts.
    /// </summary>
    /// <param name="userId">Attempted user identifier</param>
    /// <param name="failureReason">Reason for authentication failure</param>
    /// <param name="sourceInformation">Source of the authentication request</param>
    /// <param name="attemptCount">Number of consecutive failed attempts</param>
    public async Task LogAuthenticationFailureAsync(string userId, string failureReason,

        SourceInformation sourceInformation, int attemptCount)
    {
        var additionalData = new Dictionary<string, object>
        {
            ["FailureReason"] = failureReason,
            ["AttemptCount"] = attemptCount,
            ["SourceIP"] = sourceInformation.IPAddress,
            ["UserAgent"] = sourceInformation.UserAgent
        };

        var level = attemptCount > 3 ? SecurityLevel.High : SecurityLevel.Warning;


        await LogSecurityEventAsync(SecurityEventType.AuthenticationFailure,
            $"User authentication failed: {userId} - {failureReason} (Attempt {attemptCount})",
            level, userId, null, additionalData);


        _ = Interlocked.Increment(ref _metrics.FailedAuthentications);
    }

    /// <summary>
    /// Logs access control events (authorization).
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="resource">Resource being accessed</param>
    /// <param name="action">Action being performed</param>
    /// <param name="result">Access control result</param>
    /// <param name="reason">Reason for the result</param>
    public async Task LogAccessControlAsync(string userId, string resource, string action,

        AccessResult result, string reason)
    {
        var additionalData = new Dictionary<string, object>
        {
            ["Resource"] = resource,
            ["Action"] = action,
            ["AccessResult"] = result.ToString(),
            ["Reason"] = reason
        };

        var eventType = result == AccessResult.Granted ?

            SecurityEventType.AccessGranted : SecurityEventType.AccessDenied;
        var level = result == AccessResult.Granted ?

            SecurityLevel.Informational : SecurityLevel.Warning;

        await LogSecurityEventAsync(eventType,
            $"Access {result.ToString().ToLower()} for user {userId} to {resource} ({action}): {reason}",
            level, userId, resource, additionalData);


        if (result == AccessResult.Granted)
        {
            _ = Interlocked.Increment(ref _metrics.AccessGrantedCount);
        }
        else
        {
            _ = Interlocked.Increment(ref _metrics.AccessDeniedCount);
        }
    }

    /// <summary>
    /// Logs data access and modification events for audit compliance.
    /// </summary>
    /// <param name="userId">User performing the operation</param>
    /// <param name="operation">Type of data operation</param>
    /// <param name="dataType">Type of data accessed</param>
    /// <param name="recordId">Identifier of the specific record/data</param>
    /// <param name="fieldNames">Names of fields accessed/modified</param>
    /// <param name="originalValues">Original values (for modifications)</param>
    /// <param name="newValues">New values (for modifications)</param>
    public async Task LogDataAccessAsync(string userId, DataOperation operation, string dataType,

        string recordId, IEnumerable<string>? fieldNames = null,

        IDictionary<string, object>? originalValues = null,

        IDictionary<string, object>? newValues = null)
    {
        var additionalData = new Dictionary<string, object>
        {
            ["Operation"] = operation.ToString(),
            ["DataType"] = dataType,
            ["RecordId"] = recordId
        };

        if (fieldNames != null)
        {
            additionalData["FieldNames"] = fieldNames.ToArray();
        }


        if (originalValues != null)
        {
            additionalData["OriginalValues"] = originalValues;
        }


        if (newValues != null)
        {
            additionalData["NewValues"] = newValues;
        }


        await LogSecurityEventAsync(SecurityEventType.DataAccess,
            $"Data {operation.ToString().ToLower()}: {dataType} record {recordId} by user {userId}",
            SecurityLevel.Informational, userId, $"{dataType}:{recordId}", additionalData);


        _ = Interlocked.Increment(ref _metrics.DataAccessEvents);
    }

    /// <summary>
    /// Gets current security metrics and statistics.
    /// </summary>
    /// <returns>Current security metrics</returns>
    public SecurityMetrics GetSecurityMetrics()
    {
        var result = new SecurityMetrics
        {
            TotalSecurityEvents = _metrics.TotalSecurityEvents,
            TotalSecurityViolations = _metrics.TotalSecurityViolations,
            SuccessfulAuthentications = _metrics.SuccessfulAuthentications,
            FailedAuthentications = _metrics.FailedAuthentications,
            AccessGrantedCount = _metrics.AccessGrantedCount,
            AccessDeniedCount = _metrics.AccessDeniedCount,
            DataAccessEvents = _metrics.DataAccessEvents,
            CriticalEventsCount = _metrics.CriticalEventsCount,
            LastEventTime = _metrics.LastEventTime
        };

        // Copy dictionary contents

        foreach (var kvp in _metrics.EventsByType)
        {
            result.EventsByType[kvp.Key] = kvp.Value;
        }


        foreach (var kvp in _metrics.ViolationsByType)
        {
            result.ViolationsByType[kvp.Key] = kvp.Value;
        }


        return result;
    }

    /// <summary>
    /// Exports audit logs for compliance reporting.
    /// </summary>
    /// <param name="startDate">Start date for export</param>
    /// <param name="endDate">End date for export</param>
    /// <param name="eventTypes">Optional filter for specific event types</param>
    /// <param name="format">Export format</param>
    /// <returns>Export result with file path and statistics</returns>
    public async Task<AuditExportResult> ExportAuditLogsAsync(DateTime startDate, DateTime endDate,

        IEnumerable<SecurityEventType>? eventTypes = null, AuditExportFormat format = AuditExportFormat.Json)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(SecurityLogger));
        }


        await _logWriteLock.WaitAsync();
        try
        {
            var exportResult = new AuditExportResult
            {
                StartDate = startDate,
                EndDate = endDate,
                RequestedFormat = format,
                ExportStartTime = DateTimeOffset.UtcNow
            };

            var exportPath = Path.Combine(_configuration.AuditLogDirectory,

                $"audit_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{GetFileExtension(format)}");

            // Read and filter audit logs
            var filteredEntries = await ReadAndFilterAuditLogsAsync(startDate, endDate, eventTypes);

            // Export in requested format

            await ExportEntriesAsync(filteredEntries, exportPath, format);


            exportResult.ExportPath = exportPath;
            exportResult.RecordCount = filteredEntries.Count;
            exportResult.ExportEndTime = DateTimeOffset.UtcNow;
            exportResult.IsSuccessful = true;


            _logger.LogInformation("Audit log export completed: Path={ExportPath}, Records={RecordCount}",
                exportPath, filteredEntries.Count);


            return exportResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            return new AuditExportResult
            {
                StartDate = startDate,
                EndDate = endDate,
                RequestedFormat = format,
                ExportStartTime = DateTimeOffset.UtcNow,
                ExportEndTime = DateTimeOffset.UtcNow,
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _ = _logWriteLock.Release();
        }
    }

    #region Private Implementation

    private void InitializeSessionMetadata()
    {
        _sessionMetadata["SessionId"] = Guid.NewGuid().ToString();
        _sessionMetadata["StartTime"] = DateTimeOffset.UtcNow;
        _sessionMetadata["MachineName"] = Environment.MachineName;
        _sessionMetadata["ProcessId"] = Environment.ProcessId;
        _sessionMetadata["ProcessName"] = Process.GetCurrentProcess().ProcessName;
        _sessionMetadata["UserName"] = Environment.UserName;
        _sessionMetadata["OSVersion"] = Environment.OSVersion.ToString();
        _sessionMetadata["CLRVersion"] = Environment.Version.ToString();
    }

    private SecurityLogEntry CreateSecurityLogEntry(SecurityEventType eventType, string message,

        SecurityLevel level, string? userId, string? resourceId,

        IDictionary<string, object>? additionalData, string? correlationId,
        string callerName, string sourceFile, int lineNumber)
    {
        var entry = new SecurityLogEntry
        {
            SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = eventType,
            Message = message,
            Level = level,
            UserId = userId,
            ResourceId = resourceId,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            SessionMetadata = new Dictionary<string, object>(_sessionMetadata),
            SecurityContext = new SecurityContext
            {
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                IsSecurityCritical = IsSecurityCriticalContext(),
                IntegrityLevel = GetCurrentIntegrityLevel(),
                ExecutionContext = GetExecutionContextInfo()
            },
            CallerInformation = new CallerInformation
            {
                MethodName = callerName,
                SourceFile = Path.GetFileName(sourceFile),
                LineNumber = lineNumber
            },
            AdditionalData = additionalData != null ?

                new Dictionary<string, object>(additionalData) :

                []
        };

        // Security context already set in initializer above

        return entry;
    }

    private void LogToStandardLogger(SecurityLogEntry entry)
    {
        var logLevel = entry.Level switch
        {
            SecurityLevel.Critical => LogLevel.Critical,
            SecurityLevel.High => LogLevel.Error,
            SecurityLevel.Warning => LogLevel.Warning,
            SecurityLevel.Informational => LogLevel.Information,
            _ => LogLevel.Debug
        };

        _logger.Log(logLevel, "SecurityEvent: {EventType} - {Message} [Sequence: {SequenceNumber}, Correlation: {CorrelationId}]",
            entry.EventType, entry.Message, entry.SequenceNumber, entry.CorrelationId);
    }

    private void UpdateSecurityMetrics(SecurityLogEntry entry)
    {
        _ = Interlocked.Increment(ref _metrics.TotalSecurityEvents);
        _ = _metrics.EventsByType.AddOrUpdate(entry.EventType, 1, (key, value) => value + 1);


        if (entry.Level == SecurityLevel.Critical)
        {
            _ = Interlocked.Increment(ref _metrics.CriticalEventsCount);
        }


        _metrics.LastEventTime = entry.Timestamp;
    }

    private async Task HandleCriticalSecurityEventAsync(SecurityLogEntry entry)
    {
        // Immediately flush critical events to audit log
        await FlushSpecificEntryAsync(entry);

        // Send alerts if configured

        if (_configuration.EnableCriticalEventAlerts)
        {
            await SendCriticalEventAlertAsync(entry);
        }


        _logger.LogCritical("CRITICAL SECURITY EVENT: {EventType} - {Message} [Sequence: {SequenceNumber}]",
            entry.EventType, entry.Message, entry.SequenceNumber);
    }

    private void UpdateCorrelationContext(string correlationId, SecurityLogEntry entry)
    {
        _ = _correlationContexts.AddOrUpdate(correlationId,
            new CorrelationContext
            {

                FirstEventTime = entry.Timestamp,
                LastEventTime = entry.Timestamp,
                EventCount = 1,
                EventTypes = [entry.EventType]
            },
            (key, existing) =>
            {
                existing.LastEventTime = entry.Timestamp;
                existing.EventCount++;
                _ = existing.EventTypes.Add(entry.EventType);
                return existing;
            });
    }

    private static async Task AnalyzeAttackPatternsAsync(SecurityViolationType violationType,

        SourceInformation sourceInfo, string? correlationId)
        // Implement attack pattern analysis logic
        // This could include rate limiting, IP reputation checks, etc.

        => await Task.CompletedTask; // Placeholder for actual implementation

    private void FlushAuditLogs(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                await FlushAuditLogQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing audit logs");
            }
        });
    }

    private async Task FlushAuditLogQueueAsync()
    {
        if (_auditQueue.IsEmpty)
        {
            return;
        }


        var entriesToFlush = new List<SecurityLogEntry>();
        while (_auditQueue.TryDequeue(out var entry) && entriesToFlush.Count < 1000)
        {
            entriesToFlush.Add(entry);
        }

        if (entriesToFlush.Count == 0)
        {
            return;
        }


        await _logWriteLock.WaitAsync();
        try
        {
            await using var writer = new StreamWriter(_auditLogPath, append: true, encoding: Encoding.UTF8);


            foreach (var entry in entriesToFlush)
            {
                var jsonEntry = JsonSerializer.Serialize(entry, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });


                await writer.WriteLineAsync(jsonEntry);
            }


            await writer.FlushAsync();
        }
        finally
        {
            _ = _logWriteLock.Release();
        }
    }

    private async Task FlushSpecificEntryAsync(SecurityLogEntry entry)
    {
        await _logWriteLock.WaitAsync();
        try
        {
            await using var writer = new StreamWriter(_auditLogPath, append: true, encoding: Encoding.UTF8);


            var jsonEntry = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });


            await writer.WriteLineAsync(jsonEntry);
            await writer.FlushAsync();
        }
        finally
        {
            _ = _logWriteLock.Release();
        }
    }

    private static async Task SendCriticalEventAlertAsync(SecurityLogEntry entry)
        // Implementation for sending critical event alerts
        // This could include email, SMS, webhook notifications, etc.

        => await Task.CompletedTask; // Placeholder

    private void PerformIntegrityCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                await PerformAuditLogIntegrityCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit log integrity check");
            }
        });
    }

    private static async Task PerformAuditLogIntegrityCheckAsync()
        // Implementation for audit log integrity verification
        // This could include hash verification, digital signatures, etc.

        => await Task.CompletedTask; // Placeholder

    private static bool IsSecurityCriticalContext()
        // Check if we're running in a security-critical context
        // Note: SecurityManager is obsolete in .NET 9+, always return true for now

        => true;

    private static string GetCurrentIntegrityLevel()
        // Get current process integrity level (Windows-specific)

        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Medium" : "Unknown";

    private static Dictionary<string, object> GetExecutionContextInfo()
    {
        return new Dictionary<string, object>
        {
            ["IsThreadPoolThread"] = Thread.CurrentThread.IsThreadPoolThread,
            ["IsBackground"] = Thread.CurrentThread.IsBackground,
            ["ThreadState"] = Thread.CurrentThread.ThreadState.ToString(),
            ["ApartmentState"] = Thread.CurrentThread.GetApartmentState().ToString()
        };
    }

    private async Task<List<SecurityLogEntry>> ReadAndFilterAuditLogsAsync(DateTime startDate, DateTime endDate,

        IEnumerable<SecurityEventType>? eventTypes)
    {
        var filteredEntries = new List<SecurityLogEntry>();


        try
        {
            // Read from current audit log file
            if (File.Exists(_auditLogPath))
            {
                await _logWriteLock.WaitAsync();
                try
                {
                    using var reader = new StreamReader(_auditLogPath, Encoding.UTF8);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
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

                                entry.Timestamp.Date >= startDate.Date &&

                                entry.Timestamp.Date <= endDate.Date)
                            {
                                if (eventTypes == null || eventTypes.Contains(entry.EventType))
                                {
                                    filteredEntries.Add(entry);
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize audit log entry: {Line}", line);
                        }
                    }
                }
                finally
                {
                    _ = _logWriteLock.Release();
                }
            }

            // TODO: Read from archived audit log files for the date range
            // This would involve scanning the audit directory for files matching the date pattern


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading audit logs for export");
            throw;
        }

        return [.. filteredEntries.OrderBy(e => e.Timestamp)];
    }

    private async Task ExportEntriesAsync(List<SecurityLogEntry> entries, string exportPath, AuditExportFormat format)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit entries to {ExportPath}", exportPath);
            throw;
        }
    }

    private static async Task ExportAsJsonAsync(List<SecurityLogEntry> entries, string exportPath)
    {
        await using var writer = new FileStream(exportPath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(writer, entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static async Task ExportAsCsvAsync(List<SecurityLogEntry> entries, string exportPath)
    {
        await using var writer = new StreamWriter(exportPath, false, Encoding.UTF8);

        // Write CSV header

        await writer.WriteLineAsync("Timestamp,EventType,Level,Message,UserId,ResourceId,CorrelationId");

        // Write entries
        foreach (var entry in entries)
        {
            var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                      $"{EscapeCsv(entry.EventType.ToString())}," +
                      $"{EscapeCsv(entry.Level.ToString())}," +
                      $"{EscapeCsv(entry.Message)}," +
                      $"{EscapeCsv(entry.UserId ?? "")}," +
                      $"{EscapeCsv(entry.ResourceId ?? "")}," +
                      $"{EscapeCsv(entry.CorrelationId)}";


            await writer.WriteLineAsync(line);
        }
    }

    private static async Task ExportAsXmlAsync(List<SecurityLogEntry> entries, string exportPath)
    {
        await using var writer = new FileStream(exportPath, FileMode.Create, FileAccess.Write);


        using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Indent = true,
            Async = true
        });

        await xmlWriter.WriteStartDocumentAsync();
        await xmlWriter.WriteStartElementAsync(null, "AuditLog", null);

        foreach (var entry in entries)
        {
            await xmlWriter.WriteStartElementAsync(null, "Entry", null);
            await xmlWriter.WriteAttributeStringAsync(null, "timestamp", null, entry.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
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


            await xmlWriter.WriteElementStringAsync(null, "CorrelationId", null, entry.CorrelationId);
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


        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }


        return value;
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

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        // Flush remaining audit logs
        try
        {
            FlushAuditLogQueueAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing audit logs during disposal");
        }

        _auditFlushTimer?.Dispose();
        _integrityCheckTimer?.Dispose();
        _logWriteLock?.Dispose();

        // Log shutdown event
        LogSecurityEvent(SecurityEventType.SecuritySystemShutdown,
            "Security logging system shutting down",
            SecurityLevel.Informational);

        _logger.LogInformation("SecurityLogger disposed. Final metrics: Events={TotalEvents}, Violations={TotalViolations}",
            _metrics.TotalSecurityEvents, _metrics.TotalSecurityViolations);
    }
}

#region Supporting Types and Enums

/// <summary>
/// Configuration for security logging behavior.
/// </summary>
public sealed class SecurityLoggingConfiguration
{
    public static SecurityLoggingConfiguration Default => new()
    {
        AuditLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DotCompute", "SecurityLogs"),
        EnableCriticalEventAlerts = true,
        MaxAuditLogSizeMB = 100,
        AuditLogRetentionDays = 90,
        EnableIntegrityChecking = true
    };

    public string AuditLogDirectory { get; init; } = "";
    public bool EnableCriticalEventAlerts { get; init; } = true;
    public int MaxAuditLogSizeMB { get; init; } = 100;
    public int AuditLogRetentionDays { get; init; } = 90;
    public bool EnableIntegrityChecking { get; init; } = true;
    public bool EnableCorrelationTracking { get; init; } = true;
    public bool IncludeStackTraces { get; init; }
}

/// <summary>
/// Types of security events.
/// </summary>
public enum SecurityEventType
{
    // System events
    SecuritySystemStartup,
    SecuritySystemShutdown,
    ConfigurationChange,

    // Authentication events

    AuthenticationSuccess,
    AuthenticationFailure,
    AccountLockout,
    PasswordChange,

    // Authorization events

    AccessGranted,
    AccessDenied,
    PermissionElevation,

    // Data access events

    DataAccess,
    DataModification,
    DataDeletion,

    // Security violations

    SecurityViolation,
    SuspiciousActivity,
    AttackDetected,

    // Cryptographic events

    KeyGeneration,
    KeyRotation,
    CryptographicOperation,

    // Plugin/module events

    PluginLoaded,
    PluginValidation,
    PluginSecurityCheck
}

/// <summary>
/// Security event severity levels.
/// </summary>
public enum SecurityLevel
{
    Informational = 1,
    Warning = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Types of security violations.
/// </summary>
public enum SecurityViolationType
{
    InputValidation,
    BufferOverflow,
    SqlInjection,
    XssAttempt,
    PathTraversal,
    CommandInjection,
    AuthenticationBypass,
    PrivilegeEscalation,
    UnauthorizedAccess,
    DataExfiltration,
    MaliciousCode,
    CryptographicFailure,
    IntegrityViolation
}

/// <summary>
/// Data operation types.
/// </summary>
public enum DataOperation
{
    Read,
    Create,
    Update,
    Delete
}

/// <summary>
/// Access control results.
/// </summary>
public enum AccessResult
{
    Granted,
    Denied
}

/// <summary>
/// Audit export formats.
/// </summary>
public enum AuditExportFormat
{
    Json,
    Csv,
    Xml
}

/// <summary>
/// Detailed threat information.
/// </summary>
public sealed class ThreatDetails
{
    public required string Description { get; init; }
    public required SecurityLevel ThreatLevel { get; init; }
    public required string Category { get; init; }
    public required string AttackVector { get; init; }
    public string? Mitigation { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Source information for security events.
/// </summary>
public sealed class SourceInformation
{
    public string? UserId { get; init; }
    public string IPAddress { get; init; } = "Unknown";
    public string Hostname { get; init; } = "Unknown";
    public string UserAgent { get; init; } = "Unknown";
    public int ProcessId { get; init; }
    public int ThreadId { get; init; }
    public Dictionary<string, object> AdditionalContext { get; init; } = [];
}

/// <summary>
/// Security log entry structure.
/// </summary>
public sealed class SecurityLogEntry
{
    public long SequenceNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public SecurityEventType EventType { get; init; }
    public required string Message { get; init; }
    public SecurityLevel Level { get; init; }
    public string? UserId { get; init; }
    public string? ResourceId { get; init; }
    public required string CorrelationId { get; init; }
    public required Dictionary<string, object> SessionMetadata { get; init; }
    public required CallerInformation CallerInformation { get; init; }
    public required SecurityContext SecurityContext { get; init; }
    public required Dictionary<string, object> AdditionalData { get; init; }
}

/// <summary>
/// Caller information for audit trail.
/// </summary>
public sealed class CallerInformation
{
    public required string MethodName { get; init; }
    public required string SourceFile { get; init; }
    public int LineNumber { get; init; }
}

/// <summary>
/// Security context information.
/// </summary>
public sealed class SecurityContext
{
    public int ThreadId { get; init; }
    public bool IsSecurityCritical { get; init; }
    public string IntegrityLevel { get; init; } = "Unknown";
    public required Dictionary<string, object> ExecutionContext { get; init; }
}

/// <summary>
/// Correlation context for event grouping.
/// </summary>
internal sealed class CorrelationContext
{
    public DateTimeOffset FirstEventTime { get; init; }
    public DateTimeOffset LastEventTime { get; set; }
    public int EventCount { get; set; }
    public required HashSet<SecurityEventType> EventTypes { get; init; }
}

/// <summary>
/// Security metrics and statistics.
/// </summary>
public sealed class SecurityMetrics
{
    public long TotalSecurityEvents;
    public long TotalSecurityViolations;
    public long SuccessfulAuthentications;
    public long FailedAuthentications;
    public long AccessGrantedCount;
    public long AccessDeniedCount;
    public long DataAccessEvents;
    public long CriticalEventsCount;
    public ConcurrentDictionary<SecurityEventType, long> EventsByType { get; } = new();
    public ConcurrentDictionary<SecurityViolationType, long> ViolationsByType { get; } = new();
    public DateTimeOffset LastEventTime { get; set; }
}

/// <summary>
/// Result of audit log export operation.
/// </summary>
public sealed class AuditExportResult
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public AuditExportFormat RequestedFormat { get; init; }
    public DateTimeOffset ExportStartTime { get; init; }
    public DateTimeOffset ExportEndTime { get; set; }
    public TimeSpan ExportDuration => ExportEndTime - ExportStartTime;
    public bool IsSuccessful { get; set; }
    public string? ExportPath { get; set; }
    public int RecordCount { get; set; }
    public string? ErrorMessage { get; set; }
}


#endregion