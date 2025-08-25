// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions;

/// <summary>
/// Unified kernel compiler interface that replaces all duplicate compiler interfaces.
/// This is the ONLY kernel compiler interface in the entire solution.
/// </summary>
/// <typeparam name="TSource">The type of source input (e.g., KernelDefinition, Expression, string).</typeparam>
/// <typeparam name="TResult">The type of compiled result (e.g., ICompiledKernel, CompiledQuery).</typeparam>
public interface IUnifiedKernelCompiler<TSource, TResult> where TResult : ICompiledKernel
{
    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the supported source types for this compiler.
    /// </summary>
    IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; }
    
    /// <summary>
    /// Gets the compiler capabilities and features.
    /// </summary>
    IReadOnlyDictionary<string, object> Capabilities { get; }
    
    /// <summary>
    /// Compiles a kernel from the specified source.
    /// </summary>
    /// <param name="source">The kernel source to compile.</param>
    /// <param name="options">Compilation options to control the compilation process.</param>
    /// <param name="cancellationToken">Token to cancel the compilation operation.</param>
    /// <returns>A task that represents the asynchronous compilation operation.</returns>
    ValueTask<TResult> CompileAsync(
        TSource source,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a kernel source without compiling.
    /// </summary>
    /// <param name="source">The kernel source to validate.</param>
    /// <returns>A validation result indicating whether compilation is possible.</returns>
    ValidationResult Validate(TSource source);
    
    /// <summary>
    /// Asynchronously validates a kernel source with detailed analysis.
    /// </summary>
    /// <param name="source">The kernel source to validate.</param>
    /// <param name="cancellationToken">Token to cancel the validation operation.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    ValueTask<ValidationResult> ValidateAsync(
        TSource source,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Optimizes a compiled kernel for better performance.
    /// </summary>
    /// <param name="kernel">The compiled kernel to optimize.</param>
    /// <param name="level">The optimization level to apply.</param>
    /// <param name="cancellationToken">Token to cancel the optimization operation.</param>
    /// <returns>A task that represents the asynchronous optimization operation.</returns>
    ValueTask<TResult> OptimizeAsync(
        TResult kernel,
        OptimizationLevel level,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic unified kernel compiler interface for simple scenarios.
/// </summary>
public interface IUnifiedKernelCompiler : IUnifiedKernelCompiler<KernelDefinition, ICompiledKernel>
{
}

/// <summary>
/// Enhanced validation result that combines all validation features.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationIssue> _issues = new();
    
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid => !_issues.Any(i => i.Severity == IssueSeverity.Error);
    
    /// <summary>
    /// Gets all validation issues.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    
    /// <summary>
    /// Gets error messages.
    /// </summary>
    public IEnumerable<string> Errors => _issues
        .Where(i => i.Severity == IssueSeverity.Error)
        .Select(i => i.Message);
    
    /// <summary>
    /// Gets warning messages.
    /// </summary>
    public IEnumerable<string> Warnings => _issues
        .Where(i => i.Severity == IssueSeverity.Warning)
        .Select(i => i.Message);
    
    /// <summary>
    /// Gets informational messages.
    /// </summary>
    public IEnumerable<string> Information => _issues
        .Where(i => i.Severity == IssueSeverity.Information)
        .Select(i => i.Message);
    
    /// <summary>
    /// Adds a validation issue.
    /// </summary>
    public void AddIssue(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        _issues.Add(issue);
    }
    
    /// <summary>
    /// Adds an error.
    /// </summary>
    public void AddError(string message, string? code = null, SourceLocation? location = null)
    {
        _issues.Add(new ValidationIssue(IssueSeverity.Error, message, code, location));
    }
    
    /// <summary>
    /// Adds a warning.
    /// </summary>
    public void AddWarning(string message, string? code = null, SourceLocation? location = null)
    {
        _issues.Add(new ValidationIssue(IssueSeverity.Warning, message, code, location));
    }
    
    /// <summary>
    /// Adds an informational message.
    /// </summary>
    public void AddInfo(string message, string? code = null, SourceLocation? location = null)
    {
        _issues.Add(new ValidationIssue(IssueSeverity.Information, message, code, location));
    }
    
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();
    
    /// <summary>
    /// Creates a failed validation result with an error.
    /// </summary>
    public static ValidationResult Failure(string errorMessage)
    {
        var result = new ValidationResult();
        result.AddError(errorMessage);
        return result;
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
    public IssueSeverity Severity { get; }
    
    /// <summary>
    /// Gets the issue message.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Gets the optional error code.
    /// </summary>
    public string? Code { get; }
    
    /// <summary>
    /// Gets the optional source location.
    /// </summary>
    public SourceLocation? Location { get; }
    
    public ValidationIssue(IssueSeverity severity, string message, string? code = null, SourceLocation? location = null)
    {
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Code = code;
        Location = location;
    }
}

/// <summary>
/// Represents a location in source code.
/// </summary>
public readonly struct SourceLocation
{
    /// <summary>
    /// Gets the line number (1-based).
    /// </summary>
    public int Line { get; }
    
    /// <summary>
    /// Gets the column number (1-based).
    /// </summary>
    public int Column { get; }
    
    /// <summary>
    /// Gets the optional file name.
    /// </summary>
    public string? FileName { get; }
    
    public SourceLocation(int line, int column, string? fileName = null)
    {
        Line = line;
        Column = column;
        FileName = fileName;
    }
    
    public override string ToString() => FileName != null 
        ? $"{FileName}({Line},{Column})" 
        : $"({Line},{Column})";
}

/// <summary>
/// Issue severity levels.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Information,
    
    /// <summary>
    /// Warning that doesn't prevent compilation.
    /// </summary>
    Warning,
    
    /// <summary>
    /// Error that prevents compilation.
    /// </summary>
    Error
}