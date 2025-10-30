// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
        public static ValueTask AsValueTaskAsync(this Task task) => new(task);

        /// <summary>
        /// Converts a Task{T} to a ValueTask{T}.
        /// </summary>
        public static ValueTask<T> AsValueTaskAsync<T>(this Task<T> task) => new(task);
    }
}
