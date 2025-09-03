// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Threading.Tasks;

namespace DotCompute.Backends.CUDA.Extensions
{
    /// <summary>
    /// Extension methods for Task conversions.
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// Converts a Task to a ValueTask.
        /// </summary>
        public static ValueTask AsValueTask(this Task task)
        {
            return new ValueTask(task);
        }

        /// <summary>
        /// Converts a Task{T} to a ValueTask{T}.
        /// </summary>
        public static ValueTask<T> AsValueTask<T>(this Task<T> task)
        {
            return new ValueTask<T>(task);
        }
    }
}