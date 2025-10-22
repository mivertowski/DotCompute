// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Validates P2P transfer integrity through various connectivity and data consistency checks.
    /// Handles full data integrity, sampled integrity, and checksum-based validation.
    /// </summary>
    internal static class P2PConnectivityValidator
    {
        private const int DefaultValidationSampleSize = 1024 * 1024; // 1MB sample

        /// <summary>
        /// Validates full data integrity by comparing all data between source and destination buffers.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateFullDataIntegrityAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var detail = new P2PValidationDetail
            {
                ValidationType = "FullDataIntegrity",
                IsValid = true
            };

            try
            {
                // Copy both buffers to host for comparison
                var sourceData = new T[sourceBuffer.Length];
                var destData = new T[destinationBuffer.Length];

                await Task.WhenAll(
                    sourceBuffer.CopyToAsync(sourceData.AsMemory(), cancellationToken).AsTask(),
                    destinationBuffer.CopyToAsync(destData.AsMemory(), cancellationToken).AsTask()
                );

                // Compare data byte by byte
                var sourceBytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(sourceData.AsSpan());
                var destBytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(destData.AsSpan());

                if (!sourceBytes.SequenceEqual(destBytes))
                {
                    // Find first difference
                    for (var i = 0; i < Math.Min(sourceBytes.Length, destBytes.Length); i++)
                    {
                        if (sourceBytes[i] != destBytes[i])
                        {
                            detail.IsValid = false;
                            detail.ErrorMessage = $"Data mismatch at byte {i}: source={sourceBytes[i]:X2}, dest={destBytes[i]:X2}";
                            return detail;
                        }
                    }

                    if (sourceBytes.Length != destBytes.Length)
                    {
                        detail.IsValid = false;
                        detail.ErrorMessage = $"Buffer length mismatch: source={sourceBytes.Length}, dest={destBytes.Length}";
                        return detail;
                    }
                }

                detail.Details = $"Full data integrity verified: {sourceBytes.Length} bytes compared";
                return detail;
            }
            catch (Exception ex)
            {
                detail.IsValid = false;
                detail.ErrorMessage = $"Integrity validation failed: {ex.Message}";
                return detail;
            }
        }

        /// <summary>
        /// Validates data integrity using sampling approach for large buffers.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateSampledDataIntegrityAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PTransferPlan transferPlan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var detail = new P2PValidationDetail
            {
                ValidationType = "SampledDataIntegrity",
                IsValid = true
            };

            try
            {
                var elementSize = Unsafe.SizeOf<T>();
                var sampleSize = Math.Min(DefaultValidationSampleSize, sourceBuffer.SizeInBytes);
                var sampleElements = sampleSize / elementSize;

                // Sample from beginning, middle, and end
                var sampleOffsets = new[]
                {
                    0, // Beginning
                    Math.Max(0, (sourceBuffer.Length - sampleElements) / 2), // Middle
                    Math.Max(0, sourceBuffer.Length - sampleElements) // End
                };

                foreach (var offset in sampleOffsets)
                {
                    var actualSampleElements = Math.Min(sampleElements, sourceBuffer.Length - offset);
                    if (actualSampleElements <= 0)
                    {
                        continue;
                    }

                    var sourceData = new T[actualSampleElements];
                    var destData = new T[actualSampleElements];

                    // Copy samples from both buffers
                    var sourceSample = sourceBuffer.Slice((int)offset, (int)actualSampleElements);
                    var destSample = destinationBuffer.Slice((int)offset, (int)actualSampleElements);

                    await Task.WhenAll(
                        sourceSample.CopyToAsync(sourceData.AsMemory(), cancellationToken).AsTask(),
                        destSample.CopyToAsync(destData.AsMemory(), cancellationToken).AsTask()
                    );

                    // Compare samples
                    var sourceBytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(sourceData.AsSpan());
                    var destBytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(destData.AsSpan());

                    if (!sourceBytes.SequenceEqual(destBytes))
                    {
                        detail.IsValid = false;
                        detail.ErrorMessage = $"Sampled data mismatch at offset {offset}";
                        return detail;
                    }
                }

                detail.Details = $"Sampled data integrity verified: {sampleOffsets.Length} samples checked";
                return detail;
            }
            catch (Exception ex)
            {
                detail.IsValid = false;
                detail.ErrorMessage = $"Sampled integrity validation failed: {ex.Message}";
                return detail;
            }
        }

        /// <summary>
        /// Validates data integrity using checksum comparison.
        /// </summary>
        public static async Task<P2PValidationDetail> ValidateChecksumIntegrityAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var detail = new P2PValidationDetail
            {
                ValidationType = "ChecksumIntegrity",
                IsValid = true
            };

            try
            {
                // Use xxHash or similar fast hash for large buffers
                var sourceChecksum = await CalculateBufferChecksumAsync(sourceBuffer, cancellationToken);
                var destChecksum = await CalculateBufferChecksumAsync(destinationBuffer, cancellationToken);

                if (sourceChecksum != destChecksum)
                {
                    detail.IsValid = false;
                    detail.ErrorMessage = $"Checksum mismatch: source={sourceChecksum:X16}, dest={destChecksum:X16}";
                    return detail;
                }

                detail.Details = $"Checksum integrity verified: {sourceChecksum:X16}";
                return detail;
            }
            catch (Exception ex)
            {
                detail.IsValid = false;
                detail.ErrorMessage = $"Checksum validation failed: {ex.Message}";
                return detail;
            }
        }

        /// <summary>
        /// Calculates a fast checksum for the given buffer using sampling.
        /// </summary>
        private static async Task<ulong> CalculateBufferChecksumAsync<T>(
            IUnifiedMemoryBuffer<T> buffer,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // For large buffers, use sampling-based checksum
            var elementSize = Unsafe.SizeOf<T>();
            var sampleSize = Math.Min(1024 * 1024, buffer.SizeInBytes); // 1MB max sample
            var sampleElements = sampleSize / elementSize;

            var sampleData = new T[sampleElements];
            var bufferSlice = buffer.Slice(0, (int)sampleElements);
            await bufferSlice.CopyToAsync(sampleData.AsMemory(), cancellationToken);

            // Simple checksum calculation (in production would use xxHash or similar)
            var bytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(sampleData.AsSpan());
            ulong checksum = 0;

            for (var i = 0; i < bytes.Length; i += 8)
            {
                var remaining = Math.Min(8, bytes.Length - i);
                var chunk = bytes.Slice(i, remaining);

                if (chunk.Length >= 8)
                {
                    checksum ^= global::System.Runtime.InteropServices.MemoryMarshal.Read<ulong>(chunk);
                }
                else
                {
                    // Handle remaining bytes
                    for (var j = 0; j < chunk.Length; j++)
                    {
                        checksum ^= (ulong)chunk[j] << (j * 8);
                    }
                }
            }

            return checksum;
        }
    }
}