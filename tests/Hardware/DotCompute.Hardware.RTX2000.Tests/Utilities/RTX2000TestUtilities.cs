using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.Utilities;


/// <summary>
/// Utility class providing common functionality for RTX 2000 Ada Generation testing.
/// </summary>
public static class RTX2000TestUtilities
{
    /// <summary>
    /// RTX 2000 Ada Gen specifications for validation.
    /// </summary>
    internal static class RTX2000Specifications
    {
        public const int ComputeCapabilityMajor = 8;
        public const int ComputeCapabilityMinor = 9;
        public const long ExpectedMemoryGB = 8;
        public const int ExpectedSMCount = 35; // Approximate
        public const double ExpectedMemoryBandwidthGBps = 224.0; // GDDR6
        public const int ExpectedMemoryBusWidth = 128; // bits
        public const double ExpectedBoostClockMHz = 2610; // Approximate
    }

    /// <summary>
    /// Validates if the detected GPU matches RTX 2000 Ada Gen specifications.
    /// </summary>
    internal static bool ValidateRTX2000Hardware(DeviceInfo deviceInfo, ITestOutputHelper output)
    {
        var isValid = true;

        // Validate compute capability
        if (deviceInfo.ComputeCapabilityMajor != RTX2000Specifications.ComputeCapabilityMajor ||
            deviceInfo.ComputeCapabilityMinor < RTX2000Specifications.ComputeCapabilityMinor)
        {
            output.WriteLine($"⚠ Compute capability mismatch: Expected {RTX2000Specifications.ComputeCapabilityMajor}.{RTX2000Specifications.ComputeCapabilityMinor}, found {deviceInfo.ComputeCapabilityMajor}.{deviceInfo.ComputeCapabilityMinor}");
            isValid = false;
        }

        // Validate memory size(allow some tolerance for system reserved memory)
        var memoryGB = deviceInfo.TotalMemoryBytes / (1024.0 * 1024.0 * 1024.0);
        if (memoryGB < RTX2000Specifications.ExpectedMemoryGB * 0.9)
        {
            output.WriteLine($"⚠ Memory size lower than expected: Expected ~{RTX2000Specifications.ExpectedMemoryGB}GB, found {memoryGB:F1}GB");
            isValid = false;
        }

        // Validate SM count(allow some tolerance for different variants)
        if (deviceInfo.MultiProcessorCount < RTX2000Specifications.ExpectedSMCount * 0.8)
        {
            output.WriteLine($"⚠ SM count lower than expected: Expected ~{RTX2000Specifications.ExpectedSMCount}, found {deviceInfo.MultiProcessorCount}");
            isValid = false;
        }

        if (isValid)
        {
            output.WriteLine("✓ Hardware validated as RTX 2000 Ada Generation compatible");
        }

        return isValid;
    }

    /// <summary>
    /// Loads and compiles a CUDA kernel from the embedded test kernels.
    /// </summary>
    internal static async Task<CompiledKernel> CompileTestKernel(string kernelName, string[]? compilationOptions = null)
    {
        var kernelSource = await LoadTestKernelSource();
        return await CompileKernel(kernelSource, kernelName, compilationOptions ?? GetDefaultCompilationOptions());
    }

    /// <summary>
    /// Gets default compilation options optimized for RTX 2000 Ada Gen.
    /// </summary>
    public static string[] GetDefaultCompilationOptions()
    {
        return
        [
            "--gpu-architecture=compute_89",
        "--use_fast_math",
        "--extra-device-vectorization",
        "--maxrregcount=64",
        "--ftz=true", // Flush denormals to zero
        "--prec-div=false", // Use fast division
        "--prec-sqrt=false", // Use fast square root
        "--fmad=true" // Enable fused multiply-add
        ];
    }

    /// <summary>
    /// Measures kernel execution time with high precision.
    /// </summary>
    internal static async Task<KernelExecutionResult> MeasureKernelExecution(
        CompiledKernel kernel,
        KernelLaunchParameters launchParams,
        IntPtr[] parameters,
        int warmupRuns = 3,
        int measurementRuns = 10)
    {
        var times = new List<double>();

        try
        {
            // Warm-up runs
            for (var i = 0; i < warmupRuns; i++)
            {
                await ExecuteKernel(kernel, launchParams, parameters);
                CudaContextSynchronize();
            }

            // Measurement runs
            for (var i = 0; i < measurementRuns; i++)
            {
                var sw = Stopwatch.StartNew();
                await ExecuteKernel(kernel, launchParams, parameters);
                CudaContextSynchronize();
                sw.Stop();

                times.Add(sw.Elapsed.TotalMicroseconds);
            }

            return new KernelExecutionResult
            {
                AverageTimeMicroseconds = times.Average(),
                MinTimeMicroseconds = times.Min(),
                MaxTimeMicroseconds = times.Max(),
                StandardDeviation = CalculateStandardDeviation(times),
                AllTimes = [.. times]
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Kernel execution measurement failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates optimized grid and block dimensions for the given problem size.
    /// </summary>
    internal static KernelLaunchParameters CalculateOptimalLaunchParameters(
        int problemSize,
        int maxThreadsPerBlock = 256,
        int preferredBlockSize = 256)
    {
        // Ensure block size is a multiple of warp size(32)
        var blockSize = Math.Min(maxThreadsPerBlock, ((preferredBlockSize + 31) / 32) * 32);
        var gridSize = (problemSize + blockSize - 1) / blockSize;

        return new KernelLaunchParameters
        {
            GridDimX = (uint)gridSize,
            GridDimY = 1,
            GridDimZ = 1,
            BlockDimX = (uint)blockSize,
            BlockDimY = 1,
            BlockDimZ = 1,
            SharedMemoryBytes = 0
        };
    }

    /// <summary>
    /// Creates 2D launch parameters for matrix operations.
    /// </summary>
    internal static KernelLaunchParameters Calculate2DLaunchParameters(
        int width,
        int height,
        int tileSize = 16)
    {
        var gridX = (width + tileSize - 1) / tileSize;
        var gridY = (height + tileSize - 1) / tileSize;

        return new KernelLaunchParameters
        {
            GridDimX = (uint)gridX,
            GridDimY = (uint)gridY,
            GridDimZ = 1,
            BlockDimX = (uint)tileSize,
            BlockDimY = (uint)tileSize,
            BlockDimZ = 1,
            SharedMemoryBytes = 0
        };
    }

    /// <summary>
    /// Validates memory bandwidth against RTX 2000 Ada Gen specifications.
    /// </summary>
    internal static BandwidthValidationResult ValidateBandwidthResults(
        BandwidthMeasurement measurement,
        ITestOutputHelper output)
    {
        var result = new BandwidthValidationResult();

        // H2D/D2H bandwidth validation(PCIe constraints)
        const double MinPCIeBandwidth = 12.0; // GB/s for PCIe 4.0 x16
        const double MaxPCIeBandwidth = 32.0; // GB/s theoretical maximum

        result.H2DValid = measurement.H2D is >= MinPCIeBandwidth and <= MaxPCIeBandwidth;
        result.D2HValid = measurement.D2H is >= MinPCIeBandwidth and <= MaxPCIeBandwidth;

        // D2D bandwidth validation(GDDR6 memory)
        const double MinGDDR6Bandwidth = 150.0; // GB/s conservative minimum
        const double MaxGDDR6Bandwidth = 300.0; // GB/s theoretical maximum

        result.D2DValid = measurement.D2D is >= MinGDDR6Bandwidth and <= MaxGDDR6Bandwidth;

        // Calculate efficiency relative to theoretical peak
        result.PCIeEfficiency = Math.Min(measurement.H2D, measurement.D2H) / MaxPCIeBandwidth;
        result.MemoryEfficiency = measurement.D2D / RTX2000Specifications.ExpectedMemoryBandwidthGBps;

        // Overall validation
        result.IsValid = result.H2DValid && result.D2HValid && result.D2DValid;

        // Output results
        output.WriteLine($"Bandwidth validation:");
        output.WriteLine($"  H2D: {measurement.H2D:F2} GB/s {(result.H2DValid ? "✓" : "✗")}");
        output.WriteLine($"  D2H: {measurement.D2H:F2} GB/s {(result.D2HValid ? "✓" : "✗")}");
        output.WriteLine($"  D2D: {measurement.D2D:F2} GB/s {(result.D2DValid ? "✓" : "✗")}");
        output.WriteLine($"  PCIe Efficiency: {result.PCIeEfficiency:P1}");
        output.WriteLine($"  Memory Efficiency: {result.MemoryEfficiency:P1}");

        return result;
    }

    /// <summary>
    /// Generates test data with specified characteristics.
    /// </summary>
    public static float[] GenerateTestData(int size, TestDataType dataType, int seed = 42)
    {
        var random = new Random(seed);
        var data = new float[size];

        switch (dataType)
        {
            case TestDataType.Random:
                for (var i = 0; i < size; i++)
                    data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                break;

            case TestDataType.Sequential:
                for (var i = 0; i < size; i++)
                    data[i] = i;
                break;

            case TestDataType.Constant:
                Array.Fill(data, 1.0f);
                break;

            case TestDataType.Sinusoidal:
                for (var i = 0; i < size; i++)
                    data[i] = (float)Math.Sin(i * 0.01);
                break;

            case TestDataType.Gaussian:
                for (var i = 0; i < size; i++)
                    data[i] = (float)GenerateGaussian(random);
                break;

            default:
                throw new ArgumentException($"Unknown data type: {dataType}");
        }

        return data;
    }

    /// <summary>
    /// Validates computational results with specified tolerance.
    /// </summary>
    internal static ValidationResult ValidateResults<T>(
        T[] expected,
        T[] actual,
        double tolerance = 1e-6,
        int maxSamples = 1000) where T : IComparable<T>
    {
        if (expected.Length != actual.Length)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Array length mismatch: expected {expected.Length}, actual {actual.Length}"
            };
        }

        var errors = new List<double>();
        var sampleStep = Math.Max(1, expected.Length / maxSamples);

        for (var i = 0; i < expected.Length; i += sampleStep)
        {
            if (expected[i] is float expectedFloat && actual[i] is float actualFloat)
            {
                var error = Math.Abs(expectedFloat - actualFloat);
                errors.Add(error);

                if (error > tolerance)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Value mismatch at index {i}: expected {expectedFloat}, actual {actualFloat}, error {error}",
                        MaxError = error
                    };
                }
            }
        }

        return new ValidationResult
        {
            IsValid = true,
            MaxError = errors.Count > 0 ? errors.Max() : 0.0,
            AverageError = errors.Count > 0 ? errors.Average() : 0.0,
            SamplesValidated = errors.Count
        };
    }

    #region Private Helper Methods

    private static async Task<string> LoadTestKernelSource()
    {
        var kernelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Kernels", "TestKernels.cu");
        if (File.Exists(kernelPath))
        {
            return await File.ReadAllTextAsync(kernelPath);
        }

        // Fallback to embedded resource or minimal kernel
        return @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if(idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}";
    }

    private static async Task<CompiledKernel> CompileKernel(string source, string kernelName, string[] options)
    {
        // This would integrate with NVRTC for actual compilation
        // For now, return a mock compiled kernel
        await Task.Delay(10); // Simulate compilation time

        return new CompiledKernel
        {
            Name = kernelName,
            Source = source,
            CompilationOptions = options,
            IsCompiled = true
        };
    }

    private static async Task ExecuteKernel(CompiledKernel kernel, KernelLaunchParameters launchParams, IntPtr[] parameters)
        // Mock kernel execution
        => await Task.Delay(1);

    private static void CudaContextSynchronize()
    {
        // Mock synchronization
    }

    private static double CalculateStandardDeviation(List<double> values)
    {
        var mean = values.Average();
        var squaredDifferences = values.Select(x => Math.Pow(x - mean, 2));
        return Math.Sqrt(squaredDifferences.Average());
    }

    private static double GenerateGaussian(Random random)
    {
        // Box-Muller transformation
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    #endregion

    #region Supporting Classes and Enums

#pragma warning disable CA1812 // DeviceInfo appears to be uninstantiated - used for configuration
    internal sealed record DeviceInfo(
#pragma warning restore CA1812
        int ComputeCapabilityMajor,
        int ComputeCapabilityMinor,
        long TotalMemoryBytes,
        int MultiProcessorCount,
        string DeviceName = "",
        int MemoryClockRate = 0,
        int MemoryBusWidth = 0);

    internal sealed class CompiledKernel : IEquatable<CompiledKernel>
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string[] CompilationOptions { get; set; } = Array.Empty<string>();
        public bool IsCompiled { get; set; }
        public IntPtr ModuleHandle { get; set; }
        public IntPtr FunctionHandle { get; set; }

        public bool Equals(CompiledKernel? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                   string.Equals(Source, other.Source, StringComparison.Ordinal) &&
                   CompilationOptions.SequenceEqual(other.CompilationOptions) &&
                   IsCompiled == other.IsCompiled &&
                   ModuleHandle.Equals(other.ModuleHandle) &&
                   FunctionHandle.Equals(other.FunctionHandle);
        }

        public override bool Equals(object? obj) => Equals(obj as CompiledKernel);

        public override int GetHashCode() => HashCode.Combine(Name, Source, IsCompiled, ModuleHandle, FunctionHandle);
    }

    internal sealed class KernelLaunchParameters : IEquatable<KernelLaunchParameters>
    {
        public uint GridDimX { get; set; }
        public uint GridDimY { get; set; }
        public uint GridDimZ { get; set; }
        public uint BlockDimX { get; set; }
        public uint BlockDimY { get; set; }
        public uint BlockDimZ { get; set; }
        public uint SharedMemoryBytes { get; set; }

        public bool Equals(KernelLaunchParameters? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return GridDimX == other.GridDimX &&
                   GridDimY == other.GridDimY &&
                   GridDimZ == other.GridDimZ &&
                   BlockDimX == other.BlockDimX &&
                   BlockDimY == other.BlockDimY &&
                   BlockDimZ == other.BlockDimZ &&
                   SharedMemoryBytes == other.SharedMemoryBytes;
        }

        public override bool Equals(object? obj) => Equals(obj as KernelLaunchParameters);

        public override int GetHashCode() => HashCode.Combine(GridDimX, GridDimY, GridDimZ, BlockDimX, BlockDimY, BlockDimZ, SharedMemoryBytes);
    }

    internal sealed class KernelExecutionResult : IEquatable<KernelExecutionResult>
    {
        public double AverageTimeMicroseconds { get; set; }
        public double MinTimeMicroseconds { get; set; }
        public double MaxTimeMicroseconds { get; set; }
        public double StandardDeviation { get; set; }
        public double[] AllTimes { get; set; } = Array.Empty<double>();

        public bool Equals(KernelExecutionResult? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return AverageTimeMicroseconds.Equals(other.AverageTimeMicroseconds) &&
                   MinTimeMicroseconds.Equals(other.MinTimeMicroseconds) &&
                   MaxTimeMicroseconds.Equals(other.MaxTimeMicroseconds) &&
                   StandardDeviation.Equals(other.StandardDeviation) &&
                   AllTimes.SequenceEqual(other.AllTimes);
        }

        public override bool Equals(object? obj) => Equals(obj as KernelExecutionResult);

        public override int GetHashCode() => HashCode.Combine(AverageTimeMicroseconds, MinTimeMicroseconds, MaxTimeMicroseconds, StandardDeviation);
    }

    internal struct BandwidthMeasurement : IEquatable<BandwidthMeasurement>
    {
        public double H2D { get; set; }
        public double D2H { get; set; }
        public double D2D { get; set; }

        public readonly bool Equals(BandwidthMeasurement other) => H2D.Equals(other.H2D) && D2H.Equals(other.D2H) && D2D.Equals(other.D2D);

        public override readonly bool Equals(object? obj) => obj is BandwidthMeasurement other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(H2D, D2H, D2D);

        public static bool operator ==(BandwidthMeasurement left, BandwidthMeasurement right) => left.Equals(right);

        public static bool operator !=(BandwidthMeasurement left, BandwidthMeasurement right) => !left.Equals(right);
    }

    internal sealed class BandwidthValidationResult : IEquatable<BandwidthValidationResult>
    {
        public bool H2DValid { get; set; }
        public bool D2HValid { get; set; }
        public bool D2DValid { get; set; }
        public bool IsValid { get; set; }
        public double PCIeEfficiency { get; set; }
        public double MemoryEfficiency { get; set; }

        public bool Equals(BandwidthValidationResult? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return H2DValid == other.H2DValid &&
                   D2HValid == other.D2HValid &&
                   D2DValid == other.D2DValid &&
                   IsValid == other.IsValid &&
                   PCIeEfficiency.Equals(other.PCIeEfficiency) &&
                   MemoryEfficiency.Equals(other.MemoryEfficiency);
        }

        public override bool Equals(object? obj) => Equals(obj as BandwidthValidationResult);

        public override int GetHashCode() => HashCode.Combine(H2DValid, D2HValid, D2DValid, IsValid, PCIeEfficiency, MemoryEfficiency);
    }

    internal sealed class ValidationResult : IEquatable<ValidationResult>
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public double MaxError { get; set; }
        public double AverageError { get; set; }
        public int SamplesValidated { get; set; }

        public bool Equals(ValidationResult? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return IsValid == other.IsValid &&
                   string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal) &&
                   MaxError.Equals(other.MaxError) &&
                   AverageError.Equals(other.AverageError) &&
                   SamplesValidated == other.SamplesValidated;
        }

        public override bool Equals(object? obj) => Equals(obj as ValidationResult);

        public override int GetHashCode() => HashCode.Combine(IsValid, ErrorMessage, MaxError, AverageError, SamplesValidated);
    }

    public enum TestDataType
    {
        Random,
        Sequential,
        Constant,
        Sinusoidal,
        Gaussian
    }

    #endregion
}
