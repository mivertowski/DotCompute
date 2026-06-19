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
            public interface ILogger<out TCategoryName> : ILogger { }
            // Mirrors the real LoggerFactoryExtensions.CreateLogger<T>() so generated
            // runtime-factory code (loggerFactory?.CreateLogger<T>()) compiles.
            public static class LoggerFactoryExtensions
            {
                public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory) => null!;
            }
        }

        namespace DotCompute.Abstractions.RingKernels
        {
            // Mirror of the production RingKernelLaunchOptions surface used by generated wrappers.
            public sealed class RingKernelLaunchOptions
            {
                public int QueueCapacity { get; set; } = 1024;
                public static RingKernelLaunchOptions ProductionDefaults() => new RingKernelLaunchOptions();
            }

            // Mirror of the production RingKernelTelemetry returned by GetTelemetryAsync.
            public struct RingKernelTelemetry
            {
                public ulong MessagesProcessed { get; set; }
                public ulong MessagesDropped { get; set; }
                public uint QueueDepth { get; set; }
            }

            public interface IRingKernelRuntime : System.IAsyncDisposable
            {
                System.Threading.Tasks.Task LaunchAsync(string kernelId, int gridSize, int blockSize, RingKernelLaunchOptions? options = null, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task ActivateAsync(string kernelId, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task DeactivateAsync(string kernelId, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task TerminateAsync(string kernelId, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task<RingKernelStatus> GetStatusAsync(string kernelId, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task<RingKernelMetrics> GetMetricsAsync(string kernelId, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task<RingKernelTelemetry> GetTelemetryAsync(string kernelId, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task SetTelemetryEnabledAsync(string kernelId, bool enabled, System.Threading.CancellationToken ct = default);
                System.Threading.Tasks.Task ResetTelemetryAsync(string kernelId, System.Threading.CancellationToken ct = default);
            }

            public class RingKernelStatus { }
            public class RingKernelMetrics { }
        }

        namespace DotCompute.Backends.CPU.RingKernels
        {
            public class CpuRingKernelRuntime : DotCompute.Abstractions.RingKernels.IRingKernelRuntime
            {
                public CpuRingKernelRuntime(Microsoft.Extensions.Logging.ILogger logger) { }
                public CpuRingKernelRuntime(Microsoft.Extensions.Logging.ILogger<CpuRingKernelRuntime> logger) { }
                public System.Threading.Tasks.Task LaunchAsync(string kernelId, int gridSize, int blockSize, DotCompute.Abstractions.RingKernels.RingKernelLaunchOptions? options = null, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task ActivateAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task DeactivateAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task TerminateAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task<DotCompute.Abstractions.RingKernels.RingKernelStatus> GetStatusAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new DotCompute.Abstractions.RingKernels.RingKernelStatus());
                public System.Threading.Tasks.Task<DotCompute.Abstractions.RingKernels.RingKernelMetrics> GetMetricsAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new DotCompute.Abstractions.RingKernels.RingKernelMetrics());
                public System.Threading.Tasks.Task<DotCompute.Abstractions.RingKernels.RingKernelTelemetry> GetTelemetryAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(default(DotCompute.Abstractions.RingKernels.RingKernelTelemetry));
                public System.Threading.Tasks.Task SetTelemetryEnabledAsync(string kernelId, bool enabled, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task ResetTelemetryAsync(string kernelId, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
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
                public int MaxInputMessageSizeBytes { get; set; } = 65792;
                public int MaxOutputMessageSizeBytes { get; set; } = 65792;
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
