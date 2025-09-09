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

        public CudaKernelArguments() { }

        public CudaKernelArguments(params object[] arguments)
        {
            _arguments.AddRange(arguments);
        }

        public void Add<T>(T argument) where T : unmanaged => _arguments.Add(argument);

        public void AddBuffer(IntPtr devicePointer) => _arguments.Add(devicePointer);

        public object[] ToArray() => [.. _arguments];

        public int Count => _arguments.Count;
    }
}