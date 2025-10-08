// <copyright file="TransferOperation.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Memory.Types;

/// <summary>
/// Represents a queued transfer operation that can be executed asynchronously.
/// </summary>
/// <remarks>
/// This abstract class provides the foundation for implementing various types of memory transfer operations
/// that can be queued and executed in a managed, coordinated fashion.
/// </remarks>
internal abstract class TransferOperation
{
    /// <summary>
    /// Gets or sets the unique identifier for this transfer operation.
    /// </summary>
    /// <value>The unique identifier for the operation.</value>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the priority of this transfer operation.
    /// </summary>
    /// <value>The priority level (0 = lowest, 10 = highest). Default is 5.</value>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Gets or sets the creation time of this operation.
    /// </summary>
    /// <value>The UTC timestamp when this operation was created.</value>
    public DateTimeOffset CreationTime { get; protected set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the expected size of the data to be transferred.
    /// </summary>
    /// <value>The expected transfer size in bytes, or -1 if unknown.</value>
    public long ExpectedSize { get; set; } = -1;

    /// <summary>
    /// Gets or sets the estimated duration of this operation.
    /// </summary>
    /// <value>The estimated time to complete the operation, or null if unknown.</value>
    public TimeSpan? EstimatedDuration { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this operation.
    /// </summary>
    /// <value>A dictionary of metadata key-value pairs.</value>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets a value indicating whether this operation can be retried if it fails.
    /// </summary>
    /// <value>True if the operation supports retry; otherwise, false.</value>
    public virtual bool SupportsRetry => true;

    /// <summary>
    /// Gets a value indicating whether this operation can be cancelled.
    /// </summary>
    /// <value>True if the operation supports cancellation; otherwise, false.</value>
    public virtual bool SupportsCancellation => true;

    /// <summary>
    /// Executes the transfer operation asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <exception cref="TransferException">Thrown when the transfer operation fails.</exception>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates that the operation can be executed with the current configuration.
    /// </summary>
    /// <returns>True if the operation is valid and can be executed; otherwise, false.</returns>
    public virtual bool Validate() => true;

    /// <summary>
    /// Estimates the resource requirements for this operation.
    /// </summary>
    /// <returns>The estimated resource requirements.</returns>
    public virtual TransferResourceRequirements EstimateResourceRequirements()
    {
        return new TransferResourceRequirements
        {
            ExpectedMemoryUsage = Math.Max(ExpectedSize, 0),
            ExpectedDuration = EstimatedDuration ?? TimeSpan.FromSeconds(1),
            RequiresExclusiveAccess = false
        };
    }

    /// <summary>
    /// Creates a string representation of this transfer operation.
    /// </summary>
    /// <returns>A string that represents this transfer operation.</returns>
    public override string ToString()
    {
        var sizeStr = ExpectedSize >= 0 ? $"{ExpectedSize / (1024.0 * 1024.0):F2} MB" : "Unknown size";
        var durationStr = EstimatedDuration?.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s" ?? "Unknown duration";

        return $"{GetType().Name} [ID: {Id:D}, Priority: {Priority}, Size: {sizeStr}, Duration: {durationStr}]";
    }
}

/// <summary>
/// Represents the resource requirements for a transfer operation.
/// </summary>
public class TransferResourceRequirements
{
    /// <summary>
    /// Gets or sets the expected memory usage in bytes.
    /// </summary>
    /// <value>The expected memory consumption during the operation.</value>
    public long ExpectedMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the expected duration of the operation.
    /// </summary>
    /// <value>The estimated time to complete the operation.</value>
    public TimeSpan ExpectedDuration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this operation requires exclusive access to resources.
    /// </summary>
    /// <value>True if the operation needs exclusive access; otherwise, false.</value>
    public bool RequiresExclusiveAccess { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of CPU cores recommended for this operation.
    /// </summary>
    /// <value>The recommended minimum CPU cores.</value>
    public int RecommendedCpuCores { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether GPU acceleration is beneficial for this operation.
    /// </summary>
    /// <value>True if GPU acceleration would improve performance; otherwise, false.</value>
    public bool BenefitsFromGpuAcceleration { get; set; }
}

/// <summary>
/// Exception thrown when a transfer operation fails.
/// </summary>
public class TransferException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransferException"/> class.
    /// </summary>
    public TransferException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TransferException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TransferException(string message, Exception innerException) : base(message, innerException)
    {
    }
}