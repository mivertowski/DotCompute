// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Validation;

/// <summary>
/// Unified validation result that replaces all duplicate validation classes.
/// This is the ONLY validation result implementation in the entire solution.
/// </summary>
public sealed class UnifiedValidationResult
{
    private readonly List<ValidationIssue> _errors = new();
    private readonly List<ValidationIssue> _warnings = new();
    private readonly List<ValidationIssue> _information = new();
    
    /// <summary>
    /// Gets whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => _errors.Count == 0;
    
    /// <summary>
    /// Gets whether the validation has warnings.
    /// </summary>
    public bool HasWarnings => _warnings.Count > 0;
    
    /// <summary>
    /// Gets the first error message if any.
    /// </summary>
    public string? ErrorMessage => _errors.FirstOrDefault()?.Message;
    
    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Errors => _errors;
    
    /// <summary>
    /// Gets all validation warnings.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Warnings => _warnings;
    
    /// <summary>
    /// Gets all informational messages.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Information => _information;
    
    /// <summary>
    /// Gets all issues (errors, warnings, and information).
    /// </summary>
    public IEnumerable<ValidationIssue> AllIssues => 
        _errors.Concat(_warnings).Concat(_information);
    
    /// <summary>
    /// Gets the validation context if any.
    /// </summary>
    public string? Context { get; init; }
    
    /// <summary>
    /// Gets the timestamp of validation.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;


    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string message, string? code = null, string? source = null, object? data = null) => _errors.Add(new ValidationIssue(ValidationSeverity.Error, message, code, source, data));


    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string message, string? code = null, string? source = null, object? data = null) => _warnings.Add(new ValidationIssue(ValidationSeverity.Warning, message, code, source, data));


    /// <summary>
    /// Adds an informational message to the validation result.
    /// </summary>
    public void AddInfo(string message, string? code = null, string? source = null, object? data = null) => _information.Add(new ValidationIssue(ValidationSeverity.Information, message, code, source, data));


    /// <summary>
    /// Merges another validation result into this one.
    /// </summary>
    public void Merge(UnifiedValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        _errors.AddRange(other._errors);
        _warnings.AddRange(other._warnings);
        _information.AddRange(other._information);
    }
    
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static UnifiedValidationResult Success() => new();
    
    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static UnifiedValidationResult Failure(string errorMessage, string? code = null)
    {
        var result = new UnifiedValidationResult();
        result.AddError(errorMessage, code);
        return result;
    }
    
    /// <summary>
    /// Creates a validation result from an exception.
    /// </summary>
    public static UnifiedValidationResult FromException(Exception exception)
    {
        var result = new UnifiedValidationResult();
        result.AddError(exception.Message, exception.GetType().Name, exception.Source, exception);
        return result;
    }
    
    /// <summary>
    /// Throws an exception if validation failed.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ValidationException(this);
        }
    }
    
    /// <summary>
    /// Gets a summary message of all errors.
    /// </summary>
    public string GetErrorSummary() => 
        string.Join("; ", _errors.Select(e => e.Message));
    
    /// <summary>
    /// Gets a full summary of all issues.
    /// </summary>
    public string GetFullSummary()
    {
        var lines = new List<string>();
        
        if (_errors.Count > 0)
        {
            lines.Add($"Errors ({_errors.Count}):");
            lines.AddRange(_errors.Select(e => $"  - {e}"));
        }
        
        if (_warnings.Count > 0)
        {
            lines.Add($"Warnings ({_warnings.Count}):");
            lines.AddRange(_warnings.Select(w => $"  - {w}"));
        }
        
        if (_information.Count > 0)
        {
            lines.Add($"Information ({_information.Count}):");
            lines.AddRange(_information.Select(i => $"  - {i}"));
        }
        
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Represents a single validation issue.
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// Gets the severity of the issue.
    /// </summary>
    public ValidationSeverity Severity { get; }
    
    /// <summary>
    /// Gets the issue message.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Gets the optional error/warning code.
    /// </summary>
    public string? Code { get; }
    
    /// <summary>
    /// Gets the source of the issue (e.g., property name, file path).
    /// </summary>
    public string? Source { get; }
    
    /// <summary>
    /// Gets additional data associated with the issue.
    /// </summary>
    public object? Data { get; }
    
    /// <summary>
    /// Gets the line number if applicable.
    /// </summary>
    public int? LineNumber { get; init; }
    
    /// <summary>
    /// Gets the column number if applicable.
    /// </summary>
    public int? ColumnNumber { get; init; }
    
    public ValidationIssue(
        ValidationSeverity severity,
        string message,
        string? code = null,
        string? source = null,
        object? data = null)
    {
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Code = code;
        Source = source;
        Data = data;
    }
    
    public override string ToString()
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(Code))
        {
            parts.Add($"[{Code}]");
        }


        if (!string.IsNullOrEmpty(Source))
        {
            if (LineNumber.HasValue)
            {
                parts.Add(ColumnNumber.HasValue 
                    ? $"{Source}({LineNumber},{ColumnNumber})" 
                    : $"{Source}({LineNumber})");
            }
            else
            {
                parts.Add(Source);
            }
        }
        
        parts.Add(Message);
        
        return string.Join(": ", parts);
    }
}

/// <summary>
/// Validation severity levels.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Information,
    
    /// <summary>
    /// Warning that doesn't prevent operation.
    /// </summary>
    Warning,
    
    /// <summary>
    /// Error that prevents operation.
    /// </summary>
    Error
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// Gets the validation result that caused this exception.
    /// </summary>
    public UnifiedValidationResult UnifiedValidationResult { get; }
    
    public ValidationException(UnifiedValidationResult validationResult)
        : base(validationResult?.GetErrorSummary() ?? "Validation failed")
    {
        UnifiedValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }
    
    public ValidationException(string message)
        : base(message)
    {
        UnifiedValidationResult = UnifiedValidationResult.Failure(message);
    }
    
    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        UnifiedValidationResult = UnifiedValidationResult.Failure(message);
    }
    public ValidationException()
    {
    }

}