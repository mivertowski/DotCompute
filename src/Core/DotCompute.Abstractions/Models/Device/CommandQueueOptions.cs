// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Device
{
    /// <summary>
    /// Configuration options for command queue creation.
    /// Provides settings to control command queue behavior, performance characteristics,
    /// and execution properties when creating command queues for compute devices.
    /// </summary>
    /// <remarks>
    /// Command queues are used to manage the execution of compute operations on devices.
    /// These options allow fine-tuning of queue behavior for optimal performance based
    /// on specific use cases and hardware capabilities.
    /// </remarks>
    public sealed class CommandQueueOptions
    {
        /// <summary>
        /// Gets or sets whether to enable profiling for the command queue.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable profiling, which allows measuring execution times
        /// and performance metrics for queued operations; otherwise, <c>false</c>.
        /// Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// Enabling profiling may introduce slight performance overhead but provides
        /// valuable timing information for optimization purposes. This is particularly
        /// useful during development and performance tuning phases.
        /// </remarks>
        public bool EnableProfiling { get; set; }

        /// <summary>
        /// Gets or sets whether to enable out-of-order execution for the command queue.
        /// </summary>
        /// <value>
        /// <c>true</c> to allow commands to execute out of order when dependencies permit,
        /// potentially improving performance; otherwise, <c>false</c> for strict in-order execution.
        /// Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// Out-of-order execution can improve performance by allowing independent operations
        /// to run concurrently. However, it requires careful consideration of data dependencies
        /// and may complicate debugging. Use barriers or explicit synchronization when needed.
        /// </remarks>
        public bool EnableOutOfOrderExecution { get; set; }

        /// <summary>
        /// Gets or sets the priority level for the command queue.
        /// </summary>
        /// <value>
        /// A <see cref="QueuePriority"/> value indicating the execution priority.
        /// Default is <see cref="QueuePriority.Normal"/>.
        /// </value>
        /// <remarks>
        /// Higher priority queues may receive preferential scheduling by the device driver,
        /// but the actual behavior depends on the underlying compute platform and device
        /// capabilities. Not all devices support priority-based scheduling.
        /// </remarks>
        public QueuePriority Priority { get; set; } = QueuePriority.Normal;

        /// <summary>
        /// Gets the default command queue options.
        /// </summary>
        /// <value>
        /// A <see cref="CommandQueueOptions"/> instance with default settings:
        /// profiling disabled, in-order execution, and normal priority.
        /// </value>
        /// <remarks>
        /// This provides a convenient way to obtain standard options without
        /// explicitly creating and configuring a new instance. The default
        /// options are suitable for most general-purpose scenarios.
        /// </remarks>
        public static CommandQueueOptions Default { get; } = new CommandQueueOptions();

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandQueueOptions"/> class
        /// with default settings.
        /// </summary>
        /// <remarks>
        /// Creates options with profiling disabled, in-order execution enabled,
        /// and normal priority. These settings provide a good balance between
        /// performance and predictability for most use cases.
        /// </remarks>
        public CommandQueueOptions()
        {
            EnableProfiling = false;
            EnableOutOfOrderExecution = false;
            Priority = QueuePriority.Normal;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandQueueOptions"/> class
        /// with the specified configuration.
        /// </summary>
        /// <param name="enableProfiling">Whether to enable profiling.</param>
        /// <param name="enableOutOfOrderExecution">Whether to enable out-of-order execution.</param>
        /// <param name="priority">The queue priority level.</param>
        /// <remarks>
        /// This constructor allows explicit configuration of all queue options
        /// in a single call, providing a convenient way to create customized
        /// queue configurations.
        /// </remarks>
        public CommandQueueOptions(bool enableProfiling, bool enableOutOfOrderExecution, QueuePriority priority)
        {
            EnableProfiling = enableProfiling;
            EnableOutOfOrderExecution = enableOutOfOrderExecution;
            Priority = priority;
        }

        /// <summary>
        /// Creates a copy of this <see cref="CommandQueueOptions"/> instance.
        /// </summary>
        /// <returns>
        /// A new <see cref="CommandQueueOptions"/> instance with the same settings
        /// as the current instance.
        /// </returns>
        /// <remarks>
        /// This method performs a shallow copy of the options, creating an independent
        /// instance that can be modified without affecting the original.
        /// </remarks>
        public CommandQueueOptions Clone() => new(EnableProfiling, EnableOutOfOrderExecution, Priority);

        /// <summary>
        /// Returns a string representation of the command queue options.
        /// </summary>
        /// <returns>
        /// A string containing the current option values in a readable format.
        /// </returns>
        public override string ToString()
        {
            return $"CommandQueueOptions {{ " +
                   $"EnableProfiling: {EnableProfiling}, " +
                   $"EnableOutOfOrderExecution: {EnableOutOfOrderExecution}, " +
                   $"Priority: {Priority} }}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>
        /// <c>true</c> if the specified object is equal to the current instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj)
        {
            if (obj is not CommandQueueOptions other)
            {

                return false;
            }


            return EnableProfiling == other.EnableProfiling &&
                   EnableOutOfOrderExecution == other.EnableOutOfOrderExecution &&
                   Priority == other.Priority;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() => HashCode.Combine(EnableProfiling, EnableOutOfOrderExecution, Priority);

        /// <summary>
        /// Determines whether two <see cref="CommandQueueOptions"/> instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>
        /// <c>true</c> if the instances are equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator ==(CommandQueueOptions? left, CommandQueueOptions? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }


            if (left is null || right is null)
            {

                return false;
            }


            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two <see cref="CommandQueueOptions"/> instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>
        /// <c>true</c> if the instances are not equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator !=(CommandQueueOptions? left, CommandQueueOptions? right) => !(left == right);
    }
}
