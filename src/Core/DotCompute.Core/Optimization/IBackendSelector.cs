// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Optimization.Models;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Optimization.Selection;

namespace DotCompute.Core.Optimization;

/// <summary>
/// Interface for adaptive backend selection with machine learning capabilities.
/// </summary>
public interface IBackendSelector : IDisposable
{
    /// <summary>
    /// Selects the optimal backend for executing a kernel based on workload characteristics and historical performance.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute</param>
    /// <param name="workloadCharacteristics">Characteristics of the workload</param>
    /// <param name="availableBackends">Available backends to choose from</param>
    /// <param name="constraints">Optional constraints for backend selection</param>
    /// <returns>Optimal backend selection with confidence score</returns>
    public Task<BackendSelection> SelectOptimalBackendAsync(
        string kernelName,
        WorkloadCharacteristics workloadCharacteristics,
        IEnumerable<IAccelerator> availableBackends,
        SelectionConstraints? constraints = null);

    /// <summary>
    /// Records the actual performance results for learning and adaptation.
    /// </summary>
    /// <param name="kernelName">Name of the executed kernel</param>
    /// <param name="workloadCharacteristics">Workload characteristics used for selection</param>
    /// <param name="selectedBackend">Backend that was selected</param>
    /// <param name="performanceResult">Actual performance results</param>
    public Task RecordPerformanceResultAsync(
        string kernelName,
        WorkloadCharacteristics workloadCharacteristics,
        string selectedBackend,
        PerformanceResult performanceResult);
}
