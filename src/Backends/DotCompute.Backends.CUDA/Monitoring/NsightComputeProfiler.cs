// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// Wrapper for NVIDIA Nsight Compute CLI profiler for automated kernel profiling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This profiler uses the NVIDIA Nsight Compute command-line tool (ncu) to collect
    /// detailed performance metrics for CUDA kernels including warp divergence, memory
    /// coalescing, instruction throughput, and occupancy statistics.
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong>
    /// <list type="bullet">
    /// <item><description>NVIDIA Nsight Compute 2024.3+</description></item>
    /// <item><description>GPU Performance Counter permissions (see https://developer.nvidia.com/ERR_NVGPUCTRPERM)</description></item>
    /// <item><description>CUDA-capable GPU with Compute Capability 5.0+</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> On Linux, you may need to run as root or configure permissions:
    /// <code>
    /// sudo sh -c 'echo "options nvidia NVreg_RestrictProfilingToAdminUsers=0" > /etc/modprobe.d/nvidia-profiling.conf'
    /// sudo update-initramfs -u
    /// </code>
    /// Then reboot.
    /// </para>
    /// </remarks>
    public sealed partial class NsightComputeProfiler(ILogger logger) : IDisposable
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6866,
            Level = LogLevel.Warning,
            Message = "Nsight Compute profiling failed")]
        private static partial void LogProfilingFailure(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 6867,
            Level = LogLevel.Information,
            Message = "Nsight Compute profiling completed successfully")]
        private static partial void LogProfilingSuccess(ILogger logger);

        #endregion

        private const string DEFAULT_NCU_PATH = "/opt/nvidia/nsight-compute/2025.3.0/ncu";
        private const string FALLBACK_NCU_PATH = "/usr/local/cuda/bin/ncu";

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private bool _initialized;
        private bool _disposed;
        private string? _ncuPath;

        /// <summary>
        /// Initializes the Nsight Compute profiler and verifies installation.
        /// </summary>
        /// <returns>true if initialization succeeded; otherwise, false.</returns>
        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            try
            {
                // Try to find ncu in common locations
                var possiblePaths = new[]
                {
                    DEFAULT_NCU_PATH,
                    FALLBACK_NCU_PATH,
                    "/usr/local/cuda-13.0/nsight-compute-2025.3.0/ncu",
                    "/usr/local/cuda-12.0/nsight-compute-2024.3.2/ncu"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        _ncuPath = path;
                        break;
                    }
                }

                if (_ncuPath == null)
                {
                    // Try system PATH
                    var which = ExecuteCommand("which", "ncu");
                    if (which.ExitCode == 0 && !string.IsNullOrWhiteSpace(which.Output))
                    {
                        _ncuPath = which.Output.Trim();
                    }
                }

                if (_ncuPath == null)
                {
                    _logger.LogWarningMessage("Nsight Compute (ncu) not found. Profiling will not be available.");
                    return false;
                }

                // Verify ncu works
                var version = ExecuteCommand(_ncuPath, "--version");
                if (version.ExitCode != 0)
                {
                    _logger.LogWarningMessage($"Nsight Compute found at {_ncuPath} but failed to execute: {version.Error}");
                    return false;
                }

                _initialized = true;
                _logger.LogInfoMessage($"Nsight Compute initialized successfully: {version.Output.Split('\n').FirstOrDefault()?.Trim()}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error initializing Nsight Compute profiler");
                return false;
            }
        }

        /// <summary>
        /// Profiles a CUDA kernel execution with specified metrics.
        /// </summary>
        /// <param name="executablePath">Path to the executable containing the kernel.</param>
        /// <param name="kernelName">Name of the kernel to profile (can be a regex pattern).</param>
        /// <param name="metrics">Optional array of metrics to collect. If null, uses default set.</param>
        /// <param name="outputPath">Optional path to save profiling report. If null, uses temporary file.</param>
        /// <returns>Profiling results if successful; otherwise, null.</returns>
        public NsightComputeResult? ProfileKernel(
            string executablePath,
            string? kernelName = null,
            string[]? metrics = null,
            string? outputPath = null)
        {
            if (!_initialized)
            {
                if (!Initialize())
                {
                    return null;
                }
            }

            try
            {
                var tempOutput = outputPath ?? Path.Combine(Path.GetTempPath(), $"ncu_profile_{Guid.NewGuid():N}.csv");
                var args = new StringBuilder();

                // Output format: CSV for easy parsing
                args.Append(CultureInfo.InvariantCulture, $"--csv --log-file \"{tempOutput}\" ");

                // Target specific kernel if specified
                if (!string.IsNullOrEmpty(kernelName))
                {
                    args.Append(CultureInfo.InvariantCulture, $"--kernel-name \"{kernelName}\" ");
                }

                // Specify metrics
                if (metrics != null && metrics.Length > 0)
                {
                    args.Append(CultureInfo.InvariantCulture, $"--metrics {string.Join(",", metrics)} ");
                }
                else
                {
                    // Default metrics for warp divergence and memory coalescing analysis
                    args.Append("--set full ");
                }

                // Target executable
                args.Append(CultureInfo.InvariantCulture, $"\"{executablePath}\"");

                _logger.LogInfoMessage($"Starting Nsight Compute profiling: {_ncuPath} {args}");

                var result = ExecuteCommand(_ncuPath!, args.ToString(), timeoutMs: 60000);

                if (result.ExitCode != 0)
                {
                    // Check for permission error
                    if (result.Error.Contains("ERR_NVGPUCTRPERM", StringComparison.Ordinal))
                    {
                        _logger.LogWarningMessage(
                            "GPU Performance Counter permission denied. " +
                            "See https://developer.nvidia.com/ERR_NVGPUCTRPERM for instructions.");
                    }
                    else
                    {
                        _logger.LogWarningMessage($"Nsight Compute profiling failed: {result.Error}");
                    }
                    return null;
                }

                LogProfilingSuccess(_logger);

                // Parse CSV output
                var parsedResult = ParseCsvOutput(tempOutput);

                // Clean up temp file if we created it
                if (outputPath == null && File.Exists(tempOutput))
                {
                    try
                    {
                        File.Delete(tempOutput);
                    }
                    catch
                    {
                        /* Ignore cleanup errors */
                    }
                }

                return parsedResult;
            }
            catch (Exception ex)
            {
                LogProfilingFailure(_logger, ex);
                return null;
            }
        }

        /// <summary>
        /// Gets available metrics that can be profiled.
        /// </summary>
        /// <param name="deviceId">GPU device ID (default: 0).</param>
        /// <returns>List of available metric names, or empty list if query fails.</returns>
        public IReadOnlyList<string> GetAvailableMetrics(int deviceId = 0)
        {
            if (!_initialized)
            {
                if (!Initialize())
                {
                    return [];
                }
            }

            try
            {
                var result = ExecuteCommand(_ncuPath!, $"--query-metrics --device {deviceId}");

                if (result.ExitCode != 0)
                {
                    if (result.Error.Contains("ERR_NVGPUCTRPERM", StringComparison.Ordinal))
                    {
                        _logger.LogWarningMessage("GPU Performance Counter permissions required to query metrics.");
                    }
                    return [];
                }

                // Parse metric names from output (format varies by version)
                return result.Output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !line.StartsWith("Device", StringComparison.Ordinal) &&
                                   !line.StartsWith("==", StringComparison.Ordinal) &&
                                   !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error querying available metrics");
                return [];
            }
        }

        private NsightComputeResult? ParseCsvOutput(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                return null;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length < 2) // Need header + at least one data line
                {
                    return null;
                }

                var headers = lines[0].Split(',').Select(h => h.Trim('\"')).ToArray();
                var values = lines[1].Split(',').Select(v => v.Trim('\"')).ToArray();

                var metrics = new Dictionary<string, string>();
                for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
                {
                    metrics[headers[i]] = values[i];
                }

                return new NsightComputeResult
                {
                    KernelName = metrics.GetValueOrDefault("Kernel Name", "Unknown"),
                    Metrics = metrics,
                    WarpDivergence = ParseMetricValue(metrics, "Branch Efficiency"),
                    MemoryCoalescingEfficiency = ParseMetricValue(metrics, "Global Memory Load Efficiency"),
                    Occupancy = ParseMetricValue(metrics, "Achieved Occupancy"),
                    InstructionThroughput = ParseMetricValue(metrics, "Instruction Throughput"),
                };
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error parsing CSV output from {csvPath}");
                return null;
            }
        }

        private static double? ParseMetricValue(Dictionary<string, string> metrics, string metricName)
        {
            if (!metrics.TryGetValue(metricName, out var value))
            {
                return null;
            }

            // Handle percentage values (e.g., "95.5%")
            value = value.TrimEnd('%', ' ');

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return null;
        }

        private static CommandResult ExecuteCommand(string command, string arguments, int timeoutMs = 30000)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    error.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore kill errors
                }
                return new CommandResult
                {
                    ExitCode = -1,
                    Output = output.ToString(),
                    Error = "Command timed out"
                };
            }

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                Output = output.ToString(),
                Error = error.ToString()
            };
        }

        /// <summary>
        /// Disposes resources used by the profiler.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private record CommandResult
        {
            public int ExitCode { get; init; }
            public string Output { get; init; } = string.Empty;
            public string Error { get; init; } = string.Empty;
        }
    }

    /// <summary>
    /// Results from an Nsight Compute profiling session.
    /// </summary>
    public sealed class NsightComputeResult
    {
        /// <summary>
        /// Gets or sets the name of the profiled kernel.
        /// </summary>
        public string KernelName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets all collected metrics as key-value pairs.
        /// </summary>
        public Dictionary<string, string> Metrics { get; init; } = [];

        /// <summary>
        /// Gets or sets the warp divergence metric (branch efficiency %).
        /// </summary>
        public double? WarpDivergence { get; init; }

        /// <summary>
        /// Gets or sets the memory coalescing efficiency (%).
        /// </summary>
        public double? MemoryCoalescingEfficiency { get; init; }

        /// <summary>
        /// Gets or sets the achieved occupancy (%).
        /// </summary>
        public double? Occupancy { get; init; }

        /// <summary>
        /// Gets or sets the instruction throughput.
        /// </summary>
        public double? InstructionThroughput { get; init; }

        /// <summary>
        /// Converts the profiling results to a KernelMetrics instance for compatibility with CUPTI.
        /// </summary>
        /// <returns>A KernelMetrics instance containing the profiling data.</returns>
        public KernelMetrics ToKernelMetrics()
        {
            var kernelMetrics = new KernelMetrics
            {
                KernelExecutions = 1
            };

            foreach (var (key, value) in Metrics)
            {
                if (double.TryParse(value.TrimEnd('%', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
                {
                    // Map Nsight Compute metrics to CUPTI metric names
                    var metricName = key.ToUpperInvariant().Replace(" ", "_", StringComparison.Ordinal);
                    kernelMetrics.MetricValues[metricName] = numericValue;
                }
            }

            // Add specific metrics if available
            if (WarpDivergence.HasValue)
            {
                kernelMetrics.MetricValues["branch_efficiency"] = WarpDivergence.Value;
            }

            if (MemoryCoalescingEfficiency.HasValue)
            {
                kernelMetrics.MetricValues["gld_efficiency"] = MemoryCoalescingEfficiency.Value;
            }

            if (Occupancy.HasValue)
            {
                kernelMetrics.MetricValues["achieved_occupancy"] = Occupancy.Value;
            }

            return kernelMetrics;
        }
    }
}
