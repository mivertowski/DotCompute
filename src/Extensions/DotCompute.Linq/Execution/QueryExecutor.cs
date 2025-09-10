// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Linq.Operators.Interfaces;
using DotCompute.Linq.Operators.Types;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;

namespace DotCompute.Linq.Execution;


/// <summary>
/// Executes compiled compute plans on accelerators.
/// </summary>
public class QueryExecutor : IQueryExecutor
{
    private readonly IMemoryManagerFactory _memoryManagerFactory;
    private readonly ILogger<QueryExecutor> _logger;
    private readonly ConcurrentDictionary<IAccelerator, IUnifiedMemoryManager> _memoryManagers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExecutor"/> class.
    /// </summary>
    /// <param name="memoryManagerFactory">The memory manager factory.</param>
    /// <param name="logger">The logger instance.</param>
    public QueryExecutor(
        IMemoryManagerFactory memoryManagerFactory,
        ILogger<QueryExecutor> logger)
    {
        _memoryManagerFactory = memoryManagerFactory ?? throw new ArgumentNullException(nameof(memoryManagerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public object? Execute(ExecutionContext context)
        // For synchronous execution, call the async version and wait


        => ExecuteAsync(context, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<object?> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebugMessage($"Executing compute plan {context.Plan.Id} asynchronously with {context.Plan.Stages.Count} stages");

        var validation = Validate(context.Plan, context.Accelerator);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Plan validation failed: {validation.ErrorMessage}");
        }

        // Get or create memory manager for the accelerator
        var memoryManager = GetMemoryManager(context.Accelerator);

        // Set up timeout if specified
        using var timeoutCts = context.Options.Timeout.HasValue
            ? new CancellationTokenSource(context.Options.Timeout.Value)
            : null;

        using var linkedCts = timeoutCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Execute each stage in sequence
            object? result = null;
            foreach (var stage in context.Plan.Stages)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                result = await ExecuteStageAsync(stage, context, memoryManager, linkedCts.Token);
            }

            _logger.LogInfoMessage($"Successfully executed compute plan {context.Plan.Id} asynchronously");
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            _logger.LogWarningMessage($"Compute plan {context.Plan.Id} execution timed out");
            throw new TimeoutException($"Compute plan execution timed out after {context.Options.Timeout}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to execute compute plan {context.Plan.Id} asynchronously");
            throw;
        }
        finally
        {
            // Clean up buffers if memory pooling is disabled
            if (!context.Options.EnableMemoryPooling)
            {
                context.BufferPool.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(IComputePlan plan, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(accelerator);

        var hasErrors = false;
        var errorMessage = "Plan validation failed";

        // Validate memory requirements
        if (plan.EstimatedMemoryUsage > accelerator.Info.MemorySize)
        {
            hasErrors = true;
            errorMessage = $"Plan requires {plan.EstimatedMemoryUsage} bytes but accelerator only has {accelerator.Info.MemorySize} bytes";
        }

        // Validate stages
        foreach (var stage in plan.Stages)
        {
            // TODO: Validate kernel compatibility when AcceleratorCapabilities is defined

            // Validate execution configuration
            var maxThreads = accelerator.Info.MaxThreadsPerBlock;
            var blockSize = stage.Configuration.BlockDimensions.X *
                           stage.Configuration.BlockDimensions.Y *
                           stage.Configuration.BlockDimensions.Z;

            if (blockSize > maxThreads)
            {
                hasErrors = true;
                errorMessage = $"Stage {stage.Id} block size {blockSize} exceeds maximum {maxThreads}";
                break;
            }
        }

        if (hasErrors)
        {
            return DotCompute.Abstractions.Validation.UnifiedValidationResult.Failure(errorMessage);
        }

        return DotCompute.Abstractions.Validation.UnifiedValidationResult.Success();
    }

    private IUnifiedMemoryManager GetMemoryManager(IAccelerator accelerator)
    {
        return _memoryManagers.GetOrAdd(accelerator, acc =>
        {
            _logger.LogDebugMessage($"Creating memory manager for accelerator {acc.Info.Id}");
            return _memoryManagerFactory.CreateMemoryManager(acc);
        });
    }

    private async Task<object?> ExecuteStage(IComputeStage stage, ExecutionContext context, IUnifiedMemoryManager memoryManager)
    {
        _logger.LogDebugMessage($"Executing stage {stage.Id}");

        // Allocate output buffer
        var outputBuffer = await context.BufferPool.GetOrCreateAsync(
            stage.OutputBuffer,
            EstimateBufferSize(stage, context),
            memoryManager).ConfigureAwait(false);

        // Prepare kernel parameters
        var parameters = new Dictionary<string, object>();

        // Add input buffers
        foreach (var inputName in stage.InputBuffers)
        {
            if (!context.Parameters.TryGetValue(inputName, out var inputValue))
            {
                throw new InvalidOperationException($"Missing input parameter: {inputName}");
            }
            parameters[inputName] = inputValue;
        }

        // Add output buffer
        parameters[stage.OutputBuffer] = outputBuffer;

        // Add additional parameters from context
        foreach (var param in stage.Configuration.Parameters)
        {
            parameters[param.Key] = param.Value;
        }

        // Execute the kernel
        var workItems = CalculateWorkItems(stage, context);
        await stage.Kernel.ExecuteAsync(workItems, parameters, CancellationToken.None);

        // Read back the result
        var resultType = context.Plan.OutputType;

        // In a production implementation, we'd read the actual results from the GPU buffer
        // For now, simulate result based on operation type
        return SimulateKernelResult(stage, resultType, workItems);
    }

    private async Task<object?> ExecuteStageAsync(
        IComputeStage stage,
        ExecutionContext context,
        IUnifiedMemoryManager memoryManager,
        CancellationToken cancellationToken)
    {
        _logger.LogDebugMessage($"Executing stage {stage.Id} asynchronously");

        // Allocate output buffer
        var outputBuffer = await context.BufferPool.GetOrCreateAsync(
            stage.OutputBuffer,
            EstimateBufferSize(stage, context),
            memoryManager);

        // Prepare kernel parameters
        var parameters = new Dictionary<string, object>();

        // Add input buffers
        foreach (var inputName in stage.InputBuffers)
        {
            if (!context.Parameters.TryGetValue(inputName, out var inputValue))
            {
                throw new InvalidOperationException($"Missing input parameter: {inputName}");
            }
            parameters[inputName] = inputValue;
        }

        // Add output buffer
        parameters[stage.OutputBuffer] = outputBuffer;

        // Add additional parameters from context
        foreach (var param in stage.Configuration.Parameters)
        {
            parameters[param.Key] = param.Value;
        }

        // Execute the kernel
        var workItems = CalculateWorkItems(stage, context);
        await stage.Kernel.ExecuteAsync(workItems, parameters, cancellationToken);

        // Read back the result
        var resultType = context.Plan.OutputType;
        var resultSize = EstimateTypeSize(resultType);
        var resultData = new byte[resultSize];

        // For now, assume the buffer has been executed and contains results
        // In a real implementation, we'd read back from the buffer

        // Convert byte array to actual result type
        return ConvertBytesToType(resultData, resultType);
    }

    private static long EstimateBufferSize(IComputeStage stage, ExecutionContext context)
    {
        // This is a simplified estimation - in production, we'd use more sophisticated logic
        const long defaultElementCount = 1000;
        const long elementSize = 8; // Assume 8 bytes per element

        return defaultElementCount * elementSize;
    }

    private static WorkItems CalculateWorkItems(IComputeStage stage, ExecutionContext context)
    {
        var config = stage.Configuration;

        return new WorkItems
        {
            GlobalWorkSize =
            [
                config.GridDimensions.X * config.BlockDimensions.X,
            config.GridDimensions.Y * config.BlockDimensions.Y,
            config.GridDimensions.Z * config.BlockDimensions.Z
            ],
            LocalWorkSize =
            [
                config.BlockDimensions.X,
            config.BlockDimensions.Y,
            config.BlockDimensions.Z
            ]
        };
    }

    [RequiresUnreferencedCode("This method uses reflection to estimate type sizes for execution planning")]
    private static long EstimateTypeSize(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            const long defaultArraySize = 1000;
            return EstimateElementSize(elementType) * defaultArraySize;
        }

        return EstimateElementSize(type);
    }

    private static long EstimateElementSize(Type type)
    {
        if (type.IsPrimitive)
        {
            return type.Name switch
            {
                "Boolean" => sizeof(bool),
                "Byte" => sizeof(byte),
                "SByte" => sizeof(sbyte),
                "Int16" => sizeof(short),
                "UInt16" => sizeof(ushort),
                "Int32" => sizeof(int),
                "UInt32" => sizeof(uint),
                "Int64" => sizeof(long),
                "UInt64" => sizeof(ulong),
                "Single" => sizeof(float),
                "Double" => sizeof(double),
                "Decimal" => sizeof(decimal),
                "Char" => sizeof(char),
                _ => IntPtr.Size
            };
        }

        return IntPtr.Size;
    }

    [RequiresDynamicCode("This method uses Array.CreateInstance for result conversion")]
    [RequiresUnreferencedCode("This method uses reflection to convert byte data to target types")]
    private static object? ConvertBytesToType(byte[] data, Type targetType)
    {
        // This is a simplified conversion - in production, we'd use proper serialization
        if (targetType == typeof(int))
        {
            return BitConverter.ToInt32(data, 0);
        }

        if (targetType == typeof(long))
        {
            return BitConverter.ToInt64(data, 0);
        }

        if (targetType == typeof(float))
        {
            return BitConverter.ToSingle(data, 0);
        }

        if (targetType == typeof(double))
        {
            return BitConverter.ToDouble(data, 0);
        }

        if (targetType.IsArray)
        {
            // Handle array types
            var elementType = targetType.GetElementType()!;
            var elementSize = (int)EstimateElementSize(elementType);
            var elementCount = data.Length / elementSize;

            var array = Array.CreateInstance(elementType, elementCount);

            // Copy elements
            for (var i = 0; i < elementCount; i++)
            {
                var elementData = new byte[elementSize];
                Array.Copy(data, i * elementSize, elementData, 0, elementSize);
                var element = ConvertBytesToType(elementData, elementType);
                array.SetValue(element, i);
            }

            return array;
        }

        // For complex types, return the raw data
        return data;
    }

    [RequiresDynamicCode("This method uses Array.CreateInstance for simulation purposes only")]
    [RequiresUnreferencedCode("This method uses Activator.CreateInstance for simulation purposes only")]
    private static object? SimulateKernelResult(IComputeStage stage, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type resultType, WorkItems workItems)
    {
        // Simulate different types of kernel results based on the stage
        var totalWorkItems = workItems.GlobalWorkSize.Aggregate(1, (a, b) => a * b);

        if (resultType == typeof(int[]))
        {
            // Simulate array result
            var result = new int[totalWorkItems];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = i * 2; // Simulate some computation
            }
            return result;
        }

        if (resultType == typeof(float[]))
        {
            var result = new float[totalWorkItems];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = i * 1.5f;
            }
            return result;
        }

        if (resultType == typeof(int))
        {
            // Simulate reduction result
            return totalWorkItems * 42;
        }

        if (resultType == typeof(float))
        {
            return (float)(totalWorkItems * 3.14);
        }

        if (resultType == typeof(bool))
        {
            return totalWorkItems > 0;
        }

        // Default: return a collection of the appropriate size
        if (resultType.IsArray)
        {
            var elementType = resultType.GetElementType()!;
            var array = Array.CreateInstance(elementType, totalWorkItems);

            // Fill with default values
            for (var i = 0; i < totalWorkItems; i++)
            {
                array.SetValue(Activator.CreateInstance(elementType), i);
            }

            return array;
        }

        // Return default value for the type
        return Activator.CreateInstance(resultType);
    }
}

/// <summary>
/// Factory for creating memory managers.
/// </summary>
public interface IMemoryManagerFactory
{
    /// <summary>
    /// Creates a memory manager for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <returns>A memory manager instance.</returns>
    public IUnifiedMemoryManager CreateMemoryManager(IAccelerator accelerator);
}

/// <summary>
/// Default implementation of memory manager factory.
/// </summary>
public class DefaultMemoryManagerFactory : IMemoryManagerFactory
{
    private readonly ILogger<IUnifiedMemoryManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMemoryManagerFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DefaultMemoryManagerFactory(ILogger<IUnifiedMemoryManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IUnifiedMemoryManager CreateMemoryManager(IAccelerator accelerator) => accelerator.Memory;
}
