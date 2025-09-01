// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Compute
{

    /// <summary>
    /// Context for kernel execution containing work dimensions and arguments.
    /// </summary>
    public class KernelExecutionContext
    {
        /// <summary>
        /// Name of the kernel being executed.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Arguments passed to the kernel.
        /// </summary>
        public object[] Arguments { get; set; } = [];

        /// <summary>
        /// Work dimensions (global work size).
        /// </summary>
        public long[]? WorkDimensions { get; set; }

        /// <summary>
        /// Local work size for work group optimization.
        /// </summary>
        public long[]? LocalWorkSize { get; set; }

        /// <summary>
        /// Cancellation token for the execution.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Additional metadata for kernel execution.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = [];

        /// <summary>
        /// Performance hints for optimization.
        /// </summary>
        public Dictionary<string, object> PerformanceHints { get; set; } = [];
        
        /// <summary>
        /// Buffer storage for kernel parameters.
        /// </summary>
        private readonly Dictionary<int, IUnifiedMemoryBuffer> _buffers = [];
        
        /// <summary>
        /// Scalar storage for kernel parameters.
        /// </summary>
        private readonly Dictionary<int, object> _scalars = [];
        
        /// <summary>
        /// Gets a buffer parameter by index.
        /// </summary>
        /// <param name="index">The parameter index.</param>
        /// <returns>The buffer at the specified index.</returns>
        public IUnifiedMemoryBuffer GetBuffer(int index)
        {
            if (_buffers.TryGetValue(index, out var buffer))
            {

                return buffer;
            }


            throw new ArgumentException($"No buffer found at index {index}", nameof(index));
        }
        
        /// <summary>
        /// Gets a scalar parameter by index.
        /// </summary>
        /// <typeparam name="T">The scalar type.</typeparam>
        /// <param name="index">The parameter index.</param>
        /// <returns>The scalar value at the specified index.</returns>
        public T GetScalar<T>(int index)
        {
            if (_scalars.TryGetValue(index, out var scalar))
            {

                return (T)scalar;
            }


            throw new ArgumentException($"No scalar found at index {index}", nameof(index));
        }
        
        /// <summary>
        /// Sets a parameter value.
        /// </summary>
        /// <param name="index">The parameter index.</param>
        /// <param name="value">The parameter value.</param>
        public void SetParameter(int index, object value)
        {
            if (value is IUnifiedMemoryBuffer buffer)
            {
                _buffers[index] = buffer;
            }
            else
            {
                _scalars[index] = value;
            }
        }


        /// <summary>
        /// Sets a buffer parameter.
        /// </summary>
        /// <param name="index">The parameter index.</param>
        /// <param name="buffer">The buffer value.</param>
        public void SetBuffer(int index, IUnifiedMemoryBuffer buffer) => _buffers[index] = buffer ?? throw new ArgumentNullException(nameof(buffer));


        /// <summary>
        /// Sets a scalar parameter.
        /// </summary>
        /// <param name="index">The parameter index.</param>
        /// <param name="scalar">The scalar value.</param>
        public void SetScalar<T>(int index, T scalar) => _scalars[index] = scalar ?? throw new ArgumentNullException(nameof(scalar));
    }
}
