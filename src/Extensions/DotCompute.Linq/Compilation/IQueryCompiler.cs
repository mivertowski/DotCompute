// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Operators;

namespace DotCompute.Linq.Compilation;


/// <summary>
/// Defines the interface for compiling LINQ expression trees into compute plans.
/// </summary>
public interface IQueryCompiler
{
    /// <summary>
    /// Compiles an expression tree into an executable compute plan.
    /// </summary>
    /// <param name="context">The compilation context containing the expression and accelerator.</param>
    /// <returns>A compute plan that can be executed on the accelerator.</returns>
    public IComputePlan Compile(CompilationContext context);

    /// <summary>
    /// Validates whether an expression can be compiled for GPU execution.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>A validation result indicating whether compilation is possible.</returns>
    public DotCompute.Abstractions.ValidationResult Validate(Expression expression);
}

/// <summary>
/// Represents the context for compiling a LINQ expression.
/// </summary>
public class CompilationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationContext"/> class.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="expression">The expression to compile.</param>
    public CompilationContext(IAccelerator accelerator, Expression expression)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Parameters = [];
        Options = new CompilationOptions();
    }

    /// <summary>
    /// Gets the target accelerator for compilation.
    /// </summary>
    public IAccelerator Accelerator { get; }

    /// <summary>
    /// Gets the expression tree to compile.
    /// </summary>
    public Expression Expression { get; }

    /// <summary>
    /// Gets the compilation parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; }

    /// <summary>
    /// Gets or sets the compilation options.
    /// </summary>
    public CompilationOptions Options { get; set; }
}

/// <summary>
/// Represents compilation options for query expressions.
/// </summary>
public class CompilationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable operator fusion optimization.
    /// </summary>
    public bool EnableOperatorFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable memory coalescing.
    /// </summary>
    public bool EnableMemoryCoalescing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable parallel execution.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; set; } = 256;

    /// <summary>
    /// Gets or sets a value indicating whether to generate debug information.
    /// </summary>
    public bool GenerateDebugInfo { get; set; }
}

/// <summary>
/// Represents the result of expression validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">Whether the expression is valid for compilation.</param>
    /// <param name="message">An optional validation message.</param>
    /// <param name="errors">A collection of validation errors.</param>
    public ValidationResult(bool isValid, string? message = null, IEnumerable<ValidationError>? errors = null)
    {
        IsValid = isValid;
        Message = message;
        Errors = errors?.ToList() ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the expression is valid for compilation.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }
}

/// <summary>
/// Represents a validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="expression">The expression that caused the error.</param>
    public ValidationError(string code, string message, Expression? expression = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Expression = expression;
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the expression that caused the error.
    /// </summary>
    public Expression? Expression { get; }
}

/// <summary>
/// Represents an executable compute plan generated from a LINQ expression.
/// </summary>
public interface IComputePlan
{
    /// <summary>
    /// Gets the unique identifier for this compute plan.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the stages in the compute plan.
    /// </summary>
    IReadOnlyList<IComputeStage> Stages { get; }

    /// <summary>
    /// Gets the input parameters required by the plan.
    /// </summary>
    IReadOnlyDictionary<string, Type> InputParameters { get; }

    /// <summary>
    /// Gets the output type of the compute plan.
    /// </summary>
    Type OutputType { get; }

    /// <summary>
    /// Gets the estimated memory requirements in bytes.
    /// </summary>
    long EstimatedMemoryUsage { get; }

    /// <summary>
    /// Gets metadata about the compute plan.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Represents a stage in a compute plan.
/// </summary>
public interface IComputeStage
{
    /// <summary>
    /// Gets the stage identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the kernel to execute for this stage.
    /// </summary>
    Operators.IKernel Kernel { get; }

    /// <summary>
    /// Gets the input buffers for this stage.
    /// </summary>
    IReadOnlyList<string> InputBuffers { get; }

    /// <summary>
    /// Gets the output buffer for this stage.
    /// </summary>
    string OutputBuffer { get; }

    /// <summary>
    /// Gets the execution configuration for this stage.
    /// </summary>
    ExecutionConfiguration Configuration { get; }
}

/// <summary>
/// Represents execution configuration for a compute stage.
/// </summary>
public class ExecutionConfiguration
{
    /// <summary>
    /// Gets or sets the grid dimensions.
    /// </summary>
    public (int X, int Y, int Z) GridDimensions { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions.
    /// </summary>
    public (int X, int Y, int Z) BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets additional configuration parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];
}
