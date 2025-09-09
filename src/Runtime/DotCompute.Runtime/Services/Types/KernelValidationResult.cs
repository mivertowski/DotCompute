// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Types;

/// <summary>
/// Result of kernel argument validation.
/// </summary>
public class KernelValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static KernelValidationResult Success(IEnumerable<string>? warnings = null)
    {
        return new KernelValidationResult
        {
            IsValid = true,
            Warnings = warnings?.ToList() ?? []
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static KernelValidationResult Failure(
        IEnumerable<string> errors,
        IEnumerable<string>? warnings = null)
    {
        return new KernelValidationResult
        {
            IsValid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? []
        };
    }

    /// <summary>
    /// Creates a validation result from individual checks.
    /// </summary>
    public static KernelValidationResult FromChecks(params ValidationCheck[] checks)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var metadata = new Dictionary<string, object>();

        foreach (var check in checks)
        {
            if (!check.Passed && check.IsCritical)
            {
                errors.Add(check.Message);
            }
            else if (!check.Passed)
            {
                warnings.Add(check.Message);
            }


            if (check.Metadata != null)
            {
                foreach (var kvp in check.Metadata)
                {
                    metadata[kvp.Key] = kvp.Value;
                }

            }
        }

        return new KernelValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Combines multiple validation results.
    /// </summary>
    public static KernelValidationResult Combine(params KernelValidationResult[] results)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var metadata = new Dictionary<string, object>();

        foreach (var result in results)
        {
            errors.AddRange(result.Errors);
            warnings.AddRange(result.Warnings);


            foreach (var kvp in result.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }

        }

        return new KernelValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Gets a formatted error message.
    /// </summary>
    public string GetFormattedMessage()
    {
        if (IsValid)
        {

            return Warnings.Count > 0
                ? $"Validation passed with {Warnings.Count} warning(s)"
                : "Validation passed";
        }


        var message = $"Validation failed with {Errors.Count} error(s)";


        if (Warnings.Count > 0)
        {
            message += $" and {Warnings.Count} warning(s)";
        }


        if (Errors.Count > 0)
        {
            message += ":\n" + string.Join("\n", Errors.Select(e => $"  - {e}"));
        }

        return message;
    }
}

/// <summary>
/// Represents a single validation check.
/// </summary>
public class ValidationCheck
{
    /// <summary>
    /// Gets or sets whether the check passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gets or sets the check message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets whether this is a critical error.
    /// </summary>
    public bool IsCritical { get; init; } = true;

    /// <summary>
    /// Gets or sets optional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a passing check.
    /// </summary>
    public static ValidationCheck Pass(string message) => new()
    {
        Passed = true,
        Message = message,
        IsCritical = false
    };

    /// <summary>
    /// Creates a critical error.
    /// </summary>
    public static ValidationCheck Error(string message) => new()
    {
        Passed = false,
        Message = message,
        IsCritical = true
    };

    /// <summary>
    /// Creates a warning.
    /// </summary>
    public static ValidationCheck Warning(string message) => new()
    {
        Passed = false,
        Message = message,
        IsCritical = false
    };
}