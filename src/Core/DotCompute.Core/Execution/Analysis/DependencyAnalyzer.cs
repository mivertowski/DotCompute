// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Pipeline;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Analyzes dependencies between data buffers, model layers, and pipeline stages
    /// to optimize parallel execution planning.
    /// </summary>
    /// <remarks>
    /// This analyzer identifies various types of dependencies including data hazards,
    /// structural dependencies, and control flow dependencies to enable safe parallel execution.
    /// </remarks>
    public sealed class DependencyAnalyzer
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyAnalyzer"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for diagnostic information.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public DependencyAnalyzer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Analyzes data dependencies for parallel execution between input and output buffers.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type contained in the buffers.</typeparam>
        /// <param name="inputBuffers">The array of input buffers to analyze.</param>
        /// <param name="outputBuffers">The array of output buffers to analyze.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A <see cref="ValueTask{T}"/> containing a <see cref="DependencyGraph"/> 
        /// representing the analyzed dependencies.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="inputBuffers"/> or <paramref name="outputBuffers"/> is null.
        /// </exception>
        public async ValueTask<DependencyGraph> AnalyzeDataDependenciesAsync<T>(
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IUnifiedMemoryBuffer<T>[] outputBuffers,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(inputBuffers);


            ArgumentNullException.ThrowIfNull(outputBuffers);


            await Task.CompletedTask.ConfigureAwait(false);
            var graph = new DependencyGraph();

            // Analyze read-after-write and write-after-read dependencies
            for (var i = 0; i < inputBuffers.Length; i++)
            {
                for (var j = 0; j < outputBuffers.Length; j++)
                {
                    if (BuffersOverlap(inputBuffers[i], outputBuffers[j]))
                    {
                        graph.AddDependency(i, j, DependencyType.DataHazard);
                        _logger.LogTrace("Found data dependency between input {InputIndex} and output {OutputIndex}", i, j);
                    }
                }
            }

            // Add control dependencies based on execution order requirements
            for (var i = 0; i < inputBuffers.Length - 1; i++)
            {
                // Sequential processing requirement
                graph.AddDependency(i, i + 1, DependencyType.Control);
            }

            _logger.LogInformation("Analyzed data dependencies: {DependencyCount} dependencies found", graph.TotalDependencies);
            return graph;
        }

        /// <summary>
        /// Analyzes dependencies between model layers for parallel execution planning.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type processed by the layers.</typeparam>
        /// <param name="layers">The list of model layers to analyze.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A <see cref="ValueTask{T}"/> containing a <see cref="DependencyGraph"/> 
        /// representing the layer dependencies.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="layers"/> is null.</exception>
        public async ValueTask<DependencyGraph> AnalyzeLayerDependenciesAsync<T>(
            List<ModelLayer<T>> layers,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(layers);


            var graph = new DependencyGraph();

            foreach (var layer in layers)
            {
                // Add explicit dependencies from layer definition
                foreach (var depId in layer.Dependencies)
                {
                    graph.AddDependency(depId, layer.LayerId, DependencyType.Structural);
                }

                // Analyze tensor dependencies
                await AnalyzeTensorDependenciesAsync(layer, layers, graph, cancellationToken);
            }

            _logger.LogInformation("Analyzed layer dependencies for {LayerCount} layers: {DependencyCount} dependencies",
                layers.Count, graph.TotalDependencies);
            return graph;
        }

        /// <summary>
        /// Analyzes dependencies between pipeline stages for optimal execution ordering.
        /// </summary>
        /// <param name="stages">The list of pipeline stage definitions to analyze.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A <see cref="ValueTask{T}"/> containing a <see cref="DependencyGraph"/> 
        /// representing the stage dependencies.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stages"/> is null.</exception>
        public async ValueTask<DependencyGraph> AnalyzeStageDependenciesAsync(
            List<PipelineStageDefinition> stages,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stages);


            await Task.CompletedTask.ConfigureAwait(false);
            var graph = new DependencyGraph();

            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];

                // Add explicit dependencies
                foreach (var depName in stage.Dependencies)
                {
                    var depIndex = stages.FindIndex(s => s.Name == depName);
                    if (depIndex >= 0 && depIndex != i)
                    {
                        graph.AddDependency(depIndex, i, DependencyType.Structural);
                    }
                }

                // Sequential stages have implicit dependencies
                if (i > 0 && stage.Dependencies.Count == 0)
                {
                    graph.AddDependency(i - 1, i, DependencyType.Sequential);
                }
            }

            _logger.LogInformation("Analyzed stage dependencies for {StageCount} stages: {DependencyCount} dependencies",
                stages.Count, graph.TotalDependencies);
            return graph;
        }

        /// <summary>
        /// Determines if two buffers have overlapping memory regions.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type contained in the buffers.</typeparam>
        /// <param name="buffer1">The first buffer to compare.</param>
        /// <param name="buffer2">The second buffer to compare.</param>
        /// <returns><c>true</c> if the buffers overlap in memory; otherwise, <c>false</c>.</returns>
        private static bool BuffersOverlap<T>(IUnifiedMemoryBuffer<T> buffer1, IUnifiedMemoryBuffer<T> buffer2) where T : unmanaged
            // Simplified overlap detection - real implementation would check memory addresses
            // For now, assume buffers don't overlap unless they're the same reference
            => ReferenceEquals(buffer1, buffer2);

        /// <summary>
        /// Analyzes tensor dependencies within a model layer.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type processed by the layer.</typeparam>
        /// <param name="layer">The layer to analyze.</param>
        /// <param name="allLayers">All layers in the model for cross-references.</param>
        /// <param name="graph">The dependency graph to update.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private async ValueTask AnalyzeTensorDependenciesAsync<T>(
            ModelLayer<T> layer,
            List<ModelLayer<T>> allLayers,
            DependencyGraph graph,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Analyze input/output tensor dependencies
            var inputTensors = layer.InputTensors;
            var outputTensors = layer.OutputTensors;

            // Check if any layer's output is used as this layer's input
            foreach (var otherLayer in allLayers.Where(l => l.LayerId != layer.LayerId))
            {
                foreach (var outputTensor in otherLayer.OutputTensors)
                {
                    if (inputTensors.Any(input => input.Name == outputTensor.Name))
                    {
                        graph.AddDependency(otherLayer.LayerId, layer.LayerId, DependencyType.DataFlow);
                        _logger.LogTrace("Found tensor dependency: Layer {FromLayer} -> Layer {ToLayer} via tensor {TensorName}",
                            otherLayer.LayerId, layer.LayerId, outputTensor.Name);
                    }
                }
            }

            await ValueTask.CompletedTask;
        }
    }
}