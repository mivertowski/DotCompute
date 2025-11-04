using DotCompute.Linq.CodeGeneration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Optimization;
using Microsoft.Extensions.Logging;

using DotCompute.Abstractions;
namespace DotCompute.Linq.Compilation;

/// <summary>
/// Main compiler for LINQ expressions to compute kernels.
/// Coordinates expression analysis, type inference, and code generation preparation.
/// </summary>
/// <remarks>
/// <para>
/// Phase 3 Implementation: Expression Analysis Foundation.
/// </para>
/// <para>
/// The ExpressionCompiler orchestrates the complete compilation pipeline:
/// </para>
/// <list type="number">
/// <item>Expression tree parsing and operation graph construction (<see cref="ExpressionTreeVisitor"/>)</item>
/// <item>Operation categorization and analysis (<see cref="OperationCategorizer"/>)</item>
/// <item>Type inference and validation (<see cref="TypeInferenceEngine"/>)</item>
/// <item>Dependency analysis and execution ordering (<see cref="DependencyGraph"/>)</item>
/// <item>Parallelization strategy selection (<see cref="ParallelizationStrategy"/>)</item>
/// <item>Backend selection (CPU/GPU) based on workload characteristics</item>
/// </list>
/// <para>
/// Note: Phase 3 performs complete analysis but returns null for CompiledKernel.
/// Actual code generation is implemented in Phase 4 (CPU) and Phase 5 (GPU).
/// </para>
/// </remarks>
public class ExpressionCompiler : IExpressionCompiler
{
    private readonly ExpressionTreeVisitor _visitor;
    private readonly OperationCategorizer _categorizer;
    private readonly TypeInferenceEngine _typeInference;
    private readonly ILogger<ExpressionCompiler>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionCompiler"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ExpressionCompiler(ILogger<ExpressionCompiler>? logger = null)
    {
        _visitor = new ExpressionTreeVisitor();
        _categorizer = new OperationCategorizer();
        _typeInference = new TypeInferenceEngine();
        _logger = logger;
    }

    /// <inheritdoc/>
    public CompilationResult Compile<T, TResult>(Expression<Func<IQueryable<T>, TResult>> query)
    {
        return Compile(query, new CompilationOptions());
    }

    /// <inheritdoc/>
    public CompilationResult Compile<T, TResult>(
        Expression<Func<IQueryable<T>, TResult>> query,
        CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        var warnings = new System.Collections.Generic.List<string>();

        try
        {
            _logger?.LogInformation("Starting compilation of LINQ expression");

            // ==================================================================
            // Stage 1: Parse Expression Tree
            // ==================================================================
            _logger?.LogDebug("Stage 1: Parsing expression tree");
            var operationGraph = ParseExpression(query.Body);
            _logger?.LogDebug("Parsed {Count} operations", operationGraph.Operations.Count);

            // ==================================================================
            // Stage 2: Validate Operation Graph
            // ==================================================================
            _logger?.LogDebug("Stage 2: Validating operation graph");
            ValidateOperationGraph(operationGraph);
            _logger?.LogDebug("Operation graph validated successfully");

            // ==================================================================
            // Stage 3: Infer Types
            // ==================================================================
            _logger?.LogDebug("Stage 3: Inferring types");
            var typeMetadata = InferTypes<T, TResult>(operationGraph);
            _logger?.LogDebug("Inferred types: Input={Input}, Output={Output}",
                typeMetadata.InputType.Name,
                typeMetadata.ResultType.Name);

            // ==================================================================
            // Stage 4: Build Dependencies
            // ==================================================================
            _logger?.LogDebug("Stage 4: Building dependency graph");
            var dependencies = BuildDependencies(operationGraph);
            _logger?.LogDebug("Built dependency graph with {Count} operations", operationGraph.Operations.Count);

            // ==================================================================
            // Stage 5: Select Parallelization Strategy
            // ==================================================================
            _logger?.LogDebug("Stage 5: Selecting parallelization strategy");
            var strategy = SelectStrategy(operationGraph, options);
            _logger?.LogDebug("Selected strategy: {Strategy}", strategy);

            // ==================================================================
            // Stage 6: Select Backend
            // ==================================================================
            _logger?.LogDebug("Stage 6: Selecting compute backend");
            var backend = SelectBackend(operationGraph, strategy, options, warnings);
            _logger?.LogDebug("Selected backend: {Backend}", backend);

            stopwatch.Stop();

            _logger?.LogInformation(
                "Compilation analysis completed in {Time:F2}ms, backend: {Backend}, strategy: {Strategy}",
                stopwatch.Elapsed.TotalMilliseconds,
                backend,
                strategy);

            // ==================================================================
            // Return Phase 3 Result
            // ==================================================================
            // Phase 3: Analysis complete, but no compiled kernel yet
            // CompiledKernel will be null until Phase 4 (CPU code generation)
            return new CompilationResult
            {
                Success = true,
                CompiledKernel = null, // Phase 4+ will implement actual compilation
                SelectedBackend = backend,
                CompilationTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                Warnings = warnings.AsReadOnly(),
                GeneratedCode = options.GenerateDebugInfo
                    ? GenerateDebugInfo(operationGraph, typeMetadata, dependencies, strategy, backend)
                    : null
            };
        }
        catch (NotSupportedException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Compilation failed: unsupported operation");
            return CompilationResult.CreateFailure($"Unsupported operation: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Compilation failed: invalid operation");
            return CompilationResult.CreateFailure($"Invalid operation: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Compilation failed with unexpected error");
            return CompilationResult.CreateFailure($"Compilation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the expression tree and builds an operation graph.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <returns>An operation graph representing the query.</returns>
    private OperationGraph ParseExpression(Expression expression)
    {
        return _visitor.Visit(expression);
    }

    /// <summary>
    /// Validates the operation graph for correctness and supportability.
    /// </summary>
    /// <param name="graph">The operation graph to validate.</param>
    /// <exception cref="NotSupportedException">Thrown if the graph contains unsupported operations.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the graph is invalid.</exception>
    private void ValidateOperationGraph(OperationGraph graph)
    {
        if (graph.Root == null)
        {
            throw new InvalidOperationException("Operation graph has no root operation");
        }

        if (graph.Operations.Count == 0)
        {
            throw new InvalidOperationException("Operation graph contains no operations");
        }

        // Use OperationCategorizer to build dependency graph and check for cycles
        var depGraph = _categorizer.BuildDependencies(graph);

        // The DependencyGraph's TopologicalSort() will throw if there are cycles
        try
        {
            var sorted = depGraph.BaseGraph.TopologicalSort();
            _logger?.LogDebug("Topological sort produced {Count} operations", sorted.Count);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Circular dependency detected in operation graph", ex);
        }

        // Validate data flow (side effects, parallelizability)
        var dataFlow = _categorizer.AnalyzeDataFlow(graph);
        if (dataFlow.HasSideEffects)
        {
            _logger?.LogWarning("Operation graph contains side effects");
        }
    }

    /// <summary>
    /// Infers types for all operations in the graph.
    /// </summary>
    /// <param name="graph">The operation graph.</param>
    /// <returns>Type metadata for the query.</returns>
    private TypeMetadata InferTypes<T, TResult>(OperationGraph graph)
    {
        var metadata = _typeInference.InferTypes(graph, typeof(T), typeof(TResult));

        // Validate type compatibility
        if (!_typeInference.ValidateTypes(graph, out var errors))
        {
            var errorMessage = string.Join("; ", errors);
            throw new InvalidOperationException($"Type validation failed: {errorMessage}");
        }

        return metadata;
    }

    /// <summary>
    /// Builds a dependency graph for the operations.
    /// </summary>
    /// <param name="graph">The operation graph.</param>
    /// <returns>A dependency graph with topologically sorted operations.</returns>
    private DependencyGraph BuildDependencies(OperationGraph graph)
    {
        var extendedDepGraph = _categorizer.BuildDependencies(graph);

        // Return the base dependency graph from the extended analysis
        return extendedDepGraph.BaseGraph;
    }

    /// <summary>
    /// Selects the parallelization strategy based on the operation graph.
    /// </summary>
    /// <param name="graph">The operation graph.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>The selected parallelization strategy.</returns>
    private ParallelizationStrategy SelectStrategy(OperationGraph graph, CompilationOptions options)
    {
        return _categorizer.GetStrategy(graph);
    }

    /// <summary>
    /// Selects the compute backend based on the operation graph and strategy.
    /// </summary>
    /// <param name="graph">The operation graph.</param>
    /// <param name="strategy">The parallelization strategy.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="warnings">Collection to add warnings to.</param>
    /// <returns>The selected compute backend.</returns>
    private ComputeBackend SelectBackend(
        OperationGraph graph,
        ParallelizationStrategy strategy,
        CompilationOptions options,
        System.Collections.Generic.List<string> warnings)
    {
        // NOTE: CompilationOptions doesn't have TargetBackend property
        // Auto-select backend based on strategy
        var backend = strategy switch
        {
            ParallelizationStrategy.Sequential => ComputeBackend.CpuSimd,
            ParallelizationStrategy.DataParallel => ComputeBackend.CpuSimd, // SIMD on CPU
            ParallelizationStrategy.TaskParallel => ComputeBackend.CpuSimd, // Multi-threaded CPU
            ParallelizationStrategy.GpuParallel => SelectGpuBackend(warnings),
            ParallelizationStrategy.Hybrid => SelectGpuBackend(warnings),
            _ => ComputeBackend.CpuSimd
        };

        return backend;
    }

    /// <summary>
    /// Validates that the selected backend is compatible with the parallelization strategy.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    private void ValidateBackendForStrategy(
        ComputeBackend backend,
        ParallelizationStrategy strategy,
        System.Collections.Generic.List<string> warnings)
    {
        if (strategy.RequiresGpu() && backend == ComputeBackend.CpuSimd)
        {
            warnings.Add($"Strategy {strategy} recommends GPU, but CPU backend was explicitly requested");
        }
    }

    /// <summary>
    /// Selects an available GPU backend.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    private ComputeBackend SelectGpuBackend(System.Collections.Generic.List<string> warnings)
    {
        // Phase 3: We don't have runtime GPU detection yet
        // Default to CUDA, but add warning that GPU codegen is not implemented
        warnings.Add("GPU backend selected, but GPU code generation is not implemented in Phase 3. Falling back to CPU.");
        return ComputeBackend.CpuSimd;
    }

    /// <summary>
    /// Generates debug information for the compilation result.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    private string GenerateDebugInfo(
        OperationGraph graph,
        TypeMetadata typeMetadata,
        DependencyGraph dependencies,
        ParallelizationStrategy strategy,
        ComputeBackend backend)
    {
        var lines = new System.Collections.Generic.List<string>
        {
            "=== DotCompute LINQ Compilation Analysis (Phase 3) ===",
            "",
            $"Compilation Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            $"Operations: {graph.Operations.Count}",
            $"Root Operation: {graph.Root?.Id ?? "None"}",
            $"Parallelization Strategy: {strategy}",
            $"Selected Backend: {backend}",
            "",
            "=== Operation Graph ===",
        };

        foreach (var operation in graph.Operations)
        {
            lines.Add($"  [{operation.Id}] {operation.Type}");
            if (operation.Dependencies.Count > 0)
            {
                lines.Add($"    Dependencies: {string.Join(", ", operation.Dependencies)}");
            }
            lines.Add($"    Estimated Cost: {operation.EstimatedCost:F2}");

            if (operation.Metadata.TryGetValue("MethodName", out var methodName))
            {
                lines.Add($"    LINQ Method: {methodName}");
            }
        }

        lines.Add("");
        lines.Add("=== Type Information ===");
        lines.Add($"  Input Type: {typeMetadata.InputType.Name}");
        lines.Add($"  Result Type: {typeMetadata.ResultType.Name}");
        lines.Add($"  Intermediate Types: {typeMetadata.IntermediateTypes.Count}");
        lines.Add($"  SIMD Compatible: {typeMetadata.IsSimdCompatible}");
        lines.Add($"  Requires Unsafe: {typeMetadata.RequiresUnsafe}");
        lines.Add($"  Has Nullable Types: {typeMetadata.HasNullableTypes}");

        return string.Join(Environment.NewLine, lines);
    }
#pragma warning restore CA1822
}
