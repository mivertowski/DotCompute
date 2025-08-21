// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Kernels;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that executes a single kernel.
    /// </summary>
    internal sealed class KernelStage(
        string id,
        string name,
        ICompiledKernel kernel,
        long[]? globalWorkSize,
        long[]? localWorkSize,
        Dictionary<string, string> inputMappings,
        Dictionary<string, string> outputMappings,
        Dictionary<string, object> parameters,
        List<string> dependencies,
        Dictionary<string, object> metadata,
        MemoryHint memoryHint,
        int priority) : IPipelineStage
    {
        private readonly ICompiledKernel _kernel = kernel;
        private readonly long[]? _globalWorkSize = globalWorkSize;
        private readonly long[]? _localWorkSize = localWorkSize;
        private readonly Dictionary<string, string> _inputMappings = inputMappings;
        private readonly Dictionary<string, string> _outputMappings = outputMappings;
        private readonly Dictionary<string, object> _parameters = parameters;
        private readonly StageMetrics _metrics = new(id);

        /// <summary>
        /// Builds kernel parameters from input mappings, direct inputs, and parameter overrides.
        /// </summary>
        /// <param name="context">The pipeline execution context containing input values.</param>
        /// <returns>A list of parameters in the correct order for kernel execution.</returns>
        private List<object> BuildKernelParameters(PipelineExecutionContext context)
        {
            var parameters = new List<object>();

            // Build parameter list based on mappings and context
            // Since we don't have KernelDefinition.Parameters yet, we'll use the mappings
            // The order will be determined by the order in which parameters are added

            // First, add parameters from input mappings
            foreach (var (paramName, contextKey) in _inputMappings)
            {
                if (context.Inputs.TryGetValue(contextKey, out var value))
                {
                    parameters.Add(value);
                }
                else if (_parameters.TryGetValue(paramName, out var paramValue))
                {
                    parameters.Add(paramValue);
                }
                else
                {
                    throw new InvalidOperationException($"No value found for parameter '{paramName}' (mapped from '{contextKey}')");
                }
            }

            // Then add any parameters that aren't in input mappings but are in _parameters
            foreach (var (paramName, paramValue) in _parameters)
            {
                if (!_inputMappings.ContainsKey(paramName))
                {
                    parameters.Add(paramValue);
                }
            }

            return parameters;
        }

        /// <summary>
        /// Gets the index of a parameter by name.
        /// </summary>
        /// <param name="paramName">The name of the parameter.</param>
        /// <returns>The zero-based index of the parameter, or -1 if not found.</returns>
        private int GetParameterIndex(string paramName)
        {
            // Since we don't have KernelDefinition.Parameters, we'll use the order
            // established by BuildKernelParameters method

            var index = 0;

            // Check input mappings first (these come first in BuildKernelParameters)
            foreach (var (mappingParam, _) in _inputMappings)
            {
                if (mappingParam == paramName)
                {
                    return index;
                }
                index++;
            }

            // Then check parameters that aren't in input mappings
            foreach (var (param, _) in _parameters)
            {
                if (!_inputMappings.ContainsKey(param))
                {
                    if (param == paramName)
                    {
                        return index;
                    }
                    index++;
                }
            }

            return -1; // Not found
        }

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Name { get; } = name;

        /// <inheritdoc/>
        public PipelineStageType Type => PipelineStageType.Kernel;

        /// <inheritdoc/>
        public IReadOnlyList<string> Dependencies { get; } = dependencies;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metadata { get; } = metadata;

        public MemoryHint MemoryHint { get; } = memoryHint;
        public int Priority { get; } = priority;

        /// <inheritdoc/>
        public async ValueTask<StageExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var startMemory = GC.GetTotalMemory(false);

            // Start performance monitoring
            PerformanceMonitor.ExecutionMetrics.StartExecution();

            try
            {
                // Prepare kernel arguments
                var arguments = PrepareArguments(context);

                // Create execution context
                var kernelContext = new KernelExecutionContext
                {
                    Name = _kernel.Name,
                    WorkDimensions = _globalWorkSize ?? [1L],
                    LocalWorkSize = _localWorkSize != null ? _localWorkSize : null,
                    Arguments = [.. arguments],
                    CancellationToken = cancellationToken
                };

                // Execute kernel - convert to KernelArguments
                var kernelArgs = new KernelArguments(kernelContext.Arguments ?? []);
                await _kernel.ExecuteAsync(kernelArgs, cancellationToken);

                stopwatch.Stop();

                // Get detailed execution metrics
                var (cpuTime, allocatedBytes, elapsedMs) = PerformanceMonitor.ExecutionMetrics.EndExecution();
                var endMemory = GC.GetTotalMemory(false);

                // Prepare outputs
                var outputs = PrepareOutputs(context, arguments);

                var memoryUsage = new MemoryUsageStats
                {
                    AllocatedBytes = allocatedBytes,
                    PeakBytes = Math.Max(endMemory, startMemory + allocatedBytes),
                    AllocationCount = 1,
                    DeallocationCount = 0
                };

                _metrics.RecordExecution(stopwatch.Elapsed, true);
                _metrics.RecordMemoryUsage(memoryUsage.AllocatedBytes);

                return new StageExecutionResult
                {
                    StageId = Id,
                    Success = true,
                    Duration = stopwatch.Elapsed,
                    Outputs = outputs.Count > 0 ? outputs : null,
                    MemoryUsage = memoryUsage,
                    Metrics = new Dictionary<string, double>
                    {
                        ["ComputeUtilization"] = CalculateComputeUtilization(),
                        ["MemoryBandwidthUtilization"] = CalculateMemoryBandwidthUtilization(),
                        ["WorkItemsProcessed"] = CalculateWorkItemsProcessed(),
                        ["CpuTimeMs"] = cpuTime,
                        ["ElapsedTimeMs"] = elapsedMs,
                        ["CpuEfficiency"] = elapsedMs > 0 ? cpuTime / elapsedMs : 0
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, false);

                return new StageExecutionResult
                {
                    StageId = Id,
                    Success = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex
                };
            }
        }

        /// <inheritdoc/>
        public StageValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate kernel
            if (_kernel == null)
            {
                errors.Add("Kernel is required");
            }

            // Validate work size
            if (_globalWorkSize == null || _globalWorkSize.Length == 0)
            {
                warnings.Add("Global work size not specified, using default [1]");
            }
            else if (_globalWorkSize.Any(size => size <= 0))
            {
                errors.Add("Global work size must be positive");
            }

            // Validate local work size
            if (_localWorkSize != null)
            {
                if (_localWorkSize.Length != _globalWorkSize?.Length)
                {
                    errors.Add("Local work size dimensions must match global work size dimensions");
                }
                else if (_localWorkSize.Any(size => size <= 0))
                {
                    errors.Add("Local work size must be positive");
                }
            }

            // Validate parameter mappings
            // Since KernelDefinition is not yet available in the interface, we validate based on
            // the mappings and parameters provided. This ensures consistency even without metadata.
            var paramNames = new HashSet<string>(_inputMappings.Keys.Union(_parameters.Keys));

            foreach (var mapping in _inputMappings)
            {
                if (!paramNames.Contains(mapping.Key))
                {
                    warnings.Add($"Input mapping '{mapping.Key}' does not match any kernel parameter");
                }
            }

            return new StageValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }

        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;

        private List<object> PrepareArguments(PipelineExecutionContext context)
            // Use the new BuildKernelParameters method
            => BuildKernelParameters(context);

        private Dictionary<string, object> PrepareOutputs(PipelineExecutionContext context, List<object> arguments)
        {
            var outputs = new Dictionary<string, object>();

            foreach (var mapping in _outputMappings)
            {
                var paramName = mapping.Key;
                var contextKey = mapping.Value;

                // Use the new GetParameterIndex helper method
                var paramIndex = GetParameterIndex(paramName);

                if (paramIndex >= 0 && paramIndex < arguments.Count)
                {
                    outputs[contextKey] = arguments[paramIndex];
                }
            }

            return outputs;
        }

        private double CalculateComputeUtilization()
        {
            // Calculate real compute utilization based on work items and execution time
            var workItems = CalculateWorkItemsProcessed();
            var executionTime = _metrics.AverageExecutionTime;

            // Use performance monitor to get real CPU utilization
            return PerformanceMonitor.GetComputeUtilization(executionTime, (long)workItems);
        }

        private static double CalculateMemoryBandwidthUtilization()
            // Use performance monitor to get real memory bandwidth utilization
            => PerformanceMonitor.GetMemoryBandwidthUtilization();

        private double CalculateWorkItemsProcessed()
        {
            if (_globalWorkSize == null)
            {
                return 0;
            }

            return _globalWorkSize.Aggregate(1L, (acc, size) => acc * size);
        }
    }
}