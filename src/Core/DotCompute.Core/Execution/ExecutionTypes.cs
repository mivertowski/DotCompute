// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Metrics;
using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Result of parallel execution.
    /// </summary>
    public partial class ParallelExecutionResult
    {
        private DeviceExecutionResult[] _deviceResults = [];

        /// <summary>Gets or sets whether the execution was successful.</summary>
        public required bool Success { get; set; }

        /// <summary>Gets or sets the total execution time in milliseconds.</summary>
        public required double TotalExecutionTimeMs { get; set; }

        /// <summary>Gets the results from each device.</summary>
        public IReadOnlyList<DeviceExecutionResult> DeviceResults
        {
            get => _deviceResults;
            init => _deviceResults = value?.ToArray() ?? [];
        }

        /// <summary>Sets the results from each device.</summary>
        /// <param name="results">The device results to set.</param>
        public void SetDeviceResults(DeviceExecutionResult[] results) => _deviceResults = results;

        /// <summary>Gets or sets the execution strategy used.</summary>
        public required ExecutionStrategyType Strategy { get; set; }

        /// <summary>Gets or sets the overall throughput in GFLOPS.</summary>
        public double ThroughputGFLOPS { get; set; }

        /// <summary>Gets or sets the overall memory bandwidth in GB/s.</summary>
        public double MemoryBandwidthGBps { get; set; }

        /// <summary>Gets or sets the parallel efficiency percentage.</summary>
        public double EfficiencyPercentage { get; set; }

        /// <summary>Gets or sets any error message.</summary>
        public string? ErrorMessage { get; set; }
    }
}