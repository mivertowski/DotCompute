// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Exceptions;

/// <summary>
/// Base exception for all DotCompute errors.
/// </summary>
/// <remarks>
/// All DotCompute exceptions derive from this class, enabling catch-all handling
/// and consistent error reporting across the framework.
/// </remarks>
public class DotComputeException : Exception
{
    /// <summary>Gets the error code for categorization.</summary>
    public string ErrorCode { get; }

    /// <summary>Gets additional context about the error.</summary>
    public IReadOnlyDictionary<string, object>? Context { get; }

    /// <summary>Initializes a new DotComputeException.</summary>
    public DotComputeException()
        : this("An error occurred in DotCompute.") { }

    /// <summary>Initializes a new DotComputeException with a message.</summary>
    public DotComputeException(string message)
        : base(message)
    {
        ErrorCode = "DOTCOMPUTE_GENERAL";
    }

    /// <summary>Initializes a new DotComputeException with a message and inner exception.</summary>
    public DotComputeException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = "DOTCOMPUTE_GENERAL";
    }

    /// <summary>Initializes a new DotComputeException with full details.</summary>
    public DotComputeException(
        string message,
        string errorCode,
        IReadOnlyDictionary<string, object>? context = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Context = context;
    }
}

#region Device & Backend Exceptions

/// <summary>
/// Exception thrown when device/accelerator operations fail.
/// </summary>
public class DeviceException : DotComputeException
{
    /// <summary>Gets the device identifier if available.</summary>
    public string? DeviceId { get; }

    /// <summary>Gets the device type (CUDA, Metal, OpenCL, CPU).</summary>
    public string? DeviceType { get; }

    /// <summary>Initializes a new DeviceException.</summary>
    public DeviceException(string message)
        : base(message, "DEVICE_ERROR") { }

    /// <summary>Initializes a new DeviceException with device info.</summary>
    public DeviceException(string message, string? deviceId, string? deviceType)
        : base(message, "DEVICE_ERROR")
    {
        DeviceId = deviceId;
        DeviceType = deviceType;
    }

    /// <summary>Initializes a new DeviceException with inner exception.</summary>
    public DeviceException(string message, Exception innerException)
        : base(message, "DEVICE_ERROR", null, innerException) { }
}

/// <summary>
/// Exception thrown when a device is not available.
/// </summary>
public class DeviceUnavailableException : DeviceException
{
    /// <summary>Initializes a new DeviceUnavailableException.</summary>
    public DeviceUnavailableException(string message)
        : base(message) { }

    /// <summary>Initializes a new DeviceUnavailableException with device info.</summary>
    public DeviceUnavailableException(string message, string deviceType)
        : base(message, null, deviceType) { }
}

/// <summary>
/// Exception thrown when CPU fallback is required.
/// </summary>
public class CpuFallbackException : DeviceException
{
    /// <summary>Gets the original device that required fallback.</summary>
    public string OriginalDevice { get; }

    /// <summary>Gets the reason for fallback.</summary>
    public string Reason { get; }

    /// <summary>Initializes a new CpuFallbackException.</summary>
    public CpuFallbackException(string message, string originalDevice, string reason)
        : base(message)
    {
        OriginalDevice = originalDevice;
        Reason = reason;
    }
}

#endregion

#region Compilation Exceptions

/// <summary>
/// Exception thrown when kernel compilation fails.
/// </summary>
public class KernelCompilationException : DotComputeException
{
    /// <summary>Gets the kernel name if available.</summary>
    public string? KernelName { get; }

    /// <summary>Gets the source code that failed to compile.</summary>
    public string? SourceCode { get; }

    /// <summary>Gets compilation diagnostic messages.</summary>
    public IReadOnlyList<string>? Diagnostics { get; }

    /// <summary>Initializes a new KernelCompilationException.</summary>
    public KernelCompilationException(string message)
        : base(message, "COMPILATION_ERROR") { }

    /// <summary>Initializes a new KernelCompilationException with details.</summary>
    public KernelCompilationException(
        string message,
        string? kernelName,
        string? sourceCode = null,
        IReadOnlyList<string>? diagnostics = null)
        : base(message, "COMPILATION_ERROR")
    {
        KernelName = kernelName;
        SourceCode = sourceCode;
        Diagnostics = diagnostics;
    }

    /// <summary>Initializes a new KernelCompilationException with inner exception.</summary>
    public KernelCompilationException(string message, Exception innerException)
        : base(message, "COMPILATION_ERROR", null, innerException) { }
}

/// <summary>
/// Exception thrown when kernel validation fails.
/// </summary>
public class KernelValidationException : KernelCompilationException
{
    /// <summary>Gets the validation rule that failed.</summary>
    public string? ValidationRule { get; }

    /// <summary>Initializes a new KernelValidationException.</summary>
    public KernelValidationException(string message, string? kernelName = null, string? validationRule = null)
        : base(message, kernelName)
    {
        ValidationRule = validationRule;
    }
}

#endregion

#region Memory Exceptions

/// <summary>
/// Exception thrown when memory operations fail.
/// </summary>
public class MemoryOperationException : DotComputeException
{
    /// <summary>Gets the type of memory operation that failed.</summary>
    public MemoryOperationType OperationType { get; }

    /// <summary>Gets the size involved in the operation.</summary>
    public long? SizeBytes { get; }

    /// <summary>Initializes a new MemoryOperationException.</summary>
    public MemoryOperationException(string message, MemoryOperationType operationType)
        : base(message, "MEMORY_ERROR")
    {
        OperationType = operationType;
    }

    /// <summary>Initializes a new MemoryOperationException with size.</summary>
    public MemoryOperationException(string message, MemoryOperationType operationType, long sizeBytes)
        : base(message, "MEMORY_ERROR")
    {
        OperationType = operationType;
        SizeBytes = sizeBytes;
    }

    /// <summary>Initializes a new MemoryOperationException with inner exception.</summary>
    public MemoryOperationException(string message, MemoryOperationType operationType, Exception innerException)
        : base(message, "MEMORY_ERROR", null, innerException)
    {
        OperationType = operationType;
    }
}

/// <summary>
/// Exception thrown when memory allocation fails.
/// </summary>
public class MemoryAllocationException : MemoryOperationException
{
    /// <summary>Gets available memory at time of failure.</summary>
    public long? AvailableBytes { get; }

    /// <summary>Initializes a new MemoryAllocationException.</summary>
    public MemoryAllocationException(string message, long requestedBytes)
        : base(message, MemoryOperationType.Allocation, requestedBytes) { }

    /// <summary>Initializes a new MemoryAllocationException with availability info.</summary>
    public MemoryAllocationException(string message, long requestedBytes, long availableBytes)
        : base(message, MemoryOperationType.Allocation, requestedBytes)
    {
        AvailableBytes = availableBytes;
    }
}

/// <summary>
/// Exception thrown when buffer access is out of bounds.
/// </summary>
public class BufferAccessException : MemoryOperationException
{
    /// <summary>Gets the buffer length.</summary>
    public long BufferLength { get; }

    /// <summary>Gets the attempted access offset.</summary>
    public long AccessOffset { get; }

    /// <summary>Initializes a new BufferAccessException.</summary>
    public BufferAccessException(string message, long bufferLength, long accessOffset)
        : base(message, MemoryOperationType.Access)
    {
        BufferLength = bufferLength;
        AccessOffset = accessOffset;
    }
}

/// <summary>
/// Types of memory operations.
/// </summary>
public enum MemoryOperationType
{
    /// <summary>Memory allocation.</summary>
    Allocation,

    /// <summary>Memory deallocation.</summary>
    Deallocation,

    /// <summary>Memory copy.</summary>
    Copy,

    /// <summary>Buffer access.</summary>
    Access,

    /// <summary>Memory mapping.</summary>
    Mapping,

    /// <summary>P2P transfer.</summary>
    P2PTransfer
}

#endregion

#region Execution Exceptions

/// <summary>
/// Exception thrown when kernel execution fails.
/// </summary>
public class KernelExecutionException : DotComputeException
{
    /// <summary>Gets the kernel name.</summary>
    public string? KernelName { get; }

    /// <summary>Gets the execution stage where failure occurred.</summary>
    public ExecutionStage Stage { get; }

    /// <summary>Initializes a new KernelExecutionException.</summary>
    public KernelExecutionException(string message, string? kernelName = null)
        : base(message, "EXECUTION_ERROR")
    {
        KernelName = kernelName;
        Stage = ExecutionStage.Unknown;
    }

    /// <summary>Initializes a new KernelExecutionException with stage info.</summary>
    public KernelExecutionException(string message, string? kernelName, ExecutionStage stage)
        : base(message, "EXECUTION_ERROR")
    {
        KernelName = kernelName;
        Stage = stage;
    }

    /// <summary>Initializes a new KernelExecutionException with inner exception.</summary>
    public KernelExecutionException(string message, Exception innerException)
        : base(message, "EXECUTION_ERROR", null, innerException)
    {
        Stage = ExecutionStage.Unknown;
    }
}

/// <summary>
/// Exception thrown when execution times out.
/// </summary>
public class ExecutionTimeoutException : KernelExecutionException
{
    /// <summary>Gets the timeout duration.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Gets the elapsed time before timeout.</summary>
    public TimeSpan? Elapsed { get; }

    /// <summary>Initializes a new ExecutionTimeoutException.</summary>
    public ExecutionTimeoutException(string message, string? kernelName, TimeSpan timeout)
        : base(message, kernelName, ExecutionStage.Running)
    {
        Timeout = timeout;
    }
}

/// <summary>
/// Execution stages for error reporting.
/// </summary>
public enum ExecutionStage
{
    /// <summary>Unknown stage.</summary>
    Unknown,

    /// <summary>Preparation stage.</summary>
    Preparation,

    /// <summary>Launch stage.</summary>
    Launch,

    /// <summary>Running stage.</summary>
    Running,

    /// <summary>Synchronization stage.</summary>
    Synchronization,

    /// <summary>Cleanup stage.</summary>
    Cleanup
}

#endregion

#region Pipeline Exceptions

/// <summary>
/// Exception thrown when pipeline operations fail.
/// </summary>
public class PipelineException : DotComputeException
{
    /// <summary>Gets the pipeline name or ID.</summary>
    public string? PipelineId { get; }

    /// <summary>Gets the stage where failure occurred.</summary>
    public string? StageName { get; }

    /// <summary>Initializes a new PipelineException.</summary>
    public PipelineException(string message, string? pipelineId = null, string? stageName = null)
        : base(message, "PIPELINE_ERROR")
    {
        PipelineId = pipelineId;
        StageName = stageName;
    }

    /// <summary>Initializes a new PipelineException with inner exception.</summary>
    public PipelineException(string message, Exception innerException)
        : base(message, "PIPELINE_ERROR", null, innerException) { }
}

/// <summary>
/// Exception thrown when pipeline validation fails.
/// </summary>
public class PipelineValidationException : PipelineException
{
    /// <summary>Gets validation errors.</summary>
    public IReadOnlyList<string>? ValidationErrors { get; }

    /// <summary>Initializes a new PipelineValidationException.</summary>
    public PipelineValidationException(string message, IReadOnlyList<string>? errors = null)
        : base(message)
    {
        ValidationErrors = errors;
    }
}

#endregion

#region Configuration Exceptions

/// <summary>
/// Exception thrown when configuration is invalid.
/// </summary>
public class ConfigurationException : DotComputeException
{
    /// <summary>Gets the configuration key that is invalid.</summary>
    public string? ConfigurationKey { get; }

    /// <summary>Gets the invalid value.</summary>
    public object? InvalidValue { get; }

    /// <summary>Initializes a new ConfigurationException.</summary>
    public ConfigurationException(string message, string? configurationKey = null)
        : base(message, "CONFIGURATION_ERROR")
    {
        ConfigurationKey = configurationKey;
    }

    /// <summary>Initializes a new ConfigurationException with value info.</summary>
    public ConfigurationException(string message, string configurationKey, object invalidValue)
        : base(message, "CONFIGURATION_ERROR")
    {
        ConfigurationKey = configurationKey;
        InvalidValue = invalidValue;
    }
}

#endregion

#region Security Exceptions

/// <summary>
/// Exception thrown when security/authorization fails.
/// </summary>
public class SecurityException : DotComputeException
{
    /// <summary>Gets the principal that was denied.</summary>
    public string? Principal { get; }

    /// <summary>Gets the resource that was accessed.</summary>
    public string? Resource { get; }

    /// <summary>Gets the action that was attempted.</summary>
    public string? Action { get; }

    /// <summary>Initializes a new SecurityException.</summary>
    public SecurityException(string message)
        : base(message, "SECURITY_ERROR") { }

    /// <summary>Initializes a new SecurityException with access details.</summary>
    public SecurityException(string message, string? principal, string? resource, string? action)
        : base(message, "SECURITY_ERROR")
    {
        Principal = principal;
        Resource = resource;
        Action = action;
    }
}

/// <summary>
/// Exception thrown when quota is exceeded.
/// </summary>
public class QuotaExceededException : SecurityException
{
    /// <summary>Gets the quota limit.</summary>
    public long Limit { get; }

    /// <summary>Gets the current usage.</summary>
    public long CurrentUsage { get; }

    /// <summary>Gets the quota type.</summary>
    public string QuotaType { get; }

    /// <summary>Initializes a new QuotaExceededException.</summary>
    public QuotaExceededException(string message, string quotaType, long limit, long currentUsage)
        : base(message)
    {
        QuotaType = quotaType;
        Limit = limit;
        CurrentUsage = currentUsage;
    }
}

#endregion
