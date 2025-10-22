// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Kernel arguments for CUDA execution
    /// </summary>
    public sealed class CudaKernelArguments
    {
        private readonly List<object> _arguments = [];
        /// <summary>
        /// Initializes a new instance of the CudaKernelArguments class.
        /// </summary>

        public CudaKernelArguments() { }
        /// <summary>
        /// Initializes a new instance of the CudaKernelArguments class.
        /// </summary>
        /// <param name="arguments">The arguments.</param>

        public CudaKernelArguments(params object[] arguments)
        {
            _arguments.AddRange(arguments);
        }
        /// <summary>
        /// Performs add.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="argument">The argument.</param>

        public void Add<T>(T argument) where T : unmanaged => _arguments.Add(argument);
        /// <summary>
        /// Performs add buffer.
        /// </summary>
        /// <param name="devicePointer">The device pointer.</param>

        public void AddBuffer(IntPtr devicePointer) => _arguments.Add(devicePointer);
        /// <summary>
        /// Gets to array.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public object[] ToArray() => [.. _arguments];
        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        /// <value>The count.</value>

        public int Count => _arguments.Count;
    }
}