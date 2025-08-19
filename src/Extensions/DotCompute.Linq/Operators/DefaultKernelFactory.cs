// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    _generators = new Dictionary<AcceleratorType, IKernelGenerator>();
    
    // Register built-in generators
    RegisterBuiltInGenerators();
}

/// <summary>
/// Creates a kernel for the specified accelerator.
/// </summary>
/// <param name="accelerator">The target accelerator.</param>
/// <param name="definition">The kernel definition.</param>
/// <returns>A compiled kernel.</returns>
public IKernel CreateKernel(IAccelerator accelerator, KernelDefinition definition)
{
    // Try to find a matching generator
    if (_generators.TryGetValue(accelerator.Type, out var generator))
    {
        return CreateDynamicKernel(accelerator, definition, generator);
    }

    // Fallback to placeholder for unsupported accelerators
    _logger.LogWarning("No kernel generator found for accelerator type {AcceleratorType}, using placeholder", 
        accelerator.Type);
    return new PlaceholderKernel(definition.Name, definition);
}

/// <summary>
/// Creates a kernel from an expression tree.
/// </summary>
/// <param name="accelerator">The target accelerator.</param>
/// <param name="expression">The expression to compile.</param>
/// <param name="context">The generation context.</param>
/// <returns>A compiled kernel.</returns>
public IKernel CreateKernelFromExpression(IAccelerator accelerator, Expression expression, KernelGenerationContext? context = null)
{
    context ??= CreateDefaultContext(accelerator);

    if (_generators.TryGetValue(accelerator.Type, out var generator))
    {
        try
        {
            // Check if expression can be compiled
            if (!generator.CanCompile(expression))
            {
                _logger.LogWarning("Expression cannot be compiled for {AcceleratorType}, using fallback", accelerator.Type);
                return new ExpressionFallbackKernel(expression);
            }

            // Generate the kernel
            var generatedKernel = generator.GenerateKernel(expression, context);
            return new DynamicCompiledKernel(generatedKernel, accelerator, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate kernel from expression for {AcceleratorType}", accelerator.Type);
            return new ExpressionFallbackKernel(expression);
        }
    }

    // No generator available
    _logger.LogWarning("No kernel generator available for {AcceleratorType}", accelerator.Type);
    return new ExpressionFallbackKernel(expression);
}

private IKernel CreateDynamicKernel(IAccelerator accelerator, KernelDefinition definition, IKernelGenerator generator)
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
            return new DynamicCompiledKernel(generatedKernel, accelerator, _logger);
        }

        // Fallback to placeholder
        return new PlaceholderKernel(definition.Name, definition);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create dynamic kernel {KernelName}", definition.Name);
        return new PlaceholderKernel(definition.Name, definition);
    }
}

private IKernel CreateFusedKernel(IAccelerator accelerator, KernelDefinition definition, 
    IKernelGenerator generator, Dictionary<string, object> fusionMetadata)
{
    var fusionType = fusionMetadata.GetValueOrDefault("FusionType")?.ToString() ?? "Generic";
    var operations = fusionMetadata.GetValueOrDefault("FusedOperations") as string[] ?? [];
    
    _logger.LogDebug("Creating fused kernel for operations: {Operations}", string.Join(", ", operations));
    
    // Create a custom operation type for the fused kernel
    var fusedOperationType = $"Fused_{string.Join("_", operations)}";
    
    var inputTypes = ExtractInputTypes(definition);
    var outputType = ExtractOutputType(definition);
    var context = CreateGenerationContext(accelerator, definition);
    
    // Add fusion-specific metadata to context
    context.Metadata ??= new Dictionary<string, object>();
    context.Metadata["FusionType"] = fusionType;
    context.Metadata["FusedOperations"] = operations;
    context.Metadata["EstimatedSpeedup"] = fusionMetadata.GetValueOrDefault("EstimatedSpeedup", 1.2);
    
    var generatedKernel = generator.GenerateOperationKernel(fusedOperationType, inputTypes, outputType, context);
    return new DynamicCompiledKernel(generatedKernel, accelerator, _logger);
}

/// <summary>
/// Validates whether a kernel definition is supported on the accelerator.
/// </summary>
/// <param name="accelerator">The target accelerator.</param>
/// <param name="definition">The kernel definition to validate.</param>
/// <returns>True if the kernel is supported; otherwise, false.</returns>
public bool IsKernelSupported(IAccelerator accelerator, KernelDefinition definition)
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
    if (_generators.TryGetValue(accelerator.Type, out var generator))
    {
        return new[] { GetKernelLanguageForAccelerator(accelerator.Type) };
    }
    
    // Return basic language support as fallback
    return new[] { KernelLanguage.CSharp };
}

private void RegisterBuiltInGenerators()
{
    // Built-in generators are now registered through the backend plugin system
    // This method is kept for backwards compatibility but generators are loaded
    // dynamically through the backend factory pattern when accelerators are created
    
    _logger.LogDebug("Built-in generators will be registered through backend plugins");
}

private static Dictionary<string, object>? ExtractFusionMetadata(KernelDefinition definition)
{
    if (definition.Metadata.TryGetValue("FusionMetadata", out var metadata) && metadata is Dictionary<string, object> fusionData)
    {
        return fusionData;
    }
    return null;
}

private static Type[] ExtractInputTypes(KernelDefinition definition)
{
    return definition.Parameters
        .Where(p => p.Direction == ParameterDirection.In || p.Direction == ParameterDirection.InOut)
        .Select(p => p.Type)
        .ToArray();
}

private static Type ExtractOutputType(KernelDefinition definition)
{
    var outputParam = definition.Parameters
        .FirstOrDefault(p => p.Direction == ParameterDirection.Out || p.Direction == ParameterDirection.InOut);
    return outputParam?.Type ?? typeof(object);
}

private static KernelGenerationContext CreateGenerationContext(IAccelerator accelerator, KernelDefinition definition)
{
    return new KernelGenerationContext
    {
        DeviceInfo = accelerator.Info,
        UseSharedMemory = definition.Metadata.GetValueOrDefault("UseSharedMemory", true) is bool useShared && useShared,
        UseVectorTypes = definition.Metadata.GetValueOrDefault("UseVectorTypes", true) is bool useVector && useVector,
        Precision = definition.Metadata.GetValueOrDefault("Precision") is PrecisionMode precision ? precision : PrecisionMode.Single,
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
        Precision = PrecisionMode.Single
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
            DeviceInfo = new AcceleratorInfo(AcceleratorType.CPU, "Test", "1.0", 1024*1024*1024),
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
private readonly KernelDefinition _definition;

public PlaceholderKernel(string name, KernelDefinition? definition = null)
{
    Name = name;
    _definition = definition ?? new KernelDefinition { Name = name };
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
/// Gets the kernel properties.
/// </summary>
public KernelProperties Properties { get; }

/// <summary>
/// Compiles the kernel for execution.
/// </summary>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A task representing the compilation operation.</returns>
public Task CompileAsync(CancellationToken cancellationToken = default)
{
    // Placeholder compilation - just return completed task
    return Task.CompletedTask;
}

/// <summary>
/// Executes the kernel with the given parameters.
/// </summary>
/// <param name="workItems">The work items configuration.</param>
/// <param name="parameters">The kernel parameters.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A task representing the execution.</returns>
public Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
{
    // Placeholder execution - just return completed task
    // In a real implementation, this would execute the kernel code
    return Task.CompletedTask;
}

/// <summary>
/// Gets information about the kernel parameters.
/// </summary>
/// <returns>The kernel parameter information.</returns>
public IReadOnlyList<KernelParameter> GetParameterInfo()
{
    // Return parameters from definition if available
    return _definition.Parameters.ToArray();
}

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
