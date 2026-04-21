// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Globalization;

namespace DotCompute.Backends.CUDA.Hopper
{
    /// <summary>
    /// Dimensionality of a TMA transfer. Hopper's Tensor Memory Accelerator supports 1D, 2D and 3D
    /// bulk copies; multi-dimensional transfers require a <c>CUtensorMap</c> descriptor, while
    /// 1D transfers can issue <c>cp.async.bulk.shared::cluster.global</c> directly.
    /// </summary>
    public enum TmaDimensionality
    {
        /// <summary>Unspecified / default — invalid for a validated config.</summary>
        None = 0,

        /// <summary>1D bulk copy (no tensor map needed).</summary>
        OneD = 1,

        /// <summary>2D bulk copy (requires tensor map descriptor).</summary>
        TwoD = 2,

        /// <summary>3D bulk copy (requires tensor map descriptor).</summary>
        ThreeD = 3,
    }

    /// <summary>
    /// Configuration for a Hopper Tensor Memory Accelerator (<c>cp.async.bulk</c>) transfer.
    /// </summary>
    /// <remarks>
    /// Mirrors RustCompute's <c>tma.rs</c>. The transfer size is capped by PTX spec at 128 KiB and
    /// must be 16-byte aligned (the TMA engine moves 16-byte sectors). Pipeline depth controls how
    /// many mbarriers we pre-allocate so producers and consumers can overlap.
    /// </remarks>
    public readonly record struct TmaConfig
    {
        /// <summary>Maximum number of bytes per TMA operation. Must be in [16, 131072] and a multiple of 16.</summary>
        public uint MaxTransferBytes { get; init; }

        /// <summary>Number of concurrent TMA operations to pipeline. Must be in [1, 8].</summary>
        public uint PipelineDepth { get; init; }

        /// <summary>Dimensionality of the transfer.</summary>
        public TmaDimensionality Dimensionality { get; init; }

        /// <summary>Box dimension X (elements). Must be positive.</summary>
        public uint BoxDimX { get; init; }

        /// <summary>Box dimension Y (elements). Must be positive when <see cref="Dimensionality"/> is 2D or 3D.</summary>
        public uint BoxDimY { get; init; }

        /// <summary>Box dimension Z (elements). Must be positive when <see cref="Dimensionality"/> is 3D.</summary>
        public uint BoxDimZ { get; init; }

        /// <summary>Whether to multicast the transfer to all blocks in the cluster.</summary>
        public bool Multicast { get; init; }

        /// <summary>PTX / hardware lower bound on transfer size — TMA moves 16-byte sectors.</summary>
        public const uint MinTransferBytes = 16;

        /// <summary>PTX / hardware upper bound on transfer size (128 KiB).</summary>
        public const uint MaxTransferBytesCap = 128u * 1024u;

        /// <summary>Minimum supported pipeline depth.</summary>
        public const uint MinPipelineDepth = 1;

        /// <summary>Maximum supported pipeline depth.</summary>
        public const uint MaxPipelineDepth = 8;

        /// <summary>TMA alignment (bytes) — transfers must be sized to this multiple.</summary>
        public const uint AlignmentBytes = 16;

        /// <summary>
        /// Convenience factory for a 1D TMA configuration.
        /// </summary>
        /// <param name="maxTransferBytes">Transfer size per operation. Must be 16-byte aligned and within [16, 131072].</param>
        /// <param name="pipelineDepth">Number of pipeline stages. Default 2.</param>
        /// <param name="multicast">Enable DSMEM multicast. Default false.</param>
        /// <returns>A 1D TMA configuration.</returns>
        public static TmaConfig CreateOneD(uint maxTransferBytes, uint pipelineDepth = 2, bool multicast = false)
            => new()
            {
                MaxTransferBytes = maxTransferBytes,
                PipelineDepth = pipelineDepth,
                Dimensionality = TmaDimensionality.OneD,
                BoxDimX = maxTransferBytes / AlignmentBytes, // element count fallback — callers normally override.
                BoxDimY = 1,
                BoxDimZ = 1,
                Multicast = multicast,
            };

        /// <summary>
        /// Convenience factory for a 2D TMA configuration.
        /// </summary>
        public static TmaConfig CreateTwoD(uint maxTransferBytes, uint boxX, uint boxY, uint pipelineDepth = 2)
            => new()
            {
                MaxTransferBytes = maxTransferBytes,
                PipelineDepth = pipelineDepth,
                Dimensionality = TmaDimensionality.TwoD,
                BoxDimX = boxX,
                BoxDimY = boxY,
                BoxDimZ = 1,
                Multicast = false,
            };

        /// <summary>
        /// Convenience factory for a 3D TMA configuration.
        /// </summary>
        public static TmaConfig CreateThreeD(uint maxTransferBytes, uint boxX, uint boxY, uint boxZ, uint pipelineDepth = 2)
            => new()
            {
                MaxTransferBytes = maxTransferBytes,
                PipelineDepth = pipelineDepth,
                Dimensionality = TmaDimensionality.ThreeD,
                BoxDimX = boxX,
                BoxDimY = boxY,
                BoxDimZ = boxZ,
                Multicast = false,
            };

        /// <summary>
        /// Validates the TMA configuration. Throws <see cref="ArgumentException"/> with a descriptive
        /// message on the first violated invariant.
        /// </summary>
        /// <exception cref="ArgumentException">One or more invariants are violated.</exception>
        public void Validate()
        {
            if (MaxTransferBytes < MinTransferBytes)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "MaxTransferBytes ({0}) must be at least {1} bytes.",
                        MaxTransferBytes, MinTransferBytes));
            }

            if (MaxTransferBytes > MaxTransferBytesCap)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "MaxTransferBytes ({0}) exceeds the TMA hardware cap of {1} bytes (128 KiB).",
                        MaxTransferBytes, MaxTransferBytesCap));
            }

            if (MaxTransferBytes % AlignmentBytes != 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "MaxTransferBytes ({0}) must be a multiple of {1} bytes (TMA 16-byte alignment).",
                        MaxTransferBytes, AlignmentBytes));
            }

            if (PipelineDepth < MinPipelineDepth || PipelineDepth > MaxPipelineDepth)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "PipelineDepth ({0}) must be in [{1}, {2}].",
                        PipelineDepth, MinPipelineDepth, MaxPipelineDepth));
            }

            if (Dimensionality == TmaDimensionality.None)
            {
                throw new ArgumentException("Dimensionality must be set (OneD/TwoD/ThreeD).");
            }

            if (BoxDimX == 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "BoxDimX ({0}) must be positive.", BoxDimX));
            }

            if (Dimensionality >= TmaDimensionality.TwoD && BoxDimY == 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "BoxDimY ({0}) must be positive for {1} transfers.",
                        BoxDimY, Dimensionality));
            }

            if (Dimensionality == TmaDimensionality.ThreeD && BoxDimZ == 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "BoxDimZ ({0}) must be positive for 3D transfers.", BoxDimZ));
            }
        }
    }

    /// <summary>
    /// Shared-memory layout for TMA staging buffers. Mirrors RustCompute's <c>TmaSmemLayout</c>.
    /// </summary>
    public readonly record struct TmaSmemLayout
    {
        /// <summary>Offset (bytes) in shared memory where the mbarrier slots start.</summary>
        public uint BarrierOffset { get; init; }

        /// <summary>Offset (bytes) in shared memory where the payload area starts (aligned).</summary>
        public uint PayloadOffset { get; init; }

        /// <summary>Total shared memory required (bytes) for both barriers and payload.</summary>
        public uint TotalBytes { get; init; }

        /// <summary>Size of the payload area (bytes) — <c>MaxTransferBytes * PipelineDepth</c>.</summary>
        public uint PayloadBytes { get; init; }

        /// <summary>Number of mbarrier slots (equals <see cref="TmaConfig.PipelineDepth"/>).</summary>
        public uint BarrierCount { get; init; }
    }

    /// <summary>
    /// Static helpers for computing TMA shared-memory layouts and generating illustrative PTX.
    /// </summary>
    public static class TmaLayout
    {
        /// <summary>
        /// mbarrier objects are 8 bytes and must be 8-byte aligned in shared memory.
        /// </summary>
        public const uint MbarrierSizeBytes = 8;

        /// <summary>
        /// Payload alignment (bytes). Matches TMA's 16-byte hardware sector.
        /// </summary>
        public const uint PayloadAlignmentBytes = 16;

        /// <summary>
        /// Computes the shared-memory layout for a TMA staging region.
        /// </summary>
        /// <remarks>
        /// Layout: <c>[ mbarrier[PipelineDepth] ][ payload[MaxTransferBytes * PipelineDepth] ]</c>.
        /// The barrier slots come first (simpler alignment), followed by the payload aligned to 16 bytes.
        /// </remarks>
        /// <param name="config">Validated TMA configuration.</param>
        /// <returns>The computed layout.</returns>
        public static TmaSmemLayout ComputeSmemLayout(TmaConfig config)
        {
            config.Validate();

            var barrierOffset = 0u;
            var barrierBytes = config.PipelineDepth * MbarrierSizeBytes;

            // Align payload to 16 bytes (TMA requirement and matches payload alignment).
            var payloadOffset = AlignUp(barrierOffset + barrierBytes, PayloadAlignmentBytes);
            var payloadBytes = config.MaxTransferBytes * config.PipelineDepth;
            var total = payloadOffset + payloadBytes;

            return new TmaSmemLayout
            {
                BarrierOffset = barrierOffset,
                BarrierCount = config.PipelineDepth,
                PayloadOffset = payloadOffset,
                PayloadBytes = payloadBytes,
                TotalBytes = total,
            };
        }

        /// <summary>
        /// Generates a commented PTX snippet illustrating a 1D TMA bulk async copy paired with
        /// an mbarrier. This is documentation/reference output — it is not injected into the JIT
        /// pipeline. Use it in generated kernel source or inspect it for debugging.
        /// </summary>
        /// <param name="config">Validated TMA configuration.</param>
        /// <returns>A multi-line PTX snippet string.</returns>
        public static string GeneratePtxSnippet(TmaConfig config)
        {
            config.Validate();

            var multicastNote = config.Multicast
                ? "// Multicast path: cp.async.bulk.shared::cluster.global.multicast.mbarrier::complete_tx::bytes"
                : "// Unicast path:  cp.async.bulk.shared::cluster.global.mbarrier::complete_tx::bytes";

            return string.Format(
                CultureInfo.InvariantCulture,
                "// TMA 1D bulk async copy (Hopper sm_90+)\n"
                + "// Transfer size: {0} bytes, pipeline depth: {1}\n"
                + "{2}\n"
                + "\n"
                + "// Thread 0 initialises the mbarrier once per pipeline stage:\n"
                + "//   mbarrier.init.shared::cta.b64 [barrier_addr], 1;\n"
                + "\n"
                + "// Thread 0 issues the bulk copy and signals the barrier on completion:\n"
                + "//   cp.async.bulk.shared::cluster.global.mbarrier::complete_tx::bytes\n"
                + "//       [dst_smem], [src_global], {0}, [barrier_addr];\n"
                + "\n"
                + "// All threads wait for completion on the barrier:\n"
                + "//   mbarrier.try_wait.parity.shared::cta.b64 wait_done, [barrier_addr], phase;\n"
                + "\n"
                + "// Dimensionality: {3} (box={4}x{5}x{6})\n",
                config.MaxTransferBytes,
                config.PipelineDepth,
                multicastNote,
                config.Dimensionality,
                config.BoxDimX,
                config.BoxDimY,
                config.BoxDimZ);
        }

        private static uint AlignUp(uint value, uint alignment)
        {
            if (alignment == 0 || (alignment & (alignment - 1)) != 0)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Alignment ({0}) must be a positive power of 2.", alignment),
                    nameof(alignment));
            }

            return (value + (alignment - 1)) & ~(alignment - 1);
        }
    }
}
