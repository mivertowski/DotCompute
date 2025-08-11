// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Operators;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Execution;

/// <summary>
/// Executes compiled compute plans on accelerators.
/// </summary>
public class QueryExecutor : IQueryExecutor
{
    private readonly IMemoryManagerFactory _memoryManagerFactory;
    private readonly ILogger<QueryExecutor> _logger;
    private readonly ConcurrentDictionary<IAccelerator, IMemoryManager> _memoryManagers = new();

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
    {
        // For synchronous execution, call the async version and wait
        return ExecuteAsync(context).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<object?> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug("Executing compute plan {PlanId} asynchronously with {StageCount} stages",
            context.Plan.Id, context.Plan.Stages.Count);

        var validation = Validate(context.Plan, context.Accelerator);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Plan validation failed: {validation.Message}");
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

            _logger.LogInformation("Successfully executed compute plan {PlanId} asynchronously", context.Plan.Id);
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            _logger.LogWarning("Compute plan {PlanId} execution timed out", context.Plan.Id);
            throw new TimeoutException($"Compute plan execution timed out after {context.Options.Timeout}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute compute plan {PlanId} asynchronously", context.Plan.Id);
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
    public Compilation.ValidationResult Validate(IComputePlan plan, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(accelerator);

        var errors = new List<Compilation.ValidationError>();

        // Validate memory requirements
        if (plan.EstimatedMemoryUsage > accelerator.Info.MemorySize)
        {
            errors.Add(new ValidationError(
                "INSUFFICIENT_MEMORY",
                $"Plan requires {plan.EstimatedMemoryUsage} bytes but accelerator only has {accelerator.Info.MemorySize} bytes"));
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
                errors.Add(new Compilation.ValidationError(
                    "INVALID_BLOCK_SIZE",
                    $"Stage {stage.Id} block size {blockSize} exceeds maximum {maxThreads}"));
            }
        }

        if (errors.Count > 0)
        {
            return new Compilation.ValidationResult(false, "Plan validation failed", errors);
        }

        return new Compilation.ValidationResult(true, "Plan is valid for execution");
    }

    private IMemoryManager GetMemoryManager(IAccelerator accelerator)
    {
        return _memoryManagers.GetOrAdd(accelerator, acc =>
        {
            _logger.LogDebug("Creating memory manager for accelerator {AcceleratorId}", acc.Info.Id);
            return _memoryManagerFactory.CreateMemoryManager(acc);
        });
    }

    private async Task<object?> ExecuteStage(IComputeStage stage, ExecutionContext context, IMemoryManager memoryManager)
    {
        _logger.LogDebug("Executing stage {StageId}", stage.Id);

        // Allocate output buffer
        var outputBuffer = context.BufferPool.GetOrCreateAsync(
            stage.OutputBuffer,
            EstimateBufferSize(stage, context),
            memoryManager).GetAwaiter().GetResult();

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
        await stage.Kernel.ExecuteAsync(workItems, parameters);

        // Read back the result
        var resultType = context.Plan.OutputType;
        var resultSize = EstimateTypeSize(resultType);
        var resultData = new byte[resultSize];
        
        // For now, assume the buffer has been executed and contains results
        // In a real implementation, we'd read back from the buffer

        // Convert byte array to actual result type
        return ConvertBytesToType(resultData, resultType);
    }

    private async Task<object?> ExecuteStageAsync(
        IComputeStage stage,
        ExecutionContext context,
        IMemoryManager memoryManager,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing stage {StageId} asynchronously", stage.Id);

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

    private static Operators.WorkItems CalculateWorkItems(IComputeStage stage, ExecutionContext context)
    {
        var config = stage.Configuration;
        
        return new Operators.WorkItems
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
    IMemoryManager CreateMemoryManager(IAccelerator accelerator);
}

/// <summary>
/// Default implementation of memory manager factory.
/// </summary>
public class DefaultMemoryManagerFactory : IMemoryManagerFactory
{
    private readonly ILogger<UnifiedMemoryManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMemoryManagerFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DefaultMemoryManagerFactory(ILogger<UnifiedMemoryManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IMemoryManager CreateMemoryManager(IAccelerator accelerator)
    {
        return new UnifiedMemoryManager(accelerator.Memory);
    }
}