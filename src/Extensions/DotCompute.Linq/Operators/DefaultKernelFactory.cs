// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Types;
using DotCompute.Linq.Operators.Kernels;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParameterDirection = DotCompute.Linq.Operators.Parameters.ParameterDirection;

namespace DotCompute.Linq.Operators;


/// <summary>
/// Default implementation of kernel factory for LINQ operations with dynamic kernel generation.
/// </summary>
public class DefaultKernelFactory : IKernelFactory
{
    private readonly Dictionary<AcceleratorType, IKernelGenerator> _generators;
    private readonly ILogger<DefaultKernelFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultKernelFactory"/> class.
    /// </summary>
    public DefaultKernelFactory(ILogger<DefaultKernelFactory>? logger = null)
    {
        _logger = logger ?? NullLogger<DefaultKernelFactory>.Instance;
        _generators = [];

        // Register built-in generators
        RegisterBuiltInGenerators();
    }

    /// <summary>
    /// Creates a kernel for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="definition">The kernel definition.</param>
    /// <returns>A compiled kernel.</returns>
    public IKernel CreateKernel(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        // Try to find a matching generator
        if (_generators.TryGetValue(accelerator.Type, out var generator))
        {
            return CreateDynamicKernel(accelerator, definition, generator);
        }

        // Fallback to placeholder for unsupported accelerators
        _logger.LogWarningMessage($"No kernel generator found for accelerator type {accelerator.Type}, using placeholder");
        return new PlaceholderKernel(definition.Name, definition);
    }

    /// <summary>
    /// Creates a kernel from an expression tree.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="expression">The expression to compile.</param>
    /// <param name="context">The generation context.</param>
    /// <returns>A compiled kernel.</returns>
    public Operators.Interfaces.IKernel CreateKernelFromExpression(IAccelerator accelerator, Expression expression, KernelGenerationContext? context = null)
    {
        context ??= CreateDefaultContext(accelerator);

        if (_generators.TryGetValue(accelerator.Type, out var generator))
        {
            try
            {
                // Check if expression can be compiled
                if (!generator.CanCompile(expression))
                {
                    _logger.LogWarningMessage($"Expression cannot be compiled for {accelerator.Type}, using fallback");
                    return new ExpressionFallbackKernel(expression, _logger);
                }

                // Generate the kernel
                var generatedKernel = generator.GenerateKernel(expression, context);
                return new DynamicCompiledKernel(generatedKernel, accelerator, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to generate kernel from expression for {accelerator.Type}");
                return new ExpressionFallbackKernel(expression, _logger);
            }
        }

        // No generator available
        _logger.LogWarningMessage("No kernel generator available for {accelerator.Type}");
        return new ExpressionFallbackKernel(expression, _logger);
    }

    private IKernel CreateDynamicKernel(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition, IKernelGenerator generator)
    {
        try
        {
            // Check for fusion metadata in definition
            var fusionMetadata = ExtractFusionMetadata(definition);

            if (fusionMetadata != null)
            {
                // Handle fused operations
                return CreateFusedKernel(accelerator, definition, generator, fusionMetadata);
            }

            // Handle regular operation kernel
            var operationType = definition.Metadata.GetValueOrDefault("OperationType")?.ToString();
            if (!string.IsNullOrEmpty(operationType))
            {
                var inputTypes = ExtractInputTypes(definition);
                var outputType = ExtractOutputType(definition);
                var context = CreateGenerationContext(accelerator, definition);

                var generatedKernel = generator.GenerateOperationKernel(operationType, inputTypes, outputType, context);
                var linqKernel = new DynamicCompiledKernel(generatedKernel, accelerator, _logger);
                return new Adapters.CoreKernelAdapter(linqKernel);
            }

            // Fallback to placeholder
            return new PlaceholderKernel(definition.Name, definition);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create dynamic kernel {definition.Name}");
            return new PlaceholderKernel(definition.Name, definition);
        }
    }

    private IKernel CreateFusedKernel(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition,
        IKernelGenerator generator, Dictionary<string, object> fusionMetadata)
    {
        var fusionType = fusionMetadata.GetValueOrDefault("FusionType")?.ToString() ?? "Generic";
        var operations = fusionMetadata.GetValueOrDefault("FusedOperations") as string[] ?? [];

        _logger.LogDebugMessage($"Creating fused kernel for operations: {string.Join(", ", operations)}");

        // Create a custom operation type for the fused kernel
        var fusedOperationType = $"Fused_{string.Join("_", operations)}";

        var inputTypes = ExtractInputTypes(definition);
        var outputType = ExtractOutputType(definition);
        var context = CreateGenerationContext(accelerator, definition);

        // Add fusion-specific metadata to context
        context.Metadata ??= [];
        context.Metadata["FusionType"] = fusionType;
        context.Metadata["FusedOperations"] = operations;
        context.Metadata["EstimatedSpeedup"] = fusionMetadata.GetValueOrDefault("EstimatedSpeedup", 1.2);

        var generatedKernel = generator.GenerateOperationKernel(fusedOperationType, inputTypes, outputType, context);
        var linqKernel = new DynamicCompiledKernel(generatedKernel, accelerator, _logger);
        return new Adapters.CoreKernelAdapter(linqKernel);
    }

    /// <summary>
    /// Validates whether a kernel definition is supported on the accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>True if the kernel is supported; otherwise, false.</returns>
    public bool IsKernelSupported(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        if (_generators.TryGetValue(accelerator.Type, out var generator))
        {
            // Check if the operation is supported
            var operationType = definition.Metadata.GetValueOrDefault("OperationType")?.ToString();
            if (!string.IsNullOrEmpty(operationType))
            {
                return IsOperationSupported(generator, operationType);
            }
        }

        // Default to true for basic compatibility
        return true;
    }

    /// <summary>
    /// Gets the supported kernel languages for an accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to query.</param>
    /// <returns>The supported kernel languages.</returns>
    public IReadOnlyList<KernelLanguage> GetSupportedLanguages(IAccelerator accelerator)
    {

        if (_generators.TryGetValue(accelerator.Type, out _))
        {
            return new[] { GetKernelLanguageForAccelerator(accelerator.Type) };
        }

        // Return basic language support as fallback
        return new[] { KernelLanguage.CSharp };
    }

    private void RegisterBuiltInGenerators()
        // Built-in generators are now registered through the backend plugin system
        // This method is kept for backwards compatibility but generators are loaded
        // dynamically through the backend factory pattern when accelerators are created




        => _logger.LogDebugMessage("Built-in generators will be registered through backend plugins");

    private static Dictionary<string, object>? ExtractFusionMetadata(DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        if (definition.Metadata.TryGetValue("FusionMetadata", out var metadata) && metadata is Dictionary<string, object> fusionData)
        {
            return fusionData;
        }
        return null;
    }

    private static Type[] ExtractInputTypes(DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        if (definition.Metadata.TryGetValue("Parameters", out var paramsObj) && paramsObj is Parameters.KernelParameter[] parameters)
        {
            return [.. parameters
                .Where(p => p.Direction is ParameterDirection.In or ParameterDirection.InOut)
                .Select(p => p.Type)];
        }
        return [];
    }

    private static Type ExtractOutputType(DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        if (definition.Metadata.TryGetValue("Parameters", out var paramsObj) && paramsObj is Parameters.KernelParameter[] parameters)
        {
            var outputParam = parameters
                .FirstOrDefault(p => p.Direction is ParameterDirection.Out or ParameterDirection.InOut);
            return outputParam?.Type ?? typeof(object);
        }
        return typeof(object);
    }

    private static KernelGenerationContext CreateGenerationContext(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        return new KernelGenerationContext
        {
            DeviceInfo = accelerator.Info,
            UseSharedMemory = definition.Metadata.GetValueOrDefault("UseSharedMemory", true) is bool useShared && useShared,
            UseVectorTypes = definition.Metadata.GetValueOrDefault("UseVectorTypes", true) is bool useVector && useVector,
            Precision = definition.Metadata.GetValueOrDefault("Precision") is PrecisionMode precision ? precision.ToString() : PrecisionMode.Single.ToString(),
            Metadata = new Dictionary<string, object>(definition.Metadata)
        };
    }

    private static KernelGenerationContext CreateDefaultContext(IAccelerator accelerator)
    {
        return new KernelGenerationContext
        {
            DeviceInfo = accelerator.Info,
            UseSharedMemory = true,
            UseVectorTypes = true,
            Precision = PrecisionMode.Single.ToString()
        };
    }

    private static bool IsOperationSupported(IKernelGenerator generator, string operationType)
    {
        // Try to generate a test kernel to check support
        try
        {
            var testInputTypes = new[] { typeof(float[]) };
            var testOutputType = typeof(float[]);
            var testContext = new KernelGenerationContext
            {
                DeviceInfo = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024 * 1024 * 1024),
                UseSharedMemory = false,
                UseVectorTypes = false
            };

            var testKernel = generator.GenerateOperationKernel(operationType, testInputTypes, testOutputType, testContext);
            return testKernel != null;
        }
        catch
        {
            return false;
        }
    }

    private static KernelLanguage GetKernelLanguageForAccelerator(AcceleratorType acceleratorType)
    {
        return acceleratorType switch
        {
            AcceleratorType.CUDA => KernelLanguage.CUDA,
            AcceleratorType.OpenCL => KernelLanguage.OpenCL,
            AcceleratorType.Metal => KernelLanguage.Metal,
            AcceleratorType.CPU => KernelLanguage.OpenCL,
            AcceleratorType.GPU => KernelLanguage.OpenCL,
            _ => KernelLanguage.OpenCL
        };
    }
}

/// <summary>
/// Placeholder kernel implementation for development purposes.
/// </summary>
internal class PlaceholderKernel : IKernel
{
    private bool _disposed;
    private readonly DotCompute.Abstractions.Kernels.KernelDefinition _definition;

    public PlaceholderKernel(string name, DotCompute.Abstractions.Kernels.KernelDefinition? definition = null)
    {
        Name = name;
        _definition = definition ?? new DotCompute.Abstractions.Kernels.KernelDefinition { Name = name };
        Properties = new KernelProperties
        {
            MaxThreadsPerBlock = 1024,
            SharedMemorySize = 0,
            RegisterCount = 0
        };
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the source code or IL representation of the kernel.
    /// </summary>
    public string Source => _definition.Source ?? "// Placeholder kernel";

    /// <summary>
    /// Gets the entry point method name for the kernel.
    /// </summary>
    public string EntryPoint => _definition.EntryPoint ?? "main";

    /// <summary>
    /// Gets the required shared memory size in bytes.
    /// </summary>
    public int RequiredSharedMemory => 0;

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    public KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel for execution.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the compilation operation.</returns>
    public static Task CompileAsync(CancellationToken cancellationToken = default)
        // Placeholder compilation - just return completed task



        => Task.CompletedTask;

    /// <summary>
    /// Executes the kernel with the given parameters.
    /// </summary>
    /// <param name="workItems">The work items configuration.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the execution.</returns>
    public static Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        // Placeholder execution - just return completed task
        // In a real implementation, this would execute the kernel code



        => Task.CompletedTask;

    /// <summary>
    /// Gets information about the kernel parameters.
    /// </summary>
    /// <returns>The kernel parameter information.</returns>
    public static IReadOnlyList<DotCompute.Abstractions.Kernels.KernelParameter> GetParameterInfo()
        // Return empty list as placeholder doesn't have parameters



        => Array.Empty<DotCompute.Abstractions.Kernels.KernelParameter>();

    /// <summary>
    /// Disposes the kernel.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
