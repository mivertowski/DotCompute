// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Abstractions.Recovery
{
    /// <summary>
    /// Result information from a device reset operation.
    /// </summary>
    /// <remarks>
    /// Provides detailed information about reset execution, including:
    /// <list type="bullet">
    /// <item>Success status and error information</item>
    /// <item>Performance metrics (duration, operations cleared)</item>
    /// <item>State changes (memory freed, kernels cleared)</item>
    /// <item>Backend-specific diagnostics</item>
    /// </list>
    /// </remarks>
    public sealed class ResetResult
    {
        /// <summary>
        /// Gets the device identifier that was reset.
        /// </summary>
        public required string DeviceId { get; init; }

        /// <summary>
        /// Gets the device name.
        /// </summary>
        public required string DeviceName { get; init; }

        /// <summary>
        /// Gets the backend type (CUDA, Metal, OpenCL, CPU).
        /// </summary>
        public required string BackendType { get; init; }

        /// <summary>
        /// Gets whether the reset operation succeeded.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the type of reset that was performed.
        /// </summary>
        public required ResetType ResetType { get; init; }

        /// <summary>
        /// Gets the timestamp when reset was initiated.
        /// </summary>
        public required DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// Gets the duration of the reset operation.
        /// </summary>
        public required TimeSpan Duration { get; init; }

        /// <summary>
        /// Gets the error message if reset failed.
        /// </summary>
        /// <remarks>
        /// Null if <see cref="Success"/> is true.
        /// </remarks>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets the exception that caused reset failure, if any.
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Gets whether the device was reinitialized after reset.
        /// </summary>
        public bool WasReinitialized { get; init; }

        /// <summary>
        /// Gets the number of pending operations that were cancelled/completed.
        /// </summary>
        public long PendingOperationsCleared { get; init; }

        /// <summary>
        /// Gets the amount of memory freed during reset (in bytes).
        /// </summary>
        public long MemoryFreedBytes { get; init; }

        /// <summary>
        /// Gets the number of kernel compilations cleared from cache.
        /// </summary>
        public int KernelsCacheCleared { get; init; }

        /// <summary>
        /// Gets whether the memory pool was cleared.
        /// </summary>
        public bool MemoryPoolCleared { get; init; }

        /// <summary>
        /// Gets whether the kernel cache was cleared.
        /// </summary>
        public bool KernelCacheCleared { get; init; }

        /// <summary>
        /// Gets backend-specific diagnostic information.
        /// </summary>
        /// <remarks>
        /// May include:
        /// <list type="bullet">
        /// <item>CUDA: XID errors, ECC error counts, NVML diagnostics</item>
        /// <item>Metal: Command buffer statistics, shader compilation info</item>
        /// <item>OpenCL: Platform/device errors, queue flush results</item>
        /// <item>CPU: Thread pool statistics, SIMD state information</item>
        /// </list>
        /// </remarks>
        public IReadOnlyDictionary<string, string>? DiagnosticInfo { get; init; }

        /// <summary>
        /// Gets a human-readable status message describing the reset result.
        /// </summary>
        public string StatusMessage
        {
            get
            {
                return Success
                    ? $"{BackendType} reset completed: {ResetType} in {Duration.TotalMilliseconds:F2}ms" +
                      (MemoryFreedBytes > 0 ? $", {MemoryFreedBytes / (1024 * 1024):N0} MB freed" : "") +
                      (KernelsCacheCleared > 0 ? $", {KernelsCacheCleared} kernels cleared" : "")
                    : $"{BackendType} reset failed: {ErrorMessage ?? "Unknown error"}";
            }
        }

        /// <summary>
        /// Creates a successful reset result.
        /// </summary>
        public static ResetResult CreateSuccess(
            string deviceId,
            string deviceName,
            string backendType,
            ResetType resetType,
            DateTimeOffset timestamp,
            TimeSpan duration,
            bool wasReinitialized,
            long pendingOperationsCleared = 0,
            long memoryFreedBytes = 0,
            int kernelsCacheCleared = 0,
            bool memoryPoolCleared = false,
            bool kernelCacheCleared = false,
            IReadOnlyDictionary<string, string>? diagnosticInfo = null)
        {
            return new ResetResult
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                BackendType = backendType,
                Success = true,
                ResetType = resetType,
                Timestamp = timestamp,
                Duration = duration,
                WasReinitialized = wasReinitialized,
                PendingOperationsCleared = pendingOperationsCleared,
                MemoryFreedBytes = memoryFreedBytes,
                KernelsCacheCleared = kernelsCacheCleared,
                MemoryPoolCleared = memoryPoolCleared,
                KernelCacheCleared = kernelCacheCleared,
                DiagnosticInfo = diagnosticInfo
            };
        }

        /// <summary>
        /// Creates a failed reset result.
        /// </summary>
        public static ResetResult CreateFailure(
            string deviceId,
            string deviceName,
            string backendType,
            ResetType resetType,
            DateTimeOffset timestamp,
            TimeSpan duration,
            string errorMessage,
            Exception? exception = null,
            IReadOnlyDictionary<string, string>? diagnosticInfo = null)
        {
            return new ResetResult
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                BackendType = backendType,
                Success = false,
                ResetType = resetType,
                Timestamp = timestamp,
                Duration = duration,
                ErrorMessage = errorMessage,
                Exception = exception,
                WasReinitialized = false,
                DiagnosticInfo = diagnosticInfo
            };
        }

        /// <summary>
        /// Returns a string representation of the reset result.
        /// </summary>
        public override string ToString() => StatusMessage;
    }
}
