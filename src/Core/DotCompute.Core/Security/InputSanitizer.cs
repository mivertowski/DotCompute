// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
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
    // LoggerMessage delegates - Event ID range 18200-18299 for InputSanitizer (Security module)
    private static readonly Action<ILogger, Exception?> _logStatisticsError =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(18200, nameof(LogStatisticsError)),
            "Error logging statistics");

    // Wrapper method
    private static void LogStatisticsError(ILogger logger, Exception ex)
        => _logStatisticsError(logger, ex);

    private readonly ILogger _logger;
    private readonly InputSanitizationConfiguration _configuration;
    private readonly ConcurrentDictionary<string, ValidationRule> _customRules = new();
    private readonly SemaphoreSlim _validationLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly Timer _statisticsTimer;
    private volatile bool _disposed;

    // Pre-compiled regex patterns for common attacks
    private static readonly Dictionary<string, Regex> SecurityPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        // SQL Injection patterns
        ["sql_injection_basic"] = new Regex(@"('|(--|;)|(\||(\*|%))|(\b(select|insert|update|delete|drop|create|alter|exec|execute|union|script)\b))",

            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["sql_injection_advanced"] = new Regex(@"(\b(sleep|benchmark|pg_sleep|waitfor|delay)\b)|(\b(xp_cmdshell|sp_executesql)\b)|(\b(information_schema|sys\.|master\.)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // XSS patterns

        ["xss_script"] = new Regex(@"<script[^>]*>.*?</script>|javascript:|vbscript:|on\w+\s*=",

            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline),
        ["xss_html"] = new Regex(@"<\s*(iframe|object|embed|applet|meta|link|style|img|svg)[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Command injection patterns

        ["command_injection"] = new Regex(@"[;&|`$(){}[\]<>*?~]|(\\x[0-9a-f]{2})|(%[0-9a-f]{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["powershell_injection"] = new Regex(@"(invoke-expression|iex|invoke-command|icm|start-process|saps|new-object|downloadstring)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Path traversal patterns

        ["path_traversal"] = new Regex(@"(\.\.[\\/])+|(%2e%2e[\\/])+|(\.\.%2f)|(\.\.%5c)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // LDAP injection patterns

        ["ldap_injection"] = new Regex(@"[()&|!>=<~*/]|(\\\*)|(\\\()|(\\\))",
            RegexOptions.Compiled),

        // Code injection patterns

        ["code_injection"] = new Regex(@"(eval|exec|system|shell_exec|passthru|popen|proc_open|file_get_contents|readfile|include|require)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // XML injection patterns

        ["xml_injection"] = new Regex(@"(<!\[CDATA\[)|(<\?xml)|(<!\-\-)|(\]\]>)|(&lt;)|(&gt;)|(&amp;)|(&quot;)|(&apos;)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // NoSQL injection patterns

        ["nosql_injection"] = new Regex(@"(\$where|\$regex|\$ne|\$gt|\$lt|\$or|\$and|\$in|\$nin)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)
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
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? InputSanitizationConfiguration.Default;

        // Initialize statistics collection

        _statisticsTimer = new Timer(LogStatistics, null,

            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));


        _logger.LogInfoMessage($"InputSanitizer initialized with configuration: {_configuration.ToString()}");
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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(InputSanitizer));
        }


        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        await _validationLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogTrace("Sanitizing input: Context={Context}, Type={Type}, Length={Length}",

                context, sanitizationType, input.Length);

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

            _logger.LogTrace("Input sanitization completed: Context={Context}, Secure={IsSecure}, Threats={ThreatCount}",
                context, result.IsSecure, result.SecurityThreats.Count);

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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(InputSanitizer));
        }


        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);

        await _validationLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebugMessage($"Validating kernel parameters: Kernel={kernelName}, ParameterCount={parameters.Count}");

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

            _logger.LogDebugMessage($"Kernel parameter validation completed: Kernel={kernelName}, Valid={result.IsValid}, Invalid={result.InvalidParameters.Count}");

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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(InputSanitizer));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        _logger.LogTrace("Validating file path: Path={Path}, BaseDirectory={BaseDirectory}", filePath, baseDirectory);

        var result = new PathValidationResult
        {
            OriginalPath = filePath,
            BaseDirectory = baseDirectory
        };

        try
        {
            // 1. Check for path traversal patterns
            if (SecurityPatterns["path_traversal"].IsMatch(filePath))
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
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                var allowedList = allowedExtensions.Select(ext => ext.ToLowerInvariant()).ToList();


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

            _logger.LogTrace("File path validation completed: Valid={IsValid}, Threats={ThreatCount}",
                result.IsValid, result.SecurityThreats.Count);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error validating file path: {filePath}");
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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(InputSanitizer));
        }


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

            _logger.LogTrace("Work group validation completed: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
                result.IsValid, result.ValidationErrors.Count, result.ValidationWarnings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error validating work group sizes");
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
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(InputSanitizer));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentNullException.ThrowIfNull(rule);

        _ = _customRules.AddOrUpdate(context, rule, (key, existing) => rule);


        _logger.LogDebugMessage($"Added custom validation rule: Context={context}, Rule={rule.Name}");
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
            foreach (var (patternName, regex) in SecurityPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
        return input.Replace("'", "''")
                   .Replace(";", "\\;")
                   .Replace("--", "\\--")
                   .Replace("/*", "\\/*")
                   .Replace("*/", "\\*/");
    }

    private static string SanitizeFilePath(string input)
    {
        // Remove path traversal patterns and dangerous characters
        var sanitized = input.Replace("..", "")
                            .Replace("~", "")
                            .Replace("\\", "/");

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




        => Regex.Replace(input, @"[^\w@.-]", "", RegexOptions.Compiled);

    private static string SanitizeAlphaNumeric(string input) => Regex.Replace(input, @"[^a-zA-Z0-9]", "", RegexOptions.Compiled);

    private static string SanitizeNumeric(string input) => Regex.Replace(input, @"[^0-9.-]", "", RegexOptions.Compiled);

    private static string SanitizeKernelParameter(string input)
    {
        // Remove potentially dangerous characters for kernel parameters
        var dangerous = new char[] { ';', '|', '&', '`', '$', '(', ')', '{', '}', '[', ']', '<', '>', '*', '?', '~' };
        var result = input;


        foreach (var dangerousChar in dangerous)
        {
            result = result.Replace(dangerousChar.ToString(), "");
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
            foreach (var (patternName, regex) in SecurityPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
        return parameterName.ToLowerInvariant() switch
        {
            var name when name.Contains("path", StringComparison.OrdinalIgnoreCase) || name.Contains("file", StringComparison.CurrentCulture) => SanitizationType.FilePath,
            var name when name.Contains("url", StringComparison.CurrentCulture) || name.Contains("uri", StringComparison.CurrentCulture) => SanitizationType.Url,
            var name when name.Contains("email", StringComparison.CurrentCulture) => SanitizationType.Email,
            var name when name.Contains("sql", StringComparison.CurrentCulture) || name.Contains("query", StringComparison.CurrentCulture) => SanitizationType.Sql,
            var name when name.Contains("html", StringComparison.CurrentCulture) || name.Contains("markup", StringComparison.CurrentCulture) => SanitizationType.Html,
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

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        return suspiciousNames.Contains(nameWithoutExt);
    }

    private static ThreatType GetThreatTypeFromPattern(string patternName)
    {
        return patternName switch
        {
            var name when name.Contains("sql", StringComparison.CurrentCulture) => ThreatType.SqlInjection,
            var name when name.Contains("xss", StringComparison.CurrentCulture) => ThreatType.XssInjection,
            var name when name.Contains("command", StringComparison.CurrentCulture) => ThreatType.CommandInjection,
            var name when name.Contains("path", StringComparison.CurrentCulture) => ThreatType.PathTraversal,
            var name when name.Contains("ldap", StringComparison.CurrentCulture) => ThreatType.LdapInjection,
            var name when name.Contains("code") => ThreatType.CodeInjection,
            var name when name.Contains("xml", StringComparison.CurrentCulture) => ThreatType.XmlInjection,
            var name when name.Contains("nosql", StringComparison.CurrentCulture) => ThreatType.NoSqlInjection,
            _ => ThreatType.GeneralMalicious
        };
    }

    private static ThreatSeverity GetSeverityFromPattern(string patternName)
    {
        return patternName switch
        {
            var name when name.Contains("advanced", StringComparison.CurrentCulture) || name.Contains("powershell", StringComparison.CurrentCulture) => ThreatSeverity.Critical,
            var name when name.Contains("injection", StringComparison.CurrentCulture) || name.Contains("traversal", StringComparison.CurrentCulture) => ThreatSeverity.High,
            var name when name.Contains("basic", StringComparison.CurrentCulture) || name.Contains("html", StringComparison.CurrentCulture) => ThreatSeverity.Medium,
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
            _logger.LogInfoMessage($"Input validation statistics: Validations={stats.TotalValidations}, Threats={stats.TotalThreatsDetected}, Violations={stats.TotalSecurityViolations}");
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


        _logger.LogInfoMessage($"InputSanitizer disposed. Final statistics: Validations={_statistics.TotalValidations}, Threats={_statistics.TotalThreatsDetected}");
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
    General,
    Html,
    Sql,
    FilePath,
    Url,
    Email,
    AlphaNumeric,
    Numeric,
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
    SqlInjection,
    XssInjection,
    CommandInjection,
    PathTraversal,
    LdapInjection,
    CodeInjection,
    XmlInjection,
    NoSqlInjection,
    BufferOverflow,
    NullByteInjection,
    ControlCharacters,
    ExcessiveLength,
    InvalidFileType,
    SuspiciousFileName,
    SanitizationFailure,
    ProcessingError,
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
    Low = 1,
    Medium = 2,
    High = 3,
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
    public required int[] WorkGroupSize { get; init; }
    /// <summary>
    /// Gets or sets the global size.
    /// </summary>
    /// <value>The global size.</value>
    public required int[] GlobalSize { get; init; }
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
