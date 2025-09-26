// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Plans
{
    /// <summary>
    /// Execution plan for data parallel execution across multiple devices.
    /// In data parallelism, the same computation is applied to different portions of the input data,
    /// with each device processing its assigned subset of the total dataset.
    /// </summary>
    /// <typeparam name="T">The data type for the execution plan. Must be an unmanaged type.</typeparam>
    public class DataParallelExecutionPlan<T> : ExecutionPlan<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the input buffers containing the data to be processed.
        /// These buffers hold the input data that will be distributed across devices.
        /// </summary>
        public required AbstractionsMemory.IUnifiedMemoryBuffer<T>[] InputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the output buffers where results will be stored.
        /// These buffers will contain the computed results from all devices.
        /// </summary>
        public required AbstractionsMemory.IUnifiedMemoryBuffer<T>[] OutputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the device-specific tasks that define how work is distributed.
        /// Each task contains the information needed for a specific device to process its portion of the data.
        /// </summary>
        public required DataParallelDeviceTask<T>[] DeviceTasks { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataParallelExecutionPlan{T}"/> class.
        /// Sets the strategy type to DataParallel by default.
        /// </summary>
        public DataParallelExecutionPlan()
        {
            StrategyType = ExecutionStrategyType.DataParallel;
        }
    }

    /// <summary>
    /// Represents a task for a single device in data parallel execution.
    /// Contains all the information needed for one device to process its assigned portion of the data.
    /// </summary>
    /// <typeparam name="T">The data type for the device task. Must be an unmanaged type.</typeparam>
    public class DataParallelDeviceTask<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the target device that will execute this task.
        /// This is the specific accelerator (GPU, CPU, etc.) assigned to process this portion of data.
        /// </summary>
        public required IAccelerator Device { get; set; }

        /// <summary>
        /// Gets or sets the compiled kernel for this device.
        /// This is the device-specific compiled version of the kernel that will be executed.
        /// </summary>
        public required ManagedCompiledKernel CompiledKernel { get; set; }

        /// <summary>
        /// Gets or sets the device-local input buffers.
        /// These are the input data buffers that have been transferred to the device's memory.
        /// </summary>
        public required AbstractionsMemory.IUnifiedMemoryBuffer<T>[] InputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the device-local output buffers.
        /// These are the output data buffers where the device will store its computed results.
        /// </summary>
        public required AbstractionsMemory.IUnifiedMemoryBuffer<T>[] OutputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the start index in the global data array.
        /// This defines the beginning of the data range that this device will process.
        /// </summary>
        public required int StartIndex { get; set; }

        /// <summary>
        /// Gets or sets the number of elements to process.
        /// This defines how many elements from the start index this device should process.
        /// </summary>
        public required int ElementCount { get; set; }

        /// <summary>
        /// Gets or sets dependencies on other device tasks.
        /// Contains the indices of other device tasks that must complete before this task can begin.
        /// </summary>
        public List<int> Dependencies { get; set; } = [];
    }
}
