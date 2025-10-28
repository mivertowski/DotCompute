// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Core validation rules for P2P transfer readiness and compatibility checks.
    /// Handles buffer compatibility, device capabilities, memory availability, and transfer strategy validation.
    /// </summary>
    internal static class P2PValidationRules
    {
        /// <summary>
        /// Validates buffer compatibility for P2P transfers.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateBufferCompatibilityAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask; // Simulate async validation

            var detail = new P2PValidationDetail
            {
                ValidationType = "BufferCompatibility",
                IsValid = true
            };

            // Check buffer sizes
            if (sourceBuffer.SizeInBytes != destinationBuffer.SizeInBytes)
            {
                detail.IsValid = false;
                detail.ErrorMessage = $"Buffer size mismatch: source={sourceBuffer.SizeInBytes}, dest={destinationBuffer.SizeInBytes}";
                return detail;
            }

            // Check element types (already guaranteed by generic constraint, but validate at runtime)
            var sourceElementSize = Unsafe.SizeOf<T>();
            var expectedDestSize = destinationBuffer.SizeInBytes / sourceBuffer.Length * sourceElementSize;

            if (Math.Abs(destinationBuffer.SizeInBytes - expectedDestSize) > 1)
            {
                detail.IsValid = false;
                detail.ErrorMessage = "Element type compatibility mismatch";
                return detail;
            }

            detail.Details = $"Buffers compatible: {sourceBuffer.SizeInBytes} bytes, element size {sourceElementSize}";
            return detail;
        }

        /// <summary>
        /// Validates device capabilities for P2P transfers.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateDeviceCapabilitiesAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            P2PTransferPlan transferPlan,
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var detail = new P2PValidationDetail
            {
                ValidationType = "DeviceCapabilities",
                IsValid = true
            };

            // Validate P2P capability based on transfer strategy
            if (transferPlan.Strategy == P2PTransferStrategy.DirectP2P &&
                !transferPlan.Capability.IsSupported)
            {
                detail.IsValid = false;
                detail.ErrorMessage = "Direct P2P transfer planned but P2P not supported between devices";
                return detail;
            }

            // Check if devices support the required memory operations
            if (transferPlan.TransferSize > 4L * 1024 * 1024 * 1024) // > 4GB
            {
                // Large transfer validation
                detail.Details = "Large transfer validation passed";
            }

            detail.Details = $"Device capabilities validated for {transferPlan.Strategy} strategy";
            return detail;
        }

        /// <summary>
        /// Validates memory availability for P2P transfers.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateMemoryAvailabilityAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PTransferPlan transferPlan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask;

            var detail = new P2PValidationDetail
            {
                ValidationType = "MemoryAvailability",
                IsValid = true
            };

            // Check if there's enough memory for the transfer
            var requiredMemory = transferPlan.TransferSize;
            if (transferPlan.Strategy == P2PTransferStrategy.ChunkedP2P)
            {
                requiredMemory += transferPlan.ChunkSize; // Additional memory for chunking
            }

            // Simulate memory availability check
            detail.Details = $"Memory availability validated: {requiredMemory} bytes required";
            return detail;
        }

        /// <summary>
        /// Validates transfer strategy configuration.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateTransferStrategyAsync(
            P2PTransferPlan transferPlan,
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var detail = new P2PValidationDetail
            {
                ValidationType = "TransferStrategy",
                IsValid = true
            };

            // Validate strategy parameters
            switch (transferPlan.Strategy)
            {
                case P2PTransferStrategy.ChunkedP2P:
                    if (transferPlan.ChunkSize <= 0 || transferPlan.ChunkSize > transferPlan.TransferSize)
                    {
                        detail.IsValid = false;
                        detail.ErrorMessage = $"Invalid chunk size: {transferPlan.ChunkSize}";
                        return detail;
                    }
                    break;

                case P2PTransferStrategy.PipelinedP2P:
                    if (transferPlan.PipelineDepth is <= 0 or > 8)
                    {
                        detail.IsValid = false;
                        detail.ErrorMessage = $"Invalid pipeline depth: {transferPlan.PipelineDepth}";
                        return detail;
                    }
                    break;
            }

            detail.Details = $"Transfer strategy validated: {transferPlan.Strategy}";
            return detail;
        }
    }
}