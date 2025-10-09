// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using System;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Comprehensive input sanitization service that validates and sanitizes all forms of input
/// to prevent injection attacks, data corruption, and security vulnerabilities.
/// </summary>
public sealed partial class InputSanitizer : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 8001,
        Level = MsLogLevel.Information,
        Message = "InputSanitizer initialized with configuration: {Configuration}")]
    private static partial void LogInitialized(ILogger logger, string configuration);

    [LoggerMessage(
        EventId = 8002,
        Level = MsLogLevel.Trace,
        Message = "Sanitizing input: Context={Context}, Type={Type}, Length={Length}")]
    private static partial void LogSanitizingInput(ILogger logger, string context, SanitizationType type, int length);

    [LoggerMessage(
        EventId = 8003,
        Level = MsLogLevel.Trace,
        Message = "Input sanitization completed: Context={Context}, Secure={IsSecure}, Threats={ThreatCount}")]
    private static partial void LogSanitizationCompleted(ILogger logger, string context, bool isSecure, int threatCount);

    [LoggerMessage(
        EventId = 8004,
        Level = MsLogLevel.Debug,
        Message = "Validating kernel parameters: Kernel={KernelName}, ParameterCount={ParameterCount}")]
    private static partial void LogValidatingKernelParameters(ILogger logger, string kernelName, int parameterCount);

    [LoggerMessage(
        EventId = 8005,
        Level = MsLogLevel.Debug,
        Message = "Kernel parameter validation completed: Kernel={KernelName}, Valid={IsValid}, Invalid={InvalidCount}")]
    private static partial void LogKernelParameterValidationCompleted(ILogger logger, string kernelName, bool isValid, int invalidCount);

    [LoggerMessage(
        EventId = 8006,
        Level = MsLogLevel.Trace,
        Message = "Validating file path: Path={Path}, BaseDirectory={BaseDirectory}")]
    private static partial void LogValidatingFilePath(ILogger logger, string path, string baseDirectory);

    [LoggerMessage(
        EventId = 8007,
        Level = MsLogLevel.Trace,
        Message = "File path validation completed: Valid={IsValid}, Threats={ThreatCount}")]
    private static partial void LogFilePathValidationCompleted(ILogger logger, bool isValid, int threatCount);

    [LoggerMessage(
        EventId = 8008,
        Level = MsLogLevel.Error,
        Message = "Error validating file path: {FilePath}")]
    private static partial void LogFilePathValidationError(ILogger logger, Exception ex, string filePath);

    [LoggerMessage(
        EventId = 8009,
        Level = MsLogLevel.Trace,
        Message = "Work group validation completed: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}")]
    private static partial void LogWorkGroupValidationCompleted(ILogger logger, bool isValid, int errorCount, int warningCount);

    [LoggerMessage(
        EventId = 8010,
        Level = MsLogLevel.Error,
        Message = "Error validating work group sizes")]
    private static partial void LogWorkGroupValidationError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8011,
        Level = MsLogLevel.Debug,
        Message = "Added custom validation rule: Context={Context}, Rule={RuleName}")]
    private static partial void LogCustomRuleAdded(ILogger logger, string context, string ruleName);

    [LoggerMessage(
        EventId = 8012,
        Level = MsLogLevel.Information,
        Message = "Input validation statistics: Validations={TotalValidations}, Threats={TotalThreats}, Violations={TotalViolations}")]
    private static partial void LogStatistics(ILogger logger, long totalValidations, long totalThreats, long totalViolations);

    [LoggerMessage(
        EventId = 8013,
        Level = MsLogLevel.Warning,
        Message = "Error logging statistics")]
    private static partial void LogStatisticsError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8014,
        Level = MsLogLevel.Information,
        Message = "InputSanitizer disposed. Final statistics: Validations={TotalValidations}, Threats={TotalThreats}")]
    private static partial void LogDisposed(ILogger logger, long totalValidations, long totalThreats);

    #endregion

    private readonly ILogger _logger;
    private readonly InputSanitizationConfiguration _configuration;
    private readonly ConcurrentDictionary<string, ValidationRule> _customRules = new();
    private readonly SemaphoreSlim _validationLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly Timer _statisticsTimer;
    private volatile bool _disposed;

    // Source-generated regex patterns for AOT compatibility
    [GeneratedRegex(@"('|(--|;)|(\||(\*|%))|(\b(select|insert|update|delete|drop|create|alter|exec|execute|union|script)\b))", RegexOptions.IgnoreCase)]
    private static partial Regex SqlInjectionBasicRegex();

    [GeneratedRegex(@"(\b(sleep|benchmark|pg_sleep|waitfor|delay)\b)|(\b(xp_cmdshell|sp_executesql)\b)|(\b(information_schema|sys\.|master\.)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlInjectionAdvancedRegex();

    [GeneratedRegex(@"<script[^>]*>.*?</script>|javascript:|vbscript:|on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex XssScriptRegex();

    [GeneratedRegex(@"<\s*(iframe|object|embed|applet|meta|link|style|img|svg)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex XssHtmlRegex();

    [GeneratedRegex(@"[;&|`$(){}[\]<>*?~]|(\\x[0-9a-f]{2})|(%[0-9a-f]{2})", RegexOptions.IgnoreCase)]
    private static partial Regex CommandInjectionRegex();

    [GeneratedRegex(@"(invoke-expression|iex|invoke-command|icm|start-process|saps|new-object|downloadstring)", RegexOptions.IgnoreCase)]
    private static partial Regex PowershellInjectionRegex();

    [GeneratedRegex(@"(\.\.[\\/])+|(%2e%2e[\\/])+|(\.\.%2f)|(\.\.%5c)", RegexOptions.IgnoreCase)]
    private static partial Regex PathTraversalRegex();

    [GeneratedRegex(@"[()&|!>=<~*/]|(\\\*)|(\\\()|(\\\))")]
    private static partial Regex LdapInjectionRegex();

    [GeneratedRegex(@"(eval|exec|system|shell_exec|passthru|popen|proc_open|file_get_contents|readfile|include|require)", RegexOptions.IgnoreCase)]
    private static partial Regex CodeInjectionRegex();

    [GeneratedRegex(@"(<!\[CDATA\[)|(<\?xml)|(<!\-\-)|(\]\]>)|(&lt;)|(&gt;)|(&amp;)|(&quot;)|(&apos;)", RegexOptions.IgnoreCase)]
    private static partial Regex XmlInjectionRegex();

    [GeneratedRegex(@"(\$where|\$regex|\$ne|\$gt|\$lt|\$or|\$and|\$in|\$nin)", RegexOptions.IgnoreCase)]
    private static partial Regex NoSqlInjectionRegex();

    [GeneratedRegex(@"[^\w@.-]")]
    private static partial Regex EmailSanitizeRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex AlphaNumericSanitizeRegex();

    [GeneratedRegex(@"[^0-9.-]")]
    private static partial Regex NumericSanitizeRegex();

    // Pre-compiled regex patterns for common attacks
    private static readonly Dictionary<string, Func<Regex>> SecurityPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sql_injection_basic"] = SqlInjectionBasicRegex,
        ["sql_injection_advanced"] = SqlInjectionAdvancedRegex,
        ["xss_script"] = XssScriptRegex,
        ["xss_html"] = XssHtmlRegex,
        ["command_injection"] = CommandInjectionRegex,
        ["powershell_injection"] = PowershellInjectionRegex,
        ["path_traversal"] = PathTraversalRegex,
        ["ldap_injection"] = LdapInjectionRegex,
        ["code_injection"] = CodeInjectionRegex,
        ["xml_injection"] = XmlInjectionRegex,
        ["nosql_injection"] = NoSqlInjectionRegex
    };

    // Dangerous characters that need encoding/escaping
    private static readonly Dictionary<char, string> HtmlEntities = new()
    {
        ['<'] = "&lt;",
        ['>'] = "&gt;",
        ['&'] = "&amp;",
        ['"'] = "&quot;",
        ['\''] = "&#x27;",
        ['/'] = "&#x2F;"
    };

    private readonly ValidationStatistics _statistics = new();
    /// <summary>
    /// Initializes a new instance of the InputSanitizer class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public InputSanitizer(ILogger<InputSanitizer> logger, InputSanitizationConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _configuration = configuration ?? InputSanitizationConfiguration.Default;

        // Initialize statistics collection

        _statisticsTimer = new Timer(LogStatistics, null,

            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));


        LogInitialized(_logger, _configuration.ToString());
    }

    /// <summary>
    /// Validates and sanitizes a string input for security threats.
    /// </summary>
    /// <param name="input">The input string to sanitize</param>
    /// <param name="context">Context of the input (e.g., "username", "filepath", "sql_query")</param>
    /// <param name="sanitizationType">Type of sanitization to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sanitization result with cleaned input and security information</returns>
    public async Task<SanitizationResult> SanitizeStringAsync(string input, string context,

        SanitizationType sanitizationType = SanitizationType.General, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        await _validationLock.WaitAsync(cancellationToken);
        try
        {
            LogSanitizingInput(_logger, context, sanitizationType, input.Length);

            var result = new SanitizationResult
            {
                OriginalInput = input,
                Context = context,
                SanitizationType = sanitizationType,
                ProcessingStartTime = DateTimeOffset.UtcNow
            };

            // 1. Basic validation
            if (!ValidateBasicConstraints(input, context, result))
            {
                return result;
            }

            // 2. Detect security threats
            await DetectSecurityThreatsAsync(input, result, cancellationToken);

            // 3. Apply sanitization based on type
            result.SanitizedInput = await ApplySanitizationAsync(input, sanitizationType, result, cancellationToken);

            // 4. Post-sanitization validation
            await ValidatePostSanitizationAsync(result.SanitizedInput, result, cancellationToken);

            // 5. Final security check
            result.IsSecure = DetermineSecurityStatus(result);
            result.ProcessingEndTime = DateTimeOffset.UtcNow;

            // Update statistics
            UpdateStatistics(result);

            LogSanitizationCompleted(_logger, context, result.IsSecure, result.SecurityThreats.Count);

            return result;
        }
        finally
        {
            _ = _validationLock.Release();
        }
    }

    /// <summary>
    /// Validates kernel parameters for security compliance.
    /// </summary>
    /// <param name="parameters">Dictionary of parameters to validate</param>
    /// <param name="kernelName">Name of the kernel for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result for the parameters</returns>
    public async Task<ParameterValidationResult> ValidateKernelParametersAsync(
        IDictionary<string, object> parameters, string kernelName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);

        await _validationLock.WaitAsync(cancellationToken);
        try
        {
            LogValidatingKernelParameters(_logger, kernelName, parameters.Count);

            var result = new ParameterValidationResult
            {
                KernelName = kernelName,
                ParameterCount = parameters.Count,
                ValidationStartTime = DateTimeOffset.UtcNow
            };

            foreach (var (key, value) in parameters)
            {
                var paramResult = await ValidateParameterAsync(key, value, $"kernel_{kernelName}", cancellationToken);
                result.ParameterResults[key] = paramResult;


                if (!paramResult.IsSecure)
                {
                    result.HasInvalidParameters = true;
                    result.InvalidParameters.Add(key);
                }

                foreach (var threat in paramResult.SecurityThreats)
                {
                    result.SecurityThreats.Add(threat);
                }
            }

            result.IsValid = !result.HasInvalidParameters;
            result.ValidationEndTime = DateTimeOffset.UtcNow;

            LogKernelParameterValidationCompleted(_logger, kernelName, result.IsValid, result.InvalidParameters.Count);

            return result;
        }
        finally
        {
            _ = _validationLock.Release();
        }
    }

    /// <summary>
    /// Validates and sanitizes file paths to prevent path traversal attacks.
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <param name="baseDirectory">Base directory that path should be within</param>
    /// <param name="allowedExtensions">Optional list of allowed file extensions</param>
    /// <returns>Path validation result</returns>
    public PathValidationResult ValidateFilePath(string filePath, string baseDirectory,

        IEnumerable<string>? allowedExtensions = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        LogValidatingFilePath(_logger, filePath, baseDirectory);

        var result = new PathValidationResult
        {
            OriginalPath = filePath,
            BaseDirectory = baseDirectory
        };

        try
        {
            // 1. Check for path traversal patterns
            if (SecurityPatterns["path_traversal"]().IsMatch(filePath))
            {
                result.SecurityThreats.Add(new InputThreat
                {
                    ThreatType = ThreatType.PathTraversal,
                    Severity = ThreatSeverity.High,
                    Description = "Path traversal pattern detected",
                    Location = filePath
                });
            }

            // 2. Resolve full path and validate it's within base directory
            var fullPath = Path.GetFullPath(filePath);
            var fullBaseDirectory = Path.GetFullPath(baseDirectory);


            if (!fullPath.StartsWith(fullBaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                result.SecurityThreats.Add(new InputThreat
                {
                    ThreatType = ThreatType.PathTraversal,
                    Severity = ThreatSeverity.Critical,
                    Description = "Path escapes base directory",
                    Location = fullPath
                });
                result.IsValid = false;
                return result;
            }

            result.SanitizedPath = fullPath;

            // 3. Validate file extension if specified
            if (allowedExtensions != null)
            {
                var extension = Path.GetExtension(fullPath).ToUpper(CultureInfo.InvariantCulture);
                var allowedList = allowedExtensions.Select(ext => ext.ToUpper(CultureInfo.InvariantCulture)).ToList();


                if (!allowedList.Contains(extension))
                {
                    result.SecurityThreats.Add(new InputThreat
                    {
                        ThreatType = ThreatType.InvalidFileType,
                        Severity = ThreatSeverity.Medium,
                        Description = $"File extension '{extension}' not in allowed list",
                        Location = extension
                    });
                }
            }

            // 4. Check for suspicious file names
            var fileName = Path.GetFileName(fullPath);
            if (ContainsSuspiciousFileName(fileName))
            {
                result.SecurityThreats.Add(new InputThreat
                {
                    ThreatType = ThreatType.SuspiciousFileName,
                    Severity = ThreatSeverity.Medium,
                    Description = "Suspicious file name detected",
                    Location = fileName
                });
            }

            result.IsValid = !result.SecurityThreats.Any(t => t.Severity >= ThreatSeverity.High);

            LogFilePathValidationCompleted(_logger, result.IsValid, result.SecurityThreats.Count);
        }
        catch (Exception ex)
        {
            LogFilePathValidationError(_logger, ex, filePath);
            result.IsValid = false;
            result.SecurityThreats.Add(new InputThreat
            {
                ThreatType = ThreatType.ProcessingError,
                Severity = ThreatSeverity.High,
                Description = $"Path validation error: {ex.Message}",
                Location = filePath
            });
        }

        return result;
    }

    /// <summary>
    /// Validates work group sizes and dimensions for compute kernels.
    /// </summary>
    /// <param name="workGroupSize">Work group size dimensions</param>
    /// <param name="globalSize">Global size dimensions</param>
    /// <param name="maxWorkGroupSize">Maximum allowed work group size</param>
    /// <returns>Work group validation result</returns>
    public WorkGroupValidationResult ValidateWorkGroupSizes(int[] workGroupSize, int[] globalSize,

        int maxWorkGroupSize = 1024)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        ArgumentNullException.ThrowIfNull(workGroupSize);
        ArgumentNullException.ThrowIfNull(globalSize);

        var result = new WorkGroupValidationResult
        {
            WorkGroupSize = workGroupSize,
            GlobalSize = globalSize,
            MaxWorkGroupSize = maxWorkGroupSize
        };

        try
        {
            // 1. Validate dimensions match
            if (workGroupSize.Length != globalSize.Length)
            {
                result.ValidationErrors.Add("Work group and global size dimension mismatch");
                result.IsValid = false;
                return result;
            }

            // 2. Validate dimension limits (1D, 2D, 3D only)
            if (workGroupSize.Length is < 1 or > 3)
            {
                result.ValidationErrors.Add($"Invalid dimension count: {workGroupSize.Length}. Must be 1, 2, or 3");
                result.IsValid = false;
                return result;
            }

            // 3. Validate individual dimensions
            for (var i = 0; i < workGroupSize.Length; i++)
            {
                if (workGroupSize[i] <= 0)
                {
                    result.ValidationErrors.Add($"Work group size dimension {i} must be positive: {workGroupSize[i]}");
                    result.IsValid = false;
                }


                if (globalSize[i] <= 0)
                {
                    result.ValidationErrors.Add($"Global size dimension {i} must be positive: {globalSize[i]}");
                    result.IsValid = false;
                }

                // Check for integer overflow in calculations
                if (long.MaxValue / workGroupSize[i] < globalSize[i])
                {
                    result.ValidationErrors.Add($"Potential integer overflow in dimension {i}");
                    result.IsValid = false;
                }
            }

            // 4. Validate total work group size
            var totalWorkGroupSize = workGroupSize.Aggregate(1, (acc, size) => acc * size);
            if (totalWorkGroupSize > maxWorkGroupSize)
            {
                result.ValidationErrors.Add($"Total work group size {totalWorkGroupSize} exceeds maximum {maxWorkGroupSize}");
                result.IsValid = false;
            }

            // 5. Validate global size alignment
            for (var i = 0; i < workGroupSize.Length; i++)
            {
                if (globalSize[i] % workGroupSize[i] != 0)
                {
                    result.ValidationWarnings.Add($"Global size dimension {i} ({globalSize[i]}) is not evenly divisible by work group size ({workGroupSize[i]})");
                }
            }

            // 6. Check for suspicious patterns (potential DoS)
            var totalGlobalSize = globalSize.Aggregate(1L, (acc, size) => acc * size);
            if (totalGlobalSize > _configuration.MaxGlobalWorkSize)
            {
                result.ValidationErrors.Add($"Total global work size {totalGlobalSize} exceeds safety limit {_configuration.MaxGlobalWorkSize}");
                result.IsValid = false;
            }

            result.TotalWorkGroupSize = totalWorkGroupSize;
            result.TotalGlobalSize = totalGlobalSize;

            if (result.ValidationErrors.Count == 0)
            {
                result.IsValid = true;
            }

            LogWorkGroupValidationCompleted(_logger, result.IsValid, result.ValidationErrors.Count, result.ValidationWarnings.Count);
        }
        catch (Exception ex)
        {
            LogWorkGroupValidationError(_logger, ex);
            result.IsValid = false;
            result.ValidationErrors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Adds a custom validation rule for specific contexts.
    /// </summary>
    /// <param name="context">Context name for the rule</param>
    /// <param name="rule">Validation rule to add</param>
    public void AddCustomValidationRule(string context, ValidationRule rule)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentNullException.ThrowIfNull(rule);

        _ = _customRules.AddOrUpdate(context, rule, (key, existing) => rule);


        LogCustomRuleAdded(_logger, context, rule.Name);
    }

    /// <summary>
    /// Gets current input validation statistics.
    /// </summary>
    /// <returns>Current validation statistics</returns>
    public ValidationStatistics GetStatistics()
    {
        var result = new ValidationStatistics
        {
            TotalValidations = _statistics.TotalValidations,
            TotalThreatsDetected = _statistics.TotalThreatsDetected,
            TotalSecurityViolations = _statistics.TotalSecurityViolations
        };

        // Copy the dictionaries since they are readonly properties

        foreach (var kvp in _statistics.ValidationsByType)
        {
            result.ValidationsByType[kvp.Key] = kvp.Value;
        }


        foreach (var kvp in _statistics.ThreatsByType)
        {
            result.ThreatsByType[kvp.Key] = kvp.Value;
        }


        return result;
    }

    #region Private Implementation Methods

    private bool ValidateBasicConstraints(string input, string context, SanitizationResult result)
    {
        // Length validation
        if (input.Length > _configuration.MaxInputLength)
        {
            result.SecurityThreats.Add(new InputThreat
            {
                ThreatType = ThreatType.ExcessiveLength,
                Severity = ThreatSeverity.Medium,
                Description = $"Input length {input.Length} exceeds maximum {_configuration.MaxInputLength}",
                Location = "Length check"
            });
            result.IsSecure = false;
            return false;
        }

        // Null byte detection
        if (input.Contains('\0', StringComparison.OrdinalIgnoreCase))
        {
            result.SecurityThreats.Add(new InputThreat
            {
                ThreatType = ThreatType.NullByteInjection,
                Severity = ThreatSeverity.High,
                Description = "Null byte detected in input",
                Location = "Null byte check"
            });
        }

        // Control character detection
        if (ContainsControlCharacters(input))
        {
            result.SecurityThreats.Add(new InputThreat
            {
                ThreatType = ThreatType.ControlCharacters,
                Severity = ThreatSeverity.Low,
                Description = "Control characters detected in input",
                Location = "Control character check"
            });
        }

        return true;
    }

    private static async Task DetectSecurityThreatsAsync(string input, SanitizationResult result, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            foreach (var (patternName, regexFactory) in SecurityPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var regex = regexFactory();
                var matches = regex.Matches(input);
                foreach (Match match in matches)
                {
                    var threatType = GetThreatTypeFromPattern(patternName);
                    var severity = GetSeverityFromPattern(patternName);

                    result.SecurityThreats.Add(new InputThreat
                    {
                        ThreatType = threatType,
                        Severity = severity,
                        Description = $"{patternName} pattern detected: {match.Value}",
                        Location = $"Position {match.Index}"
                    });
                }
            }
        }, cancellationToken);
    }

    private static async Task<string> ApplySanitizationAsync(string input, SanitizationType sanitizationType,

        SanitizationResult result, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            return sanitizationType switch
            {
                SanitizationType.Html => SanitizeHtml(input),
                SanitizationType.Sql => SanitizeSql(input),
                SanitizationType.FilePath => SanitizeFilePath(input),
                SanitizationType.Url => SanitizeUrl(input),
                SanitizationType.Email => SanitizeEmail(input),
                SanitizationType.AlphaNumeric => SanitizeAlphaNumeric(input),
                SanitizationType.Numeric => SanitizeNumeric(input),
                SanitizationType.KernelParameter => SanitizeKernelParameter(input),
                _ => SanitizeGeneral(input)
            };
        }, cancellationToken);
    }

    private static string SanitizeHtml(string input)
    {
        var result = new StringBuilder(input.Length);


        foreach (var c in input)
        {
            if (HtmlEntities.TryGetValue(c, out var entity))
            {
                _ = result.Append(entity);
            }
            else
            {
                _ = result.Append(c);
            }
        }


        return result.ToString();
    }

    private static string SanitizeSql(string input)
    {
        // Escape single quotes and remove/escape dangerous SQL keywords
        return input.Replace("'", "''", StringComparison.Ordinal)
                   .Replace(";", "\\;", StringComparison.Ordinal)
                   .Replace("--", "\\--", StringComparison.Ordinal)
                   .Replace("/*", "\\/*", StringComparison.Ordinal)
                   .Replace("*/", "\\*/", StringComparison.Ordinal);
    }

    private static string SanitizeFilePath(string input)
    {
        // Remove path traversal patterns and dangerous characters
        var sanitized = input.Replace("..", "", StringComparison.Ordinal)
                            .Replace("~", "", StringComparison.Ordinal)
                            .Replace("\\", "/", StringComparison.Ordinal);

        // Remove control characters and null bytes

        var result = new StringBuilder();
        foreach (var c in sanitized)
        {
            if (c is >= (char)32 and not (char)127 and not (char)0) // Printable characters only
            {
                _ = result.Append(c);
            }
        }


        return result.ToString();
    }

    private static string SanitizeUrl(string input)
    {
        try
        {
            var uri = new Uri(input);
            return uri.ToString(); // This validates and normalizes the URL
        }
        catch
        {
            // If URL is invalid, return empty string or sanitized version
            return Uri.EscapeDataString(input);
        }
    }

    private static string SanitizeEmail(string input)
        // Basic email sanitization - remove dangerous characters
        => EmailSanitizeRegex().Replace(input, "");

    private static string SanitizeAlphaNumeric(string input) => AlphaNumericSanitizeRegex().Replace(input, "");

    private static string SanitizeNumeric(string input) => NumericSanitizeRegex().Replace(input, "");

    private static string SanitizeKernelParameter(string input)
    {
        // Remove potentially dangerous characters for kernel parameters
        var dangerous = new char[] { ';', '|', '&', '`', '$', '(', ')', '{', '}', '[', ']', '<', '>', '*', '?', '~' };
        var result = input;


        foreach (var dangerousChar in dangerous)
        {
            result = result.Replace(dangerousChar.ToString(), "", StringComparison.Ordinal);
        }


        return result;
    }

    private static string SanitizeGeneral(string input)
    {
        // General sanitization - remove control characters and null bytes
        var result = new StringBuilder();
        foreach (var c in input)
        {
            if (c is >= (char)32 and not (char)127 and not (char)0) // Printable characters only
            {
                _ = result.Append(c);
            }
        }


        return result.ToString();
    }

    private static async Task ValidatePostSanitizationAsync(string sanitizedInput, SanitizationResult result,

        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Verify that sanitization was effective
            foreach (var (patternName, regexFactory) in SecurityPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var regex = regexFactory();
                if (regex.IsMatch(sanitizedInput))
                {
                    result.SecurityThreats.Add(new InputThreat
                    {
                        ThreatType = ThreatType.SanitizationFailure,
                        Severity = ThreatSeverity.Critical,
                        Description = $"Sanitization failed - {patternName} pattern still present",
                        Location = "Post-sanitization check"
                    });
                }
            }
        }, cancellationToken);
    }

    private async Task<SanitizationResult> ValidateParameterAsync(string parameterName, object parameterValue,

        string context, CancellationToken cancellationToken)
    {
        var stringValue = parameterValue?.ToString() ?? "";
        var sanitizationType = DetermineSanitizationType(parameterName, parameterValue);


        return await SanitizeStringAsync(stringValue, context, sanitizationType, cancellationToken);
    }

    private static SanitizationType DetermineSanitizationType(string parameterName, object? parameterValue)
    {
        return parameterName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            var name when name.Contains("PATH", StringComparison.OrdinalIgnoreCase) || name.Contains("FILE", StringComparison.OrdinalIgnoreCase) => SanitizationType.FilePath,
            var name when name.Contains("URL", StringComparison.OrdinalIgnoreCase) || name.Contains("URI", StringComparison.OrdinalIgnoreCase) => SanitizationType.Url,
            var name when name.Contains("EMAIL", StringComparison.OrdinalIgnoreCase) => SanitizationType.Email,
            var name when name.Contains("SQL", StringComparison.OrdinalIgnoreCase) || name.Contains("QUERY", StringComparison.OrdinalIgnoreCase) => SanitizationType.Sql,
            var name when name.Contains("HTML", StringComparison.OrdinalIgnoreCase) || name.Contains("MARKUP", StringComparison.OrdinalIgnoreCase) => SanitizationType.Html,
            _ when parameterValue is int or long or float or double => SanitizationType.Numeric,
            _ => SanitizationType.General
        };
    }

    private bool DetermineSecurityStatus(SanitizationResult result)
    {
        var criticalThreats = result.SecurityThreats.Count(t => t.Severity == ThreatSeverity.Critical);
        var highThreats = result.SecurityThreats.Count(t => t.Severity == ThreatSeverity.High);


        return criticalThreats == 0 && highThreats <= _configuration.MaxHighSeverityThreats;
    }

    private static bool ContainsControlCharacters(string input) => input.Any(c => char.IsControl(c) && c != '\t' && c != '\r' && c != '\n');

    private static bool ContainsSuspiciousFileName(string fileName)
    {
        var suspiciousNames = new[]
        {
            "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5",

            "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4",

            "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToUpper(CultureInfo.InvariantCulture);
        return suspiciousNames.Select(n => n.ToUpper(CultureInfo.InvariantCulture)).Contains(nameWithoutExt);
    }

    private static ThreatType GetThreatTypeFromPattern(string patternName)
    {
        return patternName switch
        {
            var name when name.Contains("sql", StringComparison.OrdinalIgnoreCase) => ThreatType.SqlInjection,
            var name when name.Contains("xss", StringComparison.OrdinalIgnoreCase) => ThreatType.XssInjection,
            var name when name.Contains("command", StringComparison.OrdinalIgnoreCase) => ThreatType.CommandInjection,
            var name when name.Contains("path", StringComparison.OrdinalIgnoreCase) => ThreatType.PathTraversal,
            var name when name.Contains("ldap", StringComparison.OrdinalIgnoreCase) => ThreatType.LdapInjection,
            var name when name.Contains("code", StringComparison.OrdinalIgnoreCase) => ThreatType.CodeInjection,
            var name when name.Contains("xml", StringComparison.OrdinalIgnoreCase) => ThreatType.XmlInjection,
            var name when name.Contains("nosql", StringComparison.OrdinalIgnoreCase) => ThreatType.NoSqlInjection,
            _ => ThreatType.GeneralMalicious
        };
    }

    private static ThreatSeverity GetSeverityFromPattern(string patternName)
    {
        return patternName switch
        {
            var name when name.Contains("advanced", StringComparison.OrdinalIgnoreCase) || name.Contains("powershell", StringComparison.OrdinalIgnoreCase) => ThreatSeverity.Critical,
            var name when name.Contains("injection", StringComparison.OrdinalIgnoreCase) || name.Contains("traversal", StringComparison.OrdinalIgnoreCase) => ThreatSeverity.High,
            var name when name.Contains("basic", StringComparison.OrdinalIgnoreCase) || name.Contains("html", StringComparison.OrdinalIgnoreCase) => ThreatSeverity.Medium,
            _ => ThreatSeverity.Low
        };
    }

    private void UpdateStatistics(SanitizationResult result)
    {
        _statistics.TotalValidations++;
        _statistics.TotalThreatsDetected += result.SecurityThreats.Count;


        if (!result.IsSecure)
        {
            _statistics.TotalSecurityViolations++;
        }


        _ = _statistics.ValidationsByType.AddOrUpdate(result.SanitizationType, 1, (key, value) => value + 1);


        foreach (var threat in result.SecurityThreats)
        {
            _ = _statistics.ThreatsByType.AddOrUpdate(threat.ThreatType, 1, (key, value) => value + 1);
        }
    }

    private void LogStatistics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var stats = GetStatistics();
            LogStatistics(_logger, stats.TotalValidations, stats.TotalThreatsDetected, stats.TotalSecurityViolations);
        }
        catch (Exception ex)
        {
            LogStatisticsError(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;


        _statisticsTimer?.Dispose();
        _validationLock?.Dispose();


        LogDisposed(_logger, _statistics.TotalValidations, _statistics.TotalThreatsDetected);
    }
}

#region Supporting Types and Enums

/// <summary>
/// Configuration for input sanitization behavior.
/// </summary>
public sealed class InputSanitizationConfiguration
{
    /// <summary>
    /// Gets or sets the default.
    /// </summary>
    /// <value>The default.</value>
    public static InputSanitizationConfiguration Default => new()
    {
        MaxInputLength = 10_000,
        MaxHighSeverityThreats = 0,
        MaxGlobalWorkSize = 1_000_000_000L,
        EnableDetailedLogging = false,
        CacheValidationResults = true
    };
    /// <summary>
    /// Gets or sets the max input length.
    /// </summary>
    /// <value>The max input length.</value>

    public int MaxInputLength { get; init; } = 10_000;
    /// <summary>
    /// Gets or sets the max high severity threats.
    /// </summary>
    /// <value>The max high severity threats.</value>
    public int MaxHighSeverityThreats { get; init; }
    /// <summary>
    /// Gets or sets the max global work size.
    /// </summary>
    /// <value>The max global work size.</value>

    public long MaxGlobalWorkSize { get; init; } = 1_000_000_000L;
    /// <summary>
    /// Gets or sets the enable detailed logging.
    /// </summary>
    /// <value>The enable detailed logging.</value>
    public bool EnableDetailedLogging { get; init; }
    /// <summary>
    /// Gets or sets the cache validation results.
    /// </summary>
    /// <value>The cache validation results.</value>
    public bool CacheValidationResults { get; init; } = true;
    /// <summary>
    /// Gets or sets the validation timeout.
    /// </summary>
    /// <value>The validation timeout.</value>
    public TimeSpan ValidationTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()
        => $"MaxLength={MaxInputLength}, MaxThreats={MaxHighSeverityThreats}, DetailedLogging={EnableDetailedLogging}";
}
/// <summary>
/// An sanitization type enumeration.
/// </summary>

/// <summary>
/// Types of input sanitization.
/// </summary>
public enum SanitizationType
{
    /// <summary>General purpose sanitization.</summary>
    General,
    /// <summary>HTML content sanitization.</summary>
    Html,
    /// <summary>SQL query parameter sanitization.</summary>
    Sql,
    /// <summary>File path sanitization.</summary>
    FilePath,
    /// <summary>URL sanitization.</summary>
    Url,
    /// <summary>Email address sanitization.</summary>
    Email,
    /// <summary>Alphanumeric character sanitization.</summary>
    AlphaNumeric,
    /// <summary>Numeric value sanitization.</summary>
    Numeric,
    /// <summary>Kernel parameter sanitization.</summary>
    KernelParameter
}
/// <summary>
/// An threat type enumeration.
/// </summary>

/// <summary>
/// Types of security threats.
/// </summary>
public enum ThreatType
{
    /// <summary>SQL injection attack threat.</summary>
    SqlInjection,
    /// <summary>Cross-site scripting (XSS) injection threat.</summary>
    XssInjection,
    /// <summary>Command injection attack threat.</summary>
    CommandInjection,
    /// <summary>Path traversal attack threat.</summary>
    PathTraversal,
    /// <summary>LDAP injection attack threat.</summary>
    LdapInjection,
    /// <summary>Code injection attack threat.</summary>
    CodeInjection,
    /// <summary>XML injection attack threat.</summary>
    XmlInjection,
    /// <summary>NoSQL injection attack threat.</summary>
    NoSqlInjection,
    /// <summary>Buffer overflow attack threat.</summary>
    BufferOverflow,
    /// <summary>Null byte injection threat.</summary>
    NullByteInjection,
    /// <summary>Control character injection threat.</summary>
    ControlCharacters,
    /// <summary>Excessive input length threat.</summary>
    ExcessiveLength,
    /// <summary>Invalid file type threat.</summary>
    InvalidFileType,
    /// <summary>Suspicious file name threat.</summary>
    SuspiciousFileName,
    /// <summary>Sanitization process failure threat.</summary>
    SanitizationFailure,
    /// <summary>Processing error threat.</summary>
    ProcessingError,
    /// <summary>General malicious input threat.</summary>
    GeneralMalicious
}
/// <summary>
/// An threat severity enumeration.
/// </summary>

/// <summary>
/// Severity levels for threats.
/// </summary>
public enum ThreatSeverity
{
    /// <summary>Low severity threat.</summary>
    Low = 1,
    /// <summary>Medium severity threat.</summary>
    Medium = 2,
    /// <summary>High severity threat.</summary>
    High = 3,
    /// <summary>Critical severity threat requiring immediate action.</summary>
    Critical = 4
}

/// <summary>
/// Represents an input threat.
/// </summary>
public sealed class InputThreat
{
    /// <summary>
    /// Gets or sets the threat type.
    /// </summary>
    /// <value>The threat type.</value>
    public required ThreatType ThreatType { get; init; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public required ThreatSeverity Severity { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public required string Description { get; init; }
    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    /// <value>The location.</value>
    public required string Location { get; init; }
    /// <summary>
    /// Gets or sets the recommendation.
    /// </summary>
    /// <value>The recommendation.</value>
    public string? Recommendation { get; init; }
}

/// <summary>
/// Result of input sanitization.
/// </summary>
public sealed class SanitizationResult
{
    /// <summary>
    /// Gets or sets the original input.
    /// </summary>
    /// <value>The original input.</value>
    public required string OriginalInput { get; init; }
    /// <summary>
    /// Gets or sets the sanitized input.
    /// </summary>
    /// <value>The sanitized input.</value>
    public string? SanitizedInput { get; set; }
    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>
    public required string Context { get; init; }
    /// <summary>
    /// Gets or sets the sanitization type.
    /// </summary>
    /// <value>The sanitization type.</value>
    public required SanitizationType SanitizationType { get; init; }
    /// <summary>
    /// Gets or sets the processing start time.
    /// </summary>
    /// <value>The processing start time.</value>
    public DateTimeOffset ProcessingStartTime { get; init; }
    /// <summary>
    /// Gets or sets the processing end time.
    /// </summary>
    /// <value>The processing end time.</value>
    public DateTimeOffset ProcessingEndTime { get; set; }
    /// <summary>
    /// Gets or sets the processing duration.
    /// </summary>
    /// <value>The processing duration.</value>
    public TimeSpan ProcessingDuration => ProcessingEndTime - ProcessingStartTime;
    /// <summary>
    /// Gets or sets a value indicating whether secure.
    /// </summary>
    /// <value>The is secure.</value>
    public bool IsSecure { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets the security threats.
    /// </summary>
    /// <value>The security threats.</value>
    public IList<InputThreat> SecurityThreats { get; init; } = [];
}

/// <summary>
/// Result of parameter validation.
/// </summary>
public sealed class ParameterValidationResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; init; }
    /// <summary>
    /// Gets or sets the parameter count.
    /// </summary>
    /// <value>The parameter count.</value>
    public int ParameterCount { get; init; }
    /// <summary>
    /// Gets or sets the validation start time.
    /// </summary>
    /// <value>The validation start time.</value>
    public DateTimeOffset ValidationStartTime { get; init; }
    /// <summary>
    /// Gets or sets the validation end time.
    /// </summary>
    /// <value>The validation end time.</value>
    public DateTimeOffset ValidationEndTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether invalid parameters.
    /// </summary>
    /// <value>The has invalid parameters.</value>
    public bool HasInvalidParameters { get; set; }
    /// <summary>
    /// Gets or sets the invalid parameters.
    /// </summary>
    /// <value>The invalid parameters.</value>
    public IList<string> InvalidParameters { get; init; } = [];
    /// <summary>
    /// Gets or sets the parameter results.
    /// </summary>
    /// <value>The parameter results.</value>
    public Dictionary<string, SanitizationResult> ParameterResults { get; init; } = [];
    /// <summary>
    /// Gets or sets the security threats.
    /// </summary>
    /// <value>The security threats.</value>
    public IList<InputThreat> SecurityThreats { get; init; } = [];
}

/// <summary>
/// Result of file path validation.
/// </summary>
public sealed class PathValidationResult
{
    /// <summary>
    /// Gets or sets the original path.
    /// </summary>
    /// <value>The original path.</value>
    public required string OriginalPath { get; init; }
    /// <summary>
    /// Gets or sets the sanitized path.
    /// </summary>
    /// <value>The sanitized path.</value>
    public string? SanitizedPath { get; set; }
    /// <summary>
    /// Gets or sets the base directory.
    /// </summary>
    /// <value>The base directory.</value>
    public required string BaseDirectory { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; } = true;
    /// <summary>
    /// Gets or sets the security threats.
    /// </summary>
    /// <value>The security threats.</value>
    public IList<InputThreat> SecurityThreats { get; init; } = [];
}

/// <summary>
/// Result of work group validation.
/// </summary>
public sealed class WorkGroupValidationResult
{
    /// <summary>
    /// Gets or sets the work group size.
    /// </summary>
    /// <value>The work group size.</value>
    public required IReadOnlyList<int> WorkGroupSize { get; init; }
    /// <summary>
    /// Gets or sets the global size.
    /// </summary>
    /// <value>The global size.</value>
    public required IReadOnlyList<int> GlobalSize { get; init; }
    /// <summary>
    /// Gets or sets the max work group size.
    /// </summary>
    /// <value>The max work group size.</value>
    public int MaxWorkGroupSize { get; init; }
    /// <summary>
    /// Gets or sets the total work group size.
    /// </summary>
    /// <value>The total work group size.</value>
    public int TotalWorkGroupSize { get; set; }
    /// <summary>
    /// Gets or sets the total global size.
    /// </summary>
    /// <value>The total global size.</value>
    public long TotalGlobalSize { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; } = true;
    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    /// <value>The validation errors.</value>
    public IList<string> ValidationErrors { get; init; } = [];
    /// <summary>
    /// Gets or sets the validation warnings.
    /// </summary>
    /// <value>The validation warnings.</value>
    public IList<string> ValidationWarnings { get; init; } = [];
}

/// <summary>
/// Custom validation rule.
/// </summary>
public sealed class ValidationRule
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the validator.
    /// </summary>
    /// <value>The validator.</value>
    public required Func<string, bool> Validator { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public required string ErrorMessage { get; init; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public ThreatSeverity Severity { get; init; } = ThreatSeverity.Medium;
}

/// <summary>
/// Input validation statistics.
/// </summary>
public sealed class ValidationStatistics
{
    /// <summary>
    /// Gets or sets the total validations.
    /// </summary>
    /// <value>The total validations.</value>
    public long TotalValidations { get; set; }
    /// <summary>
    /// Gets or sets the total threats detected.
    /// </summary>
    /// <value>The total threats detected.</value>
    public long TotalThreatsDetected { get; set; }
    /// <summary>
    /// Gets or sets the total security violations.
    /// </summary>
    /// <value>The total security violations.</value>
    public long TotalSecurityViolations { get; set; }
    /// <summary>
    /// Gets or sets the validations by type.
    /// </summary>
    /// <value>The validations by type.</value>
    public ConcurrentDictionary<SanitizationType, long> ValidationsByType { get; } = new();
    /// <summary>
    /// Gets or sets the threats by type.
    /// </summary>
    /// <value>The threats by type.</value>
    public ConcurrentDictionary<ThreatType, long> ThreatsByType { get; } = new();
}



#endregion
