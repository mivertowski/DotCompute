// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Provides reusable attribute definitions for Ring Kernel tests.
/// </summary>
internal static class RingKernelTestAttributeDefinitions
{
    /// <summary>
    /// Complete attribute definitions for Ring Kernel tests including all enums and the attribute class.
    /// </summary>
    public const string AttributeDefinitions = """
        // Stub types for compilation
        namespace Microsoft.Extensions.Logging
        {
            public interface ILoggerFactory
            {
                ILogger CreateLogger(string categoryName);
            }
            public interface ILogger { }
        }

        namespace DotCompute.Abstractions.RingKernels
        {
            public interface IRingKernelRuntime
            {
                System.Threading.Tasks.Task LaunchAsync(string kernelId, int gridSize, int blockSize, System.Threading.CancellationToken ct);
                System.Threading.Tasks.Task ActivateAsync(string kernelId, System.Threading.CancellationToken ct);
                System.Threading.Tasks.Task DeactivateAsync(string kernelId, System.Threading.CancellationToken ct);
                System.Threading.Tasks.Task TerminateAsync(string kernelId, System.Threading.CancellationToken ct);
                System.Threading.Tasks.Task<RingKernelStatus> GetStatusAsync(string kernelId, System.Threading.CancellationToken ct);
                System.Threading.Tasks.Task<RingKernelMetrics> GetMetricsAsync(string kernelId, System.Threading.CancellationToken ct);
            }

            public class RingKernelStatus { }
            public class RingKernelMetrics { }
        }

        namespace DotCompute.Backends.CPU.RingKernels
        {
            public class CpuRingKernelRuntime : DotCompute.Abstractions.RingKernels.IRingKernelRuntime
            {
                public CpuRingKernelRuntime(Microsoft.Extensions.Logging.ILogger logger) { }
                public System.Threading.Tasks.Task LaunchAsync(string kernelId, int gridSize, int blockSize, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task ActivateAsync(string kernelId, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task DeactivateAsync(string kernelId, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task TerminateAsync(string kernelId, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task<DotCompute.Abstractions.RingKernels.RingKernelStatus> GetStatusAsync(string kernelId, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(new DotCompute.Abstractions.RingKernels.RingKernelStatus());
                public System.Threading.Tasks.Task<DotCompute.Abstractions.RingKernels.RingKernelMetrics> GetMetricsAsync(string kernelId, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(new DotCompute.Abstractions.RingKernels.RingKernelMetrics());
            }
        }

        namespace DotCompute.Abstractions.Enums
        {
            public enum RingKernelMode
            {
                Persistent,
                EventDriven
            }

            public enum MessagingStrategy
            {
                SharedMemory,
                AtomicQueue,
                P2P,
                NCCL
            }

            public enum ComputeDomain
            {
                General,
                VideoProcessing,
                AudioProcessing,
                MachineLearning,
                ImageProcessing,
                SignalProcessing,
                ScientificComputing,
                Financial,
                Cryptography,
                DataAnalytics
            }

            [System.Flags]
            public enum KernelBackends
            {
                CPU = 1,
                CUDA = 2,
                Metal = 4,
                OpenCL = 8,
                All = CPU | CUDA | Metal | OpenCL
            }
        }

        namespace DotCompute.Abstractions.Attributes
        {
            using DotCompute.Abstractions.Enums;

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class RingKernelAttribute : System.Attribute
            {
                public string? KernelId { get; set; }
                public int Capacity { get; set; } = 1024;
                public int InputQueueSize { get; set; } = 256;
                public int OutputQueueSize { get; set; } = 256;
                public RingKernelMode Mode { get; set; } = RingKernelMode.Persistent;
                public MessagingStrategy MessagingStrategy { get; set; } = MessagingStrategy.SharedMemory;
                public ComputeDomain Domain { get; set; } = ComputeDomain.General;
                public int[]? GridDimensions { get; set; }
                public int[]? BlockDimensions { get; set; }
                public bool UseSharedMemory { get; set; }
                public int SharedMemorySize { get; set; }
                public KernelBackends Backends { get; set; } = KernelBackends.CUDA | KernelBackends.OpenCL | KernelBackends.Metal;
                public bool IsParallel { get; set; } = true;
                public int VectorSize { get; set; } = 8;
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class KernelAttribute : System.Attribute
            {
                public string[]? Backends { get; set; }
                public bool IsParallel { get; set; } = true;
                public int VectorSize { get; set; } = 8;
            }
        }

        public static class Kernel
        {
            public static ThreadId ThreadId => new();
        }

        public struct ThreadId
        {
            public int X => 0;
            public int Y => 0;
            public int Z => 0;
        }
        """;

    /// <summary>
    /// Wraps source code with attribute definitions.
    /// </summary>
    public static string WithAttributeDefinitions(string sourceCode)
    {
        return sourceCode + "\n\n" + AttributeDefinitions;
    }
}
