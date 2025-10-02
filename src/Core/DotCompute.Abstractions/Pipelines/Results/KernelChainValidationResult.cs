namespace DotCompute.Abstractions.Pipelines.Results;

/// <summary>
/// Represents the result of kernel chain validation operations.
/// Contains validation status, errors, and warnings for pipeline validation.
/// </summary>
public sealed class KernelChainValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the kernel chain validation passed successfully.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the collection of validation errors that prevent successful execution.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = new List<string>();

    /// <summary>
    /// Gets the collection of validation warnings that may affect performance or behavior.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether the validation result contains any errors.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the validation result contains any warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Gets the time taken to perform the validation.
    /// </summary>
    public TimeSpan ValidationTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the number of steps validated.
    /// </summary>
    public int StepsValidated { get; init; }


    /// <summary>
    /// Creates a successful validation result with no errors or warnings.
    /// </summary>
    /// <returns>A validation result indicating success.</returns>
    public static KernelChainValidationResult Success()
        => new() { IsValid = true };

    /// <summary>
    /// Creates a successful validation result with warnings.
    /// </summary>
    /// <param name="warnings">The validation warnings.</param>
    /// <returns>A validation result indicating success with warnings.</returns>
    public static KernelChainValidationResult SuccessWithWarnings(IEnumerable<string> warnings)
        => new()
        {

            IsValid = true,

            Warnings = warnings.ToList()

        };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static KernelChainValidationResult Failure(IEnumerable<string> errors)
        => new()
        {

            IsValid = false,

            Errors = errors.ToList()

        };

    /// <summary>
    /// Creates a failed validation result with errors and warnings.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">The validation warnings.</param>
    /// <returns>A validation result indicating failure with additional warnings.</returns>
    public static KernelChainValidationResult Failure(IEnumerable<string> errors, IEnumerable<string> warnings)
        => new()
        {

            IsValid = false,

            Errors = errors.ToList(),
            Warnings = warnings.ToList()
        };

    /// <summary>
    /// Creates a validation result from a single error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static KernelChainValidationResult FromError(string error)
        => Failure(new[] { error });

    /// <summary>
    /// Creates a validation result from a single warning message.
    /// </summary>
    /// <param name="warning">The warning message.</param>
    /// <returns>A validation result indicating success with a warning.</returns>
    public static KernelChainValidationResult FromWarning(string warning)
        => SuccessWithWarnings(new[] { warning });

    /// <summary>
    /// Combines multiple validation results into a single result.
    /// The combined result is valid only if all input results are valid.
    /// </summary>
    /// <param name="results">The validation results to combine.</param>
    /// <returns>A combined validation result.</returns>
    public static KernelChainValidationResult Combine(params KernelChainValidationResult[] results)
    {
        if (results.Length == 0)
        {

            return Success();
        }


        var allErrors = results.SelectMany(r => r.Errors).ToList();
        var allWarnings = results.SelectMany(r => r.Warnings).ToList();
        var isValid = results.All(r => r.IsValid);

        return new KernelChainValidationResult
        {
            IsValid = isValid,
            Errors = allErrors,
            Warnings = allWarnings
        };
    }

    /// <summary>
    /// Gets a summary string representation of the validation result.
    /// </summary>
    /// <returns>A string summarizing the validation status.</returns>
    public override string ToString()
    {
        if (IsValid && !HasWarnings)
        {

            return "Validation passed successfully";
        }


        if (IsValid && HasWarnings)
        {

            return $"Validation passed with {Warnings.Count} warning(s)";
        }


        return $"Validation failed with {Errors.Count} error(s)" +

               (HasWarnings ? $" and {Warnings.Count} warning(s)" : "");
    }
}